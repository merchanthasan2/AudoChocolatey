using System;
using QuantumUpdater.Views.Controls;
using Xunit;

namespace QuantumUpdater.Tests;

public class AppRowModelTests
{
    private static AppRowModel Row(AppRowState state = AppRowState.Ready) =>
        new() { Key = "firefox", Name = "Mozilla Firefox", State = state };

    [Fact]
    public void Ready_has_empty_status_text()
    {
        Assert.Equal("", Row(AppRowState.Ready).StatusText());
    }

    [Fact]
    public void Waiting_held_shows_on_hold_even_with_a_place()
    {
        var row = Row(AppRowState.Waiting) with { IsHeld = true, PlaceInLine = 3 };

        Assert.Equal("Waiting · on hold", row.StatusText());
    }

    [Fact]
    public void Waiting_place_1_shows_next()
    {
        var row = Row(AppRowState.Waiting) with { PlaceInLine = 1 };

        Assert.Equal("Waiting · next", row.StatusText());
    }

    [Fact]
    public void Waiting_place_3_shows_in_line()
    {
        var row = Row(AppRowState.Waiting) with { PlaceInLine = 3 };

        Assert.Equal("Waiting · 3 in line", row.StatusText());
    }

    [Fact]
    public void Working_with_percent_shows_phase_and_percent()
    {
        var row = Row(AppRowState.Working) with { Phase = "Downloading", Percent = 45 };

        Assert.Equal("Downloading · 45%", row.StatusText());
    }

    [Fact]
    public void Working_with_percent_zero_shows_zero_percent()
    {
        var row = Row(AppRowState.Working) with { Phase = "Downloading", Percent = 0 };

        Assert.Equal("Downloading · 0%", row.StatusText());
    }

    [Fact]
    public void Working_with_percent_and_no_phase_shows_working_and_percent()
    {
        var row = Row(AppRowState.Working) with { Percent = 45 };

        Assert.Equal("Working · 45%", row.StatusText());
    }

    [Fact]
    public void Working_without_percent_shows_phase()
    {
        var row = Row(AppRowState.Working) with { Phase = "Installing" };

        Assert.Equal("Installing", row.StatusText());
    }

    [Fact]
    public void Working_without_percent_or_phase_shows_working()
    {
        var row = Row(AppRowState.Working) with { Phase = "" };

        Assert.Equal("Working", row.StatusText());
    }

    [Fact]
    public void Stopping_shows_stopping()
    {
        Assert.Equal("Stopping", Row(AppRowState.Stopping).StatusText());
    }

    [Fact]
    public void Updated_shows_version_and_time()
    {
        var doneAt = new DateTimeOffset(2026, 11, 16, 10, 31, 0, TimeSpan.Zero);
        var row = Row(AppRowState.Updated) with { DoneVersion = "3.12.8", DoneAt = doneAt };

        Assert.Equal("Updated to 3.12.8 · 10:31", row.StatusText());
    }

    [Fact]
    public void Updated_without_time_shows_version_only()
    {
        var row = Row(AppRowState.Updated) with { DoneVersion = "3.12.8" };

        Assert.Equal("Updated to 3.12.8", row.StatusText());
    }

    [Fact]
    public void Updated_without_version_shows_updated()
    {
        var doneAt = new DateTimeOffset(2026, 11, 16, 10, 31, 0, TimeSpan.Zero);
        var row = Row(AppRowState.Updated) with { DoneAt = doneAt };

        Assert.Equal("Updated", row.StatusText());
    }

    [Fact]
    public void Failed_with_error_text_shows_error_text()
    {
        var row = Row(AppRowState.Failed) with { ErrorText = "Update failed. The installer stopped (code 1603)." };

        Assert.Equal("Update failed. The installer stopped (code 1603).", row.StatusText());
    }

    [Fact]
    public void Failed_without_error_text_shows_update_failed()
    {
        Assert.Equal("Update failed", Row(AppRowState.Failed).StatusText());
    }

    [Fact]
    public void Version_text_shows_from_and_to()
    {
        var row = Row() with { FromVersion = "1.2", ToVersion = "1.3" };

        Assert.Equal("1.2 → 1.3", row.VersionText());
    }

    [Fact]
    public void Version_text_without_from_shows_not_known_and_to()
    {
        var row = Row() with { ToVersion = "1.3" };

        Assert.Equal("Not known → 1.3", row.VersionText());
    }

    [Fact]
    public void Version_text_without_to_shows_from()
    {
        var row = Row() with { FromVersion = "1.2" };

        Assert.Equal("1.2", row.VersionText());
    }

    [Fact]
    public void Version_text_without_both_shows_not_known()
    {
        Assert.Equal("Not known", Row().VersionText());
    }
}
