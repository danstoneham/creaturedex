using System.Text.Json;
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
    AnimalDataAssembler assembler,
    ContentSummariser summariser,
    EmbeddingService embeddingService,
    ImageGenerationService imageService,
    ImageScreeningService imageScreeningService,
    WikipediaService wikipediaService,
    ReferenceDataRepository referenceRepo,
    AnimalRepository animalRepo,
    CategoryRepository categoryRepo,
    TaxonomyRepository taxonomyRepo,
    PetCareGuideRepository careRepo,
    CharacteristicRepository charRepo,
    TagRepository tagRepo,
    AIService aiService,
    AIConfig aiConfig,
    ILogger<ContentGeneratorService> logger)
{
    public async Task<Guid?> GenerateAnimalAsync(string animalName, bool skipImage = false,
        int? taxonKey = null, string? scientificName = null, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Generating content for: {AnimalName} [v2 pipeline]", animalName);

            // 1. Early duplicate check
            var expectedSlug = animalName.ToLower().Replace(' ', '-');
            var earlyCheck = await animalRepo.GetBySlugIncludingUnpublishedAsync(expectedSlug);
            if (earlyCheck != null)
                throw new DuplicateAnimalException(earlyCheck.CommonName, earlyCheck.Slug);

            // 2. Assemble all factual data (NO AI involved)
            var assembled = await assembler.AssembleAsync(animalName, taxonKey, scientificName, ct);
            if (assembled == null)
                throw new InvalidOperationException(
                    $"Could not assemble data for {animalName} — neither GBIF nor Wikipedia returned usable data");

            // 3. Check slug again (assembler may generate a different slug)
            var existing = await animalRepo.GetBySlugIncludingUnpublishedAsync(assembled.Slug);
            if (existing != null)
                throw new DuplicateAnimalException(existing.CommonName, existing.Slug);

            // 4. Run constrained AI summarisation (parallel where possible)
            var introText = assembled.WikipediaIntroText ?? assembled.GbifHabitatProse ?? "";
            var fullWikiText = string.Join("\n\n", new[]
            {
                assembled.WikipediaIntroText, assembled.WikipediaDescriptionText,
                assembled.WikipediaHabitatText, assembled.WikipediaDietText,
                assembled.WikipediaBehaviourText, assembled.WikipediaConservationText
            }.Where(t => t != null));

            // Get colour codes for AI matching
            var colours = await referenceRepo.GetColoursAsync();
            var colourCodes = colours.Select(c => c.Code).ToList();

            // Run AI tasks sequentially — Ollama processes one at a time,
            // so parallel dispatch just causes timeouts on later tasks
            logger.LogInformation("Starting AI summarisation for {AnimalName} (8 tasks sequential)", assembled.CommonName);

            var summary = await summariser.SummariseIntroAsync(assembled.CommonName, introText, ct);
            logger.LogInformation("  [1/8] Intro summary done");

            var description = await summariser.SummariseDescriptionAsync(
                assembled.CommonName, assembled.WikipediaIntroText,
                assembled.WikipediaHabitatText ?? assembled.GbifHabitatProse,
                assembled.WikipediaDietText ?? assembled.GbifDietProse,
                assembled.WikipediaBehaviourText ?? assembled.GbifBehaviourProse, ct);
            logger.LogInformation("  [2/8] Description done");

            var funFacts = !string.IsNullOrWhiteSpace(fullWikiText)
                ? await summariser.ExtractFunFactsAsync(assembled.CommonName, fullWikiText, ct)
                : new List<string>();
            logger.LogInformation("  [3/8] Fun facts done ({Count} facts)", funFacts.Count);

            var matchedColours = await summariser.MatchColoursAsync(
                assembled.CommonName, assembled.WikipediaDescriptionText, colourCodes, ct);
            logger.LogInformation("  [4/8] Colours done ({Count} colours)", matchedColours.Count);

            var features = await summariser.ExtractDistinguishingFeaturesAsync(
                assembled.CommonName, assembled.WikipediaDescriptionText, ct);
            logger.LogInformation("  [5/8] Features done ({Count} features)", features.Count);

            var habitatSummary = await summariser.SummariseSectionAsync(
                assembled.CommonName, "habitat",
                assembled.WikipediaHabitatText ?? assembled.GbifHabitatProse, ct);
            logger.LogInformation("  [6/8] Habitat summary done");

            var dietSummary = await summariser.SummariseSectionAsync(
                assembled.CommonName, "diet",
                assembled.WikipediaDietText ?? assembled.GbifDietProse, ct);
            logger.LogInformation("  [7/8] Diet summary done");

            var behaviourSummary = await summariser.SummariseSectionAsync(
                assembled.CommonName, "behaviour",
                assembled.WikipediaBehaviourText ?? assembled.GbifBehaviourProse, ct);
            logger.LogInformation("  [8/8] Behaviour summary done");

            // 4b. AI fallback for missing structured measurements (98%+ confidence required)
            var fullText = CombineTexts(assembled.WikipediaIntroText,
                assembled.WikipediaDescriptionText, assembled.WikipediaHabitatText,
                assembled.WikipediaDietText, assembled.WikipediaBehaviourText,
                assembled.WikipediaConservationText, assembled.WikipediaReproductionText);

            var missingFields = new List<string>();
            if (assembled.LifespanWildMinYears == null) missingFields.Add("lifespanWildYears");
            if (assembled.LifespanCaptivityMinYears == null) missingFields.Add("lifespanCaptivityYears");
            if (assembled.GestationMinDays == null) missingFields.Add("gestationDays");
            if (assembled.LitterSizeMin == null) missingFields.Add("litterSize");
            if (assembled.ActivityPatternCode == null) missingFields.Add("activityPattern");
            if (assembled.DietTypeCode == null) missingFields.Add("dietType");

            if (missingFields.Count > 0)
            {
                logger.LogInformation("  [AI fallback] Inferring {Count} missing fields: {Fields}",
                    missingFields.Count, string.Join(", ", missingFields));

                var inferred = await summariser.InferMissingMeasurementsAsync(
                    assembled.CommonName, fullText, missingFields, ct);

                // Apply inferred values to assembled data
                foreach (var (field, value) in inferred)
                {
                    switch (field)
                    {
                        case "lifespanWildYears":
                            assembled.LifespanWildMinYears = value.Min;
                            assembled.LifespanWildMaxYears = value.Max ?? value.Min;
                            break;
                        case "lifespanCaptivityYears":
                            assembled.LifespanCaptivityMinYears = value.Min;
                            assembled.LifespanCaptivityMaxYears = value.Max ?? value.Min;
                            break;
                        case "gestationDays":
                            assembled.GestationMinDays = value.Min;
                            assembled.GestationMaxDays = value.Max ?? value.Min;
                            break;
                        case "litterSize":
                            assembled.LitterSizeMin = value.Min;
                            assembled.LitterSizeMax = value.Max ?? value.Min;
                            break;
                        case "activityPattern":
                            assembled.ActivityPatternCode = value.StringValue;
                            break;
                        case "dietType":
                            assembled.DietTypeCode = value.StringValue;
                            break;
                    }
                }

                logger.LogInformation("  [AI fallback] Accepted {Count}/{Total} inferred values",
                    inferred.Count, missingFields.Count);
            }

            // 5. Resolve category
            var category = await categoryRepo.GetBySlugAsync(assembled.CategorySlug);
            if (category == null)
            {
                logger.LogWarning("Category {Slug} not found, falling back to wild-mammals", assembled.CategorySlug);
                category = await categoryRepo.GetBySlugAsync("wild-mammals");
            }

            // 6. Create taxonomy from assembled data (NO AI)
            var taxonomy = new Taxonomy { Kingdom = "Animalia" };
            if (assembled.Taxonomy != null)
            {
                var t = assembled.Taxonomy;
                taxonomy.Kingdom = t.Kingdom;
                taxonomy.Phylum = t.Phylum;
                taxonomy.Class = t.Class;
                taxonomy.TaxOrder = t.Order;
                taxonomy.Family = t.Family;
                taxonomy.Genus = t.Genus;
                taxonomy.Species = t.Species;
                taxonomy.Subspecies = t.Subspecies;
                if (t.ColTaxonId != null) taxonomy.ColTaxonId = t.ColTaxonId;
                if (t.Authorship != null) taxonomy.Authorship = t.Authorship;
                if (t.Synonyms.Count > 0) taxonomy.Synonyms = string.Join("; ", t.Synonyms);
            }
            var taxonomyId = await taxonomyRepo.CreateAsync(taxonomy);

            // 7. Determine isPet from domestication status
            var domStatuses = await referenceRepo.GetDomesticationStatusesAsync();
            var domStatus = domStatuses.FirstOrDefault(d =>
                string.Equals(d.Code, assembled.DomesticationStatusCode, StringComparison.OrdinalIgnoreCase));
            var isPet = domStatus?.IsPet ?? false;

            // 8. Build native region string
            string? nativeRegion = null;
            if (assembled.NativeCountries.Count > 0)
            {
                nativeRegion = string.Join(", ", assembled.NativeCountries.Take(10));
                if (assembled.NativeCountries.Count > 10) nativeRegion += " and others";
                if (nativeRegion.Length > 500) nativeRegion = nativeRegion[..nativeRegion.LastIndexOf(',', 497)] + "...";
            }

            // 9. Create animal with ALL structured columns
            var animal = new Animal
            {
                Slug = assembled.Slug,
                CommonName = assembled.CommonName,
                ScientificName = assembled.ScientificName,
                Summary = summary,
                Description = description,
                CategoryId = category!.Id,
                TaxonomyId = taxonomyId,
                IsPet = isPet,
                ConservationStatus = MapConservationCodeToName(assembled.ConservationStatusCode),
                NativeRegion = nativeRegion,
                Habitat = habitatSummary,
                Diet = dietSummary,
                Lifespan = FormatLifespan(assembled),
                SizeInfo = FormatSizeInfo(assembled),
                Behaviour = behaviourSummary,
                FunFacts = JsonSerializer.Serialize(funFacts),
                GeneratedAt = DateTime.UtcNow,
                IsPublished = false,

                // GBIF identifiers
                GbifTaxonKey = assembled.GbifTaxonKey,
                GbifCanonicalName = assembled.GbifCanonicalName,

                // Map metadata
                MapTileUrlTemplate = assembled.MapMetadata?.TileUrlTemplate,
                MapObservationCount = assembled.MapMetadata?.ObservationCount,
                MapMinLat = assembled.MapMetadata?.MinLat,
                MapMaxLat = assembled.MapMetadata?.MaxLat,
                MapMinLng = assembled.MapMetadata?.MinLng,
                MapMaxLng = assembled.MapMetadata?.MaxLng,

                // NEW structured columns
                WikipediaUrl = assembled.WikipediaUrl,
                ConservationStatusCode = assembled.ConservationStatusCode,
                PopulationTrend = assembled.PopulationTrend,
                PopulationEstimate = assembled.PopulationEstimate,
                DietTypeCode = assembled.DietTypeCode,
                ActivityPatternCode = assembled.ActivityPatternCode,
                DomesticationStatusCode = assembled.DomesticationStatusCode,
                WeightMinKg = assembled.WeightMinKg,
                WeightMaxKg = assembled.WeightMaxKg,
                LengthMinCm = assembled.LengthMinCm,
                LengthMaxCm = assembled.LengthMaxCm,
                SpeedMaxKph = assembled.SpeedMaxKph,
                LifespanWildMinYears = assembled.LifespanWildMinYears,
                LifespanWildMaxYears = assembled.LifespanWildMaxYears,
                LifespanCaptivityMinYears = assembled.LifespanCaptivityMinYears,
                LifespanCaptivityMaxYears = assembled.LifespanCaptivityMaxYears,
                GestationMinDays = assembled.GestationMinDays,
                GestationMaxDays = assembled.GestationMaxDays,
                LitterSizeMin = assembled.LitterSizeMin,
                LitterSizeMax = assembled.LitterSizeMax,
                AlsoKnownAs = assembled.AlsoKnownAs,
                DistinguishingFeatures = features.Count > 0 ? JsonSerializer.Serialize(features) : null,
                ColoursJson = matchedColours.Count > 0 ? JsonSerializer.Serialize(matchedColours) : null,
                HabitatTypesJson = assembled.HabitatTypeCodes.Count > 0
                    ? JsonSerializer.Serialize(assembled.HabitatTypeCodes) : null,
                DataSourceVersion = 2,
                LastDataFetchAt = DateTime.UtcNow,
            };

            var animalId = await animalRepo.CreateAsync(animal);

            // 10. Create characteristics from numeric data
            var characteristics = new List<AnimalCharacteristic>();
            var sortOrder = 0;
            if (assembled.WeightMinKg != null || assembled.WeightMaxKg != null)
                characteristics.Add(new AnimalCharacteristic
                {
                    AnimalId = animalId, CharacteristicName = "Weight",
                    CharacteristicValue = FormatWeight(assembled.WeightMinKg, assembled.WeightMaxKg),
                    SortOrder = sortOrder++
                });
            if (assembled.LengthMinCm != null || assembled.LengthMaxCm != null)
                characteristics.Add(new AnimalCharacteristic
                {
                    AnimalId = animalId, CharacteristicName = "Length",
                    CharacteristicValue = FormatLength(assembled.LengthMinCm, assembled.LengthMaxCm),
                    SortOrder = sortOrder++
                });
            if (assembled.SpeedMaxKph != null)
                characteristics.Add(new AnimalCharacteristic
                {
                    AnimalId = animalId, CharacteristicName = "Speed",
                    CharacteristicValue = $"{assembled.SpeedMaxKph:0.#} km/h ({assembled.SpeedMaxKph * 0.621m:0.#} mph)",
                    SortOrder = sortOrder++
                });
            if (characteristics.Count > 0)
                await charRepo.BulkInsertAsync(characteristics);

            // 11. Create tags from assembled codes
            var tags = assembled.TagCodes
                .Select(code => new AnimalTag { AnimalId = animalId, Tag = code })
                .ToList();
            if (tags.Count > 0)
                await tagRepo.BulkInsertAsync(tags);

            // 12. Pet care guide (kept as AI-driven for pet animals only)
            if (isPet)
            {
                await GeneratePetCareGuideAsync(animalId, animal, ct);
            }

            // 13. Generate embedding
            var embeddingText = $"{animal.CommonName} {animal.Summary} {animal.Description} {string.Join(" ", funFacts)}";
            await embeddingService.GenerateAndStoreAsync(animalId, embeddingText, aiConfig.EmbeddingModel);

            // 14. Handle images (same priority: GBIF -> Wikipedia -> AI)
            await HandleImageAsync(animalId, animalName, assembled, skipImage, ct);

            logger.LogInformation("Successfully generated: {AnimalName} (ID: {Id}) [v2 pipeline]", animalName, animalId);
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

    private async Task HandleImageAsync(Guid animalId, string animalName, AssembledAnimalData assembled, bool skipImage, CancellationToken ct)
    {
        var imageSet = false;

        // Priority 1: GBIF image with child-safety screening
        if (assembled.GbifImage != null)
        {
            var gbifImageUrl = assembled.GbifImage.CachedUrl;
            logger.LogInformation("Screening GBIF image for {AnimalName}: {ImageUrl}", animalName, gbifImageUrl);

            var isSafe = await imageScreeningService.IsChildSafeAsync(gbifImageUrl, ct);
            if (isSafe)
            {
                await animalRepo.UpdateImageUrlAsync(animalId, gbifImageUrl);
                await animalRepo.UpdateImageAttributionAsync(animalId,
                    assembled.GbifImage.License, assembled.GbifImage.RightsHolder, assembled.GbifImage.Publisher);
                logger.LogInformation("Using GBIF image for {AnimalName}: {ImageUrl} ({License})",
                    animalName, gbifImageUrl, assembled.GbifImage.License);
                imageSet = true;
            }
            else
            {
                logger.LogWarning("GBIF image failed child-safety screening for {AnimalName}, trying fallback", animalName);
            }
        }

        // Priority 2: Wikipedia image
        if (!imageSet && assembled.WikipediaImageUrl != null)
        {
            await animalRepo.UpdateImageUrlAsync(animalId, assembled.WikipediaImageUrl);
            if (assembled.WikipediaImageLicense != null)
                await animalRepo.UpdateImageAttributionAsync(animalId,
                    assembled.WikipediaImageLicense, null, assembled.WikipediaUrl);
            logger.LogInformation("Using Wikipedia image for {AnimalName}: {ImageUrl}", animalName, assembled.WikipediaImageUrl);
            imageSet = true;
        }

        // Priority 3: Try fetching from old WikipediaService if we didn't get an image from new fetcher
        if (!imageSet)
        {
            var wikiArticle = await wikipediaService.GetAnimalArticleAsync(animalName, ct);
            if (wikiArticle?.ImageUrl != null)
            {
                await animalRepo.UpdateImageUrlAsync(animalId, wikiArticle.ImageUrl);
                if (wikiArticle.ImageLicense != null)
                    await animalRepo.UpdateImageAttributionAsync(animalId, wikiArticle.ImageLicense, null, wikiArticle.Url);
                logger.LogInformation("Using Wikipedia image (fallback) for {AnimalName}: {ImageUrl}", animalName, wikiArticle.ImageUrl);
                imageSet = true;
            }
        }

        // Priority 4: AI-generated (only if enabled)
        if (!imageSet && !skipImage && aiConfig.AutoGenerateImages)
        {
            var animal = await animalRepo.GetByIdAsync(animalId);
            if (animal != null)
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
        }
    }

    private async Task GeneratePetCareGuideAsync(Guid animalId, Animal animal, CancellationToken ct)
    {
        try
        {
            var systemPrompt = """
                You are a pet care expert writing for a children's animal encyclopedia (ages 8-16).
                Generate a pet care guide based on the animal information provided.
                Respond with ONLY valid JSON matching this schema:
                {
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
                """;
            var userPrompt = $"Generate a pet care guide for: {animal.CommonName} ({animal.ScientificName})";
            var response = (await aiService.CompleteAsync(systemPrompt, userPrompt, ct)).Trim();

            // Strip markdown fences
            if (response.StartsWith("```"))
            {
                response = response[response.IndexOf('\n')..];
                if (response.Contains("```")) response = response[..response.LastIndexOf("```")];
                response = response.Trim();
            }
            if (!response.StartsWith('{'))
            {
                var s = response.IndexOf('{');
                var e = response.LastIndexOf('}');
                if (s >= 0 && e > s) response = response[s..(e + 1)];
            }

            var json = JsonDocument.Parse(response);
            var root = json.RootElement;

            var guide = new PetCareGuide
            {
                AnimalId = animalId,
                DifficultyRating = root.TryGetProperty("difficultyRating", out var dr) ? dr.GetInt32() : 3,
                CostRangeMin = root.TryGetProperty("costRangeMin", out var cmin) && cmin.ValueKind == JsonValueKind.Number ? cmin.GetDecimal() : null,
                CostRangeMax = root.TryGetProperty("costRangeMax", out var cmax) && cmax.ValueKind == JsonValueKind.Number ? cmax.GetDecimal() : null,
                CostCurrency = root.TryGetProperty("costCurrency", out var cc) ? cc.GetString() ?? "GBP" : "GBP",
                SpaceRequirement = root.TryGetProperty("spaceRequirement", out var sr) ? sr.GetString() : null,
                TimeCommitment = root.TryGetProperty("timeCommitment", out var tc) ? tc.GetString() : null,
                Housing = root.TryGetProperty("housing", out var h) ? h.GetString() : null,
                DietAsPet = root.TryGetProperty("dietAsPet", out var d) ? d.GetString() : null,
                Exercise = root.TryGetProperty("exercise", out var ex) ? ex.GetString() : null,
                Grooming = root.TryGetProperty("grooming", out var g) ? g.GetString() : null,
                HealthConcerns = root.TryGetProperty("healthConcerns", out var hc) ? hc.GetString() : null,
                Training = root.TryGetProperty("training", out var t) ? t.GetString() : null,
                GoodWithChildren = root.TryGetProperty("goodWithChildren", out var gc) ? gc.GetBoolean() : null,
                GoodWithOtherPets = root.TryGetProperty("goodWithOtherPets", out var gp) ? gp.GetBoolean() : null,
                Temperament = root.TryGetProperty("temperament", out var tmp) ? tmp.GetString() : null,
                LegalConsiderations = root.TryGetProperty("legalConsiderations", out var lc) ? lc.GetString() : null,
            };
            await careRepo.CreateAsync(guide);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate pet care guide for {AnimalName}, skipping", animal.CommonName);
        }
    }

    // ── Helper methods ──────────────────────────────────────────────────────

    private static string? MapConservationCodeToName(string? code) => code switch
    {
        "EX" => "Extinct",
        "EW" => "Extinct in the Wild",
        "CR" => "Critically Endangered",
        "EN" => "Endangered",
        "VU" => "Vulnerable",
        "NT" => "Near Threatened",
        "LC" => "Least Concern",
        "DD" => "Data Deficient",
        "NE" => null,
        _ => null,
    };

    private static string FormatLifespan(AssembledAnimalData data)
    {
        var parts = new List<string>();
        if (data.LifespanWildMinYears != null && data.LifespanWildMaxYears != null)
            parts.Add($"{data.LifespanWildMinYears}-{data.LifespanWildMaxYears} years in the wild");
        else if (data.LifespanWildMaxYears != null)
            parts.Add($"Up to {data.LifespanWildMaxYears} years in the wild");
        if (data.LifespanCaptivityMinYears != null && data.LifespanCaptivityMaxYears != null)
            parts.Add($"{data.LifespanCaptivityMinYears}-{data.LifespanCaptivityMaxYears} years in captivity");
        else if (data.LifespanCaptivityMaxYears != null)
            parts.Add($"Up to {data.LifespanCaptivityMaxYears} years in captivity");
        return parts.Count > 0 ? string.Join("; ", parts) : "";
    }

    private static string FormatSizeInfo(AssembledAnimalData data)
    {
        var parts = new List<string>();
        if (data.WeightMinKg != null || data.WeightMaxKg != null)
            parts.Add($"Weight: {FormatWeight(data.WeightMinKg, data.WeightMaxKg)}");
        if (data.LengthMinCm != null || data.LengthMaxCm != null)
            parts.Add($"Length: {FormatLength(data.LengthMinCm, data.LengthMaxCm)}");
        if (data.SpeedMaxKph != null)
            parts.Add($"Top speed: {data.SpeedMaxKph:0.#} km/h ({data.SpeedMaxKph * 0.621m:0.#} mph)");
        return string.Join(". ", parts);
    }

    private static string FormatWeight(decimal? min, decimal? max)
    {
        if (min != null && max != null)
            return $"{min:0.#}-{max:0.#} kg ({min * 2.205m:0.#}-{max * 2.205m:0.#} lb)";
        if (max != null) return $"Up to {max:0.#} kg ({max * 2.205m:0.#} lb)";
        if (min != null) return $"From {min:0.#} kg ({min * 2.205m:0.#} lb)";
        return "";
    }

    private static string FormatLength(decimal? min, decimal? max)
    {
        if (min != null && max != null)
            return $"{min:0.#}-{max:0.#} cm ({min / 2.54m:0.#}-{max / 2.54m:0.#} in)";
        if (max != null) return $"Up to {max:0.#} cm ({max / 2.54m:0.#} in)";
        if (min != null) return $"From {min:0.#} cm ({min / 2.54m:0.#} in)";
        return "";
    }

    private static string CombineTexts(params string?[] texts)
    {
        var nonEmpty = texts.Where(t => !string.IsNullOrWhiteSpace(t));
        return string.Join("\n\n", nonEmpty);
    }
}
