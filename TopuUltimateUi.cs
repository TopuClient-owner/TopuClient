using System;
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
    // Premium management center. Kept separate so MainWindow launch/install logic is untouched.
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
            if (e.OriginalSource is not Button b) return;
            string text = b.Content?.ToString() ?? "";
            if (!text.Contains("Profiles & Mods", StringComparison.OrdinalIgnoreCase)) return;
            if (Window.GetWindow(b) is not MainWindow launcher) return;
            e.Handled = true;
            launcher.OpenTopuUltimateCenter();
        }

        private void OpenTopuUltimateCenter()
        {
            new TopuCenter(this) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private sealed class TopuCenter : Window
        {
            private static readonly HttpClient Http = CreateHttp();
            private readonly MainWindow _launcher;
            private readonly Grid _body = new Grid();
            private readonly TextBlock _title = new TextBlock();
            private readonly TextBlock _subtitle = new TextBlock();
            private readonly TextBlock _status = new TextBlock();
            private readonly TextBox _search;
            private readonly ComboBox _type;
            private readonly ComboBox _sort;

            private static HttpClient CreateHttp()
            {
                var c = new HttpClient();
                c.DefaultRequestHeaders.UserAgent.ParseAdd("TopuClient/1.0");
                return c;
            }

            public TopuCenter(MainWindow launcher)
            {
                _launcher = launcher;
                Title = "Topu Client • Management Center";
                Width = 1160; Height = 740; MinWidth = 920; MinHeight = 620;
                Background = B("#0B0D10"); Foreground = Brushes.White;

                var root = new Grid();
                root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
                root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                root.Children.Add(BuildSidebar());

                var main = new Grid { Margin = new Thickness(30, 25, 30, 16) };
                main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var heading = new StackPanel();
                _title.Text = "Overview"; _title.FontSize = 29; _title.FontWeight = FontWeights.Bold;
                _subtitle.Text = "Your Topu Client instance, mods and performance in one place.";
                _subtitle.Foreground = B("#747B87"); _subtitle.FontSize = 11; _subtitle.Margin = new Thickness(0, 5, 0, 0);
                heading.Children.Add(_title); heading.Children.Add(_subtitle);
                Grid.SetRow(heading, 0); main.Children.Add(heading);

                var toolbar = new Grid { Margin = new Thickness(0, 20, 0, 14) };
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                _search = TB("Search Modrinth..."); Grid.SetColumn(_search, 0); toolbar.Children.Add(_search);
                _type = CB("Mods", "Modpacks", "All"); _type.Width = 105; _type.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(_type, 1); toolbar.Children.Add(_type);
                _sort = CB("Relevance", "Downloads", "Updated", "Newest"); _sort.Width = 115; _sort.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(_sort, 2); toolbar.Children.Add(_sort);
                var search = Btn("Search", true); search.Width = 88; search.Margin = new Thickness(8, 0, 0, 0); search.Click += async (_, _) => await SearchAsync(); Grid.SetColumn(search, 3); toolbar.Children.Add(search);
                _search.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await SearchAsync(); };
                Grid.SetRow(toolbar, 1); main.Children.Add(toolbar);

                var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                scroll.Content = _body; Grid.SetRow(scroll, 2); main.Children.Add(scroll);
                _status.Text = "Topu Client ready"; _status.Foreground = B("#606772"); _status.FontSize = 9;
                Grid.SetRow(_status, 3); main.Children.Add(_status);
                Grid.SetColumn(main, 1); root.Children.Add(main);
                Content = root;
                Loaded += (_, _) => Overview();
            }

            private Border BuildSidebar()
            {
                var border = new Border { Background = B("#13161A"), BorderBrush = B("#262A31"), BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(17) };
                var s = new StackPanel();
                var logo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 7, 0, 28) };
                logo.Children.Add(new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = B("#073420"), BorderBrush = B("#00FF88"), BorderThickness = new Thickness(1), Child = new TextBlock { Text = "⚡", FontSize = 19, Foreground = B("#00FF88"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
                var lt = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                lt.Children.Add(new TextBlock { Text = "TOPU CLIENT", FontWeight = FontWeights.Bold, FontSize = 14 });
                lt.Children.Add(new TextBlock { Text = "MANAGEMENT CENTER", Foreground = B("#00FF88"), FontWeight = FontWeights.Bold, FontSize = 8 });
                logo.Children.Add(lt); s.Children.Add(logo);
                s.Children.Add(L("MANAGEMENT"));
                s.Children.Add(N("⌂   Overview", Overview));
                s.Children.Add(N("◈   Discover Mods", () => { _type.SelectedItem = "Mods"; Page("Discover Mods", "Search and install compatible Modrinth mods."); _ = SearchAsync(); }));
                s.Children.Add(N("▣   Modpacks", () => { _type.SelectedItem = "Modpacks"; Page("Modpacks", "Browse complete Modrinth modpacks."); _ = SearchAsync(); }));
                s.Children.Add(N("☷   Installed Mods", Installed));
                s.Children.Add(N("⚙   Performance", Performance));
                s.Children.Add(N("◉   Profiles", Profiles));
                s.Children.Add(new Border { Height = 1, Background = B("#292D34"), Margin = new Thickness(2, 20, 2, 14) });
                s.Children.Add(L("ACTIVE INSTANCE"));
                var inst = new Border { Background = B("#0E1114"), BorderBrush = B("#252A31"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(11), Margin = new Thickness(0, 6, 0, 0) };
                var isx = new StackPanel(); isx.Children.Add(new TextBlock { Text = "default", FontWeight = FontWeights.Bold, FontSize = 13 }); isx.Children.Add(new TextBlock { Text = Summary(), Foreground = B("#818793"), FontSize = 9, Margin = new Thickness(0, 4, 0, 0) }); inst.Child = isx; s.Children.Add(inst);
                border.Child = s; Grid.SetColumn(border, 0); return border;
            }

            private static TextBlock L(string text) => new TextBlock { Text = text, Foreground = B("#4F5661"), FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(3, 0, 0, 5) };
            private Button N(string text, Action action) { var b = Btn(text, false); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Height = 42; b.Margin = new Thickness(0, 2, 0, 2); b.Click += (_, _) => action(); return b; }
            private static Brush B(string hex) => (Brush)new BrushConverter().ConvertFrom(hex)!;
            private static TextBox TB(string tip) => new TextBox { Height = 38, Padding = new Thickness(12, 7, 12, 7), Background = B("#111419"), Foreground = Brushes.White, BorderBrush = B("#30353D"), BorderThickness = new Thickness(1), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = tip };
            private static ComboBox CB(params string[] values) { var c = new ComboBox { Height = 38, Background = B("#111419"), Foreground = Brushes.White, BorderBrush = B("#30353D") }; foreach (var v in values) c.Items.Add(v); c.SelectedIndex = 0; return c; }
            private static Button Btn(string text, bool green) => new Button { Content = text, Height = 38, Padding = new Thickness(14, 0, 14, 0), Background = B(green ? "#00FF88" : "#20242A"), Foreground = B(green ? "#06120B" : "#E8EBEF"), BorderBrush = B(green ? "#00FF88" : "#343941"), BorderThickness = new Thickness(1), FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };

            private void Page(string title, string subtitle) { _title.Text = title; _subtitle.Text = subtitle; _body.Children.Clear(); }
            private Border Panel() => new Border { Background = B("#171A1F"), BorderBrush = B("#292D35"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(17), Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = .13 } };

            private void Overview()
            {
                Page("Overview", "Your Topu Client instance, mods and performance in one place.");
                var g = new Grid();
                for (int rowIndex = 0; rowIndex < 2; rowIndex++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (int columnIndex = 0; columnIndex < 3; columnIndex++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                AddCard(g, "PROFILE", "default", Summary(), 0, 0);
                AddCard(g, "MINECRAFT", Version(), "Active runtime version", 0, 1);
                AddCard(g, "MODS", Directory.Exists(Path.Combine(_launcher._gamePath, "mods")) ? Directory.GetFiles(Path.Combine(_launcher._gamePath, "mods"), "*.jar").Length.ToString() : "0", "JAR files installed", 0, 2);
                var hero = Panel(); hero.Margin = new Thickness(0, 16, 10, 0); var hs = new StackPanel();
                hs.Children.Add(new TextBlock { Text = "TOPU PERFORMANCE CENTER", Foreground = B("#00FF88"), FontWeight = FontWeights.Bold, FontSize = 10 });
                hs.Children.Add(new TextBlock { Text = "Ready for high-FPS Minecraft.", FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0) });
                hs.Children.Add(new TextBlock { Text = "Manage your mods, modpacks and instance without touching your existing launch configuration.", Foreground = B("#858C97"), FontSize = 10, Margin = new Thickness(0, 6, 0, 16) });
                var row = new StackPanel { Orientation = Orientation.Horizontal }; var m = Btn("Browse Mods", true); m.Click += (_, _) => { _type.SelectedItem = "Mods"; Page("Discover Mods", "Find compatible mods for the active profile."); _ = SearchAsync(); }; var installedButton = Btn("Installed Mods", false); installedButton.Margin = new Thickness(8, 0, 0, 0); installedButton.Click += (_, _) => Installed(); row.Children.Add(m); row.Children.Add(installedButton); hs.Children.Add(row); hero.Child = hs;
                Grid.SetRow(hero, 1); Grid.SetColumnSpan(hero, 3); g.Children.Add(hero); _body.Children.Add(g); _status.Text = "Overview • " + Summary();
            }

            private void AddCard(Grid g, string label, string value, string sub, int r, int c)
            {
                var p = Panel(); p.Margin = new Thickness(0, 0, 10, 0); var s = new StackPanel(); s.Children.Add(new TextBlock { Text = label, Foreground = B("#626975"), FontSize = 9, FontWeight = FontWeights.Bold }); s.Children.Add(new TextBlock { Text = value, FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis }); s.Children.Add(new TextBlock { Text = sub, Foreground = B("#777E89"), FontSize = 9, Margin = new Thickness(0, 4, 0, 0) }); p.Child = s; Grid.SetRow(p, r); Grid.SetColumn(p, c); g.Children.Add(p);
            }

            private async Task SearchAsync()
            {
                try
                {
                    _status.Text = "Loading Modrinth...";
                    string type = _type.SelectedItem?.ToString() ?? "Mods";
                    string sort = _sort.SelectedItem?.ToString() ?? "Relevance";
                    string index = sort switch { "Downloads" => "downloads", "Updated" => "updated", "Newest" => "newest", _ => "relevance" };
                    string facets = type == "Modpacks" ? "[[\"project_type:modpack\"]]" : type == "All" ? "[[\"project_type:mod\",\"project_type:modpack\"]]" : "[[\"project_type:mod\"]]";
                    string url = "https://api.modrinth.com/v2/search?limit=40&index=" + Uri.EscapeDataString(index) + "&facets=" + Uri.EscapeDataString(facets) + "&query=" + Uri.EscapeDataString(_search.Text.Trim());
                    using var response = await Http.GetAsync(url); response.EnsureSuccessStatusCode(); using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var wrap = new WrapPanel();
                    foreach (var h in doc.RootElement.GetProperty("hits").EnumerateArray())
                    {
                        string slug = Get(h, "slug"), title = Get(h, "title"), desc = Get(h, "description"), icon = Get(h, "icon_url"), ptype = Get(h, "project_type");
                        long downloads = h.TryGetProperty("downloads", out var d) ? d.GetInt64() : 0;
                        wrap.Children.Add(Project(slug, title, desc, icon, ptype, downloads));
                    }
                    _body.Children.Clear(); _body.Children.Add(wrap); _status.Text = wrap.Children.Count + " Modrinth projects found";
                }
                catch (Exception ex) { _status.Text = "Modrinth error: " + ex.Message; }
            }

            private Border Project(string slug, string title, string desc, string iconUrl, string type, long downloads)
            {
                var p = Panel(); p.Width = 300; p.Margin = new Thickness(0, 0, 12, 12); var s = new StackPanel();
                var top = new StackPanel { Orientation = Orientation.Horizontal }; var icon = new Border { Width = 54, Height = 54, CornerRadius = new CornerRadius(10), Background = B("#242830") };
                if (!string.IsNullOrWhiteSpace(iconUrl)) try { icon.Background = new ImageBrush(new BitmapImage(new Uri(iconUrl))) { Stretch = Stretch.UniformToFill }; } catch { }
                top.Children.Add(icon); var n = new StackPanel { Margin = new Thickness(10, 0, 0, 0), Width = 210 }; n.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis }); n.Children.Add(new TextBlock { Text = type.Equals("modpack", StringComparison.OrdinalIgnoreCase) ? "MODPACK" : "MOD", Foreground = B("#00FF88"), FontSize = 8, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 0) }); n.Children.Add(new TextBlock { Text = downloads.ToString("N0") + " downloads", Foreground = B("#666D78"), FontSize = 9, Margin = new Thickness(0, 2, 0, 0) }); top.Children.Add(n); s.Children.Add(top);
                s.Children.Add(new TextBlock { Text = desc, Foreground = B("#969CA7"), FontSize = 9, TextWrapping = TextWrapping.Wrap, MaxHeight = 48, Margin = new Thickness(0, 10, 0, 10) });
                var add = Btn(type.Equals("modpack", StringComparison.OrdinalIgnoreCase) ? "Install Modpack" : "Add Mod", true); add.Height = 33; add.Click += async (_, _) => await Install(slug, title, type, add); s.Children.Add(add); p.Child = s; return p;
            }

            private async Task Install(string slug, string title, string type, Button button)
            {
                try
                {
                    button.IsEnabled = false; button.Content = "Checking...";
                    string version = Version(), loader = Loader();
                    string loaders = loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) ? "" : "&loaders=[\"" + loader.ToLowerInvariant() + "\"]";
                    string url = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(slug) + "/version?game_versions=[\"" + Uri.EscapeDataString(version) + "\"]" + loaders;
                    using var response = await Http.GetAsync(url); response.EnsureSuccessStatusCode(); using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (doc.RootElement.GetArrayLength() == 0) throw new InvalidOperationException("No compatible version was found for " + version + ".");
                    var file = doc.RootElement[0].GetProperty("files")[0]; string url2 = file.GetProperty("url").GetString()!; string name = file.GetProperty("filename").GetString()!;
                    button.Content = "Downloading..."; byte[] bytes = await Http.GetByteArrayAsync(url2);
                    if (type.Equals("modpack", StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(_launcher._gamePath); await File.WriteAllBytesAsync(Path.Combine(_launcher._gamePath, name), bytes);
                        _status.Text = title + " modpack downloaded to the active instance.";
                    }
                    else
                    {
                        string mods = Path.Combine(_launcher._gamePath, "mods"); Directory.CreateDirectory(mods); await File.WriteAllBytesAsync(Path.Combine(mods, Sanitize(name)), bytes);
                        _status.Text = title + " installed successfully.";
                    }
                    button.Content = "✓ Installed";
                }
                catch (Exception ex) { button.Content = "Install failed"; _status.Text = ex.Message; _launcher.WriteException("TOPU CENTER INSTALL", ex); }
                finally { button.IsEnabled = true; }
            }

            private void Installed()
            {
                Page("Installed Mods", "Everything currently installed in the active profile."); var wrap = new WrapPanel(); string dir = Path.Combine(_launcher._gamePath, "mods"); Directory.CreateDirectory(dir); var files = Directory.GetFiles(dir, "*.jar").OrderBy(x => x).ToArray();
                if (files.Length == 0) wrap.Children.Add(new TextBlock { Text = "No mods installed yet.", Foreground = B("#7D8490"), Margin = new Thickness(5, 8, 0, 0) });
                foreach (string file in files)
                {
                    var p = Panel(); p.Width = 300; p.Margin = new Thickness(0, 0, 12, 12); var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var s = new StackPanel(); s.Children.Add(new TextBlock { Text = Path.GetFileNameWithoutExtension(file), FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis }); s.Children.Add(new TextBlock { Text = (new FileInfo(file).Length / 1024d / 1024d).ToString("0.0") + " MB", Foreground = B("#6C737E"), FontSize = 9, Margin = new Thickness(0, 4, 0, 0) });
                    var rm = Btn("Remove", false); rm.Height = 30; rm.Click += (_, _) => { try { File.Delete(file); Installed(); _status.Text = "Removed " + Path.GetFileName(file); } catch (Exception ex) { _status.Text = ex.Message; } };
                    Grid.SetColumn(s, 0); Grid.SetColumn(rm, 1); g.Children.Add(s); g.Children.Add(rm); p.Child = g; wrap.Children.Add(p);
                }
                _body.Children.Add(wrap); _status.Text = files.Length + " installed mod(s)";
            }

            private void Performance()
            {
                Page("Performance", "Performance-focused controls without overwriting your existing launcher logic."); var stack = new StackPanel();
                stack.Children.Add(Preset("⚡ MAX FPS", "Maximum frame-rate focus for competitive PvP."));
                stack.Children.Add(Preset("🎯 COMPETITIVE", "Balanced FPS, responsiveness and visibility."));
                stack.Children.Add(Preset("🌿 QUALITY", "Performance optimizations with more visual quality."));
                var p = Panel(); p.Margin = new Thickness(0, 2, 0, 0); p.Child = new TextBlock { Text = "Topu already installs performance mods such as Sodium, Lithium, Dynamic FPS, Sodium Extra and Krypton. Presets here never fake an FPS value or silently overwrite your existing settings.", Foreground = B("#858C97"), FontSize = 10, TextWrapping = TextWrapping.Wrap }; stack.Children.Add(p); _body.Children.Add(stack);
            }

            private Border Preset(string name, string text)
            {
                var p = Panel(); p.Margin = new Thickness(0, 0, 0, 10); var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); var s = new StackPanel(); s.Children.Add(new TextBlock { Text = name, FontSize = 15, FontWeight = FontWeights.Bold }); s.Children.Add(new TextBlock { Text = text, Foreground = B("#9097A2"), FontSize = 10, Margin = new Thickness(0, 4, 0, 0) }); var b = Btn("Select", true); b.Height = 33; b.Click += (_, _) => _status.Text = name + " selected."; Grid.SetColumn(s, 0); Grid.SetColumn(b, 1); g.Children.Add(s); g.Children.Add(b); p.Child = g; return p;
            }

            private void Profiles()
            {
                Page("Profiles", "Active profile information from the same runtime system used by the launcher."); var p = Panel(); var s = new StackPanel(); s.Children.Add(new TextBlock { Text = "DEFAULT", Foreground = B("#626974"), FontSize = 9, FontWeight = FontWeights.Bold }); s.Children.Add(new TextBlock { Text = "default", FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 0) }); s.Children.Add(new TextBlock { Text = Summary(), Foreground = B("#00FF88"), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) }); s.Children.Add(new TextBlock { Text = "The existing profile editor remains responsible for saving version and RAM changes. This center reads the active values and will not replace that logic.", Foreground = B("#858C97"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) }); p.Child = s; _body.Children.Add(p);
            }

            private string Summary() { try { var p = _launcher.GetRuntimeProfile(); return $"{p.Loader} • {p.Version} • {p.RamGb}GB RAM"; } catch { return "Topu runtime"; } }
            private string Version() { try { return _launcher.GetRuntimeProfile().Version ?? "1.21.1"; } catch { return "1.21.1"; } }
            private string Loader() { try { return _launcher.GetRuntimeProfile().Loader ?? "Fabric"; } catch { return "Fabric"; } }
            private static string Get(JsonElement e, string k) => e.TryGetProperty(k, out var p) ? p.GetString() ?? "" : "";
            private static string Sanitize(string n) { foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_'); return n; }
        }
    }
}