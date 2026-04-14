/// Manages the system tray icon, its context menu, and user interactions.
///
/// # Architecture
///
/// Uses `Shell_NotifyIconW` to display an icon in the Windows notification area.
/// The icon changes colour to indicate whether snapping is enabled (green) or
/// disabled (red). Both icons are created programmatically via `CreateDIBSection`
/// + `CreateIconIndirect` — no `.ico` resource files needed.
///
/// A right-click on the tray icon shows a popup menu with three items: Toggle
/// Snapping, Reload Config, and Exit. A double-click toggles snapping directly.
///
/// # Message Flow
///
/// The tray registers a callback message (`WM_APP + 1`) that is sent to the
/// provided `hwnd`. The engine's message pump calls [`process_tray_message`] and
/// [`process_menu_command`] to convert raw `LPARAM`/`WPARAM` values into
/// [`TrayCommand`] values.
///
/// # RAII
///
/// Dropping [`TrayManager`] removes the tray icon (`NIM_DELETE`) and destroys
/// the icon handles.

use std::ptr;

use crate::tray::TrayCommand;

use windows::core::PCWSTR;
use windows::Win32::Foundation::{HWND, LPARAM, POINT, WPARAM};
use windows::Win32::Graphics::Gdi::{
    CreateCompatibleDC, CreateDIBSection, DeleteDC, DeleteObject, BITMAPINFO, BITMAPINFOHEADER,
    BI_RGB, DIB_RGB_COLORS,
};
use windows::Win32::UI::Shell::{
    Shell_NotifyIconW, NIF_ICON, NIF_INFO, NIF_MESSAGE, NIF_TIP, NIM_ADD, NIM_DELETE, NIM_MODIFY,
    NOTIFYICONDATAW,
};
use windows::Win32::UI::WindowsAndMessaging::{
    AppendMenuW, CreateIconIndirect, CreatePopupMenu, DestroyIcon, DestroyMenu,
    GetCursorPos, SetForegroundWindow, TrackPopupMenu, HICON, HMENU, ICONINFO,
    MF_STRING, TPM_BOTTOMALIGN, TPM_LEFTALIGN, WM_APP, WM_LBUTTONDBLCLK,
    WM_RBUTTONUP,
};

/// Callback message ID for tray icon events.
const WM_TRAYICON: u32 = WM_APP + 1;

/// Unique ID for the tray notification icon.
const TRAY_ICON_UID: u32 = 1;

/// Menu item IDs.
const IDM_TOGGLE: u32 = 1001;
const IDM_RELOAD: u32 = 1002;
const IDM_EXIT: u32 = 1003;

/// Helper macro to create a wide string literal at compile time.
/// Used for menu item labels.
macro_rules! w {
    ($s:literal) => {{
        const WIDE: &[u16] = &{
            const BYTES: &[u8] = $s.as_bytes();
            const LEN: usize = BYTES.len() + 1;
            let mut buf = [0u16; LEN];
            let mut i = 0;
            while i < BYTES.len() {
                buf[i] = BYTES[i] as u16;
                i += 1;
            }
            buf
        };
        PCWSTR(WIDE.as_ptr())
    }};
}

/// System tray icon and context menu manager.
///
/// Manages the lifecycle of a Shell_NotifyIcon entry including icon creation,
/// tooltip updates, balloon notifications, and the right-click context menu.
pub struct TrayManager {
    hwnd: HWND,
    nid: NOTIFYICONDATAW,
    icon_enabled: HICON,
    icon_disabled: HICON,
}

impl TrayManager {
    /// Create the tray icon and display it in the notification area.
    ///
    /// `hwnd` is the message-loop window that will receive `WM_TRAYICON` and
    /// `WM_COMMAND` messages. It does **not** need to be visible.
    ///
    /// # Errors
    ///
    /// Returns an error if icon creation or `Shell_NotifyIconW` fails.
    pub fn create(hwnd: HWND) -> Result<Self, String> {
        let icon_enabled =
            create_color_icon(0x00, 0xC0, 0x00).map_err(|e| format!("green icon: {e}"))?;
        let icon_disabled =
            create_color_icon(0xC0, 0x00, 0x00).map_err(|e| format!("red icon: {e}"))?;

        let mut nid = NOTIFYICONDATAW {
            cbSize: std::mem::size_of::<NOTIFYICONDATAW>() as u32,
            hWnd: hwnd,
            uID: TRAY_ICON_UID,
            uFlags: NIF_ICON | NIF_MESSAGE | NIF_TIP,
            uCallbackMessage: WM_TRAYICON,
            hIcon: icon_enabled,
            ..Default::default()
        };

        // Set tooltip text.
        set_tooltip(&mut nid, "MagicZones — Enabled");

        // SAFETY: nid is fully initialised.
        let ok = unsafe { Shell_NotifyIconW(NIM_ADD, &nid) };
        if !ok.as_bool() {
            // Clean up icons before returning error.
            unsafe {
                let _ = DestroyIcon(icon_enabled);
                let _ = DestroyIcon(icon_disabled);
            }
            return Err("Shell_NotifyIconW(NIM_ADD) failed".to_string());
        }

        tracing::info!("Tray icon created");

        Ok(Self {
            hwnd,
            nid,
            icon_enabled,
            icon_disabled,
        })
    }

    /// Show a context menu at the cursor position.
    ///
    /// The menu contains: Toggle Snapping, Reload Config, and Exit.
    /// Menu item selections are delivered as `WM_COMMAND` messages.
    pub fn show_context_menu(&self) {
        unsafe {
            let menu = CreatePopupMenu().unwrap_or(HMENU(ptr::null_mut()));
            if menu.is_invalid() {
                tracing::error!("CreatePopupMenu failed");
                return;
            }

            let _ = AppendMenuW(menu, MF_STRING, IDM_TOGGLE as usize, w!("Toggle Snapping"));
            let _ = AppendMenuW(menu, MF_STRING, IDM_RELOAD as usize, w!("Reload Config"));
            let _ = AppendMenuW(menu, MF_STRING, IDM_EXIT as usize, w!("Exit"));

            // Required so the menu disappears when the user clicks elsewhere.
            let _ = SetForegroundWindow(self.hwnd);

            let mut pt = POINT::default();
            let _ = GetCursorPos(&mut pt);

            let _ = TrackPopupMenu(menu, TPM_LEFTALIGN | TPM_BOTTOMALIGN, pt.x, pt.y, 0, self.hwnd, None);

            let _ = DestroyMenu(menu);
        }
    }

    /// Update the tray icon to reflect the current enabled/disabled state.
    ///
    /// Changes the icon and tooltip text.
    pub fn set_enabled(&mut self, enabled: bool) {
        self.nid.hIcon = if enabled {
            self.icon_enabled
        } else {
            self.icon_disabled
        };

        let tip = if enabled {
            "MagicZones — Enabled"
        } else {
            "MagicZones — Disabled"
        };
        set_tooltip(&mut self.nid, tip);

        self.nid.uFlags = NIF_ICON | NIF_TIP;

        // SAFETY: nid is fully initialised with updated fields.
        unsafe {
            let _ = Shell_NotifyIconW(NIM_MODIFY, &self.nid);
        }
    }

    /// Show a balloon notification from the tray icon.
    ///
    /// Useful for transient status messages (e.g. "Config reloaded").
    pub fn show_balloon(&self, title: &str, message: &str) {
        let mut nid = self.nid;
        nid.uFlags = NIF_INFO;

        // Copy title into szInfoTitle (max 63 chars + null).
        let title_wide: Vec<u16> = title.encode_utf16().chain(std::iter::once(0)).collect();
        let title_len = title_wide.len().min(nid.szInfoTitle.len());
        nid.szInfoTitle[..title_len].copy_from_slice(&title_wide[..title_len]);

        // Copy message into szInfo (max 255 chars + null).
        let msg_wide: Vec<u16> = message.encode_utf16().chain(std::iter::once(0)).collect();
        let msg_len = msg_wide.len().min(nid.szInfo.len());
        nid.szInfo[..msg_len].copy_from_slice(&msg_wide[..msg_len]);

        // SAFETY: nid is a copy with updated fields.
        unsafe {
            let _ = Shell_NotifyIconW(NIM_MODIFY, &nid);
        }
    }

    /// Process a tray callback message (`WM_TRAYICON`).
    ///
    /// Returns a [`TrayCommand`] if the user interaction maps to one:
    /// - Right-click → show context menu (handled internally, returns `None`)
    /// - Double-click → `ToggleSnapping`
    pub fn process_tray_message(&self, lparam: LPARAM) -> Option<TrayCommand> {
        let msg = (lparam.0 & 0xFFFF) as u32;
        match msg {
            WM_RBUTTONUP => {
                self.show_context_menu();
                None
            }
            WM_LBUTTONDBLCLK => Some(TrayCommand::ToggleSnapping),
            _ => None,
        }
    }

    /// Process a `WM_COMMAND` message and return the corresponding tray command.
    ///
    /// The `wparam` low word is the menu item ID selected by the user.
    pub fn process_menu_command(wparam: WPARAM) -> Option<TrayCommand> {
        let id = (wparam.0 & 0xFFFF) as u32;
        match id {
            IDM_TOGGLE => Some(TrayCommand::ToggleSnapping),
            IDM_RELOAD => Some(TrayCommand::ReloadConfig),
            IDM_EXIT => Some(TrayCommand::Exit),
            _ => None,
        }
    }
}

impl Drop for TrayManager {
    fn drop(&mut self) {
        // Remove the tray icon.
        // SAFETY: nid identifies the icon to remove.
        unsafe {
            let _ = Shell_NotifyIconW(NIM_DELETE, &self.nid);
            let _ = DestroyIcon(self.icon_enabled);
            let _ = DestroyIcon(self.icon_disabled);
        }
        tracing::info!("Tray icon removed");
    }
}

/// Set the tooltip text on a `NOTIFYICONDATAW` structure.
///
/// Truncates to 127 characters (the Win32 limit for `szTip`).
fn set_tooltip(nid: &mut NOTIFYICONDATAW, text: &str) {
    let wide: Vec<u16> = text.encode_utf16().chain(std::iter::once(0)).collect();
    let len = wide.len().min(nid.szTip.len());
    nid.szTip[..len].copy_from_slice(&wide[..len]);
}

/// Create a simple 16×16 solid-colour icon.
///
/// Used to generate the enabled (green) and disabled (red) tray icons without
/// needing external resource files.
///
/// # Errors
///
/// Returns an error if any GDI call fails.
fn create_color_icon(r: u8, g: u8, b: u8) -> Result<HICON, String> {
    const SIZE: i32 = 16;

    unsafe {
        let screen_dc = windows::Win32::Graphics::Gdi::GetDC(None);
        let mem_dc = CreateCompatibleDC(screen_dc);

        let bmi = BITMAPINFO {
            bmiHeader: BITMAPINFOHEADER {
                biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
                biWidth: SIZE,
                biHeight: SIZE, // bottom-up for icon
                biPlanes: 1,
                biBitCount: 32,
                biCompression: BI_RGB.0,
                ..Default::default()
            },
            ..Default::default()
        };

        // Color bitmap.
        let mut color_bits: *mut std::ffi::c_void = ptr::null_mut();
        let color_bmp = CreateDIBSection(
            mem_dc,
            &bmi,
            DIB_RGB_COLORS,
            &mut color_bits,
            None,
            0,
        )
        .map_err(|e| format!("CreateDIBSection (color): {e}"))?;

        if color_bits.is_null() {
            return Err("CreateDIBSection returned null bits".to_string());
        }

        // Fill with the specified colour (BGRA format).
        let pixels = std::slice::from_raw_parts_mut(
            color_bits as *mut u32,
            (SIZE * SIZE) as usize,
        );
        let bgra = 0xFF000000 | ((r as u32) << 16) | ((g as u32) << 8) | (b as u32);
        pixels.fill(bgra);

        // Mask bitmap (all zeros = fully opaque).
        let mut mask_bits: *mut std::ffi::c_void = ptr::null_mut();
        let mask_bmi = BITMAPINFO {
            bmiHeader: BITMAPINFOHEADER {
                biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
                biWidth: SIZE,
                biHeight: SIZE,
                biPlanes: 1,
                biBitCount: 32,
                biCompression: BI_RGB.0,
                ..Default::default()
            },
            ..Default::default()
        };
        let mask_bmp = CreateDIBSection(
            mem_dc,
            &mask_bmi,
            DIB_RGB_COLORS,
            &mut mask_bits,
            None,
            0,
        )
        .map_err(|e| format!("CreateDIBSection (mask): {e}"))?;

        // Zero-fill mask = fully opaque icon.
        if !mask_bits.is_null() {
            let mask_pixels = std::slice::from_raw_parts_mut(
                mask_bits as *mut u8,
                (SIZE * SIZE * 4) as usize,
            );
            mask_pixels.fill(0);
        }

        let icon_info = ICONINFO {
            fIcon: true.into(),
            xHotspot: 0,
            yHotspot: 0,
            hbmMask: mask_bmp,
            hbmColor: color_bmp,
        };

        let icon = CreateIconIndirect(&icon_info)
            .map_err(|e| format!("CreateIconIndirect: {e}"))?;

        // Clean up temporary bitmaps and DCs.
        let _ = DeleteObject(color_bmp);
        let _ = DeleteObject(mask_bmp);
        let _ = DeleteDC(mem_dc);
        windows::Win32::Graphics::Gdi::ReleaseDC(None, screen_dc);

        Ok(icon)
    }
}
