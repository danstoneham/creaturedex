namespace Creaturedex.Core.Entities;

public class Animal
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Guid? TaxonomyId { get; set; }
    public bool IsPet { get; set; }
    public string? ImageUrl { get; set; }
    public string? ConservationStatus { get; set; }
    public string? NativeRegion { get; set; }
    public string? Habitat { get; set; }
    public string? Diet { get; set; }
    public string? Lifespan { get; set; }
    public string? SizeInfo { get; set; }
    public string? Behaviour { get; set; }
    public string? FunFacts { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int Version { get; set; } = 1;

    // GBIF identifiers
    public int? GbifTaxonKey { get; set; }
    public string? GbifCanonicalName { get; set; }

    // Map metadata
    public string? MapTileUrlTemplate { get; set; }
    public int? MapObservationCount { get; set; }
    public double? MapMinLat { get; set; }
    public double? MapMaxLat { get; set; }
    public double? MapMinLng { get; set; }
    public double? MapMaxLng { get; set; }

    // Image attribution
    public string? ImageLicense { get; set; }
    public string? ImageRightsHolder { get; set; }
    public string? ImageSource { get; set; }

    // Wikipedia reference
    public string? WikipediaUrl { get; set; }

    // Conservation (structured)
    public string? ConservationStatusCode { get; set; }
    public string? PopulationTrend { get; set; }
    public string? PopulationEstimate { get; set; }

    // Diet (structured)
    public string? DietTypeCode { get; set; }

    // Activity
    public string? ActivityPatternCode { get; set; }

    // Domestication
    public string? DomesticationStatusCode { get; set; }

    // Physical measurements (metric)
    public decimal? WeightMinKg { get; set; }
    public decimal? WeightMaxKg { get; set; }
    public decimal? LengthMinCm { get; set; }
    public decimal? LengthMaxCm { get; set; }
    public decimal? SpeedMaxKph { get; set; }

    // Lifespan (structured)
    public int? LifespanWildMinYears { get; set; }
    public int? LifespanWildMaxYears { get; set; }
    public int? LifespanCaptivityMinYears { get; set; }
    public int? LifespanCaptivityMaxYears { get; set; }

    // Reproduction
    public int? GestationMinDays { get; set; }
    public int? GestationMaxDays { get; set; }
    public int? LitterSizeMin { get; set; }
    public int? LitterSizeMax { get; set; }

    // Additional structured text
    public string? AlsoKnownAs { get; set; }
    public string? DistinguishingFeatures { get; set; }
    public string? LegalProtections { get; set; }
    public string? ColoursJson { get; set; }
    public string? HabitatTypesJson { get; set; }

    // Data source tracking
    public int DataSourceVersion { get; set; } = 1;
    public DateTime? LastDataFetchAt { get; set; }
}
