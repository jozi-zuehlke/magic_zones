using FancyZonesPortable.App;
using FancyZonesPortable.Logging;

namespace FancyZonesPortable;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Initialize logging first
        Logger.Initialize();
        Logger.Info("FancyZones Portable starting...");
        Logger.Info($".NET Version: {Environment.Version}");
        Logger.Info($"OS: {Environment.OSVersion}");
        Logger.Info($"Exe Path: {Environment.ProcessPath}");

        // Global exception handlers
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Logger.Error("Unhandled UI thread exception", e.Exception);
            try
            {
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{e.Exception.Message}",
                    "FancyZones Portable — Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Logger.Error("Unhandled domain exception", ex);
            else
                Logger.Error($"Unhandled domain exception: {e.ExceptionObject}");
        };

        // Configure application
        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new App.AppContext());
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal exception in Application.Run", ex);
            throw;
        }
        finally
        {
            Logger.Info("FancyZones Portable shutting down.");
            Logger.Shutdown();
        }
    }
}
