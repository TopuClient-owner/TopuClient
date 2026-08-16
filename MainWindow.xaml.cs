using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        private MSession? _session;

        private static readonly HttpClient _httpClient =
            new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true
            })
            {
                DefaultRequestHeaders =
                {
                    {
                        "User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) TopuClient/1.0"
                    }
                }
            };

        private readonly string _configFilePath;

        public MainWindow()
        {
            InitializeComponent();

            string appFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient"
            );

            Directory.CreateDirectory(appFolder);

            _configFilePath = Path.Combine(
                appFolder,
                "username.txt"
            );

            LoadSavedUsername();
        }

        private void LoadSavedUsername()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return;

                string savedUser =
                    File.ReadAllText(_configFilePath).Trim();

                if (string.IsNullOrEmpty(savedUser))
                    return;

                if (UsernameInput != null)
                    UsernameInput.Text = savedUser;

                _session =
                    MSession.CreateOfflineSession(savedUser);
            }
            catch
            {
                // Ignore saved username errors.
            }
        }

        private void SaveUsername(string? username)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(username))
                {
                    File.WriteAllText(
                        _configFilePath,
                        username
                    );
                }
            }
            catch
            {
                // Ignore config save errors.
            }
        }

        private void TitleBar_MouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private void SwitchTab_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button btn ||
                btn.Tag is not string targetTab)
            {
                return;
            }

            TabLaunch.Visibility =
                Visibility.Collapsed;

            TabProfiles.Visibility =
                Visibility.Collapsed;

            TabAccounts.Visibility =
                Visibility.Collapsed;

            Brush defaultColor =
                new SolidColorBrush(
                    Color.FromRgb(136, 136, 136)
                );

            Thickness noBorder =
                new Thickness(0);

            TabLaunchBtn.Foreground =
                defaultColor;

            TabLaunchBtn.BorderThickness =
                noBorder;

            TabProfilesBtn.Foreground =
                defaultColor;

            TabProfilesBtn.BorderThickness =
                noBorder;

            TabAccountsBtn.Foreground =
                defaultColor;

            TabAccountsBtn.BorderThickness =
                noBorder;

            btn.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(0, 255, 136)
                );

            btn.BorderThickness =
                new Thickness(0, 0, 0, 2);

            switch (targetTab)
            {
                case "TabLaunch":
                    TabLaunch.Visibility =
                        Visibility.Visible;
                    break;

                case "TabProfiles":
                    TabProfiles.Visibility =
                        Visibility.Visible;
                    break;

                case "TabAccounts":
                    TabAccounts.Visibility =
                        Visibility.Visible;
                    break;
            }
        }

        private void RamSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (RamLabel != null)
            {
                RamLabel.Text =
                    $"{(int)e.NewValue}GB";
            }
        }

        private void SaveProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            string selectedVer =
                (VersionBox.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?? "1.21.1";

            if (SelectedProfileLabel != null)
            {
                SelectedProfileLabel.Text =
                    $"Ready to launch Fabric {selectedVer}";
            }

            if (StatusText != null)
            {
                StatusText.Text =
                    $"Profile saved: Fabric {selectedVer} with {(int)RamSlider.Value}GB RAM";
            }

            MessageBox.Show(
                "Profile settings saved successfully!",
                "Topu Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void AuthTypeBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (StatusText == null)
                return;

            if (AuthTypeBox.SelectedIndex == 0)
            {
                StatusText.Text =
                    "Auth Mode: Offline / Cracked";
            }
            else
            {
                StatusText.Text =
                    "Auth Mode: Microsoft Official";
            }
        }

        private async void MsLoginBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                StatusText.Text =
                    "Microsoft Login requires interactive browser flow implementation.";

                MessageBox.Show(
                    "Microsoft login integration requires the CmlLib.Core.Auth.Microsoft package references.",
                    "MS Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Microsoft Login Failed!";

                MessageBox.Show(
                    $"MS Login Error:\n{ex.Message}",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                if (MsLoginBtn != null)
                    MsLoginBtn.IsEnabled = true;
            }

            await Task.CompletedTask;
        }

        private void JoinServer_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is Button btn &&
                btn.Tag is string serverIp)
            {
                StatusText.Text =
                    $"Target server queued: {serverIp}";
            }
        }

        private async void SearchModrinth_Click(
            object sender,
            RoutedEventArgs e)
        {
            string query =
                ModSearchInput?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show(
                    "Please enter a mod name to search on Modrinth.",
                    "Mod Search",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            try
            {
                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text =
                        $"Searching Modrinth for '{query}'...";
                }

                string searchUrl =
                    "https://api.modrinth.com/v2/search" +
                    $"?query={Uri.EscapeDataString(query)}";

                string responseString =
                    await _httpClient.GetStringAsync(
                        searchUrl
                    );

                using JsonDocument doc =
                    JsonDocument.Parse(responseString);

                if (!doc.RootElement.TryGetProperty(
                        "hits",
                        out JsonElement hits) ||
                    hits.GetArrayLength() == 0)
                {
                    if (ModSearchStatus != null)
                    {
                        ModSearchStatus.Text =
                            "No compatible Fabric version found on Modrinth.";
                    }

                    MessageBox.Show(
                        "No compatible Fabric version found on Modrinth for your current selection.",
                        "Mod Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }

                JsonElement firstHit =
                    hits[0];

                string modTitle =
                    firstHit.TryGetProperty(
                        "title",
                        out JsonElement titleProp)
                        ? titleProp.GetString() ?? query
                        : query;

                string projectId =
                    firstHit.TryGetProperty(
                        "project_id",
                        out JsonElement idProp)
                        ? idProp.GetString() ?? ""
                        : "";

                if (string.IsNullOrEmpty(projectId))
                    return;

                string targetVer =
                    (VersionBox?.SelectedItem as ComboBoxItem)
                    ?.Content
                    ?.ToString()
                    ?? "1.21.1";

                string versionsUrl =
                    "https://api.modrinth.com/v2/project/" +
                    $"{projectId}/version" +
                    "?loaders=%5B%22fabric%22%5D" +
                    $"&game_versions=%5B%22{Uri.EscapeDataString(targetVer)}%22%5D";

                string versionsResponse =
                    await _httpClient.GetStringAsync(
                        versionsUrl
                    );

                using JsonDocument versionsDoc =
                    JsonDocument.Parse(versionsResponse);

                JsonElement versionRoot =
                    versionsDoc.RootElement;

                if (versionRoot.GetArrayLength() == 0)
                {
                    MessageBox.Show(
                        "No compatible Fabric version found.",
                        "Mod Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }

                JsonElement latestVersion =
                    versionRoot[0];

                if (!latestVersion.TryGetProperty(
                        "files",
                        out JsonElement files) ||
                    files.GetArrayLength() == 0)
                {
                    return;
                }

                string fileUrl = "";
                string fileName =
                    $"{modTitle}.jar";

                foreach (JsonElement file in
                    files.EnumerateArray())
                {
                    if (file.TryGetProperty(
                            "primary",
                            out JsonElement primaryProp) &&
                        primaryProp.GetBoolean())
                    {
                        if (file.TryGetProperty(
                                "url",
                                out JsonElement urlProp))
                        {
                            fileUrl =
                                urlProp.GetString() ?? "";
                        }

                        if (file.TryGetProperty(
                                "filename",
                                out JsonElement nameProp))
                        {
                            fileName =
                                nameProp.GetString()
                                ?? fileName;
                        }

                        break;
                    }
                }

                if (string.IsNullOrEmpty(fileUrl))
                {
                    if (files[0].TryGetProperty(
                            "url",
                            out JsonElement urlProp))
                    {
                        fileUrl =
                            urlProp.GetString() ?? "";
                    }

                    if (files[0].TryGetProperty(
                            "filename",
                            out JsonElement nameProp))
                    {
                        fileName =
                            nameProp.GetString()
                            ?? fileName;
                    }
                }

                if (string.IsNullOrEmpty(fileUrl))
                    return;

                string gamePath =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.ApplicationData),
                        ".topuclient"
                    );

                string modsFolder =
                    Path.Combine(
                        gamePath,
                        "mods"
                    );

                Directory.CreateDirectory(
                    modsFolder
                );

                string destPath =
                    Path.Combine(
                        modsFolder,
                        fileName
                    );

                byte[] modBytes =
                    await _httpClient.GetByteArrayAsync(
                        fileUrl
                    );

                await File.WriteAllBytesAsync(
                    destPath,
                    modBytes
                );

                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text =
                        $"Successfully downloaded: {modTitle}!";
                }

                MessageBox.Show(
                    $"Mod '{modTitle}' successfully added to your mods folder for {targetVer}!",
                    "Modrinth API",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text =
                        "Mod search failed.";
                }

                MessageBox.Show(
                    $"Error searching/downloading mod:\n{ex.Message}",
                    "Modrinth Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void LaunchBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            LaunchBtn.IsEnabled = false;

            try
            {
                foreach (Process proc in
                    Process.GetProcessesByName("javaw"))
                {
                    try
                    {
                        proc.Kill();
                    }
                    catch
                    {
                    }
                }

                foreach (Process proc in
                    Process.GetProcessesByName("java"))
                {
                    try
                    {
                        proc.Kill();
                    }
                    catch
                    {
                    }
                }

                string gamePath =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.ApplicationData),
                        ".topuclient"
                    );

                Directory.CreateDirectory(
                    gamePath
                );

                var path =
                    new MinecraftPath(gamePath);

                string targetVer =
                    (VersionBox.SelectedItem as ComboBoxItem)
                    ?.Content
                    ?.ToString()
                    ?? "1.21.1";

                if (AuthTypeBox.SelectedIndex == 0 ||
                    _session == null ||
                    string.IsNullOrEmpty(
                        _session.AccessToken) ||
                    _session.AccessToken == "0")
                {
                    string inputUser =
                        string.IsNullOrWhiteSpace(
                            UsernameInput.Text)
                            ? "TopuPlayer"
                            : UsernameInput.Text.Trim();

                    _session =
                        MSession.CreateOfflineSession(
                            inputUser
                        );

                    SaveUsername(
                        inputUser
                    );
                }

                string modsFolder =
                    Path.Combine(
                        gamePath,
                        "mods"
                    );

                await EnsureEssentialModsDownloaded(
                    modsFolder,
                    targetVer
                );

                StatusText.Text =
                    $"Setting up Fabric for {targetVer}...";

                string fabricVersionName =
                    await InstallFabricProfileAsync(
                        gamePath,
                        targetVer
                    );

                var launcher =
                    new MinecraftLauncher(path);

                launcher.FileProgressChanged +=
                    (senderObj, args) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusText.Text =
                                $"Checking: {args.Name} ({args.EventType})";
                        });
                    };

                // Do not use ProgressedPercentage here.
                // Your installed CmlLib version does not expose it.
                launcher.ByteProgressChanged +=
                    (senderObj, args) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusText.Text =
                                "Downloading Minecraft files...";
                        });
                    };

                string jarPath =
                    Path.Combine(
                        gamePath,
                        "versions",
                        targetVer,
                        $"{targetVer}.jar"
                    );

                if (!File.Exists(jarPath))
                {
                    StatusText.Text =
                        $"Downloading Official Minecraft {targetVer} files & assets...";

                    await launcher.InstallAsync(
                        targetVer
                    );
                }
                else
                {
                    StatusText.Text =
                        "Game files found. Fast launching...";
                }

                StatusText.Text =
                    "Creating game process...";

                int allocatedRamMb =
                    (int)RamSlider.Value * 1024;

                var launchOption =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = allocatedRamMb
                    };

                var process =
                    await launcher.BuildProcessAsync(
                        fabricVersionName,
                        launchOption
                    );

                bool started =
                    process.Start();

                if (!started)
                {
                    MessageBox.Show(
                        "process.Start() returned false!",
                        "Launch Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
                else
                {
                    StatusText.Text =
                        $"Topu Client ({fabricVersionName}) running as {_session.Username}!";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch Failed!";

                MessageBox.Show(
                    "CRITICAL LAUNCH ERROR:\n\n" +
                    $"{ex.Message}\n\n" +
                    $"Stack Trace:\n{ex.StackTrace}",
                    "Topu Client Crash",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        private async Task<string> InstallFabricProfileAsync(
            string gamePath,
            string mcVersion)
        {
            string loaderVersion =
                "0.19.3";

            string fabricVersionId =
                $"fabric-loader-{loaderVersion}-{mcVersion}";

            string versionFolder =
                Path.Combine(
                    gamePath,
                    "versions",
                    fabricVersionId
                );

            string jsonFile =
                Path.Combine(
                    versionFolder,
                    $"{fabricVersionId}.json"
                );

            if (!File.Exists(jsonFile))
            {
                Directory.CreateDirectory(
                    versionFolder
                );

                string apiUrl =
                    "https://meta.fabricmc.net/v2/versions/loader/" +
                    $"{mcVersion}/{loaderVersion}/profile/json";

                string jsonContent =
                    await _httpClient.GetStringAsync(
                        apiUrl
                    );

                await File.WriteAllTextAsync(
                    jsonFile,
                    jsonContent
                );
            }

            return fabricVersionId;
        }

        private async Task EnsureEssentialModsDownloaded(
            string modsFolder,
            string mcVersion)
        {
            Directory.CreateDirectory(
                modsFolder
            );

            var coreMods =
                new (string Name, string UrlQuery)[]
                {
                    ("fabric-api.jar", "fabric-api"),
                    ("sodium.jar", "sodium"),
                    ("lithium.jar", "lithium"),
                    ("ferritecore.jar", "ferritecore"),
                    ("sodium-extra.jar", "sodium-extra"),
                    ("dynamic-fps.jar", "dynamic-fps")
                };

            foreach (var mod in coreMods)
            {
                string destination =
                    Path.Combine(
                        modsFolder,
                        mod.Name
                    );

                if (File.Exists(destination) &&
                    new FileInfo(destination).Length >= 5000)
                {
                    continue;
                }

                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text =
                            $"Checking/Downloading {mod.Name} for {mcVersion}...";
                    });

                    string searchUrl =
                        "https://api.modrinth.com/v2/search" +
                        $"?query={Uri.EscapeDataString(mod.UrlQuery)}" +
                        "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                    string searchRes =
                        await _httpClient.GetStringAsync(
                            searchUrl
                        );

                    using JsonDocument doc =
                        JsonDocument.Parse(
                            searchRes
                        );

                    if (!doc.RootElement.TryGetProperty(
                            "hits",
                            out JsonElement hits) ||
                        hits.GetArrayLength() == 0)
                    {
                        continue;
                    }

                    string projectId =
                        hits[0].TryGetProperty(
                            "project_id",
                            out JsonElement idProp)
                            ? idProp.GetString() ?? ""
                            : "";

                    if (string.IsNullOrEmpty(projectId))
                        continue;

                    string versionsUrl =
                        "https://api.modrinth.com/v2/project/" +
                        $"{projectId}/version" +
                        "?loaders=%5B%22fabric%22%5D" +
                        $"&game_versions=%5B%22{Uri.EscapeDataString(mcVersion)}%22%5D";

                    string verRes =
                        await _httpClient.GetStringAsync(
                            versionsUrl
                        );

                    using JsonDocument verDoc =
                        JsonDocument.Parse(
                            verRes
                        );

                    JsonElement verArray =
                        verDoc.RootElement;

                    if (verArray.GetArrayLength() == 0)
                        continue;

                    JsonElement latestVerObj =
                        verArray[0];

                    if (!latestVerObj.TryGetProperty(
                            "files",
                            out JsonElement files) ||
                        files.GetArrayLength() == 0)
                    {
                        continue;
                    }

                    string fileUrl = "";

                    foreach (JsonElement file in
                        files.EnumerateArray())
                    {
                        if (file.TryGetProperty(
                                "primary",
                                out JsonElement primaryProp) &&
                            primaryProp.GetBoolean())
                        {
                            if (file.TryGetProperty(
                                    "url",
                                    out JsonElement urlProp))
                            {
                                fileUrl =
                                    urlProp.GetString()
                                    ?? "";
                            }

                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(fileUrl))
                    {
                        if (files[0].TryGetProperty(
                                "url",
                                out JsonElement urlProp))
                        {
                            fileUrl =
                                urlProp.GetString()
                                ?? "";
                        }
                    }

                    if (string.IsNullOrEmpty(fileUrl))
                        continue;

                    byte[] data =
                        await _httpClient.GetByteArrayAsync(
                            fileUrl
                        );

                    await File.WriteAllBytesAsync(
                        destination,
                        data
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"Mod download skipped for {mod.Name}: {ex.Message}"
                    );
                }
            }
        }
    }
}
