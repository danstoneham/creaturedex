namespace Creaturedex.Shared.Responses;

public class SearchResultDto
{
    public AnimalCardDto Animal { get; set; } = null!;
    public double RelevanceScore { get; set; }
    public string? Snippet { get; set; }
}
