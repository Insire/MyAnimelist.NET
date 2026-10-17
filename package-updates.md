# Package update plan

Inventory taken on 2026-10-16 with `dotnet list package --outdated`, plus a manual check of the tool versions that `dotnet list` doesn't see.

This file is a permanent work queue. Work through the steps **in order** and **delete each step once it is done**.

**Never delete this file, and never delete the Workflow section, even when no steps are left.** It's reused for future update rounds: add new steps below the workflow and refresh the inventory date above.

Steps marked **Decision needed** wait for the repo owner's answer. Don't start them before that.

## Workflow (for every step)

1. **Branch.** Work on a branch, not on `main`, for example `chore/package-updates`. **Don't commit.** The owner reviews and commits every change. Leave the changes uncommitted and suggest a commit message per step, like `chore(deps): <step title>`.
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
   - Leave the changes uncommitted. List the changed files and a suggested commit message for the owner.
   - Delete the step from this file.
   - Update `CLAUDE.md` if a fact it records changed.
8. **When all steps are done:**
   - Run `dotnet list MyAnimelist.NET.slnx package --outdated` and `dotnet list build/Build.csproj package --outdated` once more.
   - Run `dotnet list MyAnimelist.NET.slnx package --vulnerable --include-transitive`.
   - Add a step for anything new these commands turn up.
   - If they turn up nothing, leave the file with just the workflow and a line saying "No pending updates as of YYYY-MM-DD".

If a step fails and the fix isn't obvious, revert that step's change, write the failure under the step, and move on to the next independent step.

---

No pending updates as of 2026-10-17. Both solutions have no outdated or vulnerable packages, and the Cake and local tools are on their latest versions.

**Standing notes for future rounds:**

- **Verify's maintenance fee:** the integration test csproj claims `Verify_SponsorshipExemption=OpenSource` until `Verify_SponsorshipExemptionUntil=2027-10`, approved by the owner on 2026-10-17. After that month the build fails with SponsorCheck SC021.
  - Renewing means bumping it by at most 12 months, and only while the project is still open source with no revenue.
  - Ask the owner before changing what is claimed.
- **`System.Security.Cryptography.Pkcs` pin:** it's pinned to 9.0.21 in `build/Directory.Packages.props` (`vulnerable transitive dependencies`), because Cake's dependencies still resolve a vulnerable 9.0.20.
  - Each round, remove the pin, run `dotnet list build/Build.csproj package --vulnerable --include-transitive`, and drop the pin for good once that comes back clean.
- **Codecov uploader:** `CodecovUploader` 0.8.0 is the latest version, but Codecov deprecated it in favor of the Codecov CLI. Replacing it is a separate task.
- **Old tool versions in `tools/`:** after a Cake tool version bump, `tools/` keeps the old versions too.
  - Check the Setup log lines (`nuget.exe (...)`, `dotnet-coverage (...)`) to confirm the new paths are the ones resolved.
  - `UpdateOpenApiSpecTask` picks NSwag's `net10.0` build explicitly.
