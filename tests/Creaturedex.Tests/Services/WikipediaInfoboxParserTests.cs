using Creaturedex.AI.Services;

namespace Creaturedex.Tests.Services;

public class WikipediaInfoboxParserTests
{
    private readonly WikipediaInfoboxParser _parser = new();

    // -------------------------------------------------------------------------
    // Template type detection
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_DetectsSpeciesboxTemplate()
    {
        var wikitext = """
            {{Speciesbox
            | status = EN
            | status_system = IUCN3.1
            | taxon = Panthera tigris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("Speciesbox", result.TemplateType);
    }

    [Fact]
    public void Parse_DetectsPopulationTaxoboxTemplate()
    {
        var wikitext = """
            {{Population taxobox
            | name = Bengal tiger
            | status = EN
            | genus = Panthera
            | species = tigris
            | subspecies = tigris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("Population taxobox", result.TemplateType);
    }

    [Fact]
    public void Parse_DetectsDogBreedTemplate()
    {
        var wikitext = """
            {{Infobox dog breed
            | name = Golden Retriever
            | weight = {{cvt|55|–|75|lb|kg|disp=flip}}
            | coat = Double coat
            | colour = Light to dark golden
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("Infobox dog breed", result.TemplateType);
    }

    [Fact]
    public void Parse_DetectsCatBreedTemplate()
    {
        var wikitext = """
            {{Infobox cat breed
            | name = Maine Coon
            | weight = {{cvt|4|–|8|kg}}
            | coat = Long, shaggy
            | colour = Many colours
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("Infobox cat breed", result.TemplateType);
    }

    [Fact]
    public void Parse_ReturnsNullTemplateType_WhenNoInfoboxFound()
    {
        var wikitext = "This is just some plain article text with no infobox.";

        var result = _parser.Parse(wikitext);

        Assert.Null(result.TemplateType);
    }

    // -------------------------------------------------------------------------
    // IUCN status extraction
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ExtractsIucnStatus_EN()
    {
        var wikitext = """
            {{Speciesbox
            | status = EN
            | status_system = IUCN3.1
            | taxon = Panthera tigris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("EN", result.IucnStatusCode);
    }

    [Fact]
    public void Parse_ExtractsIucnStatus_VU_LionInfbox()
    {
        // Real-world-like lion infobox snippet
        var wikitext = """
            {{Speciesbox
            | name = Lion
            | image = Lion waiting in Namibia.jpg
            | status = VU
            | status_system = IUCN3.1
            | status_ref = <ref name="iucn">IUCN reference</ref>
            | trend = decreasing
            | taxon = Panthera leo
            | authority = (Linnaeus, 1758)
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("VU", result.IucnStatusCode);
    }

    [Fact]
    public void Parse_ExtractsIucnStatus_LC()
    {
        var wikitext = """
            {{Speciesbox
            | status = LC
            | status_system = IUCN3.1
            | taxon = Canis lupus familiaris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("LC", result.IucnStatusCode);
    }

    [Fact]
    public void Parse_ExtractsIucnStatus_FromPopulationTaxobox()
    {
        var wikitext = """
            {{Population taxobox
            | name = Bengal tiger
            | status = EN
            | status_system = IUCN3.1
            | genus = Panthera
            | species = tigris
            | subspecies = tigris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("EN", result.IucnStatusCode);
    }

    [Fact]
    public void Parse_ReturnsNullIucnStatus_WhenFieldMissing()
    {
        var wikitext = """
            {{Speciesbox
            | taxon = Canis lupus
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Null(result.IucnStatusCode);
    }

    [Fact]
    public void Parse_IucnStatus_IsUppercase()
    {
        var wikitext = """
            {{Speciesbox
            | status = en
            | taxon = Panthera tigris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("EN", result.IucnStatusCode);
    }

    // -------------------------------------------------------------------------
    // Population trend extraction
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ExtractsPopulationTrend_Decreasing()
    {
        var wikitext = """
            {{Speciesbox
            | status = VU
            | trend = decreasing
            | taxon = Panthera leo
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("decreasing", result.PopulationTrend);
    }

    [Fact]
    public void Parse_ExtractsPopulationTrend_Stable()
    {
        var wikitext = """
            {{Speciesbox
            | status = LC
            | trend = stable
            | taxon = Canis lupus
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("stable", result.PopulationTrend);
    }

    // -------------------------------------------------------------------------
    // Weight extraction — cvt template (lb → kg)
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ExtractsWeight_FromCvtTemplate_LbToKg()
    {
        var wikitext = """
            {{Infobox dog breed
            | name = Golden Retriever
            | weight = {{cvt|55|–|75|lb|kg|disp=flip}}
            | coat = Double coat
            | colour = Light to dark golden
            }}
            """;

        var result = _parser.Parse(wikitext);

        // 55 lb ÷ 2.205 ≈ 24.9 kg, 75 lb ÷ 2.205 ≈ 34.0 kg
        Assert.NotNull(result.WeightMinKg);
        Assert.NotNull(result.WeightMaxKg);
        Assert.InRange(result.WeightMinKg!.Value, 24m, 26m);
        Assert.InRange(result.WeightMaxKg!.Value, 33m, 35m);
    }

    [Fact]
    public void Parse_ExtractsWeight_FromCvtTemplate_KgDirect()
    {
        var wikitext = """
            {{Infobox cat breed
            | name = Maine Coon
            | weight = {{cvt|4|–|8|kg}}
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.NotNull(result.WeightMinKg);
        Assert.NotNull(result.WeightMaxKg);
        Assert.Equal(4m, result.WeightMinKg!.Value);
        Assert.Equal(8m, result.WeightMaxKg!.Value);
    }

    [Fact]
    public void Parse_ExtractsWeight_FromRangeText_Kg()
    {
        var wikitext = """
            {{Speciesbox
            | status = VU
            | weight = 180–258 kg
            | taxon = Panthera tigris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.NotNull(result.WeightMinKg);
        Assert.NotNull(result.WeightMaxKg);
        Assert.Equal(180m, result.WeightMinKg!.Value);
        Assert.Equal(258m, result.WeightMaxKg!.Value);
    }

    [Fact]
    public void Parse_ExtractsWeight_FromRangeText_Lb_ConvertsToKg()
    {
        var wikitext = """
            {{Speciesbox
            | status = LC
            | weight = 100–150 lb
            | taxon = Canis lupus
            }}
            """;

        var result = _parser.Parse(wikitext);

        // 100 lb ≈ 45.4 kg, 150 lb ≈ 68.0 kg
        Assert.NotNull(result.WeightMinKg);
        Assert.NotNull(result.WeightMaxKg);
        Assert.InRange(result.WeightMinKg!.Value, 45m, 46m);
        Assert.InRange(result.WeightMaxKg!.Value, 67m, 69m);
    }

    // -------------------------------------------------------------------------
    // Length extraction
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ExtractsLength_FromRangeText_Cm()
    {
        var wikitext = """
            {{Speciesbox
            | status = VU
            | length = 180–250 cm
            | taxon = Panthera tigris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.NotNull(result.LengthMinCm);
        Assert.NotNull(result.LengthMaxCm);
        Assert.Equal(180m, result.LengthMinCm!.Value);
        Assert.Equal(250m, result.LengthMaxCm!.Value);
    }

    [Fact]
    public void Parse_ExtractsLength_FromCvtTemplate_InToCm()
    {
        var wikitext = """
            {{Infobox dog breed
            | name = Golden Retriever
            | maleheight = {{cvt|21.5|–|24|in|cm}}
            }}
            """;

        var result = _parser.Parse(wikitext);

        // 21.5 in × 2.54 ≈ 54.6 cm, 24 in × 2.54 ≈ 61.0 cm
        Assert.NotNull(result.LengthMinCm);
        Assert.NotNull(result.LengthMaxCm);
        Assert.InRange(result.LengthMinCm!.Value, 54m, 56m);
        Assert.InRange(result.LengthMaxCm!.Value, 60m, 62m);
    }

    [Fact]
    public void Parse_ExtractsLength_FromRangeText_M_ConvertsToCm()
    {
        var wikitext = """
            {{Speciesbox
            | status = LC
            | length = 1.5–2.0 m
            | taxon = Canis lupus
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.NotNull(result.LengthMinCm);
        Assert.NotNull(result.LengthMaxCm);
        Assert.Equal(150m, result.LengthMinCm!.Value);
        Assert.Equal(200m, result.LengthMaxCm!.Value);
    }

    // -------------------------------------------------------------------------
    // Litter size — val template
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ExtractsLitterSize_FromValTemplate()
    {
        var wikitext = """
            {{Infobox dog breed
            | name = Golden Retriever
            | litter_size = {{val|7.2|2.7}}
            }}
            """;

        var result = _parser.Parse(wikitext);

        // Mean 7.2 ± 2.7 → min ≈ 5, max ≈ 10 (or just 7 rounded)
        Assert.NotNull(result.LitterSizeMin);
        Assert.NotNull(result.LitterSizeMax);
    }

    [Fact]
    public void Parse_ExtractsLitterSize_FromRangeText()
    {
        var wikitext = """
            {{Speciesbox
            | litter_size = 2–5
            | taxon = Panthera leo
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal(2, result.LitterSizeMin);
        Assert.Equal(5, result.LitterSizeMax);
    }

    [Fact]
    public void Parse_ExtractsLitterSize_SingleValue()
    {
        var wikitext = """
            {{Speciesbox
            | litter_size = 1
            | taxon = Equus quagga
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal(1, result.LitterSizeMin);
        Assert.Equal(1, result.LitterSizeMax);
    }

    // -------------------------------------------------------------------------
    // Coat and colour (domestic breed fields)
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ExtractsCoat_FromDogBreedInfobox()
    {
        var wikitext = """
            {{Infobox dog breed
            | name = Golden Retriever
            | coat = Dense double coat with water-resistant outer coat
            | colour = Light to dark golden
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("Dense double coat with water-resistant outer coat", result.Coat);
        Assert.Equal("Light to dark golden", result.Colour);
    }

    [Fact]
    public void Parse_ExtractsColour_StripsWikiLinks()
    {
        var wikitext = """
            {{Infobox dog breed
            | colour = [[Golden (color)|golden]], [[cream]], [[red]]
            }}
            """;

        var result = _parser.Parse(wikitext);

        // Should strip [[...]] wiki link syntax
        Assert.NotNull(result.Colour);
        Assert.DoesNotContain("[[", result.Colour!);
        Assert.DoesNotContain("]]", result.Colour!);
    }

    // -------------------------------------------------------------------------
    // Ref tag stripping
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_StripsRefTags_BeforeParsingStatus()
    {
        var wikitext = """
            {{Speciesbox
            | status = CR<ref name="iucn2023">IUCN Red List 2023</ref>
            | taxon = Diceros bicornis
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("CR", result.IucnStatusCode);
    }

    [Fact]
    public void Parse_StripsRefTags_BeforeParsingWeight()
    {
        var wikitext = """
            {{Speciesbox
            | weight = 800–2200 kg<ref>Smith et al. 2020</ref>
            | taxon = Elephas maximus
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal(800m, result.WeightMinKg);
        Assert.Equal(2200m, result.WeightMaxKg);
    }

    [Fact]
    public void Parse_StripsRefTagsSelfClosing()
    {
        var wikitext = """
            {{Speciesbox
            | status = NT<ref name="iucn" />
            | taxon = Loxodonta africana
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("NT", result.IucnStatusCode);
    }

    // -------------------------------------------------------------------------
    // Range parsing helper (via integration)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("180–258 kg", 180, 258)]
    [InlineData("180-258 kg", 180, 258)]       // ASCII hyphen
    [InlineData("180 – 258 kg", 180, 258)]     // spaced en-dash
    [InlineData("100 to 150 kg", 100, 150)]    // "to" form
    public void ParseRange_HandlesVariousRangeSyntaxes(string value, decimal expectedMin, decimal expectedMax)
    {
        var result = WikipediaInfoboxParser.ParseRange(value);

        Assert.NotNull(result);
        Assert.Equal(expectedMin, result.Value.Min);
        Assert.Equal(expectedMax, result.Value.Max);
    }

    [Fact]
    public void ParseRange_ReturnsSingleValue_WhenNoRange()
    {
        var result = WikipediaInfoboxParser.ParseRange("500 kg");

        Assert.NotNull(result);
        Assert.Equal(500m, result.Value.Min);
        Assert.Equal(500m, result.Value.Max);
    }

    [Fact]
    public void ParseRange_ReturnsNull_WhenNoNumbers()
    {
        var result = WikipediaInfoboxParser.ParseRange("unknown");

        Assert.Null(result);
    }

    [Fact]
    public void ParseRange_ReturnsNull_WhenValueIsNull()
    {
        var result = WikipediaInfoboxParser.ParseRange(null);

        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // Missing fields — graceful null returns
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ReturnsAllNulls_WhenWikitextIsEmpty()
    {
        var result = _parser.Parse("");

        Assert.Null(result.IucnStatusCode);
        Assert.Null(result.WeightMinKg);
        Assert.Null(result.WeightMaxKg);
        Assert.Null(result.LengthMinCm);
        Assert.Null(result.LengthMaxCm);
        Assert.Null(result.LitterSizeMin);
        Assert.Null(result.LitterSizeMax);
        Assert.Null(result.Coat);
        Assert.Null(result.Colour);
        Assert.Null(result.TemplateType);
    }

    [Fact]
    public void Parse_ReturnsPartialData_WhenOnlySomeFieldsPresent()
    {
        var wikitext = """
            {{Speciesbox
            | status = EN
            | taxon = Panthera tigris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("EN", result.IucnStatusCode);
        Assert.Null(result.WeightMinKg);
        Assert.Null(result.LengthMinCm);
        Assert.Null(result.Coat);
    }

    // -------------------------------------------------------------------------
    // Whitespace tolerance
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_HandlesExtraWhitespace_InFieldValues()
    {
        var wikitext = """
            {{Speciesbox
            |   status   =   EN
            | taxon = Panthera tigris
            }}
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("EN", result.IucnStatusCode);
    }

    // -------------------------------------------------------------------------
    // Real-world-like Bengal tiger infobox
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_BengalTigerInfobox_ExtractsCorrectData()
    {
        var wikitext = """
            {{Population taxobox
            | name = Bengal tiger
            | image = Bengal Tiger Ranthambhore.jpg
            | status = EN
            | status_system = IUCN3.1
            | status_ref = <ref name="iucn">{{cite iucn|author=...}}</ref>
            | trend = decreasing
            | genus = Panthera
            | species = tigris
            | subspecies = tigris
            | population = Bengal
            }}
            Bengal tigers weigh 180–258 kg and measure up to 3.1 m in length.
            """;

        var result = _parser.Parse(wikitext);

        Assert.Equal("Population taxobox", result.TemplateType);
        Assert.Equal("EN", result.IucnStatusCode);
        Assert.Equal("decreasing", result.PopulationTrend);
    }

    // -------------------------------------------------------------------------
    // Speed extraction
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ExtractsSpeed_FromCvtTemplate_MphToKph()
    {
        var wikitext = """
            {{Speciesbox
            | status = VU
            | speed = {{cvt|50|mi/h|km/h}}
            | taxon = Acinonyx jubatus
            }}
            """;

        var result = _parser.Parse(wikitext);

        // 50 mph × 1.609 ≈ 80.5 kph
        Assert.NotNull(result.SpeedMaxKph);
        Assert.InRange(result.SpeedMaxKph!.Value, 80m, 82m);
    }
}
