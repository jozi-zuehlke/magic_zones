# Rust Experiment Plan: `experiments/rust_magic_zones/`

## Context

MagicZones Portable is a Windows window zone-snapping utility written in C# (.NET 8 / WinForms). The user wants a Rust port as an experiment, placed in `experiments/rust_magic_zones/` inside the existing repo. The goal is a functionally equivalent app that compiles to a single, self-contained `.exe` with no runtime dependencies (more portable than the .NET version). This is an exploratory port, not a production replacement.

---

## Project Layout

```
experiments/rust_magic_zones/
├── Cargo.toml
├── build.rs                          # embed DPI-aware manifest
├── src/
│   ├── main.rs                       # init, message loop, cleanup
│   ├── app.rs                        # AppState struct + drag state machine
│   ├── config/
│   │   ├── mod.rs
│   │   ├── models.rs                 # serde structs (ZonesConfig, Settings, etc.)
│   │   ├── loader.rs                 # discover path, load, save, default gen
│   │   ├── validator.rs              # validation rules
│   │   └── settings_parser.rs        # parse modifier VK, hotkey, color, log level
│   ├── engine/
│   │   ├── mod.rs
│   │   ├── types.rs                  # Rect, ResolvedZone, SnapState, ZoneRenderInfo
│   │   ├── coordinate_converter.rs   # percent + pixel resolution, clipping
│   │   ├── zone_hit_tester.rs        # point-in-rect with priority tie-breaking
│   │   ├── snap_engine.rs            # Idle/DragActive state machine
│   │   ├── snap_applier.rs           # SetWindowPos + DWM compensation
│   │   ├── window_filter.rs          # filter dragged windows
│   │   └── modifier_key_state_tracker.rs
│   ├── hooks/
│   │   ├── mod.rs
│   │   ├── win_event_hook.rs         # SetWinEventHook, OnceLock sender, guard
│   │   ├── keyboard_hook.rs          # SetWindowsHookExW, OnceLock sender, guard
│   │   └── hotkey_manager.rs         # RegisterHotKey via HWND_MESSAGE window
│   ├── overlay/
│   │   └── window.rs                 # layered window + GDI UpdateLayeredWindow
│   ├── tray/
│   │   └── manager.rs                # Shell_NotifyIcon (raw Win32)
│   └── logger/
│       └── mod.rs                    # tracing-appender rolling file
```

---

## Key Dependencies (`Cargo.toml`)

```toml
[dependencies]
windows = { version = "0.58", features = [
    "Win32_Foundation",
    "Win32_UI_WindowsAndMessaging",
    "Win32_UI_Shell",
    "Win32_UI_HiDpi",
    "Win32_Graphics_Gdi",
    "Win32_Graphics_Dwm",
    "Win32_System_LibraryLoader",
    "Win32_System_Threading",
] }
serde            = { version = "1", features = ["derive"] }
serde_json       = "1"
notify           = "6"
tracing          = "0.1"
tracing-appender = "0.2"
tracing-subscriber = { version = "0.3", features = ["env-filter"] }
parking_lot      = "0.12"
crossbeam-channel = "0.5"

[profile.release]
opt-level = 3
lto = "thin"
codegen-units = 1
strip = true
```

---

## Critical Types

### Config (`config/models.rs`)
- `ZonesConfig { version, settings, monitors }` — mirrors `ZonesConfig.cs`
- `Settings { activation_modifier, highlight_*_color, highlight_*_opacity, toggle_hotkey, log_level, auto_reload_config, auto_reload_debounce_ms }` — serde camelCase aliases
- `MonitorConfig { id, match_by, device_name, index, coordinate_unit, zones }`
- `ZoneDefinition { id, name, priority, x, y, width, height }`

### Engine (`engine/types.rs`)
- `Rect { x, y, width, height }` — pixel-space rectangle
- `ResolvedZone { definition: ZoneDefinition, bounds: Rect }` — result of coordinate conversion
- `SnapState { Idle, DragActive }`
- `ZoneRenderInfo { zone_id, name, bounds }` — passed to overlay

### App (`app.rs`)
```rust
pub struct AppState {
    config:           Arc<parking_lot::RwLock<ZonesConfig>>,
    config_path:      PathBuf,
    config_watcher:   Option<notify::RecommendedWatcher>,
    snap_engine:      SnapEngine,
    window_filter:    WindowFilter,
    modifier_tracker: ModifierKeyStateTracker,
    activation_modifier: u32,           // VK code
    kbd_hook_guard:   Option<KeyboardHookGuard>,
    hotkey_manager:   HotkeyManager,
    win_event_rx:     Receiver<WinEventMsg>,
    kbd_rx:           Receiver<KeyboardMsg>,
    config_rx:        Receiver<()>,
    overlay:          OverlayWindow,
    tray:             TrayManager,
    enabled:          bool,
    tracking_drag:    bool,
    tracked_hwnd:     isize,
    msg_hwnd:         HWND,
    timer_id:         usize,
}
```

---

## Threading Strategy

All Win32 UI work runs on the **main thread** (STA):
- `SetWinEventHook`, `SetWindowsHookExW`, `RegisterHotKey`, `Shell_NotifyIcon`, `SetTimer`
- `GetMessage` / `DispatchMessage` loop

The hook callbacks (`extern "system" fn`) cannot capture closures. They communicate via global `OnceLock<crossbeam_channel::Sender<...>>` initialized at startup:

```rust
static WIN_EVENT_TX: OnceLock<Sender<WinEventMsg>> = OnceLock::new();
static KBD_HOOK_TX:  OnceLock<Sender<KeyboardMsg>> = OnceLock::new();
```

The main loop drains these channels on each iteration via `try_recv` before blocking on `GetMessage`.

**File watcher** (`notify`) spawns its own thread; on change it calls `PostMessageW` to the message-only HWND to wake the main loop, which does the actual reload.

**Logger** (`tracing-appender::non_blocking`) spawns its own thread for async I/O; hold the returned `WorkerGuard` in `main` for its lifetime.

**No tokio** — the Win32 message loop is the event loop; async framework adds complexity with no benefit here.

---

## Message Loop

```rust
fn run_message_loop(app: &mut AppState) {
    let mut msg = MSG::default();
    loop {
        // Drain channels before blocking
        while let Ok(ev) = app.win_event_rx.try_recv() { app.on_win_event(ev); }
        while let Ok(kv) = app.kbd_rx.try_recv()       { app.on_key_event(kv); }
        while let Ok(_)  = app.config_rx.try_recv()    { app.on_config_changed(); }

        match unsafe { GetMessageW(&mut msg, None, 0, 0) }.0 {
            0 | -1 => break,
            _ => {}
        }
        match msg.message {
            WM_TIMER   => app.on_cursor_timer_tick(),
            WM_HOTKEY  => app.on_hotkey_pressed(msg.wParam.0 as i32),
            WM_APP     => app.on_tray_message(msg.wParam, msg.lParam),
            _          => unsafe { TranslateMessage(&msg); DispatchMessageW(&msg); }
        }
    }
}
```

Cursor polling uses `SetTimer` (16 ms) → `WM_TIMER` messages in the main loop — no extra thread.

---

## Hook Callback Pattern

Callbacks are bare `extern "system" fn` pointers stored in global `OnceLock` senders:

```rust
extern "system" fn win_event_proc(
    _hook: HWINEVENTHOOK, event: u32, hwnd: HWND,
    _id_object: i32, _id_child: i32, _thread: u32, _time: u32,
) {
    let _ = std::panic::catch_unwind(|| {
        let msg = match event {
            EVENT_SYSTEM_MOVESIZESTART => WinEventMsg::MoveStarted(hwnd.0),
            EVENT_SYSTEM_MOVESIZEEND   => WinEventMsg::MoveEnded(hwnd.0),
            _ => return,
        };
        if let Some(tx) = WIN_EVENT_TX.get() { let _ = tx.try_send(msg); }
    });
}
```

Wrap all callback bodies in `catch_unwind` — panicking across FFI is UB.

Guards call unhook/unregister on `Drop`:

```rust
pub struct WinEventHookGuard(HWINEVENTHOOK);
impl Drop for WinEventHookGuard {
    fn drop(&mut self) { unsafe { UnhookWinEvent(self.0); } }
}
```

The keyboard hook guard is stored as `Option<KeyboardHookGuard>` in `AppState`; installed on `on_move_started`, dropped (via `= None`) on drag end.

---

## Overlay Rendering (`overlay/window.rs`)

Window styles: `WS_POPUP | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST`

GDI rendering sequence (called on each zone highlight change):
1. `CreateCompatibleDC(None)` → screen DC
2. `CreateCompatibleDC(screen_dc)` → mem DC
3. `CreateDIBSection` (32bpp ARGB) → bitmap + raw pixel pointer
4. `SelectObject(mem_dc, hbmp)`
5. Write BGRA pixels directly into the bitmap (zero = transparent background):
   - Inactive zones: fill rect with inactive BGRA + alpha
   - Active zone: fill rect with active BGRA + alpha (drawn last)
   - 2px borders for each zone
   - Zone name text via `DrawTextW` with `DT_CENTER | DT_VCENTER | DT_SINGLELINE` (Segoe UI 12pt)
6. `UpdateLayeredWindow(hwnd, null, &pt, &size, mem_dc, &origin, 0, &blend, ULW_ALPHA)` where `blend = BLENDFUNCTION { AC_SRC_OVER, 0, 255, AC_SRC_ALPHA }`
7. Cleanup: `SelectObject`, `DeleteObject`, `DeleteDC` × 2

---

## Drag State Machine (`app.rs`)

```
IDLE
  ↓ on_move_started(hwnd) — install kbd hook, start timer
TRACKING
  ↓ modifier key pressed → activate_snapping()
SNAPPING (overlay visible, hit-testing on WM_TIMER)
  ↓ modifier released → hide overlay → back to TRACKING
  ↓ Escape key → cancel_drag() → stop_tracking() → IDLE
  ↓ on_move_ended(hwnd), modifier held → commit_snap() + apply → stop_tracking() → IDLE
  ↓ on_move_ended(hwnd), modifier not held → no snap → stop_tracking() → IDLE
TRACKING ↓ on_move_ended → stop_tracking() → IDLE
```

Key methods mirror the C# `AppContext.cs`:
- `on_move_started`, `on_move_ended`, `on_key_down`, `on_key_up`
- `on_cursor_timer_tick` — polls `GetCursorPos` + `GetAsyncKeyState`, updates overlay
- `activate_snapping` — resolves zones, shows overlay, begins hit-testing
- `cancel_drag`, `stop_tracking`

---

## Snap Applier (`engine/snap_applier.rs`)

```rust
pub fn apply(hwnd: isize, zone_bounds: Rect) {
    if is_zoomed(hwnd) { show_window(hwnd, SW_RESTORE); }
    let compensated = compute_dwm_compensated_bounds(hwnd, zone_bounds);
    set_window_pos(hwnd, compensated, SWP_NOZORDER | SWP_NOACTIVATE);
}
```

DWM compensation: `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` vs `GetWindowRect` → compute left/top/right/bottom insets → expand zone bounds by insets so window content aligns with zone.

For testability, abstract Win32 calls behind a `WindowManager` trait:
```rust
pub trait WindowManager {
    fn set_window_pos(&self, hwnd: isize, rect: Rect, flags: u32);
    fn is_zoomed(&self, hwnd: isize) -> bool;
    fn show_window(&self, hwnd: isize, cmd: i32);
    fn get_window_rect(&self, hwnd: isize) -> Rect;
    fn get_extended_frame_bounds(&self, hwnd: isize) -> Option<Rect>;
}
```

`SnapApplier<W: WindowManager>` and `WindowFilter<W: WindowManager>` allow mock implementations in unit tests.

---

## System Tray (`tray/manager.rs`)

Use raw `Shell_NotifyIcon` (`Win32_UI_Shell` feature) rather than the `tray-icon` crate — `tray-icon` may conflict with the custom message loop.

- Register `WM_APP` as the callback message on the `HWND_MESSAGE` window
- Dynamic icons: render green/red circle into a 16×16 GDI bitmap, convert to `HICON`
- Context menu: `CreatePopupMenu` + `AppendMenuW` + `TrackPopupMenu`
- Balloon: `NIIF_INFO` flag in `Shell_NotifyIconW`

---

## DPI Awareness

Call `SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)` at the start of `main`, before any windows are created. Alternatively embed a `app.manifest` via `build.rs` and the `embed-resource` crate.

---

## Implementation Order

### Phase 1 — Pure Logic (cross-platform, fully testable)
1. `config/models.rs` — serde structs + `Default` impls
2. `config/validator.rs` — port `ConfigValidator.cs`
3. `config/settings_parser.rs` — modifier VK, hotkey, color, log level parsing
4. `config/loader.rs` — discovery, load, default generation, save
5. `engine/types.rs` — `Rect`, `ResolvedZone`, `SnapState`
6. `engine/coordinate_converter.rs` — percent + pixel, clipping
7. `engine/zone_hit_tester.rs` — priority tie-breaking hit test
8. `engine/snap_engine.rs` — state machine (pure)
9. `engine/modifier_key_state_tracker.rs` — trivial port
10. Write `#[cfg(test)]` unit tests for all Phase 1 modules

### Phase 2 — Win32 Infrastructure
11. `logger/mod.rs` — tracing-appender, 7-day log rotation
12. `hooks/win_event_hook.rs` — OnceLock sender, guard, callback
13. `hooks/keyboard_hook.rs` — same pattern
14. `hooks/hotkey_manager.rs` — HWND_MESSAGE + RegisterHotKey
15. `engine/snap_applier.rs` — real SetWindowPos + DWM compensation

### Phase 3 — UI
16. `overlay/window.rs` — layered window + GDI rendering
17. `tray/manager.rs` — Shell_NotifyIcon, context menu, balloon

### Phase 4 — Integration
18. `app.rs` — AppState + all event handler methods
19. `main.rs` — init, message loop, cleanup
20. `build.rs` — manifest embedding for DPI awareness

---

## Files to Reference During Implementation

| C# file | Maps to Rust module |
|---|---|
| `App/AppContext.cs` | `app.rs` (primary port target) |
| `App/ZoneOverlay.cs` | `overlay/window.rs` |
| `Interop/NativeMethods.cs` | `windows` crate bindings reference |
| `Interop/WinEventHook.cs` | `hooks/win_event_hook.rs` |
| `Interop/KeyboardHook.cs` | `hooks/keyboard_hook.rs` |
| `Interop/HotkeyManager.cs` | `hooks/hotkey_manager.rs` |
| `Interop/TrayManager.cs` | `tray/manager.rs` |
| `Core/Engine/SnapEngine.cs` | `engine/snap_engine.rs` |
| `Core/Engine/SnapApplier.cs` | `engine/snap_applier.rs` |
| `Core/Engine/ZoneHitTester.cs` | `engine/zone_hit_tester.rs` |
| `Core/Engine/CoordinateConverter.cs` | `engine/coordinate_converter.rs` |
| `Core/Config/` | `config/` modules |
| `tests/` | `#[cfg(test)]` in each module |

---

## Verification

1. **Unit tests** — `cargo test` (cross-platform); all Phase 1 logic tests pass.
2. **Build** — `cargo build --release --target x86_64-pc-windows-msvc` produces a single `.exe`.
3. **Manual smoke test**:
   - Launch the app — tray icon appears.
   - Hold Shift + drag a window — overlay shows with zone highlights.
   - Release mouse over a zone — window snaps to zone bounds.
   - Ctrl+Win+Z toggles snapping (tray icon changes color).
   - Edit `zones.json` while running — config reloads automatically.
   - Check `%TEMP%\MagicZonesPortable\` for log output.
4. **DWM compensation** — verify that windows with drop shadows align flush to zone edges.
5. **Config edge cases** — test with an invalid `zones.json` (app should show tray balloon error and continue with last good config or default).
