/// Hooks module — Win32 event hooks for window moves and keyboard input.
///
/// The hook installation code is Windows-only, but the message types are
/// cross-platform so the engine can be tested on any host.

#[cfg(windows)]
pub mod win_event_hook;
#[cfg(windows)]
pub mod keyboard_hook;
#[cfg(windows)]
pub mod hotkey_manager;

/// Message sent when a window move event is detected.
#[derive(Debug, Clone)]
pub enum WinEventMsg {
    /// A window has started being moved/resized.
    MoveStarted(isize),
    /// A window has finished being moved/resized.
    MoveEnded(isize),
}

/// Message sent when a keyboard event is detected.
#[derive(Debug, Clone)]
#[allow(dead_code)]
pub enum KeyboardMsg {
    /// A key was pressed (virtual-key code).
    KeyDown(u32),
    /// A key was released (virtual-key code).
    KeyUp(u32),
}
