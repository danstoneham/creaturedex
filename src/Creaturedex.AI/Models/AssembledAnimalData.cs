namespace Creaturedex.AI.Models;

public record AssembledAnimalData
{
    public required string CommonName { get; init; }
    public required string ScientificName { get; init; }
    public required string Slug { get; init; }
    public GbifTaxonomyData? Taxonomy { get; init; }
    public string? ConservationStatusCode { get; init; }
    public string? PopulationTrend { get; init; }
    public string? PopulationEstimate { get; init; }
    public string? DietTypeCode { get; set; }
    public string? ActivityPatternCode { get; set; }
    public string? DomesticationStatusCode { get; init; }
    public List<string> ColourCodes { get; init; } = [];
    public List<string> HabitatTypeCodes { get; init; } = [];
    public List<string> TagCodes { get; init; } = [];
    public decimal? WeightMinKg { get; init; }
    public decimal? WeightMaxKg { get; init; }
    public decimal? LengthMinCm { get; init; }
    public decimal? LengthMaxCm { get; init; }
    public decimal? SpeedMaxKph { get; init; }
    public int? LifespanWildMinYears { get; set; }
    public int? LifespanWildMaxYears { get; set; }
    public int? LifespanCaptivityMinYears { get; set; }
    public int? LifespanCaptivityMaxYears { get; set; }
    public int? GestationMinDays { get; set; }
    public int? GestationMaxDays { get; set; }
    public int? LitterSizeMin { get; set; }
    public int? LitterSizeMax { get; set; }
    public string? AlsoKnownAs { get; init; }
    public string? WikipediaIntroText { get; init; }
    public string? WikipediaDescriptionText { get; init; }
    public string? WikipediaHabitatText { get; init; }
    public string? WikipediaDietText { get; init; }
    public string? WikipediaBehaviourText { get; init; }
    public string? WikipediaConservationText { get; init; }
    public string? WikipediaReproductionText { get; init; }
    public string? GbifHabitatProse { get; init; }
    public string? GbifDietProse { get; init; }
    public string? GbifBehaviourProse { get; init; }
    public string? GbifConservationProse { get; init; }
    public List<string> NativeCountries { get; init; } = [];
    public GbifImageResult? GbifImage { get; init; }
    public string? WikipediaImageUrl { get; init; }
    public string? WikipediaImageLicense { get; init; }
    public string? WikipediaUrl { get; init; }
    public GbifMapMetadata? MapMetadata { get; init; }
    public int? GbifTaxonKey { get; init; }
    public string? GbifCanonicalName { get; init; }
    public string CategorySlug { get; init; } = "wild-mammals";
}
