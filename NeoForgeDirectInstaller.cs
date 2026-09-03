using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.Installer.NeoForge.Versions;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private async Task<string> InstallNeoForgeRuntimeAsync(
            string minecraftVersion,
            string javaPath,
            MinecraftLauncher launcher)
        {
            const string manifestUrl =
                "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";

            using HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("TopuClient/1.0");

            WriteLog("Downloading NeoForge version manifest...");
            using HttpResponseMessage response = await client.GetAsync(
                manifestUrl,
                CancellationToken.None);
            response.EnsureSuccessStatusCode();

            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            if (!document.RootElement.TryGetProperty("versions", out JsonElement versions) ||
                versions.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "NeoForge returned an invalid version manifest.");
            }

            string prefix = minecraftVersion.StartsWith("1.", StringComparison.Ordinal)
                ? minecraftVersion.Substring(2) + "."
                : minecraftVersion + ".";

            string? selectedVersion = versions
                .EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => x!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(x => !x!.Contains('+'))
                .LastOrDefault();

            if (string.IsNullOrWhiteSpace(selectedVersion))
            {
                throw new InvalidOperationException(
                    $"No stable NeoForge version was found for Minecraft {minecraftVersion}.");
            }

            WriteLog($"Topu selected NeoForge {selectedVersion} for Minecraft {minecraftVersion}.");

            NeoForgeVersion neoForgeVersion = new NeoForgeVersion(
                minecraftVersion,
                selectedVersion);

            NeoForgeInstaller installer = new NeoForgeInstaller(launcher);
            NeoForgeInstallOptions options = new NeoForgeInstallOptions
            {
                JavaPath = javaPath,
                SkipIfAlreadyInstalled = true,
                CancellationToken = CancellationToken.None,
                InstallerOutput = new Progress<string>(line => WriteLog("[NeoForge] " + line))
            };

            return await installer.Install(neoForgeVersion, options);
        }
    }
}
