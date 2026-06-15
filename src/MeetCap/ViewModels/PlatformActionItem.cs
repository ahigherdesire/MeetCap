using CommunityToolkit.Mvvm.ComponentModel;
using MeetCap.Models;

namespace MeetCap.ViewModels;

/// <summary>Row in the per-platform detection settings list.</summary>
public partial class PlatformActionItem : ObservableObject
{
    private readonly Action<MeetingPlatform, PlatformAction> _onChanged;

    public MeetingPlatform Platform { get; }
    public string DisplayName => Platform.DisplayName();

    [ObservableProperty] private PlatformAction _action;

    public PlatformActionItem(MeetingPlatform platform, PlatformAction action,
        Action<MeetingPlatform, PlatformAction> onChanged)
    {
        Platform = platform;
        _action = action;
        _onChanged = onChanged;
    }

    partial void OnActionChanged(PlatformAction value) => _onChanged(Platform, value);

    public IReadOnlyList<PlatformAction> Actions { get; } = new[]
    {
        PlatformAction.Ask, PlatformAction.AutoRecord, PlatformAction.Ignore,
    };
}
