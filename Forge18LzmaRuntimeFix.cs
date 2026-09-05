using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private static readonly object Forge18LzmaFixRegistration = RegisterForge18LzmaFix();

        private static object RegisterForge18LzmaFix()
        {
            EventManager.RegisterClassHandler(
                typeof(MainWindow),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(Forge18LzmaFixLoaded));
            return new object();
        }

        private static void Forge18LzmaFixLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is MainWindow window)
                _ = window.EnsureForge18LzmaAsync();
        }

        private async Task EnsureForge18LzmaAsync()
        {
            try
            {
                RuntimeProfileSettings profile = GetRuntimeProfile();
                if (!profile.Loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                    !profile.Version.Equals("1.8.9", StringComparison.OrdinalIgnoreCase))
                    return;

                string path = Path.Combine(
                    _gamePath,
                    "libraries",
                    "lzma",
                    "lzma",
                    "0.0.1",
                    "lzma-0.0.1.jar");

                const string requiredClass = "LZMA/LzmaInputStream.class";
                const string url = "https://libraries.minecraft.net/lzma/lzma/0.0.1/lzma-0.0.1.jar";

                if (IsJarValid(path, requiredClass))
                {
                    WriteLog($"Forge LZMA 0.0.1 verified: {path}");
                    return;
                }

                string? directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory))
                    throw new InvalidOperationException("Could not determine Forge LZMA directory.");

                Directory.CreateDirectory(directory);
                string temp = Path.Combine(directory, ".lzma-0.0.1-" + Guid.NewGuid().ToString("N") + ".download");

                WriteLog("Forge LZMA 0.0.1 is missing or invalid. Downloading official library...");
                WriteLog($"LZMA URL: {url}");

                try
                {
                    using HttpResponseMessage response = await Http.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead,
                        CancellationToken.None);
                    response.EnsureSuccessStatusCode();

                    await using Stream input = await response.Content.ReadAsStreamAsync();
                    await using FileStream output = new FileStream(
                        temp,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        FileOptions.SequentialScan);
                    await input.CopyToAsync(output, 81920, CancellationToken.None);
                    await output.FlushAsync(CancellationToken.None);

                    if (!IsJarValid(temp, requiredClass))
                        throw new InvalidDataException("Downloaded Forge LZMA JAR does not contain LZMA/LzmaInputStream.class.");

                    File.Move(temp, path, true);

                    if (!IsJarValid(path, requiredClass))
                        throw new InvalidDataException("Forge LZMA installation failed validation.");

                    WriteLog($"Forge LZMA 0.0.1 installed and verified: {path}");
                }
                finally
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
            }
            catch (Exception ex)
            {
                WriteException("FORGE LZMA PRELOAD ERROR", ex);
            }
        }

        private static bool IsJarValid(string path, string requiredEntry)
        {
            if (!File.Exists(path) || new FileInfo(path).Length <= 0)
                return false;

            try
            {
                using FileStream stream = File.OpenRead(path);
                using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
                return archive.GetEntry(requiredEntry) != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
