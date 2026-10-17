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

- Steps 1, 2, 3 and 6 are done and committed on `main`.
- Steps 4 and 5 are done (2026-10-17) on branch `chore/package-updates` as one commit, because Verify 33 pulls in xUnit 4. The versions are Verify.XunitV3 33.3.4, xunit.v3.mtp-v2 4.0.2 and xunit.runner.visualstudio 4.0.1. The `xunit.analyzers` pin was removed (it now resolves 2.2.0, and MTP resolves 2.5.1).
- Verify 33 fails the build (SponsorCheck SC021) unless a license property is set. The integration test csproj declares `Verify_SponsorshipExemption=OpenSource` with `Verify_SponsorshipExemptionUntil=2027-10`, approved by the owner. **Renew it before 2027-10**: bump it by at most 12 months, and only if the project is still open source with no revenue. After that month the build fails again.
- Step 3 found a vulnerable transitive `System.Security.Cryptography.Pkcs` 9.0.16 in the build project. It is now pinned to 9.0.21 in `build/Directory.Packages.props` (`vulnerable transitive dependencies`). Remove the pin once Cake's dependencies resolve a fixed version on their own.
- Next: steps 7–9. Step 9 should take `diffenginetray` to 20.x to match the DiffEngine 20 that Verify 33 uses.

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
