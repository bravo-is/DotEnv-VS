# Release validation

Status: **blocked from publication until all required IDE checks pass**.

Development testing on October 9, 2026:

- Experimental deployment fix: `Install-Experimental.ps1` now replaces the package in the selected test profile and runs its registration refresh before returning success. Same-version reinstall into the previously failing VS2026 `DotEnvClean` profile completed at 15:38 EDT. Its next CMake F5 passed at 15:38:43 without opening Options, including file precedence and unrelated-variable assertions. VSIX SHA-256 remained `4f90cbad3d1223080b778a8efb7e27364f619df0db620cae37ea475a64fbfda8`. This resolves the contributor deployment workflow; it is not evidence of a clean customer installation without a refresh.

- Automated suite: 53 checks passed on Windows using .NET SDK 8.0.425.
- Extension build and SDK VSIX packaging: passed with zero warnings and zero errors.
- VS2022 17.14 experimental profile `DotEnvTest`: solution F5 passed at 14:32 EDT, including duplicate-variable precedence and preservation of an unrelated debugger variable. CMake F5 passed at 14:39 EDT. Both sample processes wrote `PASS` and exited successfully.
- VS2026 18.10 experimental profile `DotEnvTest`: development installer accepted. Initial launch failed because the package registration was absent in the new profile; after `/UpdateConfiguration`, solution F5 passed at 14:45 EDT without opening Options. Clean installation still requires a separate test; this repair is not an acceptable user prerequisite.
- CMake settings now expose the detected executable target and generated Debug configuration. A Debug-only explicit file selection passed F5 at 14:55 EDT while the automatic file deliberately contained a conflicting sample value. Additional configurations and target switching still require validation.
- VS2022 CMake Release F5 passed at 14:58 EDT after switching from Debug, ignoring a deliberately conflicting Debug-only override. At 14:59 EDT, a separate launch with conflicting debugger `DOTENV_SAMPLE`, unrelated `DOTENV_UNTOUCHED`, and `--require-preserved` passed. This completes the development engineering milestone for both VS2022 project systems; it does not certify the final release artifact.
- Malformed CMake workspace file prevented launch and showed its path and line number. A second generic IDE error appeared afterward; cancellation handling is being refined and requires retesting.
- Captured real automatic-detection and configuration-specific Options screenshots and included both in the README.
- VS2026 18.10.3 experimental reinstall at 15:13 EDT returned success, but first launch still referenced a removed extension directory. A separate freshly initialized `DotEnvClean` profile installed successfully at 15:19 EDT but first F5 failed injection at 15:20 EDT. Neither check used a post-install registration repair. These remain failed installation checks; standard-profile installation was tested separately below.
- After repairing only the `DotEnvTest` registration, VS2026 CMake F5 passed at 15:17 EDT with conflicting debugger environment and an unrelated variable asserted. The workspace path contained spaces and Unicode. Editing the file produced the expected failing assertion at 15:18:17 and restoring it passed at 15:18:30 without restarting the IDE. Exact development VSIX SHA-256: `4f90cbad3d1223080b778a8efb7e27364f619df0db620cae37ea475a64fbfda8`.
- Standard VS2026 profile: first installation and CMake F5 passed at 15:21 EDT without opening Options or running `/UpdateConfiguration`. The same package then passed solution Debug F5 at 15:22:17 and Release F5 at 15:22:57, including precedence and preservation assertions. The observed registration failures are limited to the experimental-profile installer path so far; a fresh normal Windows user profile is still needed for clean-profile certification.
- Native C++ sample compiled with VS2022 Debug and VS2026 Release toolchains; this is compilation evidence, not F5 evidence.
- VSIX metadata, identity, packaged-file allowlist, license, and 90 × 90 icon validation passed.

The observations above cover development builds; entries that name a checksum identify their exact artifact. Contributor deployment and documentation changes do not change the installed VSIX. Build and test the final release artifact before certification.

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
