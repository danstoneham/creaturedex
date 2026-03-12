namespace Creaturedex.AI.Models;

public record GbifAnimalData
{
    public required int TaxonKey { get; init; }
    public required string CanonicalName { get; init; }
    public string? EnglishCommonName { get; init; }
    public GbifTaxonomyData? Taxonomy { get; init; }
    public string? HabitatProse { get; init; }
    public string? DietProse { get; init; }
    public string? BehaviourProse { get; init; }
    public string? PhysicalDescriptionProse { get; init; }
    public string? BreedingProse { get; init; }
    public string? ConservationProse { get; init; }
    public string? DistributionProse { get; init; }
    public string? IucnCategory { get; init; }
    public string? IucnCode { get; init; }
    public string? IucnTaxonId { get; init; }
    /// <summary>True when IUCN status came from a synonym/parent species fallback, not the taxon itself.</summary>
    public bool IucnFromSynonymFallback { get; init; }
    public GbifDistributionData Distribution { get; init; } = new();
    public IReadOnlyList<GbifVernacularName> VernacularNames { get; init; } = [];
    public GbifImageResult? BestImage { get; init; }
    public GbifMapMetadata? MapMetadata { get; init; }
}

public record GbifTaxonomyData
{
    public string Kingdom { get; init; } = "Animalia";
    public string? Phylum { get; init; }
    public string? Class { get; init; }
    public string? Order { get; init; }
    public string? Family { get; init; }
    public string? Genus { get; init; }
    public string? Species { get; init; }
    public string? Subspecies { get; init; }
    public string? ColTaxonId { get; init; }
    public string? Authorship { get; init; }
    public IReadOnlyList<string> Synonyms { get; init; } = [];
}

public record GbifVernacularName
{
    public required string Name { get; init; }
    public string? Language { get; init; }
    public string? Source { get; init; }
}

public record GbifImageResult
{
    public required string Url { get; init; }
    public required string License { get; init; }
    public required string LicenseUrl { get; init; }
    public string? RightsHolder { get; init; }
    public string? Publisher { get; init; }
    public string? Country { get; init; }
    public long? GbifOccurrenceId { get; init; }
    public string? MediaIdentifierMd5 { get; init; }
    public string CachedUrl => MediaIdentifierMd5 != null && GbifOccurrenceId != null
        ? $"https://api.gbif.org/v1/image/cache/occurrence/{GbifOccurrenceId}/media/{MediaIdentifierMd5}"
        : Url;
}

public record GbifMapMetadata
{
    public required int TaxonKey { get; init; }
    public required string TileUrlTemplate { get; init; }
    public int ObservationCount { get; init; }
    public double? MinLat { get; init; }
    public double? MaxLat { get; init; }
    public double? MinLng { get; init; }
    public double? MaxLng { get; init; }
    public int? MinYear { get; init; }
    public int? MaxYear { get; init; }
}

public record GbifDistributionData
{
    public List<string> Countries { get; init; } = [];
    public List<string> Continents { get; init; } = [];
}

public record GbifSpeciesSuggestion
{
    public required int TaxonKey { get; init; }
    public required string ScientificName { get; init; }
    public string? CommonName { get; init; }
    public string? Rank { get; init; }
    public string? Status { get; init; }
    public string? Family { get; init; }
    public string? Order { get; init; }
}
