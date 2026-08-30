using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TopuLauncher;

internal static class ForgeSupport
{
    private static readonly HttpClient Http = new();

    // Forge's official Maven installer artifacts are used instead of the GUI installer.
    // 1.20.1 currently has a latest release on the official Forge site.
    public static string GetForgeVersion(string minecraftVersion)
    {
        return minecraftVersion switch
        {
            "1.20.1" => "47.4.23",
            "1.8.9" => "11.15.1.2318",
            _ => throw new NotSupportedException($"Forge is currently supported by Topu Client for Minecraft {minecraftVersion} only on the versions explicitly configured here.")
        };
    }

    public static async Task InstallAsync(string minecraftDirectory, string minecraftVersion, CancellationToken cancellationToken = default)
    {
        string forgeVersion = GetForgeVersion(minecraftVersion);
        string installerUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{minecraftVersion}-{forgeVersion}/forge-{minecraftVersion}-{forgeVersion}-installer.jar";
        string cacheDirectory = Path.Combine(minecraftDirectory, "topu-cache", "forge");
        Directory.CreateDirectory(cacheDirectory);
        string installerPath = Path.Combine(cacheDirectory, $"forge-{minecraftVersion}-{forgeVersion}-installer.jar");

        if (!File.Exists(installerPath))
        {
            using HttpResponseMessage response = await Http.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream destination = File.Create(installerPath);
            await source.CopyToAsync(destination, cancellationToken);
        }

        // Forge's modern installer is not intended to be executed as a GUI by the launcher.
        // The launcher delegates installation to the official Forge installer in headless mode.
        string java = FindJavaExecutable();
        string arguments = $"-jar \"{installerPath}\" --installServer \"{minecraftDirectory}\"";

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = java,
                Arguments = arguments,
                WorkingDirectory = minecraftDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if (!process.Start())
            throw new InvalidOperationException("Forge installer could not be started.");

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            string error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Forge installer failed with exit code {process.ExitCode}. {error}");
        }
    }

    private static string FindJavaExecutable()
    {
        string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            string candidate = Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
            if (File.Exists(candidate)) return candidate;
        }

        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }
}
