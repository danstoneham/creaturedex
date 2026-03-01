using Creaturedex.Core.Entities;
using Creaturedex.Data.Repositories;
using Creaturedex.Shared.Requests;
using Creaturedex.Shared.Responses;

namespace Creaturedex.Api.Services;

public class AnimalService(
    AnimalRepository animalRepo,
    CategoryRepository categoryRepo,
    TaxonomyRepository taxonomyRepo,
    PetCareGuideRepository careRepo,
    CharacteristicRepository charRepo,
    TagRepository tagRepo)
{
    public async Task<(List<AnimalCardDto> Animals, int TotalCount)> BrowseAsync(BrowseAnimalsRequest request)
    {
        Guid? categoryId = null;
        if (!string.IsNullOrEmpty(request.Category))
        {
            var category = await categoryRepo.GetBySlugAsync(request.Category);
            categoryId = category?.Id;
        }

        var animals = await animalRepo.BrowseAsync(categoryId, request.IsPet, request.Tag, request.Page, request.PageSize, request.SortBy);
        var totalCount = await animalRepo.CountAsync(categoryId, request.IsPet, request.Tag);

        var categories = (await categoryRepo.GetAllAsync()).ToDictionary(c => c.Id);
        var cards = new List<AnimalCardDto>();

        foreach (var animal in animals)
        {
            var category = categories.GetValueOrDefault(animal.CategoryId);
            int? difficulty = null;
            if (animal.IsPet)
            {
                var care = await careRepo.GetByAnimalIdAsync(animal.Id);
                difficulty = care?.DifficultyRating;
            }

            cards.Add(new AnimalCardDto
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
            });
        }

        return (cards, totalCount);
    }

    public async Task<AnimalProfileResponse?> GetBySlugAsync(string slug)
    {
        var animal = await animalRepo.GetBySlugAsync(slug);
        if (animal == null) return null;

        var taxonomy = animal.TaxonomyId.HasValue
            ? await taxonomyRepo.GetByIdAsync(animal.TaxonomyId.Value)
            : null;
        var careGuide = animal.IsPet ? await careRepo.GetByAnimalIdAsync(animal.Id) : null;
        var characteristics = (await charRepo.GetByAnimalIdAsync(animal.Id)).ToList();
        var tags = (await tagRepo.GetByAnimalIdAsync(animal.Id)).Select(t => t.Tag).ToList();

        var categories = (await categoryRepo.GetAllAsync()).ToDictionary(c => c.Id);
        var category = categories.GetValueOrDefault(animal.CategoryId);

        return new AnimalProfileResponse
        {
            Animal = animal,
            Taxonomy = taxonomy,
            CareGuide = careGuide,
            Characteristics = characteristics,
            Tags = tags,
            CategoryName = category?.Name ?? "",
            CategorySlug = category?.Slug ?? "",
            IsReviewed = animal.ReviewedAt.HasValue
        };
    }

    public async Task<AnimalCardDto?> GetRandomAsync()
    {
        var animal = await animalRepo.GetRandomAsync();
        if (animal == null) return null;

        var categories = (await categoryRepo.GetAllAsync()).ToDictionary(c => c.Id);
        var category = categories.GetValueOrDefault(animal.CategoryId);
        int? difficulty = null;
        if (animal.IsPet)
        {
            var care = await careRepo.GetByAnimalIdAsync(animal.Id);
            difficulty = care?.DifficultyRating;
        }

        return new AnimalCardDto
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
        };
    }
}
