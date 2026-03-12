using Creaturedex.AI.Services;

namespace Creaturedex.Tests.Services;

public class WikipediaDataFetcherTests
{
    // -------------------------------------------------------------------------
    // LegalProtections extraction (Task 5)
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractLegalProtections_CitesAppendixI()
    {
        var result = WikipediaDataFetcher.ExtractLegalProtections(
            "The species is listed under CITES Appendix I.", null);

        Assert.NotNull(result);
        Assert.Contains("CITES Appendix I", result);
    }

    [Fact]
    public void ExtractLegalProtections_CitesAppendixII()
    {
        var result = WikipediaDataFetcher.ExtractLegalProtections(
            "It is regulated under CITES Appendix II.", null);

        Assert.NotNull(result);
        Assert.Contains("CITES Appendix II", result);
    }

    [Fact]
    public void ExtractLegalProtections_EndangeredSpeciesAct()
    {
        var result = WikipediaDataFetcher.ExtractLegalProtections(
            "The species is protected by the Endangered Species Act.", null);

        Assert.NotNull(result);
        Assert.Contains("Endangered Species Act", result);
    }

    [Fact]
    public void ExtractLegalProtections_EuHabitatsDirective()
    {
        var result = WikipediaDataFetcher.ExtractLegalProtections(
            "It is protected under the EU Habitats Directive.", null);

        Assert.NotNull(result);
        Assert.Contains("EU Habitats Directive", result);
    }

    [Fact]
    public void ExtractLegalProtections_GenericAct()
    {
        var result = WikipediaDataFetcher.ExtractLegalProtections(
            "The animal is protected under the Wildlife Protection Act.", null);

        Assert.NotNull(result);
        Assert.Contains("Wildlife Protection Act", result);
    }

    [Fact]
    public void ExtractLegalProtections_CombinedSources()
    {
        var result = WikipediaDataFetcher.ExtractLegalProtections(
            "Listed under CITES Appendix I.",
            "The species is also protected by the Endangered Species Act.");

        Assert.NotNull(result);
        Assert.Contains("CITES Appendix I", result);
        Assert.Contains("Endangered Species Act", result);
    }

    [Fact]
    public void ExtractLegalProtections_NullWhenNoProtections()
    {
        var result = WikipediaDataFetcher.ExtractLegalProtections(
            "The species is common and widespread.", null);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractLegalProtections_NullWhenBothNull()
    {
        var result = WikipediaDataFetcher.ExtractLegalProtections(null, null);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractLegalProtections_MultipleProtections()
    {
        var result = WikipediaDataFetcher.ExtractLegalProtections(
            "The snow leopard is listed under CITES Appendix I and protected by the Endangered Species Act. " +
            "It is also regulated under the EU Habitats Directive.", null);

        Assert.NotNull(result);
        Assert.Contains("CITES Appendix I", result);
        Assert.Contains("Endangered Species Act", result);
        Assert.Contains("EU Habitats Directive", result);
    }
}
