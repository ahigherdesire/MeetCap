namespace MeetCap.Views;

/// <summary>Describes one button in a <see cref="MeetingPromptWindow"/>.</summary>
public sealed record PromptButton(string Text, string Result, bool IsPrimary = false);
