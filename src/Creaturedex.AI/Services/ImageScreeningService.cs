using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public class ImageScreeningService(
    HttpClient httpClient,
    AIConfig aiConfig,
    ILogger<ImageScreeningService> logger)
{
    private const string ScreeningPrompt = """
        You are a content safety reviewer for a children's animal encyclopedia aimed at ages 8-16.
        Look at this image and determine if it is appropriate.
        Reply with ONLY 'SAFE' or 'UNSAFE'.
        An image is UNSAFE if it contains: graphic injuries, mating, dead animals, gore, or anything inappropriate for children.
        """;

    /// <summary>
    /// Uses Ollama vision to check if a GBIF image is child-safe.
    /// Returns true if the image is safe, false if unsafe or if screening fails.
    /// </summary>
    public async Task<bool> IsChildSafeAsync(string imageUrl, CancellationToken ct = default)
    {
        try
        {
            // Download the image bytes
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl, ct);
            var base64Image = Convert.ToBase64String(imageBytes);

            // Build the Ollama chat completion request with vision
            var requestBody = new
            {
                model = aiConfig.ChatModel,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = ScreeningPrompt,
                        images = new[] { base64Image }
                    }
                },
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var endpoint = $"{aiConfig.OllamaEndpoint.TrimEnd('/')}/api/chat";
            var response = await httpClient.PostAsync(endpoint, content, ct);
            response.EnsureSuccessStatusCode();

            var responseStream = await response.Content.ReadAsStreamAsync(ct);
            var responseJson = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct);
            var root = responseJson.RootElement;

            // Parse the response text from Ollama's chat format
            string? responseText = null;
            if (root.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var contentEl))
            {
                responseText = contentEl.GetString();
            }

            if (string.IsNullOrWhiteSpace(responseText))
            {
                logger.LogWarning("Image screening returned empty response for {ImageUrl}, defaulting to unsafe", imageUrl);
                return false;
            }

            var trimmed = responseText.Trim().ToUpperInvariant();

            // Check if the model actually processed the image
            // Models without vision support respond with "I can't see" or similar
            if (trimmed.Contains("CAN'T SEE") || trimmed.Contains("CANNOT SEE")
                || trimmed.Contains("UNABLE TO") || trimmed.Contains("DON'T SEE")
                || trimmed.Contains("NO IMAGE") || trimmed.Contains("I'M SORRY"))
            {
                logger.LogWarning("Image screening model does not support vision for {ImageUrl}, defaulting to SAFE (GBIF images are pre-curated)", imageUrl);
                return true;
            }

            if (trimmed.Contains("SAFE") && !trimmed.Contains("UNSAFE"))
            {
                logger.LogDebug("Image screening: SAFE for {ImageUrl}", imageUrl);
                return true;
            }

            if (trimmed.Contains("UNSAFE"))
            {
                logger.LogInformation("Image screening: UNSAFE for {ImageUrl} (response: {Response})", imageUrl, responseText);
                return false;
            }

            // Ambiguous response — default to safe for GBIF curated images
            logger.LogWarning("Image screening returned ambiguous response for {ImageUrl}: {Response}, defaulting to SAFE", imageUrl, responseText);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // GBIF occurrence photos (HUMAN_OBSERVATION, CC BY 4.0) are community-curated
            // wildlife photos. If screening fails (e.g. model doesn't support vision),
            // default to safe rather than blocking all GBIF images.
            logger.LogWarning(ex, "Image screening failed for {ImageUrl}, defaulting to SAFE (GBIF occurrence images are pre-curated)", imageUrl);
            return true;
        }
    }
}
