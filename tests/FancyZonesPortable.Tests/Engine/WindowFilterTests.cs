using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Engine;
using NSubstitute;
using Xunit;

namespace FancyZonesPortable.Tests.Engine;

public class WindowFilterTests
{
    private readonly IWindowManager _windowManager = Substitute.For<IWindowManager>();

    [Fact]
    public void ShouldSnap_NormalWindow_ReturnsTrue()
    {
        var hwnd = (nint)0x100;
        _windowManager.GetWindowClassName(hwnd).Returns("Notepad");
        _windowManager.GetWindowExStyle(hwnd).Returns(0);

        var filter = new WindowFilter(_windowManager);

        Assert.True(filter.ShouldSnap(hwnd));
    }

    [Fact]
    public void ShouldSnap_Taskbar_ReturnsFalse()
    {
        var hwnd = (nint)0x200;
        _windowManager.GetWindowClassName(hwnd).Returns("Shell_TrayWnd");
        _windowManager.GetWindowExStyle(hwnd).Returns(0);

        var filter = new WindowFilter(_windowManager);

        Assert.False(filter.ShouldSnap(hwnd));
    }

    [Fact]
    public void ShouldSnap_SecondaryTaskbar_ReturnsFalse()
    {
        var hwnd = (nint)0x300;
        _windowManager.GetWindowClassName(hwnd).Returns("Shell_SecondaryTrayWnd");
        _windowManager.GetWindowExStyle(hwnd).Returns(0);

        var filter = new WindowFilter(_windowManager);

        Assert.False(filter.ShouldSnap(hwnd));
    }

    [Fact]
    public void ShouldSnap_ToolWindow_ReturnsFalse()
    {
        var hwnd = (nint)0x400;
        _windowManager.GetWindowClassName(hwnd).Returns("SomeApp");
        _windowManager.GetWindowExStyle(hwnd).Returns(0x00000080);

        var filter = new WindowFilter(_windowManager);

        Assert.False(filter.ShouldSnap(hwnd));
    }

    [Fact]
    public void ShouldSnap_OverlayWindow_ReturnsFalse()
    {
        var overlayHwnd = (nint)0x500;
        var filter = new WindowFilter(_windowManager, overlayHwnd);

        Assert.False(filter.ShouldSnap(overlayHwnd));
    }

    [Fact]
    public void ShouldSnap_ZeroHandle_ReturnsFalse()
    {
        var filter = new WindowFilter(_windowManager);

        Assert.False(filter.ShouldSnap(0));
    }

    [Fact]
    public void ShouldSnap_NormalWindowWithOtherExStyles_ReturnsTrue()
    {
        var hwnd = (nint)0x600;
        _windowManager.GetWindowClassName(hwnd).Returns("MyApp");
        _windowManager.GetWindowExStyle(hwnd).Returns(0x100);

        var filter = new WindowFilter(_windowManager);

        Assert.True(filter.ShouldSnap(hwnd));
    }

    [Fact]
    public void ShouldSnap_ToolWindowCombinedWithOtherStyles_ReturnsFalse()
    {
        var hwnd = (nint)0x700;
        _windowManager.GetWindowClassName(hwnd).Returns("AnotherApp");
        _windowManager.GetWindowExStyle(hwnd).Returns(0x180); // 0x100 | 0x080

        var filter = new WindowFilter(_windowManager);

        Assert.False(filter.ShouldSnap(hwnd));
    }
}
