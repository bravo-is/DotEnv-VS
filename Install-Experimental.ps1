# Contributor-only deployment. Marketplace users install the VSIX normally.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$VisualStudioPath,
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]{0,39}$')][string]$RootSuffix = 'DotEnvTest',
    [string]$VsixPath = (Join-Path $PSScriptRoot 'dist/DebugEnvFile.vsix')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Never deploy to the default IDE profile, even if an empty suffix is supplied.
if ([string]::IsNullOrWhiteSpace($RootSuffix)) { throw 'An experimental root suffix is required.' }
$vsRoot = (Resolve-Path -LiteralPath $VisualStudioPath).Path.TrimEnd('\')
$package = (Resolve-Path -LiteralPath $VsixPath).Path
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$instances = & $vswhere -all -prerelease -products '*' -format json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Visual Studio discovery failed.' }
$vsMatches = @($instances | Where-Object { $_.installationPath.TrimEnd('\') -eq $vsRoot })
if ($vsMatches.Count -ne 1) { throw 'VisualStudioPath must identify one installed Visual Studio instance.' }
$instance = $vsMatches[0]
$installer = Join-Path $vsRoot 'Common7/IDE/VSIXInstaller.exe'
$devenv = Join-Path $vsRoot 'Common7/IDE/devenv.exe'
if (!(Test-Path -LiteralPath $installer) -or !(Test-Path -LiteralPath $devenv)) { throw 'Visual Studio tools are missing.' }
if (Get-Process devenv -ErrorAction SilentlyContinue) { throw 'Close Visual Studio before deploying. No processes were stopped.' }

& (Join-Path $PSScriptRoot 'Validate-Vsix.ps1') -Path $package
$extensionId = 'VsDebugEnv.81acf145-db5f-4adf-b854-120ad064a2cb'
$major = ([version]$instance.installationVersion).Major
$profile = Join-Path $env:LOCALAPPDATA "Microsoft/VisualStudio/$major.0_$($instance.instanceId)$RootSuffix"
$logRoot = Join-Path $PSScriptRoot ('.recovery/experimental-install/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

function Invoke-CheckedTool([string]$Exe, [string[]]$ToolArguments, [string]$Step) {
    Write-Host "$Step..."
    # Start-Process joins arguments into one command line on Windows.
    $quoted = foreach ($argument in $ToolArguments) {
        if ($argument.Contains('"')) { throw 'Arguments cannot contain quotation marks.' }
        # devenv treats quoted switches as file names. Quote only values needing it.
        if ($argument -match '\s') { '"' + $argument + '"' } else { $argument }
    }
    $process = Start-Process -FilePath $Exe -ArgumentList ($quoted -join ' ') -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "$Step failed (exit $($process.ExitCode)). Logs: $logRoot. Installer logs also remain in $env:TEMP." }
}

# Same-version development packages need replacement. Uninstall only this identity
# in the selected experimental profile; leave all other extensions/settings alone.
$installed = @()
$extensions = Join-Path $profile 'Extensions'
if (Test-Path -LiteralPath $extensions) {
    $installed = @(Get-ChildItem -LiteralPath $extensions -Filter extension.vsixmanifest -Recurse | Where-Object {
        [xml]$manifest = Get-Content -LiteralPath $_.FullName
        $manifest.PackageManifest.Metadata.Identity.Id -eq $extensionId
    })
}
$common = @('/quiet', "/rootSuffix:$RootSuffix", "/instanceIds:$($instance.instanceId)")
$logId = Split-Path $logRoot -Leaf
if ($installed.Count -gt 0) {
    $uninstallLog = "DotEnv-$logId-uninstall.log"
    Invoke-CheckedTool $installer ($common + @("/uninstall:$extensionId", "/logFile:$uninstallLog")) 'Uninstall'
    Copy-Item -LiteralPath (Join-Path $env:TEMP $uninstallLog) -Destination (Join-Path $logRoot 'uninstall.log')
}
$installLog = "DotEnv-$logId-install.log"
Invoke-CheckedTool $installer ($common + @("/logFile:$installLog", $package)) 'Install'
Copy-Item -LiteralPath (Join-Path $env:TEMP $installLog) -Destination (Join-Path $logRoot 'install.log')

# VSIXInstaller can leave named experimental profiles with a stale pkgdef cache.
# Rebuild it after installation, using the same VS executable and root suffix.
Invoke-CheckedTool $devenv @('/RootSuffix', $RootSuffix, '/UpdateConfiguration', '/Log', (Join-Path $logRoot 'configuration.xml')) 'Registration refresh'
Write-Host "Installed and refreshed DotEnv in $($instance.displayName), profile $RootSuffix."
Write-Host "Logs: $logRoot"
Write-Host "Launch: & '$devenv' /RootSuffix $RootSuffix"
