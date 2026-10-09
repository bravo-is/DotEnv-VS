# Build and contribute

Use Windows, the .NET 8 SDK, and .NET Framework 4.7.2 or a compatible later runtime. NuGet restores the Visual Studio reference assemblies and build tools.

```powershell
dotnet run --project tests/Tests.csproj -c Release
./Build.ps1
```

The tests are an executable suite, not a `dotnet test` project. `Build.ps1` compiles the command table and assemblies with warnings treated as errors, validates VSIX packaging and assets, and writes `dist/DotEnvForVisualStudio-<version>.vsix` plus its SHA-256 checksum. `dist/DebugEnvFile.vsix` remains a development alias.

Use a dedicated experimental Visual Studio root suffix when installing development packages. Do not use a production IDE profile for development. Run the [acceptance matrix](docs/release-validation.md) on both supported Visual Studio generations against the exact intended release installer.

Close Visual Studio, then deploy the built package with:

```powershell
./Install-Experimental.ps1 -VisualStudioPath 'C:\Program Files\Microsoft Visual Studio\2022\Community'
# For another installation, supply its installation directory, for example:
./Install-Experimental.ps1 -VisualStudioPath 'E:\VisualStudio2026' -RootSuffix DotEnvTest
```

This contributor command replaces DotEnv only in the selected experimental profile and runs `devenv /RootSuffix <suffix> /UpdateConfiguration` before reporting success. The refresh prevents a stale package registration after repeated development installs, including same-version replacements. It preserves the profile's settings and other extensions; logs stay in ignored `.recovery/experimental-install`. Start the profile using the command printed by the script, then test F5 without opening Options.

This is development deployment, not a clean customer-installation test. Test release installation separately through the normal VSIX installer without the refresh command. Regular users need no script.

Follow the [live IDE test instructions](tests/IDE-TESTS.md) to reproduce file precedence, configuration switching, and failure checks using the runnable samples.

Source layout: `src/` contains parsing, selection, settings, and debugger integration; `ui/` contains the Visual Studio package and native configuration interface; `tests/` contains automated checks; `samples/` contains runnable C++ examples.

Never commit `.recovery`, SDKs, build outputs, local settings, or real `.env` files. Use `.env.example` and sample values in tests and documentation. Keep diagnostics free of environment values.
