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

    [Fact]
    public void Apply_DwmReportsInsets_CompensatesSetWindowPos()
    {
        var hwnd = (nint)0x600;
        var zone = new Rectangle(100, 200, 800, 600);
        _windowManager.IsZoomed(hwnd).Returns(false);
        // After first SetWindowPos, GetWindowRect returns the full window rect
        _windowManager.GetWindowRect(hwnd).Returns(new Rectangle(100, 200, 800, 600));
        // DWM reports the visible area is smaller by 7px on left, right, bottom; 0 on top
        _windowManager.GetExtendedFrameBounds(hwnd).Returns(new Rectangle(107, 200, 786, 593));

        var applier = new SnapApplier(_windowManager, _logger);
        applier.Apply(hwnd, zone);

        // Second call should expand outward to compensate for invisible borders
        _windowManager.Received(1).SetWindowPos(hwnd, 93, 200, 814, 607);
    }

    [Fact]
    public void Apply_DwmReportsZeroInsets_NoCompensation()
    {
        var hwnd = (nint)0x700;
        var zone = new Rectangle(100, 200, 800, 600);
        _windowManager.IsZoomed(hwnd).Returns(false);
        _windowManager.GetWindowRect(hwnd).Returns(new Rectangle(100, 200, 800, 600));
        // Visible bounds match window rect exactly — no invisible borders
        _windowManager.GetExtendedFrameBounds(hwnd).Returns(new Rectangle(100, 200, 800, 600));

        var applier = new SnapApplier(_windowManager, _logger);
        applier.Apply(hwnd, zone);

        // Only the initial SetWindowPos, no compensation call
        _windowManager.Received(1).SetWindowPos(hwnd, 100, 200, 800, 600);
    }

    [Fact]
    public void Apply_DwmCallFails_FallsBackToNoCompensation()
    {
        var hwnd = (nint)0x800;
        var zone = new Rectangle(100, 200, 800, 600);
        _windowManager.IsZoomed(hwnd).Returns(false);
        _windowManager.GetExtendedFrameBounds(hwnd).Returns((Rectangle?)null);

        var applier = new SnapApplier(_windowManager, _logger);
        applier.Apply(hwnd, zone);

        // Only the initial SetWindowPos, no compensation
        _windowManager.Received(1).SetWindowPos(hwnd, 100, 200, 800, 600);
    }

    [Fact]
    public void Apply_DwmCompensation_WorksForDifferentZonePositions()
    {
        var hwnd = (nint)0x900;
        // Right-half zone on a 1920x1080 monitor
        var zone = new Rectangle(960, 0, 960, 1080);
        _windowManager.IsZoomed(hwnd).Returns(false);
        _windowManager.GetWindowRect(hwnd).Returns(new Rectangle(960, 0, 960, 1080));
        // Typical Win11: 7px left, 0px top, 7px right, 7px bottom
        _windowManager.GetExtendedFrameBounds(hwnd).Returns(new Rectangle(967, 0, 946, 1073));

        var applier = new SnapApplier(_windowManager, _logger);
        applier.Apply(hwnd, zone);

        _windowManager.Received(1).SetWindowPos(hwnd, 953, 0, 974, 1087);
    }
}
