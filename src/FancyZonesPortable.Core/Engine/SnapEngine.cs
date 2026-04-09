using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Config;
using FancyZonesPortable.Core.Logging;

namespace FancyZonesPortable.Core.Engine;

/// <summary>
/// Manages the drag lifecycle state machine for zone snapping.
/// States: IDLE → DRAG_ACTIVE → (snap or cancel) → IDLE.
/// </summary>
public class SnapEngine
{
    private readonly ZoneHitTester _hitTester;
    private readonly CoordinateConverter _converter;
    private readonly ILogger _logger;

    private SnapState _state = SnapState.Idle;
    private nint _draggedWindow;
    private List<ResolvedZone> _resolvedZones = [];
    private ResolvedZone? _activeZone;

    public SnapEngine(ZoneHitTester hitTester, CoordinateConverter converter, ILogger logger)
    {
        _hitTester = hitTester;
        _converter = converter;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current state of the snap engine.
    /// </summary>
    public SnapState State => _state;

    /// <summary>
    /// Gets the handle of the window currently being dragged, or zero if no drag is active.
    /// </summary>
    public nint DraggedWindow => _draggedWindow;

    /// <summary>
    /// Gets the currently active (hovered) zone, or null if none.
    /// </summary>
    public ResolvedZone? ActiveZone => _activeZone;

    /// <summary>
    /// Called when a window drag begins. Transitions from IDLE to DRAG_ACTIVE.
    /// </summary>
    /// <param name="windowHandle">The handle of the window being dragged.</param>
    /// <param name="monitor">The monitor configuration to use.</param>
    /// <param name="workingArea">The monitor's working area in pixels.</param>
    public void BeginDrag(nint windowHandle, MonitorConfig monitor, Rectangle workingArea)
    {
        if (_state != SnapState.Idle)
        {
            _logger.Warning("BeginDrag called while not in Idle state.");
            return;
        }

        _draggedWindow = windowHandle;
        _resolvedZones = _converter.Resolve(monitor, workingArea).Zones;
        _activeZone = null;
        _state = SnapState.DragActive;
        _logger.Info($"Drag started for window 0x{windowHandle:X}.");
    }

    /// <summary>
    /// Called when snapping is activated mid-drag. Transitions from IDLE to DRAG_ACTIVE
    /// and immediately hit-tests the given cursor position so the correct zone is
    /// highlighted from the very first frame.
    /// </summary>
    /// <param name="windowHandle">The handle of the window being dragged.</param>
    /// <param name="monitor">The monitor configuration to use.</param>
    /// <param name="workingArea">The monitor's working area in pixels.</param>
    /// <param name="cursorX">Current cursor X position in pixels.</param>
    /// <param name="cursorY">Current cursor Y position in pixels.</param>
    public void BeginDrag(nint windowHandle, MonitorConfig monitor, Rectangle workingArea, int cursorX, int cursorY)
    {
        BeginDrag(windowHandle, monitor, workingArea);

        if (_state == SnapState.DragActive)
        {
            _activeZone = _hitTester.HitTest(_resolvedZones, cursorX, cursorY);
        }
    }

    /// <summary>
    /// Called on each cursor move during drag. Updates the active zone.
    /// </summary>
    /// <param name="cursorX">Cursor X position in pixels.</param>
    /// <param name="cursorY">Cursor Y position in pixels.</param>
    public void UpdateCursorPosition(int cursorX, int cursorY)
    {
        if (_state != SnapState.DragActive)
        {
            return;
        }

        _activeZone = _hitTester.HitTest(_resolvedZones, cursorX, cursorY);
    }

    /// <summary>
    /// Commits the snap: returns the active zone's pixel bounds for window placement.
    /// Transitions from DRAG_ACTIVE to IDLE.
    /// </summary>
    /// <returns>The zone bounds to snap to, or null if no zone is active.</returns>
    public Rectangle? CommitSnap()
    {
        if (_state != SnapState.DragActive)
        {
            _logger.Warning("CommitSnap called while not in DragActive state.");
            return null;
        }

        Rectangle? result = null;
        if (_activeZone is not null)
        {
            result = _activeZone.Bounds;
            _logger.Info($"Snapping to zone '{_activeZone.Definition.Id}'.");
        }

        Reset();
        return result;
    }

    /// <summary>
    /// Cancels the drag without snapping. Transitions from DRAG_ACTIVE to IDLE.
    /// </summary>
    public void CancelDrag()
    {
        if (_state != SnapState.DragActive)
        {
            return;
        }

        _logger.Info("Drag cancelled.");
        Reset();
    }

    private void Reset()
    {
        _state = SnapState.Idle;
        _draggedWindow = 0;
        _resolvedZones = [];
        _activeZone = null;
    }
}

/// <summary>
/// The possible states of the snap engine.
/// </summary>
public enum SnapState
{
    /// <summary>No drag in progress.</summary>
    Idle,

    /// <summary>A window is being dragged and zones are shown.</summary>
    DragActive,
}
