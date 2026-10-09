# Build and contribute

Use Windows, the .NET 8 SDK, and .NET Framework 4.7.2 or a compatible later runtime. NuGet restores the Visual Studio reference assemblies and build tools.

```powershell
dotnet run --project tests/Tests.csproj -c Release
./Build.ps1
```

The tests are an executable suite, not a `dotnet test` project. `Build.ps1` compiles the command table and assemblies with warnings treated as errors, validates VSIX packaging and assets, and writes `dist/DotEnvForVisualStudio-<version>.vsix` plus its SHA-256 checksum. `dist/DebugEnvFile.vsix` remains a development alias.

Use a dedicated experimental Visual Studio root suffix when installing development packages. Do not use a production IDE profile for development. Run the [acceptance matrix](docs/release-validation.md) on both supported Visual Studio generations against the exact intended release installer.

Follow the [live IDE test instructions](tests/IDE-TESTS.md) to reproduce file precedence, configuration switching, and failure checks using the runnable samples.

Source layout: `src/` contains parsing, selection, settings, and debugger integration; `ui/` contains the Visual Studio package and native configuration interface; `tests/` contains automated checks; `samples/` contains runnable C++ examples.

Never commit `.recovery`, SDKs, build outputs, local settings, or real `.env` files. Use `.env.example` and sample values in tests and documentation. Keep diagnostics free of environment values.
