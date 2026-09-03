using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private static readonly string[] RuntimeVanillaVersions =
        {
            "1.8.9", "1.20.1", "1.21.1", "1.21.2", "1.21.4", "1.21.5", "1.21.8", "1.21.11", "26.1.2", "26.2"
        };

        private static readonly string[] RuntimeFabricVersions =