namespace Creaturedex.Shared.Requests;

public class MatcherRequest
{
    public string LivingSpace { get; set; } = string.Empty;
    public string ExperienceLevel { get; set; } = string.Empty;
    public string TimeAvailable { get; set; } = string.Empty;
    public string BudgetRange { get; set; } = string.Empty;
    public bool HasChildren { get; set; }
    public bool HasOtherPets { get; set; }
    public List<string> Preferences { get; set; } = [];
}
