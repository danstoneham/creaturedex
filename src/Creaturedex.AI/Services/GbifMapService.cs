using System.Text.Json;
using Creaturedex.AI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Creaturedex.AI.Services;

public class GbifMapService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<GbifMapService> logger)
{
    private const string MapsBase = "https://api.gbif.org/v2";
    private const string BasemapUrl = "https://tile.gbif.org/3857/omt/{z}/{x}/{y}@1x.png?style=gbif-light";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

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

            var total = json.TryGetProperty("total", out var tot) ? (int)tot.GetInt64() : 0;
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
                TaxonKey = taxonKey,
                TileUrlTemplate = tileTemplate,
                ObservationCount = total,
                MinLat = minLat,
                MaxLat = maxLat,
                MinLng = minLng,
                MaxLng = maxLng,
                MinYear = minYear,
                MaxYear = maxYear,
            };

            cache.Set(cacheKey, result, CacheDuration);
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
            > 80 => 3,
            > 40 => 4,
            > 20 => 5,
            > 10 => 6,
            _ => 7,
        };

        return (centerLat, centerLng, zoom);
    }
}
