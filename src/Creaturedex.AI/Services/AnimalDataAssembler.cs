using System.Text.RegularExpressions;
using Creaturedex.AI.Models;
using Creaturedex.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public partial class AnimalDataAssembler(
    GbifService gbifService,
    WikipediaDataFetcher wikipediaFetcher,
    ReferenceDataRepository referenceRepo,
    ILogger<AnimalDataAssembler> logger)
{
    // ── Valid IUCN codes ────────────────────────────────────────────────────
    private static readonly HashSet<string> ValidIucnCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "EX", "EW", "CR", "EN", "VU", "NT", "LC", "DD", "NE"
    };

    // ── Diet mapping (first match wins) ────────────────────────────────────
    private static readonly (string[] Keywords, string Code)[] DietMappings =
    [
        (["herbivore", "herbivorous", "plant", "vegetation", "graze", "grazing", "grazer", "browse", "foliage"], "herbivore"),
        (["carnivore", "carnivorous", "meat", "predator", "hunts"], "carnivore"),
        (["omnivore", "omnivorous", "variety", "both plants and"], "omnivore"),
        (["insectivore", "insectivorous", "insects", "arthropods"], "insectivore"),
        (["piscivore", "piscivorous", "fish", "fishing"], "piscivore"),
        (["frugivore", "frugivorous", "fruit"], "frugivore"),
        (["nectarivore", "nectar", "pollen"], "nectarivore"),
        (["filter", "plankton", "filter-feed"], "filter-feeder"),
        (["scavenge", "carrion"], "scavenger"),
        (["seeds", "grain"], "granivore"),
        (["leaves", "folivore"], "folivore"),
        (["ants", "termites"], "myrmecophage"),
        (["detritus", "decompos"], "detritivore"),
    ];

    // ── Activity pattern mapping ───────────────────────────────────────────
    private static readonly (string[] Keywords, string Code)[] ActivityMappings =
    [
        (["crepuscular", "dawn and dusk", "dawn or dusk", "twilight"], "crepuscular"),
        (["diurnal", "daytime", "during the day", "active during day"], "diurnal"),
        (["nocturnal", "active at night", "active during the night", "after dark", "nighttime"], "nocturnal"),
        (["cathemeral", "active throughout", "no fixed"], "cathemeral"),
    ];

    // ── Domestication template mapping ─────────────────────────────────────
    private static readonly (string Template, string Code)[] DomesticationTemplateMappings =
    [
        ("Infobox dog breed", "domesticated"),
        ("Infobox cat breed", "domesticated"),
        ("Infobox horse breed", "domesticated"),
        ("Infobox poultry breed", "domesticated"),
        ("Infobox rabbit breed", "domesticated"),
        ("Speciesbox", "wild"),
        ("Population taxobox", "wild"),
    ];

    // ── Tag inference keywords ─────────────────────────────────────────────
    private static readonly (string[] Keywords, string TagCode)[] BehaviourTagMappings =
    [
        (["predator", "apex predator", "hunts"], "predator"),
        (["migratory", "migration", "migrates"], "migratory"),
        (["solitary"], "solitary"),
        (["social", "herd", "pack", "flock", "colony", "troop"], "social"),
        (["venomous", "venom"], "venomous"),
        (["nocturnal"], "nocturnal"),
        (["burrowing", "burrow"], "burrowing"),
        (["arboreal", "tree-dwelling"], "arboreal"),
        (["aquatic", "semi-aquatic"], "aquatic"),
        (["flying", "flight"], "flying"),
    ];

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex SlugInvalidCharsRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex SlugMultipleDashesRegex();

    // ── Public API ──────────────────────────────────────────────────────────

    public async Task<AssembledAnimalData?> AssembleAsync(
        string animalName, int? taxonKey = null, string? scientificName = null,
        CancellationToken ct = default)
    {
        logger.LogInformation("Assembling data for {AnimalName}", animalName);

        // 1. Fetch GBIF and Wikipedia in parallel
        var gbifTask = gbifService.FetchAnimalDataAsync(animalName, ct);
        var wikiTask = wikipediaFetcher.FetchAsync(animalName, ct);
        await Task.WhenAll(gbifTask, wikiTask);

        var gbif = await gbifTask;
        var wiki = await wikiTask;

        if (gbif is null && wiki is null)
        {
            logger.LogWarning("Both GBIF and Wikipedia returned no data for {AnimalName}", animalName);
            return null;
        }

        logger.LogInformation(
            "Data sources for {AnimalName}: GBIF={GbifAvailable}, Wikipedia={WikiAvailable}",
            animalName, gbif is not null, wiki is not null);

        // Resolve common name and scientific name
        var commonName = ResolveCommonName(animalName, gbif, wiki);
        var resolvedScientificName = scientificName
            ?? gbif?.Taxonomy?.Species
            ?? gbif?.CanonicalName;

        // 2. Extract measurements from Wikipedia prose
        var proseExtracted = WikipediaMeasurementExtractor.Extract(
            wiki?.AppearanceText, wiki?.ReproductionText,
            CombineTexts(wiki?.AppearanceText, wiki?.HabitatText, wiki?.DietText,
                wiki?.BehaviourText, wiki?.ConservationText, wiki?.ReproductionText));

        // 3. Merge measurements: prose > infobox
        var measurements = MergeMeasurements(proseExtracted, wiki?.Infobox);

        // 4. Map conservation status
        var conservationCode = MapConservationStatus(wiki?.Infobox?.IucnStatusCode, gbif?.IucnCode);

        // Intro text used by multiple mappers below
        var introText = wiki?.IntroText ?? "";

        // 5. Map diet type (include intro text — often mentions "grazing", "herbivore", etc.)
        var dietText = CombineTexts(wiki?.DietText, gbif?.DietProse, introText, wiki?.BehaviourText, gbif?.BehaviourProse);
        var dietCode = MapDietType(dietText);

        // 6. Map activity pattern (include intro and habitat — sometimes mentions activity there)
        var behaviourText = CombineTexts(wiki?.BehaviourText, gbif?.BehaviourProse, introText, wiki?.HabitatText);
        var activityCode = MapActivityPattern(behaviourText);

        // 7. Map domestication status
        var domesticationCode = MapDomesticationStatus(wiki?.Infobox?.TemplateType, introText);

        // 8. Map habitat type codes
        var habitatText = CombineTexts(wiki?.HabitatText, gbif?.HabitatProse);
        var habitatCodes = await MapHabitatTypeCodes(habitatText, ct);

        // 9. Infer tag codes
        var tagCodes = InferTagCodes(gbif?.Taxonomy, conservationCode, domesticationCode,
            behaviourText, introText);

        // 10. Derive category slug
        var categorySlug = DeriveCategory(gbif?.Taxonomy, domesticationCode, habitatText, tagCodes);

        // 11. Generate slug
        var slug = GenerateSlug(commonName);

        // 12. Build and return
        var result = new AssembledAnimalData
        {
            CommonName = commonName,
            ScientificName = resolvedScientificName ?? animalName,
            Slug = slug,
            Taxonomy = gbif?.Taxonomy,
            ConservationStatusCode = conservationCode,
            PopulationTrend = wiki?.Infobox?.PopulationTrend,
            PopulationEstimate = wiki?.PopulationEstimate,
            DietTypeCode = dietCode,
            ActivityPatternCode = activityCode,
            DomesticationStatusCode = domesticationCode,
            HabitatTypeCodes = habitatCodes,
            TagCodes = tagCodes,
            WeightMinKg = measurements.WeightMinKg,
            WeightMaxKg = measurements.WeightMaxKg,
            LengthMinCm = measurements.LengthMinCm,
            LengthMaxCm = measurements.LengthMaxCm,
            SpeedMaxKph = measurements.SpeedMaxKph,
            LifespanWildMinYears = measurements.LifespanWildMinYears,
            LifespanWildMaxYears = measurements.LifespanWildMaxYears,
            LifespanCaptivityMinYears = measurements.LifespanCaptivityMinYears,
            LifespanCaptivityMaxYears = measurements.LifespanCaptivityMaxYears,
            GestationMinDays = measurements.GestationMinDays,
            GestationMaxDays = measurements.GestationMaxDays,
            LitterSizeMin = measurements.LitterSizeMin,
            LitterSizeMax = measurements.LitterSizeMax,
            AlsoKnownAs = wiki?.AlsoKnownAs is { Count: > 0 }
                ? string.Join(", ", wiki.AlsoKnownAs) : null,
            WikipediaIntroText = wiki?.IntroText,
            WikipediaDescriptionText = wiki?.AppearanceText,
            WikipediaHabitatText = wiki?.HabitatText,
            WikipediaDietText = wiki?.DietText,
            WikipediaBehaviourText = wiki?.BehaviourText,
            WikipediaConservationText = wiki?.ConservationText,
            WikipediaReproductionText = wiki?.ReproductionText,
            GbifHabitatProse = gbif?.HabitatProse,
            GbifDietProse = gbif?.DietProse,
            GbifBehaviourProse = gbif?.BehaviourProse,
            GbifConservationProse = gbif?.ConservationProse,
            NativeCountries = gbif?.NativeCountries?.ToList() ?? [],
            GbifImage = gbif?.BestImage,
            WikipediaImageUrl = wiki?.ImageUrl,
            WikipediaImageLicense = wiki?.ImageLicense,
            WikipediaUrl = wiki?.Url,
            MapMetadata = gbif?.MapMetadata,
            GbifTaxonKey = gbif?.TaxonKey,
            GbifCanonicalName = gbif?.CanonicalName,
            CategorySlug = categorySlug,
        };

        logger.LogInformation(
            "Assembled data for {AnimalName}: slug={Slug}, category={Category}, " +
            "conservation={Conservation}, diet={Diet}, activity={Activity}, " +
            "domestication={Domestication}, habitats=[{Habitats}], tags=[{Tags}]",
            animalName, result.Slug, result.CategorySlug,
            result.ConservationStatusCode, result.DietTypeCode, result.ActivityPatternCode,
            result.DomesticationStatusCode,
            string.Join(", ", result.HabitatTypeCodes),
            string.Join(", ", result.TagCodes));

        return result;
    }

    // ── Name resolution ─────────────────────────────────────────────────────

    private static string ResolveCommonName(string inputName, GbifAnimalData? gbif, WikipediaAnimalData? wiki)
    {
        // Prefer Wikipedia title (usually the best common name), then GBIF English name, then input
        if (wiki?.Title is { Length: > 0 } wikiTitle)
            return wikiTitle;
        if (gbif?.EnglishCommonName is { Length: > 0 } englishName)
            return englishName;
        return inputName;
    }

    // ── Slug generation ─────────────────────────────────────────────────────

    internal static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant().Replace(' ', '-');
        slug = SlugInvalidCharsRegex().Replace(slug, "");
        slug = SlugMultipleDashesRegex().Replace(slug, "-");
        return slug.Trim('-');
    }

    // ── Measurement merging ─────────────────────────────────────────────────

    internal static ExtractedMeasurements MergeMeasurements(
        ExtractedMeasurements prose, WikipediaInfoboxData? infobox)
    {
        // Priority: prose > infobox
        return new ExtractedMeasurements
        {
            WeightMinKg = prose.WeightMinKg ?? infobox?.WeightMinKg,
            WeightMaxKg = prose.WeightMaxKg ?? infobox?.WeightMaxKg,
            LengthMinCm = prose.LengthMinCm ?? infobox?.LengthMinCm,
            LengthMaxCm = prose.LengthMaxCm ?? infobox?.LengthMaxCm,
            SpeedMaxKph = prose.SpeedMaxKph ?? infobox?.SpeedMaxKph,
            LifespanWildMinYears = prose.LifespanWildMinYears ?? infobox?.LifespanWildMinYears,
            LifespanWildMaxYears = prose.LifespanWildMaxYears ?? infobox?.LifespanWildMaxYears,
            LifespanCaptivityMinYears = prose.LifespanCaptivityMinYears ?? infobox?.LifespanCaptivityMinYears,
            LifespanCaptivityMaxYears = prose.LifespanCaptivityMaxYears ?? infobox?.LifespanCaptivityMaxYears,
            GestationMinDays = prose.GestationMinDays ?? infobox?.GestationMinDays,
            GestationMaxDays = prose.GestationMaxDays ?? infobox?.GestationMaxDays,
            LitterSizeMin = prose.LitterSizeMin ?? infobox?.LitterSizeMin,
            LitterSizeMax = prose.LitterSizeMax ?? infobox?.LitterSizeMax,
        };
    }

    // ── Conservation status mapping ─────────────────────────────────────────

    internal static string? MapConservationStatus(string? wikiIucnCode, string? gbifIucnCode)
    {
        // Priority: Wikipedia infobox > GBIF
        var code = wikiIucnCode ?? gbifIucnCode;
        if (code is null) return null;

        var normalised = code.Trim().ToUpperInvariant();
        return ValidIucnCodes.Contains(normalised) ? normalised : null;
    }

    // ── Diet type mapping ───────────────────────────────────────────────────

    internal static string? MapDietType(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        foreach (var (keywords, code) in DietMappings)
        {
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return code;
            }
        }

        return null;
    }

    // ── Activity pattern mapping ────────────────────────────────────────────

    internal static string? MapActivityPattern(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        foreach (var (keywords, code) in ActivityMappings)
        {
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return code;
            }
        }

        return null;
    }

    // ── Domestication status mapping ────────────────────────────────────────

    internal static string MapDomesticationStatus(string? templateType, string introText)
    {
        // Override checks on intro text first
        if (!string.IsNullOrWhiteSpace(introText))
        {
            if (introText.Contains("semi-domesticated", StringComparison.OrdinalIgnoreCase)
                || introText.Contains("feral", StringComparison.OrdinalIgnoreCase))
                return "semi-domesticated";

            if (introText.Contains("domesticated", StringComparison.OrdinalIgnoreCase)
                || introText.Contains("companion animal", StringComparison.OrdinalIgnoreCase))
                return "domesticated";
        }

        // Template-based mapping
        if (!string.IsNullOrWhiteSpace(templateType))
        {
            foreach (var (template, code) in DomesticationTemplateMappings)
            {
                if (templateType.Contains(template, StringComparison.OrdinalIgnoreCase))
                    return code;
            }
        }

        return "wild";
    }

    // ── Habitat type code mapping ───────────────────────────────────────────

    private async Task<List<string>> MapHabitatTypeCodes(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var referenceHabitats = await referenceRepo.GetHabitatTypesAsync();
        var codes = new List<string>();

        foreach (var habitat in referenceHabitats)
        {
            if (text.Contains(habitat.Name, StringComparison.OrdinalIgnoreCase))
            {
                codes.Add(habitat.Code);
            }
        }

        if (codes.Count > 0)
            logger.LogDebug("Mapped habitat codes: [{Codes}]", string.Join(", ", codes));

        return codes;
    }

    // ── Tag inference ───────────────────────────────────────────────────────

    internal static List<string> InferTagCodes(
        GbifTaxonomyData? taxonomy, string? conservationCode,
        string? domesticationCode, string behaviourText, string introText)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allText = CombineTexts(behaviourText, introText);

        // Body-type tags from taxonomy
        if (taxonomy is not null)
        {
            if (string.Equals(taxonomy.Family, "Felidae", StringComparison.OrdinalIgnoreCase))
            {
                // Large cats (genus Panthera) vs small cats
                if (string.Equals(taxonomy.Genus, "Panthera", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(taxonomy.Genus, "Acinonyx", StringComparison.OrdinalIgnoreCase))
                    tags.Add("big-cat");
                else
                    tags.Add("feline");
            }

            if (string.Equals(taxonomy.Family, "Canidae", StringComparison.OrdinalIgnoreCase))
                tags.Add("canine");

            if (string.Equals(taxonomy.Order, "Primates", StringComparison.OrdinalIgnoreCase))
                tags.Add("primate");

            if (string.Equals(taxonomy.Order, "Rodentia", StringComparison.OrdinalIgnoreCase))
                tags.Add("rodent");

            if (string.Equals(taxonomy.Order, "Cetacea", StringComparison.OrdinalIgnoreCase)
                || string.Equals(taxonomy.Order, "Artiodactyla", StringComparison.OrdinalIgnoreCase)
                    && allText.Contains("whale", StringComparison.OrdinalIgnoreCase))
                tags.Add("cetacean");

            if (string.Equals(taxonomy.Class, "Aves", StringComparison.OrdinalIgnoreCase))
                tags.Add("bird");

            if (string.Equals(taxonomy.Class, "Reptilia", StringComparison.OrdinalIgnoreCase))
                tags.Add("reptile");

            if (string.Equals(taxonomy.Class, "Amphibia", StringComparison.OrdinalIgnoreCase))
                tags.Add("amphibian");

            if (string.Equals(taxonomy.Class, "Insecta", StringComparison.OrdinalIgnoreCase))
                tags.Add("insect");

            if (string.Equals(taxonomy.Class, "Arachnida", StringComparison.OrdinalIgnoreCase))
                tags.Add("arachnid");
        }

        // Conservation tags
        if (conservationCode is "CR" or "EN" or "VU")
            tags.Add("endangered");

        // Domestication tags
        if (string.Equals(domesticationCode, "domesticated", StringComparison.OrdinalIgnoreCase))
            tags.Add("pet");

        // Behaviour tags from text
        foreach (var (keywords, tagCode) in BehaviourTagMappings)
        {
            foreach (var keyword in keywords)
            {
                if (allText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    tags.Add(tagCode);
                    break;
                }
            }
        }

        return [.. tags];
    }

    // ── Category derivation ─────────────────────────────────────────────────

    internal static string DeriveCategory(
        GbifTaxonomyData? taxonomy, string? domesticationCode,
        string habitatText, List<string> tagCodes)
    {
        if (taxonomy is null) return "wild-mammals";

        var isDomesticated = string.Equals(domesticationCode, "domesticated", StringComparison.OrdinalIgnoreCase);
        var family = taxonomy.Family;
        var order = taxonomy.Order;
        var @class = taxonomy.Class;

        // Canidae
        if (string.Equals(family, "Canidae", StringComparison.OrdinalIgnoreCase))
            return isDomesticated ? "dogs" : "wild-mammals";

        // Felidae
        if (string.Equals(family, "Felidae", StringComparison.OrdinalIgnoreCase))
            return isDomesticated ? "cats" : "wild-mammals";

        // Small mammals
        if (string.Equals(order, "Rodentia", StringComparison.OrdinalIgnoreCase)
            || string.Equals(order, "Lagomorpha", StringComparison.OrdinalIgnoreCase))
            return "small-mammals";

        // Reptilia
        if (string.Equals(@class, "Reptilia", StringComparison.OrdinalIgnoreCase))
            return "reptiles";

        // Birds
        if (string.Equals(@class, "Aves", StringComparison.OrdinalIgnoreCase))
            return "birds";

        // Fish
        if (string.Equals(@class, "Actinopterygii", StringComparison.OrdinalIgnoreCase)
            || string.Equals(@class, "Chondrichthyes", StringComparison.OrdinalIgnoreCase))
            return "fish";

        // Insects
        if (string.Equals(@class, "Insecta", StringComparison.OrdinalIgnoreCase))
            return "insects";

        // Primates
        if (string.Equals(order, "Primates", StringComparison.OrdinalIgnoreCase))
            return "primates";

        // Mammalia special cases
        if (string.Equals(@class, "Mammalia", StringComparison.OrdinalIgnoreCase))
        {
            // Marine mammals
            if (!string.IsNullOrWhiteSpace(habitatText)
                && (habitatText.Contains("marine", StringComparison.OrdinalIgnoreCase)
                    || habitatText.Contains("ocean", StringComparison.OrdinalIgnoreCase)))
                return "ocean";

            // Farm animals
            if (isDomesticated && tagCodes.Contains("livestock", StringComparer.OrdinalIgnoreCase))
                return "farm";

            return "wild-mammals";
        }

        return "wild-mammals";
    }

    // ── Text helpers ────────────────────────────────────────────────────────

    private static string CombineTexts(params string?[] texts)
    {
        var nonEmpty = texts.Where(t => !string.IsNullOrWhiteSpace(t));
        return string.Join("\n\n", nonEmpty);
    }
}
