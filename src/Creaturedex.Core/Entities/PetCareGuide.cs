namespace Creaturedex.Core.Entities;

public class PetCareGuide
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public int DifficultyRating { get; set; } = 3;
    public decimal? CostRangeMin { get; set; }
    public decimal? CostRangeMax { get; set; }
    public string CostCurrency { get; set; } = "GBP";
    public string? SpaceRequirement { get; set; }
    public string? TimeCommitment { get; set; }
    public string? Housing { get; set; }
    public string? DietAsPet { get; set; }
    public string? Exercise { get; set; }
    public string? Grooming { get; set; }
    public string? HealthConcerns { get; set; }
    public string? Training { get; set; }
    public bool? GoodWithChildren { get; set; }
    public bool? GoodWithOtherPets { get; set; }
    public string? Temperament { get; set; }
    public string? LegalConsiderations { get; set; }
}
