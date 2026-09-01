using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TopuLauncher
{
    // Java 8 is handled separately because the old Adoptium metadata endpoint
    // can fail with DNS errors and Java 8 reports its version as 1.8.x.
    // This file does not change the Java 17/21/25 runtime path.
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
            if (sender is not Button button)
                return;

            if (Window.GetWindow(button) is not MainWindow window)
                return;

            if (!ReferenceEquals(button, window.LaunchBtn))
                return;

            RuntimeProfileSettings profile = window.GetRuntimeProfile();

            if (!profile.Loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                !profile.Version.Equals("1.8.9", StringComparison.OrdinalIgnoreCase))
                return;

            // The normal non-Fabric handler must not run first. We install and
            // verify Java 8, then invoke the existing launch pipeline.
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
                StartLaunchLog();
                WriteLog("===== TOPU JAVA 8 CHECK =====");
                WriteLog("Minecraft 1.8.9 requires Java 8.");
                WriteLog($"Profile: {_gamePath}");

                string javaPath = await EnsureJava8RuntimeAsync();

                if (!IsJava8Runtime(javaPath))
                    throw new InvalidOperationException("The installed runtime is not Java 8.");

                WriteLog($"Verified Java 8 runtime: {javaPath}");

                // The existing launcher method performs authentication,
                // Minecraft/Forge installation and process construction.
                // Java 8 is now already present and verified, so its normal
                // EnsureJavaAsync path will reuse this runtime.
                await LaunchNonFabricProfileAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Java 8 setup failed.";
                WriteException("JAVA 8 RUNTIME ERROR", ex);
                MessageBox.Show(
                    "Minecraft 1.8.9 needs Java 8.\n\n" + ex.Message +
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

            if (File.Exists(javaExe))
            {
                WriteLog("Existing java8 runtime is NOT Java 8. Removing it.");
                TryDeleteDirectory(runtimeFolder);
            }

            StatusText.Text = "Downloading Java 8...";

            // Official Eclipse Temurin 8 Windows x64 JRE archive.
            // This bypasses api.adoptium.net entirely.
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
                WriteLog("Java 8 API fallback: disabled; using official GitHub release asset.");
                WriteLog($"Java 8 download: {downloadUrl}");

                using (HttpResponseMessage response = await Http.GetAsync(
                    downloadUrl,
                    HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using Stream input = await response.Content.ReadAsStreamAsync();
                    using FileStream output = new FileStream(
                        tempArchive,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read,
                        81920,
                        FileOptions.SequentialScan);

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

                if (Directory.Exists(runtimeFolder))
                    TryDeleteDirectory(runtimeFolder);

                MoveJavaRootContents(javaRoot, extractionDirectory);
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

                // Java 8 normally reports: java version "1.8.0_xxx"
                // while newer Java versions report: java version "17...", etc.
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
