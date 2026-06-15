using MeetCap.Models;

namespace MeetCap.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }

    /// <summary>Raised after settings are persisted.</summary>
    event EventHandler? Changed;

    /// <summary>Persist the current settings to disk.</summary>
    void Save();
}
