# GBIF Integration Plan for Creaturedex
## GbifService, GbifMapService & ContentGeneratorService Refactor

---

## Overview

This document describes the full implementation plan for replacing Wikipedia as the primary factual
data source with GBIF (Global Biodiversity Information Facility) and COL (Catalogue of Life).
The goal is to produce authoritative, structured data that Ollama can format into child-friendly prose,
rather than having the AI perform taxonomic research from unstructured Wikipedia text.

### Architecture Principle

```
Common Name
    │
    ▼
GbifService.ResolveSpeciesAsync()         ← GBIF /v1/species/match
    │  → taxonKey + canonicalName
    │
    ├──▶ GbifService.FetchTaxonomyAsync()     ← COL checklistbank API
    │        → GbifTaxonomyData
    │
    ├──▶ GbifService.FetchEcologyAsync()      ← GBIF /v1/species/{key}/descriptions (parallel)
    │        → habitat, diet, behaviour, size, breeding, conservation prose
    │
    ├──▶ GbifService.FetchMetadataAsync()     ← IUCN status, vernacular names, distributions
    │        → conservation status, native regions, common name variants
    │
    ├──▶ GbifService.FetchBestImageAsync()    ← GBIF occurrence search (CC BY 4.0 filtered)
    │        → GbifImageResult
    │
    └──▶ GbifMapService.BuildMapMetadataAsync() ← GBIF Maps capabilities endpoint
             → GbifMapMetadata (tile URL + bbox + count)

All data → GbifAnimalData record
    │
    ▼
ContentGeneratorService
    → Injects GbifAnimalData as structured reference
    → Ollama writes: Summary, Description, FunFacts, Tags, PetCareGuide
    → Ollama no longer does taxonomy research or ecological fact-finding
```

---

## Part 1: New Entity Fields

### 1.1 Animal Entity — New Fields

Add the following nullable fields to `Creaturedex.Core/Entities/Animal.cs`:

```csharp
// GBIF identifiers (persisted to avoid re-resolution)
public int? GbifTaxonKey { get; set; }
public string? GbifCanonicalName { get; set; }

// Map metadata (stored so the frontend can render without calling our API)
public string? MapTileUrlTemplate { get; set; }   // e.g. the parameterised GBIF tile URL
public int? MapObservationCount { get; set; }
public double? MapMinLat { get; set; }
public double? MapMaxLat { get; set; }
public double? MapMinLng { get; set; }
public double? MapMaxLng { get; set; }

// Image attribution (required for CC BY compliance)
public string? ImageLicense { get; set; }          // e.g. "CC BY 4.0"
public string? ImageRightsHolder { get; set; }     // e.g. "Eric Watts"
public string? ImageSource { get; set; }           // e.g. "iNaturalist via GBIF"
```

### 1.2 Taxonomy Entity — New Fields

Add to `Creaturedex.Core/Entities/Taxonomy.cs`:

```csharp
public string? ColTaxonId { get; set; }       // COL stable identifier, e.g. "hC9w0Jqp4yPf8x4BHiH-21"
public string? Authorship { get; set; }        // e.g. "(Linnaeus, 1758)"
public string? Synonyms { get; set; }          // JSON array of synonym strings
```

> **Migration note:** Add EF Core migration for these nullable fields. No data loss risk.

---

## Part 2: GbifAnimalData Record

Create `Creaturedex.AI/Models/GbifAnimalData.cs`:

```csharp
namespace Creaturedex.AI.Models;

/// <summary>
/// All data retrievable from GBIF and COL for a single species.
/// Fields map directly to Animal/Taxonomy entity properties.
/// Null indicates data was unavailable from the source.
/// </summary>
public record GbifAnimalData
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public required int TaxonKey { get; init; }
    public required string CanonicalName { get; init; }          // Scientific binomial
    public string? EnglishCommonName { get; init; }              // Best English vernacular match

    // ── Taxonomy (maps to Taxonomy entity) ───────────────────────────────────
    public GbifTaxonomyData? Taxonomy { get; init; }

    // ── Ecology (maps to Animal entity string fields) ─────────────────────────
    public string? HabitatProse { get; init; }                   // from descriptions type=biology_ecology
    public string? DietProse { get; init; }                      // from descriptions type=food_feeding
    public string? BehaviourProse { get; init; }                 // from descriptions type=activity
    public string? PhysicalDescriptionProse { get; init; }       // from descriptions type=description
    public string? BreedingProse { get; init; }                  // from descriptions type=breeding (contains lifespan)
    public string? ConservationProse { get; init; }              // from descriptions type=conservation
    public string? DistributionProse { get; init; }              // from descriptions type=distribution

    // ── Conservation ─────────────────────────────────────────────────────────
    public string? IucnCategory { get; init; }                   // e.g. "VULNERABLE"
    public string? IucnCode { get; init; }                       // e.g. "VU"
    public string? IucnTaxonId { get; init; }                    // IUCN Red List ID

    // ── Distribution ─────────────────────────────────────────────────────────
    public IReadOnlyList<string> NativeCountries { get; init; } = [];
    public IReadOnlyList<string> NativeRegionSummaries { get; init; } = [];  // Human-readable region descriptions

    // ── Vernacular Names ──────────────────────────────────────────────────────
    public IReadOnlyList<GbifVernacularName> VernacularNames { get; init; } = [];

    // ── Image ─────────────────────────────────────────────────────────────────
    public GbifImageResult? BestImage { get; init; }

    // ── Map ───────────────────────────────────────────────────────────────────
    public GbifMapMetadata? MapMetadata { get; init; }
}

public record GbifTaxonomyData
{
    public string Kingdom { get; init; } = "Animalia";
    public string? Phylum { get; init; }
    public string? Class { get; init; }
    public string? Order { get; init; }
    public string? Family { get; init; }
    public string? Genus { get; init; }
    public string? Species { get; init; }
    public string? Subspecies { get; init; }
    public string? ColTaxonId { get; init; }
    public string? Authorship { get; init; }
    public IReadOnlyList<string> Synonyms { get; init; } = [];
}

public record GbifVernacularName
{
    public required string Name { get; init; }
    public string? Language { get; init; }      // ISO 639-1 code, e.g. "eng", "deu"
    public string? Source { get; init; }
}

public record GbifImageResult
{
    public required string Url { get; init; }               // Direct S3 or cached GBIF URL
    public required string License { get; init; }           // "CC_BY_4_0" or "CC_BY_NC_4_0"
    public required string LicenseUrl { get; init; }
    public string? RightsHolder { get; init; }
    public string? Publisher { get; init; }
    public string? Country { get; init; }                   // Where it was photographed
    public int? GbifOccurrenceId { get; init; }
    public string? MediaIdentifierMd5 { get; init; }        // For building GBIF cache URL
    public string CachedUrl => MediaIdentifierMd5 != null && GbifOccurrenceId != null
        ? $"https://api.gbif.org/v1/image/cache/occurrence/{GbifOccurrenceId}/media/{MediaIdentifierMd5}"
        : Url;
}

public record GbifMapMetadata
{
    public required int TaxonKey { get; init; }
    public required string TileUrlTemplate { get; init; }   // With {z}/{x}/{y} placeholders
    public int ObservationCount { get; init; }
    public double? MinLat { get; init; }
    public double? MaxLat { get; init; }
    public double? MinLng { get; init; }
    public double? MaxLng { get; init; }
    public int? MinYear { get; init; }
    public int? MaxYear { get; init; }
}
```

---

## Part 3: GbifService

Create `Creaturedex.AI/Services/GbifService.cs`:

### 3.1 Class Signature & Constructor

```csharp
using Creaturedex.AI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Creaturedex.AI.Services;

public class GbifService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<GbifService> logger)
{
    private const string GbifApiBase = "https://api.gbif.org/v1";
    private const string GbifMapsBase = "https://api.gbif.org/v2";
    private const string ColApiBase = "https://api.checklistbank.org";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<GbifAnimalData?> FetchAnimalDataAsync(string animalName, CancellationToken ct = default);

    // ── Private resolution steps ──────────────────────────────────────────────

    private async Task<(int TaxonKey, string CanonicalName)?> ResolveSpeciesAsync(string name, CancellationToken ct);
    private async Task<GbifTaxonomyData?> FetchTaxonomyAsync(int taxonKey, string canonicalName, CancellationToken ct);
    private async Task<Dictionary<string, string>> FetchDescriptionsAsync(int taxonKey, CancellationToken ct);
    private async Task<(string? Category, string? Code, string? IucnTaxonId)> FetchIucnStatusAsync(int taxonKey, CancellationToken ct);
    private async Task<List<GbifVernacularName>> FetchVernacularNamesAsync(int taxonKey, CancellationToken ct);
    private async Task<List<string>> FetchDistributionsAsync(int taxonKey, CancellationToken ct);
    private async Task<GbifImageResult?> FetchBestImageAsync(int taxonKey, CancellationToken ct);
    private async Task<GbifMapMetadata?> FetchMapMetadataAsync(int taxonKey, CancellationToken ct);
}
```

### 3.2 FetchAnimalDataAsync — Main Orchestrator

```csharp
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
    logger.LogInformation("GBIF resolved {AnimalName} → taxonKey={TaxonKey}, canonical={Canonical}",
        animalName, taxonKey, canonicalName);

    // Fan out all requests in parallel — each is independent
    var taxonomyTask     = FetchTaxonomyAsync(taxonKey, canonicalName, ct);
    var descriptionsTask = FetchDescriptionsAsync(taxonKey, ct);
    var iucnTask         = FetchIucnStatusAsync(taxonKey, ct);
    var vernacularTask   = FetchVernacularNamesAsync(taxonKey, ct);
    var distributionTask = FetchDistributionsAsync(taxonKey, ct);
    var imageTask        = FetchBestImageAsync(taxonKey, ct);
    var mapTask          = FetchMapMetadataAsync(taxonKey, ct);

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
```

### 3.3 ResolveSpeciesAsync

```csharp
private async Task<(int TaxonKey, string CanonicalName)?> ResolveSpeciesAsync(
    string name, CancellationToken ct)
{
    try
    {
        var url = $"{GbifApiBase}/species/match?name={Uri.EscapeDataString(name)}&kingdom=Animalia";
        var json = await GetJsonAsync(url, ct);

        // matchType can be: EXACT, FUZZY, HIGHERRANK, NONE
        var matchType = json.TryGetProperty("matchType", out var mt) ? mt.GetString() : null;
        if (matchType is "NONE" or null)
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
        var canonicalName = json.TryGetProperty("canonicalName", out var cn)
            ? cn.GetString() ?? name
            : name;

        if (usageKey == 0)
            return null;

        return (usageKey, canonicalName);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogWarning(ex, "GBIF species resolution failed for {Name}", name);
        return null;
    }
}
```

### 3.4 FetchTaxonomyAsync (GBIF + COL)

```csharp
private async Task<GbifTaxonomyData?> FetchTaxonomyAsync(
    int taxonKey, string canonicalName, CancellationToken ct)
{
    try
    {
        // Step 1: GBIF backbone taxonomy
        var gbifUrl = $"{GbifApiBase}/species/{taxonKey}";
        var gbif = await GetJsonAsync(gbifUrl, ct);

        string? phylum  = gbif.TryGetProperty("phylum",  out var p)  ? p.GetString()  : null;
        string? @class  = gbif.TryGetProperty("class",   out var c)  ? c.GetString()  : null;
        string? order   = gbif.TryGetProperty("order",   out var o)  ? o.GetString()  : null;
        string? family  = gbif.TryGetProperty("family",  out var f)  ? f.GetString()  : null;
        string? genus   = gbif.TryGetProperty("genus",   out var g)  ? g.GetString()  : null;
        string? species = gbif.TryGetProperty("species", out var s)  ? s.GetString()  : null;

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
            Kingdom   = "Animalia",
            Phylum    = phylum,
            Class     = @class,
            Order     = order,
            Family    = family,
            Genus     = genus,
            Species   = species,
            ColTaxonId  = colTaxonId,
            Authorship  = authorship,
            Synonyms    = synonyms,
        };
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogWarning(ex, "GBIF taxonomy fetch failed for taxonKey={TaxonKey}", taxonKey);
        return null;
    }
}
```

### 3.5 FetchDescriptionsAsync (parallel by type)

```csharp
private static readonly string[] DescriptionTypes =
[
    "biology_ecology", "food_feeding", "activity",
    "description", "breeding", "conservation", "distribution"
];

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
```

### 3.6 FetchIucnStatusAsync

```csharp
private async Task<(string? Category, string? Code, string? IucnTaxonId)> FetchIucnStatusAsync(
    int taxonKey, CancellationToken ct)
{
    try
    {
        var url = $"{GbifApiBase}/species/{taxonKey}/iucnRedListCategory";
        var json = await GetJsonAsync(url, ct);

        var category  = json.TryGetProperty("category",    out var cat)  ? cat.GetString()  : null;
        var code      = json.TryGetProperty("code",        out var c)    ? c.GetString()    : null;
        var iucnId    = json.TryGetProperty("iucnTaxonID", out var iid)  ? iid.GetString()  : null;

        return (category, code, iucnId);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogDebug(ex, "GBIF IUCN status unavailable for taxonKey={TaxonKey}", taxonKey);
        return (null, null, null);
    }
}
```

### 3.7 FetchVernacularNamesAsync

```csharp
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
            var name     = item.TryGetProperty("vernacularName", out var vn) ? vn.GetString() : null;
            var language = item.TryGetProperty("language",       out var lg) ? lg.GetString() : null;
            var source   = item.TryGetProperty("source",         out var src) ? src.GetString() : null;

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
```

### 3.8 FetchDistributionsAsync

```csharp
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
            // Prefer locality string if present, fall back to country
            var locality = item.TryGetProperty("locality", out var loc) ? loc.GetString() : null;
            var country  = item.TryGetProperty("country",  out var ctr) ? ctr.GetString() : null;
            var status   = item.TryGetProperty("establishmentMeans", out var es) ? es.GetString() : null;

            // Only include native/naturalised ranges, skip introduced
            if (status is "INTRODUCED" or "INVASIVE") continue;

            var region = locality ?? country;
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
```

### 3.9 FetchBestImageAsync — Image Selection Strategy

**Priority order:**
1. CC BY 4.0 (`license=CC_BY_4_0`) — fully free, commercially usable + must pass AI child-safety screen
2. GBIF `/species/{key}/media` scientific illustrations (Zenodo) — CC licensed, high quality but static
3. If no suitable GBIF image, return null → ContentGeneratorService falls back to Wikipedia, then AI-generated

> **Note:** CC BY-NC 4.0 images are intentionally excluded to ensure commercial freedom.

**Quality heuristics:**
- Filter `basisOfRecord=HUMAN_OBSERVATION` to exclude zoo/lab/fossil photos
- Prefer landscape orientation (width > height from image dimensions if available)
- Take the most recent photograph (`eventDate DESC`) as a proxy for quality
- Exclude known poor subjects: no filtering is perfect, so the admin review step remains essential

```csharp
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
              $"&hasCoordinate=true";  // Ensures wild observation with location

    var json = await GetJsonAsync(url, ct);
    if (!json.TryGetProperty("results", out var results)) return null;

    foreach (var occ in results.EnumerateArray())
    {
        var license    = occ.TryGetProperty("license", out var lic)    ? lic.GetString()    : null;
        var gbifId     = occ.TryGetProperty("key",     out var k)      ? (int?)k.GetInt32() : null;
        var mediaArray = occ.TryGetProperty("media",   out var media)  ? media              : (JsonElement?)null;

        if (mediaArray == null || gbifId == null) continue;

        foreach (var m in mediaArray.Value.EnumerateArray())
        {
            var type       = m.TryGetProperty("type",         out var t)   ? t.GetString()   : null;
            var identifier = m.TryGetProperty("identifier",   out var id)  ? id.GetString()  : null;
            var rights     = m.TryGetProperty("rightsHolder", out var rh)  ? rh.GetString()  : null;
            var publisher  = m.TryGetProperty("publisher",    out var pub) ? pub.GetString()  : null;

            if (type != "StillImage" || identifier == null) continue;

            // Compute MD5 for GBIF cache URL
            var md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(identifier))).ToLowerInvariant();

            return new GbifImageResult
            {
                Url                  = identifier,
                License              = licenseFilter == "CC_BY_4_0" ? "CC BY 4.0" : "CC BY-NC 4.0",
                LicenseUrl           = licenseFilter == "CC_BY_4_0"
                    ? "https://creativecommons.org/licenses/by/4.0/"
                    : "https://creativecommons.org/licenses/by-nc/4.0/",
                RightsHolder         = rights,
                Publisher            = publisher,
                Country              = occ.TryGetProperty("countryCode", out var cc) ? cc.GetString() : null,
                GbifOccurrenceId     = gbifId,
                MediaIdentifierMd5   = md5,
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
        var identifier = m.TryGetProperty("identifier",   out var id)  ? id.GetString()  : null;
        var license    = m.TryGetProperty("license",      out var lic) ? lic.GetString()  : null;
        var rights     = m.TryGetProperty("rightsHolder", out var rh)  ? rh.GetString()  : null;

        if (identifier == null) continue;

        return new GbifImageResult
        {
            Url          = identifier,
            License      = license ?? "Unknown",
            LicenseUrl   = license ?? string.Empty,
            RightsHolder = rights,
            Publisher    = "Zenodo / GBIF",
        };
    }

    return null;
}
```

### 3.10 Helper: GetJsonAsync

```csharp
private async Task<JsonElement> GetJsonAsync(string url, CancellationToken ct)
{
    var response = await httpClient.GetAsync(url, ct);
    response.EnsureSuccessStatusCode();
    var stream = await response.Content.ReadAsStreamAsync(ct);
    var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    return doc.RootElement.Clone(); // Clone to allow disposal of doc
}
```

---

## Part 4: GbifMapService

Create `Creaturedex.AI/Services/GbifMapService.cs`:

```csharp
namespace Creaturedex.AI.Services;

public class GbifMapService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<GbifMapService> logger)
{
    private const string MapsBase = "https://api.gbif.org/v2";
    private const string BasemapUrl = "https://tile.gbif.org/3857/omt/{z}/{x}/{y}@1x.png?style=gbif-light";

    /// <summary>
    /// Fetches map capabilities and builds the tile URL template for a species.
    /// Wild sightings only — excludes fossils, zoo specimens, and captive animals.
    /// </summary>
    public async Task<GbifMapMetadata?> BuildMapMetadataAsync(int taxonKey, CancellationToken ct = default)
    {
        var cacheKey = $"gbif-map:{taxonKey}";
        if (cache.TryGetValue(cacheKey, out GbifMapMetadata? cached)) return cached;

        try
        {
            // Wild observations only filter
            var capUrl = $"{MapsBase}/map/occurrence/density/capabilities.json" +
                         $"?taxonKey={taxonKey}" +
                         $"&basisOfRecord=HUMAN_OBSERVATION" +
                         $"&basisOfRecord=MACHINE_OBSERVATION";

            var response = await httpClient.GetAsync(capUrl, ct);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(ct);
            var json = (await JsonDocument.ParseAsync(stream, cancellationToken: ct)).RootElement;

            var total = json.TryGetProperty("total",   out var tot) ? tot.GetInt32()    : 0;
            var minLat = json.TryGetProperty("minLat", out var mn1) ? (double?)mn1.GetDouble() : null;
            var maxLat = json.TryGetProperty("maxLat", out var mx1) ? (double?)mx1.GetDouble() : null;
            var minLng = json.TryGetProperty("minLng", out var mn2) ? (double?)mn2.GetDouble() : null;
            var maxLng = json.TryGetProperty("maxLng", out var mx2) ? (double?)mx2.GetDouble() : null;
            var minYear = json.TryGetProperty("minYear", out var my1) ? (int?)my1.GetInt32() : null;
            var maxYear = json.TryGetProperty("maxYear", out var my2) ? (int?)my2.GetInt32() : null;

            if (total == 0)
            {
                logger.LogWarning("GBIF maps: no wild observations for taxonKey={TaxonKey}", taxonKey);
                return null;
            }

            // Build parameterised tile URL template (stored in Animal.MapTileUrlTemplate)
            // The {z}/{x}/{y} placeholders are intentionally left for Leaflet/OpenLayers
            var tileTemplate =
                $"{MapsBase}/map/occurrence/density/{{z}}/{{x}}/{{y}}@2x.png" +
                $"?taxonKey={taxonKey}" +
                $"&basisOfRecord=HUMAN_OBSERVATION" +
                $"&basisOfRecord=MACHINE_OBSERVATION" +
                $"&style=fire.point";

            var result = new GbifMapMetadata
            {
                TaxonKey         = taxonKey,
                TileUrlTemplate  = tileTemplate,
                ObservationCount = total,
                MinLat           = minLat,
                MaxLat           = maxLat,
                MinLng           = minLng,
                MaxLng           = maxLng,
                MinYear          = minYear,
                MaxYear          = maxYear,
            };

            cache.Set(cacheKey, result, TimeSpan.FromHours(24));
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "GBIF map metadata fetch failed for taxonKey={TaxonKey}", taxonKey);
            return null;
        }
    }

    /// <summary>
    /// Returns the GBIF basemap tile URL for use alongside occurrence tiles.
    /// </summary>
    public static string GetBasemapTileUrl() => BasemapUrl;

    /// <summary>
    /// Computes a sensible initial map center and zoom level from bbox metadata.
    /// Returns (latitude, longitude, zoom).
    /// </summary>
    public static (double Lat, double Lng, int Zoom) ComputeInitialView(GbifMapMetadata map)
    {
        if (map.MinLat == null || map.MaxLat == null || map.MinLng == null || map.MaxLng == null)
            return (20, 0, 2); // World view fallback

        var centerLat = (map.MinLat.Value + map.MaxLat.Value) / 2;
        var centerLng = (map.MinLng.Value + map.MaxLng.Value) / 2;

        // Rough zoom estimation from lat/lng span
        var latSpan = map.MaxLat.Value - map.MinLat.Value;
        var lngSpan = map.MaxLng.Value - map.MinLng.Value;
        var maxSpan = Math.Max(latSpan, lngSpan);

        var zoom = maxSpan switch
        {
            > 150 => 2,
            > 80  => 3,
            > 40  => 4,
            > 20  => 5,
            > 10  => 6,
            _     => 7,
        };

        return (centerLat, centerLng, zoom);
    }
}
```

---

## Part 5: ContentGeneratorService Refactor

### 5.1 Constructor Changes

Add `GbifService` (remove or keep `WikipediaService` as fallback):

```csharp
public class ContentGeneratorService(
    AIService aiService,
    EmbeddingService embeddingService,
    ImageGenerationService imageService,
    GbifService gbifService,           // NEW — primary data source
    WikipediaService wikipediaService, // KEPT — fallback only
    AnimalRepository animalRepo,
    CategoryRepository categoryRepo,
    TaxonomyRepository taxonomyRepo,
    PetCareGuideRepository careRepo,
    CharacteristicRepository charRepo,
    TagRepository tagRepo,
    AIConfig aiConfig,
    ILogger<ContentGeneratorService> logger)
```

### 5.2 GenerateAnimalAsync — New Flow

```csharp
public async Task<Guid?> GenerateAnimalAsync(string animalName, bool skipImage = false, CancellationToken ct = default)
{
    // 1. Duplicate check (unchanged)
    // ...

    // 2. Fetch GBIF data (primary)
    var gbifData = await gbifService.FetchAnimalDataAsync(animalName, ct);

    // 3. Fetch Wikipedia (fallback — only used if GBIF has missing ecology fields)
    WikipediaArticle? wikiArticle = null;
    var gbifHasSufficientData = gbifData != null && (
        gbifData.HabitatProse != null ||
        gbifData.DietProse != null ||
        gbifData.Taxonomy != null);

    if (!gbifHasSufficientData)
    {
        logger.LogInformation("GBIF data insufficient for {AnimalName}, fetching Wikipedia fallback", animalName);
        wikiArticle = await wikipediaService.GetAnimalArticleAsync(animalName, ct);
    }

    // 4. Build prompt with pre-filled structured data
    var userPrompt = BuildPrompt(animalName, gbifData, wikiArticle);

    // 5. Call Ollama — now only writes prose, not researcher
    var response = await aiService.CompleteAsync(SystemPrompt, userPrompt, ct);

    // 6. Parse response and persist
    // ... (taxonomy, animal, characteristics, tags, petCareGuide — unchanged)

    // 7. Override taxonomy with GBIF data if available (don't let AI invent taxonomy)
    if (gbifData?.Taxonomy != null)
    {
        taxonomy.Kingdom    = gbifData.Taxonomy.Kingdom;
        taxonomy.Phylum     = gbifData.Taxonomy.Phylum;
        taxonomy.Class      = gbifData.Taxonomy.Class;
        taxonomy.TaxOrder   = gbifData.Taxonomy.Order;
        taxonomy.Family     = gbifData.Taxonomy.Family;
        taxonomy.Genus      = gbifData.Taxonomy.Genus;
        taxonomy.Species    = gbifData.Taxonomy.Species;
        taxonomy.ColTaxonId = gbifData.Taxonomy.ColTaxonId;
        taxonomy.Authorship = gbifData.Taxonomy.Authorship;
        taxonomy.Synonyms   = gbifData.Taxonomy.Synonyms.Count > 0
            ? JsonSerializer.Serialize(gbifData.Taxonomy.Synonyms)
            : null;
    }

    // 8. Override conservation status with GBIF IUCN data (authoritative)
    if (gbifData?.IucnCategory != null)
    {
        // Map GBIF category string to our ConservationStatus enum string
        animal.ConservationStatus = gbifData.IucnCategory switch
        {
            "LEAST_CONCERN"         => "Least Concern",
            "NEAR_THREATENED"       => "Near Threatened",
            "VULNERABLE"            => "Vulnerable",
            "ENDANGERED"            => "Endangered",
            "CRITICALLY_ENDANGERED" => "Critically Endangered",
            "EXTINCT_IN_THE_WILD"   => "Extinct in the Wild",
            "EXTINCT"               => "Extinct",
            "DATA_DEFICIENT"        => "Data Deficient",
            _                       => animal.ConservationStatus // Keep Ollama's value if unrecognised
        };
    }

    // 9. Store GBIF identifiers and map metadata
    if (gbifData != null)
    {
        animal.GbifTaxonKey     = gbifData.TaxonKey;
        animal.GbifCanonicalName = gbifData.CanonicalName;

        if (gbifData.MapMetadata != null)
        {
            animal.MapTileUrlTemplate  = gbifData.MapMetadata.TileUrlTemplate;
            animal.MapObservationCount = gbifData.MapMetadata.ObservationCount;
            animal.MapMinLat = gbifData.MapMetadata.MinLat;
            animal.MapMaxLat = gbifData.MapMetadata.MaxLat;
            animal.MapMinLng = gbifData.MapMetadata.MinLng;
            animal.MapMaxLng = gbifData.MapMetadata.MaxLng;
        }
    }

    // 10. Image selection (priority order):
    //     a) GBIF CC BY 4.0 occurrence photo (commercially free + AI-screened for child safety)
    //     b) Wikipedia thumbnail (existing fallback)
    //     c) AI-generated (Stable Diffusion) as backup/override if neither source is suitable
    //
    // GBIF images must pass two checks before use:
    //   1. License must be CC BY 4.0 (commercially free) — reject CC BY-NC
    //   2. AI screening for child-appropriateness (via Ollama vision model)
    // If GBIF image fails either check, fall through to Wikipedia.
    // AI-generated images are always available as a manual override from admin review.

    var currentAnimal = await animalRepo.GetByIdAsync(animalId);

    bool imageSet = false;

    // Try GBIF first — must be CC BY 4.0 AND pass child-safety AI screen
    if (currentAnimal?.ImageUrl == null && gbifData?.BestImage != null
        && gbifData.BestImage.License == "CC BY 4.0")
    {
        var isSafe = await imageScreeningService.IsChildSafeAsync(gbifData.BestImage.CachedUrl, ct);
        if (isSafe)
        {
            await animalRepo.UpdateImageUrlAsync(animalId, gbifData.BestImage.CachedUrl);
            await animalRepo.UpdateImageAttributionAsync(animalId,
                gbifData.BestImage.License,
                gbifData.BestImage.RightsHolder,
                "iNaturalist via GBIF");
            imageSet = true;
        }
        else
        {
            logger.LogWarning("GBIF image for {AnimalName} failed child-safety screen, skipping",
                animalName);
        }
    }

    // Fallback to Wikipedia thumbnail
    if (!imageSet && currentAnimal?.ImageUrl == null && wikiArticle?.ImageUrl != null)
    {
        await animalRepo.UpdateImageUrlAsync(animalId, wikiArticle.ImageUrl);
        imageSet = true;
    }

    // AI-generated (Stable Diffusion) available as backup/override from admin review
    // Not auto-applied here — admin can trigger from the review panel if neither source is suitable
}
```

### 5.3 Updated Ollama System Prompt

The AI's role changes from **researcher** to **prose writer**. Remove all instructions about deriving taxonomy or facts from the reference material and replace with:

```csharp
private const string SystemPrompt = """
    You are a skilled science writer for Creaturedex, a children's animal encyclopedia.
    Your audience is 8–16 year olds. Write with warmth, enthusiasm, and clear language.
    Spell out technical terms on first use. Use metric measurements with imperial in parentheses.

    IMPORTANT: All factual data (taxonomy, conservation status, habitat, diet, size, range) has been
    pre-filled from authoritative scientific sources. Your job is ONLY to:
    1. Write an engaging `summary` (1–2 sentences, under 200 characters, hooks the reader)
    2. Write an accessible `description` (3–4 paragraphs) using the provided scientific data as your source
    3. Write child-friendly `behaviour` prose if not provided in the reference data
    4. Select the most interesting `funFacts` (3–5 items) from the provided facts
    5. Suggest `tags` based on the data provided
    6. Write `petCareGuide` content if isPet is true
    7. If specific data is missing from the reference (e.g. lifespan not found), provide your best
       knowledge but flag uncertainty with "approximately" or "typically"

    DO NOT invent taxonomy — use exactly what is provided in the reference data.
    DO NOT change conservation status — use exactly what is provided.
    Respond with ONLY the JSON schema as before, no markdown fences.
    """;
```

### 5.4 BuildPrompt Method

```csharp
private static string BuildPrompt(
    string animalName,
    GbifAnimalData? gbif,
    WikipediaArticle? wiki)
{
    var sb = new StringBuilder();
    sb.AppendLine($"Generate a complete Creaturedex profile for: {animalName}");
    sb.AppendLine();

    if (gbif != null)
    {
        sb.AppendLine("=== AUTHORITATIVE SCIENTIFIC DATA (pre-filled — do not alter) ===");
        sb.AppendLine();

        if (gbif.Taxonomy != null)
        {
            sb.AppendLine("TAXONOMY:");
            sb.AppendLine($"  Scientific name: {gbif.CanonicalName}");
            if (gbif.Taxonomy.Authorship != null) sb.AppendLine($"  Authorship: {gbif.Taxonomy.Authorship}");
            sb.AppendLine($"  Kingdom: {gbif.Taxonomy.Kingdom}");
            if (gbif.Taxonomy.Phylum  != null) sb.AppendLine($"  Phylum:  {gbif.Taxonomy.Phylum}");
            if (gbif.Taxonomy.Class   != null) sb.AppendLine($"  Class:   {gbif.Taxonomy.Class}");
            if (gbif.Taxonomy.Order   != null) sb.AppendLine($"  Order:   {gbif.Taxonomy.Order}");
            if (gbif.Taxonomy.Family  != null) sb.AppendLine($"  Family:  {gbif.Taxonomy.Family}");
            if (gbif.Taxonomy.Genus   != null) sb.AppendLine($"  Genus:   {gbif.Taxonomy.Genus}");
            if (gbif.Taxonomy.Species != null) sb.AppendLine($"  Species: {gbif.Taxonomy.Species}");
            sb.AppendLine();
        }

        if (gbif.IucnCategory != null)
            sb.AppendLine($"CONSERVATION STATUS: {gbif.IucnCategory} ({gbif.IucnCode})");

        if (gbif.NativeCountries.Count > 0)
            sb.AppendLine($"NATIVE RANGE: {string.Join(", ", gbif.NativeCountries.Take(20))}");

        if (gbif.HabitatProse != null)         sb.AppendLine($"HABITAT: {gbif.HabitatProse}");
        if (gbif.DietProse != null)             sb.AppendLine($"DIET: {gbif.DietProse}");
        if (gbif.BehaviourProse != null)        sb.AppendLine($"BEHAVIOUR: {gbif.BehaviourProse}");
        if (gbif.PhysicalDescriptionProse != null) sb.AppendLine($"PHYSICAL DESCRIPTION: {gbif.PhysicalDescriptionProse}");
        if (gbif.BreedingProse != null)         sb.AppendLine($"BREEDING / LIFESPAN: {gbif.BreedingProse}");
        if (gbif.ConservationProse != null)     sb.AppendLine($"CONSERVATION DETAIL: {gbif.ConservationProse}");

        if (gbif.VernacularNames.Count > 0)
        {
            var engNames = gbif.VernacularNames.Where(v => v.Language is "eng" or "en").Select(v => v.Name);
            sb.AppendLine($"COMMON NAMES (English): {string.Join(", ", engNames)}");
        }

        sb.AppendLine("=== END AUTHORITATIVE DATA ===");
        sb.AppendLine();
    }

    if (wiki != null)
    {
        sb.AppendLine("=== SUPPLEMENTARY WIKIPEDIA DATA (use only where GBIF data is missing) ===");
        sb.Append(WikipediaService.FormatAsReference(wiki));  // existing method, make static
        sb.AppendLine("=== END SUPPLEMENTARY DATA ===");
        sb.AppendLine();
    }

    sb.AppendLine("Using the data above, write the Summary, Description, FunFacts, Tags, and PetCareGuide (if applicable).");
    sb.AppendLine("Copy taxonomy and conservation status exactly from the authoritative data above.");

    return sb.ToString();
}
```

---

## Part 6: Dependency Injection Registration

In `Creaturedex.AI` service registration (e.g. `ServiceCollectionExtensions.cs`):

```csharp
// Register named HttpClients with base addresses and sensible timeouts
services.AddHttpClient<GbifService>(client =>
{
    client.BaseAddress = new Uri("https://api.gbif.org/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Creaturedex/1.0 (contact@creaturedex.com)");
});

services.AddHttpClient<GbifMapService>(client =>
{
    client.BaseAddress = new Uri("https://api.gbif.org/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent", "Creaturedex/1.0 (contact@creaturedex.com)");
});

services.AddScoped<GbifService>();
services.AddScoped<GbifMapService>();

// IMemoryCache is already registered in most hosts; ensure it is:
services.AddMemoryCache();
```

> **Note on User-Agent:** GBIF's terms of service request that API consumers identify themselves.
> Using a real contact email reduces the risk of being rate-limited.

---

## Part 7: Error Handling Strategy

| Scenario | Behaviour |
|---|---|
| GBIF name resolution returns NONE | Return null; ContentGeneratorService falls back to Wikipedia |
| GBIF fuzzy match < 80 confidence | Return null; fall back to Wikipedia |
| COL lookup fails | Log warning; return GBIF-only taxonomy (still correct, just without ColTaxonId/Authorship) |
| Individual description type missing | Leave field null; Ollama fills from its knowledge |
| IUCN status endpoint 404 | Log debug; ConservationStatus remains from Ollama |
| No CC BY images found or image fails child-safety screen | Return null; falls back to Wikipedia image, then AI-generated as backup |
| Map capabilities returns total=0 | Return null; no map stored on Animal record; frontend hides map tab |
| Any HTTP 429 (rate limit) | Retry once after 2s; log warning; fall back gracefully |
| Entire GBIF service unavailable | Log error; ContentGeneratorService continues with Wikipedia only |

All failure modes are **non-fatal** — a failed GBIF fetch degrades gracefully to the existing Wikipedia/Ollama pipeline.

---

## Part 8: Caching Strategy

| Cache Key | TTL | Notes |
|---|---|---|
| `gbif:{animalName.lower}` | 24 hours | Full `GbifAnimalData` record |
| `gbif-map:{taxonKey}` | 24 hours | `GbifMapMetadata` |
| `wiki:{animalName.lower}` | 1 hour | Existing Wikipedia cache (unchanged) |

The 24-hour TTL on GBIF data is appropriate because:
- GBIF taxonomy and ecological data is updated infrequently (monthly releases)
- Occurrence counts and map tiles are pre-computed by GBIF and cached on their CDN
- The generation pipeline is a one-time operation per animal; caching is mainly for regeneration/testing

For production, consider migrating from `IMemoryCache` to `IDistributedCache` (Redis) so the cache
survives app restarts and is shared across multiple instances.

---

## Part 9: Licensing Considerations

### CC BY 4.0 (preferred — ~8% of iNaturalist images via GBIF)
- Fully free including commercial use. **Only CC BY 4.0 images are used automatically.**
- Attribution still required — display `ImageRightsHolder` and `ImageSource` on the animal page.
- All GBIF images must also pass an AI child-safety screen (Ollama vision model) before use.

### CC BY-NC 4.0 (92% of iNaturalist images via GBIF)
- **Excluded from automatic selection** due to non-commercial restriction.
- If Creaturedex ever needs more image coverage, these could be reconsidered with legal sign-off.
- The `ImageLicense` field is stored on every record for audit purposes regardless.

### GBIF Data (taxonomy, ecology text)
- GBIF data itself is CC BY 4.0 when attributed to GBIF.
- Attribution text: "Occurrence data from GBIF.org" in site footer or About page is sufficient.

### COL Data
- Catalogue of Life is CC BY 4.0. Same attribution requirement.

---

## Part 10: Frontend Map Integration (Next.js)

The stored `MapTileUrlTemplate` and bbox fields enable a simple Leaflet embed on the animal page:

```typescript
// components/AnimalHabitatMap.tsx
import { MapContainer, TileLayer } from 'react-leaflet';

interface Props {
  tileUrlTemplate: string;   // from Animal.MapTileUrlTemplate
  centerLat: number;
  centerLng: number;
  zoom: number;
}

export function AnimalHabitatMap({ tileUrlTemplate, centerLat, centerLng, zoom }: Props) {
  return (
    <MapContainer center={[centerLat, centerLng]} zoom={zoom} style={{ height: 300 }}>
      {/* GBIF light basemap */}
      <TileLayer
        url="https://tile.gbif.org/3857/omt/{z}/{x}/{y}@1x.png?style=gbif-light"
        tileSize={512}
        zoomOffset={-1}
        attribution="© GBIF · © OpenMapTiles · © OpenStreetMap"
      />
      {/* Species occurrence density overlay — wild sightings only */}
      <TileLayer
        url={tileUrlTemplate}
        tileSize={512}
        zoomOffset={-1}
        opacity={0.85}
      />
    </MapContainer>
  );
}
```

The `centerLat`, `centerLng`, and `zoom` values can be computed from the stored bbox fields using
`GbifMapService.ComputeInitialView()` at generation time and stored on the Animal record, or
computed at runtime in the frontend from the bbox values.

---

## Implementation Order

1. **Migrations** — add new Animal and Taxonomy fields, generate EF Core migration
2. **`GbifAnimalData` record** — create the Models file
3. **`GbifService`** — implement and unit test each method independently using recorded API fixtures
4. **`GbifMapService`** — implement and test
5. **`ContentGeneratorService` refactor** — add `GbifService`, update prompt, update field mapping
6. **DI registration** — wire up HttpClient and services
7. **`AnimalRepository`** — add `UpdateImageAttributionAsync` method
8. **Frontend map component** — Leaflet integration using stored tile URL
9. **Admin review tooling** — the stored `ImageLicense` field enables building a
   "flag images for review" queue before publishing

