using System.Threading.Tasks;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.Forge.Installers;
using CmlLib.Core.Installer.Forge.Versions;

namespace TopuLauncher
{
    internal static class ForgeInstallerExtensions
    {
        public static Task<string> Install(this ForgeInstaller installer, ForgeVersion version)
        {
            return installer.Install(version, new ForgeInstallOptions());
        }
    }
}
