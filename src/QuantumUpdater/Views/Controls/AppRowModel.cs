using System;
using System.Globalization;

namespace QuantumUpdater.Views.Controls;

/// <summary>The state of one app row in the Updates list.</summary>
public enum AppRowState
{
    Ready,
    Waiting,
    Working,
    Stopping,
    Updated,
    Failed,
}

/// <summary>
/// The data of one app row. Pure C#, no WinUI types, so the test project can link this file.
/// </summary>
public sealed record AppRowModel
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Publisher { get; init; }

    /// <summary>The installed version. Null means not known.</summary>
    public string? FromVersion { get; init; }

    public string? ToVersion { get; init; }

    public string? SourceLabel { get; init; }

    public AppRowState State { get; init; } = AppRowState.Ready;

    /// <summary>The place in the queue. 1 means next.</summary>
    public int PlaceInLine { get; init; }

    public bool IsHeld { get; init; }

    /// <summary>The current step word, for example "Downloading".</summary>
    public string? Phase { get; init; }

    /// <summary>The progress in percent. Null means no progress is known.</summary>
    public int? Percent { get; init; }

    public string? ErrorText { get; init; }

    public string? DoneVersion { get; init; }

    public DateTimeOffset? DoneAt { get; init; }

    public bool IsSelected { get; init; }

    public bool CanSelect { get; init; }

    /// <summary>The status line text for the current state.</summary>
    public string StatusText()
    {
        switch (State)
        {
            case AppRowState.Ready:
                return "";

            case AppRowState.Waiting:
                if (IsHeld)
                {
                    return "Waiting · on hold";
                }

                if (PlaceInLine <= 1)
                {
                    return "Waiting · next";
                }

                return "Waiting · " + PlaceInLine.ToString(CultureInfo.InvariantCulture) + " in line";

            case AppRowState.Working:
                var phase = string.IsNullOrEmpty(Phase) ? "Working" : Phase;
                if (Percent.HasValue)
                {
                    return phase + " · " + Percent.Value.ToString(CultureInfo.InvariantCulture) + "%";
                }

                return phase;

            case AppRowState.Stopping:
                return "Stopping";

            case AppRowState.Updated:
                if (string.IsNullOrEmpty(DoneVersion))
                {
                    return "Updated";
                }

                if (DoneAt is null)
                {
                    return "Updated to " + DoneVersion;
                }

                return "Updated to " + DoneVersion + " · " + DoneAt.Value.ToString("HH:mm", CultureInfo.InvariantCulture);

            case AppRowState.Failed:
                return string.IsNullOrEmpty(ErrorText) ? "Update failed" : ErrorText;

            default:
                return "";
        }
    }

    /// <summary>The version text, for example "1.2 → 1.3" or "Not known → 1.3".</summary>
    public string VersionText()
    {
        if (ToVersion is null)
        {
            return string.IsNullOrEmpty(FromVersion) ? "Not known" : FromVersion;
        }

        if (string.IsNullOrEmpty(FromVersion))
        {
            return "Not known → " + ToVersion;
        }

        return FromVersion + " → " + ToVersion;
    }
}
