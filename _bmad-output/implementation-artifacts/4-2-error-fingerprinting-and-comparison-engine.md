# Story 4.2: Error Fingerprinting & Comparison Engine

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Functional Consultant,
I want errors automatically classified as new or carry-over when comparing cycles,
So that I can focus my triage on what actually changed since the last migration run.

## Acceptance Criteria

1. **Given** error messages from D365FO contain run-specific data (GUIDs, timestamps, record IDs), **When** errors are captured during a migration run, **Then** each error receives a fingerprint computed as `SHA256(entityName + "|" + normalizedMessage)` truncated to 16 hex chars (ADR-6)
2. **And** normalization strips: GUIDs, ISO 8601 timestamps, common datetime formats, numeric record IDs (5+ digit sequences), then collapses whitespace and lowercases
3. **And** the fingerprint is stored alongside the original sanitized error message in the result JSON
4. **And** the same logical error across different cycles produces the same fingerprint
5. **Given** two cycle result files exist, **When** the error comparison engine runs, **Then** `IErrorComparisonService` compares the current cycle against the previous cycle
6. **And** errors are classified as "new" (fingerprint not found in previous cycle) or "carry-over" (fingerprint exists in previous cycle) (FR9, FR10)
7. **And** the comparison handles the first-cycle case gracefully -- reports "First cycle -- no comparison available" and returns exit code 0 (NFR9)

## Tasks / Subtasks

- [x] Task 1: Create error fingerprinting service (AC: #1, #2, #3, #4)
  - [x] 1.1 Create `Fingerprinting/IErrorFingerprinter.cs` interface with method: `string ComputeFingerprint(string entityName, string errorMessage)`
  - [x] 1.2 Create `Fingerprinting/ErrorFingerprinter.cs` implementing `IErrorFingerprinter`:
    1. Normalize `errorMessage`:
       - Strip GUIDs: regex `[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}`
       - Strip ISO 8601 timestamps: regex `\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?`
       - Strip common datetime formats: regex `\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}(?::\d{2})?(?:\s*(?:AM|PM))?` (US format) and `\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}`
       - Strip numeric record IDs (5+ digit sequences): regex `\b\d{5,}\b`
       - Collapse whitespace: regex `\s+` → single space, then `Trim()`
       - Lowercase: `ToLowerInvariant()`
    2. Compute `SHA256(entityName + "|" + normalizedMessage)`
    3. Truncate hash to first 16 hex characters (8 bytes of the 32-byte hash)
    4. Return the 16-char hex string
  - [x] 1.3 Use compiled `Regex` with `RegexOptions.Compiled` for all patterns. Store as `private static readonly Regex[]` to avoid recompilation (same pattern as `RegexResultSanitizer`)
  - [x] 1.4 Null/empty safety: if `errorMessage` is null or empty, compute fingerprint using `entityName + "|"` (empty normalized message). If `entityName` is null, use empty string

- [x] Task 2: Create error comparison models (AC: #5, #6, #7)
  - [x] 2.1 Create `Comparison/Models/ErrorClassification.cs` enum: `New`, `CarryOver`
  - [x] 2.2 Create `Comparison/Models/ClassifiedError.cs` with: `EntityName` (string), `Message` (string), `Fingerprint` (string), `Classification` (ErrorClassification), `Category` (ErrorCategory)
  - [x] 2.3 Create `Comparison/Models/EntityComparisonResult.cs` with: `EntityName` (string), `CurrentStatus` (EntityStatus), `NewErrors` (List\<ClassifiedError\>), `CarryOverErrors` (List\<ClassifiedError\>), `ResolvedFingerprints` (List\<string\> -- fingerprints present in previous cycle but not in current)
  - [x] 2.4 Create `Comparison/Models/ComparisonResult.cs` with: `CurrentCycleId` (string), `PreviousCycleId` (string?), `Timestamp` (DateTimeOffset), `IsFirstCycle` (bool), `EntityComparisons` (List\<EntityComparisonResult\>), `TotalNewErrors` (int), `TotalCarryOverErrors` (int), `TotalResolvedErrors` (int)

- [x] Task 3: Create error comparison service (AC: #5, #6, #7)
  - [x] 3.1 Create `Comparison/IErrorComparisonService.cs` interface with method: `Task<ComparisonResult> CompareAsync(string? currentCycleId = null, string? previousCycleId = null, CancellationToken cancellationToken = default)`
  - [x] 3.2 Create `Comparison/ErrorComparisonService.cs` implementing `IErrorComparisonService`:
    - Constructor injects: `IMigrationResultRepository`, `ILogger<ErrorComparisonService>`
    - `CompareAsync` logic:
      1. If `currentCycleId` is null, load latest 2 cycles via `GetLatestCycleResultsAsync(2)`. First is current, second is previous
      2. If only 1 cycle exists (or 0), return `ComparisonResult` with `IsFirstCycle = true`, empty comparisons, and a log message "First cycle -- no comparison available"
      3. If `previousCycleId` is explicitly provided, load that specific cycle via `GetCycleResultAsync(previousCycleId)`
      4. For each entity in current cycle results:
         - Build set of fingerprints from previous cycle for this entity
         - Classify each error in current cycle: if fingerprint exists in previous set → `CarryOver`, else → `New`
         - Find resolved errors: fingerprints in previous cycle entity errors NOT present in current cycle
      5. Aggregate totals: `TotalNewErrors`, `TotalCarryOverErrors`, `TotalResolvedErrors`
  - [x] 3.3 Handle edge cases:
    - Entity exists in current but not in previous → all errors are `New`
    - Entity exists in previous but not in current → all previous errors are `Resolved`
    - Entity has no errors in either cycle → skip it (don't include in EntityComparisons)

- [x] Task 4: Integrate fingerprinter into MigrationPipelineService (AC: #1, #3)
  - [x] 4.1 Add `IErrorFingerprinter` to `MigrationPipelineService` constructor injection (add parameter after existing `ILogger`)
  - [x] 4.2 In the per-entity catch block (line ~165-174 of `MigrationPipelineService.cs`), after creating the `EntityError`, call: `error.Fingerprint = _fingerprinter.ComputeFingerprint(source.EntityName, ex.Message)`. Note: fingerprint is computed on the RAW error message (before sanitization) so that the fingerprint reflects the actual error content. Sanitization happens later in the repository
  - [x] 4.3 Verify the fingerprint property is serialized correctly in result JSON (uses `JsonDefaults.ResultJsonOptions` which includes camelCase naming)

- [x] Task 5: Register new services in Program.cs (AC: all)
  - [x] 5.1 Add DI registrations grouped by capability with comment headers:
    ```csharp
    // Error Analysis
    builder.Services.AddSingleton<IErrorFingerprinter, ErrorFingerprinter>();
    builder.Services.AddTransient<IErrorComparisonService, ErrorComparisonService>();
    ```
  - [x] 5.2 Place registrations AFTER the "Result Persistence" section in Program.cs (after line 67)
  - [x] 5.3 Add required `using` statements: `using Dynamics365ImportData.Fingerprinting;`, `using Dynamics365ImportData.Comparison;`

- [x] Task 6: Write unit tests for error fingerprinter (AC: #1, #2, #4)
  - [x] 6.1 Create `Unit/Fingerprinting/ErrorFingerprinterTests.cs` with tests:
    - `ComputeFingerprint_SameEntityAndMessage_ReturnsSameFingerprint` -- deterministic hash
    - `ComputeFingerprint_DifferentEntities_ReturnsDifferentFingerprints` -- entity-scoped
    - `ComputeFingerprint_MessageWithGuid_StripsGuidBeforeHashing` -- GUID normalization
    - `ComputeFingerprint_MessageWithTimestamp_StripsTimestampBeforeHashing` -- ISO 8601 normalization
    - `ComputeFingerprint_MessageWithRecordId_StripsRecordIdBeforeHashing` -- 5+ digit sequence normalization
    - `ComputeFingerprint_MessageWithDifferentWhitespace_ReturnsSameFingerprint` -- whitespace collapse
    - `ComputeFingerprint_MessageWithDifferentCase_ReturnsSameFingerprint` -- case normalization
    - `ComputeFingerprint_SameLogicalErrorDifferentRunData_ReturnsSameFingerprint` -- comprehensive: same error with different GUIDs, timestamps, record IDs produces same fingerprint
    - `ComputeFingerprint_DifferentErrors_ReturnsDifferentFingerprints`
    - `ComputeFingerprint_NullMessage_ReturnsValidFingerprint` -- null safety
    - `ComputeFingerprint_EmptyMessage_ReturnsValidFingerprint` -- empty safety
    - `ComputeFingerprint_Returns16HexChars` -- length validation
    - `ComputeFingerprint_CommonDatetimeFormat_StripsDatetime` -- US format datetime normalization

- [x] Task 7: Write unit tests for error comparison service (AC: #5, #6, #7)
  - [x] 7.1 Create `Unit/Comparison/ErrorComparisonServiceTests.cs` with tests using mocked `IMigrationResultRepository` (NSubstitute):
    - `CompareAsync_TwoCycles_ClassifiesNewErrors` -- error in current not in previous = New
    - `CompareAsync_TwoCycles_ClassifiesCarryOverErrors` -- error with same fingerprint in both = CarryOver
    - `CompareAsync_TwoCycles_IdentifiesResolvedErrors` -- error in previous not in current = Resolved
    - `CompareAsync_FirstCycle_ReturnsIsFirstCycleTrue` -- only one cycle exists
    - `CompareAsync_NoCycles_ReturnsIsFirstCycleTrue` -- zero cycles exist
    - `CompareAsync_EntityInCurrentNotInPrevious_AllErrorsNew`
    - `CompareAsync_EntityInPreviousNotInCurrent_AllErrorsResolved`
    - `CompareAsync_BothCyclesNoErrors_EmptyComparisons`
    - `CompareAsync_SpecificCycleId_LoadsSpecificCycle` -- verify explicit previousCycleId is used
    - `CompareAsync_CalculatesCorrectTotals` -- verify TotalNewErrors, TotalCarryOverErrors, TotalResolvedErrors

- [x] Task 8: Write integration tests for fingerprint-in-persistence round-trip (AC: #3)
  - [x] 8.1 Create `Integration/Fingerprinting/FingerprintPersistenceTests.cs`:
    - `Fingerprint_PersistedInResultJson_DeserializesCorrectly` -- create CycleResult with fingerprinted errors, save via repository, read back, verify fingerprints match
    - `Fingerprint_InJsonOutput_UsesCamelCase` -- verify serialized JSON has `"fingerprint"` key (not `"Fingerprint"`)

- [x] Task 9: Build verification and full test suite (AC: all)
  - [x] 9.1 Run `dotnet build` -- zero errors, zero warnings
  - [x] 9.2 Run `dotnet test` -- all 138 existing tests pass plus all new tests (163 total: 138 existing + 25 new)
  - [x] 9.3 Verify backward compatibility: existing tests (sanitizer, persistence, pipeline) unchanged
  - [x] 9.4 Verify that the fingerprint field is populated in persisted result JSON when an entity error occurs

## Dev Notes

### Recommended Implementation Sequence

Tasks should be implemented in numerical order (1 through 9). Key dependencies:
- Task 1 (fingerprinter) has no dependencies -- implement first
- Task 2 (comparison models) has no dependencies -- can be done alongside Task 1
- Task 3 (comparison service) requires Tasks 2 and requires `IMigrationResultRepository` from Story 4.1
- Task 4 (pipeline integration) requires Task 1
- Task 5 (DI registration) requires Tasks 1 and 3
- Tasks 6-8 (tests) require the code from Tasks 1-5
- Task 9 (build verification) is always last

### Purpose

This is the second story in Epic 4 (Migration Result Persistence & Error Comparison). It builds on Story 4.1's result persistence to add: (1) error fingerprinting for stable cross-cycle error identification, and (2) a comparison engine that classifies errors as new vs. carry-over. Story 4.3 will consume the comparison engine to generate markdown reports and add CLI commands.

### Critical Architecture Constraints

**ADR-6 (Error Fingerprinting):**
- Fingerprint formula: `SHA256(entityName + "|" + normalizedMessage)` truncated to 16 hex chars
- Normalization pipeline: GUIDs → timestamps → record IDs → whitespace → lowercase
- Fingerprint stored alongside original (sanitized) error message in result JSON
- Classification: fingerprint exists in previous cycle = carry-over; not found = new
- `IErrorFingerprinter` registered as **Singleton** (stateless hashing)

**ADR-5 (Report Generation Architecture):**
- `IErrorComparisonService` compares two cycle results, classifies new vs. carry-over
- Consumes `IMigrationResultRepository` for data access
- Registered as **Transient** (may hold per-comparison state)
- First-cycle case: return `IsFirstCycle = true` with empty comparisons, exit code 0

**ADR-1 (Result Persistence Format):**
- `EntityError` already has `Fingerprint` property (currently defaults to `string.Empty`)
- Result JSON schema from Story 4.1 already includes `fingerprint` field in serialized output
- `JsonDefaults.ResultJsonOptions` handles serialization (camelCase, enum as string)

### Previous Story Intelligence

**Story 4.1 (Result Persistence & Credential Sanitization) -- DONE:**
- Created `Persistence/` folder with `IMigrationResultRepository`, `JsonFileMigrationResultRepository`, `JsonDefaults`
- Created `Sanitization/` folder with `IResultSanitizer`, `RegexResultSanitizer`
- Created `Persistence/Models/` with `EntityResult`, `EntityError`, `EntityStatus`, `ErrorCategory`, `CycleSummary`
- Extended `CycleResult` with `CycleId`, `Timestamp`, `EntitiesRequested`, `Results`, `Summary`, `TotalDurationMs`
- Integrated per-entity result capture in `MigrationPipelineService` with `ConcurrentDictionary<string, EntityResult>`
- `EntityError.Fingerprint` exists as `string` property (default `string.Empty`) -- this story populates it
- `SaveCycleResultAsync` does NOT mutate caller's `CycleResult` (save-restore pattern for sanitization)
- `RegexResultSanitizer` uses `Regex` with `RegexOptions.Compiled | RegexOptions.IgnoreCase`
- **Test count: 138 tests passing**
- **Key code review finding:** `SaveCycleResultAsync` catches all exceptions internally (persistence failures don't propagate)

**Story 3.2 (Exit Code Standardization) -- DONE:**
- Exit code contract: 0 (success), 1 (partial failure), 2 (config error)
- `CommandHandler` has try/catch for `EntityValidationException` (exit 2), `OperationCanceledException` (exit 1), general `Exception` (exit 1)

### Git Intelligence

**Recent commits (latest first):**
- `5af6c0a` Story 4.1: Result persistence and credential sanitization (#13)
- `5fc24b1` Story 3.2: Exit code standardization and CLI config overrides (#12)
- `a39e4ac` Story 3.1: CLI entity selection flag (#11)

**Key code changes from Story 4.1:**
- `MigrationPipelineService.cs`: Per-entity `Stopwatch`, `ConcurrentDictionary<string, EntityResult>`, try/catch per entity in `Parallel.ForEachAsync` lambda, `CycleId`/`Timestamp` generation
- `CommandHandler.cs`: Injected `IMigrationResultRepository`, `PersistResultAsync` helper (non-fatal)
- `Program.cs`: Registered `IMigrationResultRepository` (Singleton), `IResultSanitizer` (Singleton), `PersistenceSettings` options

### Project Structure Notes

**Files to CREATE:**
```
Dynamics365ImportData/Dynamics365ImportData/Fingerprinting/
  IErrorFingerprinter.cs
  ErrorFingerprinter.cs

Dynamics365ImportData/Dynamics365ImportData/Comparison/
  IErrorComparisonService.cs
  ErrorComparisonService.cs
  Models/
    ComparisonResult.cs
    EntityComparisonResult.cs
    ClassifiedError.cs
    ErrorClassification.cs

Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Fingerprinting/
  ErrorFingerprinterTests.cs

Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Comparison/
  ErrorComparisonServiceTests.cs

Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Fingerprinting/
  FingerprintPersistenceTests.cs
```

**Files to MODIFY:**
```
Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs
  -> Add IErrorFingerprinter to constructor injection
  -> Call _fingerprinter.ComputeFingerprint() in per-entity catch block

Dynamics365ImportData/Dynamics365ImportData/Program.cs
  -> Add DI registrations for IErrorFingerprinter (Singleton), IErrorComparisonService (Transient)
  -> Add using statements for Fingerprinting and Comparison namespaces
```

**Files NOT to modify:**
- `Persistence/Models/EntityError.cs` -- `Fingerprint` property already exists (default `string.Empty`)
- `Persistence/IMigrationResultRepository.cs` -- interface unchanged
- `Persistence/JsonFileMigrationResultRepository.cs` -- unchanged, already serializes `Fingerprint` field
- `Persistence/JsonDefaults.cs` -- unchanged
- `CommandHandler.cs` -- no changes needed (comparison CLI integration is Story 4.3)
- `Sanitization/` -- no changes needed
- `Pipeline/CycleResult.cs` -- no changes needed
- `Pipeline/IMigrationPipelineService.cs` -- interface unchanged

### Architecture Compliance

**Folder structure:** Per architecture.md, new code goes in `Fingerprinting/` and `Comparison/` folders at project root level. Namespace matches folder path: `Dynamics365ImportData.Fingerprinting`, `Dynamics365ImportData.Comparison`, `Dynamics365ImportData.Comparison.Models`.

**DI lifetime rules:**
- `IErrorFingerprinter` -> **Singleton** (stateless hashing operations)
- `IErrorComparisonService` -> **Transient** (may hold per-comparison state)

**One interface + one implementation per file.** Interface file named `I{ServiceName}.cs`, implementation named `{ServiceName}.cs`. Both in the same folder.

### Library & Framework Requirements

- **System.Security.Cryptography** (included in .NET 10, no NuGet) -- for `SHA256.HashData()` static method
- **System.Text.RegularExpressions** (included in .NET 10) -- for normalization regex patterns
- **No new NuGet packages required**

### Technical Implementation Details

**Existing EntityError (from codebase -- Persistence/Models/EntityError.cs):**
```csharp
public class EntityError
{
    public string Message { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;  // Currently always empty -- this story populates it
    public ErrorCategory Category { get; set; }
}
```

**Fingerprint computation pattern:**
```csharp
public string ComputeFingerprint(string entityName, string errorMessage)
{
    var normalized = Normalize(errorMessage ?? string.Empty);
    var input = $"{entityName ?? string.Empty}|{normalized}";
    var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant(); // 8 bytes = 16 hex chars
}
```

**Normalization pipeline (applied in order):**
```csharp
private static readonly Regex GuidPattern = new(
    @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
    RegexOptions.Compiled);

private static readonly Regex Iso8601Pattern = new(
    @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?",
    RegexOptions.Compiled);

private static readonly Regex UsDatetimePattern = new(
    @"\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}(?::\d{2})?(?:\s*(?:AM|PM))?",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

private static readonly Regex DatetimeSpacePattern = new(
    @"\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}",
    RegexOptions.Compiled);

private static readonly Regex RecordIdPattern = new(
    @"\b\d{5,}\b",
    RegexOptions.Compiled);

private static readonly Regex WhitespacePattern = new(
    @"\s+",
    RegexOptions.Compiled);

private static string Normalize(string message)
{
    var result = GuidPattern.Replace(message, string.Empty);
    result = Iso8601Pattern.Replace(result, string.Empty);
    result = UsDatetimePattern.Replace(result, string.Empty);
    result = DatetimeSpacePattern.Replace(result, string.Empty);
    result = RecordIdPattern.Replace(result, string.Empty);
    result = WhitespacePattern.Replace(result, " ");
    return result.Trim().ToLowerInvariant();
}
```

**Pipeline integration point (MigrationPipelineService.cs, inside per-entity catch block ~line 165-174):**
```csharp
// CURRENT code (from Story 4.1):
catch (Exception ex)
{
    entityResult.Status = EntityStatus.Failed;
    entityResult.Errors.Add(new EntityError
    {
        Message = ex.Message,
        Category = ErrorCategory.Technical
    });
    _logger.LogError(ex, "Entity {EntityName} failed during processing", source.EntityName);
}

// MODIFIED code (add fingerprint):
catch (Exception ex)
{
    var error = new EntityError
    {
        Message = ex.Message,
        Fingerprint = _fingerprinter.ComputeFingerprint(source.EntityName, ex.Message),
        Category = ErrorCategory.Technical
    };
    entityResult.Status = EntityStatus.Failed;
    entityResult.Errors.Add(error);
    _logger.LogError(ex, "Entity {EntityName} failed during processing", source.EntityName);
}
```

**Comparison service logic:**
```csharp
public async Task<ComparisonResult> CompareAsync(
    string? currentCycleId = null,
    string? previousCycleId = null,
    CancellationToken cancellationToken = default)
{
    // Load cycles
    CycleResult? current, previous;
    if (currentCycleId is null)
    {
        var latest = await _repository.GetLatestCycleResultsAsync(2, cancellationToken);
        current = latest.Count > 0 ? latest[0] : null;
        previous = latest.Count > 1 ? latest[1] : null;
    }
    else
    {
        current = await _repository.GetCycleResultAsync(currentCycleId, cancellationToken);
        previous = previousCycleId is not null
            ? await _repository.GetCycleResultAsync(previousCycleId, cancellationToken)
            : (await _repository.GetLatestCycleResultsAsync(2, cancellationToken))
                .FirstOrDefault(c => c.CycleId != currentCycleId);
    }

    if (current is null)
    {
        _logger.LogInformation("No cycle results found -- nothing to compare");
        return new ComparisonResult { IsFirstCycle = true, Timestamp = DateTimeOffset.UtcNow };
    }

    if (previous is null)
    {
        _logger.LogInformation("First cycle -- no comparison available");
        return new ComparisonResult
        {
            CurrentCycleId = current.CycleId,
            IsFirstCycle = true,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    // Build comparison per entity...
}
```

**MigrationPipelineService constructor change:**
```csharp
// CURRENT constructor (line 25-38):
public MigrationPipelineService(
    SourceQueryCollection queries,
    SqlToXmlService sqlToXmlService,
    IServiceProvider provider,
    IOptions<Dynamics365Settings> settings,
    ILogger<MigrationPipelineService> logger)

// MODIFIED constructor:
public MigrationPipelineService(
    SourceQueryCollection queries,
    SqlToXmlService sqlToXmlService,
    IServiceProvider provider,
    IOptions<Dynamics365Settings> settings,
    IErrorFingerprinter fingerprinter,
    ILogger<MigrationPipelineService> logger)
```

### Testing Requirements

**Test patterns:** Follow `{Method}_{Scenario}_{ExpectedResult}` naming. Shouldly assertions. Arrange/Act/Assert with comment markers. Inline test data (no external files except golden files).

**Existing test infrastructure to be aware of:**
- `Integration/Pipeline/ExitCodeTests.cs` -- constructs `CommandHandler` with mocked `IMigrationResultRepository`. This test will need updating: `MigrationPipelineService` constructor now requires `IErrorFingerprinter`. If `ExitCodeTests` creates a real `MigrationPipelineService`, the new parameter must be provided. If it uses a mocked `IMigrationPipelineService` interface, no change needed
- `Integration/Pipeline/PipelineResultCaptureTests.cs` -- contract tests for pipeline behavior. These test via the `IMigrationPipelineService` interface (mocked), so no change needed
- `Unit/Persistence/CycleResultSerializationTests.cs` -- tests serialization of `EntityError.Fingerprint`. Currently tests with empty fingerprint; these should continue to pass. New tests should verify non-empty fingerprints serialize correctly

**Thread safety consideration:** `ComputeFingerprint` must be thread-safe since it's called from within `Parallel.ForEachAsync`. Use static methods and no instance state. `SHA256.HashData()` is a static thread-safe method.

### Critical Guardrails

1. **DO fingerprint the RAW error message, NOT the sanitized one** -- Fingerprinting happens in `MigrationPipelineService` (before persistence). Sanitization happens in `JsonFileMigrationResultRepository.SaveCycleResultAsync`. The fingerprint must reflect the actual error content for stable cross-cycle comparison. If you fingerprint the sanitized message, the fingerprint won't match if the sanitizer regex patterns change
2. **DO NOT add CLI commands** -- `compare-errors` / `ce` command is Story 4.3. This story only creates the services and models
3. **DO NOT add `--compare` flag** -- Story 4.3 adds the flag to migration commands
4. **DO NOT modify `IMigrationPipelineService` interface signature** -- The interface `Task<CycleResult> ExecuteAsync(PipelineMode, string[]?, CancellationToken)` stays the same
5. **DO use `SHA256.HashData()` static method** -- thread-safe, no need to create and dispose SHA256 instances. Available in .NET 10
6. **DO use compiled Regex patterns** as `private static readonly Regex` fields (same pattern as `RegexResultSanitizer` in `Sanitization/`)
7. **DO register `IErrorFingerprinter` as Singleton** -- stateless, thread-safe, no disposal needed
8. **DO register `IErrorComparisonService` as Transient** -- per architecture DI lifetime rules
9. **DO use Serilog structured message templates** for all logging -- NOT string interpolation
10. **DO propagate CancellationToken** through all async call chains
11. **DO use `JsonDefaults.ResultJsonOptions`** for any JSON operations in comparison service
12. **DO handle null/empty inputs gracefully** in both fingerprinter and comparison service
13. **DO add `IErrorFingerprinter` to `MigrationPipelineService` constructor** -- it's `internal class`, so DI resolves the new parameter automatically. The constructor is not part of any public interface
14. **DO check if ExitCodeTests needs updating** -- if the test constructs `MigrationPipelineService` directly (not via mock), it needs the new `IErrorFingerprinter` parameter

### Anti-Patterns to Avoid

- DO NOT use `new SHA256Managed()` or `SHA256.Create()` -- use `SHA256.HashData()` static method (simpler, thread-safe, no disposal)
- DO NOT normalize after sanitization -- fingerprint the raw message, let the repository sanitize for persistence separately
- DO NOT use `Console.WriteLine` for output -- use Serilog `_logger.Information`
- DO NOT create static helper classes -- use DI-injected services (`IErrorFingerprinter`)
- DO NOT use string interpolation in Serilog messages -- use structured templates
- DO NOT catch exceptions without logging them
- DO NOT add `--compare` flag or report generation -- that's Story 4.3
- DO NOT modify `EntityError` model -- the `Fingerprint` property already exists

### Performance Considerations

- SHA256 computation is lightweight (~microseconds per hash). No measurable impact on pipeline performance
- Regex normalization uses compiled patterns (one-time JIT cost, fast subsequent execution)
- `IErrorFingerprinter` as Singleton avoids per-call allocation
- Comparison service loads at most 2 cycle result files (small JSON files) -- negligible I/O
- Thread-safe by design: static SHA256.HashData(), static compiled Regex, no shared mutable state

### Fingerprint Stability Contract

The fingerprint MUST be stable across these variations of the same logical error:
- Different GUIDs in error message (e.g., `"Record 3a4b5c6d-... not found"` vs `"Record 7e8f9a0b-... not found"`)
- Different timestamps (e.g., `"Failed at 2026-01-15T10:30:00Z"` vs `"Failed at 2026-02-01T14:00:00Z"`)
- Different record IDs (e.g., `"Row 123456 invalid"` vs `"Row 789012 invalid"`)
- Different whitespace (e.g., extra spaces, tabs, newlines)
- Different case (e.g., `"NOT FOUND"` vs `"Not Found"`)

The fingerprint MUST differ for:
- Different entity names with same error message
- Genuinely different error messages (different root cause)

### References

- [Source: architecture.md#ADR-6: Error Fingerprinting]
- [Source: architecture.md#ADR-5: Report Generation Architecture]
- [Source: architecture.md#ADR-1: Result Persistence Format]
- [Source: architecture.md#DI Registration Pattern]
- [Source: architecture.md#Folder & Namespace Organization]
- [Source: architecture.md#Error Handling Pattern]
- [Source: architecture.md#Serilog Message Template Pattern]
- [Source: architecture.md#Test Naming & Style Pattern]
- [Source: epics.md#Story 4.2: Error Fingerprinting & Comparison Engine]
- [Source: prd.md#FR9-FR10: Error comparison and classification]
- [Source: prd.md#NFR9: Graceful degradation for missing historical data]
- [Source: 4-1-result-persistence-and-credential-sanitization.md (previous story intelligence)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Persistence/Models/EntityError.cs -- Fingerprint property (default empty)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs -- per-entity catch block, constructor, Parallel.ForEachAsync]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Pipeline/CycleResult.cs -- Results property with EntityResult list]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Persistence/JsonDefaults.cs -- ResultJsonOptions for serialization]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Persistence/JsonFileMigrationResultRepository.cs -- SerializeWithSanitizedErrors, GetLatestCycleResultsAsync]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Sanitization/RegexResultSanitizer.cs -- compiled Regex pattern reference]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Program.cs -- DI registration location (line 65-67)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs -- no changes needed for this story]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.5 (claude-opus-4-5-20251101)

### Debug Log References

None -- clean implementation with no blocking issues.

### Completion Notes List

- Implemented `ErrorFingerprinter` with SHA256-based fingerprinting per ADR-6. Normalization pipeline strips GUIDs, ISO 8601 timestamps, US datetime formats, space-separated datetimes, 5+ digit record IDs, then collapses whitespace and lowercases. Thread-safe via static `SHA256.HashData()` and compiled static `Regex` fields.
- Created comparison model hierarchy: `ErrorClassification` enum, `ClassifiedError`, `EntityComparisonResult`, `ComparisonResult` in `Comparison/Models/` namespace.
- Implemented `ErrorComparisonService` that loads cycle results via `IMigrationResultRepository`, classifies errors as New/CarryOver by fingerprint lookup, identifies resolved errors (fingerprints in previous but not current), and handles first-cycle/no-cycle edge cases gracefully.
- Integrated fingerprinter into `MigrationPipelineService` constructor (added `IErrorFingerprinter` parameter). Fingerprint computed on RAW error message in per-entity catch block, before repository sanitization.
- Registered `IErrorFingerprinter` as Singleton and `IErrorComparisonService` as Transient in `Program.cs` per architecture DI lifetime rules.
- Verified `ExitCodeTests` uses mocked `IMigrationPipelineService` interface -- no changes needed for constructor change.
- All 138 existing tests continue to pass unchanged (backward compatible).
- 25 new tests added: 13 fingerprinter unit tests, 10 comparison service unit tests, 2 fingerprint persistence integration tests.
- Total test count: 163 passing, 0 failures, 0 errors.
- Code review fixes applied: consistent timestamp sourcing in CompareAsync, CancellationToken propagation to BuildComparison, defensive GroupBy for duplicate entity names, removed dead `previousEntityNames` variable, added 1 test for fallback path.
- Post-review test count: 164 passing, 0 failures, 0 errors.

### File List

**New files:**
- Dynamics365ImportData/Dynamics365ImportData/Fingerprinting/IErrorFingerprinter.cs
- Dynamics365ImportData/Dynamics365ImportData/Fingerprinting/ErrorFingerprinter.cs
- Dynamics365ImportData/Dynamics365ImportData/Comparison/IErrorComparisonService.cs
- Dynamics365ImportData/Dynamics365ImportData/Comparison/ErrorComparisonService.cs
- Dynamics365ImportData/Dynamics365ImportData/Comparison/Models/ErrorClassification.cs
- Dynamics365ImportData/Dynamics365ImportData/Comparison/Models/ClassifiedError.cs
- Dynamics365ImportData/Dynamics365ImportData/Comparison/Models/EntityComparisonResult.cs
- Dynamics365ImportData/Dynamics365ImportData/Comparison/Models/ComparisonResult.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Fingerprinting/ErrorFingerprinterTests.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Comparison/ErrorComparisonServiceTests.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Fingerprinting/FingerprintPersistenceTests.cs

**Modified files:**
- Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs
- Dynamics365ImportData/Dynamics365ImportData/Program.cs

## Change Log

- 2026-02-01: Story 4.2 implemented -- Error fingerprinting service, comparison engine, pipeline integration, DI registration, and comprehensive tests (25 new tests, 163 total passing)
- 2026-02-01: Code review fixes -- ErrorComparisonService: consistent timestamp sourcing, CancellationToken propagation to BuildComparison, GroupBy for duplicate entity name safety, removed dead variable. Added 1 test (164 total passing)
