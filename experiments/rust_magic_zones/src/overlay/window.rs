/// Creates and manages the transparent overlay window used to highlight zones
/// during a window drag.
///
/// # Architecture
///
/// The overlay is a full-screen layered window (`WS_EX_LAYERED`) with per-pixel
/// alpha. It sits topmost but is click-through (`WS_EX_TRANSPARENT`) and will
/// not steal focus (`WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`).
///
/// Zone rendering uses a memory DC with a 32bpp ARGB DIB section. Each zone is
/// drawn as a semi-transparent filled rectangle with a 2px border and centred
/// label text (Segoe UI 12pt). The final bitmap is applied to the window with
/// `UpdateLayeredWindow` using `AC_SRC_OVER` + `AC_SRC_ALPHA`.
///
/// # RAII
///
/// Dropping [`OverlayWindow`] calls `DestroyWindow` to clean up the window and
/// its window class.
///
/// # GDI Resource Management
///
/// All intermediate GDI objects (DCs, bitmaps, fonts, brushes, pens) are deleted
/// after each `show_zones` call to avoid leaks.


use crate::engine::types::ZoneRenderInfo;

use windows::core::PCWSTR;
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, RECT, WPARAM};
use windows::Win32::Graphics::Gdi::{
    CreateCompatibleDC, CreateDIBSection, CreateFontW, DeleteDC, DeleteObject, DrawTextW, GetDC,
    ReleaseDC, SelectObject, SetBkMode, SetTextColor, AC_SRC_ALPHA, AC_SRC_OVER, BITMAPINFO,
    BITMAPINFOHEADER, BI_RGB, BLENDFUNCTION, DIB_RGB_COLORS, DT_CENTER, DT_NOPREFIX,
    DT_SINGLELINE, DT_VCENTER, HBITMAP, TRANSPARENT,
};
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DestroyWindow, GetSystemMetrics, RegisterClassExW,
    SetWindowPos, ShowWindow, UpdateLayeredWindow, CS_HREDRAW, CS_VREDRAW, HWND_TOPMOST,
    SM_CXVIRTUALSCREEN, SM_CYVIRTUALSCREEN, SM_XVIRTUALSCREEN, SM_YVIRTUALSCREEN,
    SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSIZE, SW_HIDE, SW_SHOWNOACTIVATE, ULW_ALPHA,
    WNDCLASSEXW, WS_EX_LAYERED, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW,
    WS_EX_TOPMOST, WS_EX_TRANSPARENT, WS_POPUP,
};

/// Name of the window class registered for the overlay.
const CLASS_NAME: &str = "MagicZonesOverlay";

/// Default inactive zone colour (blue, semi-transparent). ARGB premultiplied.
const DEFAULT_INACTIVE_COLOR: u32 = 0x40_40_60_FF; // ARGB
/// Default active zone colour (green, semi-transparent). ARGB premultiplied.
const DEFAULT_ACTIVE_COLOR: u32 = 0x60_20_A0_40; // ARGB
/// Default border colour (white, opaque).
const DEFAULT_BORDER_COLOR: u32 = 0xFF_FF_FF_FF;

fn argb_to_pma(argb: u32) -> (u8, u8, u8, u8) {
    let a = ((argb >> 24) & 0xFF) as u8;
    let r = ((argb >> 16) & 0xFF) as u8;
    let g = ((argb >> 8) & 0xFF) as u8;
    let b = (argb & 0xFF) as u8;
    // Premultiply
    let pma = |c: u8| -> u8 { ((c as u16 * a as u16) / 255) as u8 };
    (pma(r), pma(g), pma(b), a)
}

/// A transparent overlay window that shows zone boundaries during a drag.
///
/// The window covers the entire virtual screen (all monitors) and uses
/// per-pixel alpha to draw semi-transparent zone rectangles.
pub struct OverlayWindow {
    hwnd: HWND,
    active_color: u32,
    inactive_color: u32,
    border_color: u32,
}

impl OverlayWindow {
    /// Create a new overlay window covering the entire virtual screen.
    ///
    /// The window is initially hidden. Call [`show_zones`](Self::show_zones)
    /// to paint zones and make it visible.
    ///
    /// # Errors
    ///
    /// Returns an error if window class registration or window creation fails.
    pub fn create() -> Result<Self, String> {
        // Register the window class (idempotent — second call returns the existing atom).
        let class_name = to_wide(CLASS_NAME);

        let wc = WNDCLASSEXW {
            cbSize: std::mem::size_of::<WNDCLASSEXW>() as u32,
            style: CS_HREDRAW | CS_VREDRAW,
            lpfnWndProc: Some(overlay_wnd_proc),
            lpszClassName: PCWSTR(class_name.as_ptr()),
            ..Default::default()
        };

        // SAFETY: Standard Win32 class registration. If the class already exists
        // we ignore the error and proceed — CreateWindowExW will still find it.
        unsafe {
            RegisterClassExW(&wc);
        }

        // Virtual screen dimensions (spans all monitors).
        let (vx, vy, vw, vh) = unsafe {
            (
                GetSystemMetrics(SM_XVIRTUALSCREEN),
                GetSystemMetrics(SM_YVIRTUALSCREEN),
                GetSystemMetrics(SM_CXVIRTUALSCREEN),
                GetSystemMetrics(SM_CYVIRTUALSCREEN),
            )
        };

        let ex_style = WS_EX_LAYERED
            | WS_EX_TRANSPARENT
            | WS_EX_TOOLWINDOW
            | WS_EX_NOACTIVATE
            | WS_EX_TOPMOST;

        // SAFETY: Standard Win32 window creation. The class was registered above.
        let hwnd = unsafe {
            CreateWindowExW(
                ex_style,
                PCWSTR(class_name.as_ptr()),
                PCWSTR::null(),
                WS_POPUP,
                vx,
                vy,
                vw,
                vh,
                None,
                None,
                None,
                None,
            )
            .map_err(|e| format!("CreateWindowExW failed: {e}"))?
        };

        tracing::info!(
            "Overlay window created: hwnd={:?}, virtual screen=({},{} {}x{})",
            hwnd,
            vx,
            vy,
            vw,
            vh
        );

        Ok(Self {
            hwnd,
            active_color: DEFAULT_ACTIVE_COLOR,
            inactive_color: DEFAULT_INACTIVE_COLOR,
            border_color: DEFAULT_BORDER_COLOR,
        })
    }

    /// Set the colours used for zone rendering.
    ///
    /// Colours are in ARGB format (e.g. `0x80_00_FF_00` = 50% green).
    pub fn set_colors(&mut self, active: u32, inactive: u32, border: u32) {
        self.active_color = active;
        self.inactive_color = inactive;
        self.border_color = border;
    }

    /// Show the overlay and paint the given zones.
    ///
    /// `active_index` is the index of the hovered zone (if any), which will be
    /// drawn with the active colour.
    ///
    /// # GDI Pipeline
    ///
    /// 1. Create a memory DC and a 32bpp ARGB DIB section.
    /// 2. Fill each zone rectangle with premultiplied-alpha pixels (active or
    ///    inactive colour).
    /// 3. Draw a 2px border around each zone.
    /// 4. Render zone name text with `DrawTextW` (Segoe UI 12pt, centred).
    /// 5. Apply the bitmap to the layered window with `UpdateLayeredWindow`.
    /// 6. Make the window visible and ensure it is topmost.
    /// 7. Clean up all intermediate GDI resources.
    pub fn show_zones(&self, zones: &[ZoneRenderInfo], active_index: Option<usize>) {
        // SAFETY: all GDI calls below follow standard Win32 patterns and all
        // handles are cleaned up at the end of the function.
        unsafe {
            let (vx, vy, vw, vh) = (
                GetSystemMetrics(SM_XVIRTUALSCREEN),
                GetSystemMetrics(SM_YVIRTUALSCREEN),
                GetSystemMetrics(SM_CXVIRTUALSCREEN),
                GetSystemMetrics(SM_CYVIRTUALSCREEN),
            );

            let screen_dc = GetDC(None);
            let mem_dc = CreateCompatibleDC(screen_dc);

            // Create a 32bpp top-down DIB for premultiplied-alpha rendering.
            let bmi = BITMAPINFO {
                bmiHeader: BITMAPINFOHEADER {
                    biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
                    biWidth: vw,
                    biHeight: -vh, // top-down
                    biPlanes: 1,
                    biBitCount: 32,
                    biCompression: BI_RGB.0,
                    ..Default::default()
                },
                ..Default::default()
            };

            let mut bits_ptr: *mut std::ffi::c_void = std::ptr::null_mut();
            let dib = CreateDIBSection(
                mem_dc,
                &bmi,
                DIB_RGB_COLORS,
                &mut bits_ptr,
                None,
                0,
            )
            .unwrap_or(HBITMAP(std::ptr::null_mut()));

            if dib.is_invalid() || bits_ptr.is_null() {
                tracing::error!("CreateDIBSection failed");
                let _ = DeleteDC(mem_dc);
                let _ = ReleaseDC(None, screen_dc);
                return;
            }

            let old_bmp = SelectObject(mem_dc, dib);
            let pixel_count = (vw * vh) as usize;
            let pixels: &mut [u32] =
                std::slice::from_raw_parts_mut(bits_ptr as *mut u32, pixel_count);

            // Clear to fully transparent.
            pixels.fill(0);

            // Fill zone rectangles with premultiplied alpha pixels.
            for (i, zone) in zones.iter().enumerate() {
                let color = if active_index == Some(i) {
                    self.active_color
                } else {
                    self.inactive_color
                };
                let (pr, pg, pb, pa) = argb_to_pma(color);
                let pixel = (pa as u32) << 24 | (pr as u32) << 16 | (pg as u32) << 8 | pb as u32;

                let b = &zone.bounds;
                // Convert zone coords (absolute screen) to bitmap coords (relative to virtual screen origin).
                let x0 = (b.x - vx).max(0) as usize;
                let y0 = (b.y - vy).max(0) as usize;
                let x1 = ((b.x + b.width - vx) as usize).min(vw as usize);
                let y1 = ((b.y + b.height - vy) as usize).min(vh as usize);

                for y in y0..y1 {
                    let row_offset = y * vw as usize;
                    for x in x0..x1 {
                        pixels[row_offset + x] = pixel;
                    }
                }

                // 2px border (fully opaque, premultiplied).
                let (br, bg, bb, ba) = argb_to_pma(self.border_color);
                let border_pixel =
                    (ba as u32) << 24 | (br as u32) << 16 | (bg as u32) << 8 | bb as u32;
                let bw = 2usize;

                // Top and bottom borders.
                for dy in 0..bw {
                    if y0 + dy < vh as usize {
                        let row = (y0 + dy) * vw as usize;
                        for x in x0..x1 {
                            pixels[row + x] = border_pixel;
                        }
                    }
                    if y1 >= bw && (y1 - bw + dy) < vh as usize {
                        let row = (y1 - bw + dy) * vw as usize;
                        for x in x0..x1 {
                            pixels[row + x] = border_pixel;
                        }
                    }
                }
                // Left and right borders.
                for y in y0..y1 {
                    let row = y * vw as usize;
                    for dx in 0..bw {
                        if x0 + dx < vw as usize {
                            pixels[row + x0 + dx] = border_pixel;
                        }
                        if x1 >= bw && (x1 - bw + dx) < vw as usize {
                            pixels[row + x1 - bw + dx] = border_pixel;
                        }
                    }
                }
            }

            // Draw zone name text using GDI.
            let font_name = to_wide("Segoe UI");
            let font = CreateFontW(
                -16, // 12pt at 96 DPI (negative = character height)
                0,
                0,
                0,
                400, // FW_NORMAL
                0,
                0,
                0,
                0,        // DEFAULT_CHARSET
                0,        // OUT_DEFAULT_PRECIS
                0,        // CLIP_DEFAULT_PRECIS
                4,        // CLEARTYPE_QUALITY
                0,        // DEFAULT_PITCH
                PCWSTR(font_name.as_ptr()),
            );

            let old_font = SelectObject(mem_dc, font);
            SetBkMode(mem_dc, TRANSPARENT);
            SetTextColor(mem_dc, windows::Win32::Foundation::COLORREF(0x00FFFFFF)); // white text

            for zone in zones {
                let b = &zone.bounds;
                let mut rc = RECT {
                    left: b.x - vx,
                    top: b.y - vy,
                    right: b.x + b.width - vx,
                    bottom: b.y + b.height - vy,
                };
                let name_wide: Vec<u16> = zone.name.encode_utf16().collect();
                DrawTextW(
                    mem_dc,
                    &mut name_wide.clone(),
                    &mut rc,
                    DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX,
                );
            }

            SelectObject(mem_dc, old_font);
            let _ = DeleteObject(font);

            // Apply to layered window.
            let pt_src = windows::Win32::Foundation::POINT { x: 0, y: 0 };
            let pt_dst = windows::Win32::Foundation::POINT { x: vx, y: vy };
            let size = windows::Win32::Foundation::SIZE {
                cx: vw,
                cy: vh,
            };
            let blend = BLENDFUNCTION {
                BlendOp: AC_SRC_OVER as u8,
                BlendFlags: 0,
                SourceConstantAlpha: 255,
                AlphaFormat: AC_SRC_ALPHA as u8,
            };

            let _ = UpdateLayeredWindow(
                self.hwnd,
                screen_dc,
                Some(&pt_dst),
                Some(&size),
                mem_dc,
                Some(&pt_src),
                windows::Win32::Foundation::COLORREF(0),
                Some(&blend),
                ULW_ALPHA,
            );

            // Show window and ensure topmost.
            let _ = ShowWindow(self.hwnd, SW_SHOWNOACTIVATE);
            let _ = SetWindowPos(
                self.hwnd,
                HWND_TOPMOST,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE,
            );

            // Clean up GDI resources.
            SelectObject(mem_dc, old_bmp);
            let _ = DeleteObject(dib);
            let _ = DeleteDC(mem_dc);
            let _ = ReleaseDC(None, screen_dc);
        }
    }

    /// Hide the overlay window.
    pub fn hide(&self) {
        // SAFETY: self.hwnd is a valid window handle.
        unsafe {
            let _ = ShowWindow(self.hwnd, SW_HIDE);
        }
    }
}

impl Drop for OverlayWindow {
    fn drop(&mut self) {
        // SAFETY: self.hwnd was created by CreateWindowExW in `create()`.
        unsafe {
            let _ = DestroyWindow(self.hwnd);
        }
        tracing::info!("Overlay window destroyed");
    }
}

/// Window procedure for the overlay — simply delegates to `DefWindowProcW`.
///
/// The overlay is completely passive (click-through, no activation), so it
/// has no custom message handling.
extern "system" fn overlay_wnd_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    unsafe { DefWindowProcW(hwnd, msg, wparam, lparam) }
}

/// Convert a Rust string slice to a null-terminated wide (UTF-16) string.
fn to_wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}
