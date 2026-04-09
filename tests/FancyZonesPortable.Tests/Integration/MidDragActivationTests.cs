using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Config;
using FancyZonesPortable.Core.Engine;
using FancyZonesPortable.Core.Logging;
using FancyZonesPortable.Tests.Helpers;
using NSubstitute;
using Xunit;

namespace FancyZonesPortable.Tests.Integration;

/// <summary>
/// Tests for the mid-drag activation scenario: user drags without modifier,
/// then presses modifier to activate snapping. Verifies that the overlay never
/// shows a stale/wrong active zone — not even for a single frame.
/// </summary>
public class MidDragActivationTests
{
    private static readonly Rectangle WorkingArea = new(0, 0, 1920, 1080);
    private const nint TestWindowHandle = 0xE805F8;

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly CoordinateConverter _converter = new();
    private readonly ZoneHitTester _hitTester = new();

    private static MonitorConfig CreateTwoZoneMonitor() => TestHelpers.CreatePercentMonitor("primary",
        TestHelpers.CreateZone("left", "Left Half", 0, 0, 0.5, 1.0, priority: 1),
        TestHelpers.CreateZone("right", "Right Half", 0.5, 0, 0.5, 1.0, priority: 1));

    private SnapEngine CreateEngine() => new(_hitTester, _converter, _logger);

    private IReadOnlyList<ZoneRenderInfo> BuildRenderInfos(MonitorConfig monitor)
    {
        var resolved = _converter.Resolve(monitor, WorkingArea).Zones;
        return resolved.Select(z => new ZoneRenderInfo
        {
            ZoneId = z.Definition.Id,
            Name = z.Definition.Name,
            Bounds = z.Bounds,
        }).ToList();
    }

    /// <summary>
    /// Simulates the ActivateSnapping flow from AppContext: engine.BeginDrag → overlay.Show → overlay.SetActiveZone.
    /// This is the exact sequence that AppContext.ActivateSnapping() performs.
    /// </summary>
    private void SimulateActivateSnapping(
        SnapEngine engine,
        IOverlayRenderer overlay,
        MonitorConfig monitor,
        int cursorX, int cursorY)
    {
        engine.BeginDrag(TestWindowHandle, monitor, WorkingArea, cursorX, cursorY);

        var zones = BuildRenderInfos(monitor);
        overlay.Show(zones, WorkingArea, engine.ActiveZone?.Definition.Id);
    }

    /// <summary>
    /// Reproduces the bug: activate snapping in left zone, cancel, reactivate in right zone.
    /// The overlay must receive the correct active zone ("right") when Show() is called —
    /// it must never be called with the stale "left" zone from the first activation.
    /// </summary>
    [Fact]
    public void Reactivation_AfterCancel_OverlayShowsCorrectZone()
    {
        var engine = CreateEngine();
        var overlay = Substitute.For<IOverlayRenderer>();
        var monitor = CreateTwoZoneMonitor();

        // First activation: cursor in left zone (x=400)
        SimulateActivateSnapping(engine, overlay, monitor, cursorX: 400, cursorY: 500);
        overlay.Received(1).Show(
            Arg.Any<IReadOnlyList<ZoneRenderInfo>>(),
            Arg.Any<Rectangle>(),
            "left");

        // Cancel (modifier released) — same as AppContext.CancelDrag()
        engine.CancelDrag();
        overlay.Hide();

        overlay.ClearReceivedCalls();

        // Second activation: cursor has moved to right zone (x=1200)
        SimulateActivateSnapping(engine, overlay, monitor, cursorX: 1200, cursorY: 500);

        // The overlay's Show() MUST receive "right" — never the stale "left"
        overlay.Received(1).Show(
            Arg.Any<IReadOnlyList<ZoneRenderInfo>>(),
            Arg.Any<Rectangle>(),
            "right");
    }

    /// <summary>
    /// Verifies that mid-drag activation with cursor in right zone correctly
    /// passes "right" zone ID to the overlay on first frame.
    /// </summary>
    [Fact]
    public void MidDragActivation_CursorInRightZone_OverlayShowsRightZone()
    {
        var engine = CreateEngine();
        var overlay = Substitute.For<IOverlayRenderer>();
        var monitor = CreateTwoZoneMonitor();

        SimulateActivateSnapping(engine, overlay, monitor, cursorX: 1200, cursorY: 500);

        overlay.Received(1).Show(
            Arg.Any<IReadOnlyList<ZoneRenderInfo>>(),
            Arg.Any<Rectangle>(),
            "right");
    }

    /// <summary>
    /// Verifies that when cursor is outside all zones, null is passed to Show().
    /// </summary>
    [Fact]
    public void MidDragActivation_CursorOutsideZones_OverlayShowsNoActiveZone()
    {
        var engine = CreateEngine();
        var overlay = Substitute.For<IOverlayRenderer>();
        var monitor = CreateTwoZoneMonitor();

        SimulateActivateSnapping(engine, overlay, monitor, cursorX: -100, cursorY: -100);

        overlay.Received(1).Show(
            Arg.Any<IReadOnlyList<ZoneRenderInfo>>(),
            Arg.Any<Rectangle>(),
            null);
    }
}
