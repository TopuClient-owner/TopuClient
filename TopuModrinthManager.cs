using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TopuLauncher
{
    // Full Modrinth browser for the Profiles & Mods tab.
    // Keeps the existing launcher logic intact and provides browsing for mods and modpacks.
    public partial class MainWindow
    {
        private static readonly object ModrinthManagerHook = RegisterModrinthManagerHook();
        private static readonly HttpClient ModrinthHttp = CreateModrinthHttpClient();

        private static HttpClient CreateModrinthHttpClient()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TopuClient/1.0 (Modrinth Manager)");
            return client;
        }

        private static object RegisterModrinthManagerHook()
        {
            EventManager.RegisterClassHandler(
                typeof(Button),
                Button.ClickEvent,
                new RoutedEventHandler(ModrinthManagerButtonClick),
                true);
            return new object();
        }

        private static void ModrinthManagerButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button button)
                return;

            string content = button.Content?.ToString() ?? string.Empty;
            if (!content.Equals("Search & Add", StringComparison.OrdinalIgnoreCase))
                return;

            if (Window.GetWindow(button) is MainWindow window)
            {
                e.Handled = true;
                window.OpenModrinthManager();
            }
        }

        private void OpenModrinthManager()
        {
            ModrinthBrowserWindow window = new ModrinthBrowserWindow(this);
            window.Owner = this;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private sealed class ModrinthProject
        {
            public string Slug { get; init; } = "";
            public string Title { get; init; } = "";
            public string Description { get; init; } = "";
            public string IconUrl { get; init; } = "";
            public string ProjectType { get; init; } = "mod";
            public int Downloads { get; init; }
            public string[] Categories { get; init; } = Array.Empty<string>();
        }

        private sealed class ModrinthBrowserWindow : Window
        {
            private readonly MainWindow _launcher;
            private readonly WrapPanel _results;
            private readonly TextBox _search;
            private readonly ComboBox _type;
            private readonly ComboBox _sort;
            private readonly TextBlock _status;
            private readonly TextBlock _title;

            public ModrinthBrowserWindow(MainWindow launcher)
            {
                _launcher = launcher;

                Title = "Topu Client • Modrinth Manager";
                Width = 900;
                Height = 650;
                MinWidth = 760;
                MinHeight = 540;
                Background = new SolidColorBrush(Color.FromRgb(16, 17, 20));
                Foreground = Brushes.White;
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;

                Grid root = new Grid();
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Border header = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(21, 23, 27)),
                    Padding = new Thickness(22, 18, 22, 16),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(42, 45, 52)),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                StackPanel headerStack = new StackPanel();
                StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal };
                _title = new TextBlock { Text = "Modrinth Manager", FontSize = 23, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
                titleRow.Children.Add(_title);
                TextBlock badge = new TextBlock
                {
                    Text = "  MODS + MODPACKS",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136)),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 5, 0, 0)
                };
                titleRow.Children.Add(badge);
                headerStack.Children.Add(titleRow);
                headerStack.Children.Add(new TextBlock
                {
                    Text = "Browse Modrinth without leaving Topu Client. Install compatible mods or complete modpacks.",
                    Foreground = new SolidColorBrush(Color.FromRgb(125, 131, 142)),
                    FontSize = 11,
                    Margin = new Thickness(0, 5, 0, 0)
                });
                header.Child = headerStack;
                Grid.SetRow(header, 0);
                root.Children.Add(header);

                Grid controls = new Grid { Margin = new Thickness(22, 14, 22, 12) };
                controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                _search = MakeTextBox("Search mods, modpacks, Sodium, Iris, etc...");
                _search.Margin = new Thickness(0, 0, 9, 0);
                Grid.SetColumn(_search, 0);
                controls.Children.Add(_search);

                _type = MakeCombo("Mods");
                _type.Items.Add("Mods");
                _type.Items.Add("Modpacks");
                _type.Items.Add("All");
                _type.SelectedIndex = 0;
                _type.Width = 105;
                _type.Margin = new Thickness(0, 0, 8, 0);
                Grid.SetColumn(_type, 1);
                controls.Children.Add(_type);

                _sort = MakeCombo("Relevance");
                foreach (string item in new[] { "Relevance", "Downloads", "Updated", "Newest" }) _sort.Items.Add(item);
                _sort.SelectedIndex = 0;
                _sort.Width = 110;
                _sort.Margin = new Thickness(0, 0, 8, 0);
                Grid.SetColumn(_sort, 2);
                controls.Children.Add(_sort);

                Button searchButton = MakeButton("Search", true);
                searchButton.Width = 92;
                searchButton.Click += async (_, _) => await SearchAsync();
                Grid.SetColumn(searchButton, 3);
                controls.Children.Add(searchButton);

                _search.KeyDown += async (_, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Enter)
                        await SearchAsync();
                };

                Grid.SetRow(controls, 1);
                root.Children.Add(controls);

                ScrollViewer scroll = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Margin = new Thickness(22, 0, 22, 8)
                };
                _results = new WrapPanel { Orientation = Orientation.Horizontal };
                scroll.Content = _results;
                Grid.SetRow(scroll, 2);
                root.Children.Add(scroll);

                Border footer = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(12, 14, 17)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(38, 41, 47)),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(22, 10, 22, 10)
                };
                _status = new TextBlock { Text = "Search Modrinth to get started.", Foreground = new SolidColorBrush(Color.FromRgb(119, 125, 136)), FontSize = 10 };
                footer.Child = _status;
                Grid.SetRow(footer, 3);
                root.Children.Add(footer);

                Content = root;
                Loaded += async (_, _) => await SearchAsync();
            }

            private static TextBox MakeTextBox(string placeholder)
            {
                return new TextBox
                {
                    Text = "",
                    ToolTip = placeholder,
                    Height = 38,
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = new SolidColorBrush(Color.FromRgb(17, 19, 24)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(48, 52, 60)),
                    BorderThickness = new Thickness(1),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    FontSize = 12
                };
            }

            private static ComboBox MakeCombo(string text)
            {
                return new ComboBox
                {
                    Height = 38,
                    Background = new SolidColorBrush(Color.FromRgb(17, 19, 24)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(48, 52, 60)),
                    ToolTip = text
                };
            }

            private static Button MakeButton(string text, bool green = false)
            {
                return new Button
                {
                    Content = text,
                    Height = 38,
                    Padding = new Thickness(14, 0, 14, 0),
                    Background = new SolidColorBrush(green ? Color.FromRgb(0, 255, 136) : Color.FromRgb(34, 37, 43)),
                    Foreground = new SolidColorBrush(green ? Color.FromRgb(5, 15, 10) : Colors.White),
                    BorderBrush = new SolidColorBrush(green ? Color.FromRgb(0, 255, 136) : Color.FromRgb(52, 56, 64)),
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
            }

            private async Task SearchAsync()
            {
                try
                {
                    _status.Text = "Loading Modrinth...";
                    _results.Children.Clear();

                    string query = _search.Text.Trim();
                    string projectType = _type.SelectedItem?.ToString() ?? "Mods";
                    string sort = _sort.SelectedItem?.ToString() ?? "Relevance";
                    string index = sort switch
                    {
                        "Downloads" => "downloads",
                        "Updated" => "updated",
                        "Newest" => "newest",
                        _ => "relevance"
                    };

                    string facets = projectType switch
                    {
                        "Modpacks" => "[\"project_type:modpack\"]",
                        "All" => "[\"project_type:mod\",\"project_type:modpack\"]",
                        _ => "[\"project_type:mod\"]"
                    };

                    string url = "https://api.modrinth.com/v2/search?limit=40&index=" + Uri.EscapeDataString(index) +
                                 "&facets=" + Uri.EscapeDataString(facets) +
                                 "&query=" + Uri.EscapeDataString(query);

                    using HttpResponseMessage response = await ModrinthHttp.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(json);

                    List<ModrinthProject> projects = new List<ModrinthProject>();
                    foreach (JsonElement hit in doc.RootElement.GetProperty("hits").EnumerateArray())
                    {
                        projects.Add(new ModrinthProject
                        {
                            Slug = hit.TryGetProperty("slug", out JsonElement slug) ? slug.GetString() ?? "" : "",
                            Title = hit.TryGetProperty("title", out JsonElement title) ? title.GetString() ?? "Untitled" : "Untitled",
                            Description = hit.TryGetProperty("description", out JsonElement desc) ? desc.GetString() ?? "" : "",
                            IconUrl = hit.TryGetProperty("icon_url", out JsonElement icon) ? icon.GetString() ?? "" : "",
                            ProjectType = hit.TryGetProperty("project_type", out JsonElement type) ? type.GetString() ?? "mod" : "mod",
                            Downloads = hit.TryGetProperty("downloads", out JsonElement downloads) ? downloads.GetInt32() : 0,
                            Categories = hit.TryGetProperty("categories", out JsonElement categories) ? categories.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).Take(4).ToArray() : Array.Empty<string>()
                        });
                    }

                    foreach (ModrinthProject project in projects)
                        _results.Children.Add(CreateProjectCard(project));

                    _status.Text = $"{projects.Count} results • Minecraft {GetCurrentVersion()} • {GetCurrentLoader()}";
                }
                catch (Exception ex)
                {
                    _status.Text = "Could not load Modrinth: " + ex.Message;
                }
            }

            private Border CreateProjectCard(ModrinthProject project)
            {
                Border card = new Border
                {
                    Width = 265,
                    Margin = new Thickness(0, 0, 12, 12),
                    Padding = new Thickness(13),
                    Background = new SolidColorBrush(Color.FromRgb(25, 27, 32)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(43, 46, 53)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12)
                };

                Grid grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                StackPanel top = new StackPanel { Orientation = Orientation.Horizontal };
                Border icon = new Border
                {
                    Width = 54,
                    Height = 54,
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(Color.FromRgb(32, 35, 41))
                };
                if (!string.IsNullOrWhiteSpace(project.IconUrl))
                {
                    try
                    {
                        ImageBrush brush = new ImageBrush(new BitmapImage(new Uri(project.IconUrl))) { Stretch = Stretch.UniformToFill };
                        icon.Background = brush;
                    }
                    catch { }
                }
                top.Children.Add(icon);

                StackPanel heading = new StackPanel { Margin = new Thickness(10, 1, 0, 0), Width = 170 };
                heading.Children.Add(new TextBlock { Text = project.Title, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis });
                heading.Children.Add(new TextBlock { Text = project.ProjectType.Equals("modpack", StringComparison.OrdinalIgnoreCase) ? "MODPACK" : "MOD", Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136)), FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 3, 0, 0) });
                heading.Children.Add(new TextBlock { Text = FormatDownloads(project.Downloads) + " downloads", Foreground = new SolidColorBrush(Color.FromRgb(105, 111, 122)), FontSize = 9, Margin = new Thickness(0, 2, 0, 0) });
                top.Children.Add(heading);
                grid.Children.Add(top);

                TextBlock description = new TextBlock
                {
                    Text = project.Description,
                    Foreground = new SolidColorBrush(Color.FromRgb(169, 174, 184)),
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 58,
                    Margin = new Thickness(0, 11, 0, 9)
                };
                Grid.SetRow(description, 1);
                grid.Children.Add(description);

                Button install = MakeButton(project.ProjectType.Equals("modpack", StringComparison.OrdinalIgnoreCase) ? "Install Modpack" : "Add Mod", true);
                install.Height = 34;
                install.Click += async (_, _) => await InstallProjectAsync(project, install);
                Grid.SetRow(install, 2);
                grid.Children.Add(install);

                card.Child = grid;
                return card;
            }

            private async Task InstallProjectAsync(ModrinthProject project, Button button)
            {
                try
                {
                    button.IsEnabled = false;
                    button.Content = "Checking...";
                    _status.Text = "Finding a compatible " + (project.ProjectType == "modpack" ? "modpack" : "mod") + " version for " + GetCurrentVersion() + "...";

                    string version = GetCurrentVersion();
                    string loader = GetCurrentLoader();
                    string facets = project.ProjectType.Equals("modpack", StringComparison.OrdinalIgnoreCase)
                        ? ""
                        : "&loaders=[\"" + Uri.EscapeDataString(loader.ToLowerInvariant()) + "\"]";
                    string url = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(project.Slug) + "/version?game_versions=[\"" + Uri.EscapeDataString(version) + "\"]" + facets;

                    using HttpResponseMessage response = await ModrinthHttp.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement? selected = null;
                    foreach (JsonElement item in doc.RootElement.EnumerateArray())
                    {
                        if (selected == null)
                            selected = item;

                        if (item.TryGetProperty("files", out JsonElement files) && files.GetArrayLength() > 0)
                        {
                            selected = item;
                            break;
                        }
                    }

                    if (selected == null)
                        throw new InvalidOperationException("No compatible Modrinth version was found.");

                    JsonElement selectedVersion = selected.Value;
                    if (project.ProjectType.Equals("modpack", StringComparison.OrdinalIgnoreCase))
                        await InstallModpackAsync(project, selectedVersion);
                    else
                        await InstallModAsync(project, selectedVersion);

                    button.Content = "✓ Installed";
                    _status.Text = project.Title + " installed successfully.";
                }
                catch (Exception ex)
                {
                    button.Content = "Install failed";
                    _status.Text = "Install failed: " + ex.Message;
                    _launcher.WriteException("MODRINTH MANAGER ERROR", ex);
                }
                finally
                {
                    button.IsEnabled = true;
                }
            }

            private async Task InstallModAsync(ModrinthProject project, JsonElement version)
            {
                JsonElement file = FindPrimaryFile(version.GetProperty("files"));
                string downloadUrl = file.GetProperty("url").GetString() ?? throw new InvalidOperationException("Mod file has no download URL.");
                string filename = file.GetProperty("filename").GetString() ?? (project.Slug + ".jar");
                string mods = Path.Combine(_launcher._gamePath, "mods");
                Directory.CreateDirectory(mods);
                string destination = Path.Combine(mods, Sanitize(filename));
                _status.Text = "Downloading " + project.Title + "...";
                byte[] bytes = await ModrinthHttp.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(destination, bytes);
                _launcher.WriteLog("Installed Modrinth mod from manager: " + project.Title);
            }

            private async Task InstallModpackAsync(ModrinthProject project, JsonElement version)
            {
                JsonElement file = FindMrpackFile(version.GetProperty("files"));
                string downloadUrl = file.GetProperty("url").GetString() ?? throw new InvalidOperationException("Modpack has no download URL.");
                string temp = Path.Combine(Path.GetTempPath(), "TopuClient_" + Guid.NewGuid().ToString("N") + ".mrpack");
                try
                {
                    _status.Text = "Downloading modpack " + project.Title + "...";
                    byte[] bytes = await ModrinthHttp.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(temp, bytes);

                    string gameRoot = _launcher._gamePath;
                    Directory.CreateDirectory(gameRoot);
                    using ZipArchive archive = ZipFile.OpenRead(temp);
                    ZipArchiveEntry? indexEntry = archive.GetEntry("modrinth.index.json");
                    if (indexEntry == null)
                        throw new InvalidDataException("This is not a valid Modrinth .mrpack file.");

                    using StreamReader reader = new StreamReader(indexEntry.Open());
                    string indexJson = await reader.ReadToEndAsync();
                    using JsonDocument indexDoc = JsonDocument.Parse(indexJson);

                    int installed = 0;
                    JsonElement files = indexDoc.RootElement.GetProperty("files");
                    foreach (JsonElement item in files.EnumerateArray())
                    {
                        string path = item.GetProperty("path").GetString() ?? "";
                        if (string.IsNullOrWhiteSpace(path) || path.Contains("..", StringComparison.Ordinal)) continue;
                        if (!item.TryGetProperty("downloads", out JsonElement downloads) || downloads.GetArrayLength() == 0) continue;
                        string remote = downloads[0].GetString() ?? "";
                        if (string.IsNullOrWhiteSpace(remote)) continue;
                        string destination = Path.Combine(gameRoot, path.Replace('/', Path.DirectorySeparatorChar));
                        string? directory = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                        byte[] fileBytes = await ModrinthHttp.GetByteArrayAsync(remote);
                        await File.WriteAllBytesAsync(destination, fileBytes);
                        installed++;
                        _status.Text = $"Installing {project.Title}... {installed}/{files.GetArrayLength()}";
                    }

                    ZipArchiveEntry[] overrides = archive.Entries.Where(x => x.FullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase) && !x.FullName.EndsWith("/", StringComparison.Ordinal)).ToArray();
                    foreach (ZipArchiveEntry entry in overrides)
                    {
                        string relative = entry.FullName.Substring("overrides/".Length);
                        if (string.IsNullOrWhiteSpace(relative) || relative.Contains("..", StringComparison.Ordinal)) continue;
                        string destination = Path.Combine(gameRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                        string? directory = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                        entry.ExtractToFile(destination, true);
                    }

                    _launcher.WriteLog("Installed Modrinth modpack: " + project.Title);
                    _launcher.WriteLog("Modpack files installed: " + installed);
                }
                finally
                {
                    try { File.Delete(temp); } catch { }
                }
            }

            private string GetCurrentVersion()
            {
                string version = _launcher.GetRuntimeProfile().Version;
                return string.IsNullOrWhiteSpace(version) ? "1.21.1" : version;
            }

            private string GetCurrentLoader()
            {
                string loader = _launcher.GetRuntimeProfile().Loader;
                return string.IsNullOrWhiteSpace(loader) ? "Fabric" : loader;
            }

            private static JsonElement FindPrimaryFile(JsonElement files)
            {
                foreach (JsonElement file in files.EnumerateArray())
                {
                    if (file.TryGetProperty("primary", out JsonElement primary) && primary.GetBoolean())
                        return file;
                }
                return files.EnumerateArray().First();
            }

            private static JsonElement FindMrpackFile(JsonElement files)
            {
                foreach (JsonElement file in files.EnumerateArray())
                {
                    string filename = file.TryGetProperty("filename", out JsonElement name) ? name.GetString() ?? "" : "";
                    if (filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)) return file;
                }
                return files.EnumerateArray().First();
            }

            private static string Sanitize(string filename)
            {
                foreach (char c in Path.GetInvalidFileNameChars()) filename = filename.Replace(c, '_');
                return filename;
            }

            private static string FormatDownloads(int value)
            {
                if (value >= 1_000_000) return (value / 1_000_000d).ToString("0.0") + "M";
                if (value >= 1_000) return (value / 1_000d).ToString("0.0") + "K";
                return value.ToString();
            }
        }
    }
}
