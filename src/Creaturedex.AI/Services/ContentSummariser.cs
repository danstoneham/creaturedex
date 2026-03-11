namespace Creaturedex.AI.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;

public class ContentSummariser(
    AIService aiService,
    ILogger<ContentSummariser> logger)
{
    // 1. Summarise intro (max 200 chars, one sentence hook for ages 8-16)
    public async Task<string> SummariseIntroAsync(string commonName, string introText, CancellationToken ct = default)
    {
        var systemPrompt = """
            You are writing a one-sentence hook for a children's animal encyclopedia (ages 8-16).
            Summarise the provided text in max 200 characters.
            Rules:
            - Use ONLY facts from the provided text
            - Do NOT invent any information
            - Be warm and enthusiastic
            - Output ONLY the summary sentence, nothing else
            """;
        var userPrompt = $"Animal: {commonName}\n\nSource text:\n{introText}";
        return (await aiService.CompleteAsync(systemPrompt, userPrompt, ct)).Trim();
    }

    // 2. Summarise description (2-3 paragraphs for ages 8-16)
    public async Task<string> SummariseDescriptionAsync(
        string commonName, string? introText, string? habitatText, string? dietText,
        string? behaviourText, CancellationToken ct = default)
    {
        var systemPrompt = """
            You are writing a 2-3 paragraph description for a children's animal encyclopedia (ages 8-16).
            Rules:
            - Use ONLY facts from the provided text sections
            - Do NOT invent measurements, dates, names, or statistics
            - Write in clear, engaging language for young readers
            - Cover: what the animal is, where it lives, what makes it special
            - Output ONLY the paragraphs, no headings or formatting
            """;
        var sections = new List<string> { $"Animal: {commonName}" };
        if (introText != null) sections.Add($"Overview:\n{introText}");
        if (habitatText != null) sections.Add($"Habitat:\n{habitatText}");
        if (dietText != null) sections.Add($"Diet:\n{dietText}");
        if (behaviourText != null) sections.Add($"Behaviour:\n{behaviourText}");
        return (await aiService.CompleteAsync(systemPrompt, string.Join("\n\n", sections), ct)).Trim();
    }

    // 3. Extract 3-5 fun facts (verifiable, JSON array)
    public async Task<List<string>> ExtractFunFactsAsync(
        string commonName, string fullWikipediaText, CancellationToken ct = default)
    {
        var systemPrompt = """
            Extract 3-5 fascinating, verifiable facts about this animal from the provided text.
            Rules:
            - Every fact MUST be directly stated in or clearly derivable from the source text
            - Do NOT invent any facts, dates, names, or statistics
            - Prefer biological/behavioural facts over historical anecdotes
            - Write each fact as a single interesting sentence suitable for ages 8-16
            - Output as a JSON array of strings, e.g. ["Fact 1", "Fact 2", "Fact 3"]
            - Output ONLY the JSON array, nothing else
            """;
        var userPrompt = $"Animal: {commonName}\n\nSource text:\n{fullWikipediaText}";
        var response = (await aiService.CompleteAsync(systemPrompt, userPrompt, ct)).Trim();
        return ParseJsonArray(response);
    }

    // 4. Match colours from fixed list (max 6)
    public async Task<List<string>> MatchColoursAsync(
        string commonName, string? appearanceText,
        IReadOnlyList<string> validColourCodes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(appearanceText)) return [];

        var systemPrompt = $"""
            Given the appearance description of an animal, pick the most relevant colours from this fixed list:
            {string.Join(", ", validColourCodes)}

            Rules:
            - Pick 2-6 colours that best describe the animal
            - Use ONLY codes from the list above
            - Output as a JSON array of strings, e.g. ["black", "orange", "white"]
            - Output ONLY the JSON array, nothing else
            """;
        var userPrompt = $"Animal: {commonName}\n\nAppearance:\n{appearanceText}";
        var response = (await aiService.CompleteAsync(systemPrompt, userPrompt, ct)).Trim();
        var parsed = ParseJsonArray(response);
        // Validate against valid codes
        return parsed.Where(c => validColourCodes.Contains(c)).Take(6).ToList();
    }

    // 5. Extract 3-5 distinguishing features
    public async Task<List<string>> ExtractDistinguishingFeaturesAsync(
        string commonName, string? appearanceText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(appearanceText)) return [];

        var systemPrompt = """
            Extract 3-5 distinguishing physical features of this animal from the provided text.
            Rules:
            - Each feature is one short sentence (max 100 characters)
            - Focus on what makes this animal visually distinctive
            - Use ONLY facts from the text
            - Output as a JSON array of strings
            - Output ONLY the JSON array, nothing else
            """;
        var userPrompt = $"Animal: {commonName}\n\nAppearance:\n{appearanceText}";
        var response = (await aiService.CompleteAsync(systemPrompt, userPrompt, ct)).Trim();
        return ParseJsonArray(response);
    }

    // 6. Summarise a section (1-3 sentences)
    public async Task<string?> SummariseSectionAsync(
        string commonName, string sectionName, string? sectionText,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sectionText)) return null;

        var systemPrompt = $"""
            Summarise this {sectionName} information about {commonName} in 1-3 sentences for ages 8-16.
            Rules:
            - Use ONLY facts from the provided text
            - Do NOT invent any information
            - Be concise and clear
            - Output ONLY the summary, nothing else
            """;
        return (await aiService.CompleteAsync(systemPrompt, sectionText, ct)).Trim();
    }

    // JSON array parser with fallback for malformed responses
    private List<string> ParseJsonArray(string response)
    {
        try
        {
            // Try parsing as JSON array
            var result = JsonSerializer.Deserialize<List<string>>(response);
            return result ?? [];
        }
        catch (JsonException)
        {
            // Try extracting JSON array from surrounding text
            var start = response.IndexOf('[');
            var end = response.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                try
                {
                    var jsonPart = response[start..(end + 1)];
                    return JsonSerializer.Deserialize<List<string>>(jsonPart) ?? [];
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to parse JSON array from AI response: {Response}", response);
                    return [];
                }
            }
            logger.LogWarning("No JSON array found in AI response: {Response}", response);
            return [];
        }
    }
}
