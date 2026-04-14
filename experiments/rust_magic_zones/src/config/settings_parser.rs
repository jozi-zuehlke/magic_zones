/// Parses human-readable setting strings (hotkeys, colors, modifier names) into
/// structured representations used by the engine.

/// Supported modifier keys for zone activation.
#[cfg(test)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum ModifierKey {
    Shift,
    Control,
    Alt,
}

/// Log verbosity level.
#[cfg(test)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum LogLevel {
    Trace,
    Debug,
    Info,
    Warn,
    Error,
}

/// Win32-compatible hotkey binding with modifier bitmask and virtual-key code.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct HotkeyBinding {
    pub modifiers: u32,
    pub vk: u32,
}

/// Parse a modifier name string (e.g. `"Shift"`) into a [`ModifierKey`].
#[cfg(test)]
pub fn parse_modifier(s: &str) -> Option<ModifierKey> {
    match s.to_lowercase().as_str() {
        "shift" => Some(ModifierKey::Shift),
        "control" | "ctrl" => Some(ModifierKey::Control),
        "alt" => Some(ModifierKey::Alt),
        _ => None,
    }
}

/// Parsed representation of a hotkey string like `"Ctrl+Win+Z"`.
#[cfg(test)]
#[derive(Debug, Clone, PartialEq)]
pub struct ParsedHotkey {
    pub modifiers: Vec<String>,
    pub key: String,
}

/// Parse a hotkey string (e.g. `"Ctrl+Win+Z"`) into its components.
#[cfg(test)]
pub fn parse_hotkey(s: &str) -> Option<ParsedHotkey> {
    let parts: Vec<&str> = s.split('+').map(str::trim).collect();
    if parts.len() < 2 {
        return None;
    }
    let key = parts.last()?.to_string();
    let modifiers = parts[..parts.len() - 1]
        .iter()
        .map(|s| s.to_string())
        .collect();
    Some(ParsedHotkey { modifiers, key })
}

/// Parse a log level string (case-insensitive) into a [`LogLevel`].
#[cfg(test)]
pub fn parse_log_level(s: &str) -> Option<LogLevel> {
    match s.to_lowercase().as_str() {
        "trace" => Some(LogLevel::Trace),
        "debug" => Some(LogLevel::Debug),
        "info" => Some(LogLevel::Info),
        "warn" => Some(LogLevel::Warn),
        "error" => Some(LogLevel::Error),
        _ => None,
    }
}

const MOD_ALT: u32 = 0x0001;
const MOD_CONTROL: u32 = 0x0002;
const MOD_SHIFT: u32 = 0x0004;
const MOD_WIN: u32 = 0x0008;

/// Parse a hotkey string (e.g. `"Ctrl+Win+Z"`) into a Win32-compatible [`HotkeyBinding`].
///
/// At least one modifier is required; the last `+`-separated part is the key.
pub fn parse_hotkey_binding(s: &str) -> Option<HotkeyBinding> {
    let parts: Vec<&str> = s.split('+').map(str::trim).collect();
    if parts.len() < 2 {
        return None;
    }
    let (modifier_parts, key_part) = parts.split_at(parts.len() - 1);
    let key_part = key_part[0];

    let mut modifiers: u32 = 0;
    for m in modifier_parts {
        match m.to_lowercase().as_str() {
            "ctrl" | "control" => modifiers |= MOD_CONTROL,
            "alt" => modifiers |= MOD_ALT,
            "shift" => modifiers |= MOD_SHIFT,
            "win" | "windows" => modifiers |= MOD_WIN,
            _ => return None,
        }
    }

    let vk = parse_vk(key_part)?;
    Some(HotkeyBinding { modifiers, vk })
}

fn parse_vk(key: &str) -> Option<u32> {
    let upper = key.to_uppercase();
    // Single letter A-Z
    if upper.len() == 1 {
        let ch = upper.as_bytes()[0];
        if ch.is_ascii_uppercase() {
            return Some(ch as u32); // 0x41-0x5A
        }
    }
    // Single digit 0-9
    if key.len() == 1 {
        let ch = key.as_bytes()[0];
        if ch.is_ascii_digit() {
            return Some(ch as u32); // 0x30-0x39
        }
    }
    // F-keys F1-F12
    if upper.starts_with('F') {
        if let Ok(n) = upper[1..].parse::<u32>() {
            if (1..=12).contains(&n) {
                return Some(0x70 + n - 1); // VK_F1=0x70
            }
        }
    }
    None
}

/// Map a modifier name to its Win32 left-variant virtual-key code.
#[cfg(test)]
pub fn parse_modifier_vk(s: &str) -> Option<u16> {
    match s.to_lowercase().as_str() {
        "shift" => Some(0xA0),    // VK_LSHIFT
        "ctrl" | "control" => Some(0xA2), // VK_LCONTROL
        "alt" => Some(0xA4),      // VK_LMENU
        _ => None,
    }
}

/// Parse a CSS-style hex colour string (e.g. `"#0078D4"`) into `(r, g, b)`.
pub fn parse_color(s: &str) -> Option<(u8, u8, u8)> {
    let s = s.strip_prefix('#')?;
    if s.len() != 6 {
        return None;
    }
    let r = u8::from_str_radix(&s[0..2], 16).ok()?;
    let g = u8::from_str_radix(&s[2..4], 16).ok()?;
    let b = u8::from_str_radix(&s[4..6], 16).ok()?;
    Some((r, g, b))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_modifier_shift() {
        assert_eq!(parse_modifier("Shift"), Some(ModifierKey::Shift));
        assert_eq!(parse_modifier("shift"), Some(ModifierKey::Shift));
    }

    #[test]
    fn parse_modifier_control_variants() {
        assert_eq!(parse_modifier("Control"), Some(ModifierKey::Control));
        assert_eq!(parse_modifier("Ctrl"), Some(ModifierKey::Control));
    }

    #[test]
    fn parse_modifier_unknown_returns_none() {
        assert_eq!(parse_modifier("Super"), None);
    }

    #[test]
    fn parse_hotkey_valid() {
        let hk = parse_hotkey("Ctrl+Win+Z").unwrap();
        assert_eq!(hk.modifiers, vec!["Ctrl", "Win"]);
        assert_eq!(hk.key, "Z");
    }

    #[test]
    fn parse_hotkey_single_key_returns_none() {
        assert!(parse_hotkey("Z").is_none());
    }

    #[test]
    fn parse_color_valid() {
        assert_eq!(parse_color("#0078D4"), Some((0x00, 0x78, 0xD4)));
        assert_eq!(parse_color("#FFFFFF"), Some((0xFF, 0xFF, 0xFF)));
    }

    #[test]
    fn parse_color_invalid() {
        assert!(parse_color("0078D4").is_none()); // no #
        assert!(parse_color("#FFF").is_none()); // too short
    }

    // --- LogLevel tests ---

    #[test]
    fn parse_log_level_valid() {
        assert_eq!(parse_log_level("info"), Some(LogLevel::Info));
        assert_eq!(parse_log_level("DEBUG"), Some(LogLevel::Debug));
        assert_eq!(parse_log_level("Warn"), Some(LogLevel::Warn));
        assert_eq!(parse_log_level("trace"), Some(LogLevel::Trace));
        assert_eq!(parse_log_level("ERROR"), Some(LogLevel::Error));
    }

    #[test]
    fn parse_log_level_invalid() {
        assert_eq!(parse_log_level("verbose"), None);
        assert_eq!(parse_log_level(""), None);
    }

    // --- HotkeyBinding tests ---

    #[test]
    fn parse_hotkey_binding_ctrl_win_z() {
        assert_eq!(
            parse_hotkey_binding("Ctrl+Win+Z"),
            Some(HotkeyBinding { modifiers: 0x000A, vk: 0x5A })
        );
    }

    #[test]
    fn parse_hotkey_binding_shift_f1() {
        assert_eq!(
            parse_hotkey_binding("Shift+F1"),
            Some(HotkeyBinding { modifiers: 0x0004, vk: 0x70 })
        );
    }

    #[test]
    fn parse_hotkey_binding_alt_1() {
        assert_eq!(
            parse_hotkey_binding("Alt+1"),
            Some(HotkeyBinding { modifiers: 0x0001, vk: 0x31 })
        );
    }

    #[test]
    fn parse_hotkey_binding_single_key_returns_none() {
        assert!(parse_hotkey_binding("Z").is_none());
    }

    #[test]
    fn parse_hotkey_binding_unknown_key_returns_none() {
        assert!(parse_hotkey_binding("Ctrl+PageUp").is_none());
    }

    #[test]
    fn parse_hotkey_binding_case_insensitive() {
        assert_eq!(
            parse_hotkey_binding("ctrl+win+z"),
            parse_hotkey_binding("Ctrl+Win+Z")
        );
    }

    // --- ModifierVK tests ---

    #[test]
    fn parse_modifier_vk_shift() {
        assert_eq!(parse_modifier_vk("Shift"), Some(0xA0));
    }

    #[test]
    fn parse_modifier_vk_ctrl() {
        assert_eq!(parse_modifier_vk("Ctrl"), Some(0xA2));
        assert_eq!(parse_modifier_vk("Control"), Some(0xA2));
    }

    #[test]
    fn parse_modifier_vk_alt() {
        assert_eq!(parse_modifier_vk("Alt"), Some(0xA4));
    }

    #[test]
    fn parse_modifier_vk_unknown() {
        assert_eq!(parse_modifier_vk("Win"), None);
        assert_eq!(parse_modifier_vk("Super"), None);
    }
}
