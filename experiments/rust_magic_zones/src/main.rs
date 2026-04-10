#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
#![cfg_attr(not(windows), allow(dead_code, unused_imports, unused_variables))]

mod app;
mod config;
mod engine;
mod hooks;
mod logger;
mod overlay;
mod platform;
mod tray;

// ---------------------------------------------------------------------------
// Windows entry point
// ---------------------------------------------------------------------------
#[cfg(windows)]
use std::sync::Arc;

#[cfg(windows)]
use crate::app::{App, CursorTickResult};
#[cfg(windows)]
use crate::config::{ConfigFileWatcher, ConfigLoader};
#[cfg(windows)]
use crate::config::settings_parser;
#[cfg(windows)]
use crate::engine::snap_applier::SnapApplier;
#[cfg(windows)]
use crate::hooks::WinEventMsg;
#[cfg(windows)]
use crate::hooks::keyboard_hook::KeyboardHook;
#[cfg(windows)]
use crate::hooks::hotkey_manager::HotkeyManager;
#[cfg(windows)]
use crate::hooks::win_event_hook::WinEventHook;
#[cfg(windows)]
use crate::overlay::OverlayWindow;
#[cfg(windows)]
use crate::platform::windows::{Win32KeyboardState, Win32MonitorProvider, Win32WindowManager};
#[cfg(windows)]
use crate::tray::{TrayCommand, TrayManager};

#[cfg(windows)]
use windows::core::PCWSTR;
#[cfg(windows)]
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, WPARAM};
#[cfg(windows)]
use windows::Win32::UI::HiDpi::{
    SetProcessDpiAwarenessContext, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2,
};
#[cfg(windows)]
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DestroyWindow, DispatchMessageW, GetMessageW,
    KillTimer, MessageBoxW, PostQuitMessage, RegisterClassExW, SetTimer,
    TranslateMessage, MB_ICONERROR, MB_OK, MSG, WINDOW_EX_STYLE, WM_APP, WM_COMMAND,
    WM_HOTKEY, WM_TIMER, WNDCLASSEXW, WS_OVERLAPPED,
};

/// Tray icon callback message (mirrors `tray::manager::WM_TRAYICON`).
#[cfg(windows)]
const WM_TRAYICON: u32 = WM_APP + 1;

#[cfg(windows)]
fn main() {
    // Set Per-Monitor V2 DPI awareness (belt-and-suspenders with manifest).
    unsafe {
        let _ = SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    }

    if let Err(e) = run() {
        // Show a message box so the user knows what went wrong.
        let msg: Vec<u16> = format!("MagicZones failed to start:\n\n{e}")
            .encode_utf16()
            .chain(std::iter::once(0))
            .collect();
        let title: Vec<u16> = "MagicZones Error"
            .encode_utf16()
            .chain(std::iter::once(0))
            .collect();
        unsafe {
            MessageBoxW(
                None,
                PCWSTR(msg.as_ptr()),
                PCWSTR(title.as_ptr()),
                MB_OK | MB_ICONERROR,
            );
        }
    }
}

// ---------------------------------------------------------------------------
// Initialisation and message loop
// ---------------------------------------------------------------------------

#[cfg(windows)]
fn run() -> Result<(), Box<dyn std::error::Error>> {
    // --- 1. Discover / load configuration ---
    let config_path = ConfigLoader::discover_config_path().unwrap_or_else(|| {
        std::env::current_exe()
            .ok()
            .and_then(|e| e.parent().map(|p| p.join("zones.json")))
            .unwrap_or_else(|| std::path::PathBuf::from("zones.json"))
    });

    let loader = ConfigLoader::new(&config_path);
    let load_result = match loader.load_or_create_default() {
        Ok(r) => r,
        Err(e) => {
            eprintln!("Config load error: {e}");
            let config = ConfigLoader::create_default();
            crate::config::LoadResult {
                config,
                warnings: vec![format!("Using default config ({e})")],
            }
        }
    };
    let config = load_result.config;

    // --- 2. Initialise logger ---
    let log_dir = std::env::temp_dir().join("MagicZonesPortable");
    let _guard = logger::init(&log_dir, &config.settings.log_level);

    tracing::info!("MagicZones starting");
    tracing::info!("Config path: {}", config_path.display());
    for w in &load_result.warnings {
        tracing::warn!("Config warning: {w}");
    }

    // --- 3. Parse settings ---
    let hotkey_binding = settings_parser::parse_hotkey_binding(&config.settings.toggle_hotkey)
        .unwrap_or_else(|| {
            tracing::warn!(
                "Invalid toggle_hotkey '{}', falling back to Ctrl+Win+Z",
                config.settings.toggle_hotkey
            );
            settings_parser::HotkeyBinding {
                modifiers: 0x000A,
                vk: 0x5A,
            }
        });

    // --- 4. Create platform services ---
    let window_manager: Arc<dyn crate::platform::WindowManager> = Arc::new(Win32WindowManager);
    let keyboard_state: Arc<dyn crate::platform::KeyboardState> = Arc::new(Win32KeyboardState);
    let monitor_provider: Arc<dyn crate::platform::MonitorProvider> =
        Arc::new(Win32MonitorProvider);

    // --- 5. Build application ---
    let app = App::new(
        config.clone(),
        config_path.clone(),
        window_manager.clone(),
        keyboard_state,
        monitor_provider,
    );

    // --- 6. Enter message loop ---
    run_message_loop(app, &config, &loader, hotkey_binding, window_manager)?;

    tracing::info!("MagicZones shutdown complete");
    Ok(())
}

/// Create a hidden message-only window used as the tray icon's callback target.
#[cfg(windows)]
fn create_message_window() -> Result<HWND, String> {
    let class_name: Vec<u16> = "MagicZonesMsgWindow"
        .encode_utf16()
        .chain(std::iter::once(0))
        .collect();

    let wc = WNDCLASSEXW {
        cbSize: std::mem::size_of::<WNDCLASSEXW>() as u32,
        lpfnWndProc: Some(msg_wnd_proc),
        lpszClassName: PCWSTR(class_name.as_ptr()),
        ..Default::default()
    };

    unsafe {
        RegisterClassExW(&wc);

        // HWND_MESSAGE (-3) makes this a message-only window.
        let parent = HWND(-3isize as *mut _);
        CreateWindowExW(
            WINDOW_EX_STYLE::default(),
            PCWSTR(class_name.as_ptr()),
            PCWSTR::null(),
            WS_OVERLAPPED,
            0,
            0,
            0,
            0,
            Some(parent),
            None,
            None,
            None,
        )
        .map_err(|e| format!("Failed to create message window: {e}"))
    }
}

#[cfg(windows)]
extern "system" fn msg_wnd_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    unsafe { DefWindowProcW(hwnd, msg, wparam, lparam) }
}

/// Core message loop — drains channels, processes Win32 messages, and
/// dispatches events to the [`App`] coordinator.
#[cfg(windows)]
fn run_message_loop(
    mut app: App,
    config: &crate::config::ZonesConfig,
    loader: &ConfigLoader,
    hotkey_binding: settings_parser::HotkeyBinding,
    window_manager: Arc<dyn crate::platform::WindowManager>,
) -> Result<(), Box<dyn std::error::Error>> {
    // --- Channels ---
    let (win_event_tx, win_event_rx) = crossbeam_channel::unbounded();
    let (kbd_tx, kbd_rx) = crossbeam_channel::unbounded();
    let (config_tx, config_rx) = crossbeam_channel::unbounded::<()>();

    // --- Install WinEvent hook (move start / end) ---
    let _win_event_hook = WinEventHook::install(win_event_tx)
        .map_err(|e| format!("WinEventHook: {e}"))?;
    tracing::info!("WinEvent hook installed");

    // --- Register toggle hotkey ---
    let _hotkey = HotkeyManager::register_thread_hotkey(
        hotkey_binding.modifiers,
        hotkey_binding.vk,
    )
    .map_err(|e| format!("Hotkey: {e}"))?;
    tracing::info!(
        "Hotkey registered: modifiers=0x{:X} vk=0x{:X}",
        hotkey_binding.modifiers,
        hotkey_binding.vk
    );

    // --- Overlay window ---
    let mut overlay = OverlayWindow::create().map_err(|e| format!("Overlay: {e}"))?;
    apply_overlay_colors(&mut overlay, &config.settings);
    tracing::info!("Overlay window created");

    // --- Tray icon (needs a hidden message window) ---
    let msg_hwnd = create_message_window().map_err(|e| format!("MsgWindow: {e}"))?;
    let mut tray = TrayManager::create(msg_hwnd).map_err(|e| format!("Tray: {e}"))?;
    tracing::info!("Tray icon created");

    // --- Config file watcher (optional) ---
    let _config_watcher = if config.settings.auto_reload_config {
        match ConfigFileWatcher::watch(
            app.config_path(),
            config.settings.auto_reload_debounce_ms,
            config_tx,
        ) {
            Ok(w) => {
                tracing::info!("Config file watcher started");
                Some(w)
            }
            Err(e) => {
                tracing::warn!("Could not start config watcher: {e}");
                None
            }
        }
    } else {
        None
    };

    // --- Snap applier ---
    let snap_applier = SnapApplier::new(window_manager);

    // --- Keyboard hook (installed/removed dynamically during drags) ---
    let mut kbd_hook: Option<KeyboardHook> = None;

    // --- Cursor poll timer (~60 Hz) ---
    let timer_id = unsafe { SetTimer(None, 0, 16, None) };
    tracing::info!("Cursor timer started (id={})", timer_id);

    // -----------------------------------------------------------------------
    // Message loop
    // -----------------------------------------------------------------------
    loop {
        // Drain win-event channel
        while let Ok(ev) = win_event_rx.try_recv() {
            match ev {
                WinEventMsg::MoveStarted(hwnd) => {
                    app.on_move_started(hwnd);
                    if app.is_tracking() && kbd_hook.is_none() {
                        kbd_hook = KeyboardHook::install(kbd_tx.clone()).ok();
                    }
                }
                WinEventMsg::MoveEnded(_hwnd) => {
                    if let Some((hwnd, bounds)) = app.on_move_ended() {
                        snap_applier.apply_rect(hwnd, bounds);
                    }
                    overlay.hide();
                    // Uninstall keyboard hook when drag ends
                    kbd_hook = None;
                }
            }
        }

        // Drain keyboard channel
        while let Ok(kb) = kbd_rx.try_recv() {
            if app.on_key_event(&kb) {
                overlay.hide();
                kbd_hook = None;
            }
        }

        // Drain config-change channel
        while let Ok(()) = config_rx.try_recv() {
            match loader.load_with_validation() {
                Ok(result) => {
                    tracing::info!("Config reloaded ({} warnings)", result.warnings.len());
                    apply_overlay_colors(&mut overlay, &result.config.settings);
                    app.on_config_changed(result.config);
                    tray.show_balloon("MagicZones", "Configuration reloaded");
                }
                Err(e) => {
                    tracing::error!("Config reload failed: {e}");
                }
            }
        }

        // Block until a message arrives
        let mut msg = MSG::default();
        let ret = unsafe { GetMessageW(&mut msg, None, 0, 0) };
        match ret.0 {
            0 | -1 => break, // WM_QUIT or error
            _ => {}
        }

        match msg.message {
            WM_TIMER => {
                let result = app.on_cursor_tick();
                match result {
                    CursorTickResult::SnappingActivated
                    | CursorTickResult::ZoneChanged { .. } => {
                        let (zones, active) = app.get_zone_render_info();
                        overlay.show_zones(&zones, active);
                    }
                    CursorTickResult::SnappingDeactivated => {
                        overlay.hide();
                    }
                    _ => {}
                }
            }
            WM_HOTKEY => {
                let new_state = app.on_hotkey();
                tray.set_enabled(new_state);
                if !new_state {
                    overlay.hide();
                }
                tray.show_balloon(
                    "MagicZones",
                    if new_state {
                        "Snapping enabled"
                    } else {
                        "Snapping disabled"
                    },
                );
            }
            WM_COMMAND => {
                if let Some(cmd) = TrayManager::process_menu_command(msg.wParam) {
                    handle_tray_command(cmd, &mut app, &mut tray, &mut overlay, loader);
                }
            }
            x if x == WM_TRAYICON => {
                if let Some(cmd) = tray.process_tray_message(msg.lParam) {
                    handle_tray_command(cmd, &mut app, &mut tray, &mut overlay, loader);
                }
            }
            _ => unsafe {
                let _ = TranslateMessage(&msg);
                DispatchMessageW(&msg);
            },
        }
    }

    // --- Cleanup ---
    unsafe {
        let _ = KillTimer(None, timer_id);
        let _ = DestroyWindow(msg_hwnd);
    }
    tracing::info!("Message loop exited");

    Ok(())
}

/// Dispatch a [`TrayCommand`] — shared between `WM_COMMAND` and `WM_TRAYICON` paths.
#[cfg(windows)]
fn handle_tray_command(
    cmd: TrayCommand,
    app: &mut App,
    tray: &mut TrayManager,
    overlay: &mut OverlayWindow,
    loader: &ConfigLoader,
) {
    match cmd {
        TrayCommand::ToggleSnapping => {
            let new_state = app.on_hotkey();
            tray.set_enabled(new_state);
            if !new_state {
                overlay.hide();
            }
            tray.show_balloon(
                "MagicZones",
                if new_state {
                    "Snapping enabled"
                } else {
                    "Snapping disabled"
                },
            );
        }
        TrayCommand::ReloadConfig => {
            match loader.load_with_validation() {
                Ok(result) => {
                    apply_overlay_colors(overlay, &result.config.settings);
                    app.on_config_changed(result.config);
                    tray.show_balloon("MagicZones", "Configuration reloaded");
                }
                Err(e) => {
                    tracing::error!("Config reload failed: {e}");
                    tray.show_balloon("MagicZones", "Configuration reload failed");
                }
            }
        }
        TrayCommand::Exit => unsafe {
            PostQuitMessage(0);
        },
    }
}

/// Convert a parsed color (r, g, b) and opacity (0.0–1.0) into an ARGB `u32`.
#[cfg(windows)]
fn color_to_argb(r: u8, g: u8, b: u8, opacity: f64) -> u32 {
    let a = (opacity.clamp(0.0, 1.0) * 255.0) as u32;
    (a << 24) | ((r as u32) << 16) | ((g as u32) << 8) | (b as u32)
}

/// Apply highlight colours from [`Settings`] to the overlay window.
#[cfg(windows)]
fn apply_overlay_colors(overlay: &mut OverlayWindow, settings: &crate::config::Settings) {
    let active = settings_parser::parse_color(&settings.highlight_active_color)
        .map(|(r, g, b)| color_to_argb(r, g, b, settings.highlight_active_opacity))
        .unwrap_or(0x59_00_78_D4); // fallback
    let inactive = settings_parser::parse_color(&settings.highlight_inactive_color)
        .map(|(r, g, b)| color_to_argb(r, g, b, settings.highlight_inactive_opacity))
        .unwrap_or(0x33_CC_CC_CC); // fallback
    let border = 0xFF_FF_FF_FF; // white border
    overlay.set_colors(active, inactive, border);
    tracing::debug!(
        "Overlay colours applied: active=0x{:08X}, inactive=0x{:08X}",
        active,
        inactive
    );
}

// ---------------------------------------------------------------------------
// Non-Windows stub
// ---------------------------------------------------------------------------

#[cfg(not(windows))]
fn main() {
    eprintln!("MagicZones is a Windows-only application. Use `cargo test` to run tests on this platform.");
}
