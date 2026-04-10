using MagicZonesPortable.Core.Engine;
using Xunit;

namespace MagicZonesPortable.Tests.Engine;

/// <summary>
/// Tests for <see cref="ModifierKeyMapper"/> which maps specific left/right
/// modifier VK codes (as reported by WH_KEYBOARD_LL hooks) to generic modifier
/// codes used in configuration.
/// </summary>
public class ModifierKeyMapperTests
{
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;   // Alt
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;
    private const int VK_A = 0x41;

    // ── Shift ────────────────────────────────────────────────

    [Fact]
    public void LeftShift_MatchesGenericShift()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_LSHIFT, VK_SHIFT));
    }

    [Fact]
    public void RightShift_MatchesGenericShift()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_RSHIFT, VK_SHIFT));
    }

    [Fact]
    public void GenericShift_MatchesItself()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_SHIFT, VK_SHIFT));
    }

    // ── Control ──────────────────────────────────────────────

    [Fact]
    public void LeftControl_MatchesGenericControl()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_LCONTROL, VK_CONTROL));
    }

    [Fact]
    public void RightControl_MatchesGenericControl()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_RCONTROL, VK_CONTROL));
    }

    [Fact]
    public void GenericControl_MatchesItself()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_CONTROL, VK_CONTROL));
    }

    // ── Alt (Menu) ───────────────────────────────────────────

    [Fact]
    public void LeftAlt_MatchesGenericAlt()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_LMENU, VK_MENU));
    }

    [Fact]
    public void RightAlt_MatchesGenericAlt()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_RMENU, VK_MENU));
    }

    [Fact]
    public void GenericAlt_MatchesItself()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_MENU, VK_MENU));
    }

    // ── Cross-modifier mismatches ────────────────────────────

    [Fact]
    public void LeftShift_DoesNotMatchControl()
    {
        Assert.False(ModifierKeyMapper.IsModifierMatch(VK_LSHIFT, VK_CONTROL));
    }

    [Fact]
    public void LeftControl_DoesNotMatchShift()
    {
        Assert.False(ModifierKeyMapper.IsModifierMatch(VK_LCONTROL, VK_SHIFT));
    }

    [Fact]
    public void LeftAlt_DoesNotMatchShift()
    {
        Assert.False(ModifierKeyMapper.IsModifierMatch(VK_LMENU, VK_SHIFT));
    }

    [Fact]
    public void LeftShift_DoesNotMatchAlt()
    {
        Assert.False(ModifierKeyMapper.IsModifierMatch(VK_LSHIFT, VK_MENU));
    }

    // ── Non-modifier keys ────────────────────────────────────

    [Fact]
    public void RegularKey_DoesNotMatchAnyModifier()
    {
        Assert.False(ModifierKeyMapper.IsModifierMatch(VK_A, VK_SHIFT));
        Assert.False(ModifierKeyMapper.IsModifierMatch(VK_A, VK_CONTROL));
        Assert.False(ModifierKeyMapper.IsModifierMatch(VK_A, VK_MENU));
    }

    [Fact]
    public void NonModifierGenericVk_MatchesOnlyExactEqual()
    {
        Assert.True(ModifierKeyMapper.IsModifierMatch(VK_A, VK_A));
        Assert.False(ModifierKeyMapper.IsModifierMatch(VK_A, 0x42)); // VK_B
    }
}
