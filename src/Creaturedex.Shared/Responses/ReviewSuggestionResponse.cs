namespace Creaturedex.Shared.Responses;

public class ReviewSuggestionResponse
{
    public List<ReviewSuggestion> Suggestions { get; set; } = [];
}

public class ReviewSuggestion
{
    public string Field { get; set; } = string.Empty;
    public string Severity { get; set; } = "info"; // "info" or "warning"
    public string Message { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string SuggestedValue { get; set; } = string.Empty;
}
