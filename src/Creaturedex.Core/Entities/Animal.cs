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
}
