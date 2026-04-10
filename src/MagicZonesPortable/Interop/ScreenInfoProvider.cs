using MagicZonesPortable.Core.Abstractions;
using Rectangle = MagicZonesPortable.Core.Abstractions.Rectangle;

namespace MagicZonesPortable.Interop;

/// <summary>
/// Implements <see cref="IScreenInfo"/> using WinForms Screen.PrimaryScreen.
/// </summary>
internal class ScreenInfoProvider : IScreenInfo
{
    public Rectangle GetPrimaryWorkingArea()
    {
        var wa = Screen.PrimaryScreen!.WorkingArea;
        return new Rectangle(wa.X, wa.Y, wa.Width, wa.Height);
    }
}
