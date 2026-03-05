using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public record WikipediaArticle(
    string Title,
    string Summary,
    string? TaxonomyInfo,
    string? ConservationInfo,
    string? HabitatInfo,
    string? DietInfo,
    string? LifespanInfo,
    string Url,
    string? ImageUrl = null,
    string? ImageLicense = null);

public class WikipediaService(HttpClient httpClient, IMemoryCache cache, ILogger<WikipediaService> logger)
{
    private const string SummaryApiBase = "https://en.wikipedia.org/api/rest_v1/page/summary/";
    private const string QueryApiBase = "https://en.wikipedia.org/w/api.php";
    private const int MaxSummaryLength = 500;
    private const int MaxSectionLength = 400;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public async Task<WikipediaArticle?> GetAnimalArticleAsync(string animalName, CancellationToken ct = default)
    {
        var cacheKey = $"wiki:{animalName.ToLowerInvariant()}";
        if (cache.TryGetValue(cacheKey, out WikipediaArticle? cached))
        {
            logger.LogDebug("Wikipedia cache hit for {AnimalName}", animalName);
            return cached;
        }

        var article = await FetchArticleAsync(animalName, ct);
        if (article == null)
        {
            // Retry with "(animal)" suffix for disambiguation
            logger.LogDebug("No article found for {AnimalName}, retrying with animal suffix", animalName);
            article = await FetchArticleAsync($"{animalName} (animal)", ct);
        }

        if (article == null)
        {
            // Last resort: search API
            logger.LogDebug("No article found for {AnimalName}, trying search API", animalName);
            article = await SearchAndFetchAsync(animalName, ct);
        }

        if (article != null)
        {
            logger.LogInformation("Wikipedia article found for {AnimalName}: {Title} ({Url})", animalName, article.Title, article.Url);
            logger.LogInformation("  Summary: {Summary}", article.Summary);
            if (article.TaxonomyInfo != null) logger.LogInformation("  Taxonomy: {Taxonomy}", article.TaxonomyInfo);
            if (article.ConservationInfo != null) logger.LogInformation("  Conservation: {Conservation}", article.ConservationInfo);
            if (article.HabitatInfo != null) logger.LogInformation("  Habitat: {Habitat}", article.HabitatInfo);
            if (article.DietInfo != null) logger.LogInformation("  Diet: {Diet}", article.DietInfo);
            if (article.LifespanInfo != null) logger.LogInformation("  Lifespan: {Lifespan}", article.LifespanInfo);
            if (article.ImageUrl != null) logger.LogInformation("  Image: {ImageUrl} ({License})", article.ImageUrl, article.ImageLicense);
        }
        else
        {
            logger.LogWarning("No Wikipedia article found for {AnimalName} after all attempts", animalName);
        }

        cache.Set(cacheKey, article, CacheDuration);
        return article;
    }

    public string FormatAsReference(WikipediaArticle article)
    {
        var parts = new List<string> { $"Title: {article.Title}", $"Summary: {article.Summary}" };

        if (article.TaxonomyInfo != null)
            parts.Add($"Taxonomy: {article.TaxonomyInfo}");
        if (article.ConservationInfo != null)
            parts.Add($"Conservation: {article.ConservationInfo}");
        if (article.HabitatInfo != null)
            parts.Add($"Habitat & Range: {article.HabitatInfo}");
        if (article.DietInfo != null)
            parts.Add($"Diet: {article.DietInfo}");
        if (article.LifespanInfo != null)
            parts.Add($"Lifespan: {article.LifespanInfo}");

        parts.Add($"Source: {article.Url}");

        return string.Join("\n", parts);
    }

    private async Task<WikipediaArticle?> FetchArticleAsync(string title, CancellationToken ct)
    {
        try
        {
            var encodedTitle = Uri.EscapeDataString(title.Replace(' ', '_'));

            // Fetch summary via REST API
            var summaryResponse = await httpClient.GetAsync($"{SummaryApiBase}{encodedTitle}", ct);

            if (summaryResponse.StatusCode == HttpStatusCode.NotFound)
                return null;

            summaryResponse.EnsureSuccessStatusCode();
            var summaryJson = await JsonSerializer.DeserializeAsync<JsonElement>(
                await summaryResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            // Check for disambiguation
            var type = summaryJson.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            if (type == "disambiguation")
                return null;

            var articleTitle = summaryJson.GetProperty("title").GetString() ?? title;
            var summary = Truncate(
                summaryJson.TryGetProperty("extract", out var extractEl) ? extractEl.GetString() ?? "" : "",
                MaxSummaryLength);
            var pageUrl = summaryJson.TryGetProperty("content_urls", out var urls)
                && urls.TryGetProperty("desktop", out var desktop)
                && desktop.TryGetProperty("page", out var page)
                    ? page.GetString() ?? $"https://en.wikipedia.org/wiki/{encodedTitle}"
                    : $"https://en.wikipedia.org/wiki/{encodedTitle}";

            // Extract image if available (thumbnail from summary API — these are freely licensed)
            string? imageUrl = null;
            string? imageLicense = null;
            if (summaryJson.TryGetProperty("thumbnail", out var thumb)
                && thumb.TryGetProperty("source", out var thumbSrc))
            {
                imageUrl = thumbSrc.GetString();
                // Summary API thumbnails are from Wikimedia Commons (free licenses)
                imageLicense = "Wikimedia Commons";
            }
            // Prefer originalimage for higher resolution if available
            if (summaryJson.TryGetProperty("originalimage", out var origImg)
                && origImg.TryGetProperty("source", out var origSrc))
            {
                imageUrl = origSrc.GetString();
            }

            // Fetch sections using the canonical title from the summary API (case-sensitive!)
            var canonicalEncoded = Uri.EscapeDataString(articleTitle.Replace(' ', '_'));
            var sections = await FetchSectionsAsync(canonicalEncoded, ct);

            return new WikipediaArticle(
                Title: articleTitle,
                Summary: summary,
                TaxonomyInfo: sections.GetValueOrDefault("taxonomy"),
                ConservationInfo: sections.GetValueOrDefault("conservation"),
                HabitatInfo: sections.GetValueOrDefault("habitat"),
                DietInfo: sections.GetValueOrDefault("diet"),
                LifespanInfo: sections.GetValueOrDefault("lifespan"),
                Url: pageUrl,
                ImageUrl: imageUrl,
                ImageLicense: imageLicense);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch Wikipedia article for {Title}", title);
            return null;
        }
    }

    private async Task<Dictionary<string, string>> FetchSectionsAsync(string encodedTitle, CancellationToken ct)
    {
        var sections = new Dictionary<string, string>();

        try
        {
            var url = $"{QueryApiBase}?action=query&titles={encodedTitle}" +
                      "&prop=extracts&explaintext=1&exsectionformat=wiki&format=json";
            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            if (!json.TryGetProperty("query", out var query)
                || !query.TryGetProperty("pages", out var pages))
                return sections;

            // Get the first (only) page
            string? fullText = null;
            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("extract", out var extract))
                    fullText = extract.GetString();
                break;
            }

            if (fullText == null)
            {
                logger.LogWarning("Wikipedia extracts returned null for {Title}", encodedTitle);
                return sections;
            }

            logger.LogDebug("Wikipedia extract length for {Title}: {Length} chars", encodedTitle, fullText.Length);

            // Parse sections by heading pattern (== Section Name ==)
            var sectionMap = ParseSections(fullText);
            logger.LogDebug("Wikipedia sections parsed for {Title}: {Sections}",
                encodedTitle, string.Join(", ", sectionMap.Keys));

            // Map Wikipedia section names to our fields
            var taxonomyKeys = new[] { "taxonomy", "taxonomy and systematics", "classification", "scientific classification", "systematics" };
            var conservationKeys = new[] { "conservation", "conservation status", "current status", "threats", "population", "conservation and threats", "recovery efforts" };
            var habitatKeys = new[] { "habitat", "distribution", "range", "habitat and range", "distribution and habitat", "habitat and distribution", "geography" };
            var dietKeys = new[] { "diet", "feeding", "food", "diet and feeding", "feeding ecology" };

            sections["taxonomy"] = FindSection(sectionMap, taxonomyKeys);
            sections["conservation"] = FindSection(sectionMap, conservationKeys);
            sections["habitat"] = FindSection(sectionMap, habitatKeys);
            sections["diet"] = FindSection(sectionMap, dietKeys);

            // Extract lifespan by searching for keywords across all sections
            sections["lifespan"] = ExtractLifespanInfo(fullText);

            // Remove empty entries
            foreach (var key in sections.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList())
                sections.Remove(key);

            logger.LogDebug("Wikipedia matched sections for {Title}: {Sections}",
                encodedTitle, sections.Count > 0 ? string.Join(", ", sections.Keys) : "(none)");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch Wikipedia sections for {Title}", encodedTitle);
        }

        return sections;
    }

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

    private static string ExtractLifespanInfo(string fullText)
    {
        // Lifespan info is often buried in various sections — search by keyword
        var keywords = new[] { "life span", "lifespan", "life expectancy", "longevity", "can live", "live up to", "live for" };
        var lines = fullText.Split('\n');

        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();
            if (keywords.Any(k => lower.Contains(k)) && line.Trim().Length > 10)
                return Truncate(line.Trim(), MaxSectionLength);
        }

        return "";
    }

    private static string FindSection(Dictionary<string, string> sections, string[] keys)
    {
        foreach (var key in keys)
        {
            // Exact match
            if (sections.TryGetValue(key, out var value))
                return Truncate(value, MaxSectionLength);

            // Partial match
            var match = sections.FirstOrDefault(kv => kv.Key.Contains(key) || key.Contains(kv.Key));
            if (!string.IsNullOrEmpty(match.Value))
                return Truncate(match.Value, MaxSectionLength);
        }
        return "";
    }

    private async Task<WikipediaArticle?> SearchAndFetchAsync(string query, CancellationToken ct)
    {
        try
        {
            var url = $"{QueryApiBase}?action=query&list=search&srsearch={Uri.EscapeDataString(query + " animal")}" +
                      "&srnamespace=0&srlimit=1&format=json";
            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            if (json.TryGetProperty("query", out var qry)
                && qry.TryGetProperty("search", out var results)
                && results.GetArrayLength() > 0)
            {
                var firstTitle = results[0].GetProperty("title").GetString();
                if (firstTitle != null)
                    return await FetchArticleAsync(firstTitle, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Wikipedia search failed for {Query}", query);
        }

        return null;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        // Truncate at last sentence boundary within limit
        var truncated = text[..maxLength];
        var lastPeriod = truncated.LastIndexOf('.');
        return lastPeriod > maxLength / 2 ? truncated[..(lastPeriod + 1)] : truncated + "...";
    }
}
