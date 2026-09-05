using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private static readonly object Java8VanillaHandlerRegistration = RegisterJava8VanillaLaunchHandler();

        private static object RegisterJava8VanillaLaunchHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(Button),
                Button.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(Java8VanillaLaunchButtonHandler));

            return new object();
        }

        private static void Java8VanillaLaunchButtonHandler(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button || Window.GetWindow(button) is not MainWindow window)
                return;

            if (!ReferenceEquals(button, window.LaunchBtn))
                return;

            RuntimeProfileSettings profile = window.GetRuntimeProfile();

            if (!profile.Loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) ||
                !profile.Version.Equals("1.8.9", StringComparison.OrdinalIgnoreCase))
                return;

            e.Handled = true;
            _ = window.LaunchJava8VanillaProfileAsync();
        }
    }
}
