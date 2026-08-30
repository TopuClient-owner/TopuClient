using System;
using System.IO;
using System.Text;
using System.Windows;

namespace TopuLauncher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            try
            {
                base.OnStartup(e);
                MainWindow window = new MainWindow();
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                WriteCrashLog("Startup exception", ex);
                MessageBox.Show(
                    "Topu Client failed to start.\n\n" + ex.Message +
                    "\n\nA crash log was saved to:\n" + GetCrashLogPath(),
                    "Topu Client - Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            WriteCrashLog("Dispatcher unhandled exception", e.Exception);
            e.Handled = true;
            MessageBox.Show(
                "Topu Client encountered an unexpected error.\n\n" + e.Exception.Message +
                "\n\nCrash log:\n" + GetCrashLogPath(),
                "Topu Client - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                WriteCrashLog("AppDomain unhandled exception", ex);
            else
                WriteCrashLog("AppDomain unhandled exception", new Exception(Convert.ToString(e.ExceptionObject) ?? "Unknown exception"));
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteCrashLog("Unobserved task exception", e.Exception);
            e.SetObserved();
        }

        private static string GetCrashLogPath()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TopuClient");

            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "startup-crash.log");
        }

        private static void WriteCrashLog(string title, Exception ex)
        {
            try
            {
                string path = GetCrashLogPath();
                var builder = new StringBuilder();
                builder.AppendLine("===== TOPU CLIENT CRASH =====");
                builder.AppendLine(DateTimeOffset.Now.ToString("O"));
                builder.AppendLine(title);
                builder.AppendLine();
                builder.AppendLine(ex.ToString());
                builder.AppendLine();
                builder.AppendLine("OS: " + Environment.OSVersion);
                builder.AppendLine("64-bit OS: " + Environment.Is64BitOperatingSystem);
                builder.AppendLine("64-bit Process: " + Environment.Is64BitProcess);
                builder.AppendLine(".NET: " + Environment.Version);
                File.WriteAllText(path, builder.ToString());
            }
            catch
            {
                // Never allow crash logging itself to crash the launcher.
            }
        }
    }
}
