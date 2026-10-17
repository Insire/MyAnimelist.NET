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
   - `build/Util/Constants.cs`: the Codecov CLI (`CodecovCliVersion` plus one SHA-256 per platform), which the build downloads itself because it has no NuGet package. See the standing note below.
3. **Build both solutions with no warnings or errors:**

   ```sh
   dotnet build SoftThorn.MAL.ApiClient.slnx
   dotnet build build/Build.csproj
   ```

4. **Run the unit tests.** They need no secrets.

   ```sh
   dotnet test --project tests/SoftThorn.MAL.ApiClient.UnitTests/SoftThorn.MAL.ApiClient.UnitTests.csproj
   ```

5. **Run the integration tests** when a step touches test infrastructure (xUnit, Verify, Playwright, MTP, or `Microsoft.Extensions.*`). They need user secrets, which exist on the owner's machine.

   ```sh
   dotnet test --project tests/SoftThorn.MAL.ApiClient.IntegrationTests/SoftThorn.MAL.ApiClient.IntegrationTests.csproj
   ```

6. **Run the step's own checks**, listed under each step.
7. **Close out the step:**
   - Leave the changes uncommitted. List the changed files and a suggested commit message for the owner.
   - Delete the step from this file.
   - Update `CLAUDE.md` if a fact it records changed.
8. **When all steps are done:**
   - Run `dotnet list SoftThorn.MAL.ApiClient.slnx package --outdated` and `dotnet list build/Build.csproj package --outdated` once more.
   - Run `dotnet list SoftThorn.MAL.ApiClient.slnx package --vulnerable --include-transitive` and `dotnet list build/Build.csproj package --vulnerable --include-transitive`.
   - Compare `CodecovCliVersion` with the newest Codecov CLI version (see the standing note below).
   - Add a step for anything new these commands turn up.
   - If they turn up nothing, leave the file with just the workflow and a line saying "No pending updates as of YYYY-MM-DD".

If a step fails and the fix isn't obvious, revert that step's change, write the failure under the step, and move on to the next independent step.

---

No pending updates as of 2026-10-17. Both solutions have no outdated or vulnerable packages, and the Cake and local tools are on their latest versions.

**Standing notes for future rounds:**

- **Verify's maintenance fee:** the integration test csproj claims `Verify_SponsorshipExemption=OpenSource` until `Verify_SponsorshipExemptionUntil=2027-10`, approved by the owner on 2026-10-17. After that month the build fails with SponsorCheck SC021.
  - Renewing means bumping it by at most 12 months, and only while the project is still open source with no revenue.
  - Ask the owner before changing what is claimed.
- **`System.Security.Cryptography.Pkcs` pin:** it's pinned to 10.0.13 in `build/Directory.Packages.props` (`vulnerable transitive dependencies`), because Cake.Frosting 6.3.0 resolves a vulnerable 10.0.12 (since the build project moved to net10.0 on 2026-10-17; before that it was 9.0.20 → 9.0.21).
  - Each round, remove the pin, run `dotnet list build/Build.csproj package --vulnerable --include-transitive`, and drop the pin for good once that comes back clean.
- **Codecov CLI:** it has no NuGet package, so `UploadCodecovReportTask` downloads a pinned binary from `https://cli.codecov.io/<version>/<platform>/` (`CodecovCliVersion` and the per-platform SHA-256 constants in `build/Util/Constants.cs`).
  - Find the newest version by comparing `https://cli.codecov.io/latest/linux/codecov.SHA256SUM` with the versioned folders. The GitHub releases of `getsentry/prevent-cli` lag behind (v11.2.8 there while cli.codecov.io had v11.3.1 on 2026-10-17).
  - To update, bump the version and copy the hashes from `codecov.SHA256SUM` (linux, macos) and `codecov.exe.SHA256SUM` (windows) in that version's folders.
    - Some platforms are missing for some versions (v11.3.1 has no `linux-arm64` or `alpine`), so check that every folder the task uses exists.
  - The step's own check:
    - Download the Windows binary and compare its hash with the constant.
    - Run it on a Cobertura file with `upload-coverage --dry-run --disable-search --file <file> -C <sha> -B main -r Insire/SoftThorn.MAL.ApiClient --git-service github`. With `--dry-run` it sends nothing.
    - Get a Cobertura file from the unit tests with `--coverage --coverage-output-format cobertura`.
  - The real upload only runs on Azure, so watch the next pipeline build's `UploadCodecovReport` log.
- **Old tool versions in `tools/`:** after a Cake tool version bump, `tools/` keeps the old versions too, and `tools/codecov-cli/` keeps one folder per CLI version. They're gitignored and can be deleted.
  - Check the Setup log line `dotnet-coverage (...)` to confirm the new path is the one resolved.
- **Playwright and the CI image:** `azure-pipelines.yml` runs in `mcr.microsoft.com/playwright/dotnet:v<version>-noble`, which has the browsers for exactly that Playwright version.
  - When `Microsoft.Playwright.Xunit.v3` changes, change the image tag to the same version in the same step. Check that the tag exists first: `https://mcr.microsoft.com/v2/playwright/dotnet/tags/list`.
  - The image also decides the .NET SDK version on CI, so an SDK bump means waiting for (or switching to) a matching image.
  - `UpdateOpenApiSpecTask` picks NSwag's `net10.0` build explicitly.
