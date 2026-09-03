using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CmlLib.Core;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private static readonly bool DynamicNeoForgeLaunchHook = RegisterDynamicNeoForgeLaunchHook();

        private static bool RegisterDynamicNeoForgeLaunchHook()
        {
            EventManager.RegisterClassHandler(
                typeof(Button),
                Button.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(DynamicNeoForgeLaunchPreview),
                true);
            return true;
        }

        private static void DynamicNeoForgeLaunchPreview(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button || button.DataContext is MainWindow)
                return;

            MainWindow? window = Window.GetWindow(button) as MainWindow;
            if (window == null || window.LaunchBtn != button)
                return;

            RuntimeProfileSettings profile = window.GetRuntimeProfile();
            if (!profile.Loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
                return;

            e.Handled = true;
            _ = window.LaunchDynamicNeoForgeAsync(profile);
        }

        private async Task LaunchDynamicNeoForgeAsync(RuntimeProfileSettings profile)
        {
            if (_minecraftProcess != null)
            {
                try
                {
                    if (!_minecraftProcess.HasExited)
                    {
                        MessageBox.Show("Minecraft is already running.", "Topu Client", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                catch { }
                _minecraftProcess = null;
            }

            LaunchBtn.IsEnabled = false;
            try
            {
                string minecraftVersion = profile.Version;
                int ram = Math.Max(2048, profile.RamGb * 1024);

                StartLaunchLog();
                WriteLog("===== TOPU DYNAMIC NEOFORGE LAUNCH =====");
                WriteLog($"Minecraft: {minecraftVersion}");
                WriteLog($"Profile: {_gamePath}");
                WriteLog($"RAM: {ram} MB");

                _session = await AuthenticateSelectedAccountAsync();
                if (_session == null)
                    throw new InvalidOperationException("Could not create a Minecraft session.");

                string javaPath = await EnsureJavaAsync(minecraftVersion.StartsWith("26.", StringComparison.OrdinalIgnoreCase) ? 25 : 21);
                MinecraftPath minecraftPath = new MinecraftPath(_gamePath);
                MinecraftLauncher launcher = new MinecraftLauncher(minecraftPath);

                StatusText.Text = $"Installing Minecraft {minecraftVersion}...";
                await launcher.InstallAsync(minecraftVersion, CancellationToken.None);

                StatusText.Text = $"Installing NeoForge for {minecraftVersion}...";
                NeoForgeInstaller installer = new NeoForgeInstaller(launcher, Http);
                NeoForgeInstallOptions installOptions = new NeoForgeInstallOptions
                {
                    JavaPath = javaPath,
                    SkipIfAlreadyInstalled = true,
                    CancellationToken = CancellationToken.None,
                    InstallerOutput = new Progress<string>(line => WriteLog("[NeoForge] " + line))
                };

                string loaderVersionName = await installer.Install(minecraftVersion, installOptions);
                WriteLog($"Selected NeoForge version: {loaderVersionName}");

                await launcher.InstallAsync(loaderVersionName, CancellationToken.None);
                await InstallUniversalPerformancePackAsync("NeoForge", minecraftVersion);

                MLaunchOption options = new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = ram,
                    MinimumRamMb = Math.Min(1024, ram),
                    JavaPath = javaPath,
                    GameLauncherName = "Topu Client",
                    GameLauncherVersion = "1.0.0"
                };

                StatusText.Text = "Building NeoForge process...";
                Process process = await launcher.BuildProcessAsync(loaderVersionName, options, CancellationToken.None);
                if (process == null)
                    throw new InvalidOperationException("CmlLib returned a null Minecraft process.");

                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.OutputDataReceived += Minecraft_OutputDataReceived;
                process.ErrorDataReceived += Minecraft_ErrorDataReceived;

                WriteLog($"Loader version: {loaderVersionName}");
                WriteLog($"Executable: {process.StartInfo.FileName}");
                WriteLog($"Arguments: {process.StartInfo.Arguments}");
                WriteLog($"Working directory: {process.StartInfo.WorkingDirectory}");
                WriteDebugFile(process, javaPath, minecraftVersion, loaderVersionName, ram);

                StatusText.Text = $"Starting NeoForge {minecraftVersion}...";
                if (!process.Start())
                    throw new InvalidOperationException("Windows failed to start Minecraft.");

                _minecraftProcess = process;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                StatusText.Text = $"Topu Client running as {_session.Username}";
                _ = MonitorMinecraftAsync(process);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Launch failed.";
                WriteException("TOPU DYNAMIC NEOFORGE LAUNCH ERROR", ex);
                MessageBox.Show("Minecraft failed to launch.\n\n" + ex.Message + "\n\nLog:\n" + _logPath, "Topu Client", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }
    }
}
