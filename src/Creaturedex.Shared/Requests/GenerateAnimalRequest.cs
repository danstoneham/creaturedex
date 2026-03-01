namespace Creaturedex.Shared.Requests;

public class GenerateAnimalRequest
{
    public string AnimalName { get; set; } = string.Empty;
    public string? CategorySlug { get; set; }
    public bool? IsPet { get; set; }
}
