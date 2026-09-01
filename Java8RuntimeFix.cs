using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge;

namespace TopuLauncher
{
    // Java 8 is handled separately because api.adoptium.net can fail with
    // DNS errors and Java 8 reports its version as 1.8.x.
    // Java 17/21/25 handling is untouched.
    public partial class MainWindow
    {
        private static readonly object Java8HandlerRegistration = RegisterJava8LaunchHandler();

        private static object RegisterJava8LaunchHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(Button),
                Button.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(Java8LaunchButtonHandler));
            return new object();
        }

        private static void Java8LaunchButtonHandler(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button || Window.GetWindow(button) is not MainWindow window)
                return;

            if (!ReferenceEquals(button, window.LaunchBtn))
                return;

            RuntimeProfileSettings profile = window.GetRuntimeProfile();
            if (!profile.Loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                !profile.Version.Equals("1.8.9", StringComparison.OrdinalIgnoreCase))
                return;

            e.Handled = true;
            _ = window.LaunchJava8ProfileAsync();
        }

        private async Task LaunchJava8ProfileAsync()
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
                RuntimeProfileSettings profile = GetRuntimeProfile();
                int ram = Math.Max(2048, profile.RamGb * 1024);

                StartLaunchLog();
                WriteLog("===== TOPU JAVA 8 LAUNCH =====");
                WriteLog("Forge 1.8.9 requires Java 8.");
                WriteLog($"Profile: {_gamePath}");
                WriteLog($"RAM: {ram} MB");

                _session = await AuthenticateSelectedAccountAsync();
                if (_session == null)
                    throw new InvalidOperationException("Could not create a Minecraft session.");

                string javaPath = await EnsureJava8RuntimeAsync();
                if (!IsJava8Runtime(javaPath))
                    throw new InvalidOperationException("The installed runtime is not Java 8.");

                MinecraftPath minecraftPath = new MinecraftPath(_gamePath);
                MinecraftLauncher launcher = new MinecraftLauncher(minecraftPath);

                StatusText.Text = "Installing Minecraft 1.8.9...";
                await launcher.InstallAsync("1.8.9", CancellationToken.None);

                StatusText.Text = "Installing Forge for 1.8.9...";
                ForgeInstaller forge = new ForgeInstaller(launcher, Http);
                IEnumerable<ForgeVersion> versions = await forge.GetForgeVersions("1.8.9");
                ForgeVersion? selected = versions.FirstOrDefault();
                if (selected == null)
                    throw new InvalidOperationException("No Forge build was found for Minecraft 1.8.9.");

                WriteLog($"Selected Forge build: {selected.ForgeVersionName}");
                string loaderVersionName = await forge.Install(selected);

                MLaunchOption options = new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = ram,
                    MinimumRamMb = Math.Min(1024, ram),
                    JavaPath = javaPath,
                    GameLauncherName = "Topu Client",
                    GameLauncherVersion = "1.0.0"
                };

                StatusText.Text = "Building Forge 1.8.9 process...";
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
                WriteLog($"Java 8 executable: {javaPath}");
                WriteLog($"Executable: {process.StartInfo.FileName}");
                WriteLog($"Arguments: {process.StartInfo.Arguments}");
                WriteLog($"Working directory: {process.StartInfo.WorkingDirectory}");
                WriteDebugFile(process, javaPath, "1.8.9", loaderVersionName, ram);

                StatusText.Text = "Starting Forge 1.8.9...";
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
                StatusText.Text = "Java 8 launch failed.";
                WriteException("JAVA 8 LAUNCH ERROR", ex);
                MessageBox.Show(
                    "Minecraft 1.8.9 failed to launch.\n\n" + ex.Message +
                    "\n\nLog:\n" + _logPath,
                    "Java 8 Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        private async Task<string> EnsureJava8RuntimeAsync()
        {
            string runtimeFolder = Path.Combine(_gamePath, "runtime", "java8");
            string javaExe = Path.Combine(runtimeFolder, "bin", "java.exe");

            if (File.Exists(javaExe) && IsJava8Runtime(javaExe))
            {
                WriteLog($"Using existing verified Java 8: {javaExe}");
                return javaExe;
            }

            if (Directory.Exists(runtimeFolder))
            {
                WriteLog("Existing java8 runtime is missing or is NOT Java 8. Removing it.");
                TryDeleteDirectory(runtimeFolder);
            }

            StatusText.Text = "Downloading Java 8...";

            // Official Eclipse Temurin 8 Windows x64 JRE release asset.
            // This intentionally bypasses api.adoptium.net.
            const string downloadUrl =
                "https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u502-b07/OpenJDK8U-jre_x64_windows_hotspot_8u502b07.zip";

            string tempArchive = Path.Combine(
                Path.GetTempPath(),
                "topu-java8-" + Guid.NewGuid().ToString("N") + ".zip");

            string extractionDirectory = runtimeFolder +
                ".extracting-" + Guid.NewGuid().ToString("N");

            try
            {
                WriteLog("Java 8 source: Eclipse Temurin 8u502-b07");
                WriteLog($"Java 8 download: {downloadUrl}");

                using (HttpResponseMessage response = await Http.GetAsync(
                    downloadUrl,
                    HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using Stream input = await response.Content.ReadAsStreamAsync();
                    using FileStream output = new FileStream(
                        tempArchive, FileMode.Create, FileAccess.Write, FileShare.Read,
                        81920, FileOptions.SequentialScan);
                    await input.CopyToAsync(output, 81920, CancellationToken.None);
                    await output.FlushAsync(CancellationToken.None);
                }

                if (!File.Exists(tempArchive) || new FileInfo(tempArchive).Length <= 0)
                    throw new IOException("Java 8 archive download failed or was empty.");

                TryDeleteDirectory(extractionDirectory);
                Directory.CreateDirectory(extractionDirectory);
                ZipFile.ExtractToDirectory(tempArchive, extractionDirectory, true);

                string? javaRoot = FindJava8Root(extractionDirectory);
                if (javaRoot == null)
                    throw new InvalidDataException("The Java 8 archive does not contain bin\\java.exe.");

                MoveJavaRootContents(javaRoot, extractionDirectory);

                if (Directory.Exists(runtimeFolder))
                    TryDeleteDirectory(runtimeFolder);

                MoveDirectoryWithRetry(extractionDirectory, runtimeFolder);

                string installedJava = Path.Combine(runtimeFolder, "bin", "java.exe");
                if (!File.Exists(installedJava))
                    throw new InvalidDataException("Java 8 was extracted, but bin\\java.exe is missing.");

                if (!IsJava8Runtime(installedJava))
                {
                    TryDeleteDirectory(runtimeFolder);
                    throw new InvalidDataException("The downloaded runtime is not Java 8.");
                }

                WriteLog($"Java 8 installed and verified: {installedJava}");
                return installedJava;
            }
            finally
            {
                TryDeleteFileWithRetry(tempArchive);
                TryDeleteDirectory(extractionDirectory);
            }
        }

        private static string? FindJava8Root(string extractionDirectory)
        {
            string direct = Path.Combine(extractionDirectory, "bin", "java.exe");
            if (File.Exists(direct))
                return extractionDirectory;

            foreach (string directory in Directory.GetDirectories(extractionDirectory))
            {
                if (File.Exists(Path.Combine(directory, "bin", "java.exe")))
                    return directory;
            }
            return null;
        }

        private static bool IsJava8Runtime(string javaPath)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using Process process = Process.Start(info)
                    ?? throw new InvalidOperationException("Could not start Java 8.");

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                string combined = stdout + Environment.NewLine + stderr;
                WriteLog($"Java 8 verification [{javaPath}]: {combined.Trim()}");

                // Java 8 reports 1.8.x, not 8.x.
                return combined.Contains("version \"1.8.", StringComparison.OrdinalIgnoreCase) ||
                       combined.Contains("openjdk version \"1.8.", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                WriteLog($"Java 8 verification failed [{javaPath}]: {ex.Message}");
                return false;
            }
        }
    }
}
