/// Tracks the state of the activation modifier key (e.g. Shift).
///
/// The engine only activates zone snapping when the modifier is held during a drag.

use std::sync::Arc;

use crate::platform::KeyboardState;

/// Virtual-key code for Left Shift.
const VK_LSHIFT: u16 = 0xA0;
/// Virtual-key code for Right Shift.
const VK_RSHIFT: u16 = 0xA1;
/// Virtual-key code for Left Control.
const VK_LCONTROL: u16 = 0xA2;
/// Virtual-key code for Right Control.
const VK_RCONTROL: u16 = 0xA3;
/// Virtual-key code for Left Alt.
const VK_LMENU: u16 = 0xA4;
/// Virtual-key code for Right Alt.
const VK_RMENU: u16 = 0xA5;

/// Which modifier key activates zone snapping.
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum ActivationModifier {
    Shift,
    Control,
    Alt,
}

/// Polls the keyboard to check whether the activation modifier is currently held.
pub struct ModifierKeyStateTracker {
    keyboard: Arc<dyn KeyboardState>,
    modifier: ActivationModifier,
}

impl ModifierKeyStateTracker {
    /// Create a tracker for the given modifier key.
    pub fn new(keyboard: Arc<dyn KeyboardState>, modifier: ActivationModifier) -> Self {
        Self { keyboard, modifier }
    }

    /// Returns `true` if the configured modifier is currently held down.
    pub fn is_modifier_active(&self) -> bool {
        match self.modifier {
            ActivationModifier::Shift => {
                self.keyboard.is_key_pressed(VK_LSHIFT)
                    || self.keyboard.is_key_pressed(VK_RSHIFT)
            }
            ActivationModifier::Control => {
                self.keyboard.is_key_pressed(VK_LCONTROL)
                    || self.keyboard.is_key_pressed(VK_RCONTROL)
            }
            ActivationModifier::Alt => {
                self.keyboard.is_key_pressed(VK_LMENU)
                    || self.keyboard.is_key_pressed(VK_RMENU)
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::platform::mock::MockKeyboardState;

    #[test]
    fn shift_not_active_by_default() {
        let kb = Arc::new(MockKeyboardState::new());
        let tracker = ModifierKeyStateTracker::new(kb, ActivationModifier::Shift);
        assert!(!tracker.is_modifier_active());
    }

    #[test]
    fn shift_active_when_left_shift_pressed() {
        let kb = Arc::new(MockKeyboardState::new());
        kb.pressed_keys.lock().insert(VK_LSHIFT);
        let tracker = ModifierKeyStateTracker::new(kb, ActivationModifier::Shift);
        assert!(tracker.is_modifier_active());
    }

    #[test]
    fn shift_active_when_right_shift_pressed() {
        let kb = Arc::new(MockKeyboardState::new());
        kb.pressed_keys.lock().insert(VK_RSHIFT);
        let tracker = ModifierKeyStateTracker::new(kb, ActivationModifier::Shift);
        assert!(tracker.is_modifier_active());
    }

    #[test]
    fn control_modifier_tracks_ctrl_keys() {
        let kb = Arc::new(MockKeyboardState::new());
        let tracker = ModifierKeyStateTracker::new(kb.clone(), ActivationModifier::Control);
        assert!(!tracker.is_modifier_active());

        kb.pressed_keys.lock().insert(VK_LCONTROL);
        assert!(tracker.is_modifier_active());
    }

    #[test]
    fn alt_modifier_tracks_alt_keys() {
        let kb = Arc::new(MockKeyboardState::new());
        let tracker = ModifierKeyStateTracker::new(kb.clone(), ActivationModifier::Alt);
        assert!(!tracker.is_modifier_active());

        kb.pressed_keys.lock().insert(VK_RMENU);
        assert!(tracker.is_modifier_active());
    }
}
