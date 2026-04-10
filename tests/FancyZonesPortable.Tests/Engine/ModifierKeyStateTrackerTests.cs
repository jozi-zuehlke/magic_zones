using FancyZonesPortable.Core.Engine;
using Xunit;

namespace FancyZonesPortable.Tests.Engine;

/// <summary>
/// Tests for <see cref="ModifierKeyStateTracker"/> to ensure repeated key-down
/// notifications while a modifier remains physically held do not trigger
/// duplicate "pressed" transitions.
/// </summary>
public class ModifierKeyStateTrackerTests
{
    [Fact]
    public void TryPress_FirstPress_ReturnsTrue()
    {
        var tracker = new ModifierKeyStateTracker();
        Assert.True(tracker.TryPress());
    }

    [Fact]
    public void TryPress_RepeatedKeyDownWhileHeld_ReturnsFalse()
    {
        var tracker = new ModifierKeyStateTracker();
        Assert.True(tracker.TryPress());

        // Simulates key auto-repeat while key is still physically held.
        Assert.False(tracker.TryPress());
        Assert.False(tracker.TryPress());
    }

    [Fact]
    public void TryRelease_AfterPress_ReturnsTrue_AndAllowsNextPress()
    {
        var tracker = new ModifierKeyStateTracker();
        Assert.True(tracker.TryPress());
        Assert.True(tracker.TryRelease());

        Assert.True(tracker.TryPress());
    }

    [Fact]
    public void Sync_WithHeldState_BlocksDuplicatePressUntilReleased()
    {
        var tracker = new ModifierKeyStateTracker();

        // Simulates drag-start path where polling observes key already held.
        tracker.Sync(isHeld: true);
        Assert.False(tracker.TryPress());

        tracker.Sync(isHeld: false);
        Assert.True(tracker.TryPress());
    }
}
