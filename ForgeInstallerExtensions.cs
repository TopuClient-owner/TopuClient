using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core.Installer.Forge;

namespace TopuLauncher
{
    internal static class ForgeInstallerExtensions
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        public static async Task<string> Install(this ForgeInstaller installer, ForgeVersion version)
        {
            string installedVersion = await installer.Install(version, new ForgeInstallOptions());

            // Forge 1.8.9's installer can skip legacy client libraries because
            // of its old side rules. Repair the files before BuildProcessAsync.
            await RepairForgeLibrariesAsync(installedVersion);
            PatchForgeVersionJson(installedVersion);
            return installedVersion;
        }

        private static async Task RepairForgeLibrariesAsync(string versionName)
        {
            string profilesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TopuClient", "profiles");

            if (!Directory.Exists(profilesRoot) || string.IsNullOrWhiteSpace(versionName))
                return;

            string[] matches = Directory.GetFiles(
                profilesRoot, versionName + ".json", SearchOption.AllDirectories);

            foreach (string jsonPath in matches)
            {
                string? gameRoot = FindGameRoot(jsonPath);
                if (gameRoot == null)
                    continue;

                try
                {
                    using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath));

                    if (document.RootElement.TryGetProperty("libraries", out JsonElement libraries) &&
                        libraries.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement library in libraries.EnumerateArray())
                        {
                            if (!library.TryGetProperty("name", out JsonElement nameElement) ||
                                nameElement.ValueKind != JsonValueKind.String)
                                continue;

                            string? name = nameElement.GetString();
                            if (string.IsNullOrWhiteSpace(name))
                                continue;

                            if (library.TryGetProperty("downloads", out JsonElement downloads) &&
                                downloads.ValueKind == JsonValueKind.Object &&
                                downloads.TryGetProperty("artifact", out JsonElement artifact) &&
                                artifact.ValueKind == JsonValueKind.Object)
                            {
                                await EnsureArtifactAsync(gameRoot, name, artifact);
                            }
                            else
                            {
                                await EnsureLegacyArtifactAsync(gameRoot, name);
                            }
                        }
                    }

                    // Explicitly repair the three legacy jars Forge 1.8.9 needs
                    // before its LaunchWrapper transformers can run.
                    await EnsureKnownJarAsync(
                        gameRoot,
                        "org/ow2/asm/asm-all/5.0.3/asm-all-5.0.3.jar",
                        "org/objectweb/asm/ClassVisitor.class");

                    await EnsureKnownJarAsync(
                        gameRoot,
                        "lzma/lzma/0.0.1/lzma-0.0.1.jar",
                        "LZMA/LzmaInputStream.class");

                    await EnsureKnownJarAsync(
                        gameRoot,
                        "net/minecraft/launchwrapper/1.12/launchwrapper-1.12.jar",
                        "net/minecraft/launchwrapper/Launch.class");

                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Forge dependency repair failed: " + ex);
                }
            }
        }

        private static async Task EnsureArtifactAsync(string gameRoot, string name, JsonElement artifact)
        {
            if (!artifact.TryGetProperty("path", out JsonElement pathElement) ||
                pathElement.ValueKind != JsonValueKind.String)
                return;

            string? relativePath = pathElement.GetString();
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            string destination = Path.Combine(
                gameRoot, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar));

            bool valid = IsNonEmptyFile(destination);

            if (valid && artifact.TryGetProperty("sha1", out JsonElement shaElement) &&
                shaElement.ValueKind == JsonValueKind.String)
            {
                string? expected = shaElement.GetString();
                if (!string.IsNullOrWhiteSpace(expected))
                    valid = string.Equals(
                        await Sha1Async(destination), expected,
                        StringComparison.OrdinalIgnoreCase);
            }

            if (valid)
                return;

            string? url = null;
            if (artifact.TryGetProperty("url", out JsonElement urlElement) &&
                urlElement.ValueKind == JsonValueKind.String)
                url = urlElement.GetString();

            if (string.IsNullOrWhiteSpace(url))
                url = "https://libraries.minecraft.net/" + relativePath.Replace('\\', '/');

            await DownloadAsync(destination, url, name);
        }

        private static async Task EnsureLegacyArtifactAsync(string gameRoot, string name)
        {
            string[] parts = name.Split(':');
            if (parts.Length < 3)
                return;

            string group = parts[0].Replace('.', '/');
            string artifact = parts[1];
            string version = parts[2];
            string classifier = parts.Length > 3 ? "-" + parts[3] : "";
            string fileName = artifact + "-" + version + classifier + ".jar";
            string relativePath = group + "/" + artifact + "/" + version + "/" + fileName;

            await DownloadIfMissingAsync(
                Path.Combine(gameRoot, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar)),
                "https://libraries.minecraft.net/" + relativePath,
                name);
        }

        private static async Task EnsureKnownJarAsync(string gameRoot, string relativePath, string requiredClass)
        {
            string destination = Path.Combine(
                gameRoot, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (IsValidJar(destination, requiredClass))
                return;

            await DownloadAsync(
                destination,
                "https://libraries.minecraft.net/" + relativePath,
                relativePath);

            if (!IsValidJar(destination, requiredClass))
                throw new InvalidDataException("Forge library failed validation: " + relativePath);
        }

        private static async Task DownloadIfMissingAsync(string destination, string url, string name)
        {
            if (IsNonEmptyFile(destination))
                return;
            await DownloadAsync(destination, url, name);
        }

        private static async Task DownloadAsync(string destination, string url, string name)
        {
            string? directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Invalid Forge library path: " + name);

            Directory.CreateDirectory(directory);
            string temp = destination + ".topu-download-" + Guid.NewGuid().ToString("N");

            try
            {
                using HttpResponseMessage response = await Http.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using Stream input = await response.Content.ReadAsStreamAsync();
                await using FileStream output = new FileStream(
                    temp, FileMode.Create, FileAccess.Write, FileShare.None,
                    81920, FileOptions.SequentialScan);
                await input.CopyToAsync(output, 81920);
                await output.FlushAsync();
                output.Close();

                if (!File.Exists(temp) || new FileInfo(temp).Length == 0)
                    throw new InvalidDataException("Downloaded Forge library is empty: " + name);

                File.Move(temp, destination, true);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        private static bool IsNonEmptyFile(string path) =>
            File.Exists(path) && new FileInfo(path).Length > 0;

        private static bool IsValidJar(string path, string requiredClass)
        {
            if (!IsNonEmptyFile(path))
                return false;

            try
            {
                using FileStream stream = File.OpenRead(path);
                using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
                return archive.GetEntry(requiredClass) != null;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string> Sha1Async(string path)
        {
            using SHA1 sha1 = SHA1.Create();
            await using FileStream stream = File.OpenRead(path);
            byte[] hash = await sha1.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string? FindGameRoot(string jsonPath)
        {
            DirectoryInfo? directory = new FileInfo(jsonPath).Directory;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "versions")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            return null;
        }

        private static void PatchForgeVersionJson(string versionName)
        {
            if (string.IsNullOrWhiteSpace(versionName))
                return;

            string profilesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TopuClient", "profiles");
            if (!Directory.Exists(profilesRoot))
                return;

            foreach (string path in Directory.GetFiles(
                profilesRoot, versionName + ".json", SearchOption.AllDirectories))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                    if (!document.RootElement.TryGetProperty("arguments", out JsonElement arguments) ||
                        !arguments.TryGetProperty("jvm", out JsonElement jvm) ||
                        jvm.ValueKind != JsonValueKind.Array)
                        continue;

                    bool changed = false;
                    using MemoryStream memory = new MemoryStream();
                    using (Utf8JsonWriter writer = new Utf8JsonWriter(
                        memory, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();
                        foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        {
                            if (!property.NameEquals("arguments"))
                            {
                                property.WriteTo(writer);
                                continue;
                            }

                            writer.WritePropertyName("arguments");
                            writer.WriteStartObject();
                            foreach (JsonProperty argument in property.Value.EnumerateObject())
                            {
                                if (!argument.NameEquals("jvm"))
                                {
                                    argument.WriteTo(writer);
                                    continue;
                                }

                                writer.WritePropertyName("jvm");
                                writer.WriteStartArray();
                                foreach (JsonElement value in jvm.EnumerateArray())
                                {
                                    if (value.ValueKind == JsonValueKind.String &&
                                        value.GetString() == "-p")
                                    {
                                        writer.WriteStringValue("--module-path");
                                        changed = true;
                                    }
                                    else
                                    {
                                        value.WriteTo(writer);
                                    }
                                }
                                writer.WriteEndArray();
                            }
                            writer.WriteEndObject();
                        }
                        writer.WriteEndObject();
                    }

                    if (changed)
                        File.WriteAllText(path, System.Text.Encoding.UTF8.GetString(memory.ToArray()));

                    return;
                }
                catch { }
            }
        }
    }
}
