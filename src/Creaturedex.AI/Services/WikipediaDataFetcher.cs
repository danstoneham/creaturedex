using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Creaturedex.AI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public partial class WikipediaDataFetcher(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<WikipediaDataFetcher> logger)
{
    private const string SummaryApiBase = "https://en.wikipedia.org/api/rest_v1/page/summary/";
    private const string QueryApiBase = "https://en.wikipedia.org/w/api.php";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    // Regex patterns for also-known-as extraction
    [GeneratedRegex(@"(?:also|commonly|often|sometimes)\s+known\s+as\s+(?:the\s+)?(?:""([^""]+)""|'''([^']+)'''|'([^']+)'|(\w[\w\s\-]+))")]
    private static partial Regex AlsoKnownAsRegex();

    [GeneratedRegex(@"'''([^']+)'''")]
    private static partial Regex BoldTextRegex();

    [GeneratedRegex(@"\((?:also\s+called\s+|also\s+known\s+as\s+|or\s+simply\s+(?:the\s+)?)([^)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ParenAltNameRegex();

    // Population estimate patterns
    [GeneratedRegex(@"(?:estimated?\s+(?:at|population\s+of)|population\s+(?:of|estimated?\s+at|is\s+estimated?\s+(?:at|to\s+be)))\s+([\d,]+(?:\s*[-–]\s*[\d,]+)?(?:\s+(?:million|thousand|billion))?)", RegexOptions.IgnoreCase)]
    private static partial Regex PopulationEstimateRegex();

    // Legal protection patterns
    [GeneratedRegex(@"CITES\s+Appendix\s+(I{1,3}V?|IV)", RegexOptions.IgnoreCase)]
    private static partial Regex CitesAppendixRegex();

    [GeneratedRegex(@"(?:protected|listed|regulated)\s+(?:under|by)\s+(?:the\s+)?([A-Z][\w\s]+?(?:Act|Directive|Convention|Regulation))", RegexOptions.IgnoreCase)]
    private static partial Regex LegalActRegex();

    [GeneratedRegex(@"Endangered\s+Species\s+Act", RegexOptions.IgnoreCase)]
    private static partial Regex EndangeredSpeciesActRegex();

    [GeneratedRegex(@"EU\s+Habitats?\s+Directive", RegexOptions.IgnoreCase)]
    private static partial Regex EuHabitatsDirectiveRegex();

    public async Task<WikipediaAnimalData?> FetchAsync(string animalName, CancellationToken ct = default)
    {
        var cacheKey = $"wiki-data:{animalName.ToLowerInvariant()}";
        if (cache.TryGetValue(cacheKey, out WikipediaAnimalData? cached))
        {
            logger.LogDebug("WikipediaDataFetcher cache hit for {AnimalName}", animalName);
            return cached;
        }

        try
        {
            var title = await ResolveTitleAsync(animalName, ct);
            if (title == null)
            {
                logger.LogWarning("WikipediaDataFetcher: could not resolve article title for {AnimalName}", animalName);
                cache.Set(cacheKey, (WikipediaAnimalData?)null, CacheDuration);
                return null;
            }

            logger.LogDebug("WikipediaDataFetcher: resolved title for {AnimalName} -> {Title}", animalName, title);

            var encodedTitle = Uri.EscapeDataString(title.Replace(' ', '_'));

            // Parallel fetch: summary, wikitext (section 0), full plaintext
            var summaryTask = FetchSummaryAsync(encodedTitle, ct);
            var wikitextTask = FetchWikitextSection0Async(encodedTitle, ct);
            var fullTextTask = FetchFullPlaintextAsync(encodedTitle, ct);

            await Task.WhenAll(summaryTask, wikitextTask, fullTextTask);

            var summary = await summaryTask;
            var wikitext = await wikitextTask;
            var fullText = await fullTextTask;

            // Parse infobox from wikitext
            WikipediaInfoboxData? infobox = null;
            if (wikitext != null)
            {
                var parser = new WikipediaInfoboxParser();
                infobox = parser.Parse(wikitext);
            }

            // Parse sections from full plaintext
            string? appearanceText = null;
            string? habitatText = null;
            string? dietText = null;
            string? behaviourText = null;
            string? conservationText = null;
            string? reproductionText = null;

            if (fullText != null)
            {
                var sections = ParseSections(fullText);

                appearanceText = FindSection(sections,
                    ["description", "appearance", "physical description", "physical characteristics", "characteristics"]);
                habitatText = FindSection(sections,
                    ["habitat", "distribution", "distribution and habitat", "habitat and range", "habitat and distribution", "range", "geography"]);
                dietText = FindSection(sections,
                    ["diet", "feeding", "food", "diet and feeding", "feeding ecology", "digestion", "foraging"]);
                behaviourText = FindSection(sections,
                    ["behaviour", "behavior", "ecology", "ecology and behaviour", "social behaviour", "social structure", "social"]);
                conservationText = FindSection(sections,
                    ["conservation", "conservation status", "threats", "conservation and threats"]);
                reproductionText = FindSection(sections,
                    ["reproduction", "breeding", "life cycle", "reproduction and life cycle"]);
            }

            // Extract intro text, image URL/license from summary
            string? introText = null;
            string? imageUrl = null;
            string? imageLicense = null;
            string? pageUrl = $"https://en.wikipedia.org/wiki/{encodedTitle}";

            if (summary is JsonElement summaryEl)
            {
                introText = summaryEl.TryGetProperty("extract", out var extractEl)
                    ? extractEl.GetString()
                    : null;

                if (summaryEl.TryGetProperty("content_urls", out var urls)
                    && urls.TryGetProperty("desktop", out var desktop)
                    && desktop.TryGetProperty("page", out var page))
                {
                    pageUrl = page.GetString() ?? pageUrl;
                }

                if (summaryEl.TryGetProperty("thumbnail", out var thumb)
                    && thumb.TryGetProperty("source", out var thumbSrc))
                {
                    imageUrl = thumbSrc.GetString();
                    imageLicense = "Wikimedia Commons";
                }
                // Prefer original image for higher resolution
                if (summaryEl.TryGetProperty("originalimage", out var origImg)
                    && origImg.TryGetProperty("source", out var origSrc))
                {
                    imageUrl = origSrc.GetString();
                }
            }

            // Extract also-known-as from intro text
            var alsoKnownAs = ExtractAlsoKnownAs(introText);

            // Extract population estimate from conservation section (or full text)
            var populationEstimate = ExtractPopulationEstimate(conservationText)
                ?? ExtractPopulationEstimate(fullText);

            // Extract legal protections from conservation text
            var legalProtections = ExtractLegalProtections(conservationText, null);

            var result = new WikipediaAnimalData
            {
                Title = title,
                Url = pageUrl,
                Infobox = infobox,
                IntroText = introText,
                AppearanceText = NullIfEmpty(appearanceText),
                HabitatText = NullIfEmpty(habitatText),
                DietText = NullIfEmpty(dietText),
                BehaviourText = NullIfEmpty(behaviourText),
                ConservationText = NullIfEmpty(conservationText),
                ReproductionText = NullIfEmpty(reproductionText),
                AlsoKnownAs = alsoKnownAs,
                ImageUrl = imageUrl,
                ImageLicense = imageLicense,
                PopulationEstimate = populationEstimate,
                LegalProtections = legalProtections,
            };

            logger.LogInformation(
                "WikipediaDataFetcher: fetched data for {AnimalName} (title={Title}, sections=[{Sections}], alsoKnownAs=[{AKA}])",
                animalName, title,
                string.Join(", ", new[]
                {
                    appearanceText != null ? "appearance" : null,
                    habitatText != null ? "habitat" : null,
                    dietText != null ? "diet" : null,
                    behaviourText != null ? "behaviour" : null,
                    conservationText != null ? "conservation" : null,
                    reproductionText != null ? "reproduction" : null,
                }.Where(s => s != null)),
                string.Join(", ", alsoKnownAs));

            cache.Set(cacheKey, result, CacheDuration);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "WikipediaDataFetcher: failed to fetch data for {AnimalName}", animalName);
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Title resolution
    // -------------------------------------------------------------------------

    private async Task<string?> ResolveTitleAsync(string animalName, CancellationToken ct)
    {
        // Try exact
        var title = await TryGetTitleAsync(animalName, ct);
        if (title != null) return title;

        // Try with "(animal)" suffix
        title = await TryGetTitleAsync($"{animalName} (animal)", ct);
        if (title != null) return title;

        // Search API fallback
        return await SearchTitleAsync(animalName, ct);
    }

    private async Task<string?> TryGetTitleAsync(string title, CancellationToken ct)
    {
        try
        {
            var encoded = Uri.EscapeDataString(title.Replace(' ', '_'));
            var response = await httpClient.GetAsync($"{SummaryApiBase}{encoded}", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            var type = json.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            if (type == "disambiguation")
                return null;

            return json.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "WikipediaDataFetcher: TryGetTitle failed for {Title}", title);
            return null;
        }
    }

    private async Task<string?> SearchTitleAsync(string animalName, CancellationToken ct)
    {
        try
        {
            var url = $"{QueryApiBase}?action=query&list=search" +
                      $"&srsearch={Uri.EscapeDataString(animalName + " animal")}" +
                      "&srnamespace=0&srlimit=1&format=json";

            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            if (json.TryGetProperty("query", out var query)
                && query.TryGetProperty("search", out var results)
                && results.GetArrayLength() > 0)
            {
                return results[0].TryGetProperty("title", out var titleEl)
                    ? titleEl.GetString()
                    : null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "WikipediaDataFetcher: SearchTitle failed for {AnimalName}", animalName);
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Data fetching
    // -------------------------------------------------------------------------

    private async Task<JsonElement?> FetchSummaryAsync(string encodedTitle, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.GetAsync($"{SummaryApiBase}{encodedTitle}", ct);
            if (!response.IsSuccessStatusCode) return null;

            return await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "WikipediaDataFetcher: FetchSummary failed for {Title}", encodedTitle);
            return null;
        }
    }

    private async Task<string?> FetchWikitextSection0Async(string encodedTitle, CancellationToken ct)
    {
        try
        {
            var url = $"{QueryApiBase}?action=parse&page={encodedTitle}&prop=wikitext&section=0&format=json&redirects";
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            if (!json.TryGetProperty("parse", out var parse)
                || !parse.TryGetProperty("wikitext", out var wikitext)
                || !wikitext.TryGetProperty("*", out var text))
                return null;

            return text.GetString();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "WikipediaDataFetcher: FetchWikitext failed for {Title}", encodedTitle);
            return null;
        }
    }

    private async Task<string?> FetchFullPlaintextAsync(string encodedTitle, CancellationToken ct)
    {
        try
        {
            var url = $"{QueryApiBase}?action=query&titles={encodedTitle}" +
                      "&prop=extracts&explaintext=1&exsectionformat=wiki&format=json";
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            if (!json.TryGetProperty("query", out var query)
                || !query.TryGetProperty("pages", out var pages))
                return null;

            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("extract", out var extract))
                    return extract.GetString();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "WikipediaDataFetcher: FetchFullPlaintext failed for {Title}", encodedTitle);
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Section parsing
    // -------------------------------------------------------------------------

    private static Dictionary<string, string> ParseSections(string text)
    {
        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = text.Split('\n');
        string? currentSection = null;
        var currentContent = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            // Detect section headers: "== Name ==" pattern in plaintext extracts
            if (trimmed.StartsWith("==") && trimmed.EndsWith("=="))
            {
                // Save previous section
                if (currentSection != null && currentContent.Count > 0)
                    sections[currentSection] = string.Join("\n", currentContent).Trim();

                currentSection = trimmed.Trim('=', ' ').ToLowerInvariant();
                currentContent.Clear();
            }
            else if (currentSection != null)
            {
                currentContent.Add(line);
            }
        }

        // Save last section
        if (currentSection != null && currentContent.Count > 0)
            sections[currentSection] = string.Join("\n", currentContent).Trim();

        return sections;
    }

    private static string? FindSection(Dictionary<string, string> sections, string[] keys)
    {
        foreach (var key in keys)
        {
            // Exact match
            if (sections.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;

            // Partial match: section key contains our search key, or vice versa
            var match = sections.FirstOrDefault(kv =>
                !string.IsNullOrWhiteSpace(kv.Value)
                && (kv.Key.Contains(key, StringComparison.OrdinalIgnoreCase)
                    || key.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrWhiteSpace(match.Value))
                return match.Value;
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Also-known-as extraction
    // -------------------------------------------------------------------------

    private static List<string> ExtractAlsoKnownAs(string? introText)
    {
        if (string.IsNullOrWhiteSpace(introText))
            return [];

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find all bold text ('''text''' in wikitext, or just names in the plain extract)
        // The summary API returns plain text, so look for patterns in natural language

        // "also known as the X", "also called X", "commonly known as X"
        var akaMatch = AlsoKnownAsRegex().Match(introText);
        if (akaMatch.Success)
        {
            // One of the groups captured the name
            for (var g = 1; g <= 4; g++)
            {
                var val = akaMatch.Groups[g].Value.Trim();
                if (!string.IsNullOrEmpty(val))
                {
                    // May have multiple names separated by commas, "and", "or"
                    foreach (var part in SplitNames(val))
                        names.Add(part);
                    break;
                }
            }
        }

        // "(also called X, Y, and Z)" or "(also known as X)" in parentheses
        var parenMatches = ParenAltNameRegex().Matches(introText);
        foreach (Match m in parenMatches)
        {
            foreach (var part in SplitNames(m.Groups[1].Value.Trim()))
                names.Add(part);
        }

        // Bold names in wikitext (if wikitext was passed as introText — usually it's plain text from summary)
        var boldMatches = BoldTextRegex().Matches(introText);
        // Only pick up 2nd+ bold text items (first is usually the article subject itself)
        var boldList = boldMatches.Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToList();
        if (boldList.Count > 1)
        {
            foreach (var bold in boldList.Skip(1))
                names.Add(bold);
        }

        return [.. names];
    }

    private static IEnumerable<string> SplitNames(string raw)
    {
        // Split on ", ", " and ", " or "
        var parts = Regex.Split(raw, @",\s*|\s+and\s+|\s+or\s+");
        return parts
            .Select(p => p.Trim().TrimEnd('.').Trim())
            .Where(p => p.Length > 1 && p.Length < 80);
    }

    // -------------------------------------------------------------------------
    // Population estimate extraction
    // -------------------------------------------------------------------------

    private static string? ExtractPopulationEstimate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = PopulationEstimateRegex().Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static string? ExtractLegalProtections(string? conservationText, string? gbifConservationProse)
    {
        var combined = string.Join("\n\n",
            new[] { conservationText, gbifConservationProse }
                .Where(t => !string.IsNullOrWhiteSpace(t)));

        if (string.IsNullOrWhiteSpace(combined)) return null;

        var protections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // CITES Appendix
        foreach (Match m in CitesAppendixRegex().Matches(combined))
            protections.Add($"CITES Appendix {m.Groups[1].Value.Trim()}");

        // Endangered Species Act (explicit check before generic pattern)
        if (EndangeredSpeciesActRegex().IsMatch(combined))
            protections.Add("Endangered Species Act");

        // EU Habitats Directive
        if (EuHabitatsDirectiveRegex().IsMatch(combined))
            protections.Add("EU Habitats Directive");

        // Generic "protected/listed/regulated under/by [Name] Act/Directive/Convention"
        foreach (Match m in LegalActRegex().Matches(combined))
        {
            var actName = m.Groups[1].Value.Trim();
            // Avoid duplicating ESA/EU Habitats if already captured
            if (!protections.Any(p => p.Contains(actName, StringComparison.OrdinalIgnoreCase)))
                protections.Add(actName);
        }

        return protections.Count > 0 ? string.Join(", ", protections) : null;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
