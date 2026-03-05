using System.Text.Json;
using Creaturedex.Core.Entities;
using Creaturedex.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public class DuplicateAnimalException(string animalName, string slug)
    : Exception($"{animalName} already exists")
{
    public string AnimalName { get; } = animalName;
    public string Slug { get; } = slug;
}

public class ContentGeneratorService(
    AIService aiService,
    EmbeddingService embeddingService,
    ImageGenerationService imageService,
    WikipediaService wikipediaService,
    AnimalRepository animalRepo,
    CategoryRepository categoryRepo,
    TaxonomyRepository taxonomyRepo,
    PetCareGuideRepository careRepo,
    CharacteristicRepository charRepo,
    TagRepository tagRepo,
    AIConfig aiConfig,
    ILogger<ContentGeneratorService> logger)
{
    private const string SystemPrompt = """
        You are an expert zoologist, veterinarian, and animal care writer creating content for an animal encyclopedia called Creaturedex.

        Your audience is teenagers and adults without a scientific background. Explain technical terms in plain English — always spell out acronyms on first use (e.g. "DDT (a pesticide called dichlorodiphenyltrichloroethane)"). Be warm, enthusiastic, and informative — never condescending.

        When uncertain about specific numbers, provide ranges rather than guessing. Flag genuine uncertainty rather than presenting speculation as fact.

        For fun facts: stick to well-known, easily verifiable facts. NEVER invent specific dates, names, or historical events. Prefer fascinating biological/behavioural facts over historical anecdotes.

        You MUST respond with valid JSON matching this exact schema:
        {
          "commonName": "string",
          "scientificName": "string or null",
          "slug": "lowercase-hyphenated-string",
          "summary": "One or two sentence hook — friendly, interesting, max 200 chars",
          "description": "Full 3-4 paragraph description in layman's terms",
          "categorySlug": "string matching one of: dogs, cats, small-mammals, reptiles, birds, fish, insects, farm, wild-mammals, ocean, primates",
          "isPet": true/false,
          "conservationStatus": "Least Concern | Near Threatened | Vulnerable | Endangered | Critically Endangered | Extinct in the Wild | Extinct | Data Deficient | null",
          "nativeRegion": "string",
          "habitat": "string describing natural habitat",
          "diet": "string describing diet",
          "lifespan": "string e.g. '10-15 years'",
          "sizeInfo": "string with metric and imperial",
          "behaviour": "string describing behaviour",
          "funFacts": ["array", "of", "3-5 fun facts"],
          "taxonomy": {
            "kingdom": "Animalia",
            "phylum": "string",
            "class": "string",
            "order": "string",
            "family": "string",
            "genus": "string",
            "species": "string",
            "subspecies": "string or null"
          },
          "characteristics": [
            { "name": "Weight", "value": "string" },
            { "name": "Length", "value": "string" }
          ],
          "tags": ["array", "of", "relevant", "tags"],
          "petCareGuide": null OR {
            "difficultyRating": 1-5,
            "costRangeMin": number,
            "costRangeMax": number,
            "costCurrency": "GBP",
            "spaceRequirement": "string",
            "timeCommitment": "string",
            "housing": "detailed string",
            "dietAsPet": "detailed string",
            "exercise": "string",
            "grooming": "string",
            "healthConcerns": "string",
            "training": "string",
            "goodWithChildren": true/false,
            "goodWithOtherPets": true/false,
            "temperament": "string",
            "legalConsiderations": "string"
          }
        }

        Include petCareGuide ONLY if isPet is true. Respond with ONLY the JSON, no markdown fences or extra text.
        """;

    public async Task<Guid?> GenerateAnimalAsync(string animalName, bool skipImage = false, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Generating content for: {AnimalName}", animalName);

            // Early duplicate check before calling AI
            var expectedSlug = animalName.ToLower().Replace(' ', '-');
            var earlyCheck = await animalRepo.GetBySlugIncludingUnpublishedAsync(expectedSlug);
            if (earlyCheck != null)
                throw new DuplicateAnimalException(earlyCheck.CommonName, earlyCheck.Slug);

            // Fetch Wikipedia reference material for factual grounding
            var wikiArticle = await wikipediaService.GetAnimalArticleAsync(animalName, ct);
            var userPrompt = $"Generate a complete profile for: {animalName}";
            if (wikiArticle != null)
            {
                var reference = wikipediaService.FormatAsReference(wikiArticle);
                userPrompt = $"""
                    {userPrompt}

                    === REFERENCE MATERIAL (from Wikipedia — use as factual ground truth) ===
                    {reference}
                    === END REFERENCE MATERIAL ===
                    Use the reference material above for verifiable facts (taxonomy, conservation status, habitat, diet, lifespan, etc.). Prefer reference data over your own knowledge when they conflict.
                    """;
                logger.LogInformation("Injected Wikipedia reference for {AnimalName} ({Url})", animalName, wikiArticle.Url);
            }
            else
            {
                logger.LogWarning("No Wikipedia article found for {AnimalName}, generating without reference", animalName);
            }

            var response = await aiService.CompleteAsync(SystemPrompt, userPrompt, ct);

            // Strip markdown fences if present
            response = response.Trim();
            if (response.StartsWith("```")) response = response[response.IndexOf('\n')..];
            if (response.EndsWith("```")) response = response[..response.LastIndexOf("```")];
            response = response.Trim();

            var json = JsonDocument.Parse(response);
            var root = json.RootElement;

            // Resolve category
            var categorySlug = root.GetProperty("categorySlug").GetString() ?? "wild-mammals";
            var category = await categoryRepo.GetBySlugAsync(categorySlug);
            if (category == null)
            {
                logger.LogWarning("Category {Slug} not found, falling back to wild-mammals", categorySlug);
                category = await categoryRepo.GetBySlugAsync("wild-mammals");
            }

            // Check for duplicate slug
            var slug = root.GetProperty("slug").GetString() ?? animalName.ToLower().Replace(' ', '-');
            var existing = await animalRepo.GetBySlugIncludingUnpublishedAsync(slug);
            if (existing != null)
                throw new DuplicateAnimalException(existing.CommonName, existing.Slug);

            // Create taxonomy
            var taxElement = root.GetProperty("taxonomy");
            var taxonomy = new Taxonomy
            {
                Kingdom = taxElement.GetProperty("kingdom").GetString() ?? "Animalia",
                Phylum = GetStringOrNull(taxElement, "phylum"),
                Class = GetStringOrNull(taxElement, "class"),
                TaxOrder = GetStringOrNull(taxElement, "order"),
                Family = GetStringOrNull(taxElement, "family"),
                Genus = GetStringOrNull(taxElement, "genus"),
                Species = GetStringOrNull(taxElement, "species"),
                Subspecies = GetStringOrNull(taxElement, "subspecies"),
            };
            var taxonomyId = await taxonomyRepo.CreateAsync(taxonomy);

            // Create animal
            var isPet = root.GetProperty("isPet").GetBoolean();
            var funFacts = root.GetProperty("funFacts").EnumerateArray()
                .Select(f => f.GetString()).Where(f => f != null).ToList();

            var animal = new Animal
            {
                Slug = slug,
                CommonName = root.GetProperty("commonName").GetString() ?? animalName,
                ScientificName = GetStringOrNull(root, "scientificName"),
                Summary = root.GetProperty("summary").GetString() ?? "",
                Description = root.GetProperty("description").GetString() ?? "",
                CategoryId = category!.Id,
                TaxonomyId = taxonomyId,
                IsPet = isPet,
                ConservationStatus = GetStringOrNull(root, "conservationStatus"),
                NativeRegion = GetStringOrNull(root, "nativeRegion"),
                Habitat = GetStringOrNull(root, "habitat"),
                Diet = GetStringOrNull(root, "diet"),
                Lifespan = GetStringOrNull(root, "lifespan"),
                SizeInfo = GetStringOrNull(root, "sizeInfo"),
                Behaviour = GetStringOrNull(root, "behaviour"),
                FunFacts = JsonSerializer.Serialize(funFacts),
                GeneratedAt = DateTime.UtcNow,
                IsPublished = false,
            };
            var animalId = await animalRepo.CreateAsync(animal);

            // Create pet care guide if applicable
            if (isPet && root.TryGetProperty("petCareGuide", out var careElement) && careElement.ValueKind != JsonValueKind.Null)
            {
                var guide = new PetCareGuide
                {
                    AnimalId = animalId,
                    DifficultyRating = careElement.GetProperty("difficultyRating").GetInt32(),
                    CostRangeMin = GetDecimalOrNull(careElement, "costRangeMin"),
                    CostRangeMax = GetDecimalOrNull(careElement, "costRangeMax"),
                    CostCurrency = GetStringOrNull(careElement, "costCurrency") ?? "GBP",
                    SpaceRequirement = GetStringOrNull(careElement, "spaceRequirement"),
                    TimeCommitment = GetStringOrNull(careElement, "timeCommitment"),
                    Housing = GetStringOrNull(careElement, "housing"),
                    DietAsPet = GetStringOrNull(careElement, "dietAsPet"),
                    Exercise = GetStringOrNull(careElement, "exercise"),
                    Grooming = GetStringOrNull(careElement, "grooming"),
                    HealthConcerns = GetStringOrNull(careElement, "healthConcerns"),
                    Training = GetStringOrNull(careElement, "training"),
                    GoodWithChildren = GetBoolOrNull(careElement, "goodWithChildren"),
                    GoodWithOtherPets = GetBoolOrNull(careElement, "goodWithOtherPets"),
                    Temperament = GetStringOrNull(careElement, "temperament"),
                    LegalConsiderations = GetStringOrNull(careElement, "legalConsiderations"),
                };
                await careRepo.CreateAsync(guide);
            }

            // Create characteristics
            if (root.TryGetProperty("characteristics", out var charsElement))
            {
                var chars = charsElement.EnumerateArray().Select((c, i) => new AnimalCharacteristic
                {
                    AnimalId = animalId,
                    CharacteristicName = c.GetProperty("name").GetString() ?? "",
                    CharacteristicValue = c.GetProperty("value").GetString() ?? "",
                    SortOrder = i
                }).ToList();
                await charRepo.BulkInsertAsync(chars);
            }

            // Create tags
            if (root.TryGetProperty("tags", out var tagsElement))
            {
                var tags = tagsElement.EnumerateArray()
                    .Select(t => new AnimalTag { AnimalId = animalId, Tag = t.GetString() ?? "" })
                    .Where(t => !string.IsNullOrEmpty(t.Tag))
                    .ToList();
                await tagRepo.BulkInsertAsync(tags);
            }

            // Generate embedding
            var embeddingText = $"{animal.CommonName} {animal.Summary} {animal.Description} {string.Join(" ", funFacts)}";
            await embeddingService.GenerateAndStoreAsync(animalId, embeddingText, aiConfig.EmbeddingModel);

            // Generate image via Stable Diffusion (only if enabled)
            if (!skipImage && aiConfig.AutoGenerateImages)
            {
                var imageUrl = await imageService.GenerateAnimalImageAsync(
                    animal.CommonName, animal.Slug, animal.ScientificName,
                    animal.Summary, animal.Description, animal.Habitat, animal.SizeInfo, ct);
                if (imageUrl != null)
                {
                    await animalRepo.UpdateImageUrlAsync(animalId, imageUrl);
                    logger.LogInformation("Generated image for {AnimalName}: {ImageUrl}", animalName, imageUrl);
                }
            }

            // Use Wikipedia image as fallback if no AI-generated image
            var currentAnimal = await animalRepo.GetByIdAsync(animalId);
            if (currentAnimal?.ImageUrl == null && wikiArticle?.ImageUrl != null)
            {
                await animalRepo.UpdateImageUrlAsync(animalId, wikiArticle.ImageUrl);
                logger.LogInformation("Using Wikipedia image for {AnimalName}: {ImageUrl}", animalName, wikiArticle.ImageUrl);
            }

            logger.LogInformation("Successfully generated: {AnimalName} (ID: {Id})", animalName, animalId);
            return animalId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate content for: {AnimalName}", animalName);
            throw;
        }
    }

    public async Task<(Guid? Id, string Slug)> RegenerateAnimalAsync(Guid existingId, CancellationToken ct = default)
    {
        var existing = await animalRepo.GetByIdAsync(existingId)
            ?? throw new Exception($"Animal {existingId} not found");

        var animalName = existing.CommonName;
        logger.LogInformation("Regenerating content for: {AnimalName} (replacing {Id})", animalName, existingId);

        // Hard-delete the existing animal and all related records
        await animalRepo.DeleteAsync(existingId);

        // Generate a new one
        var newId = await GenerateAnimalAsync(animalName, false, ct);
        if (newId == null)
            throw new Exception($"Failed to regenerate {animalName}");

        var newAnimal = await animalRepo.GetByIdAsync(newId.Value);
        return (newId, newAnimal?.Slug ?? animalName.ToLower().Replace(' ', '-'));
    }

    public async Task<List<(string Name, Guid? Id, string? Error)>> BatchGenerateAsync(List<string> animalNames, CancellationToken ct = default)
    {
        var results = new List<(string, Guid?, string?)>();

        foreach (var name in animalNames)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var id = await GenerateAnimalAsync(name, false, ct);
                results.Add((name, id, id.HasValue ? null : "Generation returned null"));
            }
            catch (Exception ex)
            {
                results.Add((name, null, ex.Message));
            }
        }

        return results;
    }

    private static string? GetStringOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String ? val.GetString() : null;

    private static decimal? GetDecimalOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.Number ? val.GetDecimal() : null;

    private static bool? GetBoolOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) && (val.ValueKind == JsonValueKind.True || val.ValueKind == JsonValueKind.False) ? val.GetBoolean() : null;
}
