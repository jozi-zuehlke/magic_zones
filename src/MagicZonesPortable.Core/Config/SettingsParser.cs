using System;
using System.Collections.Generic;
using System.Globalization;
using MagicZonesPortable.Core.Logging;

namespace MagicZonesPortable.Core.Config;

public record HotkeyBinding(uint Modifiers, uint VirtualKey);
public record ColorValue(byte R, byte G, byte B, byte A);

/// <summary>
/// Converts human-readable config strings (hotkey combos, hex colors,
/// modifier key names) into values usable by the engine and Windows API layer.
/// </summary>
public static class SettingsParser
{
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private static readonly Dictionary<string, uint> ModifierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = MOD_CONTROL,
        ["alt"] = MOD_ALT,
        ["shift"] = MOD_SHIFT,
        ["win"] = MOD_WIN,
    };

    private static readonly Dictionary<string, int> VirtualKeyModifierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shift"] = 0x10,
        ["ctrl"] = 0x11,
        ["alt"] = 0x12,
    };

    private static readonly Dictionary<string, LogLevel> LogLevelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["debug"] = LogLevel.Debug,
        ["info"] = LogLevel.Info,
        ["warn"] = LogLevel.Warning,
        ["warning"] = LogLevel.Warning,
        ["error"] = LogLevel.Error,
    };

    /// <summary>
    /// Parses a hotkey string like "Ctrl+Win+Z" into a <see cref="HotkeyBinding"/>.
    /// Returns null for empty, null, or invalid input.
    /// </summary>
    public static HotkeyBinding? ParseHotkey(string hotkeyString)
    {
        if (string.IsNullOrEmpty(hotkeyString))
            return null;

        var parts = hotkeyString.Split('+');
        uint modifiers = 0;
        string keyPart = parts[^1];

        // All parts except the last are potential modifiers; the last is the key.
        // But if the last part is also a known modifier and there's only one part,
        // treat it as a key (single key, no modifiers).
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (ModifierMap.TryGetValue(parts[i].Trim(), out uint mod))
                modifiers |= mod;
            else
                return null; // unknown modifier token
        }

        uint? vk = ResolveVirtualKey(keyPart.Trim());
        if (vk is null)
            return null;

        return new HotkeyBinding(modifiers, vk.Value);
    }

    /// <summary>
    /// Parses a hex color string (#RRGGBB or #AARRGGBB) into a <see cref="ColorValue"/>.
    /// Returns null for invalid input.
    /// </summary>
    public static ColorValue? ParseColor(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor) || hexColor[0] != '#')
            return null;

        var hex = hexColor.AsSpan(1);

        if (hex.Length == 6)
        {
            if (!TryParseHexByte(hex.Slice(0, 2), out byte r) ||
                !TryParseHexByte(hex.Slice(2, 2), out byte g) ||
                !TryParseHexByte(hex.Slice(4, 2), out byte b))
                return null;

            return new ColorValue(r, g, b, 0xFF);
        }

        if (hex.Length == 8)
        {
            if (!TryParseHexByte(hex.Slice(0, 2), out byte a) ||
                !TryParseHexByte(hex.Slice(2, 2), out byte r) ||
                !TryParseHexByte(hex.Slice(4, 2), out byte g) ||
                !TryParseHexByte(hex.Slice(6, 2), out byte b))
                return null;

            return new ColorValue(r, g, b, a);
        }

        return null;
    }

    /// <summary>
    /// Parses a modifier key name ("Shift", "Ctrl", "Alt") into its virtual key code.
    /// Returns null for unrecognised names.
    /// </summary>
    public static int? ParseModifierKey(string modifier)
    {
        if (string.IsNullOrEmpty(modifier))
            return null;

        if (VirtualKeyModifierMap.TryGetValue(modifier, out int vk))
            return vk;

        return null;
    }

    /// <summary>
    /// Parses log level strings ("DEBUG", "INFO", "WARN", "ERROR")
    /// into a <see cref="LogLevel"/> value.
    /// Returns null for empty or invalid input.
    /// </summary>
    public static LogLevel? ParseLogLevel(string logLevel)
    {
        if (string.IsNullOrWhiteSpace(logLevel))
            return null;

        if (LogLevelMap.TryGetValue(logLevel.Trim(), out var parsed))
            return parsed;

        return null;
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static uint? ResolveVirtualKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        // Single letter A-Z
        if (key.Length == 1 && char.IsAsciiLetter(key[0]))
            return (uint)(char.ToUpperInvariant(key[0]));

        // Single digit 0-9
        if (key.Length == 1 && char.IsAsciiDigit(key[0]))
            return (uint)key[0]; // '0'-'9' == 0x30-0x39

        // Function keys F1-F12
        if (key.Length >= 2 &&
            (key[0] == 'F' || key[0] == 'f') &&
            int.TryParse(key.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int fNum) &&
            fNum >= 1 && fNum <= 12)
        {
            return (uint)(0x70 + fNum - 1);
        }

        return null;
    }

    private static bool TryParseHexByte(ReadOnlySpan<char> hex, out byte value)
    {
        return byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}
