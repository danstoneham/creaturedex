namespace Creaturedex.AI.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;

public record InferredValue(int? Min, int? Max, string? StringValue, int Confidence);

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

    // 7. AI fallback for missing structured measurements
    // Only accepts values where the AI reports >= 98% confidence.
    public async Task<Dictionary<string, InferredValue>> InferMissingMeasurementsAsync(
        string commonName, string fullText, IReadOnlyList<string> missingFields,
        CancellationToken ct = default)
    {
        if (missingFields.Count == 0 || string.IsNullOrWhiteSpace(fullText))
            return [];

        var systemPrompt = """
            You are a precise data extraction tool for an animal encyclopedia.
            Given text about an animal, extract the requested measurements.

            Rules:
            - Use ONLY facts explicitly stated in the provided text
            - Do NOT invent, estimate, or guess any values
            - For each field, provide your confidence as a percentage (0-100)
            - Only report a value if you are >= 98% certain it is correct based on the text
            - Output as a JSON object where each key is a field name and the value is an object with "value" and "confidence"
            - For ranges, use "min" and "max" keys inside the value object
            - For single values, use "value" key
            - If the data is not clearly stated in the text, set confidence to 0 and value to null
            - Output ONLY the JSON object, nothing else

            Field types:
            - lifespanWildYears: integer range (min/max years in the wild)
            - lifespanCaptivityYears: integer range (min/max years in captivity)
            - gestationDays: integer range (min/max days, convert from months/weeks if needed: 1 month = 30 days, 1 week = 7 days)
            - litterSize: integer range (min/max offspring per birth, "single" = 1)
            - activityPattern: one of "diurnal", "nocturnal", "crepuscular", "cathemeral"
            - dietType: one of "herbivore", "carnivore", "omnivore", "insectivore", "piscivore", "frugivore"

            Example output:
            {"gestationDays": {"min": 480, "max": 480, "confidence": 99}, "litterSize": {"min": 1, "max": 1, "confidence": 98}}
            """;

        var fieldsStr = string.Join(", ", missingFields);
        var userPrompt = $"Animal: {commonName}\n\nExtract these fields: {fieldsStr}\n\nSource text:\n{fullText}";

        try
        {
            var response = (await aiService.CompleteAsync(systemPrompt, userPrompt, ct)).Trim();
            return ParseMeasurementResponse(response, missingFields);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI measurement inference failed for {AnimalName}", commonName);
            return [];
        }
    }

    private Dictionary<string, InferredValue> ParseMeasurementResponse(
        string response, IReadOnlyList<string> requestedFields)
    {
        var results = new Dictionary<string, InferredValue>();
        try
        {
            // Extract JSON from response (may have surrounding text)
            var start = response.IndexOf('{');
            var end = response.LastIndexOf('}');
            if (start < 0 || end <= start) return results;

            var json = response[start..(end + 1)];
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            foreach (var field in requestedFields)
            {
                if (!doc.TryGetProperty(field, out var fieldEl)) continue;

                var confidence = fieldEl.TryGetProperty("confidence", out var confEl)
                    ? confEl.GetInt32() : 0;

                if (confidence < 98)
                {
                    logger.LogDebug("AI inference for {Field}: confidence {Confidence}% < 98%, skipping",
                        field, confidence);
                    continue;
                }

                // Extract the value based on field type
                if (fieldEl.TryGetProperty("min", out var minEl) && fieldEl.TryGetProperty("max", out var maxEl))
                {
                    results[field] = new InferredValue(
                        minEl.ValueKind == JsonValueKind.Number ? minEl.GetInt32() : null,
                        maxEl.ValueKind == JsonValueKind.Number ? maxEl.GetInt32() : null,
                        null, confidence);
                }
                else if (fieldEl.TryGetProperty("value", out var valEl))
                {
                    var strVal = valEl.ValueKind == JsonValueKind.String ? valEl.GetString() : null;
                    var intVal = valEl.ValueKind == JsonValueKind.Number ? valEl.GetInt32() : (int?)null;
                    results[field] = new InferredValue(intVal, null, strVal, confidence);
                }

                logger.LogInformation("AI inference accepted for {Field}: confidence {Confidence}%",
                    field, confidence);
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse AI measurement response: {Response}", response);
        }

        return results;
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
