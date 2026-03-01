using System.Text.Json;
using Creaturedex.Data.Repositories;

namespace Creaturedex.AI.Services;

public class MatcherAIService(
    AIService aiService,
    AnimalRepository animalRepo,
    PetCareGuideRepository careRepo)
{
    public async Task<List<(Guid AnimalId, string Explanation, int MatchScore)>> GetRecommendationsAsync(
        string livingSpace, string experienceLevel, string timeAvailable,
        string budgetRange, bool hasChildren, bool hasOtherPets,
        List<string> preferences, CancellationToken ct = default)
    {
        // Get all pet animals
        var pets = (await animalRepo.BrowseAsync(null, true, null, 1, 500, "name")).ToList();
        if (pets.Count == 0) return [];

        // Build summaries for the prompt
        var animalSummaries = new List<string>();
        foreach (var pet in pets)
        {
            var care = await careRepo.GetByAnimalIdAsync(pet.Id);
            animalSummaries.Add($"- {pet.CommonName} (ID: {pet.Id}): {pet.Summary} " +
                $"Difficulty: {care?.DifficultyRating ?? 3}/5, " +
                $"Good with children: {care?.GoodWithChildren}, " +
                $"Good with other pets: {care?.GoodWithOtherPets}");
        }

        var systemPrompt = """
            You are a pet recommendation expert. Given a user's lifestyle and preferences,
            recommend the top 5 best-matching pets from the available animals.

            Respond with valid JSON array:
            [
              { "animalId": "guid", "explanation": "Why this pet is great for them", "matchScore": 85 }
            ]

            matchScore is 0-100. Only include the JSON array, no extra text.
            """;

        var userPrompt = $"""
            User's situation:
            - Living space: {livingSpace}
            - Experience level: {experienceLevel}
            - Daily time available: {timeAvailable}
            - Budget: {budgetRange}
            - Has children: {hasChildren}
            - Has other pets: {hasOtherPets}
            - Priorities: {string.Join(", ", preferences)}

            Available pets:
            {string.Join("\n", animalSummaries)}
            """;

        try
        {
            var response = await aiService.CompleteAsync(systemPrompt, userPrompt, ct);
            response = response.Trim();
            if (response.StartsWith("```")) response = response[response.IndexOf('\n')..];
            if (response.EndsWith("```")) response = response[..response.LastIndexOf("```")];
            response = response.Trim();

            var recommendations = JsonSerializer.Deserialize<List<MatcherRecommendationJson>>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return recommendations?.Select(r => (
                Guid.Parse(r.AnimalId),
                r.Explanation,
                r.MatchScore
            )).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private class MatcherRecommendationJson
    {
        public string AnimalId { get; set; } = "";
        public string Explanation { get; set; } = "";
        public int MatchScore { get; set; }
    }
}
