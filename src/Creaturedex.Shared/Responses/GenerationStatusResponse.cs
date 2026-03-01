namespace Creaturedex.Shared.Responses;

public class GenerationStatusResponse
{
    public int TotalAnimals { get; set; }
    public int GeneratedCount { get; set; }
    public int PublishedCount { get; set; }
    public int PendingReviewCount { get; set; }
    public bool IsGenerating { get; set; }
    public string? CurrentAnimal { get; set; }
}
