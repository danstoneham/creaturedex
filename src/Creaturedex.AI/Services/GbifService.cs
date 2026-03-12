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
            IucnFromSynonymFallback = iucn.FromSynonym,
            Distribution = await distributionTask,
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

        // Normalize query to Title Case for consistent matching — GBIF vernacular names
        // are stored in Title Case (e.g. "Siberian Tiger" not "Siberian tiger")
        var normalizedQuery = System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(query.ToLower());

        try
        {
            // Strategy 1: Wikipedia lookup — most reliable for common names like "Tiger", "Elephant"
            // Wikipedia articles always use the correct common name and contain the scientific name.
            // This is the PRIMARY strategy for the 99.9% case where users type common names.
            string? wikiFamily = null;
            var wikiResult = await TryResolveViaWikipediaAsync(normalizedQuery, ct);
            if (wikiResult != null)
            {
                var (taxonKey, canonicalName) = wikiResult.Value;
                var detail = await FetchSpeciesDetailAsync(taxonKey, ct);
                wikiFamily = detail.Family;

                // Use the normalized query as the display name — "Bengal Tiger" not "Bagh"
                var displayName = normalizedQuery;

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
            var matchResult = await TryMatchScientificNameAsync(normalizedQuery, ct);
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
                await CollectVernacularSuggestionsAsync(normalizedQuery, suggestions, ct);

                var words = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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

            var status = item.TryGetProperty("taxonomicStatus", out var ts) ? ts.GetString() : null;
            var canonicalName = item.TryGetProperty("canonicalName", out var cn) ? cn.GetString() : null;
            if (canonicalName == null) continue;

            // Extract first English vernacular name from this result before resolving
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

            // Resolve synonyms to their accepted species
            if (status is not ("ACCEPTED" or null))
            {
                var acceptedKey = item.TryGetProperty("acceptedKey", out var ak) ? ak.GetInt32()
                    : item.TryGetProperty("nubKey", out var nk2) ? nk2.GetInt32() : 0;
                if (acceptedKey > 0 && acceptedKey != key)
                {
                    // Follow to accepted species — use its details but keep the vernacular name
                    try
                    {
                        var acceptedUrl = $"{GbifApiBase}/species/{acceptedKey}";
                        var accepted = await GetJsonAsync(acceptedUrl, ct);
                        var acceptedRank = accepted.TryGetProperty("rank", out var ar) ? ar.GetString() : null;
                        if (acceptedRank is "SPECIES" or "SUBSPECIES")
                        {
                            key = acceptedKey;
                            canonicalName = accepted.TryGetProperty("canonicalName", out var acn)
                                ? acn.GetString() ?? canonicalName : canonicalName;
                            status = "ACCEPTED";
                            family = accepted.TryGetProperty("family", out var af) ? af.GetString() : family;
                            order = accepted.TryGetProperty("order", out var ao) ? ao.GetString() : order;
                            rank = acceptedRank;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogDebug(ex, "Failed to resolve synonym {Key} to accepted species", key);
                    }
                }
            }

            // Deduplicate — skip if we already have this accepted species
            if (suggestions.ContainsKey(key)) continue;

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
            IucnFromSynonymFallback = (await iucnTask).FromSynonym,
            Distribution = await distributionTask,
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

            // Try to find a binomial name pattern: "Genus species" (e.g., "Panthera tigris")
            // Scientific names in text are typically italicised or in parentheses, but in
            // plaintext they appear as "Genus species" — we need to skip common-name pairs
            // like "Siberian tiger" or "Amur tiger" that also match [A-Z][a-z]+ [a-z]+.
            // Strategy: collect ALL matches, then prefer ones that look like real Latin binomials
            // (not English words). Real genera rarely match common English words.
            var matches = System.Text.RegularExpressions.Regex.Matches(
                textToSearch,
                @"\b([A-Z][a-z]+)\s+([a-z]{2,})\b");

            if (matches.Count == 0) return null;

            // Common English words that appear as first word in "Adjective noun" patterns
            // but are NOT valid genera — skip these as likely common names
            var commonEnglishFirstWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "The", "This", "That", "These", "Those", "Some", "Many", "Most", "Several",
                "All", "Any", "Each", "Every", "Both", "Few", "Other", "Such", "More",
                "African", "American", "Asian", "Australian", "European", "Arctic", "Antarctic",
                "Northern", "Southern", "Eastern", "Western", "Central", "Greater", "Lesser",
                "Giant", "Common", "Golden", "Great", "Little", "Long", "Short", "Red", "Blue",
                "Green", "Black", "White", "Brown", "Grey", "Gray", "Yellow", "Wild", "Royal",
                "Indian", "Chinese", "Japanese", "Siberian", "Bengal", "Amur", "Nile",
                "Pacific", "Atlantic", "Mediterranean", "Himalayan", "Andean", "Amazonian",
                "It", "Its", "They", "Their", "There", "Here", "Where", "When", "While",
                "One", "Two", "Three", "Four", "Five", "First", "Second", "Third",
            };

            string? scientificName = null;
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var genus = m.Groups[1].Value;
                if (!commonEnglishFirstWords.Contains(genus))
                {
                    scientificName = m.Value;
                    break;
                }
            }

            // Fall back to first match if no non-English match found
            scientificName ??= matches[0].Value;
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

                    // COL API nests data under "usage" — the top-level "id" is a search result ID
                    var usageNode = first.TryGetProperty("usage", out var usage) ? usage : first;

                    colTaxonId = usageNode.TryGetProperty("id", out var id) ? id.GetString() : null;

                    // Authorship is under usage.name.authorship
                    if (usageNode.TryGetProperty("name", out var nameObj)
                        && nameObj.TryGetProperty("authorship", out var auth))
                        authorship = auth.GetString();

                    // Fallback: top-level id if usage didn't have one
                    colTaxonId ??= first.TryGetProperty("id", out var topId) ? topId.GetString() : null;

                    // Collect synonyms from COL (may be at top level or under usage)
                    var synsSource = first.TryGetProperty("synonyms", out var syns) ? syns
                        : usageNode.TryGetProperty("synonyms", out var usyns) ? usyns
                        : (JsonElement?)null;
                    if (synsSource.HasValue)
                    {
                        foreach (var syn in synsSource.Value.EnumerateArray())
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

            // GBIF backbone sometimes places orders at the class level (e.g. Squamata as CLASS
            // instead of CLASS=Reptilia, ORDER=Squamata). When class is set but order is null,
            // check if the "class" is actually an order by looking at its rank in GBIF.
            if (@class != null && order == null)
            {
                try
                {
                    var classKeyProp = gbif.TryGetProperty("classKey", out var ck) ? (int?)ck.GetInt32() : null;
                    if (classKeyProp.HasValue)
                    {
                        var classSpecies = await GetJsonAsync($"{GbifApiBase}/species/{classKeyProp.Value}", ct);
                        var classRank = classSpecies.TryGetProperty("rank", out var cr) ? cr.GetString() : null;

                        // If GBIF says this "class" is actually ranked as ORDER, fix it
                        if (classRank == "ORDER")
                        {
                            order = @class;
                            // Look up the real class from the parent
                            var classParentKey = classSpecies.TryGetProperty("parentKey", out var cpk) ? (int?)cpk.GetInt32() : null;
                            if (classParentKey.HasValue)
                            {
                                var parentSpecies = await GetJsonAsync($"{GbifApiBase}/species/{classParentKey.Value}", ct);
                                var parentRank = parentSpecies.TryGetProperty("rank", out var pr) ? pr.GetString() : null;
                                if (parentRank == "CLASS")
                                    @class = parentSpecies.TryGetProperty("canonicalName", out var pcn) ? pcn.GetString() : @class;
                            }
                            logger.LogInformation("Fixed taxonomy: class={Class}, order={Order} (was class={OrigClass}, order=null)",
                                @class, order, order);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogDebug(ex, "Taxonomy rank check failed, using GBIF values as-is");
                }
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

    private async Task<(string? Category, string? Code, string? IucnTaxonId, bool FromSynonym)> FetchIucnStatusAsync(
        int taxonKey, CancellationToken ct)
    {
        try
        {
            var url = $"{GbifApiBase}/species/{taxonKey}/iucnRedListCategory";
            var json = await GetJsonAsync(url, ct);

            var category = json.TryGetProperty("category", out var cat) ? cat.GetString() : null;
            var code = json.TryGetProperty("code", out var c) ? c.GetString() : null;
            var iucnId = json.TryGetProperty("iucnTaxonID", out var iid) ? iid.GetString() : null;

            // If NOT_EVALUATED, try fallback strategies for taxonomic splits.
            // GBIF sometimes splits species (e.g. Giraffa reticulata) but IUCN still
            // assesses them under the old subspecies name (G. camelopardalis ssp. reticulata).
            // Strategy: check synonyms — if any synonym is a subspecies (trinomial name),
            // extract the parent species name and check its IUCN status.
            var fromSynonym = false;
            if (category is "NOT_EVALUATED" or "NOT_APPLICABLE")
            {
                logger.LogDebug("IUCN status is {Category} for taxonKey={TaxonKey}, trying synonym fallback", category, taxonKey);
                try
                {
                    var synsUrl = $"{GbifApiBase}/species/{taxonKey}/synonyms?limit=20";
                    var synsJson = await GetJsonAsync(synsUrl, ct);
                    if (synsJson.TryGetProperty("results", out var synResults))
                    {
                        foreach (var syn in synResults.EnumerateArray())
                        {
                            var synName = syn.TryGetProperty("canonicalName", out var scn) ? scn.GetString() : null;
                            if (synName == null) continue;

                            // Look for subspecies synonyms with trinomial names (Genus species subspecies)
                            var parts = synName.Split(' ');
                            if (parts.Length >= 3)
                            {
                                var parentSpeciesName = $"{parts[0]} {parts[1]}";
                                var matchUrl = $"{GbifApiBase}/species/match?name={Uri.EscapeDataString(parentSpeciesName)}&kingdom=Animalia";
                                var matchJson = await GetJsonAsync(matchUrl, ct);
                                var matchKey = matchJson.TryGetProperty("usageKey", out var mk) ? (int?)mk.GetInt32() : null;
                                var matchRank = matchJson.TryGetProperty("rank", out var mr) ? mr.GetString() : null;

                                if (matchKey.HasValue && matchKey.Value != taxonKey && matchRank == "SPECIES")
                                {
                                    var altIucnUrl = $"{GbifApiBase}/species/{matchKey.Value}/iucnRedListCategory";
                                    var altIucn = await GetJsonAsync(altIucnUrl, ct);
                                    var altCategory = altIucn.TryGetProperty("category", out var acat) ? acat.GetString() : null;
                                    if (altCategory != null && altCategory is not ("NOT_EVALUATED" or "NOT_APPLICABLE"))
                                    {
                                        logger.LogInformation(
                                            "IUCN fallback via synonym: taxonKey={TaxonKey} → synonym {SynName} → {ParentSpecies} ({AltCategory})",
                                            taxonKey, synName, parentSpeciesName, altCategory);
                                        category = altCategory;
                                        code = altIucn.TryGetProperty("code", out var ac) ? ac.GetString() : code;
                                        iucnId = altIucn.TryGetProperty("iucnTaxonID", out var aid) ? aid.GetString() : iucnId;
                                        fromSynonym = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogDebug(ex, "IUCN synonym fallback failed for taxonKey={TaxonKey}", taxonKey);
                }
            }

            return (category, code, iucnId, fromSynonym);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "GBIF IUCN status unavailable for taxonKey={TaxonKey}", taxonKey);
            return (null, null, null, false);
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

    private async Task<GbifDistributionData> FetchDistributionsAsync(int taxonKey, CancellationToken ct)
    {
        var result = new GbifDistributionData();
        try
        {
            var url = $"{GbifApiBase}/species/{taxonKey}/distributions?limit=100";
            var json = await GetJsonAsync(url, ct);

            if (!json.TryGetProperty("results", out var results)) return result;

            // Overly broad, non-specific, or technical locality values to filter out
            var broadLocalities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Global", "Worldwide", "Cosmopolitan", "Pantropical", "Circumtropical",
                "Unknown", "Various", "Multiple"
            };

            // Patterns to skip: OSPAR regions, technical ocean zones, etc.
            var skipPatterns = new[] { "OSPAR", "FAO", "ICES", "EEZ" };
            var continentSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in results.EnumerateArray())
            {
                var countryCode = item.TryGetProperty("country", out var ctr) ? ctr.GetString() : null;
                var locality = item.TryGetProperty("locality", out var loc) ? loc.GetString() : null;
                var means = item.TryGetProperty("establishmentMeans", out var es) ? es.GetString() : null;

                // Only include native/naturalised ranges, skip introduced
                if (means is "INTRODUCED" or "INVASIVE") continue;

                if (countryCode != null)
                {
                    var countryName = ResolveCountryName(countryCode);
                    if (!result.Countries.Contains(countryName, StringComparer.OrdinalIgnoreCase))
                        result.Countries.Add(countryName);

                    // Derive continent from ISO country code
                    var continent = CountryToContinent(countryCode);
                    if (continent != null)
                        continentSet.Add(continent);
                }
                else if (locality != null)
                {
                    // Skip subnational code lists (e.g. "BR-AL;BR-AP;BR-BA;...")
                    if (locality.Contains(';') || locality.Contains('|')) continue;
                    // Skip overly broad/generic localities
                    if (broadLocalities.Contains(locality.TrimEnd(',', ' '))) continue;
                    // Skip entries with ISO-like subnational codes (e.g. "BR-AL")
                    if (System.Text.RegularExpressions.Regex.IsMatch(locality, @"^[A-Z]{2}-[A-Z]{1,3}$")) continue;
                    // Skip technical/regulatory zone names (OSPAR, FAO, etc.)
                    if (skipPatterns.Any(p => locality.Contains(p, StringComparison.OrdinalIgnoreCase))) continue;

                    var cleaned = locality.TrimEnd(',', ' ', '.');
                    // Check if locality is a continent name
                    if (IsContinent(cleaned))
                    {
                        continentSet.Add(NormaliseContinentName(cleaned));
                    }
                    else if (!result.Countries.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Countries.Add(cleaned);
                    }
                }
            }

            result.Continents.AddRange(continentSet.Order());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "GBIF distributions unavailable for taxonKey={TaxonKey}", taxonKey);
        }

        return result;
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
            var gbifId = occ.TryGetProperty("key", out var k) ? (long?)k.GetInt64() : null;
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

    // ── Country name resolution ─────────────────────────────────────────────

    private static string ResolveCountryName(string isoCode)
    {
        try
        {
            var region = new System.Globalization.RegionInfo(isoCode);
            return region.EnglishName;
        }
        catch
        {
            return isoCode; // Return raw code if unrecognised
        }
    }

    // ── Continent mapping from ISO 2-letter country codes ───────────────────

    private static readonly Dictionary<string, string> CountryToContinentMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Africa
        ["DZ"] = "Africa", ["AO"] = "Africa", ["BJ"] = "Africa", ["BW"] = "Africa", ["BF"] = "Africa",
        ["BI"] = "Africa", ["CM"] = "Africa", ["CV"] = "Africa", ["CF"] = "Africa", ["TD"] = "Africa",
        ["KM"] = "Africa", ["CG"] = "Africa", ["CD"] = "Africa", ["CI"] = "Africa", ["DJ"] = "Africa",
        ["EG"] = "Africa", ["GQ"] = "Africa", ["ER"] = "Africa", ["SZ"] = "Africa", ["ET"] = "Africa",
        ["GA"] = "Africa", ["GM"] = "Africa", ["GH"] = "Africa", ["GN"] = "Africa", ["GW"] = "Africa",
        ["KE"] = "Africa", ["LS"] = "Africa", ["LR"] = "Africa", ["LY"] = "Africa", ["MG"] = "Africa",
        ["MW"] = "Africa", ["ML"] = "Africa", ["MR"] = "Africa", ["MU"] = "Africa", ["MA"] = "Africa",
        ["MZ"] = "Africa", ["NA"] = "Africa", ["NE"] = "Africa", ["NG"] = "Africa", ["RW"] = "Africa",
        ["ST"] = "Africa", ["SN"] = "Africa", ["SC"] = "Africa", ["SL"] = "Africa", ["SO"] = "Africa",
        ["ZA"] = "Africa", ["SS"] = "Africa", ["SD"] = "Africa", ["TZ"] = "Africa", ["TG"] = "Africa",
        ["TN"] = "Africa", ["UG"] = "Africa", ["ZM"] = "Africa", ["ZW"] = "Africa", ["RE"] = "Africa",
        ["YT"] = "Africa", ["SH"] = "Africa", ["EH"] = "Africa",
        // Asia
        ["AF"] = "Asia", ["AM"] = "Asia", ["AZ"] = "Asia", ["BH"] = "Asia", ["BD"] = "Asia",
        ["BT"] = "Asia", ["BN"] = "Asia", ["KH"] = "Asia", ["CN"] = "Asia", ["CY"] = "Asia",
        ["GE"] = "Asia", ["IN"] = "Asia", ["ID"] = "Asia", ["IR"] = "Asia", ["IQ"] = "Asia",
        ["IL"] = "Asia", ["JP"] = "Asia", ["JO"] = "Asia", ["KZ"] = "Asia", ["KW"] = "Asia",
        ["KG"] = "Asia", ["LA"] = "Asia", ["LB"] = "Asia", ["MY"] = "Asia", ["MV"] = "Asia",
        ["MN"] = "Asia", ["MM"] = "Asia", ["NP"] = "Asia", ["KP"] = "Asia", ["OM"] = "Asia",
        ["PK"] = "Asia", ["PS"] = "Asia", ["PH"] = "Asia", ["QA"] = "Asia", ["SA"] = "Asia",
        ["SG"] = "Asia", ["KR"] = "Asia", ["LK"] = "Asia", ["SY"] = "Asia", ["TW"] = "Asia",
        ["TJ"] = "Asia", ["TH"] = "Asia", ["TL"] = "Asia", ["TR"] = "Asia", ["TM"] = "Asia",
        ["AE"] = "Asia", ["UZ"] = "Asia", ["VN"] = "Asia", ["YE"] = "Asia",
        // Europe
        ["AL"] = "Europe", ["AD"] = "Europe", ["AT"] = "Europe", ["BY"] = "Europe", ["BE"] = "Europe",
        ["BA"] = "Europe", ["BG"] = "Europe", ["HR"] = "Europe", ["CZ"] = "Europe", ["DK"] = "Europe",
        ["EE"] = "Europe", ["FI"] = "Europe", ["FR"] = "Europe", ["DE"] = "Europe", ["GR"] = "Europe",
        ["HU"] = "Europe", ["IS"] = "Europe", ["IE"] = "Europe", ["IT"] = "Europe", ["XK"] = "Europe",
        ["LV"] = "Europe", ["LI"] = "Europe", ["LT"] = "Europe", ["LU"] = "Europe", ["MT"] = "Europe",
        ["MD"] = "Europe", ["MC"] = "Europe", ["ME"] = "Europe", ["NL"] = "Europe", ["MK"] = "Europe",
        ["NO"] = "Europe", ["PL"] = "Europe", ["PT"] = "Europe", ["RO"] = "Europe", ["RU"] = "Europe",
        ["SM"] = "Europe", ["RS"] = "Europe", ["SK"] = "Europe", ["SI"] = "Europe", ["ES"] = "Europe",
        ["SE"] = "Europe", ["CH"] = "Europe", ["UA"] = "Europe", ["GB"] = "Europe", ["VA"] = "Europe",
        // North America
        ["AG"] = "North America", ["BS"] = "North America", ["BB"] = "North America", ["BZ"] = "North America",
        ["CA"] = "North America", ["CR"] = "North America", ["CU"] = "North America", ["DM"] = "North America",
        ["DO"] = "North America", ["SV"] = "North America", ["GD"] = "North America", ["GT"] = "North America",
        ["HT"] = "North America", ["HN"] = "North America", ["JM"] = "North America", ["MX"] = "North America",
        ["NI"] = "North America", ["PA"] = "North America", ["KN"] = "North America", ["LC"] = "North America",
        ["VC"] = "North America", ["TT"] = "North America", ["US"] = "North America", ["GL"] = "North America",
        ["PR"] = "North America", ["VI"] = "North America",
        // South America
        ["AR"] = "South America", ["BO"] = "South America", ["BR"] = "South America", ["CL"] = "South America",
        ["CO"] = "South America", ["EC"] = "South America", ["GY"] = "South America", ["PY"] = "South America",
        ["PE"] = "South America", ["SR"] = "South America", ["UY"] = "South America", ["VE"] = "South America",
        ["GF"] = "South America", ["FK"] = "South America",
        // Oceania
        ["AU"] = "Oceania", ["FJ"] = "Oceania", ["KI"] = "Oceania", ["MH"] = "Oceania",
        ["FM"] = "Oceania", ["NR"] = "Oceania", ["NZ"] = "Oceania", ["PW"] = "Oceania",
        ["PG"] = "Oceania", ["WS"] = "Oceania", ["SB"] = "Oceania", ["TO"] = "Oceania",
        ["TV"] = "Oceania", ["VU"] = "Oceania", ["NC"] = "Oceania", ["GU"] = "Oceania",
        // Antarctica
        ["AQ"] = "Antarctica",
    };

    private static string? CountryToContinent(string isoCode) =>
        CountryToContinentMap.TryGetValue(isoCode, out var continent) ? continent : null;

    private static readonly HashSet<string> ContinentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Africa", "Asia", "Europe", "North America", "South America", "Oceania",
        "Antarctica", "Central America", "Northern Africa", "Southern Africa",
        "Eastern Africa", "Western Africa", "Sub-Saharan Africa",
        "Southeast Asia", "South Asia", "East Asia", "Central Asia",
        "Northern Europe", "Southern Europe", "Western Europe", "Eastern Europe",
    };

    private static bool IsContinent(string value) => ContinentNames.Contains(value);

    private static string NormaliseContinentName(string value)
    {
        // Map sub-regions to their parent continent
        if (value.Contains("Africa", StringComparison.OrdinalIgnoreCase)) return "Africa";
        if (value.Contains("Asia", StringComparison.OrdinalIgnoreCase)) return "Asia";
        if (value.Contains("Europe", StringComparison.OrdinalIgnoreCase)) return "Europe";
        if (value.Contains("America", StringComparison.OrdinalIgnoreCase))
            return value.Contains("South", StringComparison.OrdinalIgnoreCase) ? "South America"
                : value.Contains("Central", StringComparison.OrdinalIgnoreCase) ? "North America"
                : "North America";
        return value;
    }
}
