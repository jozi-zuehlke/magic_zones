using FancyZonesPortable.App;
using FancyZonesPortable.Core.Logging;

namespace FancyZonesPortable;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var logger = new Logger();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            logger.Error("Unhandled UI thread exception.", e.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                logger.Error("Unhandled domain exception.", ex);
        };

        Application.Run(new App.AppContext());
    }
}
