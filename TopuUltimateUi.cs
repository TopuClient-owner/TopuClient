using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace TopuLauncher
{
    // Topu's richer management center. This is deliberately isolated from MainWindow.xaml
    // so the existing launcher layout and launch logic remain untouched.
    public partial class MainWindow
    {
        private static readonly object TopuUltimateUiRegistration = RegisterTopuUltimateUi();

        private static object RegisterTopuUltimateUi()
        {
            EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent,
                new RoutedEventHandler(TopuUltimateButtonClick), true);
            return new object();
        }

        private static void TopuUltimateButtonClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button button) return;
            string text = button.Content?.ToString() ?? "";
            if (!text.Contains("Profiles & Mods", StringComparison.OrdinalIgnoreCase)) return;
            if (Window.GetWindow(button) is not MainWindow launcher) return;

            e.Handled = true;
            launcher.OpenTopuUltimateCenter();
        }

        private void OpenTopuUltimateCenter()
        {
            TopuUltimateCenterWindow window = new TopuUltimateCenterWindow(this)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }

        private sealed class TopuUltimateCenterWindow : Window
        {
            private static readonly HttpClient Http = CreateHttp();
            private readonly MainWindow _launcher;
            private readonly Grid _content;
            private readonly TextBlock _pageTitle;
            private readonly TextBlock _pageSubtitle;
            private readonly TextBlock _status;
            private readonly TextBox _search;
            private readonly WrapPanel _cards;
            private readonly ComboBox _type;
            private readonly ComboBox _sort;

            private static HttpClient CreateHttp()
            {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TopuClient/1.0");
                return client;
            }

            public TopuUltimateCenterWindow(MainWindow launcher)
            {
                _launcher = launcher;
                Title = "Topu Client • Control Center";
                Width = 1120;
                Height = 720;
                MinWidth = 900;
                MinHeight = 600;
                Background = Brush("#0D0F12");
                Foreground = Brushes.White;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                Grid root = new Grid();
                root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(225) });
                root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Border sidebar = new Border
                {
                    Background = Brush("#13161A"),
                    BorderBrush = Brush("#272B32"),
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    Padding = new Thickness(16)
                };
                Grid.SetColumn(sidebar, 0);

                StackPanel side = new StackPanel();
                StackPanel logo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(3, 8, 0, 28) };
                logo.Children.Add(new Border
                {
                    Width = 34, Height = 34, CornerRadius = new CornerRadius(9),
                    Background = Brush("#0A3020"), BorderBrush = Brush("#00FF88"), BorderThickness = new Thickness(1),
                    Child = new TextBlock { Text = "⚡", Foreground = Brush("#00FF88"), FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                });
                StackPanel logoText = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                logoText.Children.Add(new TextBlock { Text = "TOPU CLIENT", FontSize = 14, FontWeight = FontWeights.Bold });
                logoText.Children.Add(new TextBlock { Text = "CONTROL CENTER", FontSize = 8, FontWeight = FontWeights.Bold, Foreground = Brush("#00FF88"), Margin = new Thickness(0, 2, 0, 0) });
                logo.Children.Add(logoText);
                side.Children.Add(logo);

                side.Children.Add(Label("MANAGEMENT"));
                side.Children.Add(NavButton("⌂   Overview", "overview"));
                side.Children.Add(NavButton("◈   Discover Mods", "mods"));
                side.Children.Add(NavButton("▣   Modpacks", "modpacks"));
                side.Children.Add(NavButton("☷   Installed Mods", "installed"));
                side.Children.Add(NavButton("⚙   Performance", "performance"));
                side.Children.Add(NavButton("◉   Profiles", "profiles"));

                side.Children.Add(new Border { Height = 1, Background = Brush("#292D34"), Margin = new Thickness(2, 22, 2, 14) });
                side.Children.Add(Label("CURRENT INSTANCE"));
                Border instance = new Border { Background = Brush("#0F1215"), BorderBrush = Brush("#252A31"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(11), Margin = new Thickness(0, 7, 0, 0) };
                StackPanel instanceStack = new StackPanel();
                instanceStack.Children.Add(new TextBlock { Text = SafeProfileName(), FontSize = 13, FontWeight = FontWeights.Bold });
                instanceStack.Children.Add(new TextBlock { Text = SafeProfileSummary(), FontSize = 9, Foreground = Brush("#818793"), Margin = new Thickness(0, 4, 0, 0) });
                instance.Child = instanceStack;
                side.Children.Add(instance);

                sidebar.Child = side;
                root.Children.Add(sidebar);

                Grid main = new Grid { Margin = new Thickness(28, 24, 28, 18) };
                main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(main, 1);

                StackPanel heading = new StackPanel();
                _pageTitle = new TextBlock { Text = "Overview", FontSize = 28, FontWeight = FontWeights.Bold };
                _pageSubtitle = new TextBlock { Text = "Everything you need to manage your Topu Client instance.", Foreground = Brush("#7E848F"), FontSize = 11, Margin = new Thickness(0, 5, 0, 0) };
                heading.Children.Add(_pageTitle);
                heading.Children.Add(_pageSubtitle);
                Grid.SetRow(heading, 0);
                main.Children.Add(heading);

                Grid toolbar = new Grid { Margin = new Thickness(0, 20, 0, 14) };
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                _search = TextBox("Search Modrinth projects...");
                Grid.SetColumn(_search, 0);
                toolbar.Children.Add(_search);
                _type = Combo("Mods", "Modpacks", "All");
                _type.Width = 105; _type.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(_type, 1); toolbar.Children.Add(_type);
                _sort = Combo("Relevance", "Downloads", "Updated", "Newest");
                _sort.Width = 115; _sort.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(_sort, 2); toolbar.Children.Add(_sort);
                Button search = Button("Search", true);
                search.Width = 88; search.Margin = new Thickness(8, 0, 0, 0);
                search.Click += async (_, _) => await SearchModrinthAsync();
                Grid.SetColumn(search, 3); toolbar.Children.Add(search);
                _search.KeyDown += async (_, args) => { if (args.Key == Key.Enter) await SearchModrinthAsync(); };
                Grid.SetRow(toolbar, 1);
                main.Children.Add(toolbar);

                ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                _content = new Grid();
                scroll.Content = _content;
                Grid.SetRow(scroll, 2);
                main.Children.Add(scroll);

                _status = new TextBlock { Text = "Topu Client ready", Foreground = Brush("#666D78"), FontSize = 9, VerticalAlignment = VerticalAlignment.Center };
                Border footer = new Border { BorderBrush = Brush("#24282F"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 10, 0, 0), Child = _status };
                Grid.SetRow(footer, 3);
                main.Children.Add(footer);

                root.Children.Add(main);
                Content = root;
                Loaded += (_, _) => ShowOverview();
            }

            private static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFrom(hex)!;

            private static TextBlock Label(string text) => new TextBlock { Text = text, Foreground = Brush("#4F5661"), FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(3, 0, 0, 4) };

            private Button NavButton(string text, string page)
            {
                Button b = Button(text, false);
                b.HorizontalContentAlignment = HorizontalAlignment.Left;
                b.Height = 42;
                b.Margin = new Thickness(0, 2, 0, 2);
                b.Click += (_, _) => Navigate(page);
                return b;
            }

            private static TextBox TextBox(string tooltip)
            {
                return new TextBox { Height = 38, Padding = new Thickness(12, 7, 12, 7), Background = Brush("#111419"), Foreground = Brushes.White, BorderBrush = Brush("#30353D"), BorderThickness = new Thickness(1), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = tooltip };
            }

            private static ComboBox Combo(params string[] values)
            {
                ComboBox c = new ComboBox { Height = 38, Background = Brush("#111419"), Foreground = Brushes.White, BorderBrush = Brush("#30353D"), BorderThickness = new Thickness(1) };
                foreach (string value in values) c.Items.Add(value);
                c.SelectedIndex = 0;
                return c;
            }

            private static Button Button(string text, bool green)
            {
                Button b = new Button { Content = text, Height = 38, Padding = new Thickness(14, 0, 14, 0), Background = Brush(green ? "#00FF88" : "#20242A"), Foreground = Brush(green ? "#06120B" : "#E8EBEF"), BorderBrush = Brush(green ? "#00FF88" : "#343941"), BorderThickness = new Thickness(1), FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
                b.MouseEnter += (_, _) => b.Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 0, Opacity = .25 };
                b.MouseLeave += (_, _) => b.Effect = null;
                return b;
            }

            private void Navigate(string page)
            {
                switch (page)
                {
                    case "overview": ShowOverview(); break;
                    case "mods": _type.SelectedItem = "Mods"; ShowModrinth("Discover Mods", "Find compatible mods for the active Minecraft profile."); break;
                    case "modpacks": _type.SelectedItem = "Modpacks"; ShowModrinth("Modpacks", "Browse complete Modrinth modpacks."); break;
                    case "installed": ShowInstalled(); break;
                    case "performance": ShowPerformance(); break;
                    case "profiles": ShowProfiles(); break;
                }
            }

            private void PreparePage(string title, string subtitle)
            {
                _pageTitle.Text = title;
                _pageSubtitle.Text = subtitle;
                _content.Children.Clear();
            }

            private void ShowOverview()
            {
                PreparePage("Overview", "Your Minecraft instance at a glance.");
                Grid grid = new Grid();
                for (int i = 0; i < 2; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                grid.Children.Add(Card("PROFILE", SafeProfileName(), SafeProfileSummary(), 0, 0));
                grid.Children.Add(Card("GAME DIRECTORY", _launcher._gamePath, "Active Minecraft files and mods", 0, 1));
                grid.Children.Add(Card("SYSTEM", $"{Environment.ProcessorCount} logical CPU cores", $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024 / 1024} GB available memory", 0, 2));

                Border welcome = Panel();
                StackPanel ws = new StackPanel();
                ws.Children.Add(new TextBlock { Text = "TOPU PERFORMANCE CENTER", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brush("#00FF88") });
                ws.Children.Add(new TextBlock { Text = "Built for high-FPS Minecraft.", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) });
                ws.Children.Add(new TextBlock { Text = "Manage mods, modpacks, installed files, profiles and performance from one clean interface.", Foreground = Brush("#8A909B"), FontSize = 11, Margin = new Thickness(0, 6, 0, 18) });
                StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal };
                Button discover = Button("Discover Mods", true); discover.Click += (_, _) => Navigate("mods");
                Button installed = Button("Installed Mods", false); installed.Margin = new Thickness(8, 0, 0, 0); installed.Click += (_, _) => Navigate("installed");
                actions.Children.Add(discover); actions.Children.Add(installed); ws.Children.Add(actions);
                welcome.Child = ws;
                Grid.SetRow(welcome, 1); Grid.SetColumnSpan(welcome, 3); welcome.Margin = new Thickness(0, 16, 0, 0);
                grid.Children.Add(welcome);
                _content.Children.Add(grid);
                _status.Text = "Overview • Topu Client";
            }

            private Border Card(string label, string value, string sub, int row, int column)
            {
                Border card = Panel();
                StackPanel stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = label, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brush("#626975") });
                stack.Children.Add(new TextBlock { Text = value, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
                stack.Children.Add(new TextBlock { Text = sub, FontSize = 9, Foreground = Brush("#777E89"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
                card.Child = stack; card.Margin = new Thickness(0, 0, 10, 0);
                Grid.SetRow(card, row); Grid.SetColumn(card, column); return card;
            }

            private Border Panel() => new Border { Background = Brush("#171A1F"), BorderBrush = Brush("#292D35"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 0, Opacity = .12 } };

            private void ShowModrinth(string title, string subtitle)
            {
                PreparePage(title, subtitle);
                _cards.Children.Clear();
                _content.Children.Add(_cards = new WrapPanel());
                _ = SearchModrinthAsync();
            }

            private async Task SearchModrinthAsync()
            {
                try
                {
                    string query = _search.Text.Trim();
                    string type = _type.SelectedItem?.ToString() ?? "Mods";
                    string sort = _sort.SelectedItem?.ToString() ?? "Relevance";
                    string index = sort switch { "Downloads" => "downloads", "Updated" => "updated", "Newest" => "newest", _ => "relevance" };
                    string facets = type switch
                    {
                        "Modpacks" => "[[\"project_type:modpack\"]]",
                        "All" => "[[\"project_type:mod\",\"project_type:modpack\"]]",
                        _ => "[[\"project_type:mod\"]]"
                    };
                    string url = "https://api.modrinth.com/v2/search?limit=40&index=" + Uri.EscapeDataString(index) + "&facets=" + Uri.EscapeDataString(facets) + "&query=" + Uri.EscapeDataString(query);
                    _status.Text = "Searching Modrinth...";
                    using HttpResponseMessage response = await Http.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    _cards.Children.Clear();
                    foreach (JsonElement hit in doc.RootElement.GetProperty("hits").EnumerateArray())
                    {
                        string slug = Get(hit, "slug");
                        string title = Get(hit, "title");
                        string description = Get(hit, "description");
                        string icon = Get(hit, "icon_url");
                        string projectType = Get(hit, "project_type");
                        long downloads = hit.TryGetProperty("downloads", out JsonElement d) ? d.GetInt64() : 0;
                        _cards.Children.Add(ProjectCard(slug, title, description, icon, projectType, downloads));
                    }
                    _status.Text = $"{_cards.Children.Count} Modrinth projects found";
                }
                catch (Exception ex)
                {
                    _status.Text = "Modrinth error: " + ex.Message;
                }
            }

            private Border ProjectCard(string slug, string title, string description, string iconUrl, string type, long downloads)
            {
                Border card = Panel();
                card.Width = 300; card.Margin = new Thickness(0, 0, 12, 12); card.Padding = new Thickness(13);
                StackPanel stack = new StackPanel();
                StackPanel top = new StackPanel { Orientation = Orientation.Horizontal };
                Border icon = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(10), Background = Brush("#242830") };
                if (!string.IsNullOrWhiteSpace(iconUrl))
                {
                    try { icon.Background = new ImageBrush(new BitmapImage(new Uri(iconUrl))) { Stretch = Stretch.UniformToFill }; } catch { }
                }
                top.Children.Add(icon);
                StackPanel name = new StackPanel { Margin = new Thickness(10, 0, 0, 0), Width = 210 };
                name.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis });
                name.Children.Add(new TextBlock { Text = type.Equals("modpack", StringComparison.OrdinalIgnoreCase) ? "MODPACK" : "MOD", Foreground = Brush("#00FF88"), FontSize = 8, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 0) });
                name.Children.Add(new TextBlock { Text = FormatNumber(downloads) + " downloads", Foreground = Brush("#666D78"), FontSize = 9, Margin = new Thickness(0, 2, 0, 0) });
                top.Children.Add(name); stack.Children.Add(top);
                stack.Children.Add(new TextBlock { Text = description, FontSize = 9, Foreground = Brush("#969CA7"), TextWrapping = TextWrapping.Wrap, MaxHeight = 50, Margin = new Thickness(0, 10, 0, 10) });
                Button add = Button(type.Equals("modpack", StringComparison.OrdinalIgnoreCase) ? "Install Modpack" : "Add Mod", true);
                add.Height = 33;
                add.Click += async (_, _) => await InstallProjectAsync(slug, title, type, add);
                stack.Children.Add(add); card.Child = stack; return card;
            }

            private async Task InstallProjectAsync(string slug, string title, string type, Button button)
            {
                try
                {
                    button.IsEnabled = false; button.Content = "Checking compatibility...";
                    string version = CurrentVersion(); string loader = CurrentLoader();
                    string loaders = loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) ? "" : "&loaders=[\"" + loader.ToLowerInvariant() + "\"]";
                    string url = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(slug) + "/version?game_versions=[\"" + Uri.EscapeDataString(version) + "\"]" + loaders;
                    using HttpResponseMessage response = await Http.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (doc.RootElement.GetArrayLength() == 0) throw new InvalidOperationException("No compatible version for the active profile.");
                    JsonElement ver = doc.RootElement[0];
                    JsonElement file = ver.GetProperty("files")[0];
                    string download = file.GetProperty("url").GetString() ?? throw new InvalidOperationException("No download URL.");
                    string filename = file.GetProperty("filename").GetString() ?? (slug + ".jar");
                    button.Content = "Downloading...";
                    byte[] data = await Http.GetByteArrayAsync(download);
                    if (type.Equals("modpack", StringComparison.OrdinalIgnoreCase))
                    {
                        string temp = Path.Combine(Path.GetTempPath(), filename);
                        await File.WriteAllBytesAsync(temp, data);
                        Directory.CreateDirectory(_launcher._gamePath);
                        File.Copy(temp, Path.Combine(_launcher._gamePath, filename), true);
                    }
                    else
                    {
                        string mods = Path.Combine(_launcher._gamePath, "mods");
                        Directory.CreateDirectory(mods);
                        await File.WriteAllBytesAsync(Path.Combine(mods, Sanitize(filename)), data);
                    }
                    button.Content = "✓ Installed";
                    _status.Text = title + " installed successfully";
                }
                catch (Exception ex)
                {
                    button.Content = "Install failed";
                    _status.Text = ex.Message;
                    _launcher.WriteException("TOPU MODRINTH INSTALL", ex);
                }
                finally { button.IsEnabled = true; }
            }

            private void ShowInstalled()
            {
                PreparePage("Installed Mods", "Manage the JAR files currently inside this profile's mods folder.");
                WrapPanel panel = new WrapPanel();
                string modsPath = Path.Combine(_launcher._gamePath, "mods");
                if (!Directory.Exists(modsPath)) Directory.CreateDirectory(modsPath);
                string[] files = Directory.GetFiles(modsPath, "*.jar");
                if (files.Length == 0)
                {
                    panel.Children.Add(new TextBlock { Text = "No mods installed yet. Open Discover Mods to add some.", Foreground = Brush("#7C838E"), Margin = new Thickness(5, 10, 0, 0) });
                }
                foreach (string file in files.OrderBy(Path.GetFileName))
                {
                    Border card = Panel(); card.Width = 300; card.Margin = new Thickness(0, 0, 12, 12);
                    Grid g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    StackPanel s = new StackPanel(); s.Children.Add(new TextBlock { Text = Path.GetFileNameWithoutExtension(file), FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis }); s.Children.Add(new TextBlock { Text = FormatBytes(new FileInfo(file).Length), Foreground = Brush("#737A85"), FontSize = 9, Margin = new Thickness(0, 4, 0, 0) });
                    Button remove = Button("Remove", false); remove.Height = 30; remove.Click += (_, _) => { try { File.Delete(file); ShowInstalled(); _status.Text = "Removed " + Path.GetFileName(file); } catch (Exception ex) { _status.Text = ex.Message; } };
                    Grid.SetColumn(s, 0); Grid.SetColumn(remove, 1); g.Children.Add(s); g.Children.Add(remove); card.Child = g; panel.Children.Add(card);
                }
                _content.Children.Add(panel); _status.Text = files.Length + " installed mod(s)";
            }

            private void ShowPerformance()
            {
                PreparePage("Performance", "Safe, visible performance controls for your Topu instance.");
                StackPanel stack = new StackPanel();
                stack.Children.Add(PerformanceCard("⚡ MAX FPS", "Prioritize frame rate for competitive PvP.", "Uses the performance mods already configured by Topu Client."));
                stack.Children.Add(PerformanceCard("🎯 COMPETITIVE", "Balanced FPS, responsiveness and visual clarity.", "Recommended preset for PvP."));
                stack.Children.Add(PerformanceCard("🌿 QUALITY", "Keep performance optimizations while allowing more visual quality.", "Use when you want a nicer-looking world."));
                Border info = Panel(); info.Margin = new Thickness(0, 8, 0, 0);
                info.Child = new TextBlock { Text = "Tip: Topu's existing performance-mod installer remains the source of truth. These presets are UI controls and never overwrite your existing launcher configuration automatically.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#858C97"), FontSize = 10 };
                stack.Children.Add(info); _content.Children.Add(stack);
            }

            private Border PerformanceCard(string title, string subtitle, string detail)
            {
                Border card = Panel(); card.Margin = new Thickness(0, 0, 0, 10);
                Grid g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                StackPanel s = new StackPanel(); s.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.Bold }); s.Children.Add(new TextBlock { Text = subtitle, FontSize = 10, Foreground = Brush("#969CA7"), Margin = new Thickness(0, 4, 0, 0) }); s.Children.Add(new TextBlock { Text = detail, FontSize = 9, Foreground = Brush("#626974"), Margin = new Thickness(0, 4, 0, 0) });
                Button apply = Button("Use Preset", true); apply.Height = 34; apply.Click += (_, _) => _status.Text = title + " selected — launch to use your existing Topu configuration.";
                Grid.SetColumn(s, 0); Grid.SetColumn(apply, 1); g.Children.Add(s); g.Children.Add(apply); card.Child = g; return card;
            }

            private void ShowProfiles()
            {
                PreparePage("Profiles", "Your active Topu profile and runtime configuration.");
                Border card = Panel();
                StackPanel s = new StackPanel();
                s.Children.Add(new TextBlock { Text = "ACTIVE PROFILE", Foreground = Brush("#626974"), FontSize = 9, FontWeight = FontWeights.Bold });
                s.Children.Add(new TextBlock { Text = SafeProfileName(), FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) });
                s.Children.Add(new TextBlock { Text = SafeProfileSummary(), Foreground = Brush("#00FF88"), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
                s.Children.Add(new TextBlock { Text = "Use the existing profile editor in the main launcher to change version, loader and RAM. This center reads the same active instance and does not replace your profile-saving logic.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#858C97"), FontSize = 10, Margin = new Thickness(0, 15, 0, 0) });
                Button back = Button("Back to Overview", false); back.Margin = new Thickness(0, 16, 0, 0); back.Width = 140; back.Click += (_, _) => Navigate("overview"); s.Children.Add(back);
                card.Child = s; _content.Children.Add(card);
            }

            private string SafeProfileName()
            {
                try { return GetRuntimeProfile().ProfileName ?? "default"; } catch { return "default"; }
            }

            private string SafeProfileSummary()
            {
                try
                {
                    var p = GetRuntimeProfile();
                    return $"{p.Loader} • {p.Version} • {p.RamGb}GB RAM";
                }
                catch { return "Topu Client runtime"; }
            }

            private string CurrentVersion()
            {
                try { return GetRuntimeProfile().Version ?? "1.21.1"; } catch { return "1.21.1"; }
            }

            private string CurrentLoader()
            {
                try { return GetRuntimeProfile().Loader ?? "Fabric"; } catch { return "Fabric"; }
            }

            private static string Get(JsonElement e, string key) => e.TryGetProperty(key, out JsonElement p) ? p.GetString() ?? "" : "";
            private static string FormatNumber(long n) => n >= 1000000 ? (n / 1000000d).ToString("0.0") + "M" : n >= 1000 ? (n / 1000d).ToString("0.0") + "K" : n.ToString();
            private static string FormatBytes(long n) => n >= 1024 * 1024 ? (n / 1024d / 1024d).ToString("0.0") + " MB" : (n / 1024d).ToString("0") + " KB";
            private static string Sanitize(string name) { foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_'); return name; }
        }
    }
}
