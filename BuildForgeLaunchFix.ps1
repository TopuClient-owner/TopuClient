$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot 'MainWindow.LoaderRuntime.cs'
if (-not (Test-Path $sourcePath)) {
    throw "MainWindow.LoaderRuntime.cs was not found."
}

$source = [System.IO.File]::ReadAllText($sourcePath)
$marker = '// TOPU FORGE MODULE PATH FIX'

if ($source.Contains($marker)) {
    Write-Host 'Forge launch fix already applied.'
    exit 0
}

$oldCall = @'
                if (process == null)
'@

$newCall = @'
                // TOPU FORGE MODULE PATH FIX
                // Forge 1.20.1 uses BootstrapLauncher as a Java module. CmlLib's
                // generated command can contain a normal -cp in front of the
                // module-path command, which makes Java unable to resolve
                // cpw.mods.bootstraplauncher.BootstrapLauncher on some runtimes.
                if (loaderType.Equals("Forge", StringComparison.OrdinalIgnoreCase))
                {
                    process.StartInfo.Arguments = NormalizeForgeProcessArguments(process.StartInfo.Arguments);
                    WriteLog("Applied Forge module-path launch normalization.");
                }

                if (process == null)
'@

if (-not $source.Contains($oldCall)) {
    throw 'Could not find the Forge process insertion point.'
}
$source = $source.Replace($oldCall, $newCall)

$methodMarker = '        private Button? FindButtonByContent(Panel parent, string content)'
$method = @'
        private string NormalizeForgeProcessArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return arguments;

            // Forge 1.20.1's BootstrapLauncher belongs on the module path.
            // Remove the broad classpath generated before it and use the
            // module-path form expected by Forge's official launch command.
            string normalized = System.Text.RegularExpressions.Regex.Replace(
                arguments,
                @"(?<!\S)-cp\s+(?:\"[^\"]*\"|\S+)",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"(?<!\S)-p(?=\s)",
                "--module-path",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

            return normalized.Trim();
        }

'@

if (-not $source.Contains($methodMarker)) {
    throw 'Could not find the Forge helper insertion point.'
}
$source = $source.Replace($methodMarker, $method + $methodMarker)

[System.IO.File]::WriteAllText($sourcePath, $source, [System.Text.UTF8Encoding]::new($false))
Write-Host 'Applied Forge module-path launch fix.'
