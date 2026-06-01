using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileFinder.Core;

/// <summary>
/// User preferences persisted to <c>%LocalAppData%\FileFinder\settings.json</c>.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Engine used for searches at startup. Defaults to the fastest (MASM).</summary>
    public SearchEngine DefaultEngine { get; set; } = SearchEngine.Masm;

    /// <summary>UI language code ("en", "es").</summary>
    public string Language { get; set; } = "en";

    /// <summary>Drive roots the user selected to index (e.g. "C:\\"). Empty = default to system drive.</summary>
    public List<string> IndexedDrives { get; set; } = new();

    [JsonIgnore]
    public static string Path => System.IO.Path.Combine(IndexCache.Directory, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), JsonOpts) ?? new AppSettings();
        }
        catch { /* fall back to defaults on any corruption */ }
        return new AppSettings();
    }

    public void Save()
    {
        try { File.WriteAllText(Path, JsonSerializer.Serialize(this, JsonOpts)); }
        catch { /* non-fatal */ }
    }
}
