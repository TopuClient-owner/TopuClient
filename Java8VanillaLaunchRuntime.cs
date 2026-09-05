using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CmlLib.Core;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private async Task LaunchJava8VanillaProfileAsync()
        {
            if (_minecraftProcess != null)
            {
                try
                {
                    if (!_minecraftProcess.HasExited)
                    {
                        MessageBox.Show(
                            "Minecraft is already running.",
                            "Topu Client",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                }
                catch { }

                _minecraftProcess = null;
            }

            LaunchBtn.IsEnabled = false;

            try
            {
                RuntimeProfileSettings profile = GetRuntimeProfile();

                if (!profile.Loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) ||
                    !profile.Version.Equals("1.8.9", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The Java 8 Vanilla launcher was invoked for a non-Vanilla 1.8.9 profile.");
                }

                int ram = Math.Max(2048, profile.RamGb * 1024);

                StartLaunchLog();
                WriteLog("===== TOPU JAVA 8 VANILLA LAUNCH =====");
                WriteLog("Minecraft 1.8.9 Vanilla requires Java 8.");
                WriteLog("Forge installation is explicitly disabled for this launch.");
                WriteLog($"Profile: {_gamePath}");
                WriteLog($"Loader: {profile.Loader}");
                WriteLog($"Minecraft: {profile.Version}");
                WriteLog($"RAM: {ram} MB");

                _session = await AuthenticateSelectedAccountAsync();
                if (_session == null)
                    throw new InvalidOperationException("Could not create a Minecraft session.");

                string javaPath = await EnsureJava8RuntimeAsync();
                if (!IsJava8Runtime(javaPath))
                    throw new InvalidOperationException("The installed runtime is not Java 8.");

                MinecraftPath minecraftPath = new MinecraftPath(_gamePath);
                MinecraftLauncher launcher = new MinecraftLauncher(minecraftPath);

                StatusText.Text = "Installing Vanilla Minecraft 1.8.9...";
                await launcher.InstallAsync("1.8.9", CancellationToken.None);

                MLaunchOption options = new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = ram,
                    MinimumRamMb = Math.Min(1024, ram),
                    JavaPath = javaPath,
                    GameLauncherName = "Topu Client",
                    GameLauncherVersion = "1.0.0"
                };

                StatusText.Text = "Building Vanilla 1.8.9 process...";
                Process process = await launcher.BuildProcessAsync(
                    "1.8.9",
                    options,
                    CancellationToken.None);

                if (process == null)
                    throw new InvalidOperationException("CmlLib returned a null Minecraft process.");

                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.OutputDataReceived += Minecraft_OutputDataReceived;
                process.ErrorDataReceived += Minecraft_ErrorDataReceived;

                WriteLog("Loader version: 1.8.9");
                WriteLog($"Java 8 executable: {javaPath}");
                WriteLog($"Executable: {process.StartInfo.FileName}");
                WriteLog($"Arguments: {process.StartInfo.Arguments}");
                WriteLog($"Working directory: {process.StartInfo.WorkingDirectory}");
                WriteDebugFile(process, javaPath, "1.8.9", "1.8.9", ram);

                if (process.StartInfo.Arguments.Contains("FMLTweaker", StringComparison.OrdinalIgnoreCase) ||
                    process.StartInfo.Arguments.Contains("net.minecraftforge", StringComparison.OrdinalIgnoreCase) ||
                    process.StartInfo.Arguments.Contains("launchwrapper", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Vanilla 1.8.9 launch was contaminated with Forge arguments. Launch aborted before starting Minecraft.");
                }

                StatusText.Text = "Starting Vanilla 1.8.9...";
                if (!process.Start())
                    throw new InvalidOperationException("Windows failed to start Minecraft.");

                _minecraftProcess = process;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                StatusText.Text = $"Topu Client running Vanilla 1.8.9 as {_session.Username}";
                _ = MonitorMinecraftAsync(process);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Vanilla 1.8.9 launch failed.";
                WriteException("JAVA 8 VANILLA LAUNCH ERROR", ex);
                MessageBox.Show(
                    "Minecraft 1.8.9 Vanilla failed to launch.\n\n" +
                    ex.Message +
                    "\n\nLog:\n" +
                    _logPath,
                    "Vanilla 1.8.9 Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }
    }
}
