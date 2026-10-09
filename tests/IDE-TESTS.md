# Live IDE tests

These checks are for extension contributors. Users do not need launch configuration files to use DotEnv.

Record the exact VSIX checksum, Visual Studio version, profile suffix, timestamp, and outcome in the [release validation record](../docs/release-validation.md). Use only the supplied sample values. Build success alone is not evidence that the debugger injected the environment.

## Basic launch and precedence

1. For development, close Visual Studio and run `Install-Experimental.ps1` with the intended installation path and `-VsixPath` pointing to the exact artifact. It installs and refreshes the experimental profile's registration. Launch the printed command and open the solution sample. Do not open DotEnv Options before the first F5. Keep normal customer-installation checks separate; they must not use this refresh helper.
2. Copy its `.env.example` to `.env`. Press F5. The sample must exit zero and write a new `dotenv-result.txt` containing `PASS`. Its project already supplies a conflicting debugger variable and an unrelated variable; both assertions must pass.
3. Open the CMake sample folder. Copy `.env.example` to `.env`, wait for configuration, select `DotEnvSample.exe`, and press F5. Check the result timestamp under `out/build/debug`.
4. For the separate CMake precedence test, use **Debug → Debug and Launch Settings for DotEnvSample**. Add the following to the generated launch configuration:

   ```json
   "args": ["--require-preserved"],
   "env": {
     "DOTENV_SAMPLE": "from-debug-settings",
     "DOTENV_UNTOUCHED": "preserved"
   }
   ```

5. Press F5 again. Both assertions must pass. This test-only configuration stays in the ignored `.vs` directory. Its syntax follows [Microsoft's CMake debugging documentation](https://learn.microsoft.com/en-us/cpp/build/configure-cmake-debugging-sessions?view=msvc-170).

## Configuration selection and rereading

1. Copy the CMake sample `.env.example` to `debug.env`. In DotEnv Options choose workspace defaults, **Debug**, **Choose file**, and `debug.env`; save.
2. Change `.env` to `DOTENV_SAMPLE=wrong-automatic-file`. Debug F5 must still pass.
3. Restore `.env` from `.env.example`. Change `debug.env` to `DOTENV_SAMPLE=wrong-debug-override`. Switch the IDE preset to Release, wait for configuration, and press F5. Release must pass, demonstrating that the Debug override did not leak.
4. Restore both files. Repeat with a target-level override and solution project/configuration overrides.
5. Edit the selected file between launches without restarting the IDE. The next process must receive the updated content.

## Failure behavior

Use `DOTENV_INVALID_LINE` as malformed sample content. F5 must stop before the sample process runs, and diagnostics must identify the selected path and line without displaying values. Check that the result file timestamp has not changed. A missing explicitly selected file must similarly block; a missing automatic file must allow the program to launch without injection. Disabled mode must bypass malformed content.

Restore the sample files after testing. Complete the remaining matrix rows, including multiple startup projects, out-of-tree builds, Unicode paths, independent IDE instances, upgrades, unsupported launches, disable/uninstall, and Marketplace download validation on both supported IDE generations.
