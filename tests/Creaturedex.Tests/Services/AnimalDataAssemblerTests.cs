using Creaturedex.AI.Services;

namespace Creaturedex.Tests.Services;

public class AnimalDataAssemblerTests
{
    // -------------------------------------------------------------------------
    // PopulationTrend extraction (Task 4)
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractPopulationTrend_InfoboxTakesPriority()
    {
        var result = AnimalDataAssembler.ExtractPopulationTrend("Decreasing", "population is stable");
        Assert.Equal("Decreasing", result);
    }

    [Fact]
    public void ExtractPopulationTrend_InfoboxIncreasing()
    {
        var result = AnimalDataAssembler.ExtractPopulationTrend("Increasing", "");
        Assert.Equal("Increasing", result);
    }

    [Fact]
    public void ExtractPopulationTrend_InfoboxStable()
    {
        var result = AnimalDataAssembler.ExtractPopulationTrend("Stable", "");
        Assert.Equal("Stable", result);
    }

    [Fact]
    public void ExtractPopulationTrend_FallsBackToProse_Declining()
    {
        var result = AnimalDataAssembler.ExtractPopulationTrend(null,
            "The population has declined significantly over the past century.");
        Assert.Equal("Decreasing", result);
    }

    [Fact]
    public void ExtractPopulationTrend_FallsBackToProse_Recovering()
    {
        var result = AnimalDataAssembler.ExtractPopulationTrend(null,
            "Thanks to conservation efforts, the species is recovering.");
        Assert.Equal("Increasing", result);
    }

    [Fact]
    public void ExtractPopulationTrend_FallsBackToProse_Stable()
    {
        var result = AnimalDataAssembler.ExtractPopulationTrend(null,
            "The population remains stable across its range.");
        Assert.Equal("Stable", result);
    }

    [Fact]
    public void ExtractPopulationTrend_NullWhenNoData()
    {
        var result = AnimalDataAssembler.ExtractPopulationTrend(null, "");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractPopulationTrend_NullWhenBothNull()
    {
        var result = AnimalDataAssembler.ExtractPopulationTrend(null, "");
        Assert.Null(result);
    }

    [Fact]
    public void ExtractPopulationTrend_IgnoresUnknownInfobox()
    {
        var result = AnimalDataAssembler.ExtractPopulationTrend("Unknown",
            "The population is declining rapidly.");
        Assert.Equal("Decreasing", result);
    }

    // -------------------------------------------------------------------------
    // ActivityPattern mapping (Task 3)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("The animal is active at dawn and dusk.", "crepuscular")]
    [InlineData("It hunts during the day.", "diurnal")]
    [InlineData("The species hunts at night.", "nocturnal")]
    [InlineData("Active day and night with no fixed pattern.", "cathemeral")]
    [InlineData("Most active at dusk, resting by midday.", "crepuscular")]
    [InlineData("It forages during the day.", "diurnal")]
    [InlineData("It emerges at night to feed.", "nocturnal")]
    public void MapActivityPattern_ExpandedKeywords(string text, string expectedCode)
    {
        var result = AnimalDataAssembler.MapActivityPattern(text);
        Assert.Equal(expectedCode, result);
    }
}
