using System.Text.Json;

namespace OpenCmd.Services;

sealed class AppSettings
{
    public string Shell { get; set; } = "powershell.exe";
    public List<string> Recent { get; set; } = [];
    public Dictionary<string, string> Commands { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Unchecked { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Order { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        Commands = CopyMap(Commands);
        Unchecked = CopyLists(Unchecked);
        Order = CopyLists(Order);
        Recent = Recent
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(PathUtil.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static Dictionary<string, string> CopyMap(Dictionary<string, string>? source)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (source == null)
            return map;

        foreach (var (key, value) in source)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                continue;
            try
            {
                map[PathUtil.Normalize(key)] = value;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
            {
            }
        }

        return map;
    }

    static Dictionary<string, List<string>> CopyLists(Dictionary<string, List<string>>? source)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (source == null)
            return map;

        foreach (var (key, value) in source)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            try
            {
                map[PathUtil.Normalize(key)] = value
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path =>
                    {
                        try { return PathUtil.Normalize(path); }
                        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException) { return ""; }
                    })
                    .Where(path => path.Length > 0)
                    .ToList();
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
            {
            }
        }

        return map;
    }
}

static class SettingsStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    static string SettingsFile
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OpenCmd");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new AppSettings();

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile), Options) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
