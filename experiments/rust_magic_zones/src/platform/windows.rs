/// Real Win32 implementations of platform traits.
///
/// This module is only compiled on Windows (`#[cfg(windows)]`).

use windows::Win32::Foundation::{HWND, POINT, RECT};
use windows::Win32::Graphics::Dwm::{DwmGetWindowAttribute, DWMWA_EXTENDED_FRAME_BOUNDS};
use windows::Win32::Graphics::Gdi::{
    EnumDisplayMonitors, GetMonitorInfoW, HDC, HMONITOR, MONITORINFOEXW,
};
use windows::Win32::UI::Input::KeyboardAndMouse::GetAsyncKeyState;
use windows::Win32::UI::WindowsAndMessaging::{
    GetClassNameW, GetCursorPos, GetForegroundWindow, GetWindowLongW, GetWindowRect,
    GetWindowTextW, IsWindowVisible, IsZoomed, SetWindowPos, ShowWindow, SHOW_WINDOW_CMD,
    SET_WINDOW_POS_FLAGS, WINDOW_LONG_PTR_INDEX,
};

use crate::engine::types::Rect;
use crate::platform::{KeyboardState, MonitorInfo, MonitorProvider, WindowManager};

/// Production window manager backed by Win32 calls.
pub struct Win32WindowManager;

impl WindowManager for Win32WindowManager {
    fn set_window_pos(&self, hwnd: isize, x: i32, y: i32, width: i32, height: i32, flags: u32) {
        unsafe {
            let _ = SetWindowPos(
                HWND(hwnd as *mut _),
                HWND(std::ptr::null_mut()),
                x,
                y,
                width,
                height,
                SET_WINDOW_POS_FLAGS(flags),
            );
        }
    }

    fn get_window_rect(&self, hwnd: isize) -> Option<Rect> {
        unsafe {
            let mut rect = RECT::default();
            GetWindowRect(HWND(hwnd as *mut _), &mut rect).ok()?;
            Some(Rect::new(
                rect.left,
                rect.top,
                rect.right - rect.left,
                rect.bottom - rect.top,
            ))
        }
    }

    fn get_extended_frame_bounds(&self, hwnd: isize) -> Option<Rect> {
        unsafe {
            let mut rect = RECT::default();
            DwmGetWindowAttribute(
                HWND(hwnd as *mut _),
                DWMWA_EXTENDED_FRAME_BOUNDS,
                &mut rect as *mut _ as *mut _,
                std::mem::size_of::<RECT>() as u32,
            )
            .ok()?;
            Some(Rect::new(
                rect.left,
                rect.top,
                rect.right - rect.left,
                rect.bottom - rect.top,
            ))
        }
    }

    fn is_zoomed(&self, hwnd: isize) -> bool {
        unsafe { IsZoomed(HWND(hwnd as *mut _)).as_bool() }
    }

    fn show_window(&self, hwnd: isize, cmd: i32) {
        unsafe {
            let _ = ShowWindow(HWND(hwnd as *mut _), SHOW_WINDOW_CMD(cmd));
        }
    }

    fn get_cursor_pos(&self) -> Option<(i32, i32)> {
        unsafe {
            let mut pt = POINT::default();
            GetCursorPos(&mut pt).ok()?;
            Some((pt.x, pt.y))
        }
    }

    fn get_foreground_window(&self) -> isize {
        unsafe { GetForegroundWindow().0 as isize }
    }

    fn is_window_visible(&self, hwnd: isize) -> bool {
        unsafe { IsWindowVisible(HWND(hwnd as *mut _)).as_bool() }
    }

    fn get_window_long(&self, hwnd: isize, index: i32) -> i32 {
        unsafe { GetWindowLongW(HWND(hwnd as *mut _), WINDOW_LONG_PTR_INDEX(index)) }
    }

    fn get_class_name(&self, hwnd: isize) -> String {
        unsafe {
            let mut buf = [0u16; 256];
            let len = GetClassNameW(HWND(hwnd as *mut _), &mut buf);
            String::from_utf16_lossy(&buf[..len as usize])
        }
    }

    fn get_window_text(&self, hwnd: isize) -> String {
        unsafe {
            let mut buf = [0u16; 512];
            let len = GetWindowTextW(HWND(hwnd as *mut _), &mut buf);
            String::from_utf16_lossy(&buf[..len as usize])
        }
    }
}

/// Production keyboard state backed by Win32 calls.
pub struct Win32KeyboardState;

impl KeyboardState for Win32KeyboardState {
    fn is_key_pressed(&self, vk: u16) -> bool {
        unsafe { GetAsyncKeyState(vk as i32) < 0 }
    }

    fn get_async_key_state(&self, vk: i32) -> i16 {
        unsafe { GetAsyncKeyState(vk) }
    }
}

/// Production monitor provider backed by Win32 calls.
pub struct Win32MonitorProvider;

impl MonitorProvider for Win32MonitorProvider {
    fn get_monitor_info(&self) -> Vec<MonitorInfo> {
        let mut monitors: Vec<MonitorInfo> = Vec::new();
        unsafe {
            let _ = EnumDisplayMonitors(
                HDC(std::ptr::null_mut()),
                None,
                Some(enum_monitor_callback),
                windows::Win32::Foundation::LPARAM(&mut monitors as *mut _ as isize),
            );
        }
        monitors
    }
}

unsafe extern "system" fn enum_monitor_callback(
    hmon: HMONITOR,
    _hdc: HDC,
    _rect: *mut RECT,
    lparam: windows::Win32::Foundation::LPARAM,
) -> windows::Win32::Foundation::BOOL {
    let monitors = &mut *(lparam.0 as *mut Vec<MonitorInfo>);
    let mut info = MONITORINFOEXW::default();
    info.monitorInfo.cbSize = std::mem::size_of::<MONITORINFOEXW>() as u32;
    if GetMonitorInfoW(hmon, &mut info as *mut _ as *mut _).as_bool() {
        let wa = info.monitorInfo.rcWork;
        let name = String::from_utf16_lossy(
            &info
                .szDevice
                .iter()
                .take_while(|&&c| c != 0)
                .copied()
                .collect::<Vec<_>>(),
        );
        monitors.push(MonitorInfo {
            device_name: name,
            index: monitors.len() as u32,
            work_area: Rect::new(wa.left, wa.top, wa.right - wa.left, wa.bottom - wa.top),
        });
    }
    windows::Win32::Foundation::BOOL::from(true)
}
