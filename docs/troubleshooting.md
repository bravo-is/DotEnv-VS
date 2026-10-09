# Troubleshooting

Open **View → Output**, then select **DotEnv for Visual Studio**. Diagnostics show the selected path and reason, never environment values.

| Symptom | Check |
| --- | --- |
| Nothing is loaded | Automatic looks only for `.env` beside the `.vcxproj`, then beside the solution, or in the opened CMake folder. No file is a normal no-op. |
| Selected file does not exist | Open DotEnv settings and browse to the file, or choose Automatic/Disabled. Explicit missing files stop F5. |
| Invalid syntax | Open the reported file at the reported line and compare with the [syntax reference](dotenv-syntax.md). Save as UTF-8. |
| Wrong configuration | Check **Apply to** and **Configuration**. A project configuration override takes priority over a workspace default. |
| Cannot associate executable | Check the startup project's Debugging Command. For CMake, reconfigure and choose an executable target. Distinct targets must have distinguishable executable paths. |
| CMake information is unavailable | Wait for configuration to finish, then retry. The extension reads CMake's generated target metadata. |
| A setting will not save | Correct the validation error first. Invalid/future settings files are not silently overwritten; restore `settings.json.bak` if appropriate. |
| Unsupported workflow | Version 1 targets local native C++ F5. C#, remote/WSL, attach, ARM64 IDEs, and Ctrl+F5 are outside its support promise. |

When reporting an issue, include Visual Studio version, extension version, project system, configuration, and reproduction steps using sample values. Never attach your real `.env`, passwords, tokens, or environment dumps.
