# Package update plan

Inventory taken on 2026-10-16 with `dotnet list package --outdated`, plus a manual check of the tool versions that `dotnet list` doesn't see.

This file is a permanent work queue. Work through the steps **in order** and **delete each step once it is done**.

**Never delete this file, and never delete the Workflow section, even when no steps are left.** It's reused for future update rounds: add new steps below the workflow and refresh the inventory date above.

Steps marked **Decision needed** wait for the repo owner's answer. Don't start them before that.

## Workflow (for every step)

1. **Branch.** Work on a branch, not on `main`, for example `chore/package-updates`. Keep one commit per step, with a message like `chore(deps): <step title>`.
2. **Change versions only where they are defined.** Versions live in these places, never in a `.csproj`:
   - `Directory.Packages.props`: library and test packages (central package management).
   - `build/Directory.Packages.props`: packages for the Cake build project.
   - `build/Program.cs`: tools Cake downloads (`InstallTool(...)` URIs).
   - `.config/dotnet-tools.json`: local dotnet tools (`dotnet tool restore`).
3. **Build both solutions with no warnings or errors:**

   ```sh
   dotnet build MyAnimelist.NET.slnx
   dotnet build build/Build.csproj
   ```

4. **Run the unit tests.** They need no secrets.

   ```sh
   dotnet test --project tests/MyAnimelist.NET.UnitTests/MyAnimelist.NET.UnitTests.csproj
   ```

5. **Run the integration tests** when a step touches test infrastructure (xUnit, Verify, Playwright, MTP, or `Microsoft.Extensions.*`). They need user secrets, which exist on the owner's machine.

   ```sh
   dotnet test --project tests/MyAnimelist.NET.IntegrationTests/MyAnimelist.NET.IntegrationTests.csproj
   ```

6. **Run the step's own checks**, listed under each step.
7. **Close out the step:**
   - Commit.
   - Delete the step from this file.
   - Update `CLAUDE.md` if a fact it records changed.
8. **When all steps are done:**
   - Run `dotnet list MyAnimelist.NET.slnx package --outdated` and `dotnet list build/Build.csproj package --outdated` once more.
   - Run `dotnet list MyAnimelist.NET.slnx package --vulnerable --include-transitive`.
   - Add a step for anything new these commands turn up.
   - If they turn up nothing, leave the file with just the workflow and a line saying "No pending updates as of YYYY-MM-DD".

If a step fails and the fix isn't obvious, revert that step's change, write the failure under the step, and move on to the next independent step.

---

**Progress (2026-10-16):**

- Steps 1, 2, 3 and 6 are done and verified: both builds are clean and all 10 tests pass. They are not committed yet.
- Step 3 found a vulnerable transitive `System.Security.Cryptography.Pkcs` 9.0.16 in the build project. It is now pinned to 9.0.21 in `build/Directory.Packages.props` (`vulnerable transitive dependencies`). Remove the pin once Cake's dependencies resolve a fixed version on their own.
- Next: step 7. Steps 4 and 5 wait for the Verify decision.

## Step 4: xUnit v4 (major). Depends on step 5

| Package | From | To |
| --- | --- | --- |
| xunit.v3.mtp-v2 | 3.2.2 | 4.0.2 |
| xunit.runner.visualstudio | 3.1.5 | 4.0.1 |
| xunit.analyzers (central pin) | 1.27.0 | Remove the pin, or raise it to what xunit 4 requires |

**Breaking changes in xunit 4 and how they affect this repo:**

| Change | Effect here |
| --- | --- |
| Microsoft Testing Platform v1 support dropped; the default is now MTP v2 (2.3.3) | None. We already use `mtp-v2`. Check that `Microsoft.Testing.Extensions.CodeCoverage` 18.12.0 restores against MTP 2.3.3 without NU1605/NU1109 errors. |
| The `Xunit.Runners` namespace was removed | None. Not used. |
| `[assembly: CollectionBehavior]` parallelization properties are obsolete | None. Not used. |
| New ordering model, which breaks custom `ITestCaseOrderer` implementations | None. Not used. |
| MTP report switches renamed (`--report-ctrf` → `--report-xunit-ctrf`, `--report-junit` → `--report-xunit-junit`, and so on) | None. `--report-xunit-trx` (used in `build/Tasks/TestTask.cs`) is unchanged. Confirm that `--filter-trait` still works. |
| Mono support dropped; the native console tool needs the .NET 10 SDK | None. |
| xunit.runner.visualstudio 4.0 only supports xunit v3 4.0 projects and uses the new config file schema | Update it together with xunit.v3.mtp-v2. |

`CentralPackageTransitivePinningEnabled` is on, so the `xunit.analyzers` 1.27.0 entry pins that version for every project. If xunit 4 needs a newer analyzer, restore fails with a downgrade error. Remove the entry, or set it to the version xunit 4 asks for.

**Coupling:** Verify 33 contains a change titled "xunit3 v4" (Verify PR #1848). Verify.XunitV3 31.x probably doesn't work with xunit v4. Do step 5 first, or both together.

- **Checks:**
  - Run all tests through the Cake task's options:

    ```sh
    dotnet test --solution MyAnimelist.NET.slnx --filter-trait "TestCategory=Unit-Test" --filter-trait "TestCategory=Integration-Test" --coverage --report-xunit-trx
    ```

  - Confirm that a `.trx` and a `.coverage` file are written.

Release notes: [xunit.v3 4.0.0](https://xunit.net/releases/v3/4.0.0), [xunit.runner.visualstudio 4.0.0](https://xunit.net/releases/visualstudio/4.0.0).

## Step 5: Verify.XunitV3 31.21.0 → 33.3.4 (two majors). Decision needed

**What needs deciding: the Open Source Maintenance Fee (OSMF).**

- **Who it applies to:** Verify versions released after 2026-09-01 are covered by a maintenance fee for **organizations that generate revenue, and for government agencies**. The fee is a GitHub sponsorship of $5–$100 per month, depending on organization size.
- **How it's enforced:** from v33, a build-time "SponsorCheck" runs. A sponsoring organization declares `Verify_GitHubSponsorAccount` in `Directory.Build.props`. Nothing phones home and there are no license keys.
- **Not documented:** whether a missing sponsorship gives a warning or fails the build. Find out on the first restore or build.
- **Other users:** individuals and open-source projects are not named as covered. The exemptions are listed in the [fee terms](https://github.com/VerifyTests/Verify/blob/main/docs/maintenance-fee.md).
- **Verify 32.0.1** says "OSMF is not required for this release". It's unclear whether 32.x supports xunit v4.

**Options:**

- (a) Go to 33.x, accepting or confirming exemption from the fee. This unblocks xunit v4.
- (b) Stay on 31.x and skip xunit v4 (step 4) for now.
- (c) Replace Verify. It's used for exactly one snapshot test (`OAuthFlowTests`), which could become a plain `Assert.Equal` on the progress list.

**Behaviour changes in 32/33 that could change snapshots:**

| Version | Change |
| --- | --- |
| 32 | Date precision is now decided by ticks. |
| 32 | Explicit `DefaultValueHandling` for bools is honored. |
| 32 | Member converters prefer the exact type. |
| 33 | Dates and UTC offsets are formatted with the invariant culture. |
| 33 | `VerifyResult.Text` is read with the configured encoding. |
| 33 | Argon 0.36 and DiffEngine 20.3. |

Our only snapshot holds strings and doubles, so a diff is unlikely.

- **Checks:**
  - Run the integration tests.
  - If `OAuthFlowTests.*.received.txt` appears, review the diff before accepting it.
  - Update `diffenginetray` in step 9 to match DiffEngine 20.

Release notes: [Verify 32.0.0](https://github.com/VerifyTests/Verify/releases/tag/32.0.0), [Verify 33.0.0](https://github.com/VerifyTests/Verify/releases/tag/33.0.0).

## Step 7: Cake build project packages

| Package | From | To | Notes |
| --- | --- | --- | --- |
| Cake.Frosting | 6.2.0 | 6.3.0 | Minor release. |
| Cake.Codecov | 3.0.0 | 6.0.0 | Majors 4, 5 and 6 only retarget to Cake 4, 5 and 6 and drop .NET 6/7. **This is a fix**: 3.0.0 targets Cake 3, but the build runs Cake 6. |
| Cake.GitVersioning | 3.10.85 | 3.10.94 | Patch release. |
| HtmlAgilityPack | 1.12.1 | 1.13.1 | Minor release. |
| ReportGenerator (package) | 5.4.7 | 5.5.11 | Minor release. Also align the Cake tool in step 8. |
| Jint | 4.2.2 | 4.17.0 | Minor releases, but with behaviour changes (see below). |

**Jint behaviour changes in 4.15–4.17:**

- `JSON.stringify` output is invariant-culture.
- `JSON.parse` rejects trailing commas.
- `Int32Extensions`, `Int64Extensions` and `DoubleExtensions` became internal.
- Number parsing fixes.
- `Array.prototype.concat` now spreads CLR arrays.

We use Jint for one thing: `UpdateOpenApiSpecTask` runs the Redoc page script and reads `__redoc_state.spec.data` with `ToObject()`.

- **Checks:**
  - Run `./build.ps1 --target UpdateOpenApiSpec`.
  - `assets/MyAnimelist.NET_OpenApiSpec.json` should only change if MyAnimeList changed its spec. Review `git diff` on the spec and on `src/AutoGeneratedClient.cs`.
  - If the diff is purely formatting caused by Jint, it's caused by the update. Look into it before committing.
  - Run `./build.ps1 --target CoberturaReport` to exercise ReportGenerator.

## Step 8: Tools that Cake downloads (`build/Program.cs`)

| Tool | From | To | Notes |
| --- | --- | --- | --- |
| GitVersion.Tool | 6.1.0 | 6.8.2 | Minor releases within v6. |
| nuget.commandline | 6.13.2 | 7.9.0 | NuGet 7.0 errors on SHA-1 fingerprints in `sign`/`mssign` and removed `project.json` support. We only use `nuget push`, which is unaffected. |
| ReportGenerator | 5.4.4 | 5.5.11 | Align with the package from step 7. |
| dotnet-coverage | 17.14.2 | 18.12.0 | Major release. `ConvertCoverageTask` runs `dotnet-coverage merge ... -f xml`; check that it still produces the XML. |
| NSwag.ConsoleCore | 14.7.1 | already latest | |
| CodecovUploader | 0.8.0 | already latest | Codecov has deprecated this uploader in favor of the Codecov CLI. That's a separate task, not part of this plan. |

- **Checks:** run `./build.ps1`, the full pipeline. In Release, the integration tests need the secrets as environment variables, because user secrets only load in Debug. Confirm the logs show ConvertCoverage, CoberturaReport and HtmlReport running, and look at `tmp_build/results/reports`.

## Step 9: Local dotnet tools (`.config/dotnet-tools.json`)

| Tool | From | To | Notes |
| --- | --- | --- | --- |
| nswag.consolecore | 14.6.1 | 14.7.1 | Aligns with the Cake-installed 14.7.1, so manual and Cake regeneration use the same generator. |
| diffenginetray | 19.3.1 | 20.8.2 | Major release. Match it to DiffEngine 20 if step 5 moved to Verify 33. I couldn't find 20.0.0 release notes. It's a dev-only tray app, so low risk. |

- **Checks:** run `dotnet tool restore`. Then run `dotnet nswag run assets/MyAnimelist.NET_Config.nswag` and confirm `src/AutoGeneratedClient.cs` has no unexpected diff beyond the generator version comment.
