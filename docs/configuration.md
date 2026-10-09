# Choose an environment file

Open **Tools → DotEnv for Visual Studio**, or **Tools → Options → DotEnv for Visual Studio → Environment files**.

Choose the scope in **Apply to**, then a configuration (or **All configurations**). Select a mode, validate, and click **Save selection**.

| Mode | Behavior |
| --- | --- |
| Automatic | Use the project's `.env`, falling back to the solution's `.env`. CMake uses the opened folder's `.env`. |
| Choose file | Read exactly the file you select. Missing or invalid files stop the launch. |
| Disabled | Leave the debugger's environment unchanged. |
| Inherit | Remove this override and use the next applicable setting. |

The order, from most specific to least specific, is project/target configuration, project/target, workspace configuration, workspace default. No setting means Automatic.

For example, select your project, choose **Release|x64**, choose `release.env`, and save. Debug continues to inherit its existing setting. Files are never merged.

Relative paths in workspace defaults start at the workspace root. Paths in project overrides start beside the `.vcxproj`; CMake target paths start at the opened folder. Browse selects an absolute path.

Paths may use `${workspaceRoot}`, `${projectDir}`, `${configuration}`, `${platform}`, `${exeDir}`, or `${buildDir}` when available for the selected launch. Prefer Browse for a fixed file. An unknown or unavailable macro is an error. Values *inside* the environment file are always literal.

Settings are local to your Windows account at `%LOCALAPPDATA%\VsDebugEnv\settings.json`, with a previous-version `.bak` copy. Settings use format version 2. Keep settings and real environment files out of source control.

## Upgrading from Debug Env File

The extension retains its VSIX identity. Existing explicit selections retain their executable-relative path and legacy macro behavior; disabled settings remain disabled. Automatic selection now uses conventional `.env` locations. It no longer discovers package-manager-specific filenames. Select a generated file using **Choose file** if needed.
