using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Config;
using FancyZonesPortable.Core.Engine;
using FancyZonesPortable.Core.Logging;
using NSubstitute;
using Xunit;

namespace FancyZonesPortable.Tests.Engine;

/// <summary>
/// Tests for <see cref="SnapEngine"/>: drag state machine transitions
/// (IDLE → DRAG_ACTIVE → snap/cancel → IDLE).
/// </summary>
public class SnapEngineTests
{
    private static readonly Rectangle WorkingArea = new(0, 0, 1920, 1080);
    private static readonly nint TestWindowHandle = (nint)0xBEEF;

    private static MonitorConfig CreateTwoZoneMonitor() => new()
    {
        Id = "test-monitor",
        CoordinateUnit = "percent",
        Zones =
        [
            new ZoneDefinition { Id = "left", Name = "Left", Priority = 1, X = 0, Y = 0, Width = 0.5, Height = 1.0 },
            new ZoneDefinition { Id = "right", Name = "Right", Priority = 1, X = 0.5, Y = 0, Width = 0.5, Height = 1.0 },
        ],
    };

    private static SnapEngine CreateEngine() =>
        new(new ZoneHitTester(), new CoordinateConverter(), Substitute.For<ILogger>());

    private static SnapEngine CreateEngineWithDrag()
    {
        var engine = CreateEngine();
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        return engine;
    }

    // --- State tests ---

    [Fact]
    public void InitialState_IsIdle()
    {
        var engine = CreateEngine();
        Assert.Equal(SnapState.Idle, engine.State);
    }

    [Fact]
    public void BeginDrag_FromIdle_TransitionsToDragActive()
    {
        var engine = CreateEngine();
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        Assert.Equal(SnapState.DragActive, engine.State);
    }

    [Fact]
    public void BeginDrag_WhileDragActive_StaysInDragActive()
    {
        var engine = CreateEngineWithDrag();
        engine.BeginDrag((nint)0xDEAD, CreateTwoZoneMonitor(), WorkingArea);
        Assert.Equal(SnapState.DragActive, engine.State);
        // Original window handle should be preserved
        Assert.Equal(TestWindowHandle, engine.DraggedWindow);
    }

    [Fact]
    public void BeginDrag_SetsDraggedWindow()
    {
        var engine = CreateEngine();
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        Assert.Equal(TestWindowHandle, engine.DraggedWindow);
    }

    // --- UpdateCursorPosition tests ---

    [Fact]
    public void UpdateCursorPosition_WhileIdle_DoesNothing()
    {
        var engine = CreateEngine();
        engine.UpdateCursorPosition(400, 500);
        Assert.Equal(SnapState.Idle, engine.State);
        Assert.Null(engine.ActiveZone);
    }

    [Fact]
    public void UpdateCursorPosition_WhileDragActive_UpdatesActiveZone()
    {
        var engine = CreateEngineWithDrag();
        Assert.Null(engine.ActiveZone);
        engine.UpdateCursorPosition(400, 500);
        Assert.NotNull(engine.ActiveZone);
    }

    [Fact]
    public void UpdateCursorPosition_CursorInZone_SetsActiveZone()
    {
        var engine = CreateEngineWithDrag();
        engine.UpdateCursorPosition(400, 500);
        Assert.NotNull(engine.ActiveZone);
        Assert.Equal("left", engine.ActiveZone.Definition.Id);
    }

    [Fact]
    public void UpdateCursorPosition_CursorOutsideAllZones_ActiveZoneIsNull()
    {
        var engine = CreateEngineWithDrag();
        engine.UpdateCursorPosition(-100, -100);
        Assert.Null(engine.ActiveZone);
    }

    [Fact]
    public void UpdateCursorPosition_CursorMovesFromOneZoneToAnother_ActiveZoneChanges()
    {
        var engine = CreateEngineWithDrag();
        engine.UpdateCursorPosition(400, 500);
        Assert.Equal("left", engine.ActiveZone!.Definition.Id);

        engine.UpdateCursorPosition(1200, 500);
        Assert.Equal("right", engine.ActiveZone!.Definition.Id);
    }

    // --- CommitSnap tests ---

    [Fact]
    public void CommitSnap_WithActiveZone_ReturnsBounds()
    {
        var engine = CreateEngineWithDrag();
        engine.UpdateCursorPosition(400, 500);
        var result = engine.CommitSnap();

        Assert.NotNull(result);
        // Left zone: percent (0,0,0.5,1.0) on 1920x1080 → pixel (0,0,960,1080)
        Assert.Equal(new Rectangle(0, 0, 960, 1080), result.Value);
    }

    [Fact]
    public void CommitSnap_WithActiveZone_TransitionsToIdle()
    {
        var engine = CreateEngineWithDrag();
        engine.UpdateCursorPosition(400, 500);
        engine.CommitSnap();
        Assert.Equal(SnapState.Idle, engine.State);
    }

    [Fact]
    public void CommitSnap_WithNoActiveZone_ReturnsNull()
    {
        var engine = CreateEngineWithDrag();
        // No UpdateCursorPosition, so no active zone
        var result = engine.CommitSnap();
        Assert.Null(result);
    }

    [Fact]
    public void CommitSnap_WhileIdle_ReturnsNull()
    {
        var engine = CreateEngine();
        var result = engine.CommitSnap();
        Assert.Null(result);
    }

    // --- CancelDrag tests ---

    [Fact]
    public void CancelDrag_WhileDragActive_TransitionsToIdle()
    {
        var engine = CreateEngineWithDrag();
        engine.CancelDrag();
        Assert.Equal(SnapState.Idle, engine.State);
    }

    [Fact]
    public void CancelDrag_WhileIdle_DoesNothing()
    {
        var engine = CreateEngine();
        engine.CancelDrag();
        Assert.Equal(SnapState.Idle, engine.State);
    }

    [Fact]
    public void CancelDrag_ThenCommitSnap_ReturnsNull()
    {
        var engine = CreateEngineWithDrag();
        engine.UpdateCursorPosition(400, 500);
        engine.CancelDrag();
        var result = engine.CommitSnap();
        Assert.Null(result);
    }

    // --- Full lifecycle tests ---

    [Fact]
    public void FullDragCycle_BeginUpdateCommit_SnapsCorrectly()
    {
        var engine = CreateEngine();

        // Begin
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        Assert.Equal(SnapState.DragActive, engine.State);
        Assert.Equal(TestWindowHandle, engine.DraggedWindow);

        // Update to right zone
        engine.UpdateCursorPosition(1200, 500);
        Assert.Equal("right", engine.ActiveZone!.Definition.Id);

        // Commit
        var result = engine.CommitSnap();
        Assert.NotNull(result);
        // Right zone: percent (0.5,0,0.5,1.0) on 1920x1080 → pixel (960,0,960,1080)
        Assert.Equal(new Rectangle(960, 0, 960, 1080), result.Value);
        Assert.Equal(SnapState.Idle, engine.State);
    }

    [Fact]
    public void FullDragCycle_BeginUpdateCancel_NoSnap()
    {
        var engine = CreateEngine();

        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        engine.UpdateCursorPosition(400, 500);
        Assert.NotNull(engine.ActiveZone);

        engine.CancelDrag();
        Assert.Equal(SnapState.Idle, engine.State);
        Assert.Null(engine.ActiveZone);
    }

    [Fact]
    public void AfterCommit_CanBeginNewDrag()
    {
        var engine = CreateEngine();

        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        engine.UpdateCursorPosition(400, 500);
        engine.CommitSnap();

        // Start a new drag
        var newHandle = (nint)0xCAFE;
        engine.BeginDrag(newHandle, CreateTwoZoneMonitor(), WorkingArea);
        Assert.Equal(SnapState.DragActive, engine.State);
        Assert.Equal(newHandle, engine.DraggedWindow);
    }

    [Fact]
    public void AfterCancel_CanBeginNewDrag()
    {
        var engine = CreateEngine();

        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        engine.CancelDrag();

        // Start a new drag
        var newHandle = (nint)0xCAFE;
        engine.BeginDrag(newHandle, CreateTwoZoneMonitor(), WorkingArea);
        Assert.Equal(SnapState.DragActive, engine.State);
        Assert.Equal(newHandle, engine.DraggedWindow);
    }

    [Fact]
    public void MultipleDragCycles_WithoutReset_WorkCorrectly()
    {
        var engine = CreateEngine();

        // First cycle: begin → cancel
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        engine.CancelDrag();
        Assert.Equal(SnapState.Idle, engine.State);

        // Second cycle: begin → move → commit → returns zone
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        engine.UpdateCursorPosition(1200, 500);
        var result = engine.CommitSnap();
        Assert.NotNull(result);
        Assert.Equal(new Rectangle(960, 0, 960, 1080), result.Value);
        Assert.Equal(SnapState.Idle, engine.State);
    }

    [Fact]
    public void BeginDrag_CancelDrag_BeginDrag_DifferentCursorPosition()
    {
        var engine = CreateEngine();

        // First activation: cursor in left zone
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        engine.UpdateCursorPosition(400, 500);
        Assert.Equal("left", engine.ActiveZone!.Definition.Id);
        engine.CancelDrag();

        // Second activation: cursor in right zone
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea);
        engine.UpdateCursorPosition(1200, 500);
        Assert.Equal("right", engine.ActiveZone!.Definition.Id);

        var result = engine.CommitSnap();
        Assert.NotNull(result);
        Assert.Equal(new Rectangle(960, 0, 960, 1080), result.Value);
    }

    // --- BeginDrag with initial cursor position (mid-drag activation) ---

    [Fact]
    public void BeginDrag_WithInitialCursorPosition_SetsActiveZoneImmediately()
    {
        var engine = CreateEngine();

        // Simulate mid-drag activation: cursor is already in the right zone
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea, cursorX: 1200, cursorY: 500);

        // ActiveZone should be set immediately — no UpdateCursorPosition needed
        Assert.Equal(SnapState.DragActive, engine.State);
        Assert.NotNull(engine.ActiveZone);
        Assert.Equal("right", engine.ActiveZone.Definition.Id);
    }

    [Fact]
    public void BeginDrag_WithInitialCursorInLeftZone_SetsLeftZoneActive()
    {
        var engine = CreateEngine();

        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea, cursorX: 400, cursorY: 500);

        Assert.NotNull(engine.ActiveZone);
        Assert.Equal("left", engine.ActiveZone.Definition.Id);
    }

    [Fact]
    public void BeginDrag_WithInitialCursorOutsideAllZones_ActiveZoneIsNull()
    {
        var engine = CreateEngine();

        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea, cursorX: -100, cursorY: -100);

        Assert.Equal(SnapState.DragActive, engine.State);
        Assert.Null(engine.ActiveZone);
    }

    [Fact]
    public void BeginDrag_WithInitialCursor_ThenCommit_SnapsToCorrectZone()
    {
        var engine = CreateEngine();

        // Activate with cursor already in right zone
        engine.BeginDrag(TestWindowHandle, CreateTwoZoneMonitor(), WorkingArea, cursorX: 1200, cursorY: 500);

        // Commit immediately without any UpdateCursorPosition call
        var result = engine.CommitSnap();
        Assert.NotNull(result);
        Assert.Equal(new Rectangle(960, 0, 960, 1080), result.Value);
    }
}
