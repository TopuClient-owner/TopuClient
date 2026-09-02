using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.Forge.Installers;
using CmlLib.Core.Installer.Forge.Versions;

namespace TopuLauncher
{
    internal static class ForgeInstallerExtensions
    {
        private static readonly HttpClient Http = new HttpClient();

        public static async Task<string> Install(this ForgeInstaller installer, ForgeVersion version)
        {
            string installedVersion = await installer.Install(version, new ForgeInstallOptions());

            // CmlLib's legacy Forge installer can leave old Forge libraries absent
            // even though they are declared by the generated Forge version JSON.
            // Repair every declared artifact before the process is built so the
            // Java 8 LaunchWrapper classpath is actually usable.
            await RepairForgeLibrariesAsync(installedVersion);

            PatchForgeVersionJson(installedVersion);
            return installedVersion;
        }

        private static async Task RepairForgeLibrariesAsync(string versionName)
        {
            if (string.IsNullOrWhiteSpace(versionName))
                return;

            string profilesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TopuClient",
                "profiles");

            if (!Directory.Exists(profilesRoot))
                return;

            string fileName = versionName + ".json";
            string[] matches = Directory.GetFiles(profilesRoot, fileName, SearchOption.AllDirectories);

            foreach (string jsonPath in matches)
            {
                try
                {
                    string json = await File.ReadAllTextAsync(jsonPath);
                    using JsonDocument document = JsonDocument.Parse(json);

                    if (!document.RootElement.TryGetProperty("libraries", out JsonElement libraries) ||
                        libraries.ValueKind != JsonValueKind.Array)
                        continue;

                    string? gameRoot = FindGameRoot(jsonPath);
                    if (gameRoot == null)
                        continue;

                    foreach (JsonElement library in libraries.EnumerateArray())
                    {
                        if (!library.TryGetProperty("name", out JsonElement nameElement) ||
                            nameElement.ValueKind != JsonValueKind.String)
                            continue;

                        string? name = nameElement.GetString();
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        // Old Forge JSON normally uses Maven coordinates:
                        // group:artifact:version[:classifier].
                        // The artifact download entry, when present, is preferred.
                        if (library.TryGetProperty("downloads", out JsonElement downloads) &&
                            downloads.ValueKind == JsonValueKind.Object &&
                            downloads.TryGetProperty("artifact", out JsonElement artifact) &&
                            artifact.ValueKind == JsonValueKind.Object)
                        {
                            await EnsureArtifactAsync(gameRoot, name, artifact);
                        }
                        else
                        {
                            await EnsureLegacyMavenArtifactAsync(gameRoot, name);
                        }
                    }

                    return;
                }
                catch (Exception ex)
                {
                    // Dependency repair should report the problem, but do not hide
                    // the original Forge installer error if the JSON is temporarily locked.
                    System.Diagnostics.Debug.WriteLine(
                        "Forge dependency repair failed: " + ex);
                }
            }
        }

        private static async Task EnsureArtifactAsync(
            string gameRoot,
            string libraryName,
            JsonElement artifact)
        {
            if (!artifact.TryGetProperty("path", out JsonElement pathElement) ||
                pathElement.ValueKind != JsonValueKind.String)
                return;

            string? relativePath = pathElement.GetString();
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            string destination = Path.Combine(
                gameRoot,
                "libraries",
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            bool valid = File.Exists(destination) && new FileInfo(destination).Length > 0;

            if (valid && artifact.TryGetProperty("sha1", out JsonElement shaElement) &&
                shaElement.ValueKind == JsonValueKind.String)
            {
                string? expectedSha1 = shaElement.GetString();
                if (!string.IsNullOrWhiteSpace(expectedSha1))
                    valid = string.Equals(
                        await ComputeSha1Async(destination),
                        expectedSha1,
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

            await DownloadArtifactAsync(destination, url, libraryName);
        }

        private static async Task EnsureLegacyMavenArtifactAsync(
            string gameRoot,
            string libraryName)
        {
            string[] parts = libraryName.Split(':');
            if (parts.Length < 3)
                return;

            string groupPath = parts[0].Replace('.', '/');
            string artifact = parts[1];
            string version = parts[2];
            string? classifier = parts.Length >= 4 ? parts[3] : null;

            string fileName = artifact + "-" + version +
                (string.IsNullOrWhiteSpace(classifier) ? "" : "-" + classifier) +
                ".jar";

            string relativePath = groupPath + "/" + artifact + "/" + version + "/" + fileName;
            string destination = Path.Combine(
                gameRoot,
                "libraries",
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(destination) && new FileInfo(destination).Length > 0)
                return;

            string url = "https://libraries.minecraft.net/" + relativePath;
            await DownloadArtifactAsync(destination, url, libraryName);
        }

        private static async Task DownloadArtifactAsync(
            string destination,
            string url,
            string libraryName)
        {
            string? directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Could not determine Forge library directory for " + libraryName);

            Directory.CreateDirectory(directory);

            string temporary = destination + ".topu-download-" + Guid.NewGuid().ToString("N");

            try
            {
                using HttpResponseMessage response = await Http.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using Stream input = await response.Content.ReadAsStreamAsync();
                await using FileStream output = new FileStream(
                    temporary,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.SequentialScan);

                await input.CopyToAsync(output, 81920);
                await output.FlushAsync();
                output.Close();

                if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                    throw new InvalidDataException("Downloaded Forge library is empty: " + libraryName);

                if (File.Exists(destination))
                    File.Delete(destination);

                File.Move(temporary, destination);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }

        private static string? FindGameRoot(string jsonPath)
        {
            DirectoryInfo? directory = new FileInfo(jsonPath).Directory;
            while (directory != null)
            {
                string libraries = Path.Combine(directory.FullName, "libraries");
                if (Directory.Exists(libraries))
                    return directory.FullName;

                directory = directory.Parent;
            }

            // If the libraries directory does not exist yet, the profile root is
            // the directory containing the versions directory, when present.
            directory = new FileInfo(jsonPath).Directory;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "versions")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }

        private static async Task<string> ComputeSha1Async(string path)
        {
            using System.Security.Cryptography.SHA1 sha1 =
                System.Security.Cryptography.SHA1.Create();
            await using FileStream stream = File.OpenRead(path);
            byte[] hash = await sha1.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static void PatchForgeVersionJson(string versionName)
        {
            if (string.IsNullOrWhiteSpace(versionName))
                return;

            string profilesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TopuClient",
                "profiles");

            if (!Directory.Exists(profilesRoot))
                return;

            string fileName = versionName + ".json";
            string[] matches = Directory.GetFiles(profilesRoot, fileName, SearchOption.AllDirectories);

            foreach (string path in matches)
            {
                try
                {
                    string json = File.ReadAllText(path);
                    using JsonDocument document = JsonDocument.Parse(json);

                    if (!document.RootElement.TryGetProperty("arguments", out JsonElement arguments) ||
                        !arguments.TryGetProperty("jvm", out JsonElement jvm) ||
                        jvm.ValueKind != JsonValueKind.Array)
                        continue;

                    bool changed = false;
                    List<JsonElementValue> values = new List<JsonElementValue>();
                    foreach (JsonElement element in jvm.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String &&
                            string.Equals(element.GetString(), "-p", StringComparison.Ordinal))
                        {
                            values.Add(new JsonElementValue("--module-path"));
                            changed = true;
                        }
                        else
                        {
                            values.Add(new JsonElementValue(element));
                        }
                    }

                    if (!changed)
                        continue;

                    using MemoryStream stream = new MemoryStream();
                    using (Utf8JsonWriter writer = new Utf8JsonWriter(
                        stream,
                        new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();
                        foreach (JsonProperty property in document.RootElement.EnumerateObject())
                        {
                            if (property.NameEquals("arguments"))
                            {
                                writer.WritePropertyName("arguments");
                                writer.WriteStartObject();
                                foreach (JsonProperty argumentProperty in property.Value.EnumerateObject())
                                {
                                    if (argumentProperty.NameEquals("jvm"))
                                    {
                                        writer.WritePropertyName("jvm");
                                        writer.WriteStartArray();
                                        foreach (JsonElementValue value in values)
                                            value.WriteTo(writer);
                                        writer.WriteEndArray();
                                    }
                                    else
                                    {
                                        argumentProperty.WriteTo(writer);
                                    }
                                }
                                writer.WriteEndObject();
                            }
                            else
                            {
                                property.WriteTo(writer);
                            }
                        }
                        writer.WriteEndObject();
                    }

                    File.WriteAllText(path, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
                    return;
                }
                catch
                {
                    // Do not prevent Forge installation if a profile JSON is locked.
                }
            }
        }

        private readonly struct JsonElementValue
        {
            private readonly JsonElement _element;
            private readonly string? _string;
            private readonly bool _isString;

            public JsonElementValue(string value)
            {
                _element = default;
                _string = value;
                _isString = true;
            }

            public JsonElementValue(JsonElement element)
            {
                _element = element.Clone();
                _string = null;
                _isString = false;
            }

            public void WriteTo(Utf8JsonWriter writer)
            {
                if (_isString)
                    writer.WriteStringValue(_string);
                else
                    _element.WriteTo(writer);
            }
        }
    }
}