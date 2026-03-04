namespace Creaturedex.Shared.Requests;

public class UpdateAnimalRequest
{
    public string CommonName { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public bool IsPet { get; set; }
    public string? ConservationStatus { get; set; }
    public string? NativeRegion { get; set; }
    public string? Habitat { get; set; }
    public string? Diet { get; set; }
    public string? Lifespan { get; set; }
    public string? SizeInfo { get; set; }
    public string? Behaviour { get; set; }
    public string? FunFacts { get; set; }
    public List<string> Tags { get; set; } = [];
}
