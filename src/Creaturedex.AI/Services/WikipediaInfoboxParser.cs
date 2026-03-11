using System.Text.RegularExpressions;

namespace Creaturedex.AI.Services;

public record WikipediaInfoboxData
{
    public string? IucnStatusCode { get; init; }        // EX, EW, CR, EN, VU, NT, LC, DD
    public string? PopulationTrend { get; init; }        // Increasing, Stable, Decreasing
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
    public string? Coat { get; init; }
    public string? Colour { get; init; }
    public string? TemplateType { get; init; }          // "Speciesbox", "Infobox dog breed", etc.
}

/// <summary>
/// Pure parsing class — no HTTP calls, no side effects.
/// Extracts structured data from Wikipedia infobox wikitext.
/// </summary>
public partial class WikipediaInfoboxParser
{
    // -------------------------------------------------------------------------
    // Generated regex patterns
    // -------------------------------------------------------------------------

    [GeneratedRegex(@"\{\{([^|}\n]+)", RegexOptions.None)]
    private static partial Regex TemplateNameRegex();

    [GeneratedRegex(@"\|\s*(\w+)\s*=\s*([^\|]*?)(?=\n\s*\||\n\s*\}\}|$)", RegexOptions.Singleline)]
    private static partial Regex FieldRegex();

    [GeneratedRegex(@"<ref[^>]*/\s*>|<ref[^>]*>.*?</ref>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex RefTagRegex();

    [GeneratedRegex(@"\[\[(?:[^\]|]*\|)?([^\]|]+)\]\]")]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(@"\{\{cvt\|([^}]+)\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex CvtTemplateRegex();

    [GeneratedRegex(@"\{\{val\|([^}]+)\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex ValTemplateRegex();

    // Range: "180–258 kg", "180-258 kg", "180 – 258 kg", "100 to 150 kg"
    [GeneratedRegex(@"([\d.]+)\s*(?:–|-|to)\s*([\d.]+)")]
    private static partial Regex RangeRegex();

    // Single numeric value with optional unit
    [GeneratedRegex(@"([\d.]+)\s*([a-zA-Z/]+)?")]
    private static partial Regex SingleValueRegex();

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses wikitext and returns structured data extracted from the first infobox template.
    /// All fields are nullable — missing fields return null.
    /// </summary>
    public WikipediaInfoboxData Parse(string wikitext)
    {
        if (string.IsNullOrWhiteSpace(wikitext))
            return new WikipediaInfoboxData();

        var infobox = ExtractInfoboxBlock(wikitext);
        if (infobox == null)
            return new WikipediaInfoboxData();

        var templateType = ExtractTemplateName(infobox);
        var fields = ParseFields(infobox);

        return new WikipediaInfoboxData
        {
            TemplateType = templateType,
            IucnStatusCode = ParseStatus(fields),
            PopulationTrend = GetField(fields, "trend"),
            WeightMinKg = ParseWeightMin(fields),
            WeightMaxKg = ParseWeightMax(fields),
            LengthMinCm = ParseLengthMin(fields),
            LengthMaxCm = ParseLengthMax(fields),
            SpeedMaxKph = ParseSpeed(fields),
            LifespanWildMinYears = ParseLifespanWildMin(fields),
            LifespanWildMaxYears = ParseLifespanWildMax(fields),
            LifespanCaptivityMinYears = ParseLifespanCaptivityMin(fields),
            LifespanCaptivityMaxYears = ParseLifespanCaptivityMax(fields),
            GestationMinDays = ParseGestationMin(fields),
            GestationMaxDays = ParseGestationMax(fields),
            LitterSizeMin = ParseLitterSizeMin(fields),
            LitterSizeMax = ParseLitterSizeMax(fields),
            Coat = ParseText(fields, "coat"),
            Colour = ParseText(fields, "colour") ?? ParseText(fields, "color"),
        };
    }

    // -------------------------------------------------------------------------
    // Infobox block extraction (brace-matching)
    // -------------------------------------------------------------------------

    private static string? ExtractInfoboxBlock(string wikitext)
    {
        // Find the opening {{ of any infobox/taxobox/speciesbox
        var start = wikitext.IndexOf("{{", StringComparison.Ordinal);
        if (start < 0) return null;

        var depth = 0;
        var i = start;

        while (i < wikitext.Length)
        {
            if (i + 1 < wikitext.Length && wikitext[i] == '{' && wikitext[i + 1] == '{')
            {
                depth++;
                i += 2;
            }
            else if (i + 1 < wikitext.Length && wikitext[i] == '}' && wikitext[i + 1] == '}')
            {
                depth--;
                i += 2;
                if (depth == 0)
                    return wikitext[start..i];
            }
            else
            {
                i++;
            }
        }

        // Unclosed template — return everything from start
        return wikitext[start..];
    }

    // -------------------------------------------------------------------------
    // Template name extraction
    // -------------------------------------------------------------------------

    private static string? ExtractTemplateName(string infobox)
    {
        // {{Template Name\n or {{Template Name|
        var match = TemplateNameRegex().Match(infobox);
        if (!match.Success) return null;

        return match.Groups[1].Value.Trim();
    }

    // -------------------------------------------------------------------------
    // Field parsing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses "| field = value" lines from the infobox block.
    /// Handles nested templates inside values by using a line-by-line approach
    /// with continuation for nested braces.
    /// </summary>
    private static Dictionary<string, string> ParseFields(string infobox)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Split by pipe characters that start a line (field separator)
        // We need to be careful about pipes inside nested {{ }} templates
        var lines = SplitIntoFieldLines(infobox);

        foreach (var line in lines)
        {
            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = line[..eqIdx].Trim().TrimStart('|').Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;

            var rawValue = line[(eqIdx + 1)..].Trim();

            // Strip trailing }} that is the template close (not part of a nested template).
            // Only strip if the value doesn't have more {{ openers than }} closers.
            rawValue = StripTrailingTemplateClose(rawValue);

            // Strip <ref>...</ref> tags
            rawValue = RefTagRegex().Replace(rawValue, "").Trim();

            if (!string.IsNullOrWhiteSpace(key))
                fields[key] = rawValue;
        }

        return fields;
    }

    /// <summary>
    /// Splits the infobox into "field lines" where each entry is one "| key = value" segment.
    /// Tracks brace depth to avoid splitting on pipes inside nested templates.
    /// </summary>
    private static List<string> SplitIntoFieldLines(string infobox)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;
        var i = 0;

        while (i < infobox.Length)
        {
            var ch = infobox[i];

            if (i + 1 < infobox.Length && ch == '{' && infobox[i + 1] == '{')
            {
                depth++;
                current.Append("{{");
                i += 2;
                continue;
            }

            if (i + 1 < infobox.Length && ch == '}' && infobox[i + 1] == '}')
            {
                depth--;
                current.Append("}}");
                i += 2;
                continue;
            }

            // A pipe at depth 0 (or 1, since we're inside the outer {{ }}) that starts a new field
            if (ch == '|' && depth <= 1)
            {
                var seg = current.ToString().Trim();
                if (!string.IsNullOrEmpty(seg))
                    result.Add(seg);
                current.Clear();
                current.Append('|');
                i++;
                continue;
            }

            current.Append(ch);
            i++;
        }

        var last = current.ToString().Trim();
        if (!string.IsNullOrEmpty(last))
            result.Add(last);

        return result;
    }

    // -------------------------------------------------------------------------
    // Field accessors
    // -------------------------------------------------------------------------

    private static string? GetField(Dictionary<string, string> fields, string name)
    {
        return fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    /// <summary>
    /// Extracts a field from the raw infobox string by field name.
    /// Public for use in tests or external parsing.
    /// </summary>
    public static string? ExtractField(string infobox, string fieldName)
    {
        var escapedName = Regex.Escape(fieldName);
        var pattern = $@"\|\s*{escapedName}\s*=\s*([^\|]*?)(?=\n\s*\||$)";
        var match = Regex.Match(infobox, pattern,
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var value = match.Groups[1].Value.Trim();
        value = RefTagRegex().Replace(value, "").Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // -------------------------------------------------------------------------
    // IUCN status
    // -------------------------------------------------------------------------

    private static string? ParseStatus(Dictionary<string, string> fields)
    {
        var raw = GetField(fields, "status");
        if (raw == null) return null;

        // Take only the first word (ignore any trailing markup)
        var word = raw.Split(' ', '\t', '\n')[0].Trim();
        return string.IsNullOrEmpty(word) ? null : word.ToUpperInvariant();
    }

    // -------------------------------------------------------------------------
    // Weight
    // -------------------------------------------------------------------------

    private static decimal? ParseWeightMin(Dictionary<string, string> fields)
    {
        var (min, _, unit) = ParseMeasurement(fields, ["weight", "maleweight"]);
        if (min == null) return null;
        // If unit is already kg (or cvt template already converted), no further conversion needed.
        // For plain range text with a non-kg unit, convert now.
        var converted = NeedsWeightConversion(unit) ? ConvertToKg(min.Value, unit) : min.Value;
        return RoundToTwo(converted);
    }

    private static decimal? ParseWeightMax(Dictionary<string, string> fields)
    {
        var (_, max, unit) = ParseMeasurement(fields, ["weight", "maleweight"]);
        if (max == null) return null;
        var converted = NeedsWeightConversion(unit) ? ConvertToKg(max.Value, unit) : max.Value;
        return RoundToTwo(converted);
    }

    /// <summary>
    /// Returns true when the unit from plain range text (not cvt) needs converting to kg.
    /// CVT templates already output the target unit, so their results don't need further conversion.
    /// </summary>
    private static bool NeedsWeightConversion(string? unit)
    {
        if (unit == null) return false;
        var u = unit.ToLowerInvariant();
        return u is "lb" or "lbs" or "g";
    }

    // -------------------------------------------------------------------------
    // Length
    // -------------------------------------------------------------------------

    private static decimal? ParseLengthMin(Dictionary<string, string> fields)
    {
        var (min, _, unit) = ParseMeasurement(fields, ["length", "maleheight", "height"]);
        if (min == null) return null;
        var converted = NeedsLengthConversion(unit) ? ConvertToCm(min.Value, unit) : min.Value;
        return RoundToTwo(converted);
    }

    private static decimal? ParseLengthMax(Dictionary<string, string> fields)
    {
        var (_, max, unit) = ParseMeasurement(fields, ["length", "maleheight", "height"]);
        if (max == null) return null;
        var converted = NeedsLengthConversion(unit) ? ConvertToCm(max.Value, unit) : max.Value;
        return RoundToTwo(converted);
    }

    private static bool NeedsLengthConversion(string? unit)
    {
        if (unit == null) return false;
        var u = unit.ToLowerInvariant();
        return u is "in" or "ft" or "m" or "mm";
    }

    // -------------------------------------------------------------------------
    // Speed
    // -------------------------------------------------------------------------

    private static decimal? ParseSpeed(Dictionary<string, string> fields)
    {
        var raw = GetField(fields, "speed");
        if (raw == null) return null;

        // Try cvt template first
        var cvt = TryParseCvtTemplate(raw);
        if (cvt != null)
        {
            var (min, max, unit) = cvt.Value;
            var val = max ?? min;
            if (val == null) return null;
            return RoundToTwo(ConvertSpeedToKph(val.Value, unit));
        }

        // Fall back to range text
        var range = ParseRange(raw);
        if (range == null) return null;

        var speedVal = range.Value.Max;
        var unit2 = range.Value.Unit;
        return RoundToTwo(ConvertSpeedToKph(speedVal, unit2));
    }

    // -------------------------------------------------------------------------
    // Lifespan
    // -------------------------------------------------------------------------

    private static int? ParseLifespanWildMin(Dictionary<string, string> fields)
    {
        var (min, _, _) = ParseIntMeasurement(fields, ["lifespan"]);
        return min;
    }

    private static int? ParseLifespanWildMax(Dictionary<string, string> fields)
    {
        var (_, max, _) = ParseIntMeasurement(fields, ["lifespan"]);
        return max;
    }

    private static int? ParseLifespanCaptivityMin(Dictionary<string, string> fields)
    {
        var (min, _, _) = ParseIntMeasurement(fields, ["lifespan_captivity", "captive_lifespan"]);
        return min;
    }

    private static int? ParseLifespanCaptivityMax(Dictionary<string, string> fields)
    {
        var (_, max, _) = ParseIntMeasurement(fields, ["lifespan_captivity", "captive_lifespan"]);
        return max;
    }

    // -------------------------------------------------------------------------
    // Gestation
    // -------------------------------------------------------------------------

    private static int? ParseGestationMin(Dictionary<string, string> fields)
    {
        var (min, _, _) = ParseIntMeasurement(fields, ["gestation", "gestation_period"]);
        return min;
    }

    private static int? ParseGestationMax(Dictionary<string, string> fields)
    {
        var (_, max, _) = ParseIntMeasurement(fields, ["gestation", "gestation_period"]);
        return max;
    }

    // -------------------------------------------------------------------------
    // Litter size
    // -------------------------------------------------------------------------

    private static int? ParseLitterSizeMin(Dictionary<string, string> fields)
    {
        var raw = GetField(fields, "litter_size") ?? GetField(fields, "litter_avg");
        if (raw == null) return null;

        var (min, _) = ParseLitterSize(raw);
        return min;
    }

    private static int? ParseLitterSizeMax(Dictionary<string, string> fields)
    {
        var raw = GetField(fields, "litter_size") ?? GetField(fields, "litter_avg");
        if (raw == null) return null;

        var (_, max) = ParseLitterSize(raw);
        return max;
    }

    private static (int? Min, int? Max) ParseLitterSize(string raw)
    {
        // {{val|7.2|2.7}} — mean ± SD → min = floor(mean - sd), max = ceil(mean + sd)
        var valMatch = ValTemplateRegex().Match(raw);
        if (valMatch.Success)
        {
            var parts = valMatch.Groups[1].Value.Split('|');
            if (parts.Length >= 1 && decimal.TryParse(parts[0].Trim(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var mean))
            {
                if (parts.Length >= 2 && decimal.TryParse(parts[1].Trim(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var sd))
                {
                    return ((int)Math.Max(1, Math.Floor((double)(mean - sd))),
                            (int)Math.Ceiling((double)(mean + sd)));
                }
                // No SD — just the mean
                var rounded = (int)Math.Round((double)mean);
                return (rounded, rounded);
            }
        }

        // Range "2–5"
        var range = ParseRange(raw);
        if (range != null)
        {
            return ((int)range.Value.Min, (int)range.Value.Max);
        }

        return (null, null);
    }

    // -------------------------------------------------------------------------
    // Text fields (coat, colour)
    // -------------------------------------------------------------------------

    private static string? ParseText(Dictionary<string, string> fields, string fieldName)
    {
        var raw = GetField(fields, fieldName);
        if (raw == null) return null;

        // Strip wiki links [[target|display]] → display, [[target]] → target
        raw = WikiLinkRegex().Replace(raw, "$1");

        // Strip any remaining [[ or ]]
        raw = raw.Replace("[[", "").Replace("]]", "");

        // Strip inline templates like {{color|...}} leaving inner text
        // Simple approach: strip {{ }} template wrappers
        raw = Regex.Replace(raw, @"\{\{[^}]+\}\}", "").Trim();

        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    // -------------------------------------------------------------------------
    // Measurement parsing (decimal)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tries each field name in order, returns the first successfully parsed measurement.
    /// Returns (min, max, unit) in the measurement's natural unit,
    /// with unit conversion already applied to appropriate SI units.
    /// </summary>
    private static (decimal? Min, decimal? Max, string? Unit) ParseMeasurement(
        Dictionary<string, string> fields, string[] fieldNames)
    {
        foreach (var name in fieldNames)
        {
            var raw = GetField(fields, name);
            if (raw == null) continue;

            // Try cvt template first
            var cvt = TryParseCvtTemplate(raw);
            if (cvt != null)
            {
                var (cMin, cMax, cUnit) = cvt.Value;
                return (cMin, cMax, cUnit);
            }

            // Fall back to plain range text
            var range = ParseRange(raw);
            if (range != null)
            {
                return (range.Value.Min, range.Value.Max, range.Value.Unit);
            }
        }

        return (null, null, null);
    }

    private static (int? Min, int? Max, string? Unit) ParseIntMeasurement(
        Dictionary<string, string> fields, string[] fieldNames)
    {
        var (min, max, unit) = ParseMeasurement(fields, fieldNames);
        if (min == null) return (null, null, null);

        return ((int)Math.Round(min.Value), (int)Math.Round(max ?? min.Value), unit);
    }

    // -------------------------------------------------------------------------
    // CVT template parsing  {{cvt|55|–|75|lb|kg|disp=flip}}
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses a {{cvt}} template and returns (min, max, targetUnit) with values
    /// already converted to the target unit.
    /// </summary>
    private static (decimal? Min, decimal? Max, string Unit)? TryParseCvtTemplate(string raw)
    {
        var match = CvtTemplateRegex().Match(raw);
        if (!match.Success) return null;

        return ParseCvtTemplate("{{cvt|" + match.Groups[1].Value + "}}");
    }

    /// <summary>
    /// Parses a {{cvt}} template string.
    /// Handles formats:
    ///   {{cvt|55|–|75|lb|kg|disp=flip}}
    ///   {{cvt|50|mi/h|km/h}}
    ///   {{cvt|4|–|8|kg}}
    ///   {{cvt|21.5|–|24|in|cm}}
    /// Returns (min, max, targetUnit) already converted.
    /// </summary>
    public static (decimal? Min, decimal? Max, string Unit)? ParseCvtTemplate(string template)
    {
        // Strip outer {{ }}
        var inner = template.Trim();
        if (inner.StartsWith("{{")) inner = inner[2..];
        if (inner.EndsWith("}}")) inner = inner[..^2];

        // Split by | (ignore the "cvt" prefix)
        var parts = inner.Split('|');
        if (parts.Length < 2) return null;

        // Remove "cvt" at start and any "key=value" options at end
        var tokens = parts
            .Skip(1)  // skip "cvt"
            .Select(p => p.Trim())
            .Where(p => !p.Contains('='))   // drop disp=flip, abbr=on, etc.
            .ToList();

        if (tokens.Count == 0) return null;

        // Determine if this is a range: first token is number, second is separator, third is number
        // or just: number, sourceUnit, targetUnit
        decimal? min = null;
        decimal? max = null;
        string? sourceUnit = null;
        string? targetUnit = null;

        // Try to parse numeric tokens
        var numericTokens = new List<(int Index, decimal Value)>();
        var unitTokens = new List<(int Index, string Value)>();

        for (var i = 0; i < tokens.Count; i++)
        {
            if (IsRangeSeparator(tokens[i]))
                continue;

            if (decimal.TryParse(tokens[i],
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
            {
                numericTokens.Add((i, num));
            }
            else if (!string.IsNullOrWhiteSpace(tokens[i]))
            {
                unitTokens.Add((i, tokens[i]));
            }
        }

        if (numericTokens.Count == 0) return null;

        if (numericTokens.Count == 1)
        {
            min = max = numericTokens[0].Value;
        }
        else
        {
            min = numericTokens[0].Value;
            max = numericTokens[^1].Value;
        }

        // Source unit is the first unit token after the last numeric token
        if (unitTokens.Count >= 1)
            sourceUnit = unitTokens[0].Value;
        if (unitTokens.Count >= 2)
            targetUnit = unitTokens[1].Value;

        // If no sourceUnit, default to the targetUnit = sourceUnit (no conversion needed)
        if (sourceUnit == null) return null;

        // Determine effective target unit
        var effTarget = targetUnit ?? sourceUnit;

        // Convert values
        var convertedMin = ConvertUnit(min.Value, sourceUnit, effTarget);
        var convertedMax = max.HasValue ? ConvertUnit(max.Value, sourceUnit, effTarget) : convertedMin;

        return (convertedMin, convertedMax, effTarget);
    }

    private static bool IsRangeSeparator(string token)
    {
        return token is "–" or "-" or "to" or "and" or "or";
    }

    // -------------------------------------------------------------------------
    // Unit conversion
    // -------------------------------------------------------------------------

    private static decimal ConvertUnit(decimal value, string fromUnit, string toUnit)
    {
        // Normalise
        fromUnit = fromUnit.ToLowerInvariant().Trim();
        toUnit = toUnit.ToLowerInvariant().Trim();

        if (fromUnit == toUnit) return value;

        // Weight conversions → kg
        if (toUnit is "kg")
        {
            return fromUnit switch
            {
                "lb" or "lbs" => value / 2.205m,
                "g" => value / 1000m,
                _ => value
            };
        }

        // Length conversions → cm
        if (toUnit is "cm")
        {
            return fromUnit switch
            {
                "in" => value * 2.54m,
                "ft" => value * 30.48m,
                "m" => value * 100m,
                "mm" => value / 10m,
                _ => value
            };
        }

        // Speed conversions → km/h
        if (toUnit is "km/h")
        {
            return fromUnit switch
            {
                "mi/h" or "mph" => value * 1.609m,
                "m/s" => value * 3.6m,
                _ => value
            };
        }

        return value;
    }

    private static decimal ConvertSpeedToKph(decimal value, string? unit)
    {
        if (unit == null) return value;
        return ConvertUnit(value, unit.ToLowerInvariant(), "km/h");
    }

    private static decimal ConvertToKg(decimal value, string? unit)
    {
        if (unit == null) return value;
        return ConvertUnit(value, unit.ToLowerInvariant(), "kg");
    }

    private static decimal ConvertToCm(decimal value, string? unit)
    {
        if (unit == null) return value;
        return ConvertUnit(value, unit.ToLowerInvariant(), "cm");
    }

    // -------------------------------------------------------------------------
    // Range parsing
    // -------------------------------------------------------------------------

    public record struct RangeResult(decimal Min, decimal Max, string? Unit);

    /// <summary>
    /// Parses range strings like "180–258 kg", "180-258 kg", "100 to 150 lb", "500 kg".
    /// Returns null if no numeric value found.
    /// </summary>
    public static RangeResult? ParseRange(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Strip ref tags
        value = RefTagRegex().Replace(value, "").Trim();

        // Try range patterns: "180–258 kg", "180 – 258 kg", "180-258 kg", "100 to 150 kg"
        var rangeMatch = RangeRegex().Match(value);
        if (rangeMatch.Success)
        {
            var min = decimal.Parse(rangeMatch.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            var max = decimal.Parse(rangeMatch.Groups[2].Value,
                System.Globalization.CultureInfo.InvariantCulture);

            // Extract unit after the max number
            var afterMax = value[(rangeMatch.Index + rangeMatch.Length)..].Trim();
            var unit = ExtractUnit(afterMax);

            return new RangeResult(min, max, unit);
        }

        // Single value: "500 kg"
        var singleMatch = SingleValueRegex().Match(value);
        if (singleMatch.Success
            && decimal.TryParse(singleMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var single))
        {
            var unit = singleMatch.Groups[2].Success
                ? singleMatch.Groups[2].Value.Trim()
                : null;
            return new RangeResult(single, single, string.IsNullOrEmpty(unit) ? null : unit);
        }

        return null;
    }

    private static string? ExtractUnit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Take the first "word" token that looks like a unit
        var firstWord = text.Split(' ', '\t')[0].Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(firstWord)) return null;

        // Known units
        return firstWord switch
        {
            "kg" or "lb" or "lbs" or "g" => firstWord,
            "cm" or "m" or "mm" or "in" or "ft" => firstWord,
            "km/h" or "mi/h" or "mph" or "m/s" => firstWord,
            _ => firstWord.Length <= 5 ? firstWord : null   // allow short unknowns
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static decimal RoundToTwo(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Strips a trailing "}}" from a field value only when it represents the infobox template
    /// close rather than part of a nested template. We count {{ and }} pairs — if }} count
    /// exceeds {{ count, the trailing }} is the outer template close.
    /// </summary>
    private static string StripTrailingTemplateClose(string value)
    {
        if (!value.TrimEnd().EndsWith("}}"))
            return value;

        // Count {{ and }} occurrences
        var opens = 0;
        var closes = 0;
        for (var i = 0; i < value.Length - 1; i++)
        {
            if (value[i] == '{' && value[i + 1] == '{') { opens++; i++; }
            else if (value[i] == '}' && value[i + 1] == '}') { closes++; i++; }
        }

        // If closes > opens, the last }} is the outer template close — strip it
        if (closes > opens)
        {
            var idx = value.LastIndexOf("}}");
            return value[..idx].Trim();
        }

        return value;
    }
}
