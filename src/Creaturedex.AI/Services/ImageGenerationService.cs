using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public class ImageGenerationService(
    HttpClient httpClient,
    AIConfig aiConfig,
    ILogger<ImageGenerationService> logger)
{
    // Base quality prefix — tells RealVisXL V5.0 to produce its best photorealistic output
    private const string QualityPrefix =
        "RAW photo, masterpiece, best quality, ultra high res, photorealistic, " +
        "8k uhd, dslr, high quality, film grain";

    // Photography style suffix — consistent look across all animal images
    private const string StyleSuffix =
        "professional wildlife photography, National Geographic, " +
        "tack sharp focus, natural ambient lighting, deep depth of field, everything in focus, " +
        "shot on Canon EOS R5, 200mm telephoto lens, f8 aperture";

    // Comprehensive negative prompt tuned for RealVisXL V5.0
    private const string NegativePrompt =
        // Anti-illustration
        "cartoon, illustration, painting, drawing, anime, 3d render, CGI, digital art, sketch, " +
        // Anti-artifact
        "deformed, ugly, disfigured, bad anatomy, extra limbs, mutated, malformed, fused fingers, " +
        "too many fingers, long neck, extra tails, extra ears, duplicate, clone, " +
        // Anti-text/overlay
        "text, watermark, signature, logo, caption, border, frame, title, label, stamp, " +
        // Anti-quality-issues
        "blurry, out of focus, motion blur, noisy, grainy, pixelated, jpeg artifacts, " +
        "overexposed, underexposed, oversaturated, HDR, overprocessed, " +
        // Anti-human/unnatural
        "human, person, people, hands, fingers, man-made objects, cage, enclosure, zoo, " +
        // Anti-composition-issues
        "cropped, cut off, truncated, collage, split image, multiple views, tiled, " +
        // Anti-blur
        "bokeh, depth of field, blurred background, soft focus, vignette, black border";

    public async Task<ImageGenerationResult?> GenerateWithCustomPromptAsync(
        string prompt, string fileName, string? negativePrompt = null, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Generating test image: {FileName}", fileName);

            var response = await SendGenerateRequestAsync(prompt, negativePrompt ?? NegativePrompt, ct);
            if (response is null) return null;

            var relativeUrl = await SaveImageAsync(response.ImageBase64!, fileName, ct);
            return new ImageGenerationResult(relativeUrl, prompt, negativePrompt ?? NegativePrompt, response.Seed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate test image: {Message}", ex.Message);
            throw; // Let the controller handle it with the real error
        }
    }

    public async Task<string?> GenerateAnimalImageAsync(
        string animalName, string slug, string? scientificName, string summary,
        string? description, string? habitat, string? sizeInfo,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildPrompt(animalName, scientificName, summary, description, habitat, sizeInfo);
            logger.LogInformation("Generating image for {AnimalName} via Stable Diffusion", animalName);

            var response = await SendGenerateRequestAsync(prompt, NegativePrompt, ct);
            if (response is null)
            {
                logger.LogWarning("Stable Diffusion returned null image for {AnimalName}", animalName);
                return null;
            }

            var fileName = $"{slug}.png";
            var relativeUrl = await SaveImageAsync(response.ImageBase64!, fileName, ct);
            logger.LogInformation("Saved image for {AnimalName} to {Path} (seed: {Seed})", animalName, relativeUrl, response.Seed);

            return relativeUrl;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate image for {AnimalName}", animalName);
            return null;
        }
    }

    /// <summary>
    /// Builds the full positive prompt for an animal using all available profile data.
    /// Format: [quality prefix], [species identity], [physical appearance], [habitat/environment], [style suffix]
    /// </summary>
    public static string BuildPrompt(
        string animalName, string? scientificName, string summary,
        string? description = null, string? habitat = null, string? sizeInfo = null)
    {
        // Species identity — be as specific as possible about exactly what animal this is
        var species = !string.IsNullOrWhiteSpace(scientificName)
            ? $"a {animalName} ({scientificName})"
            : $"a {animalName}";

        // Physical appearance — extract from description if available
        var appearance = ExtractAppearanceCues(animalName, description, sizeInfo);

        // Environment context
        var environment = !string.IsNullOrWhiteSpace(habitat)
            ? $"in its natural habitat of {habitat.TrimEnd('.')}"
            : "in its natural wild habitat";

        return $"{QualityPrefix}, {species}, {appearance}, {environment}, " +
               $"full body visible, single animal centered in frame, {StyleSuffix}";
    }

    /// <summary>
    /// Pulls physical descriptors from the LLM-generated description to make the image prompt
    /// as breed/species-accurate as possible.
    /// </summary>
    private static string ExtractAppearanceCues(string animalName, string? description, string? sizeInfo)
    {
        var cues = new List<string>();

        if (!string.IsNullOrWhiteSpace(sizeInfo))
            cues.Add(sizeInfo.TrimEnd('.'));

        // Pull the first sentence or two from the description — this typically contains
        // the physical appearance details (colour, markings, build, coat type)
        if (!string.IsNullOrWhiteSpace(description))
        {
            var sentences = description.Split(new[] { ". ", ".\n" }, StringSplitOptions.RemoveEmptyEntries);
            // Take up to 2 sentences that likely describe appearance (usually the opening)
            var appearanceSentences = sentences
                .Take(3)
                .Where(s => ContainsAppearanceKeyword(s))
                .Take(2)
                .Select(s => s.Trim().TrimEnd('.'));

            cues.AddRange(appearanceSentences);
        }

        return cues.Count > 0
            ? string.Join(", ", cues)
            : $"detailed accurate depiction of a {animalName}";
    }

    private static bool ContainsAppearanceKeyword(string sentence)
    {
        var keywords = new[]
        {
            "colour", "color", "fur", "coat", "feather", "scale", "skin", "stripe",
            "spot", "marking", "pattern", "build", "tall", "long", "short", "slender",
            "stocky", "muscular", "weight", "wing", "tail", "snout", "mane", "horn",
            "tusk", "beak", "crest", "eye", "leg", "paw", "claw", "shell", "fin",
            "thick", "thin", "bushy", "sleek", "fluffy", "dense", "coarse",
            "reddish", "brown", "black", "white", "grey", "gray", "golden", "orange",
            "blue", "green", "yellow", "silver", "dark", "light", "pale", "bright"
        };
        var lower = sentence.ToLowerInvariant();
        return keywords.Any(k => lower.Contains(k));
    }

    /// <summary>Returns the current negative prompt template for preview.</summary>
    public static string GetNegativePrompt() => NegativePrompt;

    private async Task<GenerateImageResponse?> SendGenerateRequestAsync(
        string prompt, string negativePrompt, CancellationToken ct)
    {
        var request = new GenerateImageRequest
        {
            Prompt = prompt,
            NegativePrompt = negativePrompt,
            Width = 1216,
            Height = 832,
            NumInferenceSteps = 50,
            GuidanceScale = 4.0f
        };

        var jsonContent = System.Net.Http.Json.JsonContent.Create(request);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{aiConfig.StableDiffusionEndpoint}/generate")
        {
            Content = jsonContent
        };

        var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        // Read full response as string first to handle large base64 payloads
        var json = await response.Content.ReadAsStringAsync(ct);
        var result = System.Text.Json.JsonSerializer.Deserialize<GenerateImageResponse>(json);
        return result?.ImageBase64 is null ? null : result;
    }

    private async Task<string> SaveImageAsync(string base64, string fileName, CancellationToken ct)
    {
        var imageBytes = Convert.FromBase64String(base64);
        var directory = Path.Combine(AppContext.BaseDirectory, aiConfig.ImageStoragePath);
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        return $"/images/animals/{fileName}";
    }

    private class GenerateImageRequest
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("negative_prompt")]
        public string NegativePrompt { get; set; } = string.Empty;

        [JsonPropertyName("width")]
        public int Width { get; set; } = 1216;

        [JsonPropertyName("height")]
        public int Height { get; set; } = 832;

        [JsonPropertyName("num_inference_steps")]
        public int NumInferenceSteps { get; set; } = 30;

        [JsonPropertyName("guidance_scale")]
        public float GuidanceScale { get; set; } = 7.5f;

        [JsonPropertyName("seed")]
        public int? Seed { get; set; }
    }

    private class GenerateImageResponse
    {
        [JsonPropertyName("image_base64")]
        public string? ImageBase64 { get; set; }

        [JsonPropertyName("seed")]
        public long Seed { get; set; }
    }

    public record ImageGenerationResult(string ImageUrl, string PromptUsed, string NegativePromptUsed, long Seed);
}
