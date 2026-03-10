using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Creaturedex.AI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public class GbifService(
    HttpClient httpClient,
    IMemoryCache cache,
    GbifMapService mapService,
    WikipediaService wikipediaService,
    ILogger<GbifService> logger)
{
    private const string GbifApiBase = "https://api.gbif.org/v1";
    private const string ColApiBase = "https://api.checklistbank.org";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private static readonly string[] DescriptionTypes =
    [
        "biology_ecology", "food_feeding", "activity",
        "description", "breeding", "conservation", "distribution"
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<GbifAnimalData?> FetchAnimalDataAsync(string animalName, CancellationToken ct = default)
    {
        var cacheKey = $"gbif:{animalName.ToLowerInvariant()}";
        if (cache.TryGetValue(cacheKey, out GbifAnimalData? cached))
        {
            logger.LogDebug("GBIF cache hit for {AnimalName}", animalName);
            return cached;
        }

        var resolved = await ResolveSpeciesAsync(animalName, ct);
        if (resolved is null)
        {
            logger.LogWarning("GBIF could not resolve species for {AnimalName}", animalName);
            return null;
        }

        var (taxonKey, canonicalName) = resolved.Value;
        logger.LogInformation("GBIF resolved {AnimalName} -> taxonKey={TaxonKey}, canonical={Canonical}",
            animalName, taxonKey, canonicalName);

        // Fan out all requests in parallel — each is independent
        var taxonomyTask = FetchTaxonomyAsync(taxonKey, canonicalName, ct);
        var descriptionsTask = FetchDescriptionsAsync(taxonKey, ct);
        var iucnTask = FetchIucnStatusAsync(taxonKey, ct);
        var vernacularTask = FetchVernacularNamesAsync(taxonKey, ct);
        var distributionTask = FetchDistributionsAsync(taxonKey, ct);
        var imageTask = FetchBestImageAsync(taxonKey, ct);
        var mapTask = mapService.BuildMapMetadataAsync(taxonKey, ct);

        await Task.WhenAll(taxonomyTask, descriptionsTask, iucnTask,
                           vernacularTask, distributionTask, imageTask, mapTask);

        var descriptions = await descriptionsTask;
        var iucn = await iucnTask;
        var vernaculars = await vernacularTask;

        // Derive English common name from vernacular list
        var englishName = vernaculars
            .Where(v => v.Language is "eng" or "en")
            .Select(v => v.Name)
            .FirstOrDefault();

        var result = new GbifAnimalData
        {
            TaxonKey = taxonKey,
            CanonicalName = canonicalName,
            EnglishCommonName = englishName,
            Taxonomy = await taxonomyTask,
            HabitatProse = descriptions.GetValueOrDefault("biology_ecology"),
            DietProse = descriptions.GetValueOrDefault("food_feeding"),
            BehaviourProse = descriptions.GetValueOrDefault("activity"),
            PhysicalDescriptionProse = descriptions.GetValueOrDefault("description"),
            BreedingProse = descriptions.GetValueOrDefault("breeding"),
            ConservationProse = descriptions.GetValueOrDefault("conservation"),
            DistributionProse = descriptions.GetValueOrDefault("distribution"),
            IucnCategory = iucn.Category,
            IucnCode = iucn.Code,
            IucnTaxonId = iucn.IucnTaxonId,
            NativeCountries = await distributionTask,
            VernacularNames = vernaculars,
            BestImage = await imageTask,
            MapMetadata = await mapTask,
        };

        cache.Set(cacheKey, result, CacheDuration);
        return result;
    }

    public async Task<List<GbifSpeciesSuggestion>> SearchSpeciesAsync(string query, CancellationToken ct = default)
    {
        var suggestions = new Dictionary<int, GbifSpeciesSuggestion>(); // keyed by taxonKey for dedup

        try
        {
            // Strategy 1: Wikipedia lookup — most reliable for common names like "Tiger", "Elephant"
            // Wikipedia articles always use the correct common name and contain the scientific name.
            // This is the PRIMARY strategy for the 99.9% case where users type common names.
            string? wikiFamily = null;
            var wikiResult = await TryResolveViaWikipediaAsync(query, ct);
            if (wikiResult != null)
            {
                var (taxonKey, canonicalName) = wikiResult.Value;
                var detail = await FetchSpeciesDetailAsync(taxonKey, ct);
                wikiFamily = detail.Family;

                // Use the user's query as the display name — "Bengal Tiger" not "Bagh"
                // This is what the user typed and what they expect to see
                var displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                    .ToTitleCase(query.ToLower());

                suggestions[taxonKey] = new GbifSpeciesSuggestion
                {
                    TaxonKey = taxonKey,
                    ScientificName = canonicalName,
                    CommonName = displayName,
                    Rank = detail.Rank,
                    Status = "ACCEPTED",
                    Family = detail.Family,
                    Order = detail.Order,
                };
            }

            // Strategy 2: Scientific name match — for users typing "Panthera tigris" directly
            var matchResult = await TryMatchScientificNameAsync(query, ct);
            if (matchResult != null)
            {
                var (taxonKey, canonicalName) = matchResult.Value;
                if (!suggestions.ContainsKey(taxonKey))
                {
                    var detail = await FetchSpeciesDetailAsync(taxonKey, ct);
                    suggestions[taxonKey] = new GbifSpeciesSuggestion
                    {
                        TaxonKey = taxonKey,
                        ScientificName = canonicalName,
                        CommonName = detail.CommonName,
                        Rank = detail.Rank,
                        Status = detail.Status,
                        Family = detail.Family,
                        Order = detail.Order,
                    };
                }
            }

            // Strategy 3: Vernacular name search — only if Wikipedia didn't give us a result
            // When Wikipedia resolves successfully, the GBIF vernacular results are usually
            // irrelevant noise (tiger scallops, tiger frogs, etc.)
            if (wikiResult == null)
            {
                await CollectVernacularSuggestionsAsync(query, suggestions, ct);

                var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 2)
                {
                    var baseName = words[^1];
                    await CollectVernacularSuggestionsAsync(baseName, suggestions, ct);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "GBIF species search failed for {Query}", query);
        }

        return [.. suggestions.Values.Take(8)];
    }

    private async Task CollectVernacularSuggestionsAsync(
        string name, Dictionary<int, GbifSpeciesSuggestion> suggestions, CancellationToken ct)
    {
        const string backboneDatasetKey = "d7dddbf4-2cf0-4f39-9b2a-bb099caae36c";
        var url = $"{GbifApiBase}/species/search" +
                  $"?q={Uri.EscapeDataString(name)}" +
                  $"&rank=SPECIES" +
                  $"&qField=VERNACULAR" +
                  $"&datasetKey={backboneDatasetKey}" +
                  $"&limit=10";

        var json = await GetJsonAsync(url, ct);
        if (!json.TryGetProperty("results", out var results)) return;

        foreach (var item in results.EnumerateArray())
        {
            var key = item.TryGetProperty("nubKey", out var nk) ? nk.GetInt32()
                    : item.TryGetProperty("key", out var k) ? k.GetInt32() : 0;
            if (key == 0) continue;

            // Deduplicate — prefer ACCEPTED over SYNONYM
            var status = item.TryGetProperty("taxonomicStatus", out var ts) ? ts.GetString() : null;
            if (suggestions.ContainsKey(key))
            {
                if (status != "ACCEPTED") continue;
                // Replace existing entry only if new one is ACCEPTED
            }

            var canonicalName = item.TryGetProperty("canonicalName", out var cn) ? cn.GetString() : null;
            if (canonicalName == null) continue;

            // Extract first English vernacular name
            string? commonName = null;
            if (item.TryGetProperty("vernacularNames", out var vns))
            {
                foreach (var vn in vns.EnumerateArray())
                {
                    var lang = vn.TryGetProperty("language", out var lg) ? lg.GetString() : null;
                    if (lang is "eng" or "en" or null)
                    {
                        var vnStr = vn.ValueKind == JsonValueKind.String
                            ? vn.GetString()
                            : vn.TryGetProperty("vernacularName", out var vnProp) ? vnProp.GetString() : null;
                        if (vnStr != null)
                        {
                            commonName = vnStr;
                            break;
                        }
                    }
                }
            }

            var rank = item.TryGetProperty("rank", out var r) ? r.GetString() : null;
            var family = item.TryGetProperty("family", out var f) ? f.GetString() : null;
            var order = item.TryGetProperty("order", out var o) ? o.GetString() : null;

            suggestions[key] = new GbifSpeciesSuggestion
            {
                TaxonKey = key,
                ScientificName = canonicalName,
                CommonName = commonName,
                Rank = rank,
                Status = status,
                Family = family,
                Order = order,
            };
        }
    }

    private async Task CollectBroadSuggestionsAsync(
        string name, Dictionary<int, GbifSpeciesSuggestion> suggestions, CancellationToken ct)
    {
        // Search without qField restriction — matches scientific names, vernaculars across all datasets
        // Filtered to GBIF backbone + SPECIES rank + Animalia kingdom
        const string backboneDatasetKey = "d7dddbf4-2cf0-4f39-9b2a-bb099caae36c";
        var url = $"{GbifApiBase}/species/search" +
                  $"?q={Uri.EscapeDataString(name)}" +
                  $"&rank=SPECIES" +
                  $"&datasetKey={backboneDatasetKey}" +
                  $"&limit=10";

        var json = await GetJsonAsync(url, ct);
        if (!json.TryGetProperty("results", out var results)) return;

        foreach (var item in results.EnumerateArray())
        {
            // Only include animals (kingdom = Animalia)
            var kingdom = item.TryGetProperty("kingdom", out var kg) ? kg.GetString() : null;
            if (kingdom != null && kingdom != "Animalia") continue;

            var key = item.TryGetProperty("nubKey", out var nk) ? nk.GetInt32()
                    : item.TryGetProperty("key", out var k) ? k.GetInt32() : 0;
            if (key == 0 || suggestions.ContainsKey(key)) continue;

            var canonicalName = item.TryGetProperty("canonicalName", out var cn) ? cn.GetString() : null;
            if (canonicalName == null) continue;

            var status = item.TryGetProperty("taxonomicStatus", out var ts) ? ts.GetString() : null;

            // Extract first English vernacular name
            string? commonName = null;
            if (item.TryGetProperty("vernacularNames", out var vns))
            {
                foreach (var vn in vns.EnumerateArray())
                {
                    var lang = vn.TryGetProperty("language", out var lg) ? lg.GetString() : null;
                    if (lang is "eng" or "en" or null)
                    {
                        commonName = vn.ValueKind == JsonValueKind.String
                            ? vn.GetString()
                            : vn.TryGetProperty("vernacularName", out var vnProp) ? vnProp.GetString() : null;
                        if (commonName != null) break;
                    }
                }
            }

            suggestions[key] = new GbifSpeciesSuggestion
            {
                TaxonKey = key,
                ScientificName = canonicalName,
                CommonName = commonName,
                Rank = item.TryGetProperty("rank", out var r) ? r.GetString() : null,
                Status = status,
                Family = item.TryGetProperty("family", out var f) ? f.GetString() : null,
                Order = item.TryGetProperty("order", out var o) ? o.GetString() : null,
            };
        }
    }

    private async Task<(string? CommonName, string? Rank, string? Status, string? Family, string? Order)>
        FetchSpeciesDetailAsync(int taxonKey, CancellationToken ct)
    {
        try
        {
            var url = $"{GbifApiBase}/species/{taxonKey}";
            var json = await GetJsonAsync(url, ct);

            var rank = json.TryGetProperty("rank", out var r) ? r.GetString() : null;
            var status = json.TryGetProperty("taxonomicStatus", out var ts) ? ts.GetString() : null;
            var family = json.TryGetProperty("family", out var f) ? f.GetString() : null;
            var order = json.TryGetProperty("order", out var o) ? o.GetString() : null;

            // Fetch English common name from vernacular names
            string? commonName = null;
            try
            {
                var vnUrl = $"{GbifApiBase}/species/{taxonKey}/vernacularNames?limit=20";
                var vnJson = await GetJsonAsync(vnUrl, ct);
                if (vnJson.TryGetProperty("results", out var results))
                {
                    foreach (var item in results.EnumerateArray())
                    {
                        var lang = item.TryGetProperty("language", out var lg) ? lg.GetString() : null;
                        if (lang is "eng" or "en")
                        {
                            commonName = item.TryGetProperty("vernacularName", out var vn) ? vn.GetString() : null;
                            if (commonName != null) break;
                        }
                    }
                }
            }
            catch { /* vernacular names are optional */ }

            return (commonName, rank, status, family, order);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Failed to fetch species detail for taxonKey={TaxonKey}", taxonKey);
            return (null, null, null, null, null);
        }
    }

    // ── Public data fetch by taxon key ───────────────────────────────────────────

    public async Task<GbifAnimalData?> FetchAnimalDataByKeyAsync(int taxonKey, string canonicalName, CancellationToken ct = default)
    {
        var cacheKey = $"gbif:key:{taxonKey}";
        if (cache.TryGetValue(cacheKey, out GbifAnimalData? cached))
        {
            logger.LogDebug("GBIF cache hit for taxonKey={TaxonKey}", taxonKey);
            return cached;
        }

        logger.LogInformation("GBIF fetching data for taxonKey={TaxonKey}, canonical={Canonical}", taxonKey, canonicalName);

        var taxonomyTask = FetchTaxonomyAsync(taxonKey, canonicalName, ct);
        var descriptionsTask = FetchDescriptionsAsync(taxonKey, ct);
        var iucnTask = FetchIucnStatusAsync(taxonKey, ct);
        var vernacularTask = FetchVernacularNamesAsync(taxonKey, ct);
        var distributionTask = FetchDistributionsAsync(taxonKey, ct);
        var imageTask = FetchBestImageAsync(taxonKey, ct);
        var mapTask = mapService.BuildMapMetadataAsync(taxonKey, ct);

        await Task.WhenAll(taxonomyTask, descriptionsTask, iucnTask,
                           vernacularTask, distributionTask, imageTask, mapTask);

        var descriptions = await descriptionsTask;
        var vernaculars = await vernacularTask;

        var englishName = vernaculars
            .Where(v => v.Language is "eng" or "en")
            .Select(v => v.Name)
            .FirstOrDefault();

        var result = new GbifAnimalData
        {
            TaxonKey = taxonKey,
            CanonicalName = canonicalName,
            EnglishCommonName = englishName,
            Taxonomy = await taxonomyTask,
            HabitatProse = descriptions.GetValueOrDefault("biology_ecology"),
            DietProse = descriptions.GetValueOrDefault("food_feeding"),
            BehaviourProse = descriptions.GetValueOrDefault("activity"),
            PhysicalDescriptionProse = descriptions.GetValueOrDefault("description"),
            BreedingProse = descriptions.GetValueOrDefault("breeding"),
            ConservationProse = descriptions.GetValueOrDefault("conservation"),
            DistributionProse = descriptions.GetValueOrDefault("distribution"),
            IucnCategory = (await iucnTask).Category,
            IucnCode = (await iucnTask).Code,
            IucnTaxonId = (await iucnTask).IucnTaxonId,
            NativeCountries = await distributionTask,
            VernacularNames = vernaculars,
            BestImage = await imageTask,
            MapMetadata = await mapTask,
        };

        cache.Set(cacheKey, result, CacheDuration);
        return result;
    }

    // ── Private resolution steps ──────────────────────────────────────────────

    private async Task<(int TaxonKey, string CanonicalName)?> ResolveSpeciesAsync(
        string name, CancellationToken ct)
    {
        // Strategy: Try multiple approaches to resolve common names to GBIF taxon keys.
        // Priority: Wikipedia (best for common names) → scientific match → vernacular search → base-word fallback

        try
        {
            // Attempt 1: Wikipedia → GBIF (most reliable for common names like "Tiger", "Elephant")
            var result = await TryResolveViaWikipediaAsync(name, ct);
            if (result != null) return result;

            // Attempt 2: Scientific name match (fast, for binomial names)
            result = await TryMatchScientificNameAsync(name, ct);
            if (result != null) return result;

            // Attempt 3: Vernacular name search against GBIF backbone
            result = await TrySearchVernacularNameAsync(name, ct);
            if (result != null) return result;

            // Attempt 4: For multi-word names, try the base word
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
            {
                var baseName = words[^1];
                logger.LogDebug("Trying base name {BaseName} from {Name}", baseName, name);

                result = await TryResolveViaWikipediaAsync(baseName, ct);
                if (result != null) return result;

                result = await TrySearchVernacularNameAsync(baseName, ct);
                if (result != null) return result;
            }

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "GBIF species resolution failed for {Name}", name);
            return null;
        }
    }

    private async Task<(int TaxonKey, string CanonicalName)?> TryResolveViaWikipediaAsync(
        string name, CancellationToken ct)
    {
        // Use Wikipedia to resolve common names → scientific names → GBIF taxon keys.
        // Wikipedia's first sentence almost always contains the binomial name, e.g.:
        //   "The tiger (Panthera tigris) is the largest living cat species..."
        try
        {
            var article = await wikipediaService.GetAnimalArticleAsync(name, ct);
            if (article == null) return null;

            // Extract scientific name from taxonomy info or summary
            // Wikipedia taxonomy sections typically mention the binomial name
            var textToSearch = article.TaxonomyInfo ?? article.Summary;
            if (textToSearch == null) return null;

            // Try to find a binomial name pattern: two capitalised-then-lowercase words
            // that looks like "Genus species" (e.g., "Panthera tigris", "Anas platyrhynchos")
            var binomialMatch = System.Text.RegularExpressions.Regex.Match(
                textToSearch,
                @"\b([A-Z][a-z]+)\s+([a-z]{2,})\b");

            if (!binomialMatch.Success) return null;

            var scientificName = binomialMatch.Value;
            logger.LogDebug("Wikipedia resolved {Name} → scientific name candidate: {Scientific}", name, scientificName);

            // Now look up this scientific name in GBIF
            var result = await TryMatchScientificNameAsync(scientificName, ct);
            if (result != null)
            {
                logger.LogInformation("Wikipedia→GBIF resolution: {Name} → {Scientific} (key={Key})",
                    name, result.Value.CanonicalName, result.Value.TaxonKey);
            }
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Wikipedia→GBIF resolution failed for {Name}", name);
            return null;
        }
    }

    private async Task<(int TaxonKey, string CanonicalName)?> TryMatchScientificNameAsync(
        string name, CancellationToken ct)
    {
        var url = $"{GbifApiBase}/species/match?name={Uri.EscapeDataString(name)}&kingdom=Animalia";
        var json = await GetJsonAsync(url, ct);

        var matchType = json.TryGetProperty("matchType", out var mt) ? mt.GetString() : null;
        if (matchType is "NONE" or null)
            return null;

        // Reject higher-rank matches (kingdom, phylum, class, order, family, genus)
        var rank = json.TryGetProperty("rank", out var r) ? r.GetString() : null;
        if (rank is not ("SPECIES" or "SUBSPECIES"))
            return null;

        // Require at least genus-level confidence for fuzzy matches
        var confidence = json.TryGetProperty("confidence", out var conf) ? conf.GetInt32() : 0;
        if (matchType == "FUZZY" && confidence < 80)
        {
            logger.LogWarning("GBIF fuzzy match for {Name} has low confidence ({Confidence}), skipping",
                name, confidence);
            return null;
        }

        var usageKey = json.TryGetProperty("usageKey", out var uk) ? uk.GetInt32() : 0;
        if (usageKey == 0) return null;

        // If the match is a SYNONYM, follow to the ACCEPTED species
        // e.g., "Felis tigris" (synonym) → "Panthera tigris" (accepted, key 5219416)
        var status = json.TryGetProperty("status", out var st) ? st.GetString() : null;
        if (status is not ("ACCEPTED" or null))
        {
            var acceptedKey = json.TryGetProperty("acceptedUsageKey", out var ak) ? ak.GetInt32() : 0;
            if (acceptedKey > 0)
            {
                // Fetch the accepted species details
                var acceptedUrl = $"{GbifApiBase}/species/{acceptedKey}";
                var accepted = await GetJsonAsync(acceptedUrl, ct);
                var acceptedCanonical = accepted.TryGetProperty("canonicalName", out var acn)
                    ? acn.GetString() : null;
                var acceptedRank = accepted.TryGetProperty("rank", out var ar) ? ar.GetString() : null;

                if (acceptedCanonical != null && acceptedRank is "SPECIES" or "SUBSPECIES")
                {
                    logger.LogDebug("Followed synonym {Name} → accepted {Accepted} (key={Key})",
                        name, acceptedCanonical, acceptedKey);
                    return (acceptedKey, acceptedCanonical);
                }
            }
        }

        var canonicalName = json.TryGetProperty("canonicalName", out var cn)
            ? cn.GetString() ?? name
            : name;

        return (usageKey, canonicalName);
    }

    private async Task<(int TaxonKey, string CanonicalName)?> TrySearchVernacularNameAsync(
        string name, CancellationToken ct)
    {
        // Search GBIF backbone taxonomy by vernacular name, filtered to species rank
        const string backboneDatasetKey = "d7dddbf4-2cf0-4f39-9b2a-bb099caae36c";
        var url = $"{GbifApiBase}/species/search" +
                  $"?q={Uri.EscapeDataString(name)}" +
                  $"&rank=SPECIES" +
                  $"&qField=VERNACULAR" +
                  $"&datasetKey={backboneDatasetKey}" +
                  $"&limit=10";

        var json = await GetJsonAsync(url, ct);

        if (!json.TryGetProperty("results", out var results))
            return null;

        // Prefer ACCEPTED species with an exact vernacular name match
        foreach (var item in results.EnumerateArray())
        {
            var status = item.TryGetProperty("taxonomicStatus", out var ts) ? ts.GetString() : null;
            if (status != "ACCEPTED") continue;

            var key = item.TryGetProperty("nubKey", out var nk) ? nk.GetInt32()
                    : item.TryGetProperty("key", out var k) ? k.GetInt32() : 0;
            if (key == 0) continue;

            var canonicalName = item.TryGetProperty("canonicalName", out var cn) ? cn.GetString() : null;
            if (canonicalName == null) continue;

            // Check if any vernacular name is a case-insensitive match
            // vernacularNames is an array of objects: {"vernacularName": "...", "language": "..."}
            if (item.TryGetProperty("vernacularNames", out var vns))
            {
                var hasMatch = false;
                foreach (var vn in vns.EnumerateArray())
                {
                    var vnStr = vn.ValueKind == JsonValueKind.String
                        ? vn.GetString()
                        : vn.TryGetProperty("vernacularName", out var vnProp) ? vnProp.GetString() : null;
                    if (vnStr != null && vnStr.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        hasMatch = true;
                        break;
                    }
                }

                if (hasMatch)
                {
                    logger.LogInformation("GBIF vernacular match: {Name} -> {Canonical} (key={Key})",
                        name, canonicalName, key);
                    return (key, canonicalName);
                }
            }
        }

        // If no exact match, take the first ACCEPTED result as best guess
        foreach (var item in results.EnumerateArray())
        {
            var status = item.TryGetProperty("taxonomicStatus", out var ts) ? ts.GetString() : null;
            if (status != "ACCEPTED") continue;

            var key = item.TryGetProperty("nubKey", out var nk) ? nk.GetInt32()
                    : item.TryGetProperty("key", out var k) ? k.GetInt32() : 0;
            var canonicalName = item.TryGetProperty("canonicalName", out var cn) ? cn.GetString() : null;

            if (key > 0 && canonicalName != null)
            {
                logger.LogInformation("GBIF vernacular best-guess: {Name} -> {Canonical} (key={Key})",
                    name, canonicalName, key);
                return (key, canonicalName);
            }
        }

        return null;
    }

    private async Task<(int TaxonKey, string CanonicalName)?> TryBroadSearchAsync(
        string name, CancellationToken ct)
    {
        // Search without qField restriction — matches scientific names and vernaculars across all datasets
        // This catches species like "Tiger" (Panthera tigris) where the backbone vernacular is "Bagh"
        const string backboneDatasetKey = "d7dddbf4-2cf0-4f39-9b2a-bb099caae36c";
        var url = $"{GbifApiBase}/species/search" +
                  $"?q={Uri.EscapeDataString(name)}" +
                  $"&rank=SPECIES" +
                  $"&datasetKey={backboneDatasetKey}" +
                  $"&limit=10";

        var json = await GetJsonAsync(url, ct);
        if (!json.TryGetProperty("results", out var results)) return null;

        // Prefer ACCEPTED Animalia species
        foreach (var item in results.EnumerateArray())
        {
            var kingdom = item.TryGetProperty("kingdom", out var kg) ? kg.GetString() : null;
            if (kingdom != null && kingdom != "Animalia") continue;

            var status = item.TryGetProperty("taxonomicStatus", out var ts) ? ts.GetString() : null;
            if (status != "ACCEPTED") continue;

            var key = item.TryGetProperty("nubKey", out var nk) ? nk.GetInt32()
                    : item.TryGetProperty("key", out var k) ? k.GetInt32() : 0;
            var canonicalName = item.TryGetProperty("canonicalName", out var cn) ? cn.GetString() : null;

            if (key > 0 && canonicalName != null)
            {
                logger.LogInformation("GBIF broad search: {Name} -> {Canonical} (key={Key})",
                    name, canonicalName, key);
                return (key, canonicalName);
            }
        }

        return null;
    }

    private async Task<GbifTaxonomyData?> FetchTaxonomyAsync(
        int taxonKey, string canonicalName, CancellationToken ct)
    {
        try
        {
            // Step 1: GBIF backbone taxonomy
            var gbifUrl = $"{GbifApiBase}/species/{taxonKey}";
            var gbif = await GetJsonAsync(gbifUrl, ct);

            string? phylum = gbif.TryGetProperty("phylum", out var p) ? p.GetString() : null;
            string? @class = gbif.TryGetProperty("class", out var c) ? c.GetString() : null;
            string? order = gbif.TryGetProperty("order", out var o) ? o.GetString() : null;
            string? family = gbif.TryGetProperty("family", out var f) ? f.GetString() : null;
            string? genus = gbif.TryGetProperty("genus", out var g) ? g.GetString() : null;
            string? species = gbif.TryGetProperty("species", out var s) ? s.GetString() : null;

            // Step 2: COL for authoritative taxon ID, authorship and synonyms
            string? colTaxonId = null;
            string? authorship = null;
            var synonyms = new List<string>();

            try
            {
                var colUrl = $"{ColApiBase}/dataset/3/nameusage/search" +
                             $"?q={Uri.EscapeDataString(canonicalName)}&content=SCIENTIFIC_NAME&limit=1";
                var col = await GetJsonAsync(colUrl, ct);

                if (col.TryGetProperty("result", out var results) && results.GetArrayLength() > 0)
                {
                    var first = results[0];
                    colTaxonId = first.TryGetProperty("id", out var id) ? id.GetString() : null;

                    if (first.TryGetProperty("name", out var nameObj)
                        && nameObj.TryGetProperty("authorship", out var auth))
                        authorship = auth.GetString();

                    // Collect synonyms from COL
                    if (first.TryGetProperty("synonyms", out var syns))
                    {
                        foreach (var syn in syns.EnumerateArray())
                        {
                            var synName = syn.TryGetProperty("label", out var lbl) ? lbl.GetString() : null;
                            if (synName != null) synonyms.Add(synName);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "COL lookup failed for {CanonicalName}, continuing with GBIF taxonomy only",
                    canonicalName);
            }

            return new GbifTaxonomyData
            {
                Kingdom = "Animalia",
                Phylum = phylum,
                Class = @class,
                Order = order,
                Family = family,
                Genus = genus,
                Species = species,
                ColTaxonId = colTaxonId,
                Authorship = authorship,
                Synonyms = synonyms,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "GBIF taxonomy fetch failed for taxonKey={TaxonKey}", taxonKey);
            return null;
        }
    }

    private async Task<Dictionary<string, string>> FetchDescriptionsAsync(
        int taxonKey, CancellationToken ct)
    {
        var result = new Dictionary<string, string>();

        // Fetch all pages of descriptions in one call, then filter by type client-side
        // GBIF returns all types in a single paginated endpoint
        try
        {
            var url = $"{GbifApiBase}/species/{taxonKey}/descriptions?limit=100";
            var json = await GetJsonAsync(url, ct);

            if (!json.TryGetProperty("results", out var results)) return result;

            foreach (var item in results.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var t) ? t.GetString()?.ToLowerInvariant() : null;
                var description = item.TryGetProperty("description", out var d) ? d.GetString() : null;

                if (type == null || description == null) continue;
                if (!DescriptionTypes.Contains(type)) continue;

                // If multiple entries exist for same type, concatenate (some species have multi-source)
                if (result.ContainsKey(type))
                    result[type] += " " + description;
                else
                    result[type] = description;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "GBIF descriptions fetch failed for taxonKey={TaxonKey}", taxonKey);
        }

        return result;
    }

    private async Task<(string? Category, string? Code, string? IucnTaxonId)> FetchIucnStatusAsync(
        int taxonKey, CancellationToken ct)
    {
        try
        {
            var url = $"{GbifApiBase}/species/{taxonKey}/iucnRedListCategory";
            var json = await GetJsonAsync(url, ct);

            var category = json.TryGetProperty("category", out var cat) ? cat.GetString() : null;
            var code = json.TryGetProperty("code", out var c) ? c.GetString() : null;
            var iucnId = json.TryGetProperty("iucnTaxonID", out var iid) ? iid.GetString() : null;

            return (category, code, iucnId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "GBIF IUCN status unavailable for taxonKey={TaxonKey}", taxonKey);
            return (null, null, null);
        }
    }

    private async Task<List<GbifVernacularName>> FetchVernacularNamesAsync(
        int taxonKey, CancellationToken ct)
    {
        var names = new List<GbifVernacularName>();
        try
        {
            var url = $"{GbifApiBase}/species/{taxonKey}/vernacularNames?limit=100";
            var json = await GetJsonAsync(url, ct);

            if (!json.TryGetProperty("results", out var results)) return names;

            foreach (var item in results.EnumerateArray())
            {
                var name = item.TryGetProperty("vernacularName", out var vn) ? vn.GetString() : null;
                var language = item.TryGetProperty("language", out var lg) ? lg.GetString() : null;
                var source = item.TryGetProperty("source", out var src) ? src.GetString() : null;

                if (name != null)
                    names.Add(new GbifVernacularName { Name = name, Language = language, Source = source });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "GBIF vernacular names unavailable for taxonKey={TaxonKey}", taxonKey);
        }

        return names;
    }

    private async Task<List<string>> FetchDistributionsAsync(int taxonKey, CancellationToken ct)
    {
        var regions = new List<string>();
        try
        {
            var url = $"{GbifApiBase}/species/{taxonKey}/distributions?limit=100";
            var json = await GetJsonAsync(url, ct);

            if (!json.TryGetProperty("results", out var results)) return regions;

            foreach (var item in results.EnumerateArray())
            {
                // Prefer country over locality — localities are often granular municipalities
                // which produce very long strings unsuitable for a children's encyclopedia
                var country = item.TryGetProperty("country", out var ctr) ? ctr.GetString() : null;
                var locality = item.TryGetProperty("locality", out var loc) ? loc.GetString() : null;
                var status = item.TryGetProperty("establishmentMeans", out var es) ? es.GetString() : null;

                // Only include native/naturalised ranges, skip introduced
                if (status is "INTRODUCED" or "INVASIVE") continue;

                // Use country if available; only fall back to locality if it looks like a
                // broad region name (not a municipality — skip entries with pipe-separated lists)
                var region = country;
                if (region == null && locality != null && !locality.Contains('|'))
                    region = locality;

                if (region != null && !regions.Contains(region))
                    regions.Add(region);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "GBIF distributions unavailable for taxonKey={TaxonKey}", taxonKey);
        }

        return regions;
    }

    private async Task<GbifImageResult?> FetchBestImageAsync(int taxonKey, CancellationToken ct)
    {
        try
        {
            // Attempt 1: CC BY 4.0 only (commercially free)
            var result = await TryFetchOccurrenceImageAsync(taxonKey, "CC_BY_4_0", ct);
            if (result != null) return result;

            // Attempt 2: Scientific illustrations from species media endpoint
            // CC BY-NC is intentionally excluded — commercial freedom required
            result = await TryFetchSpeciesMediaImageAsync(taxonKey, ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "GBIF image fetch failed for taxonKey={TaxonKey}", taxonKey);
            return null;
        }
    }

    private async Task<GbifImageResult?> TryFetchOccurrenceImageAsync(
        int taxonKey, string licenseFilter, CancellationToken ct)
    {
        var url = $"{GbifApiBase}/occurrence/search" +
                  $"?taxonKey={taxonKey}" +
                  $"&mediaType=StillImage" +
                  $"&license={licenseFilter}" +
                  $"&basisOfRecord=HUMAN_OBSERVATION" +
                  $"&limit=20" +
                  $"&hasCoordinate=true"; // Ensures wild observation with location

        var json = await GetJsonAsync(url, ct);
        if (!json.TryGetProperty("results", out var results)) return null;

        foreach (var occ in results.EnumerateArray())
        {
            var gbifId = occ.TryGetProperty("key", out var k) ? (int?)k.GetInt32() : null;
            var mediaArray = occ.TryGetProperty("media", out var media) ? media : (JsonElement?)null;

            if (mediaArray == null || gbifId == null) continue;

            foreach (var m in mediaArray.Value.EnumerateArray())
            {
                var type = m.TryGetProperty("type", out var t) ? t.GetString() : null;
                var identifier = m.TryGetProperty("identifier", out var id) ? id.GetString() : null;
                var rights = m.TryGetProperty("rightsHolder", out var rh) ? rh.GetString() : null;
                var publisher = m.TryGetProperty("publisher", out var pub) ? pub.GetString() : null;

                if (type != "StillImage" || identifier == null) continue;

                // Compute MD5 for GBIF cache URL
                var md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(identifier))).ToLowerInvariant();

                return new GbifImageResult
                {
                    Url = identifier,
                    License = licenseFilter == "CC_BY_4_0" ? "CC BY 4.0" : "CC BY-NC 4.0",
                    LicenseUrl = licenseFilter == "CC_BY_4_0"
                        ? "https://creativecommons.org/licenses/by/4.0/"
                        : "https://creativecommons.org/licenses/by-nc/4.0/",
                    RightsHolder = rights,
                    Publisher = publisher,
                    Country = occ.TryGetProperty("countryCode", out var cc) ? cc.GetString() : null,
                    GbifOccurrenceId = gbifId,
                    MediaIdentifierMd5 = md5,
                };
            }
        }

        return null;
    }

    private async Task<GbifImageResult?> TryFetchSpeciesMediaImageAsync(int taxonKey, CancellationToken ct)
    {
        var url = $"{GbifApiBase}/species/{taxonKey}/media?limit=10";
        var json = await GetJsonAsync(url, ct);

        if (!json.TryGetProperty("results", out var results)) return null;

        foreach (var m in results.EnumerateArray())
        {
            var identifier = m.TryGetProperty("identifier", out var id) ? id.GetString() : null;
            var license = m.TryGetProperty("license", out var lic) ? lic.GetString() : null;
            var rights = m.TryGetProperty("rightsHolder", out var rh) ? rh.GetString() : null;

            if (identifier == null) continue;

            return new GbifImageResult
            {
                Url = identifier,
                License = license ?? "Unknown",
                LicenseUrl = license ?? string.Empty,
                RightsHolder = rights,
                Publisher = "Zenodo / GBIF",
            };
        }

        return null;
    }

    private async Task<JsonElement> GetJsonAsync(string url, CancellationToken ct)
    {
        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone(); // Clone to allow disposal of doc
    }
}
