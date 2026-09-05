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
        private static bool _topuLunarClassHooked;
        private Border? _lunarHost;
        private Grid? _lunarPageHost;
        private StackPanel? _lunarHomePage;

        static MainWindow()
        {
            if (_topuLunarClassHooked)
                return;

            _topuLunarClassHooked = true;
            EventManager.RegisterClassHandler(
                typeof(MainWindow),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, _) => ((MainWindow)sender).InitializeTopuLunarUi()));
        }

        private void InitializeTopuLunarUi()
        {
            if (_lunarHost != null)
                return;

            try
            {
                var oldRoot = Content as Border;
                if (oldRoot == null)
                    return;

                var oldRootGrid = oldRoot.Child as Grid;
                if (oldRootGrid == null || oldRootGrid.Children.Count < 2)
                    return;

                Grid? oldPageGrid = null;
                if (oldRootGrid.Children[1] is Grid oldMainGrid && oldMainGrid.Children.Count >= 2)
                {
                    if (oldMainGrid.Children[1] is ScrollViewer scroll && scroll.Content is Grid contentGrid)
                        oldPageGrid = contentGrid;
                }

                if (oldPageGrid == null)
                    return;

                oldPageGrid.Children.Remove(TabLaunch);
                oldPageGrid.Children.Remove(TabProfiles);
                oldPageGrid.Children.Remove(TabAccounts);

                var shell = new Border
                {
                    Background = Brush("#080A0D"),
                    BorderBrush = Brush("#252B31"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    ClipToBounds = true
                };

                var shellGrid = new Grid();
                shellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });
                shellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                shellGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

                shellGrid.Children.Add(BuildTopBar());

                var body = new Grid();
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(238) });
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetRow(body, 1);
                shellGrid.Children.Add(body);

                body.Children.Add(BuildSidebar());

                _lunarPageHost = new Grid
                {
                    Margin = new Thickness(0),
                    Background = Brush("#0A0D10")
                };
                Grid.SetColumn(_lunarPageHost, 1);
                body.Children.Add(_lunarPageHost);

                _lunarHomePage = BuildHomePage();
                _lunarPageHost.Children.Add(_lunarHomePage);

                PrepareExistingPage(TabProfiles);
                PrepareExistingPage(TabAccounts);
                _lunarPageHost.Children.Add(TabProfiles);
                _lunarPageHost.Children.Add(TabAccounts);
                TabProfiles.Visibility = Visibility.Collapsed;
                TabAccounts.Visibility = Visibility.Collapsed;

                var footer = new Border
                {
                    Background = Brush("#07090B"),
                    BorderBrush = Brush("#171C21"),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(18, 0, 18, 0)
                };
                Grid.SetRow(footer, 2);
                var footerGrid = new Grid();
                footerGrid.Children.Add(Text("TOPU CLIENT  •  PERFORMANCE EDITION", 9, "#626B75", true));
                var footerRight = Text("READY TO LAUNCH", 9, "#00FF88", true);
                footerRight.HorizontalAlignment = HorizontalAlignment.Right;
                footerGrid.Children.Add(footerRight);
                footer.Child = footerGrid;
                shellGrid.Children.Add(footer);

                shell.Child = shellGrid;
                Content = shell;
                _lunarHost = shell;
                ApplyPageVisibility("home");
            }
            catch
            {
                // Existing XAML stays as the safe fallback if the visual shell cannot be built.
            }
        }

        private UIElement BuildTopBar()
        {
            var bar = new Border
            {
                Background = Brush("#0B0E12"),
                BorderBrush = Brush("#1D2329"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(22, 0, 16, 0),
                Cursor = Cursors.SizeAll
            };
            bar.MouseDown += TitleBar_MouseDown;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logoFrame = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(11),
                Background = Brush("#0C261B"),
                BorderBrush = Brush("#1B6243"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7)
            };
            logoFrame.Child = new Image
            {
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("/Assets/TopuLauncher.ico", UriKind.Relative)),
                Stretch = Stretch.Uniform
            };
            brand.Children.Add(logoFrame);

            var brandText = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            brandText.Children.Add(Text("TOPU CLIENT", 16, "#F4F7FA", true));
            brandText.Children.Add(Text("PERFORMANCE LAUNCHER", 8, "#00FF88", true));
            brand.Children.Add(brandText);
            grid.Children.Add(brand);

            var right = new StackPanel { Grid.Column = 1, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var account = new Border
            {
                Background = Brush("#11161B"),
                BorderBrush = Brush("#252C33"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 10, 0)
            };
            var accountStack = new StackPanel();
            accountStack.Children.Add(Text("ACCOUNT", 7, "#626B75", true));
            var accountText = Text("No account", 10, "#E8EDF1", true);
            accountText.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = LaunchAccountLabel });
            accountStack.Children.Add(accountText);
            account.Child = accountStack;
            right.Children.Add(account);

            right.Children.Add(WindowButton("—", Minimize_Click));
            right.Children.Add(WindowButton("×", Close_Click));
            grid.Children.Add(right);
            bar.Child = grid;
            return bar;
        }

        private UIElement BuildSidebar()
        {
            var side = new Border
            {
                Background = Brush("#0C0F13"),
                BorderBrush = Brush("#1D2329"),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var menu = new StackPanel { Margin = new Thickness(15, 22, 15, 0) };
            menu.Children.Add(Text("MAIN MENU", 8, "#4E5863", true, new Thickness(11, 0, 0, 10)));
            menu.Children.Add(NavButton("⌂", "HOME", "home"));
            menu.Children.Add(NavButton("▣", "PROFILES", "profiles"));
            menu.Children.Add(NavButton("●", "ACCOUNTS", "accounts"));
            menu.Children.Add(Text("QUICK PLAY", 8, "#4E5863", true, new Thickness(11, 25, 0, 10)));
            menu.Children.Add(ServerButton("G", "GamerTee", "gametee.net"));
            menu.Children.Add(ServerButton("S", "Sharpness.gg", "sharpness.gg"));
            root.Children.Add(menu);

            var active = new Border
            {
                Margin = new Thickness(15, 0, 15, 16),
                Padding = new Thickness(12),
                Background = Brush("#0A1510"),
                BorderBrush = Brush("#174D36"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12)
            };
            var activeStack = new StackPanel();
            activeStack.Children.Add(Text("ACTIVE PROFILE", 7, "#53605A", true));
            var activeProfile = Text("default", 13, "#F4F7FA", true, new Thickness(0, 5, 0, 0));
            activeProfile.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = SidebarProfileLabel });
            activeStack.Children.Add(activeProfile);
            var runtime = Text("READY", 8, "#00FF88", true, new Thickness(0, 3, 0, 0));
            runtime.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = SidebarRuntimeLabel });
            activeStack.Children.Add(runtime);
            active.Child = activeStack;
            Grid.SetRow(active, 1);
            root.Children.Add(active);

            side.Child = root;
            return side;
        }

        private StackPanel BuildHomePage()
        {
            var page = new StackPanel { Margin = new Thickness(30, 26, 30, 26) };

            var welcome = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            welcome.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            welcome.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var welcomeText = new StackPanel();
            welcomeText.Children.Add(Text("WELCOME BACK", 9, "#00FF88", true));
            welcomeText.Children.Add(Text("Ready for Minecraft?", 30, "#F4F7FA", true, new Thickness(0, 5, 0, 0)));
            welcomeText.Children.Add(Text("Your performance profile is loaded and ready to go.", 11, "#78828C", false, new Thickness(0, 5, 0, 0)));
            welcome.Children.Add(welcomeText);

            var status = new Border
            {
                Width = 170,
                Height = 58,
                Background = Brush("#0D1712"),
                BorderBrush = Brush("#1A4E37"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(13)
            };
            var statusStack = new StackPanel();
            statusStack.Children.Add(Text("TOPU ENGINE", 7, "#56645D", true));
            statusStack.Children.Add(Text("● OPTIMIZED", 10, "#00FF88", true, new Thickness(0, 4, 0, 0)));
            status.Child = statusStack;
            Grid.SetColumn(status, 1);
            welcome.Children.Add(status);
            page.Children.Add(welcome);

            var hero = new Border
            {
                Height = 205,
                Background = Brush("#0D1713"),
                BorderBrush = Brush("#205A3E"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(26),
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 0, Opacity = 0.25 }
            };
            var heroGrid = new Grid();
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(235) });

            var heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            heroText.Children.Add(Text("TOPU PERFORMANCE EDITION", 9, "#00FF88", true));
            heroText.Children.Add(Text("Maximum FPS.\nMinimum distraction.", 27, "#F4F7FA", true, new Thickness(0, 8, 0, 0)));
            heroText.Children.Add(Text("A clean PvP-focused launcher built around your selected profile.", 10, "#81908A", false, new Thickness(0, 9, 0, 0)));
            heroGrid.Children.Add(heroText);

            var launchPanel = new Border
            {
                Background = Brush("#09110D"),
                BorderBrush = Brush("#1B5A3D"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16)
            };
            Grid.SetColumn(launchPanel, 1);
            var launchStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            launchStack.Children.Add(Text("CURRENT PROFILE", 7, "#59665F", true));
            var profile = Text("default", 18, "#FFFFFF", true, new Thickness(0, 4, 0, 0));
            profile.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = LaunchProfileLabel });
            launchStack.Children.Add(profile);
            var details = Text("Selected version  •  Selected loader  •  Selected RAM", 8, "#AAB4AE", false, new Thickness(0, 3, 0, 12));
            launchStack.Children.Add(details);

            var launch = new Button
            {
                Height = 52,
                Content = "PLAY NOW  ›",
                Style = MakeButtonStyle("#00E87A", "#04120B", "#23FF94", 11)
            };
            launch.Click += (_, _) => LaunchBtn_Click(LaunchBtn, new RoutedEventArgs(Button.ClickEvent));
            launchStack.Children.Add(launch);
            launchPanel.Child = launchStack;
            heroGrid.Children.Add(launchPanel);
            hero.Child = heroGrid;
            page.Children.Add(hero);

            var detailsRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            detailsRow.ColumnDefinitions.Add(new ColumnDefinition());
            detailsRow.ColumnDefinitions.Add(new ColumnDefinition());
            detailsRow.ColumnDefinitions.Add(new ColumnDefinition());
            detailsRow.Children.Add(InfoCard("ACCOUNT", LaunchAccountLabel, "Microsoft / Offline"));
            var profileCard = InfoCard("PROFILE", LaunchProfileLabel, "Active configuration");
            Grid.SetColumn(profileCard, 1);
            detailsRow.Children.Add(profileCard);
            var runtimeCard = InfoCard("VERSION", LaunchVersionLabel, "Selected Minecraft version");
            Grid.SetColumn(runtimeCard, 2);
            detailsRow.Children.Add(runtimeCard);
            page.Children.Add(detailsRow);

            page.Children.Add(Text("QUICK CONNECT", 8, "#5A646E", true, new Thickness(2, 18, 0, 8)));
            var quick = new StackPanel { Orientation = Orientation.Horizontal };
            quick.Children.Add(QuickButton("GAMERTEE", "gametee.net"));
            quick.Children.Add(QuickButton("SHARPNESS.GG", "sharpness.gg"));
            page.Children.Add(quick);

            return page;
        }

        private Border InfoCard(string title, TextBlock source, string subtitle)
        {
            var card = new Border
            {
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(15),
                Height = 88,
                Background = Brush("#101419"),
                BorderBrush = Brush("#222A31"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12)
            };
            var stack = new StackPanel();
            stack.Children.Add(Text(title, 7, "#5E6872", true));
            var value = Text("—", 14, "#F4F7FA", true, new Thickness(0, 6, 0, 0));
            value.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = source });
            stack.Children.Add(value);
            stack.Children.Add(Text(subtitle, 8, "#68727C", false, new Thickness(0, 3, 0, 0)));
            card.Child = stack;
            return card;
        }

        private Button QuickButton(string text, string server)
        {
            var b = new Button
            {
                Content = text,
                Width = 138,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 0),
                Style = MakeButtonStyle("#141A20", "#DCE3E8", "#1B252D", 9)
            };
            b.Tag = server;
            b.Click += (_, _) => JoinServer_Click(b, new RoutedEventArgs(Button.ClickEvent));
            return b;
        }

        private Button ServerButton(string icon, string name, string server)
        {
            var b = new Button
            {
                Height = 42,
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Style = MakeButtonStyle("#0F1317", "#D9E0E5", "#172027", 9)
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border { Width = 25, Height = 25, CornerRadius = new CornerRadius(7), Background = Brush("#10251B") };
            badge.Child = Text(icon, 9, "#00FF88", true, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Center);
            row.Children.Add(badge);
            row.Children.Add(Text(name, 9, "#DDE4E8", true, new Thickness(9, 0, 0, 0)));
            b.Content = row;
            b.Tag = server;
            b.Click += (_, _) => JoinServer_Click(b, new RoutedEventArgs(Button.ClickEvent));
            return b;
        }

        private Button NavButton(string icon, string text, string page)
        {
            var b = new Button
            {
                Height = 46,
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Style = MakeButtonStyle("Transparent", "#8B959F", "#161D23", 9)
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(Text(icon, 14, "#00FF88", true, horizontal: HorizontalAlignment.Center, vertical: VerticalAlignment.Center));
            row.Children.Add(Text(text, 9, "#C6CDD2", true, new Thickness(13, 0, 0, 0)));
            b.Content = row;
            b.Click += (_, _) => ApplyPageVisibility(page);
            return b;
        }

        private void ApplyPageVisibility(string page)
        {
            if (_lunarHomePage == null || _lunarPageHost == null)
                return;

            _lunarHomePage.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
            TabProfiles.Visibility = page == "profiles" ? Visibility.Visible : Visibility.Collapsed;
            TabAccounts.Visibility = page == "accounts" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PrepareExistingPage(StackPanel page)
        {
            page.Margin = new Thickness(30, 26, 30, 26);
            page.Background = Brushes.Transparent;
        }

        private Button WindowButton(string text, RoutedEventHandler handler)
        {
            var b = new Button
            {
                Content = text,
                Width = 38,
                Height = 38,
                Margin = new Thickness(3, 0, 0, 0),
                Style = MakeButtonStyle("Transparent", "#7D8791", "#1A2127", 15)
            };
            b.Click += handler;
            return b;
        }

        private Style MakeButtonStyle(string background, string foreground, string hover, double fontSize)
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brush(background)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(foreground)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#263039")));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.FontSizeProperty, fontSize));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 5, 10, 5)));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brush(hover)));
            hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#31503F")));
            style.Triggers.Add(hoverTrigger);
            return style;
        }

        private static SolidColorBrush Brush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);

        private static TextBlock Text(string text, double size, string color, bool bold, Thickness? margin = null,
            HorizontalAlignment horizontal = HorizontalAlignment.Left, VerticalAlignment vertical = VerticalAlignment.Top)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = Brush(color),
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Margin = margin ?? new Thickness(0),
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                TextWrapping = TextWrapping.Wrap
            };
        }
    }
}
