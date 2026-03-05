using System.Text.Json;
using Creaturedex.Core.Entities;
using Creaturedex.Shared.Responses;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public class ContentReviewService(AIService aiService, WikipediaService wikipediaService, ILogger<ContentReviewService> logger)
{
    private const string ReviewPrompt = """
        You are a content reviewer for an animal encyclopedia called Creaturedex.
        The audience is teenagers and adults without a scientific background.

        Review the following animal profile and suggest improvements. For each suggestion:
        - Identify the field name using EXACTLY one of these camelCase keys: "commonName", "scientificName", "summary", "description", "habitat", "diet", "lifespan", "sizeInfo", "behaviour", "nativeRegion", "conservationStatus", "funFacts"
        - Set severity to "warning" for factual errors or serious issues, "info" for style/completeness improvements
        - Provide a brief message explaining WHY the change is needed
        - Provide the current value of the field
        - Provide your suggested replacement text for that field

        Check for:
        1. Wikipedia accuracy — compare ALL verifiable facts (taxonomy, conservation status, habitat, diet, lifespan, native region) against the Wikipedia reference material provided below the profile. Flag any discrepancies as severity "warning". If no Wikipedia reference is provided, skip this check.
        2. Suspicious claims — flag any fun facts or statements that cite specific dates, names, statistics, or historical events that seem unusually specific or hard to verify. Set severity to "warning" and say "This claim should be verified by a human" in the message. Do NOT try to correct specific dates/names/events yourself — you may hallucinate different but equally wrong details. Instead, suggest removing the dubious fact and replacing it with a well-known, easily verifiable fact about the animal.
        3. Relevance — does every piece of content actually relate to THIS specific animal? Fun facts that are generic, about a different animal, or only loosely connected should be flagged and replaced with genuinely specific facts about THIS animal.
        4. Accessibility — are all acronyms and technical terms explained in plain English? Flag any unexplained acronyms (e.g. "DDT" should be "DDT (a harmful pesticide)").
        5. Completeness — are any sections thin or missing important info?
        6. Tone — is it accessible and age-appropriate? Not too technical, not condescending?
        7. Consistency — do fields contradict each other?
        8. Engagement — is it interesting and well-written?

        IMPORTANT rules for suggestions:
        - ALL suggestions must remain focused on the animal being reviewed. Never replace content about this animal with content about a different animal.
        - The suggestedValue MUST ALWAYS be the COMPLETE replacement text for the entire field. NEVER provide just a snippet, correction, or partial text. The suggestedValue will directly replace the current field value, so it must be complete and ready to use.
        - For long-form fields like "description", "summary", "habitat", "behaviour", "diet": the suggestedValue must be the FULL text of that field with your correction applied. If you are fixing one sentence in a 4-paragraph description, include ALL 4 paragraphs in suggestedValue with only the relevant sentence changed.
        - For "funFacts": the suggestedValue must be the COMPLETE JSON array of fun facts (not just the one being changed), since the field stores all fun facts together. If a fun fact is inaccurate or not specifically about this animal, replace it with a DIFFERENT interesting and accurate fun fact about the SAME animal.
        - The currentValue for funFacts should be the specific fact being changed (quote it in full), NOT a reference like "the second fun fact".
        - In the message field, always QUOTE the specific content you're referring to rather than using ordinal references like "the first", "the second". For example say: 'The fact "Peregrine Falcons were the first birds to fly in space" is inaccurate' rather than 'The second fun fact is inaccurate'.
        - When replacing a fun fact, the replacement must NOT duplicate or overlap with any of the OTHER existing fun facts. Check all existing facts before suggesting a replacement.
        - Keep the same number of fun facts unless removing one that cannot be replaced.

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
        // Fetch Wikipedia reference for fact-checking
        var wikiArticle = await wikipediaService.GetAnimalArticleAsync(animal.CommonName, ct);
        var wikiReference = "";
        if (wikiArticle != null)
        {
            wikiReference = $"""

                === REFERENCE MATERIAL (from Wikipedia — use to verify facts) ===
                {wikipediaService.FormatAsReference(wikiArticle)}
                === END REFERENCE MATERIAL ===
                """;
            logger.LogInformation("Injected Wikipedia reference for review of {AnimalName}", animal.CommonName);
        }

        var profileText = $"""
            commonName: {animal.CommonName}
            scientificName: {animal.ScientificName ?? "N/A"}
            summary: {animal.Summary}
            description: {animal.Description}
            habitat: {animal.Habitat ?? "N/A"}
            diet: {animal.Diet ?? "N/A"}
            lifespan: {animal.Lifespan ?? "N/A"}
            sizeInfo: {animal.SizeInfo ?? "N/A"}
            behaviour: {animal.Behaviour ?? "N/A"}
            nativeRegion: {animal.NativeRegion ?? "N/A"}
            conservationStatus: {animal.ConservationStatus ?? "N/A"}
            funFacts: {animal.FunFacts ?? "N/A"}
            tags: {string.Join(", ", tags)}
            isPet: {animal.IsPet}
            {wikiReference}
            """;

        try
        {
            // Use a long independent timeout rather than the request cancellation token,
            // since the Ollama connection can be slow but we don't want client disconnects to cancel retries
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            string? response = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    response = await aiService.CompleteViaStreamAsync(ReviewPrompt, profileText, cts.Token);
                    break;
                }
                catch (Exception ex) when (attempt < 2 && (ex is TaskCanceledException || ex is HttpRequestException || ex is IOException))
                {
                    logger.LogWarning(ex, "Review attempt {Attempt} failed for {AnimalName}, retrying...", attempt + 1, animal.CommonName);
                    await Task.Delay(3000, CancellationToken.None);
                }
            }

            if (response == null)
                throw new Exception("All retry attempts failed");

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
                    CurrentValue = GetJsonValueAsString(s, "currentValue"),
                    SuggestedValue = GetJsonValueAsString(s, "suggestedValue")
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

    private static string GetJsonValueAsString(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var val))
            return "";
        return val.ValueKind switch
        {
            JsonValueKind.String => val.GetString() ?? "",
            JsonValueKind.Array or JsonValueKind.Object => val.GetRawText(),
            _ => val.GetRawText()
        };
    }
}
