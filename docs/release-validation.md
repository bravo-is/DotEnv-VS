# Release validation

Status: **blocked from publication until all required IDE checks pass**.

Development testing on October 9, 2026:

- Automated suite: 53 checks passed on Windows using .NET SDK 8.0.425.
- Extension build and SDK VSIX packaging: passed with zero warnings and zero errors.
- VS2022 17.14 experimental profile `DotEnvTest`: solution F5 passed at 14:32 EDT, including duplicate-variable precedence and preservation of an unrelated debugger variable. CMake F5 passed at 14:39 EDT. Both sample processes wrote `PASS` and exited successfully.
- VS2026 18.10 experimental profile `DotEnvTest`: development installer accepted. Initial launch failed because the package registration was absent in the new profile; after `/UpdateConfiguration`, solution F5 passed at 14:45 EDT without opening Options. Clean installation still requires a separate test; this repair is not an acceptable user prerequisite.
- CMake settings now expose the detected executable target and generated Debug configuration. A Debug-only explicit file selection passed F5 at 14:55 EDT while the automatic file deliberately contained a conflicting sample value. Additional configurations and target switching still require validation.
- VS2022 CMake Release F5 passed at 14:58 EDT after switching from Debug, ignoring a deliberately conflicting Debug-only override. At 14:59 EDT, a separate launch with conflicting debugger `DOTENV_SAMPLE`, unrelated `DOTENV_UNTOUCHED`, and `--require-preserved` passed. This completes the development engineering milestone for both VS2022 project systems; it does not certify the final release artifact.
- Malformed CMake workspace file prevented launch and showed its path and line number. A second generic IDE error appeared afterward; cancellation handling is being refined and requires retesting.
- Captured real automatic-detection and configuration-specific Options screenshots and included both in the README.
- VS2026 experimental uninstall and reinstall both returned success for development VSIX SHA-256 `4f90cbad3d1223080b778a8efb7e27364f619df0db620cae37ea475a64fbfda8`. A fresh CMake sample with spaces and Unicode in its path is open. First F5 after this install is pending because desktop activation failed twice; no registration-repair command has been run after this reinstall.
- Native C++ sample compiled with VS2022 Debug and VS2026 Release toolchains; this is compilation evidence, not F5 evidence.
- VSIX metadata, identity, packaged-file allowlist, license, and 90 × 90 icon validation passed.

The installed test package predates subsequent source fixes. Rebuild and reinstall before treating results as release evidence. This record is not a release certification.

## Acceptance matrix

Record PASS/FAIL, date, exact Visual Studio version, VSIX SHA-256, profile suffix, and supporting evidence for each row in both VS2022 and VS2026. Never record environment values.

| Check | VS2022 | VS2026 |
| --- | --- | --- |
| Clean install; first F5 without opening Options | Pending | Pending |
| C++ solution automatic project file; solution fallback | Pending | Pending |
| CMake automatic workspace file | Pending | Pending |
| File overrides debugger duplicate; unrelated variables preserved | Pending | Pending |
| Debug/Release switching and per-project overrides | Pending | Pending |
| Out-of-tree build and startup target changes | Pending | Pending |
| Multiple startup projects and simultaneous IDE instances | Pending | Pending |
| File edits between launches | Pending | Pending |
| Unicode and spaced paths | Pending | Pending |
| Missing automatic file; missing explicit file | Pending | Pending |
| Malformed file; disabled mode | Pending | Pending |
| Unsupported launch types do not receive injection | Pending | Pending |
| Upgrade from 0.2.1 preserves explicit and disabled settings | Pending | Pending |
| Extension disable and uninstall | Pending | Pending |
| Automatic and configuration-specific UI screenshots | Pending | Pending |
| Public Marketplace artifact installs and both quick starts pass | Pending | Pending |

## Release procedure

1. Complete the engineering milestone: actual F5 injection, project association, and precedence in both project systems.
2. Resolve every acceptance failure; do not silently narrow the planned support scope.
3. Set final version, build through CI, validate VSIX metadata/assets, and produce SHA-256.
4. Run the matrix against that exact installer and record evidence. Capture two real screenshots using sample values.
5. Prepare the Marketplace listing under Israel Bravo. The owner completes sign-in/MFA and account agreements interactively. Preferred publisher id: `bravo-is`; internal listing name: `DotEnvForVisualStudio`, subject to availability.
6. Upload the verified VSIX privately, inspect the rendered overview and compatibility, then make public.
7. Install the public download in clean profiles and repeat both samples on both Visual Studio generations.
8. Publish the matching GitHub release and add the verified Marketplace link to the README.

No self-signed production package. The initial release is intended to be unsigned. Keep account recovery records, settings, SDKs, and real environment files outside published artifacts.
