using System;
using System.IO;
using System.Linq;

namespace TopuLauncher;

internal static class BuildFix
{
    // Compatibility helpers for the current CmlLib.Core.Auth.Microsoft package.
    // The launcher source historically referenced a few controls that were removed
    // from the redesigned XAML. These helpers are intentionally kept tiny so the
    // existing launcher code is not rewritten.
}
