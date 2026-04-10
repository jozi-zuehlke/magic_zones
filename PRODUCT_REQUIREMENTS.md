# Product Requirements Document: MagicZones Portable

**Version:** 1.0
**Date:** 2026-04-09
**Status:** Draft

---

## 1. Problem Statement

Microsoft PowerToys' FancyZones feature lets Windows users define custom screen regions ("zones") and snap application windows into them by holding Shift while dragging. It dramatically improves window organization for power users.

**The gap:** PowerToys requires a privileged installer and writes to the Windows registry. In corporate environments where end-users lack local administrator rights and software installation is restricted by Group Policy or MDM (Intune, SCCM), PowerToys cannot be used without IT involvement. Third-party commercial alternatives (Divvy, AquaSnap Pro, etc.) are paid products that also typically require installation.

No free, portable, zero-installation window zone manager exists for Windows that a corporate user can place in their Documents folder or a USB drive and run without escalated privileges.

**The solution:** A framework-dependent .NET 8 single-binary application that reads a JSON config file from its own directory. Users carry it on a USB stick or drop it in their user profile folder, configure it once, and use it across any corporate machine without IT involvement.

---

## 2. Goals

1. **Portable single-binary deployment.** A single `.exe` that runs from any user-writable directory with no installer, no registry writes, and no elevation required.
2. **Familiar zone-snapping UX.** Hold Shift and drag a window → a highlight overlay shows the nearest zone → release the mouse to snap the window to that zone.
3. **Config-file-driven layout.** All zone definitions are expressed in a human-readable JSON file editable in Notepad. No GUI editor required.
4. **Low resource footprint.** Under 30 MB private working set at idle; negligible CPU when not dragging.
5. **Corporate-safe.** No UAC prompts, no registry writes, no network access, no installer.

---

## 3. Non-Goals (v1)

- Visual zone editor (drag-to-draw UI) — v2
- Multi-monitor support — v2 (schema is forward-compatible, but the engine only acts on primary monitor in v1)
- Saved/restorable window layouts — v2
- Per-application zone rules — v2
- Keyboard-only window navigation between zones — v2
- Auto-start on login (no self-registration in startup locations) — users configure this themselves
- macOS or Linux support

---

## 4. User Stories

| ID | Story |
|----|-------|
| US-01 | As a corporate user with no admin rights, I want to drop a single `.exe` into my Documents folder and run it — no installer, no UAC prompt. |
| US-02 | On first run with no config file present, the app generates a default `zones.json` next to the exe and shows a notification pointing me to it. |
| US-03 | I open `zones.json` in Notepad, edit zone coordinates, save, and trigger a reload from the system tray — new layout takes effect immediately. |
| US-04 | I hold Shift, drag a window by its title bar, see a highlight overlay on the nearest zone, and release the mouse — the window resizes to fill that zone exactly. |
| US-05 | I have a large "workspace" zone and a smaller "sidebar" zone that overlaps it. The smaller (higher-priority) zone wins when my cursor is inside both. |
| US-06 | I started a Shift+drag but changed my mind. I release Shift (or press Escape) before releasing the mouse — the window drops at its current position, no snap. |
| US-07 | I right-click the tray icon and toggle snapping off to drag windows freely, then toggle it back on. |
| US-08 | I edit `zones.json`, click "Reload Config" in the tray menu, and see a confirmation notification with the number of zones loaded. |
| US-09 | I made a typo in `zones.json` — the app shows a balloon notification describing the parse error; the previous config remains active. |

---

## 5. Functional Requirements

### 5.1 Zone Configuration
- **FR-01** Read zones from `zones.json` in the same directory as the executable.
- **FR-02** Fallback to `%APPDATA%\MagicZonesPortable\zones.json` if not found beside the exe.
- **FR-03** Generate a default `zones.json` on first run if neither location has a config file.
- **FR-04** Zone coordinates may be expressed as absolute pixels or as a fraction (0.0–1.0) of the monitor's working area. The unit is declared per monitor in the config.
- **FR-05** Each zone has a unique `id`, a human-readable `name`, and an integer `priority`. Lower value = higher precedence.
- **FR-06** Zones that extend outside monitor bounds are clipped to the monitor boundary on load; a warning notification is shown.

### 5.2 Window Snapping Behavior
- **FR-07** Snapping triggers only when the user holds Shift and drags a window by its title bar (detected via `SetWinEventHook` on `EVENT_SYSTEM_MOVESIZESTART` / `EVENT_SYSTEM_MOVESIZEEND`).
- **FR-08** Snap is applied on mouse button release (`EVENT_SYSTEM_MOVESIZEEND`), not continuously during drag.
- **FR-09** Zone selection uses the cursor position at the moment of release.
- **FR-10** If the cursor is in exactly one zone → that zone wins.
- **FR-11** If the cursor is in multiple overlapping zones → the zone with the lowest `priority` value wins. Ties broken by order in the `zones` array.
- **FR-12** If the cursor is in no zone → no snap; window releases at dragged position.
- **FR-13** On snap: window is resized and repositioned to exactly fill the zone rectangle via `SetWindowPos` with `SWP_NOZORDER | SWP_NOACTIVATE`.
- **FR-14** If the user releases Shift before releasing the mouse, snap is cancelled.
- **FR-15** Pressing Escape during a Shift+drag cancels the snap.
- **FR-16** The engine does not snap: the taskbar (`Shell_TrayWnd`), tool windows (`WS_EX_TOOLWINDOW`), or the app's own overlay window.
- **FR-17** Maximized windows are restored (`ShowWindow SW_RESTORE`) before being repositioned to the zone.

### 5.3 Zone Highlight Overlay
- **FR-18** During a Shift+drag, a semi-transparent overlay window renders all zones on the screen.
- **FR-19** The zone the cursor is currently in (highest-priority candidate) is rendered as "active" (configurable color and opacity).
- **FR-20** All other zones are rendered in a dimmer "inactive" style to provide spatial context.
- **FR-21** The overlay is a click-through, always-on-top, non-activating layered window (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE`) — it does not interfere with the drag.
- **FR-22** The overlay is hidden when no Shift+drag is active.
- **FR-23** Overlay rendering uses GDI+ or WinForms painting; no elevated privileges required.

### 5.4 System Tray
- **FR-24** A `NotifyIcon` is added to the tray on startup.
- **FR-25** The tray icon visually indicates enabled/disabled state (two icon variants or a checkmark).
- **FR-26** Right-click context menu items:
  - **Enable / Disable** (toggle; shows current state)
  - **Reload Config** (re-read `zones.json` from disk)
  - **Open Config File** (opens `zones.json` in default text editor)
  - **About** (version, .NET version, config file path)
  - **Exit**
- **FR-27** Tray icon tooltip shows application name and enabled/disabled state.

### 5.5 Global Hotkey
- **FR-28** A configurable global hotkey (default: `Ctrl+Win+Z`) toggles snapping on/off.
- **FR-29** If the hotkey is already registered by another app, the application continues without it and shows a one-time warning notification.
- **FR-30** Hotkey registration uses `RegisterHotKey` (HWND-based, not a low-level hook).

---

## 6. Technical Requirements

### 6.1 Runtime and Deployment
- **TR-01** Written in C# targeting .NET 8.
- **TR-02** Published as a framework-dependent single-file executable: `dotnet publish -r win-x64 --self-contained false -p:PublishSingleFile=true`. No companion DLLs, no runtime config files.
- **TR-03** Primary target: `win-x64`. `win-arm64` is desirable but not required for v1.
- **TR-04** No UAC elevation required. All required APIs (`SetWinEventHook`, `SetWindowsHookEx`, `SetWindowPos`, `RegisterHotKey`, `NotifyIcon`) are available to standard user accounts.
- **TR-05** No writes to `HKLM` or any HKEY_LOCAL_MACHINE path. Avoid all registry writes.
- **TR-06** Requires a Windows Forms message loop (for `NotifyIcon`, hooks, hotkey messages). A minimal `ApplicationContext` approach (no visible main window) is the preferred pattern.

### 6.2 Windows API Surface

| Purpose | API |
|---------|-----|
| Detect window move/resize start and end | `SetWinEventHook` with `EVENT_SYSTEM_MOVESIZESTART` (0x000A) and `EVENT_SYSTEM_MOVESIZEEND` (0x000B) |
| Track cursor during drag | `GetCursorPos` polled on a ~16 ms timer during drag |
| Shift key state | `GetAsyncKeyState(VK_SHIFT)` at drag start; polled during drag |
| Escape key during drag | `SetWindowsHookEx(WH_KEYBOARD_LL)` installed only during active drag |
| Move and resize window | `SetWindowPos` |
| Restore maximized window | `ShowWindow(SW_RESTORE)`, `IsZoomed` |
| Overlay transparency | `SetLayeredWindowAttributes` or `UpdateLayeredWindow` |
| Global hotkey | `RegisterHotKey` / `UnregisterHotKey` |
| Tray icon | `System.Windows.Forms.NotifyIcon` |
| Open config in default editor | `Process.Start` with `UseShellExecute = true` |
| Monitor working area | `Screen.PrimaryScreen.WorkingArea` |

- **TR-07** The low-level mouse hook (`WH_MOUSE_LL`) is installed only during an active Shift+drag, not permanently, to minimize global hook overhead.
- **TR-08** Hook callbacks must be fast (<50 ms). They post a message or update a volatile field; rendering is deferred to the UI thread.
- **TR-09** JSON deserialization uses `System.Text.Json` (built into .NET 8; no extra NuGet dependencies for config).
- **TR-10** A `FileSystemWatcher` on `zones.json` enables automatic reload on file save, with a 500 ms debounce.
- **TR-11** The application declares Per-Monitor DPI Aware v2 in its application manifest.

### 6.3 Error Handling and Logging
- **TR-12** Rolling log file written to `%TEMP%\MagicZonesPortable\fzp-{date}.log`. Captures startup info, config load results, hook status, snap events, and errors.
- **TR-13** Log files older than 7 days are deleted on startup.
- **TR-14** Unhandled exceptions are caught by `Application.ThreadException` / `AppDomain.CurrentDomain.UnhandledException`, logged, and shown as tray balloons before exit.

---

## 7. Config File Format

### 7.1 Design Principles
- Human-editable JSON (not YAML/TOML) — `System.Text.Json` built into .NET 8.
- Supports pixel or percentage coordinates for resolution independence.
- Unknown JSON properties are silently ignored (forward-compatible).
- Multi-monitor-ready in schema; v1 engine only processes primary monitor zones.

### 7.2 Top-Level Schema

```json
{
  "version": 1,
  "settings": { ... },
  "monitors": [ ... ]
}
```

### 7.3 `settings` Object

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `activationModifier` | string | `"Shift"` | Modifier key held during drag. Values: `"Shift"`, `"Ctrl"`, `"Alt"`. |
| `highlightActiveZoneColor` | string | `"#0078D4"` | Hex color (RGB or ARGB) for the highlighted active zone. |
| `highlightActiveZoneOpacity` | number | `0.5` | Active zone fill opacity (0.0–1.0). |
| `highlightInactiveZoneColor` | string | `"#888888"` | Hex color for inactive zone borders. |
| `highlightInactiveZoneOpacity` | number | `0.2` | Inactive zone indicator opacity. |
| `toggleHotkey` | string | `"Ctrl+Win+Z"` | Global enable/disable hotkey. Format: modifiers joined by `+`, then key name. |
| `logLevel` | string | `"INFO"` | Minimum log severity written to file. Values: `"DEBUG"`, `"INFO"`, `"WARN"`, `"ERROR"`. |
| `autoReloadConfig` | boolean | `true` | Watch `zones.json` for changes and reload automatically. |
| `autoReloadDebounceMs` | integer | `500` | Milliseconds to debounce file-change events before reloading. |

### 7.4 Monitor Object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Logical name (e.g., `"primary"`). Used in logs. |
| `matchBy` | string | Yes | How to select the physical monitor: `"primary"`, `"deviceName"`, or `"index"`. |
| `deviceName` | string | Conditional | Required when `matchBy` is `"deviceName"` (e.g., `"\\\\.\\DISPLAY1"`). |
| `index` | integer | Conditional | Required when `matchBy` is `"index"` (zero-based). |
| `coordinateUnit` | string | Yes | `"pixels"` or `"percent"`. |
| `zones` | array | Yes | Array of zone objects. |

### 7.5 Zone Object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique identifier within the monitor (e.g., `"left-half"`). |
| `name` | string | Yes | Display name shown in the overlay. |
| `priority` | integer | Yes | Selection precedence when overlapping. Lower value = higher precedence. ≥ 0. |
| `x` | number | Yes | Left edge. Pixels from working area left, or fraction of working area width. |
| `y` | number | Yes | Top edge. Pixels from working area top, or fraction of working area height. |
| `width` | number | Yes | Zone width in the configured unit. |
| `height` | number | Yes | Zone height in the configured unit. |

### 7.6 Annotated Example

```json
{
  "version": 1,
  "settings": {
    "activationModifier": "Shift",
    "highlightActiveZoneColor": "#0078D4",
    "highlightActiveZoneOpacity": 0.5,
    "highlightInactiveZoneColor": "#888888",
    "highlightInactiveZoneOpacity": 0.2,
    "toggleHotkey": "Ctrl+Win+Z",
    "logLevel": "INFO",
    "autoReloadConfig": true,
    "autoReloadDebounceMs": 500
  },
  "monitors": [
    {
      "id": "primary",
      "matchBy": "primary",
      "coordinateUnit": "percent",
      "zones": [
        { "id": "left-half",          "name": "Left Half",          "priority": 10, "x": 0.0,  "y": 0.0, "width": 0.5,  "height": 1.0 },
        { "id": "right-half",         "name": "Right Half",         "priority": 10, "x": 0.5,  "y": 0.0, "width": 0.5,  "height": 1.0 },
        { "id": "top-right-quarter",  "name": "Top Right Quarter",  "priority": 5,  "x": 0.5,  "y": 0.0, "width": 0.5,  "height": 0.5 },
        { "id": "sidebar",            "name": "Sidebar",            "priority": 1,  "x": 0.75, "y": 0.0, "width": 0.25, "height": 1.0 }
      ]
    }
  ]
}
```

**Overlap resolution in this example:**
- Cursor in left 50%: → `left-half` (no overlap)
- Cursor in right 50%, bottom half: → `right-half` (priority 10; no other zone there)
- Cursor in right 50%, top half: → `top-right-quarter` (priority 5 beats `right-half` priority 10)
- Cursor in rightmost 25%: → `sidebar` (priority 1 beats everything)

---

## 8. Behavior Specification

### 8.1 Drag Lifecycle State Machine

```
IDLE
  → [MOVESIZESTART + Shift held]  → DRAG_ACTIVE (install overlay, start cursor poll)
  → [MOVESIZESTART + Shift not held] → IDLE (ignore)

DRAG_ACTIVE
  → [Shift released mid-drag]     → DRAG_CANCELLED
  → [Escape pressed]              → DRAG_CANCELLED
  → [MOVESIZEEND, state = ACTIVE] → apply snap → IDLE
  → [MOVESIZEEND, state = CANCELLED] → IDLE (no snap)
```

### 8.2 Zone Hit Testing Algorithm

On each cursor poll tick and at `MOVESIZEEND`:
1. `GetCursorPos` → cursor point in screen coordinates.
2. For each zone on the primary monitor, test `rect.Contains(cursorPoint)`.
3. Collect all containing zones into a candidate set.
4. If empty → no active zone.
5. If one → that zone is active.
6. If multiple → sort by `priority` ascending, then by array index ascending; first = active zone.

### 8.3 Snap Application

1. If active zone is null → do nothing.
2. Get target HWND from the `MOVESIZEEND` event.
3. If `IsZoomed(hwnd)` → `ShowWindow(hwnd, SW_RESTORE)`.
4. Convert zone geometry to absolute screen pixels:
   - `percent` unit: multiply `x`/`width` by `workingArea.Width`, `y`/`height` by `workingArea.Height`.
   - Add `workingArea.Left` / `workingArea.Top` offset.
5. `SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE)`.
6. Log: `[SNAP] hwnd={hwnd} window="{title}" zone="{id}" rect={x},{y},{w},{h}`.

### 8.4 Overlapping Zones
- Zones may overlap arbitrarily; no validation prevents it.
- Hit testing is purely point-in-rectangle; no proximity/magnetic behavior in v1.
- Equal-priority overlapping zones: earlier in the `zones` array wins.
- The overlay renders all zones simultaneously; active zone is drawn last (on top).

### 8.5 Config Reload
1. Parse new `zones.json` in memory.
2. On failure: existing config stays active; tray error notification shown.
3. On success: new config atomically replaces old config.
4. Any in-progress drag uses pre-reload geometry for that drag only.
5. Tray notification confirms reload with zone count.

### 8.6 Windows the Engine Ignores

| Criterion | Check |
|-----------|-------|
| Taskbar | Class name `Shell_TrayWnd` or `Shell_SecondaryTrayWnd` |
| Tool windows | `GWL_EXSTYLE` has `WS_EX_TOOLWINDOW` bit |
| The app's own overlay | HWND matches the overlay window's HWND |

### 8.7 DPI Awareness
- Application manifest declares Per-Monitor DPI Aware v2.
- All coordinates from `SetWinEventHook`, `GetCursorPos`, and `Screen.WorkingArea` are in physical pixels when the process is DPI-aware.
- Percent-unit zone geometry is computed against physical working area pixel dimensions.

---

## 9. MVP Scope

The v1 MVP delivers exactly these features:

| # | Feature |
|---|---------|
| 1 | Single-file framework-dependent `.exe`; no installer; no admin rights |
| 2 | Read `zones.json` from exe directory (fallback `%APPDATA%`) |
| 3 | Generate default `zones.json` on first run |
| 4 | System tray: Enable/Disable, Reload Config, Open Config File, About, Exit |
| 5 | Shift+drag snapping on primary monitor |
| 6 | Semi-transparent zone highlight overlay during drag |
| 7 | Snap on mouse release; resolve overlaps by priority then array order |
| 8 | Cancel snap: release Shift or press Escape mid-drag |
| 9 | Global hotkey (`Ctrl+Win+Z`) to toggle snapping |
| 10 | Config hot-reload: manual (tray) and automatic (FileSystemWatcher) |
| 11 | Error notifications for config parse failures |
| 12 | Rolling log in `%TEMP%` |
| 13 | Pixel and percent coordinate units |
| 14 | Restore maximized windows before snapping |

**MVP Success Criteria:**
1. Runs on Windows 10 22H2 / Windows 11 with .NET 8 runtime installed and no admin account.
2. ≥ 8 non-overlapping zones reliably snappable.
3. Overlapping zones with distinct priorities always resolve to the expected zone.
4. < 30 MB private working set at idle.
5. Config reload completes in < 1 s.
6. Overlay appears within 100 ms of Shift+drag start; disappears within 100 ms of drag end.
7. Zero registry writes (verifiable with Process Monitor).
8. Normal (non-Shift) window dragging is completely unaffected.

---

## 10. Future Enhancements (v2+)

| Feature | Notes |
|---------|-------|
| Multi-monitor support | Config schema already accommodates this; v2 engine applies zones per-monitor |
| Visual zone editor | WPF/WinForms overlay for drawing zones; saves to existing JSON format |
| Proximity/magnetic snap threshold | `snapProximityPx` setting: snap even when cursor is N px outside zone boundary |
| Keyboard navigation between zones | Hotkeys to move foreground window to adjacent zone |
| Layout profiles | Named zone sets switchable via tray menu or hotkey |
| Per-application zone affinity | Assign process name pattern to auto-snap to a specific zone on launch |
| Saved window layout snapshots | Record and restore which app is in which zone |
| Always-show zone outlines | Render zone borders permanently, not only during drag |
| ARM64 build | `win-arm64` single-file binary |

---

## Appendix A: Recommended Project Structure

```
MagicZonesPortable/
  MagicZonesPortable.csproj      # net8.0-windows, UseWindowsForms, PublishSingleFile
  Program.cs                     # Entry point, Application.Run(new AppContext())
  App/
    AppContext.cs                 # ApplicationContext subclass; owns NotifyIcon, engine
    SnapEngine.cs                 # Drag lifecycle state machine + zone hit testing
    ZoneOverlay.cs                # Transparent always-on-top overlay Form
    HotkeyManager.cs              # RegisterHotKey / UnregisterHotKey P/Invoke wrapper
    ConfigWatcher.cs              # FileSystemWatcher + debounce timer
  Config/
    ZonesConfig.cs                # Top-level deserialization model
    MonitorConfig.cs              # Per-monitor model
    ZoneDefinition.cs             # Individual zone model
    SettingsConfig.cs             # Global settings model
    ConfigLoader.cs               # File discovery, parsing, validation, default generation
  Interop/
    NativeMethods.cs              # All P/Invoke declarations (SetWinEventHook, SetWindowPos, etc.)
    WinEventHook.cs               # SetWinEventHook RAII wrapper + delegate lifetime management
  Logging/
    Logger.cs                     # Simple rolling file logger
  Resources/
    icon_enabled.ico
    icon_disabled.ico
    app.manifest                  # DPI awareness, UAC level (asInvoker)
```

---

## Appendix B: Key P/Invoke Signatures

```csharp
// SetWinEventHook — for MOVESIZESTART / MOVESIZEEND
[DllImport("user32.dll")] static extern IntPtr SetWinEventHook(
    uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
    WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
const uint EVENT_SYSTEM_MOVESIZEEND   = 0x000B;
const uint WINEVENT_OUTOFCONTEXT = 0x0000;

// SetWindowPos — snap window to zone
[DllImport("user32.dll")] static extern bool SetWindowPos(
    IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
const uint SWP_NOZORDER    = 0x0004;
const uint SWP_NOACTIVATE  = 0x0010;

// GetCursorPos — cursor position during drag
[DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);

// GetAsyncKeyState — Shift key sampling
[DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
const int VK_SHIFT = 0x10;

// RegisterHotKey / UnregisterHotKey — global toggle
[DllImport("user32.dll")] static extern bool RegisterHotKey(
    IntPtr hWnd, int id, uint fsModifiers, uint vk);
[DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
const uint MOD_CONTROL = 0x0002;
const uint MOD_WIN     = 0x0008;

// IsZoomed / ShowWindow — handle maximized windows
[DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hWnd);
[DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
const int SW_RESTORE = 9;
```
