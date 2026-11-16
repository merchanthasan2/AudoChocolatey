using QuantumUpdater.Views.Controls;
using Xunit;

namespace QuantumUpdater.Tests;

public class IconSlotTests
{
    [Fact]
    public void After_a_second_Begin_the_first_ticket_is_not_current()
    {
        var slot = new IconSlot();
        var first = slot.Begin("firefox");
        var second = slot.Begin("7zip.7zip");

        Assert.False(slot.IsCurrent(first));
        Assert.True(slot.IsCurrent(second));
    }

    [Fact]
    public void Fresh_slot_has_no_current_ticket()
    {
        var slot = new IconSlot();

        Assert.False(slot.IsCurrent(0));
    }

    [Fact]
    public void Key_follows_the_last_Begin()
    {
        var slot = new IconSlot();
        Assert.Null(slot.Key);

        slot.Begin("firefox");
        Assert.Equal("firefox", slot.Key);

        slot.Begin("7zip.7zip");
        Assert.Equal("7zip.7zip", slot.Key);
    }
}
