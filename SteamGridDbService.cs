using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

namespace RemotePlayLauncher;

/// <summary>
/// Fetches game art from the SteamGridDB API.
/// All results are cached in-memory (one session = one lookup per game).
/// </summary>
public static class SteamGridDbService
{
    private const string ApiKey  = "3fe8cb1fb0329b929fc9eb4194477038";
    private const string BaseUrl = "https://www.steamgriddb.com/api/v2";

    private static readonly HttpClient Http;
    private static readonly SemaphoreSlim Throttle = new(5, 5);

    private static readonly Dictionary<string, string?> IconCache = new();
    private static readonly Dictionary<string, string?> GridCache = new();
    private static readonly object Lock = new();

    static SteamGridDbService()
    {
        Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        Http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiKey);
    }

    // ── Grids (460×215 landscape banners) ───────────────────────────────────

    /// <summary>
    /// Returns the thumbnail URL for the best matching 460×215 grid banner,
    /// preferring clean styles (no_logo → alternate → any).
    /// </summary>
    public static async Task<string?> GetGridUrlAsync(string steamAppId)
    {
        lock (Lock) { if (GridCache.TryGetValue(steamAppId, out var c)) return c; }

        await Throttle.WaitAsync();
        try
        {
            // Ask for no_logo first (clean art without text overlay), fall back to others
            var url = $"{BaseUrl}/grids/steam/{steamAppId}?dimensions=460x215" +
                      "&styles=no_logo,alternate,blurred,white_logo,material";
            var json = await Http.GetStringAsync(url);

            var thumb = ExtractFirstThumb(json);
            lock (Lock) GridCache[steamAppId] = thumb;
            return thumb;
        }
        catch { }
        finally { Throttle.Release(); }

        lock (Lock) GridCache[steamAppId] = null;
        return null;
    }

    // ── Icons (256×256, kept for potential future use) ───────────────────────

    public static async Task<string?> GetIconUrlAsync(string steamAppId)
    {
        lock (Lock) { if (IconCache.TryGetValue(steamAppId, out var c)) return c; }

        await Throttle.WaitAsync();
        try
        {
            var url = $"{BaseUrl}/icons/steam/{steamAppId}?styles=official,white,black,custom";
            var json = await Http.GetStringAsync(url);

            var thumb = ExtractFirstThumb(json);
            lock (Lock) IconCache[steamAppId] = thumb;
            return thumb;
        }
        catch { }
        finally { Throttle.Release(); }

        lock (Lock) IconCache[steamAppId] = null;
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string? ExtractFirstThumb(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.GetProperty("success").GetBoolean()) return null;

        var data = root.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;

        var first = data[0];

        if (first.TryGetProperty("thumb", out var te) && te.ValueKind == JsonValueKind.String)
        {
            var t = te.GetString();
            if (!string.IsNullOrEmpty(t)) return t;
        }
        if (first.TryGetProperty("url", out var ue) && ue.ValueKind == JsonValueKind.String)
            return ue.GetString();

        return null;
    }
}
