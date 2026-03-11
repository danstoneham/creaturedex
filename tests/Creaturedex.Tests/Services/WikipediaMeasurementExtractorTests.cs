using Creaturedex.AI.Services;

namespace Creaturedex.Tests.Services;

public class WikipediaMeasurementExtractorTests
{
    // -------------------------------------------------------------------------
    // Null / empty input
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_NullInput_ReturnsAllNulls()
    {
        var result = WikipediaMeasurementExtractor.Extract(null, null, null);

        Assert.Null(result.WeightMinKg);
        Assert.Null(result.WeightMaxKg);
        Assert.Null(result.LengthMinCm);
        Assert.Null(result.LengthMaxCm);
        Assert.Null(result.SpeedMaxKph);
        Assert.Null(result.LifespanWildMinYears);
        Assert.Null(result.LifespanWildMaxYears);
        Assert.Null(result.LifespanCaptivityMinYears);
        Assert.Null(result.LifespanCaptivityMaxYears);
        Assert.Null(result.GestationMinDays);
        Assert.Null(result.GestationMaxDays);
        Assert.Null(result.LitterSizeMin);
        Assert.Null(result.LitterSizeMax);
    }

    [Fact]
    public void Extract_EmptyInput_ReturnsAllNulls()
    {
        var result = WikipediaMeasurementExtractor.Extract("", "", "");

        Assert.Null(result.WeightMinKg);
        Assert.Null(result.LengthMinCm);
        Assert.Null(result.SpeedMaxKph);
    }

    [Fact]
    public void Extract_NoNumericData_ReturnsAllNulls()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "This animal has no numeric data",
            "It reproduces somehow.",
            "This animal has no numeric data. It reproduces somehow.");

        Assert.Null(result.WeightMinKg);
        Assert.Null(result.GestationMinDays);
    }

    // -------------------------------------------------------------------------
    // Weight — kg range
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_Weight_KgRange_EnDash()
    {
        // Tiger-style: "Males weigh 90–300 kg"
        var result = WikipediaMeasurementExtractor.Extract(
            "The tiger is the largest living cat species. Males weigh 90–300 kg (200–660 lb) and measure 220–330 cm in total length.",
            null, null);

        Assert.Equal(90m, result.WeightMinKg);
        Assert.Equal(300m, result.WeightMaxKg);
    }

    [Fact]
    public void Extract_Weight_KgRange_Hyphen()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Adults weigh 120-180 kg.",
            null, null);

        Assert.Equal(120m, result.WeightMinKg);
        Assert.Equal(180m, result.WeightMaxKg);
    }

    [Fact]
    public void Extract_Weight_KgRange_ToSeparator()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Body mass ranges from 50 to 80 kg.",
            null, null);

        Assert.Equal(50m, result.WeightMinKg);
        Assert.Equal(80m, result.WeightMaxKg);
    }

    [Fact]
    public void Extract_Weight_SingleKgValue()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "The animal weighs about 45 kg.",
            null, null);

        Assert.Equal(45m, result.WeightMinKg);
        Assert.Equal(45m, result.WeightMaxKg);
    }

    [Fact]
    public void Extract_Weight_LbToKgConversion()
    {
        // "200–440 lb (91–200 kg)" — take the kg value from parens
        var result = WikipediaMeasurementExtractor.Extract(
            "Adults weigh 200–440 lb (91–200 kg).",
            null, null);

        Assert.Equal(91m, result.WeightMinKg);
        Assert.Equal(200m, result.WeightMaxKg);
    }

    [Fact]
    public void Extract_Weight_LbOnly_Converts()
    {
        // ~45.35 kg each
        var result = WikipediaMeasurementExtractor.Extract(
            "The dog weighs 100 lb.",
            null, null);

        Assert.NotNull(result.WeightMinKg);
        Assert.True(result.WeightMinKg > 44m && result.WeightMinKg < 46m);
    }

    [Fact]
    public void Extract_Weight_MassOf_Pattern()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "It has a body mass of 60–90 kg.",
            null, null);

        Assert.Equal(60m, result.WeightMinKg);
        Assert.Equal(90m, result.WeightMaxKg);
    }

    [Fact]
    public void Extract_Weight_CommaThousandSeparator()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "The elephant weighs 4,000–6,000 kg.",
            null, null);

        Assert.Equal(4000m, result.WeightMinKg);
        Assert.Equal(6000m, result.WeightMaxKg);
    }

    // -------------------------------------------------------------------------
    // Length
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_Length_CmRange_TotalLength()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Males weigh 90–300 kg (200–660 lb) and measure 220–330 cm in total length.",
            null, null);

        Assert.Equal(220m, result.LengthMinCm);
        Assert.Equal(330m, result.LengthMaxCm);
    }

    [Fact]
    public void Extract_Length_MeterTosCm()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Head-to-body length is 1.5–2.0 m.",
            null, null);

        Assert.Equal(150m, result.LengthMinCm);
        Assert.Equal(200m, result.LengthMaxCm);
    }

    [Fact]
    public void Extract_Length_FeetTosCm()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Body length 5–7 ft.",
            null, null);

        Assert.NotNull(result.LengthMinCm);
        // 5 ft = 152.4 cm
        Assert.True(result.LengthMinCm > 150m && result.LengthMinCm < 155m);
        // 7 ft = 213.36 cm
        Assert.True(result.LengthMaxCm > 210m && result.LengthMaxCm < 216m);
    }

    [Fact]
    public void Extract_Length_InchesToCm()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Body length is 24–36 in.",
            null, null);

        Assert.NotNull(result.LengthMinCm);
        // 24 in = 60.96 cm
        Assert.True(result.LengthMinCm > 60m && result.LengthMinCm < 62m);
    }

    [Fact]
    public void Extract_Length_BodyLengthOf_Pattern()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "The body length of adults is 100–130 cm.",
            null, null);

        Assert.Equal(100m, result.LengthMinCm);
        Assert.Equal(130m, result.LengthMaxCm);
    }

    // -------------------------------------------------------------------------
    // Speed
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_Speed_KmhUpTo()
    {
        // Cheetah-style
        var result = WikipediaMeasurementExtractor.Extract(
            "The cheetah can reach speeds of up to 112 km/h (70 mph).",
            null, null);

        Assert.Equal(112m, result.SpeedMaxKph);
    }

    [Fact]
    public void Extract_Speed_MphConverted()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "It can run at 60 mph.",
            null, null);

        Assert.NotNull(result.SpeedMaxKph);
        // 60 * 1.609 = 96.54
        Assert.True(result.SpeedMaxKph > 96m && result.SpeedMaxKph < 98m);
    }

    [Fact]
    public void Extract_Speed_KphKeyword()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Top speed is 80 kph.",
            null, null);

        Assert.Equal(80m, result.SpeedMaxKph);
    }

    [Fact]
    public void Extract_Speed_RangeTakesMax()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "The animal can reach speeds of 60–80 km/h.",
            null, null);

        Assert.Equal(80m, result.SpeedMaxKph);
    }

    [Fact]
    public void Extract_Speed_CanRunPattern()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "It can run 95 km/h in short bursts.",
            null, null);

        Assert.Equal(95m, result.SpeedMaxKph);
    }

    // -------------------------------------------------------------------------
    // Lifespan — wild
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_Lifespan_WildRange()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Lions typically live 10–14 years in the wild. In captivity, they can live up to 25 years.",
            null, null);

        Assert.Equal(10, result.LifespanWildMinYears);
        Assert.Equal(14, result.LifespanWildMaxYears);
    }

    [Fact]
    public void Extract_Lifespan_CaptivityUpTo()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Lions typically live 10–14 years in the wild. In captivity, they can live up to 25 years.",
            null, null);

        Assert.Null(result.LifespanCaptivityMinYears);
        Assert.Equal(25, result.LifespanCaptivityMaxYears);
    }

    [Fact]
    public void Extract_Lifespan_LifespanOfPattern()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "The lifespan of the species is 8–12 years.",
            null, null);

        Assert.Equal(8, result.LifespanWildMinYears);
        Assert.Equal(12, result.LifespanWildMaxYears);
    }

    [Fact]
    public void Extract_Lifespan_LifeExpectancy()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "Life expectancy is 15–20 years.",
            null, null);

        Assert.Equal(15, result.LifespanWildMinYears);
        Assert.Equal(20, result.LifespanWildMaxYears);
    }

    [Fact]
    public void Extract_Lifespan_CaptivityRange()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "In captivity, the animal lives 18–22 years.",
            null, null);

        Assert.Equal(18, result.LifespanCaptivityMinYears);
        Assert.Equal(22, result.LifespanCaptivityMaxYears);
    }

    [Fact]
    public void Extract_Lifespan_CanLiveUpTo()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            "The species can live up to 30 years.",
            null, null);

        Assert.Equal(30, result.LifespanWildMaxYears);
    }

    // -------------------------------------------------------------------------
    // Gestation
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_Gestation_Days_Single()
    {
        // Lion-style
        var result = WikipediaMeasurementExtractor.Extract(
            null,
            "The gestation period is about 110 days. The female usually gives birth to a litter of 1–4 cubs.",
            null);

        Assert.Equal(110, result.GestationMinDays);
        Assert.Equal(110, result.GestationMaxDays);
    }

    [Fact]
    public void Extract_Gestation_Days_Range()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            null,
            "Gestation period of 93–95 days.",
            null);

        Assert.Equal(93, result.GestationMinDays);
        Assert.Equal(95, result.GestationMaxDays);
    }

    [Fact]
    public void Extract_Gestation_Weeks_Converts()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            null,
            "The gestation period is 9–10 weeks.",
            null);

        // 9 weeks = 63 days, 10 weeks = 70 days
        Assert.Equal(63, result.GestationMinDays);
        Assert.Equal(70, result.GestationMaxDays);
    }

    [Fact]
    public void Extract_Gestation_Months_Converts()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            null,
            "Pregnancy lasts 2–3 months.",
            null);

        // 2 months = 60 days, 3 months = 90 days
        Assert.Equal(60, result.GestationMinDays);
        Assert.Equal(90, result.GestationMaxDays);
    }

    [Fact]
    public void Extract_Gestation_FallsBackToFullText()
    {
        // No reproductionText, but fullText has the data
        var result = WikipediaMeasurementExtractor.Extract(
            null, null,
            "The gestation period is 63 days and litter size is 3–5.");

        Assert.Equal(63, result.GestationMinDays);
        Assert.Equal(63, result.GestationMaxDays);
    }

    // -------------------------------------------------------------------------
    // Litter size
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_LitterSize_Range()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            null,
            "The female usually gives birth to a litter of 1–4 cubs.",
            null);

        Assert.Equal(1, result.LitterSizeMin);
        Assert.Equal(4, result.LitterSizeMax);
    }

    [Fact]
    public void Extract_LitterSize_PerLitter()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            null,
            "Females produce 2–6 pups per litter.",
            null);

        Assert.Equal(2, result.LitterSizeMin);
        Assert.Equal(6, result.LitterSizeMax);
    }

    [Fact]
    public void Extract_LitterSize_TypicallyYoung()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            null,
            "The female typically gives birth to 3–5 young.",
            null);

        Assert.Equal(3, result.LitterSizeMin);
        Assert.Equal(5, result.LitterSizeMax);
    }

    [Fact]
    public void Extract_LitterSize_Single()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            null,
            "Litters consist of 1 cub.",
            null);

        Assert.Equal(1, result.LitterSizeMin);
        Assert.Equal(1, result.LitterSizeMax);
    }

    // -------------------------------------------------------------------------
    // Compound / combined text (Tiger example from spec)
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_Tiger_WeightAndLength_FromDescriptionText()
    {
        const string description =
            "The tiger is the largest living cat species. " +
            "Males weigh 90–300 kg (200–660 lb) and measure 220–330 cm in total length.";

        var result = WikipediaMeasurementExtractor.Extract(description, null, null);

        Assert.Equal(90m, result.WeightMinKg);
        Assert.Equal(300m, result.WeightMaxKg);
        Assert.Equal(220m, result.LengthMinCm);
        Assert.Equal(330m, result.LengthMaxCm);
    }

    [Fact]
    public void Extract_Lion_GestationAndLitter()
    {
        const string reproduction =
            "The gestation period is about 110 days. " +
            "The female usually gives birth to a litter of 1–4 cubs.";

        var result = WikipediaMeasurementExtractor.Extract(null, reproduction, null);

        Assert.Equal(110, result.GestationMinDays);
        Assert.Equal(110, result.GestationMaxDays);
        Assert.Equal(1, result.LitterSizeMin);
        Assert.Equal(4, result.LitterSizeMax);
    }

    [Fact]
    public void Extract_Lion_Lifespan()
    {
        const string description =
            "Lions typically live 10–14 years in the wild. In captivity, they can live up to 25 years.";

        var result = WikipediaMeasurementExtractor.Extract(description, null, null);

        Assert.Equal(10, result.LifespanWildMinYears);
        Assert.Equal(14, result.LifespanWildMaxYears);
        Assert.Null(result.LifespanCaptivityMinYears);
        Assert.Equal(25, result.LifespanCaptivityMaxYears);
    }

    // -------------------------------------------------------------------------
    // Fallback to fullText
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_FallsBackToFullText_ForWeight()
    {
        var result = WikipediaMeasurementExtractor.Extract(
            null, null,
            "Adults weigh 50–70 kg and can run 40 km/h.");

        Assert.Equal(50m, result.WeightMinKg);
        Assert.Equal(70m, result.WeightMaxKg);
        Assert.Equal(40m, result.SpeedMaxKph);
    }

    // -------------------------------------------------------------------------
    // Unicode dashes and minus signs
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_Weight_UnicodeMinus()
    {
        // U+2212 MINUS SIGN
        var result = WikipediaMeasurementExtractor.Extract(
            "Adults weigh 120\u2212180 kg.",
            null, null);

        Assert.Equal(120m, result.WeightMinKg);
        Assert.Equal(180m, result.WeightMaxKg);
    }
}
