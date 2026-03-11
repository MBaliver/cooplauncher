using System.IO;
using System.Text.Json;

namespace RemotePlayLauncher;

public class LauncherConfig
{
    public string? DonorExePath { get; set; }
    public Dictionary<string, string> ExecutableOverrides { get; set; } = new();

    private static string ConfigPath
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoopLauncher");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "launcher_config.json");
        }
    }

    public static LauncherConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(ConfigPath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try 
        { 
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); 
        }
        catch { }
    }
}
