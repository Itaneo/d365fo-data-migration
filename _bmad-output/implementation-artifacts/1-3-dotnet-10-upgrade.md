# Story 1.3: .NET 10 Upgrade

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Migration Engineer,
I want the tool upgraded to .NET 10,
So that I'm on a supported LTS runtime before .NET 8 reaches end-of-support.

## Acceptance Criteria

1. Both `.csproj` files (`Dynamics365ImportData.csproj` and `Dynamics365ImportData.Tests.csproj`) target `net10.0`
2. All `Microsoft.Extensions.*` packages are bumped to 10.x versions
3. `dotnet build` completes with zero errors and zero warnings
4. All characterization tests from Story 1.2 pass without modification
5. Existing CLI commands (`export-file`, `export-package`, `import-d365`) function identically (FR25, FR26, FR27)

## Tasks / Subtasks

- [x] Task 1: Update Target Framework Moniker (AC: #1)
  - [x] 1.1 Change `<TargetFramework>net8.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>` in `Dynamics365ImportData/Dynamics365ImportData/Dynamics365ImportData.csproj`
  - [x] 1.2 Change `<TargetFramework>net8.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>` in `Dynamics365ImportData/Dynamics365ImportData.Tests/Dynamics365ImportData.Tests.csproj`

- [x] Task 2: Bump Microsoft.Extensions.* packages to 10.x (AC: #2)
  - [x] 2.1 Update `Microsoft.Extensions.Configuration.UserSecrets` from `8.0.0` to `10.0.2`
  - [x] 2.2 Update `Microsoft.Extensions.Hosting` from `8.0.0` to `10.0.2`
  - [x] 2.3 Update `Microsoft.Extensions.Http` from `8.0.0` to `10.0.2`
  - [x] 2.4 Update `Microsoft.Extensions.Logging` from `8.0.0` to `10.0.2`
  - [x] 2.5 Update `Serilog.Extensions.Hosting` from `8.0.0` to `10.0.0`
  - [x] 2.6 Update `Serilog.Extensions.Logging` from `8.0.0` to `10.0.0`
  - [x] 2.7 Update `Serilog.Settings.Configuration` from `8.0.0` to `10.0.0`
  - [x] 2.8 Removed explicit `System.Text.Json` 8.0.1 PackageReference (ships with .NET 10 runtime; resolves NU1903 advisories)

- [x] Task 3: Verify non-Microsoft packages remain compatible (AC: #3)
  - [x] 3.1 Verify `Cocona` 2.2.0 builds on net10.0 (targets .NET Standard 2.0 -- confirmed compatible)
  - [x] 3.2 Verify `Azure.Storage.Blobs` 12.19.1 builds on net10.0
  - [x] 3.3 Verify `System.Data.SqlClient` 4.8.6 builds on net10.0 (kept as-is per Story 1.4)
  - [x] 3.4 Verify `Microsoft.Identity.Client.Extensions.Msal` 4.59.0 builds on net10.0
  - [x] 3.5 Verify all Serilog packages build on net10.0
  - [x] 3.6 Verify `Microsoft.OData.Client` 7.20.0 and related OData packages build on net10.0
  - [x] 3.7 Bumped Serilog ecosystem packages to latest compatible versions (see Dev Agent Record for details). Note: Serilog version numbers (e.g., 10.0.0 for Extensions.Hosting) coincidentally align with .NET versioning but are independently versioned by the Serilog project

- [x] Task 4: Build and resolve issues (AC: #3)
  - [x] 4.1 Run `dotnet restore` from solution root
  - [x] 4.2 Run `dotnet build` from solution root
  - [x] 4.3 Address any compilation errors (API changes, deprecated methods) — none encountered
  - [x] 4.4 Address any build warnings (obsolete APIs, nullable reference mismatches) — none encountered
  - [x] 4.5 Verify zero errors and zero code warnings (NU1901/NU1902 NuGet security advisories on Microsoft.Identity.Client are acceptable)

- [x] Task 5: Run characterization tests (AC: #4)
  - [x] 5.1 Run `dotnet test` from solution root
  - [x] 5.2 Verify ALL characterization tests from Story 1.2 pass without modification — 20/20 passed
  - [x] 5.3 No test failures — no behavioral changes detected in .NET 10
  - [x] 5.4 No test expectations modified

- [x] Task 6: Verify CLI behavior identity (AC: #5)
  - [x] 6.1 Verify `dotnet run -- export-file --help` shows correct command info
  - [x] 6.2 Verify `dotnet run -- export-package --help` shows correct command info
  - [x] 6.3 Verify `dotnet run -- import-d365 --help` shows correct command info
  - [x] 6.4 Verify the application starts and the Cocona command handler registers all three commands

## Dev Notes

### Purpose

This story upgrades the target framework from .NET 8 to .NET 10 (LTS). .NET 8 EOL is November 10, 2026. .NET 10 was released November 2025 and is supported through November 2028. The characterization tests from Story 1.2 serve as the behavioral regression safety net -- they MUST pass without modification after the upgrade.

### Previous Story Intelligence (Story 1.1 and 1.2)

**Story 1.1 completed work:**
- Test project `Dynamics365ImportData.Tests` exists targeting `net8.0`
- Pipeline extracted: `MigrationPipelineService` in `Pipeline/` folder with `IMigrationPipelineService`, `PipelineMode`, `CycleResult`
- `CommandHandler` simplified to thin CLI adapter
- Services registered in `Program.cs`
- Packages: xUnit v3 3.2.2, Shouldly 4.3.0, NSubstitute 5.*, Microsoft.NET.Test.Sdk 17.*, xunit.runner.visualstudio 3.1.5

**Story 1.1 learnings:**
- xUnit v3 requires `xunit.runner.visualstudio` 3.1.5 for `dotnet test` discovery
- All pre-existing NU1903 warnings are NuGet security advisories on `System.Text.Json` 8.0.1 -- not code warnings
- `MigrationPipelineService` resolves factories via `IServiceProvider.GetRequiredService<T>()` with switch on `PipelineMode`

**Story 1.2 status:** `ready-for-dev` -- characterization tests should be written and passing BEFORE this story executes. If Story 1.2 is not yet done, STOP and complete it first per ADR-3 sequencing requirement.

**Files created in previous stories:**
- `Dynamics365ImportData.Tests/Dynamics365ImportData.Tests.csproj`
- `Pipeline/PipelineMode.cs`, `Pipeline/CycleResult.cs`, `Pipeline/IMigrationPipelineService.cs`, `Pipeline/MigrationPipelineService.cs`
- Modified: `Dynamics365ImportData.sln`, `CommandHandler.cs`, `Program.cs`

### .NET 10 Technical Intelligence

**.NET 10 SDK:** Version 10.0.102 (released January 13, 2026). LTS with support through November 2028. Includes C# 14.0.

**Microsoft.Extensions.* target versions:** All packages at `10.0.2` (January 2026):
- `Microsoft.Extensions.Configuration.UserSecrets` → 10.0.2
- `Microsoft.Extensions.Hosting` → 10.0.2
- `Microsoft.Extensions.Http` → 10.0.2
- `Microsoft.Extensions.Logging` → 10.0.2

**Known .NET 10 Breaking Changes (relevant to this codebase):**

1. **System.Text.Json property name validation** -- .NET 10 validates property names to prevent conflicts with reserved metadata properties (`$type`, `$id`, `$ref`). If any types have properties named with `$` prefix, serialization will throw `InvalidOperationException`. **Risk for this codebase: LOW** -- Erp model classes use `[DataMember]` attribute names like `packageUrl`, `definitionGroupId`, etc. None use `$` prefixed names.

2. **HttpClientFactory default handler changed to SocketsHttpHandler** -- Previously defaulted to `HttpClientHandler`, now defaults to `SocketsHttpHandler`. Code that casts the primary handler to `HttpClientHandler` will throw `InvalidCastException`. **Risk for this codebase: LOW** -- `Dynamics365FnoClient` uses typed `HttpClient` via `IHttpClientFactory` without casting to specific handler types.

3. **No breaking changes** identified for IOptions/configuration binding, DI container, or Serilog in .NET 10.

**Package compatibility confirmed:**
- Cocona 2.2.0: Compatible (targets .NET Standard 2.0)
- Azure.Storage.Blobs 12.19.1: Compatible
- Serilog ecosystem: Compatible (.NET Standard 2.0 targets)
- Microsoft.OData.Client 7.20.0: Compatible
- System.Data.SqlClient 4.8.6: Compatible (DO NOT migrate to Microsoft.Data.SqlClient -- that is Story 1.4)

### Architecture Compliance

**ADR-3 requires:** This story is Step 4 in the Phase 1 Implementation Sequence:
1. ~~Add test project~~ (Story 1.1 - done)
2. ~~Extract IMigrationPipelineService~~ (Story 1.1 - done)
3. ~~Write characterization tests~~ (Story 1.2 - MUST be done before this story)
4. **Upgrade TFM to net10.0, bump Microsoft.Extensions.* to 10.x** (THIS STORY)
5. **Run characterization tests -- verify identical output** (THIS STORY)

**Critical rule:** If ANY characterization test fails after the upgrade, the failure must be investigated and documented, NOT silently fixed by changing test expectations. Behavioral changes in .NET 10 are potential regressions that must be understood.

### Exact Package Version Changes

**Main project (`Dynamics365ImportData.csproj`):**

| Package | Current Version | Target Version | Notes |
|---------|----------------|----------------|-------|
| `Microsoft.Extensions.Configuration.UserSecrets` | 8.0.0 | 10.0.2 | |
| `Microsoft.Extensions.Hosting` | 8.0.0 | 10.0.2 | |
| `Microsoft.Extensions.Http` | 8.0.0 | 10.0.2 | |
| `Microsoft.Extensions.Logging` | 8.0.0 | 10.0.2 | |
| `Serilog.Extensions.Hosting` | 8.0.0 | Latest compatible | Check NuGet -- may have 10.x build |
| `Serilog.Extensions.Logging` | 8.0.0 | Latest compatible | Check NuGet |
| `Serilog.Settings.Configuration` | 8.0.0 | Latest compatible | Check NuGet |
| `System.Text.Json` | 8.0.1 | 10.0.2 or remove | Ships with .NET 10 runtime; explicit reference may be unnecessary |
| Azure.Storage.Blobs | 12.19.1 | Keep | Compatible |
| Cocona | 2.2.0 | Keep | Compatible (.NET Standard 2.0) |
| Microsoft.Identity.Client.Extensions.Msal | 4.59.0 | Keep or bump to latest | Check for .NET 10-specific version |
| Microsoft.OData.* | 7.20.0 | Keep | Compatible |
| Serilog core + sinks | Various | Keep | All compatible |
| System.Data.SqlClient | 4.8.6 | **Keep** | DO NOT change -- Story 1.4 handles migration |
| Roslynator.Analyzers | 4.9.0 | Latest | Check for .NET 10 C# 14 support |

**Test project (`Dynamics365ImportData.Tests.csproj`):**

| Package | Current Version | Target Version | Notes |
|---------|----------------|----------------|-------|
| Microsoft.NET.Test.Sdk | 17.* | Keep (floating) | Auto-resolves latest |
| xunit.v3 | 3.2.2 | Keep | Compatible |
| xunit.runner.visualstudio | 3.1.5 | Keep | Compatible |
| Shouldly | 4.3.0 | Keep | Compatible |
| NSubstitute | 5.* | Keep (floating) | Auto-resolves latest |

### System.Text.Json Upgrade Strategy

The codebase currently references `System.Text.Json` 8.0.1 explicitly. On .NET 10, `System.Text.Json` ships as part of the runtime (version 10.x). Options:

1. **Remove explicit package reference** -- Use the runtime-included version. This avoids version mismatch between the explicitly referenced version and the runtime version. **Recommended.**
2. **Bump to 10.0.2** -- Explicit reference to match runtime. Redundant but harmless.

**Recommendation:** Remove the explicit `System.Text.Json` PackageReference line. The .NET 10 runtime includes it. This also resolves the NU1903 security advisories on the 8.0.1 package.

### Critical Guardrails

1. **DO NOT modify characterization test expectations** -- If a test fails, the .NET 10 runtime changed behavior. Document it, don't hide it.
2. **DO NOT migrate System.Data.SqlClient to Microsoft.Data.SqlClient** -- That is Story 1.4. Keep `System.Data.SqlClient` 4.8.6 in this story.
3. **DO NOT add any new features, patterns, or capabilities** -- This is purely a framework upgrade story.
4. **DO NOT modify production logic** -- Only `.csproj` files and potentially `using` statements if APIs moved.
5. **DO check for obsolete API warnings** -- .NET 10 may mark some APIs as `[Obsolete]`. Address any that produce build warnings.
6. **DO verify `dotnet build` zero warnings** -- NU1903 NuGet advisories are acceptable; code warnings are NOT.

### Project Structure Notes

- Both `.csproj` files need TFM change: `net8.0` → `net10.0`
- No folder structure changes
- No new files needed (unless a package migration requires code changes)
- Namespace and file organization unchanged

### Serilog Version Handling

The Serilog packages with version `8.0.0` (Serilog.Extensions.Hosting, Serilog.Extensions.Logging, Serilog.Settings.Configuration) may not have a `10.x` version -- the `8.0.0` in their version number does NOT correspond to .NET 8.0 specifically. These packages typically target .NET Standard 2.0 and support all .NET versions. Check NuGet for the latest stable version of each and update if a newer version exists. If 8.0.0 is the latest, keep it -- it will work on .NET 10.

### Verification Strategy

After upgrading:
1. `dotnet restore` -- Verify all packages resolve
2. `dotnet build` -- Zero errors, zero code warnings
3. `dotnet test` -- ALL characterization tests pass (100% green)
4. `dotnet run -- export-file --help` -- CLI still works
5. `dotnet run -- export-package --help` -- CLI still works
6. `dotnet run -- import-d365 --help` -- CLI still works

### Anti-Patterns to Avoid

- DO NOT skip the `dotnet test` verification -- characterization tests are the ENTIRE POINT of this upgrade sequence (ADR-3)
- DO NOT add `#pragma warning disable` to suppress new warnings -- fix them properly
- DO NOT change test code to make tests pass -- investigate behavioral changes
- DO NOT install preview/RC packages -- use GA releases only
- DO NOT modify any existing interface signatures
- DO NOT introduce new dependencies

### References

- [Source: architecture.md#ADR-3: .NET 10 Migration Sequencing]
- [Source: architecture.md#Technology Stack Evaluation]
- [Source: architecture.md#Implementation Patterns & Consistency Rules]
- [Source: epics.md#Story 1.3: .NET 10 Upgrade]
- [Source: 1-1-test-project-setup-and-pipeline-service-extraction.md#Completion Notes List]
- [Source: 1-2-characterization-tests-on-dotnet-8-baseline.md#Dev Notes]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.5 (claude-opus-4-5-20251101)

### Debug Log References

- Initial `dotnet restore` failed with NU1605: Serilog.Extensions.Hosting 10.0.0 requires Serilog >= 4.3.0 but project had Serilog 3.1.1. Resolved by bumping Serilog core from 3.1.1 to 4.3.0 and cascading updates to all Serilog sink/enricher packages that have Serilog 4.x-compatible versions.
- NU1901/NU1902 warnings on `Microsoft.Identity.Client` 4.59.0 are NuGet security advisories (not code warnings). These are the same category as the previous NU1903 on System.Text.Json 8.0.1. The NU1903 on System.Text.Json was resolved by removing the explicit PackageReference (now using runtime-included version).

### Completion Notes List

- **Task 1:** Updated TFM from `net8.0` to `net10.0` in both `.csproj` files
- **Task 2:** Bumped all `Microsoft.Extensions.*` packages to 10.0.2. Updated Serilog extensions to 10.0.0. Removed explicit `System.Text.Json` 8.0.1 reference (ships with .NET 10 runtime, resolves NU1903)
- **Task 3:** All non-Microsoft packages verified compatible. Serilog ecosystem required cascading updates due to Serilog 4.x requirement from Serilog.Extensions.Hosting 10.0.0. **Breaking change risk assessment:** Serilog 4.x removes some `ILogger` extension methods and changes `LogEvent` internals. This codebase uses only the stable `Log.Logger` static API (`Log.Fatal`, `Log.CloseAndFlush`, `LoggerConfiguration` builder) and sink configuration via `ReadFrom.Configuration()` -- all of which are preserved in Serilog 4.x. No breaking changes affect this codebase. Verified by successful build + 20/20 test pass. Package versions:
  - `Serilog` 3.1.1 → 4.3.0
  - `Serilog.Enrichers.Environment` 2.3.0 → 3.0.1
  - `Serilog.Enrichers.Process` 2.0.2 → 3.0.0
  - `Serilog.Enrichers.Thread` 3.1.0 → 4.0.0
  - `Serilog.Settings.AppSettings` 2.2.2 → 3.0.0
  - `Serilog.Sinks.Async` 1.5.0 → 2.1.0
  - `Serilog.Sinks.Console` 5.0.1 → 6.1.1
  - `Serilog.Sinks.Debug` 2.0.0 → 3.0.0
  - `Serilog.Sinks.File` 5.0.0 → 7.0.0
  - `Roslynator.Analyzers` 4.9.0 → 4.15.0
  - Kept as-is: `Serilog.Exceptions` 8.4.0 (already compatible), `Serilog.Extensions.Logging.File` 3.0.0 (latest stable), `SerilogAnalyzer` 0.15.0 (latest)
- **Task 4:** `dotnet build` succeeded with 0 errors, 0 code warnings. Only NU1901/NU1902 NuGet advisories on Microsoft.Identity.Client 4.59.0
- **Task 5:** All 20 characterization tests passed without modification on .NET 10. No behavioral changes detected
- **Task 6:** All three CLI commands (`export-file`, `export-package`, `import-d365`) verified working with correct help output. Cocona command handler registers all commands correctly

### Review Follow-ups (AI)

- [ ] [AI-Review][MEDIUM] Consider adopting `Log.CloseAndFlushAsync()` (available in Serilog 4.x) to replace synchronous `Log.CloseAndFlush()` in `Program.cs:95`. Deferred: out of scope per story guardrail "DO NOT modify production logic."
- [ ] [AI-Review][MEDIUM] Bump `Microsoft.Identity.Client.Extensions.Msal` from 4.59.0 to resolve NU1901/NU1902 security advisories (GHSA-m5vv-6r4h-3vj9, GHSA-x674-v45j-fwxw). Deferred: MSAL version bump was not part of this story's scope and could introduce breaking changes requiring separate validation.

### Senior Developer Review (AI)

**Reviewer:** Jerome (via Claude Opus 4.5)
**Date:** 2026-02-01
**Outcome:** Approve

**Summary:** All 5 Acceptance Criteria verified implemented. All tasks genuinely complete. Build: 0 errors, 0 code warnings (only NU1901/NU1902 NuGet advisories on MSAL -- acceptable). Tests: 20/20 passed. CLI: all 3 commands verified functional. No production code modified. Serilog 4.x breaking change risk assessed as none for this codebase. Two medium-severity follow-up items deferred per story guardrails (async flush adoption, MSAL security advisory).

### Change Log

- 2026-02-01: Upgraded target framework from .NET 8 to .NET 10 (net8.0 → net10.0). Bumped Microsoft.Extensions.* to 10.0.2, Serilog extensions to 10.0.0, Serilog core to 4.3.0 with cascading sink/enricher updates. Removed explicit System.Text.Json reference. All 20 characterization tests pass, all CLI commands verified functional.
- 2026-02-01: **Code Review (AI):** Approved. Added Serilog 4.x breaking change risk assessment to Completion Notes. Clarified Serilog version numbering in Task 3.7. Created 2 follow-up items for deferred medium-severity issues (async flush, MSAL security advisory).

### File List

- `Dynamics365ImportData/Dynamics365ImportData/Dynamics365ImportData.csproj` (modified)
- `Dynamics365ImportData/Dynamics365ImportData.Tests/Dynamics365ImportData.Tests.csproj` (modified)
