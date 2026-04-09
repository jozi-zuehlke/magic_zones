namespace FancyZonesPortable.Core.Engine;

/// <summary>
/// Maps specific left/right modifier virtual-key codes reported by low-level
/// keyboard hooks to the generic modifier codes used in configuration.
/// </summary>
public static class ModifierKeyMapper
{
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;

    /// <summary>
    /// Returns true when <paramref name="hookVkCode"/> (the virtual-key code
    /// delivered by a low-level keyboard hook) matches <paramref name="genericModifierVk"/>
    /// (the generic modifier VK stored in config, e.g. 0x10 for Shift).
    /// Both left and right variants match the generic code.
    /// </summary>
    public static bool IsModifierMatch(int hookVkCode, int genericModifierVk)
    {
        if (hookVkCode == genericModifierVk)
            return true;

        return genericModifierVk switch
        {
            VK_SHIFT => hookVkCode is VK_LSHIFT or VK_RSHIFT,
            VK_CONTROL => hookVkCode is VK_LCONTROL or VK_RCONTROL,
            VK_MENU => hookVkCode is VK_LMENU or VK_RMENU,
            _ => false,
        };
    }
}
