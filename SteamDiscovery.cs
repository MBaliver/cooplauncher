using System.IO;
using System.Linq;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;

namespace RemotePlayLauncher;

public record SteamGame(string AppId, string Name, string GameFolder, string ExecutablePath);

public static class SteamDiscovery
{
    public static string? SteamBasePath { get; private set; }

    // ── Exclusion filter ────────────────────────────────────────────────────

    // AppIds of known non-game tools
    private static readonly HashSet<string> ExcludedAppIds = new()
    {
        "228980",  // Steamworks Common Redistributables
        "431960",  // Wallpaper Engine
        "1391110", // Steam Linux Runtime - Soldier
        "1628350", // Steam Linux Runtime - Sniper
        "1070560", // Steam Linux Runtime
        "1245040", // Proton - Experimental
        "961940",  // Proton 3.7
        "2180100", // Proton Hotfix
        "1580130", // Steam Linux Runtime - ..
    };

    private static readonly string[] ExcludeNameFragments =
    {
        "redistributable", "proton", "steam linux runtime",
        "steamworks", "directx", "vcredist", "microsoft visual c++",
        "dotnet runtime", "physx", "openal"
    };

    public static bool ShouldExclude(string appId, string name) =>
        ExcludedAppIds.Contains(appId) ||
        ExcludeNameFragments.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));

    // ── Icon ────────────────────────────────────────────────────────────────

    /// <summary>Returns the first available local Steam icon path for the given AppId, or null.</summary>
    public static string? GetLocalIconPath(string appId)
    {
        if (SteamBasePath == null) return null;
        var cache = Path.Combine(SteamBasePath, "appcache", "librarycache");
        foreach (var suffix in new[] { "_header.jpg", "_icon.jpg", "_library_600x900.jpg" })
        {
            var p = Path.Combine(cache, appId + suffix);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    // ── Discovery ───────────────────────────────────────────────────────────

    public static string? GetSteamPath()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
                    .OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (key?.GetValue("InstallPath") is string p && Directory.Exists(p))
                {
                    SteamBasePath = p;
                    return p;
                }
            }
            catch { }
        }
        foreach (var d in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" })
        {
            if (Directory.Exists(d))
            {
                SteamBasePath = d;
                return d;
            }
        }
        return null;
    }

    public static List<SteamGame> Discover()
    {
        var results = new List<SteamGame>();
        var steam = GetSteamPath();
        if (steam == null) return results;

        foreach (var lib in GetLibraries(steam))
        {
            var steamapps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(steamapps)) continue;

            foreach (var acf in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                try
                {
                    var game = ParseManifest(acf, lib);
                    if (game != null && !ShouldExclude(game.AppId, game.Name))
                        results.Add(game);
                }
                catch { }
            }
        }

        return results.OrderBy(g => g.Name).ToList();
    }

    private static List<string> GetLibraries(string steamPath)
    {
        var libs = new List<string> { steamPath };
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return libs;

        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(vdf));
            if (root.Value is not VObject obj) return libs;

            foreach (var child in obj)
            {
                string? libPath = null;
                if (child.Value is VObject entry && entry.TryGetValue("path", out var tok))
                    libPath = tok.Value<string>();
                else if (child.Value is VValue val && int.TryParse(child.Key, out _))
                    libPath = val.Value<string>();

                libPath = libPath?.Replace("\\\\", "\\");
                if (!string.IsNullOrEmpty(libPath) && Directory.Exists(libPath) && !libs.Contains(libPath))
                    libs.Add(libPath);
            }
        }
        catch { }
        return libs;
    }

    private static SteamGame? ParseManifest(string acfPath, string libPath)
    {
        var root = VdfConvert.Deserialize(File.ReadAllText(acfPath));
        if (root.Value is not VObject state) return null;

        var appId = state.TryGetValue("appid", out var a) ? a.Value<string>() : null;
        var name = state.TryGetValue("name", out var n) ? n.Value<string>() : null;
        var dir = state.TryGetValue("installdir", out var d) ? d.Value<string>() : null;

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(dir))
            return null;

        var folder = Path.Combine(libPath, "steamapps", "common", dir);
        if (!Directory.Exists(folder)) return null;

        return new SteamGame(appId, name, folder, FindExe(folder, name) ?? string.Empty);
    }

    private static string? FindExe(string folder, string gameName)
    {
        var exes = Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly).ToList();
        if (exes.Count == 0) return null;
        if (exes.Count == 1) return exes[0];
        var clean = Strip(gameName);
        return exes.OrderByDescending(e => Score(clean, Strip(Path.GetFileNameWithoutExtension(e)))).First();
    }

    private static string Strip(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static double Score(string a, string b)
    {
        if (a == b) return 1.0;
        if (a.Contains(b) || b.Contains(a)) return 0.8;
        return (double)a.Intersect(b).Count() / Math.Max(a.Length, b.Length);
    }
}
