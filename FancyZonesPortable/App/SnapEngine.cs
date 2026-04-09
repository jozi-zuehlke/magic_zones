using System.Drawing;
using System.Text;
using FancyZonesPortable.Config;
using FancyZonesPortable.Interop;
using FancyZonesPortable.Logging;

namespace FancyZonesPortable.App;

/// <summary>
/// Drag lifecycle state machine + zone hit testing + snap application.
///
/// State machine:
///   IDLE
///     → [MOVESIZESTART on eligible window] → TRACKING
///
///   TRACKING  (window is being dragged; modifier NOT held; overlay hidden)
///     → [modifier pressed]   → ACTIVE   (show overlay, start hit-testing)
///     → [MOVESIZEEND]        → IDLE     (no snap)
///
///   ACTIVE    (window is being dragged; modifier IS held; overlay visible)
///     → [modifier released]  → TRACKING (hide overlay, keep tracking)
///     → [Escape pressed]     → TRACKING (hide overlay, keep tracking)
///     → [MOVESIZEEND]        → snap     → IDLE
/// </summary>
internal sealed class SnapEngine : IDisposable
{
    private enum DragState { Idle, Tracking, Active }

    private DragState _state = DragState.Idle;
    private IntPtr _draggedHwnd = IntPtr.Zero;
    private WinEventHook? _moveStartHook;
    private WinEventHook? _moveEndHook;
    private System.Windows.Forms.Timer? _cursorPollTimer;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;

    // Extra GC roots for delegates passed to native code — prevents collection
    // even if the WinEventHook wrapper is somehow the only other reference.
    private NativeMethods.WinEventDelegate? _moveStartDelegate;
    private NativeMethods.WinEventDelegate? _moveEndDelegate;

    private readonly ZoneOverlay _overlay;
    private List<ZoneDefinition> _zones = new();
    private SettingsConfig _settings = new();
    private Rectangle _workingArea;
    private bool _enabled = true;
    private bool _disposed;

    private const int OBJID_WINDOW = 0;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Logger.Info($"Snapping {(_enabled ? "enabled" : "disabled")}");
            if (!_enabled)
                CancelDrag();
        }
    }

    public IntPtr OverlayHandle => _overlay.Handle;

    public SnapEngine(ZoneOverlay overlay)
    {
        _overlay = overlay;

        // Cursor poll timer (~16ms = ~60fps)
        _cursorPollTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _cursorPollTimer.Tick += OnCursorPoll;
    }

    /// <summary>
    /// Installs the win event hooks to detect window move/resize start/end.
    /// </summary>
    public void Start()
    {
        // Store delegates as explicit fields to guarantee they stay rooted for
        // the entire lifetime of the engine — prevents GC from collecting the
        // native callback thunks while the hooks are active.
        _moveStartDelegate = OnMoveSizeStart;
        _moveEndDelegate = OnMoveSizeEnd;

        _moveStartHook = new WinEventHook(
            NativeMethods.EVENT_SYSTEM_MOVESIZESTART,
            NativeMethods.EVENT_SYSTEM_MOVESIZESTART,
            _moveStartDelegate);

        _moveEndHook = new WinEventHook(
            NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
            NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
            _moveEndDelegate);

        Logger.Info("SnapEngine started — hooks installed.");
    }

    /// <summary>
    /// Updates the zone configuration. Safe to call during a drag (new config applies next drag).
    /// </summary>
    public void UpdateConfig(List<ZoneDefinition> zones, SettingsConfig settings, Rectangle workingArea)
    {
        _zones = zones;
        _settings = settings;
        _workingArea = workingArea;
        _overlay.UpdateZones(zones, settings);
    }

    private void OnMoveSizeStart(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Only process top-level window events (OBJID_WINDOW == 0)
        if (idObject != OBJID_WINDOW) return;

        Logger.Info($"[EVENT] MOVESIZESTART hwnd=0x{hwnd:X} class=\"{GetClassName(hwnd)}\" title=\"{GetWindowTitle(hwnd)}\"");

        if (!_enabled)
        {
            Logger.Info("[EVENT] Ignored: snapping is disabled.");
            return;
        }
        if (_state != DragState.Idle)
        {
            Logger.Info($"[EVENT] Ignored: engine not idle (state={_state}).");
            return;
        }
        if (hwnd == IntPtr.Zero)
        {
            Logger.Info("[EVENT] Ignored: null hwnd.");
            return;
        }

        if (ShouldIgnoreWindow(hwnd)) return;

        // Always start tracking the drag — the user can press/release the
        // activation modifier at any point during the drag.
        _draggedHwnd = hwnd;
        _cursorPollTimer?.Start();
        InstallKeyboardHook();

        // If modifier is already held, go straight to Active; otherwise Tracking.
        if (IsActivationModifierHeld())
        {
            _state = DragState.Active;
            _overlay.ShowOverlay(_workingArea);
            UpdateActiveZone();
            Logger.Info($"[DRAG_START] Active (modifier held) hwnd=0x{hwnd:X} zones={_zones.Count}");
        }
        else
        {
            _state = DragState.Tracking;
            Logger.Info($"[DRAG_START] Tracking (modifier not held) hwnd=0x{hwnd:X}");
        }
    }

    private void OnMoveSizeEnd(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != OBJID_WINDOW) return;

        Logger.Info($"[EVENT] MOVESIZEEND hwnd=0x{hwnd:X} state={_state}");

        if (_state == DragState.Idle) return;

        var wasActive = _state == DragState.Active;
        var targetHwnd = _draggedHwnd;

        CleanupDragState();

        if (!wasActive || targetHwnd == IntPtr.Zero)
        {
            Logger.Info("[DRAG_END] Not in active snap mode — no snap.");
            return;
        }

        // Final hit test at cursor position
        var activeZone = HitTest();
        if (activeZone == null)
        {
            Logger.Info("[DRAG_END] No zone at cursor position — no snap.");
            return;
        }

        ApplySnap(targetHwnd, activeZone);
    }

    private void OnCursorPoll(object? sender, EventArgs e)
    {
        if (_state == DragState.Idle) return;

        bool modifierHeld = IsActivationModifierHeld();

        if (_state == DragState.Tracking && modifierHeld)
        {
            // Modifier just pressed mid-drag → activate overlay
            _state = DragState.Active;
            _overlay.ShowOverlay(_workingArea);
            Logger.Info("[DRAG] Modifier pressed — entering active snap mode.");
            UpdateActiveZone();
        }
        else if (_state == DragState.Active && !modifierHeld)
        {
            // Modifier released mid-drag → deactivate overlay, keep tracking
            _state = DragState.Tracking;
            _overlay.HideOverlay();
            Logger.Info("[DRAG] Modifier released — returning to tracking mode.");
        }
        else if (_state == DragState.Active)
        {
            UpdateActiveZone();
        }
    }

    private void UpdateActiveZone()
    {
        var zone = HitTest();
        _overlay.SetActiveZone(zone?.Id);
    }

    private ZoneDefinition? HitTest()
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return null;

        var candidates = new List<ZoneDefinition>();

        foreach (var zone in _zones)
        {
            if (zone.AbsoluteRect.Contains(pt.X, pt.Y))
                candidates.Add(zone);
        }

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        // Sort by priority ascending, then by array index ascending
        candidates.Sort((a, b) =>
        {
            int cmp = a.Priority.CompareTo(b.Priority);
            return cmp != 0 ? cmp : a.ArrayIndex.CompareTo(b.ArrayIndex);
        });

        return candidates[0];
    }

    private void ApplySnap(IntPtr hwnd, ZoneDefinition zone)
    {
        // Restore maximized windows first
        if (NativeMethods.IsZoomed(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            Logger.Info("[SNAP] Restored maximized window before snapping.");
        }

        var rect = zone.AbsoluteRect;

        // Compensate for the invisible DWM extended frame border that modern
        // Windows 10/11 windows have (~7 px on left, right, bottom; 0 on top).
        // Without this, the visible window appears inset from the zone edges.
        var border = GetInvisibleBorderSize(hwnd);
        int x = rect.X - border.Left;
        int y = rect.Y - border.Top;
        int w = rect.Width + border.Left + border.Right;
        int h = rect.Height + border.Top + border.Bottom;

        NativeMethods.SetWindowPos(
            hwnd, IntPtr.Zero,
            x, y, w, h,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        var title = GetWindowTitle(hwnd);
        Logger.Info($"[SNAP] hwnd=0x{hwnd:X} window=\"{title}\" zone=\"{zone.Id}\" zone_rect={rect.X},{rect.Y},{rect.Width},{rect.Height} border=L{border.Left},T{border.Top},R{border.Right},B{border.Bottom}");
    }

    /// <summary>
    /// Measures the invisible DWM border around a window by comparing GetWindowRect
    /// (full frame including invisible border) with DwmGetWindowAttribute(EXTENDED_FRAME_BOUNDS)
    /// (visible bounds only). Returns the per-side invisible margin.
    /// </summary>
    private static (int Left, int Top, int Right, int Bottom) GetInvisibleBorderSize(IntPtr hwnd)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var windowRect))
            return (0, 0, 0, 0);

        int hr = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out var frameBounds,
            System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>());

        if (hr != 0)
            return (0, 0, 0, 0);

        return (
            Left:   frameBounds.Left   - windowRect.Left,
            Top:    frameBounds.Top    - windowRect.Top,
            Right:  windowRect.Right   - frameBounds.Right,
            Bottom: windowRect.Bottom  - frameBounds.Bottom
        );
    }

    private bool IsActivationModifierHeld()
    {
        int vk = _settings.ActivationModifier.ToUpperInvariant() switch
        {
            "SHIFT" => NativeMethods.VK_SHIFT,
            "CTRL" or "CONTROL" => NativeMethods.VK_CONTROL,
            "ALT" => NativeMethods.VK_MENU,
            _ => NativeMethods.VK_SHIFT
        };

        return (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    private bool ShouldIgnoreWindow(IntPtr hwnd)
    {
        // Ignore the overlay itself
        if (hwnd == _overlay.Handle)
        {
            Logger.Info($"[EVENT] Ignored: hwnd 0x{hwnd:X} is the overlay window.");
            return true;
        }

        // Ignore taskbar
        var className = GetClassName(hwnd);
        if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            Logger.Info($"[EVENT] Ignored: hwnd 0x{hwnd:X} is taskbar ({className}).");
            return true;
        }

        // Ignore tool windows (WS_EX_TOOLWINDOW)
        var exStyle = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
        {
            Logger.Info($"[EVENT] Ignored: hwnd 0x{hwnd:X} is a tool window (exStyle=0x{exStyle:X}).");
            return true;
        }

        return false;
    }

    private void InstallKeyboardHook()
    {
        _keyboardProc = KeyboardHookCallback;
        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _keyboardProc,
            NativeMethods.GetModuleHandle(null),
            0);

        if (_keyboardHook == IntPtr.Zero)
            Logger.Warn("Failed to install keyboard hook for Escape detection.");
    }

    private void UninstallKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        _keyboardProc = null;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= NativeMethods.HC_ACTION && wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
        {
            int vkCode = System.Runtime.InteropServices.Marshal.ReadInt32(lParam);
            if (vkCode == NativeMethods.VK_ESCAPE && _state == DragState.Active)
            {
                Logger.Info("[DRAG] Escape pressed — returning to tracking mode.");
                _state = DragState.Tracking;
                _overlay.HideOverlay();
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void CancelDrag()
    {
        if (_state != DragState.Idle)
            CleanupDragState();
    }

    private void CleanupDragState()
    {
        _cursorPollTimer?.Stop();
        _overlay.HideOverlay();
        UninstallKeyboardHook();
        _state = DragState.Idle;
        _draggedHwnd = IntPtr.Zero;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        int len = NativeMethods.GetWindowTextLength(hwnd);
        if (len == 0) return "";
        var sb = new StringBuilder(len + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CancelDrag();

        _cursorPollTimer?.Stop();
        _cursorPollTimer?.Dispose();
        _cursorPollTimer = null;

        _moveStartHook?.Dispose();
        _moveEndHook?.Dispose();
        _moveStartHook = null;
        _moveEndHook = null;
        _moveStartDelegate = null;
        _moveEndDelegate = null;

        Logger.Info("SnapEngine disposed.");
    }
}
