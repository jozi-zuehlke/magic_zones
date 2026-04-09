using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Config;
using FancyZonesPortable.Core.Engine;
using FancyZonesPortable.Core.Logging;
using FancyZonesPortable.Tests.Helpers;
using NSubstitute;
using Xunit;

namespace FancyZonesPortable.Tests.Integration;

/// <summary>
/// Integration tests exercising the full pipeline: config → coordinate conversion → hit testing → snap decision.
/// Uses real objects (no mocks except ILogger).
/// </summary>
public class EndToEndTests
{
    private static readonly Rectangle StandardWorkingArea = new(0, 0, 1920, 1080);
    private const nint TestWindowHandle = 0x1234;

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly CoordinateConverter _converter = new();
    private readonly ZoneHitTester _hitTester = new();

    private SnapEngine CreateEngine() => new(_hitTester, _converter, _logger);

    private Rectangle? DragAndSnap(SnapEngine engine, MonitorConfig monitor, Rectangle workingArea, int cursorX, int cursorY)
    {
        engine.BeginDrag(TestWindowHandle, monitor, workingArea);
        engine.UpdateCursorPosition(cursorX, cursorY);
        return engine.CommitSnap();
    }

    [Fact]
    public void PRDExample_CursorInLeftHalf_SnapsToLeftHalf()
    {
        var config = TestHelpers.CreatePRDExampleConfig();
        var monitor = config.Monitors[0];
        var engine = CreateEngine();

        var result = DragAndSnap(engine, monitor, StandardWorkingArea, 400, 540);

        Assert.NotNull(result);
        Assert.Equal(new Rectangle(0, 0, 960, 1080), result.Value);
    }

    [Fact]
    public void PRDExample_CursorInSidebar_SnapsToSidebar()
    {
        var config = TestHelpers.CreatePRDExampleConfig();
        var monitor = config.Monitors[0];
        var engine = CreateEngine();

        var result = DragAndSnap(engine, monitor, StandardWorkingArea, 1800, 500);

        Assert.NotNull(result);
        Assert.Equal(new Rectangle(1440, 0, 480, 1080), result.Value);
    }

    [Fact]
    public void PRDExample_CursorInTopRightQuarter_SnapsToTopRightQuarter()
    {
        var config = TestHelpers.CreatePRDExampleConfig();
        var monitor = config.Monitors[0];
        var engine = CreateEngine();

        var result = DragAndSnap(engine, monitor, StandardWorkingArea, 1200, 200);

        Assert.NotNull(result);
        Assert.Equal(new Rectangle(960, 0, 960, 540), result.Value);
    }

    [Fact]
    public void PRDExample_CursorInRightHalfBottomOnly_SnapsToRightHalf()
    {
        var config = TestHelpers.CreatePRDExampleConfig();
        var monitor = config.Monitors[0];
        var engine = CreateEngine();

        // (1100, 800) is in the right-half zone but not in top-right-quarter (y >= 540) or sidebar (x < 1440)
        var result = DragAndSnap(engine, monitor, StandardWorkingArea, 1100, 800);

        Assert.NotNull(result);
        Assert.Equal(new Rectangle(960, 0, 960, 1080), result.Value);
    }

    [Fact]
    public void PRDExample_CursorOutside_NoSnap()
    {
        var config = TestHelpers.CreatePRDExampleConfig();
        var monitor = config.Monitors[0];
        var engine = CreateEngine();

        var result = DragAndSnap(engine, monitor, StandardWorkingArea, -100, -100);

        Assert.Null(result);
    }

    [Fact]
    public void FullCycle_DragAndCancel_NoSnap()
    {
        var config = TestHelpers.CreatePRDExampleConfig();
        var monitor = config.Monitors[0];
        var engine = CreateEngine();

        engine.BeginDrag(TestWindowHandle, monitor, StandardWorkingArea);
        engine.UpdateCursorPosition(400, 540);
        Assert.NotNull(engine.ActiveZone);

        engine.CancelDrag();
        var result = engine.CommitSnap();

        Assert.Null(result);
    }

    [Fact]
    public void ConfigReload_NewConfigApplied()
    {
        var engine = CreateEngine();

        // Config A: two zones (left/right halves)
        var configA = TestHelpers.CreateValidConfig();
        var monitorA = configA.Monitors[0];

        var resultA = DragAndSnap(engine, monitorA, StandardWorkingArea, 400, 540);
        Assert.NotNull(resultA);
        Assert.Equal(new Rectangle(0, 0, 960, 1080), resultA.Value);

        // Config B: single full-screen zone
        var monitorB = TestHelpers.CreatePercentMonitor("primary",
            TestHelpers.CreateZone("full", "Full Screen", 0, 0, 1.0, 1.0, priority: 1));

        var resultB = DragAndSnap(engine, monitorB, StandardWorkingArea, 400, 540);
        Assert.NotNull(resultB);
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), resultB.Value);
    }

    [Fact]
    public void PixelCoordinates_SnapCorrectly()
    {
        var monitor = TestHelpers.CreatePixelMonitor("pixel-monitor",
            TestHelpers.CreateZone("left", "Left Panel", 0, 0, 800, 1080, priority: 1),
            TestHelpers.CreateZone("right", "Right Panel", 800, 0, 1120, 1080, priority: 1));

        var engine = CreateEngine();

        var result = DragAndSnap(engine, monitor, StandardWorkingArea, 400, 540);

        Assert.NotNull(result);
        Assert.Equal(new Rectangle(0, 0, 800, 1080), result.Value);
    }

    [Fact]
    public void OverlappingZones_HighestPrecedenceWins()
    {
        // Three overlapping zones covering the same area, listed in worst-priority-first order
        var monitor = TestHelpers.CreatePercentMonitor("overlap-test",
            TestHelpers.CreateZone("low", "Low Priority", 0, 0, 1.0, 1.0, priority: 10),
            TestHelpers.CreateZone("mid", "Mid Priority", 0, 0, 1.0, 1.0, priority: 5),
            TestHelpers.CreateZone("high", "High Priority", 0, 0, 1.0, 1.0, priority: 1));

        var engine = CreateEngine();

        engine.BeginDrag(TestWindowHandle, monitor, StandardWorkingArea);
        engine.UpdateCursorPosition(960, 540);

        Assert.NotNull(engine.ActiveZone);
        Assert.Equal("high", engine.ActiveZone.Definition.Id);

        var result = engine.CommitSnap();
        Assert.NotNull(result);
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), result.Value);
    }

    [Fact]
    public void WorkingAreaWithTaskbarOffset_ZonesOffsetCorrectly()
    {
        var workingArea = new Rectangle(0, 40, 1920, 1040);
        var monitor = TestHelpers.CreatePercentMonitor("taskbar-test",
            TestHelpers.CreateZone("left", "Left Half", 0, 0, 0.5, 1.0, priority: 1),
            TestHelpers.CreateZone("right", "Right Half", 0.5, 0, 0.5, 1.0, priority: 1));

        var engine = CreateEngine();

        // Cursor in the left half of the offset working area
        var result = DragAndSnap(engine, monitor, workingArea, 400, 540);

        Assert.NotNull(result);
        Assert.Equal(new Rectangle(0, 40, 960, 1040), result.Value);
    }
}
