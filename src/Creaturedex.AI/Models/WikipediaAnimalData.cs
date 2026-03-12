namespace Creaturedex.AI.Models;

using Creaturedex.AI.Services;

public record WikipediaAnimalData
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public WikipediaInfoboxData? Infobox { get; init; }
    public string? IntroText { get; init; }
    public string? AppearanceText { get; init; }
    public string? HabitatText { get; init; }
    public string? DietText { get; init; }
    public string? BehaviourText { get; init; }
    public string? ConservationText { get; init; }
    public string? ReproductionText { get; init; }
    public List<string> AlsoKnownAs { get; init; } = [];
    public string? ImageUrl { get; init; }
    public string? ImageLicense { get; init; }
    public string? PopulationEstimate { get; init; }
    public string? LegalProtections { get; init; }
}
