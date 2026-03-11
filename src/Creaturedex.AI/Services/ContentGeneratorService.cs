using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Creaturedex.AI.Models;
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
    GbifService gbifService,
    ImageScreeningService imageScreeningService,
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
        You are a skilled science writer for Creaturedex, a children's animal encyclopedia.
        Your audience is 8–16 year olds. Write with warmth, enthusiasm, and clear language.
        IMPORTANT: All factual data has been pre-filled from authoritative scientific sources.
        Your job is ONLY to summarise and rephrase the provided data in an engaging way.
        DO NOT invent or fabricate any numerical data (weights, lengths, speeds, populations, dates).
        DO NOT invent taxonomy — use exactly what is provided.
        DO NOT change conservation status — use exactly what is provided.
        For characteristics (Weight, Length, Speed, etc.): ONLY include values explicitly stated in the provided data. If the source data does not mention a specific measurement, omit that characteristic entirely rather than guessing.

        When uncertain about specific numbers, OMIT them rather than guessing. Never present fabricated numbers as fact.

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

    public async Task<Guid?> GenerateAnimalAsync(string animalName, bool skipImage = false,
        int? taxonKey = null, string? scientificName = null, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Generating content for: {AnimalName}", animalName);

            // Early duplicate check before calling AI
            var expectedSlug = animalName.ToLower().Replace(' ', '-');
            var earlyCheck = await animalRepo.GetBySlugIncludingUnpublishedAsync(expectedSlug);
            if (earlyCheck != null)
                throw new DuplicateAnimalException(earlyCheck.CommonName, earlyCheck.Slug);

            // Fetch GBIF data as PRIMARY source — skip resolution if taxonKey provided
            GbifAnimalData? gbifData;
            if (taxonKey.HasValue && scientificName != null)
            {
                logger.LogInformation("Using pre-resolved GBIF taxonKey={TaxonKey} ({ScientificName}) for {AnimalName}",
                    taxonKey.Value, scientificName, animalName);
                gbifData = await gbifService.FetchAnimalDataByKeyAsync(taxonKey.Value, scientificName, ct);
            }
            else
            {
                gbifData = await gbifService.FetchAnimalDataAsync(animalName, ct);
            }
            if (gbifData != null)
                logger.LogInformation("GBIF data fetched for {AnimalName} (taxonKey={TaxonKey})", animalName, gbifData.TaxonKey);
            else
                logger.LogWarning("No GBIF data found for {AnimalName}, will use Wikipedia as fallback", animalName);

            var gbifHasSufficientData = gbifData != null && (
                gbifData.HabitatProse != null || gbifData.DietProse != null || gbifData.Taxonomy != null);
            var gbifIucnMissing = gbifData?.IucnCategory == null
                || MapIucnCategory(gbifData.IucnCategory) == null;
            var gbifIucnFromSynonym = gbifData?.IucnFromSynonymFallback == true;

            // Always fetch Wikipedia — it provides supplementary data (physical descriptions,
            // verified measurements) that keeps the AI from fabricating numbers
            WikipediaArticle? wikiArticle = await wikipediaService.GetAnimalArticleAsync(animalName, ct);
            if (wikiArticle != null)
                logger.LogInformation("Wikipedia fetched for {AnimalName} ({Url})", animalName, wikiArticle.Url);

            // If GBIF has no direct IUCN (missing or from synonym fallback), try Wikipedia infobox
            // Wikipedia infoboxes have subspecies-level IUCN assessments that GBIF doesn't expose
            string? wikiIucnStatus = null;
            if (gbifIucnMissing || gbifIucnFromSynonym)
            {
                wikiIucnStatus = await wikipediaService.GetIucnStatusFromInfoboxAsync(animalName, ct);
                if (wikiIucnStatus != null)
                    logger.LogInformation("IUCN status from Wikipedia infobox: {Status} (GBIF was {GbifStatus}{Fallback})",
                        wikiIucnStatus,
                        gbifData?.IucnCategory ?? "null",
                        gbifIucnFromSynonym ? " via synonym fallback" : "");
            }

            // Build prompt from available data sources
            var userPrompt = BuildPrompt(animalName, gbifData, wikiArticle, wikiIucnStatus);

            var response = await aiService.CompleteAsync(SystemPrompt, userPrompt, ct);

            // Extract JSON from response — Ollama may wrap it in markdown fences or preamble text
            response = response.Trim();
            if (response.StartsWith("```"))
            {
                response = response[response.IndexOf('\n')..];
                if (response.Contains("```"))
                    response = response[..response.LastIndexOf("```")];
                response = response.Trim();
            }

            // If response still doesn't start with '{', try to find the JSON object
            if (!response.StartsWith('{'))
            {
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                    response = response[jsonStart..(jsonEnd + 1)];
            }

            // Try to parse, and if it fails attempt repair
            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(response);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Initial JSON parse failed at byte {Position}, attempting repair",
                    ex.BytePositionInLine);

                // Log a snippet around the error position for diagnostics
                if (ex.BytePositionInLine is > 0 and < long.MaxValue)
                {
                    var pos = (int)ex.BytePositionInLine.Value;
                    var start = Math.Max(0, pos - 50);
                    var end = Math.Min(response.Length, pos + 50);
                    logger.LogWarning("JSON context around error: ...{Snippet}...",
                        response[start..end]);
                }

                response = RepairJson(response);
                try
                {
                    json = JsonDocument.Parse(response);
                    logger.LogInformation("JSON repair succeeded");
                }
                catch (JsonException retryEx)
                {
                    logger.LogError(retryEx, "JSON repair failed, raw response length={Length}", response.Length);
                    throw new InvalidOperationException(
                        $"AI produced invalid JSON that could not be repaired. Error: {retryEx.Message}", retryEx);
                }
            }
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

            // Create taxonomy — start from AI response, then override with GBIF if available
            var taxonomy = new Taxonomy { Kingdom = "Animalia" };
            if (root.TryGetProperty("taxonomy", out var taxElement))
            {
                taxonomy.Kingdom = GetStringOrNull(taxElement, "kingdom") ?? "Animalia";
                taxonomy.Phylum = GetStringOrNull(taxElement, "phylum");
                taxonomy.Class = GetStringOrNull(taxElement, "class");
                taxonomy.TaxOrder = GetStringOrNull(taxElement, "order");
                taxonomy.Family = GetStringOrNull(taxElement, "family");
                taxonomy.Genus = GetStringOrNull(taxElement, "genus");
                taxonomy.Species = GetStringOrNull(taxElement, "species");
                taxonomy.Subspecies = GetStringOrNull(taxElement, "subspecies");
            }

            // Override taxonomy with authoritative GBIF data
            if (gbifData?.Taxonomy != null)
            {
                var gbifTax = gbifData.Taxonomy;
                taxonomy.Kingdom = gbifTax.Kingdom;
                taxonomy.Phylum = gbifTax.Phylum ?? taxonomy.Phylum;
                taxonomy.Class = gbifTax.Class ?? taxonomy.Class;
                taxonomy.TaxOrder = gbifTax.Order ?? taxonomy.TaxOrder;
                taxonomy.Family = gbifTax.Family ?? taxonomy.Family;
                taxonomy.Genus = gbifTax.Genus ?? taxonomy.Genus;
                taxonomy.Species = gbifTax.Species ?? taxonomy.Species;
                taxonomy.Subspecies = gbifTax.Subspecies ?? taxonomy.Subspecies;

                // If GBIF has no order but the AI set order == class, clear it to avoid duplication
                // (e.g. GBIF treats Squamata as a CLASS with no order level)
                if (gbifTax.Order == null && taxonomy.TaxOrder != null
                    && string.Equals(taxonomy.TaxOrder, taxonomy.Class, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Clearing duplicated order={Order} (same as class) for {AnimalName}",
                        taxonomy.TaxOrder, animalName);
                    taxonomy.TaxOrder = null;
                }

                // Store CoL data if available
                if (gbifTax.ColTaxonId != null) taxonomy.ColTaxonId = gbifTax.ColTaxonId;
                if (gbifTax.Authorship != null) taxonomy.Authorship = gbifTax.Authorship;
                if (gbifTax.Synonyms.Count > 0) taxonomy.Synonyms = string.Join("; ", gbifTax.Synonyms);

                logger.LogInformation("Taxonomy overridden with GBIF data for {AnimalName}", animalName);
            }

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

            // Override conservation status — priority: Wikipedia infobox > GBIF direct > GBIF synonym fallback
            // Wikipedia infoboxes have subspecies-level IUCN data that GBIF doesn't expose
            if (wikiIucnStatus != null)
            {
                animal.ConservationStatus = wikiIucnStatus;
                logger.LogInformation("Conservation status set from Wikipedia infobox: {Status}", wikiIucnStatus);
            }
            else if (gbifData?.IucnCategory != null)
            {
                var mappedGbifStatus = MapIucnCategory(gbifData.IucnCategory);
                if (mappedGbifStatus != null)
                {
                    animal.ConservationStatus = mappedGbifStatus;
                    logger.LogInformation("Conservation status set from GBIF: {Status}{Fallback}",
                        mappedGbifStatus, gbifIucnFromSynonym ? " (via synonym fallback)" : "");
                }
            }

            // Override scientific name with GBIF canonical name if available
            if (gbifData?.CanonicalName != null)
                animal.ScientificName = gbifData.CanonicalName;

            // Override native region with GBIF distribution data if available
            if (gbifData?.NativeCountries.Count > 0)
            {
                // Cap at 10 regions to keep it readable for a children's encyclopedia
                var region = string.Join(", ", gbifData.NativeCountries.Take(10));
                if (gbifData.NativeCountries.Count > 10)
                    region += " and others";
                // Truncate to fit NativeRegion column (NVARCHAR(500))
                if (region.Length > 500)
                    region = region[..region.LastIndexOf(',', 497)] + "...";
                animal.NativeRegion = region;
            }

            // Store GBIF identifiers
            if (gbifData != null)
            {
                animal.GbifTaxonKey = gbifData.TaxonKey;
                animal.GbifCanonicalName = gbifData.CanonicalName;
            }

            // Store map metadata
            if (gbifData?.MapMetadata != null)
            {
                var map = gbifData.MapMetadata;
                animal.MapTileUrlTemplate = map.TileUrlTemplate;
                animal.MapObservationCount = map.ObservationCount;
                animal.MapMinLat = map.MinLat;
                animal.MapMaxLat = map.MaxLat;
                animal.MapMinLng = map.MinLng;
                animal.MapMaxLng = map.MaxLng;
            }

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

            // Image priority: 1) GBIF CC BY 4.0 (child-safe screened), 2) Wikipedia thumbnail, 3) AI-generated (manual only)
            var imageSet = false;

            // Priority 1: GBIF image with child-safety screening
            if (gbifData?.BestImage != null)
            {
                var gbifImageUrl = gbifData.BestImage.CachedUrl;
                logger.LogInformation("Screening GBIF image for {AnimalName}: {ImageUrl}", animalName, gbifImageUrl);

                var isSafe = await imageScreeningService.IsChildSafeAsync(gbifImageUrl, ct);
                if (isSafe)
                {
                    await animalRepo.UpdateImageUrlAsync(animalId, gbifImageUrl);
                    await animalRepo.UpdateImageAttributionAsync(
                        animalId,
                        gbifData.BestImage.License,
                        gbifData.BestImage.RightsHolder,
                        gbifData.BestImage.Publisher);
                    logger.LogInformation("Using GBIF image for {AnimalName}: {ImageUrl} ({License})",
                        animalName, gbifImageUrl, gbifData.BestImage.License);
                    imageSet = true;
                }
                else
                {
                    logger.LogWarning("GBIF image failed child-safety screening for {AnimalName}, trying fallback", animalName);
                }
            }

            // Priority 2: Wikipedia thumbnail as fallback
            if (!imageSet && wikiArticle?.ImageUrl != null)
            {
                await animalRepo.UpdateImageUrlAsync(animalId, wikiArticle.ImageUrl);
                if (wikiArticle.ImageLicense != null)
                {
                    await animalRepo.UpdateImageAttributionAsync(
                        animalId,
                        wikiArticle.ImageLicense,
                        null,
                        wikiArticle.Url);
                }
                logger.LogInformation("Using Wikipedia image for {AnimalName}: {ImageUrl}", animalName, wikiArticle.ImageUrl);
                imageSet = true;
            }

            // Priority 2b: If we didn't fetch Wikipedia earlier but have no image, fetch it now just for the image
            if (!imageSet && gbifHasSufficientData)
            {
                var wikiForImage = await wikipediaService.GetAnimalArticleAsync(animalName, ct);
                if (wikiForImage?.ImageUrl != null)
                {
                    await animalRepo.UpdateImageUrlAsync(animalId, wikiForImage.ImageUrl);
                    if (wikiForImage.ImageLicense != null)
                    {
                        await animalRepo.UpdateImageAttributionAsync(
                            animalId,
                            wikiForImage.ImageLicense,
                            null,
                            wikiForImage.Url);
                    }
                    logger.LogInformation("Using Wikipedia image (fallback) for {AnimalName}: {ImageUrl}", animalName, wikiForImage.ImageUrl);
                    imageSet = true;
                }
            }

            // Priority 3: AI-generated via Stable Diffusion (only if enabled and no other image)
            if (!imageSet && !skipImage && aiConfig.AutoGenerateImages)
            {
                var imageUrl = await imageService.GenerateAnimalImageAsync(
                    animal.CommonName, animal.Slug, animal.ScientificName,
                    animal.Summary, animal.Description, animal.Habitat, animal.SizeInfo, ct);
                if (imageUrl != null)
                {
                    await animalRepo.UpdateImageUrlAsync(animalId, imageUrl);
                    logger.LogInformation("Generated AI image for {AnimalName}: {ImageUrl}", animalName, imageUrl);
                }
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
        var newId = await GenerateAnimalAsync(animalName, skipImage: false, ct: ct);
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
                var id = await GenerateAnimalAsync(name, skipImage: false, ct: ct);
                results.Add((name, id, id.HasValue ? null : "Generation returned null"));
            }
            catch (Exception ex)
            {
                results.Add((name, null, ex.Message));
            }
        }

        return results;
    }

    private static string BuildPrompt(string animalName, GbifAnimalData? gbif, WikipediaArticle? wiki, string? wikiIucnOverride = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate a complete profile for: {animalName}");
        sb.AppendLine();

        if (gbif != null)
        {
            sb.AppendLine("=== PRIMARY DATA (from GBIF — authoritative scientific source) ===");

            if (gbif.Taxonomy != null)
            {
                sb.AppendLine();
                sb.AppendLine("TAXONOMY (use exactly as provided — do not modify):");
                sb.AppendLine($"  Kingdom: {gbif.Taxonomy.Kingdom}");
                if (gbif.Taxonomy.Phylum != null) sb.AppendLine($"  Phylum: {gbif.Taxonomy.Phylum}");
                if (gbif.Taxonomy.Class != null) sb.AppendLine($"  Class: {gbif.Taxonomy.Class}");
                if (gbif.Taxonomy.Order != null) sb.AppendLine($"  Order: {gbif.Taxonomy.Order}");
                if (gbif.Taxonomy.Family != null) sb.AppendLine($"  Family: {gbif.Taxonomy.Family}");
                if (gbif.Taxonomy.Genus != null) sb.AppendLine($"  Genus: {gbif.Taxonomy.Genus}");
                if (gbif.Taxonomy.Species != null) sb.AppendLine($"  Species: {gbif.Taxonomy.Species}");
                if (gbif.Taxonomy.Subspecies != null) sb.AppendLine($"  Subspecies: {gbif.Taxonomy.Subspecies}");
            }

            var mappedIucn = gbif.IucnCategory != null ? MapIucnCategory(gbif.IucnCategory) : null;
            // Use Wikipedia-extracted IUCN as fallback when GBIF has no status
            var effectiveIucn = mappedIucn ?? wikiIucnOverride;
            if (effectiveIucn != null)
            {
                sb.AppendLine();
                sb.AppendLine($"CONSERVATION STATUS (use exactly as provided — do not modify): {effectiveIucn}");
            }

            if (gbif.NativeCountries.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"NATIVE RANGE: {string.Join(", ", gbif.NativeCountries.Take(15))}");
            }

            if (gbif.HabitatProse != null)
            {
                sb.AppendLine();
                sb.AppendLine($"HABITAT: {gbif.HabitatProse}");
            }

            if (gbif.DietProse != null)
            {
                sb.AppendLine();
                sb.AppendLine($"DIET: {gbif.DietProse}");
            }

            if (gbif.BehaviourProse != null)
            {
                sb.AppendLine();
                sb.AppendLine($"BEHAVIOUR: {gbif.BehaviourProse}");
            }

            if (gbif.PhysicalDescriptionProse != null)
            {
                sb.AppendLine();
                sb.AppendLine($"PHYSICAL DESCRIPTION: {gbif.PhysicalDescriptionProse}");
            }

            if (gbif.BreedingProse != null)
            {
                sb.AppendLine();
                sb.AppendLine($"BREEDING: {gbif.BreedingProse}");
            }

            if (gbif.ConservationProse != null)
            {
                sb.AppendLine();
                sb.AppendLine($"CONSERVATION NOTES: {gbif.ConservationProse}");
            }

            if (gbif.DistributionProse != null)
            {
                sb.AppendLine();
                sb.AppendLine($"DISTRIBUTION: {gbif.DistributionProse}");
            }

            if (gbif.CanonicalName != null)
            {
                sb.AppendLine();
                sb.AppendLine($"SCIENTIFIC NAME: {gbif.CanonicalName}");
            }

            sb.AppendLine();
            sb.AppendLine("=== END PRIMARY DATA ===");
        }

        if (wiki != null)
        {
            sb.AppendLine();
            sb.AppendLine("=== SUPPLEMENTARY DATA (from Wikipedia — use to fill gaps not covered by primary data) ===");
            sb.AppendLine(WikipediaService.FormatAsReference(wiki));
            sb.AppendLine("=== END SUPPLEMENTARY DATA ===");
        }

        sb.AppendLine();
        sb.AppendLine("Using the data above, write an engaging profile. Include: Summary, Description, FunFacts, Tags, and PetCareGuide (if applicable).");
        sb.AppendLine("Use the provided taxonomy and conservation status exactly as given. Write prose fields (summary, description, habitat, diet, behaviour) in your own words for a young audience.");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts IUCN conservation status from Wikipedia text (conservation section or summary).
    /// Wikipedia often has subspecies-level IUCN assessments that GBIF doesn't expose.
    /// </summary>
    internal static string? ExtractIucnFromWikipediaText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Match phrases like "listed as Endangered", "classified as Critically Endangered",
        // "conservation status: Vulnerable", "IUCN Red List as Endangered", etc.
        // Order matters — check longer phrases first to avoid partial matches
        var statusKeywords = new (string Pattern, string Status)[]
        {
            ("Critically Endangered", "Critically Endangered"),
            ("Extinct in the Wild", "Extinct in the Wild"),
            ("Near Threatened", "Near Threatened"),
            ("Least Concern", "Least Concern"),
            ("Data Deficient", "Data Deficient"),
            ("Endangered", "Endangered"),
            ("Vulnerable", "Vulnerable"),
            ("Extinct", "Extinct"),
        };

        foreach (var (pattern, status) in statusKeywords)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return status;
        }

        return null;
    }

    private static string? MapIucnCategory(string gbifCategory) => gbifCategory switch
    {
        "LEAST_CONCERN" or "LC" => "Least Concern",
        "NEAR_THREATENED" or "NT" => "Near Threatened",
        "VULNERABLE" or "VU" => "Vulnerable",
        "ENDANGERED" or "EN" => "Endangered",
        "CRITICALLY_ENDANGERED" or "CR" => "Critically Endangered",
        "EXTINCT_IN_THE_WILD" or "EW" => "Extinct in the Wild",
        "EXTINCT" or "EX" => "Extinct",
        "DATA_DEFICIENT" or "DD" => "Data Deficient",
        "NOT_EVALUATED" or "NE" => null, // Explicitly return null — don't pass to AI
        _ => null,
    };

    private static string? GetStringOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String ? val.GetString() : null;

    private static decimal? GetDecimalOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.Number ? val.GetDecimal() : null;

    private static bool? GetBoolOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var val) && (val.ValueKind == JsonValueKind.True || val.ValueKind == JsonValueKind.False) ? val.GetBoolean() : null;

    /// <summary>
    /// Attempts to repair common JSON malformations produced by Ollama:
    /// - Unescaped quotes inside string values
    /// - Colons where commas are expected (between key-value pairs)
    /// - Trailing commas before ] or }
    /// - Control characters inside strings
    /// </summary>
    private static string RepairJson(string json)
    {
        // Step 1: Fix control characters inside strings (newlines, tabs)
        json = FixControlCharactersInStrings(json);

        // Step 2: Walk through character by character fixing structural issues
        var sb = new StringBuilder(json.Length);
        var inString = false;
        var escaped = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (escaped)
            {
                sb.Append(c);
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                sb.Append(c);
                escaped = true;
                continue;
            }

            if (c == '"' && !escaped)
            {
                // Check for unescaped quotes inside strings
                if (inString)
                {
                    // Look ahead: is this the real end of the string?
                    var afterQuote = SkipWhitespace(json, i + 1);
                    if (afterQuote < json.Length)
                    {
                        var next = json[afterQuote];
                        // Valid chars after a closing quote: , } ] :
                        if (next == ',' || next == '}' || next == ']' || next == ':')
                        {
                            inString = false;
                            sb.Append(c);
                            continue;
                        }

                        // If next char is another quote (empty string follows or new key),
                        // this is the real end
                        if (next == '"')
                        {
                            inString = false;
                            sb.Append(c);
                            continue;
                        }

                        // Otherwise this quote is inside the string — escape it
                        sb.Append('\\');
                        sb.Append('"');
                        continue;
                    }
                }

                inString = !inString;
                sb.Append(c);
                continue;
            }

            // Fix colon where comma is expected (between values outside strings)
            if (c == ':' && !inString)
            {
                // A colon is valid only after a key (string). Look back to see if previous
                // non-whitespace token was a closing quote (key) or something else.
                var beforeColon = SkipWhitespaceBack(sb.ToString(), sb.Length - 1);
                if (beforeColon >= 0 && sb[beforeColon] == '"')
                {
                    // This is a normal key:value separator
                    sb.Append(c);
                }
                else
                {
                    // Colon after a value (number, bool, string, array, object) — should be comma
                    sb.Append(',');
                }
                continue;
            }

            sb.Append(c);
        }

        var result = sb.ToString();

        // Step 3: Fix trailing commas before } or ]
        result = Regex.Replace(result, @",\s*([}\]])", "$1");

        return result;
    }

    private static string FixControlCharactersInStrings(string json)
    {
        // Replace unescaped newlines/tabs inside JSON strings
        var sb = new StringBuilder(json.Length);
        var inString = false;
        var escaped = false;

        foreach (var c in json)
        {
            if (escaped)
            {
                sb.Append(c);
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                sb.Append(c);
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                sb.Append(c);
                continue;
            }

            if (inString)
            {
                switch (c)
                {
                    case '\n': sb.Append("\\n"); continue;
                    case '\r': sb.Append("\\r"); continue;
                    case '\t': sb.Append("\\t"); continue;
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static int SkipWhitespace(string s, int from)
    {
        while (from < s.Length && char.IsWhiteSpace(s[from])) from++;
        return from;
    }

    private static int SkipWhitespaceBack(string s, int from)
    {
        while (from >= 0 && char.IsWhiteSpace(s[from])) from--;
        return from;
    }
}
