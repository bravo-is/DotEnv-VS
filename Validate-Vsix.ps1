param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Path))
try {
    $expected = @('extension.vsixmanifest','VsDebugEnv.dll','VsDebugEnv.UI.dll','VsDebugEnv.vsdconfig','VsDebugEnv.UI.pkgdef','LICENSE','icon.png','[Content_Types].xml','manifest.json','catalog.json')
    $actual = @($archive.Entries.FullName)
    $difference = Compare-Object $expected $actual
    if ($difference) { throw 'VSIX has missing or unexpected files. Check the packaging allowlist.' }
    $reader = [IO.StreamReader]::new($archive.GetEntry('extension.vsixmanifest').Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $identity = $manifest.PackageManifest.Metadata.Identity
    if ($identity.Id -ne 'VsDebugEnv.81acf145-db5f-4adf-b854-120ad064a2cb') { throw 'Upgrade identity changed.' }
    if ($identity.Publisher -ne 'Israel Bravo') { throw 'Unexpected publisher.' }
    if ($manifest.PackageManifest.Metadata.DisplayName -ne 'DotEnv for Visual Studio') { throw 'Unexpected product name.' }
    foreach ($name in @($manifest.PackageManifest.Assets.Asset.Path) + @($manifest.PackageManifest.Metadata.Icon, $manifest.PackageManifest.Metadata.License)) {
        if (-not $archive.GetEntry($name)) { throw "Missing manifest asset: $name" }
    }
    Add-Type -AssemblyName System.Drawing
    $stream = $archive.GetEntry('icon.png').Open()
    try {
        $icon = [Drawing.Image]::FromStream($stream)
        try { if ($icon.Width -ne 90 -or $icon.Height -ne 90) { throw 'Marketplace icon must be 90 x 90.' } }
        finally { $icon.Dispose() }
    } finally { $stream.Dispose() }
    Write-Host "Validated DotEnv $($identity.Version): identity, assets, icon, and source-exclusion allowlist."
} finally { $archive.Dispose() }
