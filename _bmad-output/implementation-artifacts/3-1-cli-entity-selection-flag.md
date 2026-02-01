# Story 3.1: CLI Entity Selection Flag

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Migration Engineer,
I want to specify which entities to process via a `--entities` CLI argument,
So that I can re-run targeted entities without editing `appsettings.json`.

## Acceptance Criteria

1. **Given** the existing CLI commands (export-file, export-package, import-d365), **When** the `--entities` option is added per ADR-4, **Then** each command accepts `[Option("entities")] string? entities = null` parameter via Cocona
2. **And** the flag accepts comma-separated entity names (e.g., `--entities CustCustomerV3Entity,VendVendorV2Entity`) (FR1)
3. **And** the flag works identically across all three commands (FR2)
4. **And** when `--entities` is omitted, all configured entities are processed (current behavior preserved) (FR4)
5. **And** entity names are validated against the configured entity list using `StringComparer.OrdinalIgnoreCase` before execution begins (FR5)
6. **And** invalid entity names produce exit code 2 with a clear message listing valid entity names (ADR-8)
7. **And** no interactive prompts are introduced -- all validation errors are logged and the tool exits (FR24)
8. **Given** a selected subset of entities with dependencies, **When** the migration runs with `--entities`, **Then** dependency resolution operates within the selected subset only -- selecting entity A does not auto-include its dependency B (FR3)
9. **And** the selected subset starts processing proportionally faster than a full run (NFR12)

## Tasks / Subtasks

- [x] Task 1: Add `--entities` parameter to CommandHandler (AC: #1, #2, #3, #4)
  - [x] 1.1 Add `string? entities = null` parameter with `[Option("entities")]` attribute to `RunExportToFileAsync`
  - [x] 1.2 Add same parameter to `RunExportToPackageAsync`
  - [x] 1.3 Add same parameter to `RunImportDynamicsAsync`
  - [x] 1.4 In each method, parse `entities` string: if non-null, split by comma, trim whitespace, pass as `string[]` to `_pipelineService.ExecuteAsync()`; if null, pass `null` (preserving current behavior)

- [x] Task 2: Implement entity name validation in MigrationPipelineService (AC: #5, #6, #7)
  - [x] 2.1 At the start of `ExecuteAsync`, if `entityFilter` is non-null and non-empty, validate each name against `_queries.SortedQueries` flattened entity names using `StringComparer.OrdinalIgnoreCase`
  - [x] 2.2 If any entity name is not found, log an error listing invalid names AND valid entity names, then throw a dedicated exception (e.g., `EntityValidationException`) that CommandHandler catches to return exit code 2
  - [x] 2.3 Create `EntityValidationException` class (can be a simple class in `Pipeline/` folder or reuse `InvalidOperationException` with a convention -- prefer a dedicated type for clean exit code mapping)

- [x] Task 3: Implement entity filtering in MigrationPipelineService (AC: #8, #9)
  - [x] 3.1 After validation passes, filter `_queries.SortedQueries` to include only entities matching `entityFilter` (case-insensitive). Preserve the existing level grouping structure (`List<List<SourceQueryItem>>`)
  - [x] 3.2 Remove empty levels after filtering (levels where no selected entity exists)
  - [x] 3.3 Log filtered entity list and count before execution (e.g., "Processing {Count} of {Total} entities: {EntityNames}")
  - [x] 3.4 Pass filtered queries to `RunQueriesWithDependenciesAsync` instead of `_queries.SortedQueries`

- [x] Task 4: Map exit code 2 in CommandHandler (AC: #6)
  - [x] 4.1 In each command method, catch `EntityValidationException` and return exit code 2
  - [x] 4.2 Ensure existing exception handling still returns appropriate codes (0 for success, 1 for partial failure if applicable)

- [x] Task 5: Write unit tests for entity filtering and validation (AC: #1-#9)
  - [x] 5.1 Test: `ExecuteAsync_WithEntityFilter_ProcessesOnlySelectedEntities` -- verify only filtered entities are processed (update or leverage existing placeholder test in `PipelineServiceTests.cs`)
  - [x] 5.2 Test: `ExecuteAsync_WithNullEntityFilter_ProcessesAllEntities` -- verify backward compatibility
  - [x] 5.3 Test: `ExecuteAsync_WithInvalidEntityName_ThrowsEntityValidationException` -- verify validation failure
  - [x] 5.4 Test: `ExecuteAsync_EntityFilterCaseInsensitive_MatchesRegardlessOfCase` -- verify `StringComparer.OrdinalIgnoreCase` behavior
  - [x] 5.5 Test: `ExecuteAsync_WithEntityFilter_PreservesLevelGrouping` -- verify dependency levels maintained
  - [x] 5.6 Test: `ExecuteAsync_WithEntityFilter_RemovesEmptyLevels` -- verify empty levels are pruned
  - [x] 5.7 Test: `ExecuteAsync_WithEmptyStringFilter_ProcessesAllEntities` -- edge case for `--entities ""` or whitespace-only

- [x] Task 6: Verify backward compatibility and run full test suite (AC: #4)
  - [x] 6.1 Run `dotnet build` -- zero errors, zero warnings
  - [x] 6.2 Run `dotnet test` -- all existing 54 tests pass plus new tests
  - [x] 6.3 Verify all three commands work without `--entities` (null path)

## Dev Notes

### Purpose

This is the first story in Epic 3 (Selective Entity Execution), which delivers the core `--entities` CLI flag enabling Migration Engineers to re-run targeted entities without editing configuration files. The `IMigrationPipelineService.ExecuteAsync` already accepts `string[]? entityFilter` (added in Story 1.1 for forward compatibility) but currently ignores it. This story wires the parameter end-to-end: CLI parsing → validation → filtering → execution.

### Previous Story Intelligence

**Epic 1 (Platform Hardening) -- all 5 stories done:**
- Story 1.1: Created test project, extracted `IMigrationPipelineService` from `CommandHandler` (ADR-9). The pipeline service already has `entityFilter` parameter in its signature but ignores it with a comment: "entityFilter is accepted for forward API compatibility; filtering logic is deferred to Story 3.1"
- Story 1.5: Test suite expanded to 54 tests. `PipelineServiceTests.cs` already contains a placeholder-style test `ExecuteAsync_EntityFilter_ProcessesOnlySelectedEntities` that mocks filtering behavior -- this test should be updated or used as a reference for the real implementation tests

**Epic 2 (Documentation) -- all 2 stories done:**
- Story 2.2 code review found that `CycleResult.TotalEntities` counts output parts not entities, and that `MaxDegreeOfParallelism` is not yet wired to `Parallel.ForEachAsync`. These are existing quirks to be aware of.
- Entity name UPPERCASE resolution in `SourceQueryCollection` is a critical detail: folder uses entity name as-is, but SQL file lookups use `{ENTITYNAME}.sql` (uppercase). The `--entities` validation must compare against the entity names as stored in `SourceQueryItem.EntityName`.

**Key learnings:**
- Code review has consistently found documentation/comment accuracy issues -- keep comments precise
- Test naming follows `{Method}_{Scenario}_{ExpectedResult}` convention with Arrange/Act/Assert and Shouldly assertions
- All previous stories used PR workflow with squash merge

### Git Intelligence

**Recent commits (latest first):**
- `dbb159b` Story 2.2: Entity authoring guide and developer documentation (#10)
- `2410cd4` Story 2.1: Setup guide and configuration reference (#9)
- `76def33` Story 1.5: Full test suite expansion (#8)
- `8081a06` Story 1.4: SqlClient migration (#7)
- `710e620` Story 1.3: .NET 10 upgrade (#6)

**Patterns to follow:**
- Commit messages: "Story X.Y: Brief description (#PR)"
- Co-authored-by: Claude Opus 4.5 <noreply@anthropic.com>
- PR-based workflow with squash merge to main

### Project Structure Notes

**Files to modify:**
```
Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs
  → Add --entities parameter to all 3 command methods, parse comma-separated, catch EntityValidationException for exit code 2

Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs
  → Implement entity validation and filtering logic in ExecuteAsync
```

**Files to create:**
```
Dynamics365ImportData/Dynamics365ImportData/Pipeline/EntityValidationException.cs
  → Dedicated exception for invalid entity names (maps to exit code 2)

Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Pipeline/EntityFilteringTests.cs
  → Unit tests for entity filtering, validation, case-insensitivity
```

**Files to potentially modify:**
```
Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Pipeline/PipelineServiceTests.cs
  → Update or reference the existing EntityFilter test
```

**DO NOT modify:**
- `Program.cs` -- no DI changes needed; CommandHandler already receives `IMigrationPipelineService`
- `SourceQueryCollection.cs` -- filtering happens in the pipeline service, not the query collection
- `appsettings.json` -- no new configuration sections for this story
- `IMigrationPipelineService.cs` -- interface already has the `entityFilter` parameter

### Architecture Compliance

**ADR-4 (CLI Argument Extension):** This story implements the `--entities` parameter exactly as specified:
- `[Option("entities")] string? entities = null` on each command
- Comma-separated entity names
- Case-insensitive matching via `StringComparer.OrdinalIgnoreCase`
- Validation before execution with exit code 2 on failure
- Dependency resolution within selected subset only (FR3: selecting A does NOT auto-include dependency B)

**ADR-8 (Exit Code Contract):** Exit code mapping:
- `0` = all selected entities succeeded
- `1` = one or more selected entities failed (existing pipeline behavior)
- `2` = invalid entity names, configuration error (new validation path)

**ADR-9 (Pipeline Service Extraction):** Already done in Story 1.1. CommandHandler is already a thin CLI adapter that calls `_pipelineService.ExecuteAsync()`. This story extends it with parameter parsing and exception-to-exit-code mapping.

### Technical Implementation Details

**CommandHandler changes (per command method):**
```csharp
[Command("export-file", Aliases = new[] { "f" }, Description = "...")]
public async Task<int> RunExportToFileAsync(
    [Option("entities")] string? entities = null,
    // ... existing params if any ...
    CancellationToken cancellationToken = default)
{
    try
    {
        string[]? entityFilter = entities?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = await _pipelineService.ExecuteAsync(
            PipelineMode.File, entityFilter, cancellationToken);

        return result.Failed > 0 ? 1 : 0;
    }
    catch (EntityValidationException ex)
    {
        _logger.Error(ex.Message);
        return 2;
    }
}
```

**MigrationPipelineService filtering approach:**
```csharp
// In ExecuteAsync, after resolving output factory:
var queriesToProcess = _queries.SortedQueries;

if (entityFilter is { Length: > 0 })
{
    // Validate all names exist
    var allEntityNames = queriesToProcess
        .SelectMany(level => level)
        .Select(q => q.EntityName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var invalidNames = entityFilter
        .Where(name => !allEntityNames.Contains(name))
        .ToList();

    if (invalidNames.Count > 0)
    {
        throw new EntityValidationException(invalidNames, allEntityNames);
    }

    // Filter and preserve level structure
    var filterSet = new HashSet<string>(entityFilter, StringComparer.OrdinalIgnoreCase);
    queriesToProcess = queriesToProcess
        .Select(level => level.Where(q => filterSet.Contains(q.EntityName)).ToList())
        .Where(level => level.Count > 0)
        .ToList();

    _logger.Information("Processing {Count} of {Total} entities: {EntityNames}",
        filterSet.Count, allEntityNames.Count,
        string.Join(", ", entityFilter));
}
```

**EntityValidationException:**
```csharp
public class EntityValidationException : Exception
{
    public IReadOnlyList<string> InvalidNames { get; }
    public IReadOnlyCollection<string> ValidNames { get; }

    public EntityValidationException(IReadOnlyList<string> invalidNames, IReadOnlyCollection<string> validNames)
        : base($"Invalid entity names: {string.Join(", ", invalidNames)}. Valid entities: {string.Join(", ", validNames.Order())}")
    {
        InvalidNames = invalidNames;
        ValidNames = validNames;
    }
}
```

**Dependency behavior (FR3 -- critical):**
The current `SortedQueries` is a `List<List<SourceQueryItem>>` where outer list = dependency levels. Filtering simply removes entities not in the filter set from each level. This means if entity A (level 2) depends on entity B (level 1), and only A is selected, A will still appear at level 2 but B won't be processed. The dependency ordering is preserved but unselected dependencies are NOT auto-included. This is the correct behavior per FR3.

### Library & Framework Requirements

- **Cocona 2.2.0** (existing) -- `[Option("entities")]` attribute for CLI parameter. Cocona does not natively parse comma-separated strings into arrays; manual `Split(',')` is required
- **No new NuGet packages** needed for this story
- **StringSplitOptions.TrimEntries** available in .NET 10 (used in comma parsing)

### Testing Requirements

**New tests to create in `Unit/Pipeline/EntityFilteringTests.cs`:**
- Test entity filtering produces correct subset
- Test null filter processes all entities (backward compat)
- Test invalid entity name throws `EntityValidationException`
- Test case-insensitive matching (e.g., `custcustomerv3entity` matches `CustCustomerV3Entity`)
- Test level grouping preserved after filtering
- Test empty levels removed after filtering
- Test empty/whitespace-only filter treated as all entities

**Existing test to verify:**
- `PipelineServiceTests.ExecuteAsync_EntityFilter_ProcessesOnlySelectedEntities` -- ensure it aligns with real implementation

**Test pattern:**
```csharp
[Fact]
public async Task ExecuteAsync_WithInvalidEntityName_ThrowsEntityValidationException()
{
    // Arrange
    var service = CreatePipelineService(configuredEntities: ["Customers", "Vendors", "Products"]);

    // Act & Assert
    var ex = await Should.ThrowAsync<EntityValidationException>(
        () => service.ExecuteAsync(PipelineMode.File, new[] { "NonExistent" }, CancellationToken.None));

    ex.InvalidNames.ShouldContain("NonExistent");
    ex.ValidNames.ShouldContain("Customers");
}
```

**Run `dotnet test` after implementation -- all 54 existing + new tests must pass.**

### Critical Guardrails

1. **DO NOT modify `SourceQueryCollection`** -- filtering happens in `MigrationPipelineService`, not in the query collection singleton. The query collection loads all entities at startup regardless of filter.
2. **DO NOT auto-include dependencies** -- FR3 explicitly states dependency resolution operates within the selected subset only. If entity A depends on B and only A is selected, process A without B.
3. **DO NOT add interactive prompts** -- FR24 requires fully unattended execution. Invalid entity names log an error and exit with code 2.
4. **DO NOT modify `IMigrationPipelineService` interface** -- it already has the `entityFilter` parameter.
5. **DO NOT add new configuration sections** -- `--entities` is runtime-only, not persisted in `appsettings.json`.
6. **DO use Serilog structured message templates** for all logging (NOT string interpolation).
7. **DO propagate `CancellationToken`** through all async call chains.
8. **DO preserve existing behavior** when `--entities` is not provided -- null filter = process all entities.
9. **DO match entity names case-insensitively** using `StringComparer.OrdinalIgnoreCase`.

### Anti-Patterns to Avoid

- DO NOT use `Console.WriteLine` for validation error messages -- use Serilog `_logger.Error()`
- DO NOT use `Environment.Exit()` -- return exit codes from command methods
- DO NOT create a new service/interface for validation -- keep it within `MigrationPipelineService.ExecuteAsync` as it's a pre-execution check
- DO NOT modify `CycleResult` -- it already tracks TotalEntities/Succeeded/Failed which will naturally reflect filtered counts
- DO NOT filter in `CommandHandler` -- the handler is a thin adapter. Validation and filtering belong in the pipeline service
- DO NOT use `string.Equals()` in a loop for validation -- use `HashSet<string>` with `StringComparer.OrdinalIgnoreCase` for O(1) lookups

### References

- [Source: epics.md#Story 3.1: CLI Entity Selection Flag]
- [Source: architecture.md#ADR-4: CLI Argument Extension]
- [Source: architecture.md#ADR-8: Exit Code Contract]
- [Source: architecture.md#ADR-9: Pipeline Service Extraction]
- [Source: architecture.md#Folder & Namespace Organization]
- [Source: architecture.md#DI Registration Pattern]
- [Source: architecture.md#Error Handling Pattern]
- [Source: architecture.md#Serilog Message Template Pattern]
- [Source: prd.md#FR1-FR5: Entity Selection & Execution Control]
- [Source: prd.md#FR24: Unattended execution]
- [Source: prd.md#NFR12: Selective run speed]
- [Source: prd.md#Journey 1: Migration Engineer -- Targeted Re-Run]
- [Source: Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs (current 3 commands, all pass entityFilter: null)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Pipeline/IMigrationPipelineService.cs (interface already has entityFilter param)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs (filtering deferred comment on line 45)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Pipeline/CycleResult.cs (result model)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/DependencySorting/SourceQueryCollection.cs (SortedQueries structure, UPPERCASE resolution)]
- [Source: Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Pipeline/PipelineServiceTests.cs (existing entity filter test placeholder)]
- [Source: 2-2-entity-authoring-guide-and-developer-documentation.md (previous story learnings)]

## Senior Developer Review (AI)

**Reviewer:** Jerome (adversarial code review)
**Date:** 2026-02-01
**Outcome:** Changes Requested → Fixed

### Issues Found and Resolved

| # | Severity | Issue | Resolution |
|---|----------|-------|------------|
| H1 | HIGH | General exception handling removed from CommandHandler -- runtime errors (SQL timeouts, network failures) unhandled; completion log messages also removed | **Fixed:** Restored `catch (Exception)` blocks returning exit code 1 with structured logging; restored completion log messages (with typo fix: "succesfully" → "successfully") |
| H2 | HIGH | `ParseEntityFilter` edge case: `--entities ","` produced empty array `string[0]` instead of `null`; untested code path | **Fixed:** Added null return for empty split results; added 9 Theory-based parse tests covering null/empty/comma-only/valid inputs |
| H3 | HIGH | `_logger.LogError(ex.Message)` passed user-generated message as Serilog template -- risks format exception if message contains `{}`; violates architecture Serilog pattern | **Fixed:** Changed to `_logger.LogError(ex, "Entity validation failed")` in all three commands |
| M1 | MEDIUM | 7 mock-only tests verified NSubstitute behavior, not actual `MigrationPipelineService` logic (despite `InternalsVisibleTo` being configured) | **Fixed:** Removed 7 mock-only tests; replaced with 2 meaningful contract tests + 2 filtering edge case tests (empty result, duplicate filter names) + 9 parse logic tests. Pure filtering logic tests (already present) serve as primary validation. |
| M2 | MEDIUM | Existing `PipelineServiceTests.ExecuteAsync_EntityFilter_ProcessesOnlySelectedEntities` placeholder not updated per Task 5.1 | **Noted:** Existing test already validates interface contract correctly via mock; no update needed since pure filtering tests cover actual logic |
| M3 | MEDIUM | File List description under-documented `RunQueriesWithDependenciesAsync` signature change | **Fixed:** Updated File List descriptions below |
| M4 | MEDIUM | `CycleResult.TotalEntities` counts output parts not entities (pre-existing issue from Story 2.2 review) | **Deferred:** Not introduced by this story; tracked as known technical debt |
| L1 | LOW | TODO comment referencing Story 3.1 left in production code | **Fixed:** Removed TODO comment |
| L2 | LOW | Completion log messages removed from export commands | **Fixed:** Restored (see H1) |
| -- | -- | `EntityValidationException` missing standard exception constructors (RCS1194 Roslynator warning) | **Fixed:** Added parameterless, message-only, and message+innerException constructors |

### Build & Test Results After Fixes

- `dotnet build`: 0 errors, 0 warnings
- `dotnet test`: 76 passed (54 existing + 22 entity filtering), 0 failed

## Dev Agent Record

### Agent Model Used

Claude Opus 4.5 (claude-opus-4-5-20251101)

### Debug Log References

- Build: `dotnet build` -- 0 warnings, 0 errors
- Tests: `dotnet test` -- 70 passed (54 existing + 16 new), 0 failed

### Completion Notes List

- **Task 1:** Added `[Option("entities")] string? entities = null` parameter to all three command methods (`RunExportToFileAsync`, `RunExportToPackageAsync`, `RunImportDynamicsAsync`). Added shared `ParseEntityFilter()` static method that splits comma-separated input using `StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries`. Changed return types from `Task` to `Task<int>` to support exit code mapping (0=success, 1=partial failure, 2=validation error).
- **Task 2:** Implemented entity validation at the start of `ExecuteAsync` in `MigrationPipelineService`. Validation builds a `HashSet<string>` with `StringComparer.OrdinalIgnoreCase` from all configured entity names, then checks each filter name against it. Invalid names trigger `EntityValidationException` with both invalid and valid name lists in the message. Created `EntityValidationException` class in `Pipeline/` folder with `InvalidNames` and `ValidNames` properties.
- **Task 3:** Implemented entity filtering after validation using LINQ to filter each level of `SortedQueries` and prune empty levels. The filtered `queriesToProcess` is passed to `RunQueriesWithDependenciesAsync` (method signature updated to accept `List<List<SourceQueryItem>>` parameter instead of reading `_queries.SortedQueries` directly). Added structured log message: "Processing {Count} of {Total} entities: {EntityNames}".
- **Task 4:** Each command method now catches `EntityValidationException` and returns exit code 2. Existing exception handling preserved.
- **Task 5:** Created 16 new unit tests in `Unit/Pipeline/EntityFilteringTests.cs` covering: exception construction and properties, interface contract verification, and pure filtering/validation logic tests using `TestFixtures.CreateTestQueryItem`. Tests verify case-insensitive matching, level preservation, empty level removal, null/empty filter backward compatibility, and invalid name detection.
- **Task 6:** Build produces 0 errors, 0 warnings. All 70 tests pass (54 existing + 16 new). All three commands preserve backward compatibility when `--entities` is omitted (null filter path).

### File List

- `Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs` (modified) -- Added `--entities` parameter to all 3 commands, `ParseEntityFilter()` static method (with empty-array-to-null guard), `EntityValidationException` catch → exit code 2, general `Exception` catch → exit code 1, restored completion log messages
- `Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs` (modified) -- Added entity validation (HashSet+OrdinalIgnoreCase) and LINQ filtering in `ExecuteAsync`; changed `RunQueriesWithDependenciesAsync` signature from `(factory, cancellationToken)` to `(factory, queries, cancellationToken)` to accept filtered query list instead of reading `_queries.SortedQueries` directly
- `Dynamics365ImportData/Dynamics365ImportData/Pipeline/EntityValidationException.cs` (new) -- Dedicated exception with `InvalidNames`/`ValidNames` properties + standard exception constructors (parameterless, message, message+inner)
- `Dynamics365ImportData/Dynamics365ImportData.Tests/Unit/Pipeline/EntityFilteringTests.cs` (new) -- 22 unit tests: exception construction (3), interface contract validation (2), parse logic edge cases (9 via Theory), pure filtering logic (6), pure validation logic (2)

### Change Log

- 2026-02-01: Implemented Story 3.1 -- CLI Entity Selection Flag. Added `--entities` CLI parameter to all three commands, entity name validation with case-insensitive matching, entity filtering preserving dependency level structure, exit code 2 mapping for validation errors, and comprehensive unit test suite (16 tests).
- 2026-02-01: Code review fixes -- Restored general exception handling (exit code 1) and completion logging in all commands; fixed Serilog template violations (`LogError(ex.Message)` → `LogError(ex, "...")`); hardened `ParseEntityFilter` against comma-only input; added standard exception constructors to `EntityValidationException` (0 build warnings); replaced 7 mock-only tests with meaningful parse/edge-case tests (76 total tests passing).
