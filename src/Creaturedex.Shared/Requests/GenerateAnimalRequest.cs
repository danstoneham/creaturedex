namespace Creaturedex.Shared.Requests;

public class GenerateAnimalRequest
{
    public string AnimalName { get; set; } = string.Empty;
    public string? CategorySlug { get; set; }
    public bool? IsPet { get; set; }
    public bool SkipImage { get; set; } = true;
    public int? TaxonKey { get; set; }
    public string? ScientificName { get; set; }
}
