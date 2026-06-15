using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MeetCap.Models;

namespace MeetCap.Services;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON in %APPDATA%\MeetCap.</summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public AppSettings Settings { get; private set; }

    public event EventHandler? Changed;

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetCap");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Settings = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                    return loaded;
            }
        }
        catch
        {
            // Corrupt or unreadable settings: fall back to defaults rather than crash.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Best-effort persistence; a transient IO error shouldn't take down the app.
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
