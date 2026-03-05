namespace Creaturedex.Shared.Responses;

public class AnimalCardDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool IsPet { get; set; }
    public string? ImageUrl { get; set; }
    public string? ConservationStatus { get; set; }
    public int? DifficultyRating { get; set; }
    public bool IsPublished { get; set; }
}
