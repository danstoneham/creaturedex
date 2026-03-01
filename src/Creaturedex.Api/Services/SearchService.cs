using Creaturedex.Data.Repositories;
using Creaturedex.Shared.Responses;

namespace Creaturedex.Api.Services;

public class SearchService(
    SearchRepository searchRepo,
    CategoryRepository categoryRepo,
    PetCareGuideRepository careRepo)
{
    public async Task<List<SearchResultDto>> SearchAsync(string query, string type = "text")
    {
        var results = await searchRepo.FullTextSearchAsync(query);
        var categories = (await categoryRepo.GetAllAsync()).ToDictionary(c => c.Id);
        var dtos = new List<SearchResultDto>();

        foreach (var (animal, score) in results)
        {
            var category = categories.GetValueOrDefault(animal.CategoryId);
            int? difficulty = null;
            if (animal.IsPet)
            {
                var care = await careRepo.GetByAnimalIdAsync(animal.Id);
                difficulty = care?.DifficultyRating;
            }

            dtos.Add(new SearchResultDto
            {
                Animal = new AnimalCardDto
                {
                    Id = animal.Id,
                    Slug = animal.Slug,
                    CommonName = animal.CommonName,
                    ScientificName = animal.ScientificName,
                    Summary = animal.Summary,
                    CategorySlug = category?.Slug ?? "",
                    CategoryName = category?.Name ?? "",
                    IsPet = animal.IsPet,
                    ImageUrl = animal.ImageUrl,
                    ConservationStatus = animal.ConservationStatus,
                    DifficultyRating = difficulty
                },
                RelevanceScore = score,
                Snippet = animal.Summary.Length > 200 ? animal.Summary[..200] + "..." : animal.Summary
            });
        }

        return dtos;
    }
}
