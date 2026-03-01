namespace Creaturedex.Shared.Responses;

public class MatcherResultResponse
{
    public List<MatcherRecommendation> Recommendations { get; set; } = [];
}

public class MatcherRecommendation
{
    public AnimalCardDto Animal { get; set; } = null!;
    public string Explanation { get; set; } = string.Empty;
    public int MatchScore { get; set; }
}
