# Privacy

DotEnv for Visual Studio has no telemetry and makes no external network calls.

The extension reads the selected environment file locally and supplies its values to the debugged process. It stores local selection preferences under `%LOCALAPPDATA%\VsDebugEnv`. Output diagnostics include paths, selection reasons, and safe error details; they do not include environment values.

The debugged application can read the supplied values and is responsible for its own handling of them. Visual Studio, GitHub, and Visual Studio Marketplace have their own privacy policies.

Do not include secrets in bug reports or screenshots. Support is handled through repository issues.
