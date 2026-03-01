using Creaturedex.Core.Entities;

namespace Creaturedex.Shared.Responses;

public class AnimalProfileResponse
{
    public Animal Animal { get; set; } = null!;
    public Taxonomy? Taxonomy { get; set; }
    public PetCareGuide? CareGuide { get; set; }
    public List<AnimalCharacteristic> Characteristics { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public bool IsReviewed { get; set; }
}
