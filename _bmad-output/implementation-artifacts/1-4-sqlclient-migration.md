# Story 1.4: SqlClient Migration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Migration Engineer,
I want `System.Data.SqlClient` replaced with `Microsoft.Data.SqlClient`,
So that the tool uses the actively maintained SQL client library with current security patches.

## Acceptance Criteria

1. The `System.Data.SqlClient` NuGet package is removed from `Dynamics365ImportData.csproj`
2. `Microsoft.Data.SqlClient` NuGet package (version 6.1.4) is added to `Dynamics365ImportData.csproj`
3. All `using System.Data.SqlClient` statements are updated to `using Microsoft.Data.SqlClient`
4. `Encrypt=true` default behavior is verified against target SQL Server configuration and documented
5. Connection string format is validated (backward compatible or documented changes)
6. All characterization tests from Story 1.2 pass without modification
7. `dotnet build` completes with zero code warnings (NU1903 NuGet advisories acceptable)

## Tasks / Subtasks

- [x] Task 1: Swap NuGet package reference (AC: #1, #2)
  - [x] 1.1 Remove `<PackageReference Include="System.Data.SqlClient" Version="4.8.6" />` from `Dynamics365ImportData/Dynamics365ImportData/Dynamics365ImportData.csproj` (line 53)
  - [x] 1.2 Add `<PackageReference Include="Microsoft.Data.SqlClient" Version="6.1.4" />` in its place
  - [x] 1.3 Run `dotnet restore` from solution root to verify package resolves

- [x] Task 2: Update using directives (AC: #3)
  - [x] 2.1 In `Dynamics365ImportData/Dynamics365ImportData/Services/SqlToXmlService.cs` (line 8): change `using System.Data.SqlClient;` to `using Microsoft.Data.SqlClient;`
  - [x] 2.2 Search entire codebase for any other `using System.Data.SqlClient` references (grep for `System.Data.SqlClient`) -- there should be none beyond SqlToXmlService.cs
  - [x] 2.3 Search for any `System.Data.SqlTypes` references that may need changing to `Microsoft.Data.SqlTypes` (expected: none in this codebase)

- [x] Task 3: Build and resolve issues (AC: #7)
  - [x] 3.1 Run `dotnet build` from solution root
  - [x] 3.2 Address any compilation errors -- the API surface for `SqlConnection`, `SqlCommand`, `SqlDataReader` is identical between packages
  - [x] 3.3 Address any build warnings from the new package
  - [x] 3.4 Verify zero code warnings (NU1903 NuGet advisories and SNI-related info messages are acceptable)

- [x] Task 4: Run characterization tests (AC: #6)
  - [x] 4.1 Run `dotnet test` from solution root
  - [x] 4.2 Verify ALL characterization tests from Story 1.2 pass without modification
  - [x] 4.3 Pay special attention to SQL connection behavior tests -- these specifically baseline `System.Data.SqlClient` behavior
  - [x] 4.4 If any SQL connection test fails, investigate behavioral differences between packages and document in Dev Agent Record
  - [x] 4.5 Do NOT modify test expectations to make them pass -- behavioral changes must be understood and documented

- [x] Task 5: Resolve transitive dependency conflict (AC: #7)
  - [x] 5.1 `Microsoft.Data.SqlClient` 6.1.4 pulls `Azure.Identity` 1.17.1 which requires `Microsoft.Identity.Client.Extensions.Msal` >= 4.78.0 — bumped from 4.59.0 to 4.78.0 in csproj
  - [x] 5.2 Verified build succeeds and all 20 tests pass after version bump — no behavioral regressions observed

- [x] Task 6: Verify Encrypt=true behavior and document (AC: #4, #5)
  - [x] 6.1 Document in Dev Notes that `Microsoft.Data.SqlClient` 4.0+ defaults `Encrypt=true` (vs `Encrypt=false` in `System.Data.SqlClient`)
  - [x] 6.2 Reviewed `appsettings.json` — connection string `Server=localhost;Database=AxDb;Trusted_Connection=True;` does NOT explicitly set `Encrypt`, so connections to SQL Server without trusted certificates will fail
  - [x] 6.3 Added Completion Note documenting that users may need to add `Encrypt=False` or `TrustServerCertificate=True` for development SQL Server instances without valid certificates
  - [x] 6.4 Did NOT modify `appsettings.json` or connection string handling code -- this is a documentation/awareness task only, as connection strings are user-configured

- [x] Task 7: Verify CLI behavior (AC: #6)
  - [x] 7.1 Verify `dotnet run -- export-file --help` shows correct command info
  - [x] 7.2 Verify `dotnet run -- export-package --help` shows correct command info
  - [x] 7.3 Verify `dotnet run -- import-d365 --help` shows correct command info
  - [x] 7.4 Verify the application starts and Cocona command handler registers all three commands

## Dev Notes

### Purpose

This story migrates from `System.Data.SqlClient` (legacy, receiving security fixes only, deprecated for .NET 9+) to `Microsoft.Data.SqlClient` (actively maintained replacement). This is Step 6 in the Phase 1 Implementation Sequence per ADR-3. The characterization tests from Story 1.2 serve as the behavioral regression safety net.

### Scope of Change

This is a minimal, surgical change. The codebase has **exactly one file** that uses SqlClient types:

**`Services/SqlToXmlService.cs`** -- Uses:
- `SqlConnection` (line 30) -- constructed with `source.SourceConnectionString`
- `SqlCommand` (line 36) -- created via `con.CreateCommand()`
- `SqlDataReader` (line 39) -- created via `cmd.ExecuteReaderAsync()`

All three types (`SqlConnection`, `SqlCommand`, `SqlDataReader`) have identical API surfaces in `Microsoft.Data.SqlClient`. The migration is a package swap + using directive change.

**`.csproj` reference:** Single line change from `System.Data.SqlClient` 4.8.6 to `Microsoft.Data.SqlClient` 6.1.4.

### Previous Story Intelligence

**Story 1.1 (done):**
- Test project created, pipeline extracted, `InternalsVisibleTo` added for test project
- xUnit v3 3.2.2, Shouldly 4.3.0, NSubstitute 5.*, xunit.runner.visualstudio 3.1.5
- All NU1903 warnings are NuGet security advisories -- not code warnings

**Story 1.2 (ready-for-dev, MUST be done before this story):**
- Characterization tests include SQL connection behavior tests (`SqlConnectionBehaviorTests.cs`) that baseline `System.Data.SqlClient` behavior
- These tests verify connection string parsing and exception types -- the exact behavior that may differ post-migration

**Story 1.3 (ready-for-dev, MUST be done before this story):**
- .NET 10 TFM upgrade -- both projects target `net10.0`
- All Microsoft.Extensions.* bumped to 10.x
- `System.Data.SqlClient` 4.8.6 was explicitly KEPT in Story 1.3 for this story to handle

**CRITICAL PREREQUISITE:** Stories 1.2 AND 1.3 MUST be completed before this story. If they are not done, STOP. Per ADR-3, the sequence is: characterization tests (1.2) -> .NET 10 upgrade (1.3) -> SqlClient migration (1.4).

### Microsoft.Data.SqlClient 6.1.4 Technical Intelligence

**Package version:** 6.1.4 (latest stable, released January 15, 2026)
- MIT licensed
- Supports .NET 8.0+ (including .NET 10 via .NET 8+ targeting)
- v7.0 preview exists with explicit .NET 10 TFM but is not GA -- use 6.1.4

**Critical behavioral difference -- Encrypt default:**
- `System.Data.SqlClient`: `Encrypt` defaults to `false` (connections are unencrypted by default)
- `Microsoft.Data.SqlClient` 4.0+: `Encrypt` defaults to `true` (Mandatory)
- Impact: Connections to SQL Server without trusted TLS certificates will FAIL with: `"The certificate chain was issued by an authority that is not trusted."`
- Mitigation for dev/test environments: Add `Encrypt=False` or `TrustServerCertificate=True` to connection string
- This is a CONNECTION STRING change by the USER, not a code change. Document it.

**Connection string keyword differences:**
- `SqlConnectionStringBuilder` in `Microsoft.Data.SqlClient` emits keywords with spaces (e.g., `Application Intent` instead of `ApplicationIntent`)
- This codebase does NOT use `SqlConnectionStringBuilder` in production code -- connection strings come directly from `appsettings.json` as raw strings passed to `SqlConnection` constructor
- Impact: **NONE** for this codebase. Connection strings are user-configured, not builder-constructed.

**SNI native DLL dependency (Windows):**
- `Microsoft.Data.SqlClient` depends on `Microsoft.Data.SqlClient.SNI` native DLL on Windows
- This is automatically included via NuGet package -- no manual action needed
- For managed-only mode: `AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true)` -- NOT recommended unless there are specific deployment issues

**API surface compatibility:**
- `SqlConnection`, `SqlCommand`, `SqlDataReader` -- identical API surface to `System.Data.SqlClient`
- `OpenAsync()`, `CreateCommand()`, `ExecuteReaderAsync()`, `ReadAsync()`, `IsDBNull()`, `GetName()`, `GetValue()` -- all present and identical
- `CommandTimeout` property -- identical
- `FieldCount` property -- identical

**Namespace changes beyond SqlClient:**
- `Microsoft.SqlServer.Server` types moved to `Microsoft.Data.SqlClient.Server` -- NOT used in this codebase
- `System.Data.SqlTypes` types moved to `Microsoft.Data.SqlTypes` -- NOT used in this codebase (no grep matches)

### Architecture Compliance

**ADR-3 requires:** This story is Step 6 in the Phase 1 Implementation Sequence:
1. ~~Add test project~~ (Story 1.1 - done)
2. ~~Extract IMigrationPipelineService~~ (Story 1.1 - done)
3. ~~Write characterization tests~~ (Story 1.2 - MUST be done before this story)
4. ~~Upgrade TFM to net10.0~~ (Story 1.3 - MUST be done before this story)
5. ~~Run characterization tests -- verify identical output~~ (Story 1.3)
6. **Migrate System.Data.SqlClient -> Microsoft.Data.SqlClient as distinct step** (THIS STORY)

**Critical rule:** If ANY characterization test fails after the migration, the failure must be investigated and documented. The SQL connection behavior tests from Story 1.2 specifically baseline `System.Data.SqlClient` behavior for this comparison.

### Exact File Changes

**File 1: `Dynamics365ImportData/Dynamics365ImportData/Dynamics365ImportData.csproj`**
- Remove: `<PackageReference Include="System.Data.SqlClient" Version="4.8.6" />` (line 53)
- Add: `<PackageReference Include="Microsoft.Data.SqlClient" Version="6.1.4" />`

**File 2: `Dynamics365ImportData/Dynamics365ImportData/Services/SqlToXmlService.cs`**
- Change line 8: `using System.Data.SqlClient;` -> `using Microsoft.Data.SqlClient;`

**No other files should be modified.** If the build or tests reveal other files needing changes, document them in the Dev Agent Record.

### Expected Test Behavior Changes

The characterization tests from Story 1.2 include SQL connection behavior tests. Potential differences:

1. **`SqlConnection_InvalidConnectionString_ThrowsExpectedException`** -- Exception type should remain `ArgumentException` or `SqlException`. If the exception type or message differs between packages, document the difference.

2. **`SqlConnection_ConnectionStringFormat_ParsesCorrectly`** -- If this test uses `SqlConnectionStringBuilder`, the output format may differ (spaced keywords). Document any differences.

If tests fail due to behavioral differences, the correct action is:
- Document the exact behavioral difference in the Dev Agent Record
- Evaluate whether the difference is acceptable (e.g., different error message text) vs. breaking (e.g., different exception type)
- If acceptable, update the test expectations with a comment explaining the change
- If breaking, escalate for architectural decision

### Project Structure Notes

- No folder structure changes
- No new files created
- Only two existing files modified
- Namespace and file organization unchanged

### Anti-Patterns to Avoid

- DO NOT add `Encrypt=False` or `TrustServerCertificate=True` to the codebase or default configuration -- these are user-configured values
- DO NOT modify characterization test expectations without documenting WHY the behavior differs
- DO NOT add any new features, patterns, or capabilities -- this is purely a library swap
- DO NOT modify any interface signatures
- DO NOT install preview/RC packages -- use GA release 6.1.4
- DO NOT install `Microsoft.SqlServer.Server` NuGet package -- it conflicts with `Microsoft.Data.SqlClient`
- DO NOT modify production logic beyond the `using` directive change
- DO NOT add `AppContext.SetSwitch` for managed SNI unless there is a specific deployment failure

### Verification Strategy

After the migration:
1. `dotnet restore` -- Verify Microsoft.Data.SqlClient 6.1.4 resolves
2. `dotnet build` -- Zero errors, zero code warnings
3. `dotnet test` -- ALL characterization tests pass (100% green)
4. `dotnet run -- export-file --help` -- CLI still works
5. `dotnet run -- export-package --help` -- CLI still works
6. `dotnet run -- import-d365 --help` -- CLI still works

### References

- [Source: architecture.md#ADR-3: .NET 10 Migration Sequencing]
- [Source: architecture.md#Technology Stack Evaluation]
- [Source: architecture.md#Implementation Patterns & Consistency Rules]
- [Source: epics.md#Story 1.4: SqlClient Migration]
- [Source: 1-1-test-project-setup-and-pipeline-service-extraction.md#Completion Notes List]
- [Source: 1-2-characterization-tests-on-dotnet-8-baseline.md#Dev Notes]
- [Source: 1-3-dotnet-10-upgrade.md#Dev Notes]
- [NuGet: Microsoft.Data.SqlClient 6.1.4](https://www.nuget.org/packages/Microsoft.Data.SqlClient/6.1.4)
- [Microsoft: Porting from System.Data.SqlClient](https://github.com/dotnet/SqlClient/blob/main/porting-cheat-sheet.md)
- [Microsoft: System.Data.SqlClient deprecation](https://techcommunity.microsoft.com/blog/sqlserver/announcement-system-data-sqlclient-package-is-now-deprecated/4227205)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.5 (claude-opus-4-5-20251101)

### Debug Log References

- `dotnet restore` succeeded after resolving transitive dependency conflict (Microsoft.Identity.Client.Extensions.Msal 4.59.0 → 4.78.0 required by Azure.Identity 1.17.1, a transitive dependency of Microsoft.Data.SqlClient 6.1.4)
- `dotnet build` completed with 0 warnings, 0 errors
- `dotnet test` passed all 20 tests (100% green), including both SqlConnectionBehaviorTests
- All 3 CLI commands (`export-file`, `export-package`, `import-d365`) verified working via `--help`

### Completion Notes List

- Replaced `System.Data.SqlClient` 4.8.6 with `Microsoft.Data.SqlClient` 6.1.4 in csproj
- Updated `Microsoft.Identity.Client.Extensions.Msal` from 4.59.0 to 4.78.0 to resolve transitive dependency conflict introduced by `Microsoft.Data.SqlClient` 6.1.4 → `Azure.Identity` 1.17.1 dependency chain
- Updated `using System.Data.SqlClient` to `using Microsoft.Data.SqlClient` in `SqlToXmlService.cs` (production code)
- Updated `using System.Data.SqlClient` to `using Microsoft.Data.SqlClient` in `SqlConnectionBehaviorTests.cs` (test code) — this file also directly used SqlClient types for characterization testing
- No `System.Data.SqlTypes` references found in the codebase — no additional changes needed
- All 20 characterization tests passed without any test expectation modifications — `Microsoft.Data.SqlClient` behaves identically to `System.Data.SqlClient` for all tested scenarios (`ArgumentException` for malformed connection strings, `SqlConnectionStringBuilder` property parsing)
- **IMPORTANT — Encrypt=true default change:** `Microsoft.Data.SqlClient` 4.0+ defaults `Encrypt=true` (vs `false` in `System.Data.SqlClient`). The existing `appsettings.json` connection string (`Server=localhost;Database=AxDb;Trusted_Connection=True;`) does NOT explicitly set `Encrypt`. Users connecting to SQL Server instances without trusted TLS certificates will need to add `Encrypt=False` or `TrustServerCertificate=True` to their connection strings. No code changes made per story instructions — this is a user-configured value.
- Build produces zero code warnings, zero errors

### Change Log

- 2026-02-01: Story 1.4 SqlClient Migration — Migrated from System.Data.SqlClient 4.8.6 to Microsoft.Data.SqlClient 6.1.4. Updated transitive dependency Microsoft.Identity.Client.Extensions.Msal to 4.78.0. All 20 tests pass. Zero build warnings.
- 2026-02-01: Code review fixes — Updated stale comments in SqlConnectionBehaviorTests.cs to reference Microsoft.Data.SqlClient instead of System.Data.SqlClient. Added Task 5 documenting transitive dependency resolution. Updated File List to include appsettings.json as reviewed-not-modified. Renumbered Tasks 5-6 to 6-7.

### File List

- `Dynamics365ImportData/Dynamics365ImportData/Dynamics365ImportData.csproj` (modified — package references updated: SqlClient swap + MSAL transitive bump)
- `Dynamics365ImportData/Dynamics365ImportData/Services/SqlToXmlService.cs` (modified — using directive changed)
- `Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Services/SqlConnectionBehaviorTests.cs` (modified — using directive changed, test comments updated during code review)
- `Dynamics365ImportData/Dynamics365ImportData/appsettings.json` (reviewed, not modified — Encrypt=true impact assessed per AC #4/#5)
