using Creaturedex.AI;
using Creaturedex.AI.Services;
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
    TagRepository tagRepo,
    ImageGenerationService imageService,
    WikipediaService wikipediaService,
    ContentReviewService reviewService,
    AIConfig aiConfig)
{
    public async Task<(List<AnimalCardDto> Animals, int TotalCount)> BrowseAsync(BrowseAnimalsRequest request)
    {
        Guid? categoryId = null;
        if (!string.IsNullOrEmpty(request.Category))
        {
            var category = await categoryRepo.GetBySlugAsync(request.Category);
            categoryId = category?.Id;
        }

        var animals = await animalRepo.BrowseAsync(categoryId, request.IsPet, request.Tag, request.Page, request.PageSize, request.SortBy, request.IncludeDrafts);
        var totalCount = await animalRepo.CountAsync(categoryId, request.IsPet, request.Tag, request.IncludeDrafts);

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
                DifficultyRating = difficulty,
                IsPublished = animal.IsPublished
            });
        }

        return (cards, totalCount);
    }

    public async Task<AnimalProfileResponse?> GetBySlugAsync(string slug, bool includeUnpublished = false)
    {
        var animal = includeUnpublished
            ? await animalRepo.GetBySlugIncludingUnpublishedAsync(slug)
            : await animalRepo.GetBySlugAsync(slug);
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

    public async Task<Animal?> UpdateAnimalAsync(Guid id, UpdateAnimalRequest request, string? reviewedBy)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null) return null;

        animal.CommonName = request.CommonName;
        animal.ScientificName = request.ScientificName;
        animal.Summary = request.Summary;
        animal.Description = request.Description;
        animal.CategoryId = request.CategoryId;
        animal.IsPet = request.IsPet;
        animal.ConservationStatus = request.ConservationStatus;
        animal.NativeRegion = request.NativeRegion;
        animal.Habitat = request.Habitat;
        animal.Diet = request.Diet;
        animal.Lifespan = request.Lifespan;
        animal.SizeInfo = request.SizeInfo;
        animal.Behaviour = request.Behaviour;
        animal.FunFacts = request.FunFacts;
        animal.ReviewedBy = reviewedBy;

        await animalRepo.UpdateAsync(animal);

        await tagRepo.DeleteByAnimalIdAsync(animal.Id);
        if (request.Tags.Count > 0)
        {
            var tags = request.Tags.Select(t => new AnimalTag { AnimalId = animal.Id, Tag = t }).ToList();
            await tagRepo.BulkInsertAsync(tags);
        }

        return animal;
    }

    public async Task<string?> UploadImageAsync(Guid id, Stream fileStream, string fileName, long fileLength)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null) return null;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var storedFileName = $"{animal.Slug}{ext}";
        var storagePath = Path.Combine(AppContext.BaseDirectory, aiConfig.ImageStoragePath);
        Directory.CreateDirectory(storagePath);
        var filePath = Path.Combine(storagePath, storedFileName);

        await using var output = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(output);

        var imageUrl = $"/images/animals/{storedFileName}";
        await animalRepo.UpdateImageUrlAsync(id, imageUrl);

        return imageUrl;
    }

    public async Task<Animal?> GetByIdAsync(Guid id) =>
        await animalRepo.GetByIdAsync(id);

    public async Task<IEnumerable<Animal>> GetUnreviewedAsync() =>
        await animalRepo.GetUnreviewedAsync();

    public async Task MarkReviewedAsync(Guid id) =>
        await animalRepo.MarkReviewedAsync(id);

    public async Task PublishAsync(Guid id) =>
        await animalRepo.PublishAsync(id);

    public async Task UnpublishAsync(Guid id) =>
        await animalRepo.UnpublishAsync(id);

    public async Task PublishAllAsync() =>
        await animalRepo.PublishAllAsync();

    public async Task<(string? ImageUrl, string? AnimalName)> GenerateImageAsync(Guid id, CancellationToken ct)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null) return (null, null);

        var imageUrl = await imageService.GenerateAnimalImageAsync(
            animal.CommonName, animal.Slug, animal.ScientificName,
            animal.Summary, animal.Description, animal.Habitat, animal.SizeInfo, ct);

        if (imageUrl != null)
            await animalRepo.UpdateImageUrlAsync(id, imageUrl);

        return (imageUrl, animal.CommonName);
    }

    public async Task<(string? ImageUrl, string? Source, string? License)> FetchWikipediaImageAsync(Guid id, CancellationToken ct)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null) return (null, null, null);

        var article = await wikipediaService.GetAnimalArticleAsync(animal.CommonName, ct);
        if (article?.ImageUrl == null) return (null, null, null);

        await animalRepo.UpdateImageUrlAsync(id, article.ImageUrl);
        return (article.ImageUrl, article.Url, article.ImageLicense);
    }

    public async Task<List<ReviewSuggestion>?> ReviewAnimalAsync(Guid id, CancellationToken ct)
    {
        var animal = await animalRepo.GetByIdAsync(id);
        if (animal == null) return null;

        var tags = (await tagRepo.GetByAnimalIdAsync(id)).Select(t => t.Tag).ToList();
        return await reviewService.ReviewAnimalAsync(animal, tags, ct);
    }

    public (string Prompt, string NegativePrompt)? PreviewPromptForAnimal(Animal animal)
    {
        var prompt = ImageGenerationService.BuildPrompt(
            animal.CommonName, animal.ScientificName, animal.Summary,
            animal.Description, animal.Habitat, animal.SizeInfo);
        var negativePrompt = ImageGenerationService.GetNegativePrompt();
        return (prompt, negativePrompt);
    }
}
