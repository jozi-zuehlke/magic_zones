using FancyZonesPortable.Core.Config;
using Xunit;

namespace FancyZonesPortable.Tests.Config;

/// <summary>
/// Tests for <see cref="SettingsParser"/>: hotkey parsing, color parsing,
/// and modifier key parsing.
/// </summary>
public class SettingsParserTests
{
    // ── ParseHotkey ──────────────────────────────────────────────────

    [Fact]
    public void ParseHotkey_CtrlWinZ_ReturnsCorrectModifiersAndKey()
    {
        var result = SettingsParser.ParseHotkey("Ctrl+Win+Z");
        Assert.NotNull(result);
        Assert.Equal(0x000Au, result.Modifiers);
        Assert.Equal(0x5Au, result.VirtualKey);
    }

    [Fact]
    public void ParseHotkey_AltShiftF1_ReturnsCorrectValues()
    {
        var result = SettingsParser.ParseHotkey("Alt+Shift+F1");
        Assert.NotNull(result);
        Assert.Equal(0x0005u, result.Modifiers);
        Assert.Equal(0x70u, result.VirtualKey);
    }

    [Fact]
    public void ParseHotkey_SingleKey_NoModifiers()
    {
        var result = SettingsParser.ParseHotkey("Z");
        Assert.NotNull(result);
        Assert.Equal(0u, result.Modifiers);
        Assert.Equal(0x5Au, result.VirtualKey);
    }

    [Fact]
    public void ParseHotkey_CaseInsensitive()
    {
        var upper = SettingsParser.ParseHotkey("Ctrl+Win+Z");
        var lower = SettingsParser.ParseHotkey("ctrl+win+z");
        Assert.NotNull(upper);
        Assert.NotNull(lower);
        Assert.Equal(upper.Modifiers, lower.Modifiers);
        Assert.Equal(upper.VirtualKey, lower.VirtualKey);
    }

    [Fact]
    public void ParseHotkey_EmptyString_ReturnsNull()
    {
        Assert.Null(SettingsParser.ParseHotkey(""));
    }

    [Fact]
    public void ParseHotkey_NullString_ReturnsNull()
    {
        Assert.Null(SettingsParser.ParseHotkey(null!));
    }

    [Fact]
    public void ParseHotkey_InvalidKey_ReturnsNull()
    {
        Assert.Null(SettingsParser.ParseHotkey("Ctrl+???"));
    }

    [Fact]
    public void ParseHotkey_DuplicateModifiers_Handled()
    {
        var result = SettingsParser.ParseHotkey("Ctrl+Ctrl+Z");
        Assert.NotNull(result);
        Assert.Equal(0x0002u, result.Modifiers); // same as single Ctrl
        Assert.Equal(0x5Au, result.VirtualKey);
    }

    [Fact]
    public void ParseHotkey_NumberKey()
    {
        var result = SettingsParser.ParseHotkey("Ctrl+1");
        Assert.NotNull(result);
        Assert.Equal(0x31u, result.VirtualKey);
    }

    [Fact]
    public void ParseHotkey_F12()
    {
        var result = SettingsParser.ParseHotkey("F12");
        Assert.NotNull(result);
        Assert.Equal(0u, result.Modifiers);
        Assert.Equal(0x7Bu, result.VirtualKey);
    }

    // ── ParseColor ───────────────────────────────────────────────────

    [Fact]
    public void ParseColor_ValidRGB_ReturnsCorrectValues()
    {
        var result = SettingsParser.ParseColor("#0078D4");
        Assert.NotNull(result);
        Assert.Equal(0x00, result.R);
        Assert.Equal(0x78, result.G);
        Assert.Equal(0xD4, result.B);
        Assert.Equal(0xFF, result.A);
    }

    [Fact]
    public void ParseColor_ValidARGB_ReturnsCorrectValues()
    {
        var result = SettingsParser.ParseColor("#800078D4");
        Assert.NotNull(result);
        Assert.Equal(0x80, result.A);
        Assert.Equal(0x00, result.R);
        Assert.Equal(0x78, result.G);
        Assert.Equal(0xD4, result.B);
    }

    [Fact]
    public void ParseColor_LowercaseHex_Works()
    {
        var result = SettingsParser.ParseColor("#aabbcc");
        Assert.NotNull(result);
        Assert.Equal(0xAA, result.R);
        Assert.Equal(0xBB, result.G);
        Assert.Equal(0xCC, result.B);
        Assert.Equal(0xFF, result.A);
    }

    [Fact]
    public void ParseColor_InvalidFormat_ReturnsNull()
    {
        Assert.Null(SettingsParser.ParseColor("not-a-color"));
    }

    [Fact]
    public void ParseColor_MissingHash_ReturnsNull()
    {
        Assert.Null(SettingsParser.ParseColor("0078D4"));
    }

    [Fact]
    public void ParseColor_WrongLength_ReturnsNull()
    {
        Assert.Null(SettingsParser.ParseColor("#0078"));
    }

    [Fact]
    public void ParseColor_NullString_ReturnsNull()
    {
        Assert.Null(SettingsParser.ParseColor(null!));
    }

    // ── ParseModifierKey ─────────────────────────────────────────────

    [Fact]
    public void ParseModifierKey_Shift_ReturnsVKShift()
    {
        Assert.Equal(0x10, SettingsParser.ParseModifierKey("Shift"));
    }

    [Fact]
    public void ParseModifierKey_Ctrl_ReturnsVKControl()
    {
        Assert.Equal(0x11, SettingsParser.ParseModifierKey("Ctrl"));
    }

    [Fact]
    public void ParseModifierKey_Alt_ReturnsVKMenu()
    {
        Assert.Equal(0x12, SettingsParser.ParseModifierKey("Alt"));
    }

    [Fact]
    public void ParseModifierKey_CaseInsensitive()
    {
        Assert.Equal(
            SettingsParser.ParseModifierKey("Shift"),
            SettingsParser.ParseModifierKey("shift"));
        Assert.Equal(
            SettingsParser.ParseModifierKey("Shift"),
            SettingsParser.ParseModifierKey("SHIFT"));
    }

    [Fact]
    public void ParseModifierKey_InvalidModifier_ReturnsNull()
    {
        Assert.Null(SettingsParser.ParseModifierKey("Tab"));
    }

    // ── ParseLogLevel ────────────────────────────────────────────────

    [Theory]
    [InlineData("DEBUG")]
    [InlineData("INFO")]
    [InlineData("WARN")]
    [InlineData("ERROR")]
    [InlineData("warning")]
    public void ParseLogLevel_ValidValues_ReturnsValue(string value)
    {
        Assert.NotNull(SettingsParser.ParseLogLevel(value));
    }

    [Fact]
    public void ParseLogLevel_InvalidValue_ReturnsNull()
    {
        Assert.Null(SettingsParser.ParseLogLevel("TRACE"));
    }
}
