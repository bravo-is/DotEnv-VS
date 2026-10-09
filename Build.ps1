# Developer-only build entry point. Installing the prebuilt VSIX requires no script.
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build ui/VsDebugEnv.UI.csproj -c Release -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Extension build failed.' }

    $buildTools = dotnet msbuild ui/VsDebugEnv.UI.csproj -nologo -getProperty:PkgMicrosoft_VSSDK_BuildTools
    if ($LASTEXITCODE -ne 0 -or -not $buildTools) { throw 'Could not locate restored VSSDK BuildTools.' }
    $vsixUtil = Join-Path $buildTools.Trim() 'tools/vssdk/bin/VsixUtil.exe'
    $createPkgDef = Join-Path $buildTools.Trim() 'tools/vssdk/bin/CreatePkgDef.exe'
    & $createPkgDef '/out=ui/obj/VsDebugEnv.UI.pkgdef' /codebase (Join-Path $PSScriptRoot 'ui/bin/Release/net472/VsDebugEnv.UI.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Package registration generation failed.' }

    New-Item -ItemType Directory -Path dist -Force | Out-Null
    $files = @(
        'src/bin/Release/net472/VsDebugEnv.dll'
        'ui/bin/Release/net472/VsDebugEnv.UI.dll'
        'src/bin/Release/net472/VsDebugEnv.vsdconfig'
        'ui/obj/VsDebugEnv.UI.pkgdef'
        'LICENSE'
        'assets/icon.png'
    ) | ForEach-Object { @{ path = (Join-Path $PSScriptRoot $_) } }
    @{ files = @($files) } | ConvertTo-Json -Depth 4 |
        Set-Content dist/package-files.json -Encoding UTF8

    # The SDK emits manifest.json and catalog.json required by modern VSIX installers.
    # A plain ZIP of the DLLs and XML manifest is insufficient.
    & $vsixUtil package -outputPath dist/DebugEnvFile.vsix `
        -sourceManifest source.extension.vsixmanifest `
        -files dist/package-files.json `
        -vsixSchemaPath (Join-Path $buildTools.Trim() 'tools/vssdk/schemas') `
        -is64BitBuild -setupProductArch amd64

    if ($LASTEXITCODE -ne 0) { throw 'VSIX packaging or validation failed.' }
    [xml]$manifest = Get-Content source.extension.vsixmanifest
    $version = $manifest.PackageManifest.Metadata.Identity.Version
    $versioned = "dist/DotEnvForVisualStudio-$version.vsix"
    Copy-Item -LiteralPath dist/DebugEnvFile.vsix -Destination $versioned
    & ./Validate-Vsix.ps1 -Path $versioned
    $hash = (Get-FileHash -LiteralPath $versioned -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path $versioned -Leaf)" | Set-Content "$versioned.sha256" -Encoding ascii
    Write-Host "Built dist/DebugEnvFile.vsix"
} finally {
    Pop-Location
}
