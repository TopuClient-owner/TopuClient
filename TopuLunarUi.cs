using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private Border? _lunarHost;
        private Grid? _lunarPages;
        private StackPanel? _lunarHome;

        private void InitializeTopuLunarUi()
        {
            if (_lunarHost != null) return;

            try
            {
                var root = Content as Border;
                var rootGrid = root?.Child as Grid;
                if (rootGrid == null || rootGrid.Children.Count < 2) return;
                if (rootGrid.Children[1] is not Grid mainGrid || mainGrid.Children.Count < 2) return;
                if (mainGrid.Children[1] is not ScrollViewer scroll || scroll.Content is not Grid oldPages) return;

                oldPages.Children.Remove(TabLaunch);
                oldPages.Children.Remove(TabProfiles);
                oldPages.Children.Remove(TabAccounts);

                var shell = new Border
                {
                    Background = B("#080A0D"), BorderBrush = B("#252C33"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18), ClipToBounds = true
                };
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                grid.Children.Add(BuildTopBar());

                var body = new Grid();
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(238) });
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetRow(body, 1);
                grid.Children.Add(body);
                body.Children.Add(BuildSidebar());

                _lunarPages = new Grid { Background = B("#0A0D10") };
                Grid.SetColumn(_lunarPages, 1);
                body.Children.Add(_lunarPages);

                _lunarHome = BuildHomePage();
                _lunarPages.Children.Add(_lunarHome);
                PreparePage(TabProfiles);
                PreparePage(TabAccounts);
                _lunarPages.Children.Add(TabProfiles);
                _lunarPages.Children.Add(TabAccounts);
                TabProfiles.Visibility = Visibility.Collapsed;
                TabAccounts.Visibility = Visibility.Collapsed;

                var footer = new Border
                {
                    Background = B("#07090B"), BorderBrush = B("#171C21"), BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(18, 0, 18, 0)
                };
                Grid.SetRow(footer, 2);
                var fg = new Grid();
                fg.Children.Add(T("TOPU CLIENT  •  PERFORMANCE EDITION", 9, "#626B75", true));
                var ready = T("READY TO LAUNCH", 9, "#00FF88", true); ready.HorizontalAlignment = HorizontalAlignment.Right;
                fg.Children.Add(ready); footer.Child = fg; grid.Children.Add(footer);

                shell.Child = grid;
                Content = shell;
                _lunarHost = shell;
                ShowPage("home");
            }
            catch
            {
                // Original XAML remains the fallback if the optional shell cannot initialize.
            }
        }

        private UIElement BuildTopBar()
        {
            var bar = new Border
            {
                Background = B("#0B0E12"), BorderBrush = B("#1D2329"), BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(22, 0, 16, 0), Cursor = Cursors.SizeAll
            };
            bar.MouseDown += TitleBar_MouseDown;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logo = new Border
            {
                Width = 42, Height = 42, Padding = new Thickness(7), CornerRadius = new CornerRadius(11),
                Background = B("#0C261B"), BorderBrush = B("#1B6243"), BorderThickness = new Thickness(1)
            };
            logo.Child = new Image
            {
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("/Assets/TopuLauncher.ico", UriKind.Relative)),
                Stretch = Stretch.Uniform
            };
            brand.Children.Add(logo);
            var wordmark = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            wordmark.Children.Add(T("TOPU CLIENT", 16, "#F4F7FA", true));
            wordmark.Children.Add(T("PERFORMANCE LAUNCHER", 8, "#00FF88", true));
            brand.Children.Add(wordmark); g.Children.Add(brand);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(right, 1);
            var account = new Border
            {
                Background = B("#11161B"), BorderBrush = B("#252C33"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 10, 0)
            };
            var accountText = T("No account", 10, "#E8EDF1", true);
            accountText.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = LaunchAccountLabel });
            var accountStack = new StackPanel(); accountStack.Children.Add(T("ACCOUNT", 7, "#626B75", true)); accountStack.Children.Add(accountText);
            account.Child = accountStack; right.Children.Add(account);
            right.Children.Add(WindowButton("—", Minimize_Click)); right.Children.Add(WindowButton("×", Close_Click));
            g.Children.Add(right); bar.Child = g; return bar;
        }

        private UIElement BuildSidebar()
        {
            var side = new Border { Background = B("#0C0F13"), BorderBrush = B("#1D2329"), BorderThickness = new Thickness(0, 0, 1, 0) };
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var menu = new StackPanel { Margin = new Thickness(15, 22, 15, 0) };
            menu.Children.Add(T("MAIN MENU", 8, "#4E5863", true, new Thickness(11, 0, 0, 10)));
            menu.Children.Add(NavButton("⌂", "HOME", "home"));
            menu.Children.Add(NavButton("▣", "PROFILES", "profiles"));
            menu.Children.Add(NavButton("●", "ACCOUNTS", "accounts"));
            menu.Children.Add(T("QUICK PLAY", 8, "#4E5863", true, new Thickness(11, 25, 0, 10)));
            menu.Children.Add(ServerButton("G", "GamerTee", "gametee.net"));
            menu.Children.Add(ServerButton("S", "Sharpness.gg", "sharpness.gg")); root.Children.Add(menu);

            var active = new Border
            {
                Margin = new Thickness(15, 0, 15, 16), Padding = new Thickness(12), Background = B("#0A1510"),
                BorderBrush = B("#174D36"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12)
            };
            var stack = new StackPanel(); stack.Children.Add(T("ACTIVE PROFILE", 7, "#53605A", true));
            var profile = T("default", 13, "#F4F7FA", true, new Thickness(0, 5, 0, 0));
            profile.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = SidebarProfileLabel }); stack.Children.Add(profile);
            var runtime = T("READY", 8, "#00FF88", true, new Thickness(0, 3, 0, 0));
            runtime.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = SidebarRuntimeLabel }); stack.Children.Add(runtime);
            active.Child = stack; Grid.SetRow(active, 1); root.Children.Add(active); side.Child = root; return side;
        }

        private StackPanel BuildHomePage()
        {
            var page = new StackPanel { Margin = new Thickness(30, 26, 30, 26) };
            page.Children.Add(T("WELCOME BACK", 9, "#00FF88", true));
            page.Children.Add(T("Ready for Minecraft?", 30, "#F4F7FA", true, new Thickness(0, 5, 0, 0)));
            page.Children.Add(T("Your performance profile is loaded and ready to go.", 11, "#78828C", false, new Thickness(0, 5, 0, 20)));

            var hero = new Border
            {
                Height = 205, Background = B("#0D1713"), BorderBrush = B("#205A3E"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16), Padding = new Thickness(26), Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 0, Opacity = 0.25 }
            };
            var heroGrid = new Grid();
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(235) });

            var heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            heroText.Children.Add(T("TOPU PERFORMANCE EDITION", 9, "#00FF88", true));
            heroText.Children.Add(T("Maximum FPS.\nMinimum distraction.", 27, "#F4F7FA", true, new Thickness(0, 8, 0, 0)));
            heroText.Children.Add(T("A clean PvP-focused launcher built around your selected profile.", 10, "#81908A", false, new Thickness(0, 9, 0, 0)));
            heroGrid.Children.Add(heroText);

            var launchPanel = new Border
            {
                Background = B("#09110D"), BorderBrush = B("#1B5A3D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(16)
            };
            Grid.SetColumn(launchPanel, 1);
            var launchStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            launchStack.Children.Add(T("CURRENT PROFILE", 7, "#59665F", true));
            var profile = T("default", 18, "#FFFFFF", true, new Thickness(0, 4, 0, 0));
            profile.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = LaunchProfileLabel }); launchStack.Children.Add(profile);
            launchStack.Children.Add(T("Selected version  •  Selected loader  •  Selected RAM", 8, "#AAB4AE", false, new Thickness(0, 3, 0, 12)));
            var launch = new Button { Height = 52, Content = "PLAY NOW  ›", Style = MakeButtonStyle("#00E87A", "#04120B", "#23FF94", 11) };
            launch.Click += (_, _) => LaunchBtn_Click(LaunchBtn, new RoutedEventArgs(Button.ClickEvent)); launchStack.Children.Add(launch);
            launchPanel.Child = launchStack; heroGrid.Children.Add(launchPanel); hero.Child = heroGrid; page.Children.Add(hero);

            var detailsRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            detailsRow.ColumnDefinitions.Add(new ColumnDefinition()); detailsRow.ColumnDefinitions.Add(new ColumnDefinition()); detailsRow.ColumnDefinitions.Add(new ColumnDefinition());
            detailsRow.Children.Add(InfoCard("ACCOUNT", LaunchAccountLabel, "Microsoft / Offline"));
            var profileCard = InfoCard("PROFILE", LaunchProfileLabel, "Active configuration"); Grid.SetColumn(profileCard, 1); detailsRow.Children.Add(profileCard);
            var runtimeCard = InfoCard("VERSION", LaunchVersionLabel, "Selected Minecraft version"); Grid.SetColumn(runtimeCard, 2); detailsRow.Children.Add(runtimeCard);
            page.Children.Add(detailsRow);
            page.Children.Add(T("QUICK CONNECT", 8, "#5A646E", true, new Thickness(2, 18, 0, 8)));
            var quick = new StackPanel { Orientation = Orientation.Horizontal }; quick.Children.Add(QuickButton("GAMERTEE", "gametee.net")); quick.Children.Add(QuickButton("SHARPNESS.GG", "sharpness.gg")); page.Children.Add(quick);
            return page;
        }

        private Border InfoCard(string title, TextBlock source, string subtitle)
        {
            var card = new Border { Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(15), Height = 88, Background = B("#101419"), BorderBrush = B("#222A31"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12) };
            var stack = new StackPanel(); stack.Children.Add(T(title, 7, "#5E6872", true));
            var value = T("—", 14, "#F4F7FA", true, new Thickness(0, 6, 0, 0)); value.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = source });
            stack.Children.Add(value); stack.Children.Add(T(subtitle, 8, "#68727C", false, new Thickness(0, 3, 0, 0))); card.Child = stack; return card;
        }

        private Button NavButton(string icon, string text, string page)
        {
            var b = new Button { Height = 46, Margin = new Thickness(0, 0, 0, 5), HorizontalContentAlignment = HorizontalAlignment.Left, Style = MakeButtonStyle("Transparent", "#C6CDD2", "#161D23", 9) };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(T(icon, 14, "#00FF88", true, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Center));
            row.Children.Add(T(text, 9, "#C6CDD2", true, new Thickness(13, 0, 0, 0))); b.Content = row; b.Click += (_, _) => ShowPage(page); return b;
        }

        private Button ServerButton(string icon, string name, string address)
        {
            var b = new Button { Height = 42, Margin = new Thickness(0, 0, 0, 5), HorizontalContentAlignment = HorizontalAlignment.Left, Style = MakeButtonStyle("#0F1317", "#D9E0E5", "#172027", 9), Tag = address };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border { Width = 25, Height = 25, CornerRadius = new CornerRadius(7), Background = B("#10251B") };
            badge.Child = T(icon, 9, "#00FF88", true, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Center);
            row.Children.Add(badge); row.Children.Add(T(name, 9, "#DDE4E8", true, new Thickness(9, 0, 0, 0))); b.Content = row; b.Click += (_, _) => JoinServer_Click(b, new RoutedEventArgs(Button.ClickEvent)); return b;
        }

        private Button QuickButton(string text, string address)
        {
            var b = new Button { Content = text, Width = 138, Height = 36, Margin = new Thickness(0, 0, 8, 0), Tag = address, Style = MakeButtonStyle("#141A20", "#DCE3E8", "#1B252D", 9) };
            b.Click += (_, _) => JoinServer_Click(b, new RoutedEventArgs(Button.ClickEvent)); return b;
        }

        private void ShowPage(string page)
        {
            if (_lunarHome == null) return;
            _lunarHome.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
            TabProfiles.Visibility = page == "profiles" ? Visibility.Visible : Visibility.Collapsed;
            TabAccounts.Visibility = page == "accounts" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PreparePage(StackPanel page) => page.Margin = new Thickness(30, 26, 30, 26);

        private Button WindowButton(string text, RoutedEventHandler handler)
        {
            var b = new Button { Content = text, Width = 38, Height = 38, Margin = new Thickness(3, 0, 0, 0), Style = MakeButtonStyle("Transparent", "#7D8791", "#1A2127", 15) };
            b.Click += handler; return b;
        }

        private Style MakeButtonStyle(string bg, string fg, string hover, double size)
        {
            var s = new Style(typeof(Button));
            s.Setters.Add(new Setter(Control.BackgroundProperty, B(bg))); s.Setters.Add(new Setter(Control.ForegroundProperty, B(fg)));
            s.Setters.Add(new Setter(Control.BorderBrushProperty, B("#263039"))); s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            s.Setters.Add(new Setter(Control.FontSizeProperty, size)); s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold)); s.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter); template.VisualTree = border; s.Setters.Add(new Setter(Button.TemplateProperty, template));
            var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; trigger.Setters.Add(new Setter(Control.BackgroundProperty, B(hover))); trigger.Setters.Add(new Setter(Control.BorderBrushProperty, B("#31503F"))); s.Triggers.Add(trigger);
            return s;
        }

        private static SolidColorBrush B(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
        private static TextBlock T(string text, double size, string color, bool bold, Thickness? margin = null, HorizontalAlignment horizontal = HorizontalAlignment.Left, VerticalAlignment vertical = VerticalAlignment.Top) => new TextBlock
        {
            Text = text, FontSize = size, Foreground = B(color), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Margin = margin ?? new Thickness(0), HorizontalAlignment = horizontal, VerticalAlignment = vertical, TextWrapping = TextWrapping.Wrap
        };
    }
}
