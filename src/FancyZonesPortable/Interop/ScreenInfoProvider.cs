using FancyZonesPortable.Core.Abstractions;
using Rectangle = FancyZonesPortable.Core.Abstractions.Rectangle;

namespace FancyZonesPortable.Interop;

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
