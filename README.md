# DotEnv for Visual Studio

Load a `.env` file when you press F5 in a native C++ solution or CMake project. Free, MIT-licensed, and package-manager independent.

**Development preview:** 1.0 is not published yet. Real F5 validation and the release acceptance matrix are still in progress. The current development package is 0.9.0.

## Quick start

1. Install **DotEnv for Visual Studio** and restart Visual Studio.
2. Put `.env` beside your `.vcxproj`, or in your opened CMake folder.
3. Press **F5**.

```dotenv
DOTENV_SAMPLE=loaded-from-file
TOOLS=C:\tools\bin
```

A solution project without its own `.env` falls back to the solution directory's `.env`. No automatically discovered file means debugging continues normally. Exactly one file is loaded for each process, and it is reread each time.

![Visual Studio Options showing Automatic mode and the detected CMake workspace .env path](docs/images/automatic-detection.jpg)

## Choose a different file

Open **Tools → DotEnv for Visual Studio**. Select your project or target, choose a configuration (or **All configurations**), select **Choose file**, and browse to your file. Click **Validate**, then **Save selection**.

Use **Disabled** to turn off injection for that scope, or **Inherit** to remove an override. Settings stay on your computer; no scripts or project/launch-file edits are required.

![Visual Studio Options showing a saved Debug-only override that selects debug.env in a CMake workspace](docs/images/configuration-override.jpg)

[Advanced configuration](docs/configuration.md) · [Syntax reference](docs/dotenv-syntax.md) · [Troubleshooting](docs/troubleshooting.md)

## Planned 1.0 workflows

| Workflow | Release scope |
| --- | --- |
| Native C++ `.sln` / `.vcxproj`, local F5 | Included; IDE validation pending |
| CMake opened as a folder, local F5 | Included; IDE validation pending |
| VS 2022 17.12+ on x64 Windows | Included; IDE validation pending |
| VS 2026 on x64 Windows | Included; IDE validation pending |
| C#, remote/WSL, attach, ARM64 IDE, Ctrl+F5 | Outside initial support promise |

See the [release validation record](docs/release-validation.md) for tested artifacts and remaining checks. The release will not be published until the included workflows pass.

## Try the examples

- [C++ solution](samples/solution): copy `.env.example` to `.env`, open `DotEnvSample.sln`, and press F5.
- [CMake folder](samples/cmake): copy `.env.example` to `.env`, open this folder in Visual Studio, wait for configuration, choose `DotEnvSample`, and press F5.

Each program prints PASS or FAIL and writes `dotenv-result.txt`. The solution example deliberately supplies a conflicting debugger variable to check that the file takes precedence.

## Project information

[Build and contribute](CONTRIBUTING.md) · [Changelog](CHANGELOG.md) · [MIT license](LICENSE) · [Privacy](PRIVACY.md)

No telemetry or external network calls. Output diagnostics report file paths and safe errors, never environment values. Keep real `.env` files out of source control and issue reports.
