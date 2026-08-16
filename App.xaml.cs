using System;
using System.Windows;

namespace TopuLauncher
{
    public partial class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var app = new App();
            app.InitializeComponent();
            
            var mainWindow = new MainWindow();
            app.Run(mainWindow);
        }
    }
}
