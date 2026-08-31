using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.Forge.Installers;
using CmlLib.Core.Installer.Forge.Versions;

namespace TopuLauncher
{
    internal static class ForgeInstallerExtensions
    {
        public static async Task<string> Install(this ForgeInstaller installer, ForgeVersion version)
        {
            string installedVersion = await installer.Install(version, new ForgeInstallOptions());
            PatchForgeVersionJson(installedVersion);
            return installedVersion;
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
