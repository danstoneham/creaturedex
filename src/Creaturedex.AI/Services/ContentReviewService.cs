using System.Text.Json;
using Creaturedex.Core.Entities;
using Creaturedex.Shared.Responses;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public class ContentReviewService(AIService aiService, ILogger<ContentReviewService> logger)
{
    private const string ReviewPrompt = """
        You are a content reviewer for an animal encyclopedia called Creaturedex.
        The audience is teenagers and adults without a scientific background.

        Review the following animal profile and suggest improvements. For each suggestion:
        - Identify the field name (e.g. "summary", "description", "habitat", "diet", etc.)
        - Set severity to "warning" for factual errors or serious issues, "info" for style/completeness improvements
        - Provide a brief message explaining WHY the change is needed
        - Provide the current value of the field
        - Provide your suggested replacement text for that field

        Check for:
        1. Factual accuracy — are the facts correct?
        2. Completeness — are any sections thin or missing important info?
        3. Tone — is it accessible and age-appropriate? Not too technical, not condescending?
        4. Consistency — do fields contradict each other?
        5. Engagement — is it interesting and well-written?

        If the content is good and needs no changes, return an empty suggestions array.

        Respond with ONLY valid JSON matching this schema:
        {
          "suggestions": [
            {
              "field": "fieldName",
              "severity": "info" or "warning",
              "message": "why this change is needed",
              "currentValue": "the current text",
              "suggestedValue": "your improved text"
            }
          ]
        }

        Respond with ONLY the JSON, no markdown fences or extra text.
        """;

    public async Task<List<ReviewSuggestion>> ReviewAnimalAsync(Animal animal, List<string> tags, CancellationToken ct = default)
    {
        var profileText = $"""
            Common Name: {animal.CommonName}
            Scientific Name: {animal.ScientificName ?? "N/A"}
            Summary: {animal.Summary}
            Description: {animal.Description}
            Habitat: {animal.Habitat ?? "N/A"}
            Diet: {animal.Diet ?? "N/A"}
            Lifespan: {animal.Lifespan ?? "N/A"}
            Size Info: {animal.SizeInfo ?? "N/A"}
            Behaviour: {animal.Behaviour ?? "N/A"}
            Native Region: {animal.NativeRegion ?? "N/A"}
            Conservation Status: {animal.ConservationStatus ?? "N/A"}
            Fun Facts: {animal.FunFacts ?? "N/A"}
            Tags: {string.Join(", ", tags)}
            Is Pet: {animal.IsPet}
            """;

        try
        {
            var response = await aiService.CompleteAsync(ReviewPrompt, profileText, ct);

            // Strip markdown fences if present
            response = response.Trim();
            if (response.StartsWith("```")) response = response[response.IndexOf('\n')..];
            if (response.EndsWith("```")) response = response[..response.LastIndexOf("```")];
            response = response.Trim();

            var json = JsonDocument.Parse(response);
            var suggestions = json.RootElement.GetProperty("suggestions").EnumerateArray()
                .Select(s => new ReviewSuggestion
                {
                    Field = s.GetProperty("field").GetString() ?? "",
                    Severity = s.GetProperty("severity").GetString() ?? "info",
                    Message = s.GetProperty("message").GetString() ?? "",
                    CurrentValue = s.GetProperty("currentValue").GetString() ?? "",
                    SuggestedValue = s.GetProperty("suggestedValue").GetString() ?? ""
                })
                .ToList();

            return suggestions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to review animal: {AnimalName}", animal.CommonName);
            return [new ReviewSuggestion
            {
                Field = "general",
                Severity = "warning",
                Message = $"AI review failed: {ex.Message}",
                CurrentValue = "",
                SuggestedValue = ""
            }];
        }
    }
}
