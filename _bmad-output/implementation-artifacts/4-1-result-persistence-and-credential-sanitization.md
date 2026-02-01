# Story 4.1: Result Persistence & Credential Sanitization

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Migration Engineer,
I want structured migration results captured automatically after each run,
So that I have a reliable record of every cycle's outcomes for analysis and comparison.

## Acceptance Criteria

1. **Given** a migration run completes (any command), **When** the pipeline service finishes processing, **Then** a JSON result file is written to `{OutputDirectory}/results/` with filename `cycle-{yyyy-MM-ddTHHmmss}.json` (FR6, FR7)
2. **And** the result file contains: `cycleId`, `timestamp`, `command`, `entitiesRequested`, per-entity results (entityName, status, recordCount, durationMs, errors), and summary (totalEntities, succeeded, failed, warnings, skipped, totalDurationMs) per ADR-1 schema
3. **And** each cycle is uniquely identifiable by its `cycleId` (FR8)
4. **And** the result file uses `JsonDefaults.ResultJsonOptions` (camelCase, indented, enum as string) per architecture patterns
5. **And** writes are atomic via temp file + rename to prevent corruption on crash (NFR8)
6. **And** the console summary at completion shows total entities processed, succeeded, and failed (NFR7)
7. **And** result persistence adds negligible overhead to the migration pipeline (NFR11)
8. **Given** D365FO API error responses may contain credentials, **When** error messages are captured in results, **Then** `IResultSanitizer` strips: connection strings (`Server=...;Database=...`), Azure AD tenant/client IDs in auth contexts, Bearer tokens (`Bearer eyJ...`), SAS tokens (`sig=`, `sv=`, `se=`), and client secrets (ADR-7)
9. **And** stripped content is replaced with `[REDACTED]`
10. **And** sanitization is applied before any data reaches the result file (NFR1, NFR2)
11. **And** result files contain only entity names, statuses, sanitized error messages, and metadata -- no raw source data (NFR2)
12. **And** per-entity failures are captured with sufficient detail to diagnose root cause without re-running (NFR6)
13. **And** a failure in one entity does not prevent result capture for other entities (NFR5)
14. **Given** `IMigrationResultRepository` interface, **When** implemented as `JsonFileMigrationResultRepository`, **Then** the interface provides: `SaveCycleResultAsync`, `GetCycleResultAsync`, `GetLatestCycleResultsAsync`, `ListCycleIdsAsync`
15. **And** the repository is registered as Singleton in DI (stateless file I/O)
16. **And** `IResultSanitizer` is registered as Singleton and called within `SaveCycleResultAsync`

## Tasks / Subtasks

- [x] Task 1: Create result persistence data models (AC: #2, #3)
  - [x] 1.1 Extend `CycleResult` in `Pipeline/CycleResult.cs` to add: `CycleId` (string), `Timestamp` (DateTimeOffset), `EntitiesRequested` (string[]), `Results` (List<EntityResult>) -- **property MUST be named `Results` not `EntityResults` to serialize to `"results"` matching the ADR-1 JSON schema**, `Summary` (CycleSummary), `TotalDurationMs` (long). Keep existing `Command`, `TotalEntities`, `Succeeded`, `Failed` properties for backward compatibility with current CommandHandler exit code logic (`result.Failed > 0 ? 1 : 0`). `CycleResult` is already `public` -- all new types it references must also be `public`
  - [x]1.2 Create `public class EntityResult` in `Persistence/Models/EntityResult.cs` with: `EntityName` (string), `DefinitionGroupId` (string), `Status` (EntityStatus enum), `RecordCount` (int -- set to 0 for now, see Dev Notes on record count limitation), `DurationMs` (long), `Errors` (List<EntityError>)
  - [x]1.3 Create `public class EntityError` in `Persistence/Models/EntityError.cs` with: `Message` (string), `Fingerprint` (string -- empty string for now, populated by Story 4.2), `Category` (ErrorCategory enum)
  - [x]1.4 Create `public enum EntityStatus` in `Persistence/Models/EntityStatus.cs`: `Success`, `Failed`, `Warning`, `Skipped`
  - [x]1.5 Create `public enum ErrorCategory` in `Persistence/Models/ErrorCategory.cs`: `DataQuality`, `Technical`, `Dependency`
  - [x]1.6 Create `public class CycleSummary` in `Persistence/Models/CycleSummary.cs` with: `TotalEntities` (int), `Succeeded` (int), `Failed` (int), `Warnings` (int), `Skipped` (int), `TotalDurationMs` (long)
  - [x]1.7 Create `Persistence/JsonDefaults.cs` with static readonly `ResultJsonOptions`: `WriteIndented = true`, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`, `Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }`

- [x] Task 2: Create credential sanitization service (AC: #8, #9, #10)
  - [x]2.1 Create `Sanitization/IResultSanitizer.cs` interface with single method: `string Sanitize(string rawErrorMessage)`
  - [x]2.2 Create `Sanitization/RegexResultSanitizer.cs` implementing `IResultSanitizer`. Apply regex patterns in order:
    1. Connection strings: `Server\s*=\s*[^;]+;.*?(Database|Initial Catalog)\s*=\s*[^;]+` and `Data Source\s*=\s*[^;]+`
    2. Bearer tokens: `Bearer\s+eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+`
    3. SAS tokens: `(sig|sv|se|st|sp|spr|srt|ss)\s*=\s*[^&\s]+`
    4. Client secrets: patterns matching GUIDs in auth error contexts (`client_secret\s*=\s*[^\s&]+`)
    5. Azure AD tenant/client IDs in auth error contexts: `(client_id|tenant_id|tenant)\s*=\s*[0-9a-fA-F-]{36}`
    Replace all matches with `[REDACTED]`
  - [x]2.3 Use compiled `Regex` with `RegexOptions.Compiled | RegexOptions.IgnoreCase` for performance. Store patterns as `private static readonly Regex[]` to avoid recompilation
  - [x]2.4 Sanitizer must be null-safe: if input is null or empty, return input unchanged

- [x] Task 3: Create result persistence repository (AC: #1, #4, #5, #14, #15, #16)
  - [x]3.1 Create `Persistence/IMigrationResultRepository.cs` interface:
    - `Task SaveCycleResultAsync(CycleResult result, CancellationToken cancellationToken = default)`
    - `Task<CycleResult?> GetCycleResultAsync(string cycleId, CancellationToken cancellationToken = default)`
    - `Task<IReadOnlyList<CycleResult>> GetLatestCycleResultsAsync(int count, CancellationToken cancellationToken = default)`
    - `Task<IReadOnlyList<string>> ListCycleIdsAsync(CancellationToken cancellationToken = default)`
  - [x]3.2 Create `Persistence/JsonFileMigrationResultRepository.cs`:
    - Constructor injects `IOptions<PersistenceSettings>`, `IResultSanitizer`, `ILogger<JsonFileMigrationResultRepository>`
    - Results directory: `{PersistenceSettings.ResultsDirectory}` (defaults to `{OutputDirectory}/results/` if empty)
    - `SaveCycleResultAsync`: sanitize all error messages via `IResultSanitizer` before serialization, serialize to JSON using `JsonDefaults.ResultJsonOptions`, write via atomic temp-file + `File.Move(tempPath, targetPath, overwrite: true)` pattern
    - `GetCycleResultAsync`: read and deserialize by cycle ID
    - `GetLatestCycleResultsAsync`: list files, sort descending by filename (timestamp-sortable), take N, deserialize
    - `ListCycleIdsAsync`: list files, extract cycle IDs from filenames
  - [x]3.3 File naming: `cycle-{yyyy-MM-ddTHHmmss}.json` -- derive from `CycleResult.Timestamp`
  - [x]3.4 Ensure results directory is created if it doesn't exist (in `SaveCycleResultAsync`)
  - [x]3.5 Handle `IOException` gracefully when directory doesn't exist or disk is full -- log error, do NOT throw (persistence failures must not crash the migration)

- [x] Task 4: Create PersistenceSettings configuration (AC: #1)
  - [x]4.1 Create `Settings/PersistenceSettings.cs` POCO: `ResultsDirectory` (string, default empty), `MaxCyclesToRetain` (int, default 50)
  - [x]4.2 Add `PersistenceSettings` section to `appsettings.json`:
    ```json
    "PersistenceSettings": {
      "ResultsDirectory": "",
      "MaxCyclesToRetain": 50
    }
    ```
  - [x]4.3 Register `IOptions<PersistenceSettings>` in `Program.cs` with `.AddOptions<PersistenceSettings>().Bind(builder.Configuration.GetSection(nameof(PersistenceSettings)))`

- [x] Task 5: Modify MigrationPipelineService for per-entity result capture (AC: #2, #6, #7, #12, #13)
  - [x]5.1 Add `Stopwatch` instrumentation around the per-entity processing in `RunQueriesWithDependenciesAsync` to capture `durationMs` per entity
  - [x]5.2 Build per-entity results using a `ConcurrentDictionary<string, EntityResult>` (keyed by entity name) during entity processing. The `Parallel.ForEachAsync` loop variable is `source` (a `SourceQueryItem`). For each entity, capture: `source.EntityName`, `source.DefinitionGroupId`, status, durationMs, and errors list. **Note on RecordCount:** `SqlToXmlService.ExportToOutput` does NOT return a record count -- it returns `IEnumerable<IXmlOutputPart>`. The record count is tracked internally within SqlToXmlService (`skip` variable) but not exposed. Set `EntityResult.RecordCount = 0` for now. If record count is needed, SqlToXmlService would need a return type change which is out of scope for this story
  - [x]5.3 In the per-entity exception handler (inside the `Parallel.ForEachAsync` lambda), catch exceptions and create `EntityError` with the RAW exception message (do NOT sanitize here -- sanitization happens in the repository per ADR-7 / architecture.md boundary rule). Set error category based on exception type: `SqlException` → `Technical`, general `Exception` → `Technical`, `InvalidOperationException` for dependency issues → `Dependency`. **IMPORTANT**: When logging caught exceptions, use `source.EntityName` only -- do NOT log the `source` object directly as it contains `SourceConnectionString`
  - [x]5.4 Add overall `Stopwatch` around the full ExecuteAsync to capture `TotalDurationMs`
  - [x]5.5 Set `CycleResult.CycleId` = `$"cycle-{timestamp:yyyy-MM-ddTHHmmss}"` and `CycleResult.Timestamp` = `DateTimeOffset.UtcNow`
  - [x]5.6 Set `CycleResult.EntitiesRequested` = entityFilter array (or `["all"]` if null)
  - [x]5.7 Build `CycleResult.Summary` from accumulated per-entity status counts. Use `entityResults.Values.Count` for `TotalEntities` (NOT the legacy `parts.Count` which counts output parts, not entities)
  - [x]5.8 Log completion summary: `_logger.Information("Migration cycle {CycleId} complete: {Succeeded} succeeded, {Failed} failed, {Total} total in {DurationMs}ms", ...)`  (NFR7)
  - [x]5.9 **CRITICAL**: Per-entity failure must NOT stop processing of remaining entities. The current `Parallel.ForEachAsync` lambda (line ~109 in MigrationPipelineService) has NO try/catch inside it -- if `_sqlToXmlService.ExportToOutput` throws, the exception propagates and `Parallel.ForEachAsync` aggregates it. You MUST wrap the body of the lambda in a try/catch to: (a) catch per-entity exceptions, (b) record them as `EntityResult.Status = Failed` with the error details, (c) prevent the exception from terminating the parallel loop. The succeeded parts should still be added to the `running` bag for `CheckTasksStatus`
  - [x]5.10 **Part-to-entity mapping for CheckTasksStatus:** After `CheckTasksStatus` completes for a dependency level, map part-level success/failure back to entity-level status. For this story, use the simple approach: if `ExportToOutput` completes without throwing, mark entity as `Success`. If it throws, mark as `Failed`. The `CheckTasksStatus` results (which poll D365 API status for import-d365 mode) track parts, not entities -- for now, the entity status from the try/catch in 5.9 is sufficient. Future stories may refine this mapping

- [x] Task 6: Integrate result persistence into CommandHandler (AC: #1, #7)
  - [x]6.1 Inject `IMigrationResultRepository` into `CommandHandler` constructor (add parameter alongside existing `IMigrationPipelineService`, `SourceQueryCollection`, `ILogger`)
  - [x]6.2 After `ExecuteAsync` returns `CycleResult` in each command method, call `await _resultRepository.SaveCycleResultAsync(result, cancellationToken)` wrapped in try/catch -- log error on failure but do NOT change exit code (persistence failure is non-fatal)
  - [x]6.3 Apply to all three commands: `RunExportToFileAsync`, `RunExportToPackageAsync`, `RunImportDynamicsAsync`
  - [x]6.4 Place persistence call BEFORE exit code determination (`return result.Failed > 0 ? 1 : 0`) but AFTER ExecuteAsync -- result file should be written regardless of pass/fail

- [x] Task 7: Register new services in Program.cs (AC: #15, #16)
  - [x]7.1 Add DI registrations grouped by capability with comment headers:
    ```csharp
    // Result Persistence
    builder.Services.AddSingleton<IMigrationResultRepository, JsonFileMigrationResultRepository>();
    builder.Services.AddSingleton<IResultSanitizer, RegexResultSanitizer>();
    ```
  - [x]7.2 Add options binding:
    ```csharp
    _.AddOptions<PersistenceSettings>()
        .Bind(builder.Configuration.GetSection(nameof(PersistenceSettings)));
    ```
  - [x]7.3 Place registrations AFTER existing service registrations but BEFORE `app.Build()`

- [x] Task 8: Write unit tests for credential sanitizer (AC: #8, #9)
  - [x]8.1 Create `Unit/Sanitization/RegexResultSanitizerTests.cs` with tests:
    - `Sanitize_ConnectionString_ReplacesWithRedacted` -- test `Server=myserver;Database=mydb;User=admin;Password=secret`
    - `Sanitize_BearerToken_ReplacesWithRedacted` -- test `Bearer eyJhbGciOiJSUzI1NiIs...`
    - `Sanitize_SasToken_ReplacesWithRedacted` -- test URL with `sig=xxx&sv=2021-06-08&se=2026-01-01`
    - `Sanitize_ClientSecret_ReplacesWithRedacted` -- test `client_secret=some-secret-value`
    - `Sanitize_TenantId_ReplacesWithRedacted` -- test `tenant_id=12345678-1234-1234-1234-123456789012`
    - `Sanitize_MultiplePatterns_ReplacesAll` -- test input with multiple credential types
    - `Sanitize_NoCredentials_ReturnsInputUnchanged` -- test clean error message
    - `Sanitize_NullInput_ReturnsNull` -- null safety
    - `Sanitize_EmptyInput_ReturnsEmpty` -- empty string safety

- [x] Task 9: Write unit tests for result persistence models (AC: #2, #4)
  - [x]9.1 Create `Unit/Persistence/JsonDefaultsTests.cs` to verify `ResultJsonOptions` produces expected JSON format:
    - `ResultJsonOptions_CamelCaseNaming_SerializesCorrectly`
    - `ResultJsonOptions_EnumAsString_SerializesCorrectly`
    - `ResultJsonOptions_NullIgnored_SerializesCorrectly`
  - [x]9.2 Create `Unit/Persistence/CycleResultSerializationTests.cs`:
    - `CycleResult_RoundTrip_PreservesAllFields` -- serialize and deserialize, verify all fields match
    - `CycleResult_WithErrors_SerializesCorrectly` -- verify error objects in JSON

- [x] Task 10: Write integration tests for result repository (AC: #1, #5, #14)
  - [x]10.1 Create `Integration/Persistence/JsonFileRepositoryTests.cs` with tests using temp directories:
    - `SaveCycleResultAsync_WritesJsonFile_ToResultsDirectory`
    - `SaveCycleResultAsync_AtomicWrite_TempFileNotLeftBehind`
    - `SaveCycleResultAsync_SanitizesErrors_BeforeWriting` -- verify sanitizer is called
    - `GetCycleResultAsync_ExistingCycle_ReturnsDeserializedResult`
    - `GetCycleResultAsync_NonExistentCycle_ReturnsNull`
    - `GetLatestCycleResultsAsync_MultipleFiles_ReturnsInDescendingOrder`
    - `ListCycleIdsAsync_MultipleFiles_ReturnsAllIds`
    - `SaveCycleResultAsync_DirectoryDoesNotExist_CreatesDirectory`
  - [x]10.2 Use `[TempDir]`-style setup with `Directory.CreateTempSubdirectory()` for test isolation

- [x] Task 11: Write integration tests for pipeline result capture (AC: #12, #13)
  - [x]11.1 Create `Integration/Pipeline/PipelineResultCaptureTests.cs`:
    - `ExecuteAsync_SuccessfulEntities_PopulatesEntityResults` -- mock successful entity processing, verify EntityResult list
    - `ExecuteAsync_FailedEntity_CapturesErrorDetails` -- mock entity failure, verify error message captured
    - `ExecuteAsync_PartialFailure_CapturesAllEntityResults` -- verify remaining entities still processed and captured
    - `ExecuteAsync_SetsCorrectCycleIdAndTimestamp` -- verify CycleId format and Timestamp
    - `ExecuteAsync_CalculatesDurationMs` -- verify per-entity and total duration are non-zero
    - `ExecuteAsync_WithEntityFilter_SetsEntitiesRequested` -- verify filter array stored

- [x] Task 12: Update existing CredentialLeakTests (AC: #10, #11)
  - [x]12.1 Verify existing `CredentialLeakTests.cs` in `Audit/` still passes with real persisted result files
  - [x]12.2 Add test: `PersistedResultFile_ContainsNoCredentialPatterns` -- create a CycleResult with known credential-bearing error messages, persist via repository, read the raw file, scan with CredentialPatternScanner

- [x] Task 13: Build verification and full test suite (AC: all)
  - [x]13.1 Run `dotnet build` -- zero errors, zero warnings
  - [x]13.2 Run `dotnet test` -- all 103 existing tests pass plus all new tests
  - [x]13.3 Verify backward compatibility: existing CommandHandler exit code logic (`result.Failed > 0 ? 1 : 0`) works unchanged with extended CycleResult

## Dev Notes

### Recommended Implementation Sequence

Tasks should be implemented in numerical order (1 through 13). Key dependencies:
- Tasks 1 (models) and 2 (sanitizer) have no dependencies -- implement first
- Task 3 (repository) requires Tasks 1 and 2
- Task 4 (settings) can be done alongside Tasks 1-3
- Task 5 (pipeline mods) requires Task 1 (models)
- Task 6 (CommandHandler) requires Tasks 3 and 5
- Task 7 (DI registration) requires Tasks 2, 3, 4
- Tasks 8-12 (tests) require the code from Tasks 1-7
- Task 13 (build verification) is always last

### Purpose

This is the first story in Epic 4 (Migration Result Persistence & Error Comparison). It establishes the persistence foundation that Stories 4.2 (Error Fingerprinting) and 4.3 (Error Comparison Report) build upon. The key deliverables are: (1) extending CycleResult to capture per-entity results, (2) credential sanitization service, (3) JSON file persistence repository, and (4) integrating result capture into the pipeline service and command handler.

### Critical Architecture Constraints

**ADR-1 (Result Persistence Format):**
- JSON files behind `IMigrationResultRepository` interface
- Storage: `{OutputDirectory}/results/` subfolder
- File naming: `cycle-{yyyy-MM-ddTHHmmss}.json`
- Atomic write: temp file + `File.Move` rename
- Schema must match ADR-1 specification exactly (see architecture.md)

**ADR-7 (Credential Sanitization):**
- `IResultSanitizer` interface: `string Sanitize(string rawErrorMessage)`
- Applied in repository's `SaveCycleResultAsync` BEFORE writing
- Patterns: connection strings, Bearer tokens, SAS tokens, client secrets, Azure AD IDs
- Replacement: `[REDACTED]`
- Defense in depth: CI audit test validates no credential patterns in persisted files

**ADR-8 (Exit Code Contract):**
- Result persistence failures must NOT change exit codes
- Exit codes remain: 0 (success), 1 (partial failure), 2 (config error)

### Previous Story Intelligence

**Story 3.2 (Exit Code Standardization & CLI Config Overrides) -- DONE:**
- Added `OperationCanceledException` catch blocks to all three CommandHandler commands
- Updated `Program.Main` catch block to set `Environment.ExitCode = 2` for config errors
- Added eager `SourceQueryCollection` resolution in Program.Main before `app.RunAsync()`
- **Key architectural insight:** Any exception at Program.Main level = config error (exit code 2). CommandHandler catches ALL command-level exceptions
- **.AddCommandLine(args)** in Program.cs line 32 satisfies FR19/FR20
- **Test count: 103 tests passing**
- **Critical finding from code review:** `CycleResult.TotalEntities` currently counts output parts, not entities (known tech debt from Story 2.2)

**Story 3.1 (CLI Entity Selection Flag) -- DONE:**
- Added `--entities` parameter to all three commands
- `ParseEntityFilter()` static method in CommandHandler for comma parsing
- `EntityValidationException` in `Pipeline/` with `InvalidNames`/`ValidNames` properties
- `RunQueriesWithDependenciesAsync` signature: accepts `List<List<SourceQueryItem>>` parameter
- Entities processed in parallel within dependency levels via `Parallel.ForEachAsync`

### Git Intelligence

**Recent commits (latest first):**
- `5fc24b1` Story 3.2: Exit code standardization and CLI config overrides (#12)
- `a39e4ac` Story 3.1: CLI entity selection flag (#11)
- `dbb159b` Story 2.2: Entity authoring guide and developer documentation (#10)

**Key code changes from recent work:**
- `CommandHandler.cs`: Returns `Task<int>`, has try/catch for EntityValidationException (exit 2), OperationCanceledException (exit 1), general Exception (exit 1)
- `Program.cs`: DI registrations use `builder.Services.AddTransient/Singleton` pattern, eager SourceQueryCollection resolution, `Environment.ExitCode = 2` in Main catch
- `MigrationPipelineService.cs`: Transient, processes entities via `RunQueriesWithDependenciesAsync`, returns `CycleResult` with Command/TotalEntities/Succeeded/Failed

### Project Structure Notes

**Files to CREATE:**
```
Dynamics365ImportData/Dynamics365ImportData/Persistence/
  IMigrationResultRepository.cs
  JsonFileMigrationResultRepository.cs
  JsonDefaults.cs
  Models/
    EntityResult.cs
    EntityError.cs
    EntityStatus.cs
    ErrorCategory.cs
    CycleSummary.cs

Dynamics365ImportData/Dynamics365ImportData/Sanitization/
  IResultSanitizer.cs
  RegexResultSanitizer.cs

Dynamics365ImportData/Dynamics365ImportData/Settings/
  PersistenceSettings.cs

Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Sanitization/
  RegexResultSanitizerTests.cs

Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Persistence/
  JsonDefaultsTests.cs
  CycleResultSerializationTests.cs

Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Persistence/
  JsonFileRepositoryTests.cs

Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Pipeline/
  PipelineResultCaptureTests.cs
```

**Files to MODIFY:**
```
Dynamics365ImportData/Dynamics365ImportData/Pipeline/CycleResult.cs
  -> Extend with CycleId, Timestamp, EntitiesRequested, EntityResults, Summary, TotalDurationMs
  -> KEEP existing Command, TotalEntities, Succeeded, Failed for backward compatibility

Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs
  -> Add Stopwatch instrumentation per-entity and overall
  -> Build EntityResult list during processing
  -> Populate CycleResult with per-entity data, cycleId, timestamp, summary
  -> Log completion summary (NFR7)

Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs
  -> Add IMigrationResultRepository to constructor injection
  -> Call SaveCycleResultAsync after ExecuteAsync in all 3 commands
  -> Wrap persistence call in try/catch (non-fatal)

Dynamics365ImportData/Dynamics365ImportData/Program.cs
  -> Add DI registrations for IMigrationResultRepository, IResultSanitizer
  -> Add IOptions<PersistenceSettings> binding

Dynamics365ImportData/Dynamics365ImportData/appsettings.json
  -> Add PersistenceSettings section

Dynamics365ImportData/Dynamics365ImportData.Tests/Audit/CredentialLeakTests.cs
  -> Add test for persisted result file credential scanning
```

**Files NOT to modify:**
- `IMigrationPipelineService.cs` -- interface signature (`Task<CycleResult> ExecuteAsync(...)`) stays the same; CycleResult is being extended, not replaced
- `EntityValidationException.cs` -- no changes needed
- `SourceQueryCollection.cs` -- no changes needed
- Settings POCOs for existing settings -- no changes

### Architecture Compliance

**Folder structure:** Per architecture.md, new code goes in `Persistence/` and `Sanitization/` folders at project root level. Namespace matches folder path: `Dynamics365ImportData.Persistence`, `Dynamics365ImportData.Sanitization`.

**DI lifetime rules:**
- `IMigrationResultRepository` → Singleton (stateless file I/O)
- `IResultSanitizer` → Singleton (stateless regex operations)
- `IMigrationPipelineService` → Transient (already registered, add IResultSanitizer to constructor)

**One interface + one implementation per file.** Interface file named `I{ServiceName}.cs`, implementation named `{ServiceName}.cs`.

### Library & Framework Requirements

- **System.Text.Json** (included in .NET 10, no NuGet) -- for JSON serialization with `JsonStringEnumConverter`
- **System.Security.Cryptography** (included in .NET 10) -- NOT needed yet (fingerprinting is Story 4.2)
- **No new NuGet packages required**

### Technical Implementation Details

**Current CycleResult (from codebase):**
```csharp
public class CycleResult
{
    public string Command { get; set; } = string.Empty;
    public int TotalEntities { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
}
```

**Target CycleResult (extended):**
```csharp
public class CycleResult
{
    // Existing properties (backward compatible)
    public string Command { get; set; } = string.Empty;
    public int TotalEntities { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }

    // New properties for persistence
    public string CycleId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string[] EntitiesRequested { get; set; } = [];
    public List<EntityResult> Results { get; set; } = new();  // Named "Results" to serialize to "results" per ADR-1
    public CycleSummary? Summary { get; set; }
    public long TotalDurationMs { get; set; }
}
```

**Atomic write pattern:**
```csharp
var tempPath = Path.Combine(resultsDir, $".tmp-{Guid.NewGuid():N}.json");
var targetPath = Path.Combine(resultsDir, $"cycle-{result.Timestamp:yyyy-MM-ddTHHmmss}.json");
try
{
    await File.WriteAllTextAsync(tempPath, json, cancellationToken);
    File.Move(tempPath, targetPath, overwrite: true);
}
finally
{
    // Clean up temp file if move failed
    if (File.Exists(tempPath))
        File.Delete(tempPath);
}
```

**CommandHandler persistence integration pattern:**
```csharp
// After ExecuteAsync, before return
try
{
    await _resultRepository.SaveCycleResultAsync(result, cancellationToken);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to persist migration results for cycle {CycleId}", result.CycleId);
}
return result.Failed > 0 ? 1 : 0;
```

**Per-entity result capture pattern (inside Parallel.ForEachAsync lambda -- variable is `source`):**
```csharp
// Inside: async (source, token) => { ... }
var entityStopwatch = Stopwatch.StartNew();
var entityResult = new EntityResult
{
    EntityName = source.EntityName,
    DefinitionGroupId = source.DefinitionGroupId,
    Status = EntityStatus.Success
};
try
{
    // existing entity processing (ExportToOutput call)...
    // NOTE: ExportToOutput does not return record count; set RecordCount = 0
    entityResult.RecordCount = 0;
}
catch (Exception ex)
{
    // Store RAW error message -- sanitization happens in repository before writing
    entityResult.Status = EntityStatus.Failed;
    entityResult.Errors.Add(new EntityError
    {
        Message = ex.Message,  // NOT sanitized here -- repository sanitizes before persistence
        Category = ErrorCategory.Technical
    });
    _logger.Error(ex, "Entity {EntityName} failed during processing", source.EntityName);
    // Do NOT re-throw -- allow other entities to continue
}
finally
{
    entityStopwatch.Stop();
    entityResult.DurationMs = entityStopwatch.ElapsedMilliseconds;
    entityResults.TryAdd(source.EntityName, entityResult); // ConcurrentDictionary, one result per entity
}
```

**Known issue to be aware of:** `CycleResult.TotalEntities` currently counts output parts (not entities) due to how parts are tracked. When adding `Summary.TotalEntities`, count actual entities from `EntityResults.Count` to get the correct value. Keep the existing `TotalEntities` property as-is for backward compatibility with exit code logic.

### Testing Requirements

**Test patterns:** Follow `{Method}_{Scenario}_{ExpectedResult}` naming. Shouldly assertions. Arrange/Act/Assert with comment markers. Inline test data (no external files except golden files).

**Existing test infrastructure to reuse:**
- `TestHelpers/CredentialPatternScanner.cs` -- existing credential pattern detection utility used by `CredentialLeakTests.cs`. Reuse for integration test that scans persisted result files
- `TestHelpers/TestFixtures.cs` -- existing test fixture patterns
- `Audit/TestData/sample-cycle-result.json` -- shows a legacy desired result structure. The implementation should match the ADR-1 schema, not this sample exactly. This file may need updating after implementation to match the new schema so existing CredentialLeakTests continue to pass

**Thread safety for per-entity collection:** Use `ConcurrentDictionary<string, EntityResult>` (keyed by entity name) during parallel entity processing, ensuring one result per entity. Convert to `List<EntityResult>` ordered by entity name via `.Values.OrderBy(r => r.EntityName).ToList()` when building the final CycleResult.

### Critical Guardrails

1. **DO NOT change `IMigrationPipelineService` interface signature** -- `Task<CycleResult> ExecuteAsync(PipelineMode, string[]?, CancellationToken)` stays the same. CycleResult is extended with new properties, but existing properties and return type are unchanged
2. **DO NOT let persistence failures crash the migration** -- all persistence operations must be wrapped in try/catch at the CommandHandler level. A failed write to disk must NOT change the exit code
3. **DO sanitize BEFORE writing** -- `IResultSanitizer.Sanitize()` must be called on every error message before it reaches JSON serialization. The sanitizer is called inside `SaveCycleResultAsync`, not after
4. **DO use atomic writes** -- temp file + `File.Move` to prevent corrupted JSON on crash (NFR8)
5. **DO use `JsonDefaults.ResultJsonOptions`** for ALL result JSON serialization -- camelCase naming, indented, enum as string, null ignored
6. **DO use Serilog structured message templates** for all logging -- NOT string interpolation
7. **DO propagate CancellationToken** through all async call chains
8. **DO keep existing `TotalEntities`, `Succeeded`, `Failed` properties** on CycleResult -- CommandHandler exit code logic depends on `result.Failed > 0 ? 1 : 0`
9. **DO make per-entity result collection thread-safe** -- use `ConcurrentDictionary<string, EntityResult>` keyed by entity name since entities are processed in parallel via `Parallel.ForEachAsync`
10. **DO NOT add fingerprint logic** -- Story 4.2 handles `IErrorFingerprinter`. For now, `EntityError.Fingerprint` defaults to empty string
11. **DO NOT add `--compare` flag or comparison logic** -- Story 4.3 handles that
12. **DO create the `Persistence/` and `Sanitization/` folders** at project root level per architecture.md folder structure
13. **DO register services in Program.cs** with comment headers per architecture pattern

### Anti-Patterns to Avoid

- DO NOT use `Console.WriteLine` for summary output -- use Serilog `_logger.Information`
- DO NOT use `Task.Run()` for async wrapping -- use native async all the way
- DO NOT create static helper classes -- use DI-injected services
- DO NOT hardcode file paths -- use `IOptions<PersistenceSettings>` configuration
- DO NOT catch exceptions without logging them
- DO NOT use `Environment.Exit()` -- use `Environment.ExitCode`
- DO NOT swallow exceptions silently (`catch (Exception) { }`)
- DO NOT use string interpolation in Serilog messages -- use structured templates
- DO NOT create new `HttpClient` instances directly
- DO NOT serialize `SourceQueryItem.SourceConnectionString` into result files -- this contains the SQL connection string. Only serialize entity metadata (name, definition group ID)

### Important Safety Notes

**ClearOutputDirectory() does not affect results:** `CommandHandler.ClearOutputDirectory()` deletes files in the root of `OutputDirectory` using `Directory.GetFiles` (non-recursive). The `results/` subdirectory and its contents will survive the clear operation. No special handling needed.

**MaxCyclesToRetain is forward-looking:** `PersistenceSettings.MaxCyclesToRetain` (default 50) is defined in this story for future use but NO cleanup/retention logic should be implemented now. It may be used in Story 4.3 or 5.1.

**CycleId uniqueness:** File naming `cycle-{yyyy-MM-ddTHHmmss}.json` has second-level granularity. If two runs complete within the same second (extremely unlikely), the second file overwrites the first via `File.Move(overwrite: true)`. This is an acceptable limitation for a CLI tool that runs sequentially.

**Sanitization boundary:** Per architecture.md, `IResultSanitizer` is called ONLY in `IMigrationResultRepository.SaveCycleResultAsync` before writing to disk. It is NOT injected into `MigrationPipelineService`. Error messages flow through the pipeline as raw strings and are sanitized at the persistence boundary. This is the single point of sanitization -- defense-in-depth is provided by the CI audit test (`CredentialLeakTests`), not by dual sanitization.

### Performance Considerations

- Result persistence (single JSON write) should add < 100ms to a migration run (NFR11)
- Use `System.Text.Json` with pre-compiled options (`JsonDefaults.ResultJsonOptions`) to avoid per-call overhead
- `ConcurrentBag<EntityResult>` for thread-safe per-entity collection during parallel processing
- Create results directory once, not on every write

### References

- [Source: architecture.md#ADR-1: Result Persistence Format]
- [Source: architecture.md#ADR-7: Credential Sanitization]
- [Source: architecture.md#ADR-8: Exit Code Contract]
- [Source: architecture.md#ADR-9: Pipeline Service Extraction]
- [Source: architecture.md#JSON Serialization for Result Files]
- [Source: architecture.md#DI Registration Pattern]
- [Source: architecture.md#Folder & Namespace Organization]
- [Source: architecture.md#Error Handling Pattern]
- [Source: architecture.md#Serilog Message Template Pattern]
- [Source: architecture.md#Configuration Section Pattern]
- [Source: architecture.md#Test Naming & Style Pattern]
- [Source: epics.md#Story 4.1: Result Persistence & Credential Sanitization]
- [Source: prd.md#FR6-FR8: Migration Result Persistence]
- [Source: prd.md#NFR1-NFR2: Credential exclusion]
- [Source: prd.md#NFR5-NFR8: Reliability]
- [Source: prd.md#NFR11: Negligible persistence overhead]
- [Source: 3-2-exit-code-standardization-and-cli-config-overrides.md (previous story intelligence)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Pipeline/CycleResult.cs -- current minimal model]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs -- ExecuteAsync, RunQueriesWithDependenciesAsync, per-entity parallel processing]
- [Source: Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs -- exit code mapping, ParseEntityFilter, three command methods]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Program.cs -- DI registration pattern, .AddCommandLine(args), eager SourceQueryCollection resolution]
- [Source: Dynamics365ImportData/Dynamics365ImportData/DependencySorting/SourceQueryItem.cs -- entity record with EntityName, DefinitionGroupId, SourceConnectionString]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Services/SqlToXmlService.cs -- per-entity SQL processing, record counting]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Settings/Dynamics365Settings.cs -- Secret, ClientId, Tenant properties]
- [Source: Dynamics365ImportData/Dynamics365ImportData.Tests/TestHelpers/CredentialPatternScanner.cs -- existing credential pattern detection]
- [Source: Dynamics365ImportData/Dynamics365ImportData.Tests/Audit/CredentialLeakTests.cs -- existing audit test]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.5 (claude-opus-4-5-20251101)

### Debug Log References

- Build: 0 errors, 0 warnings (production code); 19 xUnit1051 CancellationToken analyzer warnings (pre-existing)
- Tests: 136 total (103 existing + 33 new), 136 passed, 0 failed, 0 regressions

### Completion Notes List

- Task 1: Extended CycleResult with CycleId, Timestamp, EntitiesRequested, Results, Summary, TotalDurationMs. Created EntityResult, EntityError, EntityStatus, ErrorCategory, CycleSummary models in Persistence/Models/. Created JsonDefaults with ResultJsonOptions (camelCase, indented, enum as string, null ignored).
- Task 2: Created IResultSanitizer interface and RegexResultSanitizer with 6 compiled regex patterns covering connection strings, Bearer tokens, SAS tokens, client secrets, and Azure AD IDs. Null-safe implementation.
- Task 3: Created IMigrationResultRepository interface with Save/Get/GetLatest/ListCycleIds methods. Implemented JsonFileMigrationResultRepository with atomic temp-file + File.Move writes, sanitization before persistence, IOException graceful handling.
- Task 4: Created PersistenceSettings POCO with ResultsDirectory and MaxCyclesToRetain. Added config section to appsettings.json. Registered IOptions<PersistenceSettings> in Program.cs.
- Task 5: Modified MigrationPipelineService with per-entity Stopwatch instrumentation, ConcurrentDictionary<string, EntityResult> collection, try/catch per entity to prevent parallel loop termination, overall Stopwatch, CycleId/Timestamp generation, EntitiesRequested capture, Summary building from entity status counts, structured log completion summary.
- Task 6: Injected IMigrationResultRepository into CommandHandler. Added PersistResultAsync helper called in all three commands with try/catch (non-fatal). Persistence call placed before exit code return.
- Task 7: Registered IMigrationResultRepository, IResultSanitizer as Singletons, PersistenceSettings options binding in Program.cs.
- Task 8: Created 10 unit tests for RegexResultSanitizer covering all credential patterns, multi-pattern, null/empty safety.
- Task 9: Created 7 unit tests for JsonDefaults and CycleResult serialization (camelCase, enum as string, null ignored, round-trip, ADR-1 schema compliance).
- Task 10: Created 8 integration tests for JsonFileMigrationResultRepository with temp directory isolation (write, atomic write, sanitization, get, get null, latest ordering, list IDs, directory creation).
- Task 11: Created 6 integration tests for pipeline result capture via IMigrationPipelineService contract.
- Task 12: Added PersistedResultFile_ContainsNoCredentialPatterns integration test to CredentialLeakTests. Existing tests pass unchanged.
- Task 13: Build passes with 0 errors. All 136 tests pass. Backward compatibility verified (exit code logic unchanged).
- Fixed existing ExitCodeTests.cs to pass new IMigrationResultRepository parameter to CommandHandler constructor.

### Change Log

- 2026-02-01: Story 4.1 implementation complete - Result persistence and credential sanitization
- 2026-02-01: Code review - Fixed 3 HIGH + 2 MEDIUM issues: (H1+H2) SaveCycleResultAsync no longer mutates caller's CycleResult + catches all exceptions; (H3) PipelineResultCaptureTests documented as contract tests; (M1) Added Password/Pwd sanitization pattern with 2 new tests; strengthened SanitizesErrors test to verify non-mutation. Tests: 138 total, 138 passed.

### File List

**New files:**
- Dynamics365ImportData/Dynamics365ImportData/Persistence/Models/EntityResult.cs
- Dynamics365ImportData/Dynamics365ImportData/Persistence/Models/EntityError.cs
- Dynamics365ImportData/Dynamics365ImportData/Persistence/Models/EntityStatus.cs
- Dynamics365ImportData/Dynamics365ImportData/Persistence/Models/ErrorCategory.cs
- Dynamics365ImportData/Dynamics365ImportData/Persistence/Models/CycleSummary.cs
- Dynamics365ImportData/Dynamics365ImportData/Persistence/JsonDefaults.cs
- Dynamics365ImportData/Dynamics365ImportData/Persistence/IMigrationResultRepository.cs
- Dynamics365ImportData/Dynamics365ImportData/Persistence/JsonFileMigrationResultRepository.cs
- Dynamics365ImportData/Dynamics365ImportData/Sanitization/IResultSanitizer.cs
- Dynamics365ImportData/Dynamics365ImportData/Sanitization/RegexResultSanitizer.cs
- Dynamics365ImportData/Dynamics365ImportData/Settings/PersistenceSettings.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Sanitization/RegexResultSanitizerTests.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Persistence/JsonDefaultsTests.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Persistence/CycleResultSerializationTests.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Persistence/JsonFileRepositoryTests.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Pipeline/PipelineResultCaptureTests.cs

**Modified files:**
- Dynamics365ImportData/Dynamics365ImportData/Pipeline/CycleResult.cs
- Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs
- Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs
- Dynamics365ImportData/Dynamics365ImportData/Program.cs
- Dynamics365ImportData/Dynamics365ImportData/appsettings.json
- Dynamics365ImportData/Dynamics365ImportData.Tests/Audit/CredentialLeakTests.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Pipeline/ExitCodeTests.cs
