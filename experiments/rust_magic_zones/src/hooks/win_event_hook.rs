/// Installs a Win32 `SetWinEventHook` to detect window move start/end events.
///
/// # Architecture
///
/// Win32 event hooks require a callback function with a C ABI signature, so we
/// cannot capture a `Sender` in a closure. Instead we store the sender in a
/// global [`OnceLock`] and read it from the callback.
///
/// The hook is installed with `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`
/// so the callback runs on the installing thread's message pump (no DLL injection)
/// and we don't receive events for our own windows.
///
/// # RAII
///
/// [`WinEventHook`] is an RAII guard: dropping it calls `UnhookWinEvent`.
///
/// # Panics / Safety
///
/// The callback is wrapped in [`std::panic::catch_unwind`] so a panic in the
/// channel send path will not unwind across the FFI boundary.

use std::sync::OnceLock;

use crossbeam_channel::Sender;

use crate::hooks::WinEventMsg;

use windows::Win32::UI::Accessibility::{SetWinEventHook, UnhookWinEvent, HWINEVENTHOOK};
use windows::Win32::UI::WindowsAndMessaging::{
    EVENT_SYSTEM_MOVESIZEEND, EVENT_SYSTEM_MOVESIZESTART, WINEVENT_OUTOFCONTEXT,
    WINEVENT_SKIPOWNPROCESS,
};
use windows::Win32::Foundation::HWND;

/// Global sender for the win-event callback. Set once during [`WinEventHook::install`].
static WIN_EVENT_TX: OnceLock<Sender<WinEventMsg>> = OnceLock::new();

/// RAII guard for a Win32 `SetWinEventHook` registration.
///
/// When dropped, the hook is uninstalled via `UnhookWinEvent`.
pub struct WinEventHook {
    hook: HWINEVENTHOOK,
}

impl WinEventHook {
    /// Install the hook and begin sending [`WinEventMsg`] events to `sender`.
    ///
    /// # Errors
    ///
    /// Returns an error if:
    /// - The global sender has already been set (double-install).
    /// - `SetWinEventHook` returns a null handle.
    pub fn install(sender: Sender<WinEventMsg>) -> Result<Self, String> {
        WIN_EVENT_TX
            .set(sender)
            .map_err(|_| "WinEventHook: global sender already initialised".to_string())?;

        // SAFETY: We pass a valid function pointer with the required ABI.
        // WINEVENT_OUTOFCONTEXT means no DLL injection; callback runs on our thread.
        let hook = unsafe {
            SetWinEventHook(
                EVENT_SYSTEM_MOVESIZESTART,
                EVENT_SYSTEM_MOVESIZEEND,
                None, // no DLL
                Some(win_event_proc),
                0, // all processes
                0, // all threads
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS,
            )
        };

        if hook.is_invalid() {
            return Err("SetWinEventHook returned an invalid handle".to_string());
        }

        tracing::info!("WinEventHook installed");
        Ok(Self { hook })
    }
}

impl Drop for WinEventHook {
    fn drop(&mut self) {
        // SAFETY: `self.hook` was returned by a successful `SetWinEventHook` call.
        let ok = unsafe { UnhookWinEvent(self.hook) };
        if ok.as_bool() {
            tracing::info!("WinEventHook uninstalled");
        } else {
            tracing::warn!("UnhookWinEvent failed");
        }
    }
}

/// Raw Win32 callback invoked by the OS on the hook-installing thread.
///
/// Wrapped in `catch_unwind` so a panic cannot unwind across the FFI boundary.
///
/// # Parameters (per Win32 docs)
///
/// - `_hook`: Handle to the event hook (unused).
/// - `event`: The event constant (`EVENT_SYSTEM_MOVESIZESTART` or `…END`).
/// - `hwnd`: Window handle the event relates to.
/// - remaining parameters are unused positional arguments required by the ABI.
extern "system" fn win_event_proc(
    _hook: HWINEVENTHOOK,
    event: u32,
    hwnd: HWND,
    _id_object: i32,
    _id_child: i32,
    _id_event_thread: u32,
    _event_time: u32,
) {
    let _ = std::panic::catch_unwind(|| {
        let msg = match event {
            x if x == EVENT_SYSTEM_MOVESIZESTART => WinEventMsg::MoveStarted(hwnd.0 as isize),
            x if x == EVENT_SYSTEM_MOVESIZEEND => WinEventMsg::MoveEnded(hwnd.0 as isize),
            _ => return,
        };

        if let Some(tx) = WIN_EVENT_TX.get() {
            let _ = tx.send(msg);
        }
    });
}
