using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Engine;
using FancyZonesPortable.Core.Logging;
using NSubstitute;
using Xunit;

namespace FancyZonesPortable.Tests.Engine;

public class SnapApplierTests
{
    private readonly IWindowManager _windowManager = Substitute.For<IWindowManager>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public void Apply_NormalWindow_CallsSetWindowPos()
    {
        var hwnd = (nint)0x100;
        var zone = new Rectangle(0, 0, 960, 1080);
        _windowManager.IsZoomed(hwnd).Returns(false);

        var applier = new SnapApplier(_windowManager, _logger);
        applier.Apply(hwnd, zone);

        _windowManager.Received(1).SetWindowPos(hwnd, 0, 0, 960, 1080);
    }

    [Fact]
    public void Apply_MaximizedWindow_RestoresBeforeSnapping()
    {
        var hwnd = (nint)0x200;
        var zone = new Rectangle(0, 0, 960, 1080);
        _windowManager.IsZoomed(hwnd).Returns(true);

        var applier = new SnapApplier(_windowManager, _logger);
        applier.Apply(hwnd, zone);

        Received.InOrder(() =>
        {
            _windowManager.ShowWindow(hwnd, 9); // SW_RESTORE
            _windowManager.SetWindowPos(hwnd, 0, 0, 960, 1080);
        });
    }

    [Fact]
    public void Apply_NonMaximizedWindow_DoesNotCallRestore()
    {
        var hwnd = (nint)0x300;
        var zone = new Rectangle(0, 0, 960, 1080);
        _windowManager.IsZoomed(hwnd).Returns(false);

        var applier = new SnapApplier(_windowManager, _logger);
        applier.Apply(hwnd, zone);

        _windowManager.DidNotReceive().ShowWindow(hwnd, Arg.Any<int>());
    }

    [Fact]
    public void Apply_CorrectZoneBounds_PassedToSetWindowPos()
    {
        var hwnd = (nint)0x400;
        var zone = new Rectangle(100, 200, 800, 600);
        _windowManager.IsZoomed(hwnd).Returns(false);

        var applier = new SnapApplier(_windowManager, _logger);
        applier.Apply(hwnd, zone);

        _windowManager.Received(1).SetWindowPos(hwnd, 100, 200, 800, 600);
    }

    [Fact]
    public void Apply_LogsSnapEvent()
    {
        var hwnd = (nint)0x500;
        var zone = new Rectangle(100, 200, 800, 600);
        _windowManager.IsZoomed(hwnd).Returns(false);

        var applier = new SnapApplier(_windowManager, _logger);
        applier.Apply(hwnd, zone);

        _logger.Received(1).Info(Arg.Is<string>(msg =>
            msg.Contains("100") && msg.Contains("200") &&
            msg.Contains("800") && msg.Contains("600")));
    }
}
