using System.Globalization;
using System.Text.RegularExpressions;

namespace Creaturedex.AI.Services;

/// <summary>
/// Structured measurements extracted from Wikipedia article prose.
/// All fields are nullable — absent or unconfident data returns null.
/// </summary>
public record ExtractedMeasurements
{
    public decimal? WeightMinKg { get; init; }
    public decimal? WeightMaxKg { get; init; }
    public decimal? LengthMinCm { get; init; }
    public decimal? LengthMaxCm { get; init; }
    public decimal? SpeedMaxKph { get; init; }
    public int? LifespanWildMinYears { get; init; }
    public int? LifespanWildMaxYears { get; init; }
    public int? LifespanCaptivityMinYears { get; init; }
    public int? LifespanCaptivityMaxYears { get; init; }
    public int? GestationMinDays { get; init; }
    public int? GestationMaxDays { get; init; }
    public int? LitterSizeMin { get; init; }
    public int? LitterSizeMax { get; init; }
}

/// <summary>
/// Pure static class — no HTTP calls, no side effects.
/// Extracts numeric measurements from Wikipedia article prose using regex.
/// </summary>
public static partial class WikipediaMeasurementExtractor
{
    // -------------------------------------------------------------------------
    // Shared sub-patterns
    // -------------------------------------------------------------------------

    // Number with optional thousands-comma: "1,000" or "1000" or "1.5"
    private const string N = @"[\d,]+(?:\.\d+)?";

    // Range separator: en-dash, hyphen, minus, " to ", " and "
    private const string S = @"(?:\s*(?:[-–]|to)\s*)";

    // Optional range: captures group 1 (min) and group 2 (max, may be empty)
    // N(S N)?  — used inline below

    // -------------------------------------------------------------------------
    // Weight
    // -------------------------------------------------------------------------

    // "weigh(s|ing) [about] X[-–]Y kg"  /  "mass of X[-–]Y kg"
    [GeneratedRegex(
        @"(?:weigh(?:s|ing|ed)?|(?:body\s+)?mass\s+of)\s+(?:about\s+|approximately\s+|up\s+to\s+)?(" +
        N + @")(?:" + S + @"(" + N + @"))?\s*kg\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex WeightKgPrimaryRegex();

    // "X[-–]Y kg" bare range (catches "body mass of 60–90 kg", "4,000–6,000 kg")
    [GeneratedRegex(
        @"(" + N + @")" + S + @"(" + N + @")\s*kg\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex WeightKgRangeRegex();

    // "single kg value X kg" — last resort bare single
    [GeneratedRegex(
        @"\b(" + N + @")\s*kg\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex WeightKgSingleRegex();

    // "200–440 lb (91–200 kg)"  — kg value in parens wins
    [GeneratedRegex(
        @"(" + N + @")(?:" + S + @"(" + N + @"))?\s*lb\s*\(\s*(" + N + @")(?:" + S + @"(" + N + @"))?\s*kg\s*\)",
        RegexOptions.IgnoreCase)]
    private static partial Regex WeightLbParenKgRegex();

    // "weigh X[-–]Y lb" with no kg companion
    [GeneratedRegex(
        @"(?:weigh(?:s|ing|ed)?|(?:body\s+)?mass\s+of)\s+(?:about\s+|approximately\s+)?(" +
        N + @")(?:" + S + @"(" + N + @"))?\s*lb\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex WeightLbOnlyRegex();

    // -------------------------------------------------------------------------
    // Length
    // -------------------------------------------------------------------------

    // "total/body/head-to-body length [of ...] X[-–]Y cm|m|ft|in"
    // Allows arbitrary words between "length" and the number (e.g. "of adults is")
    [GeneratedRegex(
        @"(?:total|body|head[\s\-]to[\s\-]body|snout[\s\-]to[\s\-]vent|head[\s\-]body)?\s*length(?:\s+of)?(?:[^0-9]{0,40}?)(" +
        N + @")(?:" + S + @"(" + N + @"))?\s*(cm|m\b|ft\b|in\b)",
        RegexOptions.IgnoreCase)]
    private static partial Regex LengthPrimaryRegex();

    // "measure(s|ing) [about] X[-–]Y cm|m|ft|in"
    [GeneratedRegex(
        @"measure(?:s|ing)?\s+(?:about\s+|approximately\s+)?(" +
        N + @")(?:" + S + @"(" + N + @"))?\s*(cm|m\b|ft\b|in\b)",
        RegexOptions.IgnoreCase)]
    private static partial Regex LengthMeasureRegex();

    // -------------------------------------------------------------------------
    // Speed
    // -------------------------------------------------------------------------

    // "speed(s) of/up to X[-–]Y km/h|kph|mph" / "can run X km/h" / "reach speeds of up to X"
    [GeneratedRegex(
        @"(?:(?:top\s+)?speed(?:s)?(?:\s+of)?(?:\s+up\s+to)?|can\s+run(?:\s+at)?|reach(?:es|ing)?\s+(?:speeds?\s+of)?(?:\s+up\s+to)?)\s+(?:about\s+|approximately\s+)?(" +
        N + @")(?:" + S + @"(" + N + @"))?\s*(km/h|kph|mph)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SpeedPrimaryRegex();

    // Bare "X km/h" or "X kph" or "X mph" — used as fallback when speed keyword present
    [GeneratedRegex(
        @"\b(" + N + @")\s*(km/h|kph|mph)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SpeedBareRegex();

    // -------------------------------------------------------------------------
    // Lifespan
    // -------------------------------------------------------------------------

    // Wild: "live(s) X[-–]Y years" / "lives for X[-–]Y years"
    // Does NOT match if followed by "in captivity" within the same short window
    [GeneratedRegex(
        @"live[sd]?\s+(?:for\s+)?(?:about\s+)?(" + N + @")(?:" + S + @"(" + N + @"))?\s*years?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex LifespanLivesRegex();

    // "lifespan of X[-–]Y years" / "lifespan is X[-–]Y years" / "lifespan of the X is Y years"
    // Allow up to 60 chars of arbitrary text between "lifespan" and the number
    // e.g. "lifespan of a white rhinoceros is estimated to be 40–50 years"
    [GeneratedRegex(
        @"lifespan(?:[^0-9]{0,60}?)(" + N + @")(?:" + S + @"(" + N + @"))?\s*years?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex LifespanOfRegex();

    // "life expectancy [of/is] X[-–]Y years" — allow up to 15 chars gap
    [GeneratedRegex(
        @"life\s+expectancy(?:[^0-9]{0,15}?)(" + N + @")(?:" + S + @"(" + N + @"))?\s*years?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex LifeExpectancyRegex();

    // "can live up to X years"
    [GeneratedRegex(
        @"can\s+live\s+(?:up\s+to\s+)?(?:about\s+)?(" + N + @")\s*years?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex LifespanCanLiveRegex();

    // Captivity: "in captivity/managed care/a zoo/aquaria[,] [they/it] [can] live[s] [up to] X[-–]Y years"
    // Group 1 = optional "up to" marker, Group 2 = min, Group 3 = optional max
    [GeneratedRegex(
        @"(?:in\s+captivity|in\s+(?:a\s+)?zoos?|in\s+managed\s+care|under\s+human\s+care|in\s+human\s+custody|in\s+(?:a\s+)?zoological|in\s+aquaria|in\s+aquariums)\b[^.]{0,60}?(?:live[sd]?|survive[sd]?)\s+(?:for\s+)?(up\s+to\s+)?(?:about\s+)?(" +
        N + @")(?:" + S + @"(" + N + @"))?\s*years?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex LifespanCaptivityLivesRegex();

    // -------------------------------------------------------------------------
    // Gestation
    // -------------------------------------------------------------------------

    // "gestation (period) [for a white rhino] [is/of/lasts] [about/approximately] X[-–]Y days|weeks|months"
    // "pregnancy [lasts] X[-–]Y days|weeks|months"
    // Allows up to 50 chars of arbitrary non-digit text between the keyword group and the number,
    // so phrases like "for a white rhino is approximately" are handled correctly.
    [GeneratedRegex(
        @"(?:gestation(?:\s+period)?|pregnancy)(?:[^0-9]{0,50}?)(?:about\s+|approximately\s+|around\s+)?(" +
        N + @")(?:" + S + @"(" + N + @"))?\s*(days?|weeks?|months?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex GestationRegex();

    // -------------------------------------------------------------------------
    // Litter size
    // -------------------------------------------------------------------------

    // "litter(s) [of/consisting of] X[-–]Y [cubs/pups/...]"
    [GeneratedRegex(
        @"litters?\s+(?:of\s+|consist(?:ing)?\s+of\s+|typically\s+)?(" +
        N + @")(?:" + S + @"(" + N + @"))?\s*(?:cubs?|pups?|offspring|young|kittens?|calves?|foals?|lambs?|piglets?|kits?|fawns?|joeys?|whelps?|infants?|babies|neonates?)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex LitterSizePrimaryRegex();

    // "X[-–]Y cubs/pups/... per litter"
    [GeneratedRegex(
        @"(" + N + @")(?:" + S + @"(" + N + @"))?\s*(?:cubs?|pups?|offspring|young|kittens?|calves?|foals?|lambs?|piglets?|kits?|fawns?|joeys?)\s+per\s+litter",
        RegexOptions.IgnoreCase)]
    private static partial Regex LitterSizePerLitterRegex();

    // "typically/usually/normally [gives birth to] X[-–]Y young/cubs/pups/..."
    [GeneratedRegex(
        @"(?:typically|usually|normally)\s+(?:gives?\s+birth\s+to\s+)?(" + N + @")(?:" + S + @"(" + N + @"))?\s*" +
        @"(?:cubs?|pups?|offspring|young|kittens?|calves?|foals?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex LitterSizeTypicallyRegex();

    // "gives birth to a single/one/twin calf/pup/..." — handles word-number births without "typically"
    // Also matches "a single calf is born" style after word normalisation
    [GeneratedRegex(
        @"(?:gives?\s+birth\s+to\s+(?:a\s+)?|(?:a|an)\s+)(" + N + @")(?:" + S + @"(" + N + @"))?\s*" +
        @"(?:cubs?|pups?|offspring|young|kittens?|calves?|foals?|lambs?|fawns?|joeys?|whelps?|infants?|babies|neonates?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex LitterSizeSingleBirthRegex();

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts numeric measurements from Wikipedia article prose.
    /// </summary>
    /// <param name="descriptionText">Text from Appearance/Description section (weight, length, speed).</param>
    /// <param name="reproductionText">Text from Reproduction/Behaviour section (gestation, litter).</param>
    /// <param name="fullText">Concatenation of all available sections (fallback).</param>
    public static ExtractedMeasurements Extract(
        string? descriptionText,
        string? reproductionText,
        string? fullText)
    {
        var desc = Normalise(descriptionText);
        var repro = Normalise(reproductionText);
        var full = Normalise(fullText);

        var (wMin, wMax) = ExtractWeight(desc) ?? ExtractWeight(full) ?? (null, null);
        var (lMin, lMax) = ExtractLength(desc) ?? ExtractLength(full) ?? (null, null);
        var speed = ExtractSpeed(desc) ?? ExtractSpeed(full);

        var (lwMin, lwMax) = ExtractLifespanWild(desc) ?? ExtractLifespanWild(full) ?? (null, null);
        var (lcMin, lcMax) = ExtractLifespanCaptivity(desc) ?? ExtractLifespanCaptivity(full) ?? (null, null);

        var (gMin, gMax) = ExtractGestation(repro) ?? ExtractGestation(full) ?? (null, null);
        var (lsMin, lsMax) = ExtractLitterSize(repro) ?? ExtractLitterSize(full) ?? (null, null);

        return new ExtractedMeasurements
        {
            WeightMinKg = wMin,
            WeightMaxKg = wMax,
            LengthMinCm = lMin,
            LengthMaxCm = lMax,
            SpeedMaxKph = speed,
            LifespanWildMinYears = lwMin,
            LifespanWildMaxYears = lwMax,
            LifespanCaptivityMinYears = lcMin,
            LifespanCaptivityMaxYears = lcMax,
            GestationMinDays = gMin,
            GestationMaxDays = gMax,
            LitterSizeMin = lsMin,
            LitterSizeMax = lsMax,
        };
    }

    // -------------------------------------------------------------------------
    // Weight
    // -------------------------------------------------------------------------

    private static (decimal? Min, decimal? Max)? ExtractWeight(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Priority 1: "200–440 lb (91–200 kg)" — use the kg value in parens
        var m = WeightLbParenKgRegex().Match(text);
        if (m.Success)
        {
            var kMin = ParseNum(m.Groups[3].Value);
            var kMax = ParseNum(m.Groups[4].Value);
            if (kMin.HasValue)
                return (R2(kMin.Value), R2(kMax ?? kMin.Value));
        }

        // Priority 2: "weigh(s|ing) X–Y kg" / "mass of X–Y kg"
        m = WeightKgPrimaryRegex().Match(text);
        if (m.Success)
        {
            var min = ParseNum(m.Groups[1].Value);
            var max = ParseNum(m.Groups[2].Value);
            if (min.HasValue)
                return (R2(min.Value), R2(max ?? min.Value));
        }

        // Priority 3: plain "X–Y kg" range (requires both sides of range)
        m = WeightKgRangeRegex().Match(text);
        if (m.Success)
        {
            var min = ParseNum(m.Groups[1].Value);
            var max = ParseNum(m.Groups[2].Value);
            if (min.HasValue && max.HasValue)
                return (R2(min.Value), R2(max.Value));
        }

        // Priority 4: bare lb with no kg companion
        m = WeightLbOnlyRegex().Match(text);
        if (m.Success)
        {
            var min = ParseNum(m.Groups[1].Value);
            var max = ParseNum(m.Groups[2].Value);
            if (min.HasValue)
                return (R2(LbToKg(min.Value)), R2(LbToKg(max ?? min.Value)));
        }

        // Priority 5: bare single "X kg"
        m = WeightKgSingleRegex().Match(text);
        if (m.Success)
        {
            var val = ParseNum(m.Groups[1].Value);
            if (val.HasValue)
                return (R2(val.Value), R2(val.Value));
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Length
    // -------------------------------------------------------------------------

    private static (decimal? Min, decimal? Max)? ExtractLength(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var m = LengthPrimaryRegex().Match(text);
        if (m.Success)
        {
            var min = ParseNum(m.Groups[1].Value);
            var max = ParseNum(m.Groups[2].Value);
            if (min.HasValue)
            {
                var unit = m.Groups[3].Value;
                return (R2(ToCm(min.Value, unit)), R2(ToCm(max ?? min.Value, unit)));
            }
        }

        m = LengthMeasureRegex().Match(text);
        if (m.Success)
        {
            var min = ParseNum(m.Groups[1].Value);
            var max = ParseNum(m.Groups[2].Value);
            if (min.HasValue)
            {
                var unit = m.Groups[3].Value;
                return (R2(ToCm(min.Value, unit)), R2(ToCm(max ?? min.Value, unit)));
            }
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Speed
    // -------------------------------------------------------------------------

    private static decimal? ExtractSpeed(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Try lead-in keyword pattern first
        var m = SpeedPrimaryRegex().Match(text);
        if (m.Success)
        {
            var min = ParseNum(m.Groups[1].Value);
            var max = ParseNum(m.Groups[2].Value);
            var unit = m.Groups[3].Value;
            var val = max ?? min;
            if (val.HasValue)
                return R2(ToKph(val.Value, unit));
        }

        // Bare fallback if speed keyword is present anywhere in text
        if (HasSpeedKeyword(text))
        {
            // Collect all bare speed matches and take max
            decimal? best = null;
            foreach (Match bm in SpeedBareRegex().Matches(text))
            {
                var val = ParseNum(bm.Groups[1].Value);
                var unit = bm.Groups[2].Value;
                if (val.HasValue)
                {
                    var kph = R2(ToKph(val.Value, unit));
                    if (best == null || kph > best)
                        best = kph;
                }
            }
            if (best.HasValue) return best;
        }

        return null;
    }

    private static bool HasSpeedKeyword(string text) =>
        text.Contains("speed", StringComparison.OrdinalIgnoreCase)
        || text.Contains(" run ", StringComparison.OrdinalIgnoreCase)
        || text.Contains("sprint", StringComparison.OrdinalIgnoreCase)
        || text.Contains("mph", StringComparison.OrdinalIgnoreCase)
        || text.Contains("km/h", StringComparison.OrdinalIgnoreCase)
        || text.Contains("kph", StringComparison.OrdinalIgnoreCase);

    // -------------------------------------------------------------------------
    // Lifespan
    // -------------------------------------------------------------------------

    private static (int? Min, int? Max)? ExtractLifespanWild(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var sentences = Sentences(text);
        int? wMin = null, wMax = null;

        foreach (var s in sentences)
        {
            if (IsCaptivitySentence(s)) continue;

            // "live(s) X–Y years"
            var m = LifespanLivesRegex().Match(s);
            if (m.Success)
            {
                var min = ParseInt(m.Groups[1].Value);
                var max = ParseInt(m.Groups[2].Value);
                if (min.HasValue)
                {
                    wMin = min;
                    wMax = max ?? min;
                    break;
                }
            }

            // "lifespan of/is X–Y years"
            m = LifespanOfRegex().Match(s);
            if (m.Success)
            {
                var min = ParseInt(m.Groups[1].Value);
                var max = ParseInt(m.Groups[2].Value);
                if (min.HasValue)
                {
                    wMin = min;
                    wMax = max ?? min;
                    break;
                }
            }

            // "life expectancy [of] X–Y years"
            m = LifeExpectancyRegex().Match(s);
            if (m.Success)
            {
                var min = ParseInt(m.Groups[1].Value);
                var max = ParseInt(m.Groups[2].Value);
                if (min.HasValue)
                {
                    wMin = min;
                    wMax = max ?? min;
                    break;
                }
            }

            // "can live up to X years"
            m = LifespanCanLiveRegex().Match(s);
            if (m.Success)
            {
                var val = ParseInt(m.Groups[1].Value);
                if (val.HasValue)
                {
                    wMax = val;
                    break;
                }
            }
        }

        if (!wMin.HasValue && !wMax.HasValue) return null;
        return (wMin, wMax);
    }

    private static (int? Min, int? Max)? ExtractLifespanCaptivity(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var sentences = Sentences(text);
        foreach (var s in sentences)
        {
            // Try reversed pattern first on ALL sentences (captivity context is after the number)
            var rm = LifespanCaptivityReversedRegex().Match(s);
            if (rm.Success)
            {
                var first = ParseInt(rm.Groups[1].Value);
                var second = ParseInt(rm.Groups[2].Value);
                if (first.HasValue)
                    return (null, second ?? first);  // "over/up to X years in zoos" — max only
            }

            if (!IsCaptivitySentence(s)) continue;

            // "in captivity ... live(s) [up to] X[-–]Y years"
            // Group 1 = "up to" (present → only max), Group 2 = first number, Group 3 = second number
            var m = LifespanCaptivityLivesRegex().Match(s);
            if (m.Success)
            {
                var isUpTo = m.Groups[1].Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value);
                var first  = ParseInt(m.Groups[2].Value);
                var second = ParseInt(m.Groups[3].Value);
                if (first.HasValue)
                {
                    if (isUpTo)
                        return (null, second ?? first);     // "up to X" — max only
                    return (first, second ?? first);        // "X–Y" — full range
                }
            }

            // "can live up to X years" in captivity context
            m = LifespanCanLiveRegex().Match(s);
            if (m.Success)
            {
                var val = ParseInt(m.Groups[1].Value);
                if (val.HasValue)
                    return (null, val);
            }
        }

        return null;
    }

    // "can live over 40 years in zoos" — captivity context comes AFTER the number
    [GeneratedRegex(
        @"(?:live[sd]?|survive[sd]?)\s+(?:for\s+)?(?:over\s+|up\s+to\s+|about\s+|more\s+than\s+)?(" +
        N + @")(?:" + S + @"(" + N + @"))?\s*years?\s+(?:in\s+(?:zoos?|captivity|aquaria|aquariums)|in\s+managed\s+care|under\s+human\s+care)",
        RegexOptions.IgnoreCase)]
    private static partial Regex LifespanCaptivityReversedRegex();

    private static bool IsCaptivitySentence(string s) =>
        s.Contains("captivity", StringComparison.OrdinalIgnoreCase)
        || s.Contains("captive", StringComparison.OrdinalIgnoreCase)
        || s.Contains("in zoos", StringComparison.OrdinalIgnoreCase)
        || s.Contains("in a zoo", StringComparison.OrdinalIgnoreCase)
        || s.Contains("managed care", StringComparison.OrdinalIgnoreCase)
        || s.Contains("under human care", StringComparison.OrdinalIgnoreCase)
        || s.Contains("human custody", StringComparison.OrdinalIgnoreCase)
        || s.Contains("zoological", StringComparison.OrdinalIgnoreCase)
        || s.Contains("in aquaria", StringComparison.OrdinalIgnoreCase)
        || s.Contains("in aquariums", StringComparison.OrdinalIgnoreCase);

    // -------------------------------------------------------------------------
    // Gestation
    // -------------------------------------------------------------------------

    private static (int? Min, int? Max)? ExtractGestation(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var m = GestationRegex().Match(text);
        if (!m.Success) return null;

        var min = ParseDecimal(m.Groups[1].Value);
        var max = ParseDecimal(m.Groups[2].Value);
        // Normalise unit: strip trailing 's', lowercase
        var unit = m.Groups[3].Value.ToLowerInvariant().TrimEnd('s');

        if (!min.HasValue) return null;

        var minDays = (int)Math.Round(ToDays(min.Value, unit));
        var maxDays = max.HasValue ? (int)Math.Round(ToDays(max.Value, unit)) : minDays;

        return (minDays, maxDays);
    }

    // -------------------------------------------------------------------------
    // Litter size
    // -------------------------------------------------------------------------

    private static (int? Min, int? Max)? ExtractLitterSize(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Pre-process: replace word-numbers with digits so regexes can match them.
        // "a single calf" → "a 1 calf", "twin cubs" → "2 cubs", etc.
        var normalised = NormaliseWordNumbers(text);

        // "litter(s) of X–Y cubs"
        var m = LitterSizePrimaryRegex().Match(normalised);
        if (m.Success)
        {
            var min = ParseInt(m.Groups[1].Value);
            var max = ParseInt(m.Groups[2].Value);
            if (min.HasValue)
                return (min, max ?? min);
        }

        // "X–Y cubs per litter"
        m = LitterSizePerLitterRegex().Match(normalised);
        if (m.Success)
        {
            var min = ParseInt(m.Groups[1].Value);
            var max = ParseInt(m.Groups[2].Value);
            if (min.HasValue)
                return (min, max ?? min);
        }

        // "typically X–Y young"
        m = LitterSizeTypicallyRegex().Match(normalised);
        if (m.Success)
        {
            var min = ParseInt(m.Groups[1].Value);
            var max = ParseInt(m.Groups[2].Value);
            if (min.HasValue)
                return (min, max ?? min);
        }

        // "gives birth to a single calf" / "a single calf is born" (after word normalisation)
        m = LitterSizeSingleBirthRegex().Match(normalised);
        if (m.Success)
        {
            var min = ParseInt(m.Groups[1].Value);
            var max = ParseInt(m.Groups[2].Value);
            if (min.HasValue)
                return (min, max ?? min);
        }

        return null;
    }

    /// <summary>
    /// Replaces English word-numbers used in birth/litter contexts with their digit equivalents,
    /// so that downstream numeric regexes can match them.
    /// Only replaces whole-word occurrences to avoid altering unrelated text.
    /// </summary>
    private static string NormaliseWordNumbers(string text) =>
        Regex.Replace(
            Regex.Replace(
            Regex.Replace(
            Regex.Replace(
            Regex.Replace(
            Regex.Replace(
                text,
                @"\bsingle\b",  "1",   RegexOptions.IgnoreCase),
                @"\bone\b",     "1",   RegexOptions.IgnoreCase),
                @"\btwin(?:s)?\b", "2", RegexOptions.IgnoreCase),
                @"\btwo\b",     "2",   RegexOptions.IgnoreCase),
                @"\bthree\b",   "3",   RegexOptions.IgnoreCase),
                @"\bfour\b",    "4",   RegexOptions.IgnoreCase);

    // -------------------------------------------------------------------------
    // Unit conversions
    // -------------------------------------------------------------------------

    private static decimal LbToKg(decimal lb) => lb / 2.205m;

    private static decimal ToCm(decimal value, string unit) =>
        unit.ToLowerInvariant().Trim() switch
        {
            "m" => value * 100m,
            "ft" => value * 30.48m,
            "in" => value * 2.54m,
            "mm" => value / 10m,
            _ => value      // already cm
        };

    private static decimal ToKph(decimal value, string unit) =>
        unit.ToLowerInvariant() switch
        {
            "mph" => value * 1.609m,
            "m/s" => value * 3.6m,
            _ => value      // km/h or kph
        };

    private static decimal ToDays(decimal value, string normalisedUnit) =>
        normalisedUnit switch
        {
            "week" => value * 7m,
            "month" => value * 30m,
            _ => value      // already days
        };

    // -------------------------------------------------------------------------
    // Parsing helpers
    // -------------------------------------------------------------------------

    private static decimal? ParseNum(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace(",", "");     // remove thousands separator
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
            ? v : null;
    }

    private static decimal? ParseDecimal(string? s) => ParseNum(s);

    private static int? ParseInt(string? s)
    {
        var d = ParseNum(s);
        return d.HasValue ? (int)Math.Round(d.Value) : null;
    }

    // -------------------------------------------------------------------------
    // Text helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Normalises text: replaces Unicode dashes/minus with ASCII hyphen,
    /// collapses whitespace.
    /// </summary>
    private static string Normalise(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text
            .Replace('\u2212', '-')     // MINUS SIGN
            .Replace('\u2013', '-')     // EN DASH
            .Replace('\u2014', '-');    // EM DASH
    }

    /// <summary>Splits text into sentences on ". ", ".\n", "! ", "? ".</summary>
    private static string[] Sentences(string text) =>
        text.Split([". ", ".\n", "! ", "? "], StringSplitOptions.RemoveEmptyEntries);

    private static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
