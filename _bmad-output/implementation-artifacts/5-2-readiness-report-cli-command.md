# Story 5.2: Readiness Report CLI Command

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Project Director,
I want to generate readiness reports on demand via the command line,
So that I can produce updated reports whenever needed for steering committee meetings.

## Acceptance Criteria

1. **Given** the readiness report service is implemented, **When** the `readiness-report` / `rr` command is added per ADR-5, **Then** the command accepts `[Option("cycles")] int cycles = 5` to control how many cycles to include
2. **And** the command accepts `[Option("threshold")] string? thresholdConfig = null` to override threshold settings
3. **And** the command accepts `[Option("output")] string? outputPath = null` to override the report output path
4. **And** the report is written to a predictable file path for downstream consumption
5. **And** exit code 0 is returned on successful generation
6. **And** exit code 1 is returned if the report was generated but with warnings (e.g., missing cycle data)
7. **And** exit code 2 is returned for configuration errors (e.g., invalid threshold format)
8. **And** the command operates without interactive prompts (FR24)

## Tasks / Subtasks

- [x] Task 1: Add `IReadinessReportService` dependency to `CommandHandler` (AC: all)
  - [x] 1.1 Add `IReadinessReportService` field to `CommandHandler`
  - [x] 1.2 Add `IReadinessReportService` parameter to `CommandHandler` constructor
  - [x] 1.3 Store injected service in readonly field

- [x] Task 2: Implement `readiness-report` / `rr` CLI command in `CommandHandler` (AC: #1, #2, #3, #4, #5, #6, #7, #8)
  - [x] 2.1 Add `RunReadinessReportAsync` method with `[Command("readiness-report", Aliases = new[] { "rr" }, Description = "Generate migration readiness report across multiple cycles")]` attribute
  - [x] 2.2 Add `[Option("cycles")] int cycles = 5` parameter for cycle count (default 5)
  - [x] 2.3 Add `[Option("threshold")] string? thresholdConfig = null` parameter for threshold override
  - [x] 2.4 Add `[Option("output")] string? outputPath = null` parameter for output path override
  - [x] 2.5 Add `CancellationToken cancellationToken = default` parameter
  - [x] 2.6 Implement threshold parsing: parse `thresholdConfig` string (format: `"success:N,warning:M"`) into override values
  - [x] 2.7 Validate threshold format -- return exit code 2 with clear error message for invalid format
  - [x] 2.8 Call `_readinessReportService.GenerateAsync(cycles, outputPath, cancellationToken)` with parsed parameters
  - [x] 2.9 Return exit code 0 on successful report generation
  - [x] 2.10 Return exit code 1 if service returns null (no cycles found -- warning scenario)
  - [x] 2.11 Return exit code 2 for argument validation errors (negative cycles, invalid threshold format)
  - [x] 2.12 Handle `OperationCanceledException` -> exit code 1
  - [x] 2.13 Handle general `Exception` -> exit code 1 with error logging
  - [x] 2.14 No interactive prompts -- all errors logged and tool exits (FR24)

- [x] Task 3: Update existing test infrastructure to include `IReadinessReportService` (AC: all)
  - [x] 3.1 Update `ExitCodeTests.CreateCommandHandler()` to include mocked `IReadinessReportService` in `CommandHandler` constructor
  - [x] 3.2 Update `CompareErrorsCommandTests` constructor to include mocked `IReadinessReportService` in `CommandHandler` constructor
  - [x] 3.3 Verify all existing 206 tests still pass with constructor change

- [x] Task 4: Write unit/integration tests for readiness-report command (AC: #1-#8)
  - [x] 4.1 Create `Integration/Reporting/ReadinessReportCommandTests.cs` following `CompareErrorsCommandTests` pattern
  - [x] 4.2 Test: `RunReadinessReportAsync_SuccessfulGeneration_ReturnsExitCode0` -- mock service returns file path (AC: #5)
  - [x] 4.3 Test: `RunReadinessReportAsync_NoCyclesFound_ReturnsExitCode1` -- mock service returns null (AC: #6)
  - [x] 4.4 Test: `RunReadinessReportAsync_DefaultCycles_PassesFiveToService` -- verify default cycles=5 passed (AC: #1)
  - [x] 4.5 Test: `RunReadinessReportAsync_CustomCycles_PassesValueToService` -- verify custom cycle count (AC: #1)
  - [x] 4.6 Test: `RunReadinessReportAsync_CustomOutputPath_PassesPathToService` -- verify output path (AC: #3)
  - [x] 4.7 Test: `RunReadinessReportAsync_InvalidThreshold_ReturnsExitCode2` -- invalid threshold format (AC: #7)
  - [x] 4.8 Test: `RunReadinessReportAsync_ValidThreshold_ParsesCorrectly` -- valid threshold parsed and applied
  - [x] 4.9 Test: `RunReadinessReportAsync_Canceled_ReturnsExitCode1` -- cancellation handling
  - [x] 4.10 Test: `RunReadinessReportAsync_ServiceException_ReturnsExitCode1` -- general exception handling
  - [x] 4.11 Test: `RunReadinessReportAsync_ZeroCycles_ReturnsExitCode2` -- invalid argument validation
  - [x] 4.12 Test: `RunReadinessReportAsync_NegativeCycles_ReturnsExitCode2` -- invalid argument validation
  - [x] 4.13 Test: `RunReadinessReportAsync_ReportPath_LoggedToConsole` -- verify path logging (AC: #4)

- [x] Task 5: Build verification and full test suite (AC: all)
  - [x] 5.1 Run `dotnet build` -- zero errors, zero warnings
  - [x] 5.2 Run `dotnet test` -- all 206 existing tests pass plus all new tests (218 total)
  - [x] 5.3 Verify backward compatibility: existing commands (`export-file`, `export-package`, `import-d365`, `compare-errors`) function identically
  - [x] 5.4 Verify `readiness-report` command is discoverable via `--help` (registered via [Command] attribute, verified by build)
  - [x] 5.5 Verify `rr` alias works (registered via Aliases parameter, verified by build)

## Dev Notes

### Purpose

This is the second and final story in Epic 5 (Readiness Reporting). Story 5.1 created the `IReadinessReportService` with all the aggregation, trend calculation, and markdown generation logic. This story wires up the CLI command (`readiness-report` / `rr`) that invokes the service on demand, completing the Project Director's Journey 4 (Go/No-Go Decision).

This is a **thin CLI adapter story** -- the heavy lifting is already done in Story 5.1's `ReadinessReportService`. This story adds the Cocona command method, threshold parsing, argument validation, and exit code mapping.

### Critical Architecture Constraints

**ADR-5 (Report Generation Architecture):**
- `readiness-report` / `rr` -- Generate readiness report across multiple cycles
- `[Option("cycles")] int cycles = 5` -- number of cycles to include
- `[Option("threshold")] string? thresholdConfig = null` -- override threshold settings
- Output: markdown file in configurable output path
- [Source: architecture.md#ADR-5: Report Generation Architecture]

**ADR-8 (Exit Code Contract):**
- 0 = success (report generated successfully)
- 1 = partial failure (report generated with warnings, e.g., no cycle data found, or general runtime error)
- 2 = configuration error (invalid arguments, invalid threshold format -- didn't start)
- [Source: architecture.md#ADR-8: Exit Code Contract]

**FR24 (Unattended Execution):**
- No interactive prompts -- all validation errors are logged and the tool exits
- [Source: epics.md#FR24]

### Previous Story Intelligence

**Story 5.1 (Readiness Report Generation) -- DONE:**
- Created `IReadinessReportService` with `GenerateAsync(int? cycleCount, string? outputPath, CancellationToken)` returning `string?` (file path or null)
- Returns null when no cycles found -- this is the signal for exit code 1 in the CLI command
- `ReportSettings` POCO with `DefaultCycleRange` (5), `SuccessThreshold` (0), `WarningThreshold` (5), `OutputDirectory` ("")
- Already registered in DI as Transient
- Already has `IOptions<ReportSettings>` binding in `Program.cs`
- **206 tests passing** (184 original + 22 from Story 5.1)
- **KEY INSIGHT:** The threshold override via CLI (`--threshold` option) needs to feed into the service. The service currently reads thresholds from `IOptions<ReportSettings>`. The CLI command can either: (a) create a new `IReadinessReportService` instance with overridden settings, or (b) the simpler approach: the service's `GenerateAsync` already accepts `cycleCount` as override -- but thresholds are read from `IOptions<ReportSettings>`. The pragmatic approach is to parse the `--threshold` string in the command handler and call `GenerateAsync` with the cycle count -- for threshold overrides, since the service reads from `IOptions<ReportSettings>`, the command handler should temporarily update the options values. **However**, modifying `IOptions<T>` at runtime is not idiomatic. The cleanest approach per architecture patterns: accept the `--threshold` flag but note that threshold configuration is done via `appsettings.json` or `ReportSettings`. If the --threshold flag is provided, parse it, log a warning that runtime threshold override is not yet supported or implement it as an additional overload parameter on `GenerateAsync`.
- **DESIGN DECISION NEEDED:** The epics specify `[Option("threshold")] string? thresholdConfig = null` but `IReadinessReportService.GenerateAsync` doesn't accept threshold overrides as parameters. Two approaches:
  1. **Simple:** Add `int? successThreshold = null, int? warningThreshold = null` parameters to `GenerateAsync` -- service uses these when provided, falls back to `IOptions<ReportSettings>` when null
  2. **Current:** Leave `GenerateAsync` as-is and don't implement threshold override (defer)
  - **Recommended: Approach 1** -- extend `GenerateAsync` with optional threshold parameters. This is a backward-compatible change (all existing callers pass no threshold). Matches the PRD requirement (FR18).

**Story 4.3 (Error Comparison Report & CLI Integration) -- DONE:**
- Established the CLI command pattern for standalone report commands
- `compare-errors` / `ce` is the closest existing command to what we're building
- `RunCompareErrorsAsync` method structure: try/catch with exit codes 0/1
- Pattern: call service → log result → return exit code
- **Use `RunCompareErrorsAsync` as the direct template for `RunReadinessReportAsync`**
- [Source: CommandHandler.cs:145-177]

### Git Intelligence

**Recent commits (latest first):**
- `71f7520` Stories 4.3 & 5.1: Reporting services and CLI integration (#15)
- `7402d78` Story 4.2: Error fingerprinting and comparison engine (#14)
- `5af6c0a` Story 4.1: Result persistence and credential sanitization (#13)
- `5fc24b1` Story 3.2: Exit code standardization and CLI config overrides (#12)
- `a39e4ac` Story 3.1: CLI entity selection flag (#11)

**Key patterns from git history:**
- All CLI commands follow the same pattern: parse args, call service, handle exceptions, return exit code
- `CommandHandler` constructor grows with each new service dependency
- Tests in `ExitCodeTests` and `CompareErrorsCommandTests` construct `CommandHandler` directly with mocked services -- **constructor change will require updates**
- Each story adds services, registers in DI, writes tests, verifies build
- `compare-errors` / `ce` command in commit `71f7520` is the closest pattern reference

### Project Structure Notes

**Files to MODIFY:**
```
Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs
  -> Add IReadinessReportService field and constructor parameter
  -> Add RunReadinessReportAsync command method

Dynamics365ImportData/Dynamics365ImportData/Reporting/IReadinessReportService.cs
  -> Add optional threshold parameters to GenerateAsync signature

Dynamics365ImportData/Dynamics365ImportData/Reporting/ReadinessReportService.cs
  -> Update GenerateAsync to accept and use optional threshold overrides

Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Pipeline/ExitCodeTests.cs
  -> Update CreateCommandHandler to include IReadinessReportService mock

Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Comparison/CompareErrorsCommandTests.cs
  -> Update constructor to include IReadinessReportService mock in CommandHandler creation
```

**Files to CREATE:**
```
Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Reporting/ReadinessReportCommandTests.cs
  -> Tests for the readiness-report CLI command
```

**Files NOT to modify:**
- `Program.cs` -- `IReadinessReportService` already registered in DI, no new registrations needed
- `appsettings.json` -- `ReportSettings` section already present
- `Settings/ReportSettings.cs` -- unchanged
- `Reporting/Models/*` -- all model classes unchanged
- Existing unit tests for `ReadinessReportService` -- unchanged
- `Persistence/` -- unchanged
- `Sanitization/` -- unchanged
- `Fingerprinting/` -- unchanged
- `Comparison/` -- unchanged (except test files for constructor update)

### Architecture Compliance

**Folder structure:** No new folders needed. New command method goes in existing `CommandHandler.cs`. New tests go in existing `Integration/Reporting/` folder.

**DI lifetime rules:** `IReadinessReportService` is already registered as Transient -- no DI changes needed.

**CommandHandler pattern:** Follow existing pattern -- thin adapter method (~15-25 lines) that validates args, calls service, maps results to exit codes.

**Constructor injection:** `CommandHandler` already takes 6 dependencies. Adding `IReadinessReportService` as 7th is consistent with the pattern (each new service adds a constructor parameter).

### Library & Framework Requirements

- **Cocona 2.2.0** (already installed) -- `[Command]`, `[Option]` attributes for CLI
- **No new NuGet packages required**

### Technical Implementation Details

**Command method signature:**
```csharp
[Command("readiness-report", Aliases = new[] { "rr" }, Description = "Generate migration readiness report across multiple cycles")]
public async Task<int> RunReadinessReportAsync(
    [Option("cycles")] int cycles = 5,
    [Option("threshold")] string? thresholdConfig = null,
    [Option("output")] string? outputPath = null,
    CancellationToken cancellationToken = default)
```

**Threshold parsing logic:**
```csharp
// Format: "success:N,warning:M" (e.g., "success:0,warning:10")
// Both parts optional: "success:0" or "warning:10" are valid
// Invalid format -> exit code 2
private static (int? successThreshold, int? warningThreshold) ParseThresholdConfig(string thresholdConfig)
{
    int? success = null, warning = null;
    foreach (var part in thresholdConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var kv = part.Split(':', 2);
        if (kv.Length != 2) throw new FormatException($"Invalid threshold format: '{part}'. Expected 'key:value'.");
        switch (kv[0].Trim().ToLowerInvariant())
        {
            case "success": success = int.Parse(kv[1].Trim()); break;
            case "warning": warning = int.Parse(kv[1].Trim()); break;
            default: throw new FormatException($"Unknown threshold key: '{kv[0]}'. Valid keys: success, warning.");
        }
    }
    return (success, warning);
}
```

**Extended `IReadinessReportService.GenerateAsync` signature:**
```csharp
Task<string?> GenerateAsync(
    int? cycleCount = null,
    string? outputPath = null,
    int? successThreshold = null,
    int? warningThreshold = null,
    CancellationToken cancellationToken = default);
```

**Exit code mapping:**
```
Report generated successfully (path returned) → exit code 0
Service returns null (no cycles found)         → exit code 1
cycles <= 0                                     → exit code 2
Invalid threshold format                        → exit code 2
OperationCanceledException                      → exit code 1
General Exception                               → exit code 1
```

**Existing constructor injection pattern (current state):**
```csharp
public CommandHandler(
    IMigrationPipelineService pipelineService,
    SourceQueryCollection queries,
    IMigrationResultRepository resultRepository,
    IErrorComparisonService comparisonService,
    IErrorComparisonReportService reportService,
    ILogger<CommandHandler> logger)
```

**New constructor (adds `IReadinessReportService`):**
```csharp
public CommandHandler(
    IMigrationPipelineService pipelineService,
    SourceQueryCollection queries,
    IMigrationResultRepository resultRepository,
    IErrorComparisonService comparisonService,
    IErrorComparisonReportService reportService,
    IReadinessReportService readinessReportService,
    ILogger<CommandHandler> logger)
```

### Testing Requirements

**Test patterns:** Follow `{Method}_{Scenario}_{ExpectedResult}` naming. Shouldly assertions. Arrange/Act/Assert with comment markers. Follow `CompareErrorsCommandTests` pattern for integration tests that construct a real `CommandHandler`.

**Existing test infrastructure changes:**
- `ExitCodeTests.CreateCommandHandler()` currently creates `CommandHandler` with 6 parameters -- needs 7th `IReadinessReportService` mock
- `CompareErrorsCommandTests` constructor creates `CommandHandler` directly -- needs same update
- Both updates are mechanical: add `Substitute.For<IReadinessReportService>()` to the constructor call

**New test class `ReadinessReportCommandTests`:**
- Should follow `CompareErrorsCommandTests` structure: `IDisposable`, temp directories, `SourceQueryCollection` from temp files
- Mock `IReadinessReportService` to return file paths or null
- Test all exit code scenarios
- Test parameter passing (cycles, output, threshold)
- **All 206 existing tests must continue to pass**

**Mock setup pattern:**
```csharp
var mockReadinessService = Substitute.For<IReadinessReportService>();
mockReadinessService.GenerateAsync(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult<string?>("/path/to/report.md"));
```

### Critical Guardrails

1. **DO NOT modify `Program.cs`** -- `IReadinessReportService` is already registered, `ReportSettings` is already bound. No DI changes needed
2. **DO NOT modify `appsettings.json`** -- `ReportSettings` section already present with correct defaults
3. **DO NOT modify existing `ReadinessReportService` tests** -- unit tests in `Unit/Reporting/ReadinessReportServiceTests.cs` should remain unchanged
4. **DO update existing integration tests** that construct `CommandHandler` -- they need the new constructor parameter
5. **DO follow the `compare-errors` command pattern exactly** -- it's the closest existing code
6. **DO validate `cycles` argument** before calling service -- cycles <= 0 is exit code 2
7. **DO parse `--threshold` string carefully** with clear error messages for invalid format
8. **DO use Serilog structured message templates** for all logging -- NOT string interpolation
9. **DO propagate `CancellationToken`** through all async calls
10. **DO extend `IReadinessReportService.GenerateAsync`** with optional threshold parameters -- backward-compatible change
11. **DO update `ReadinessReportService.GenerateAsync`** to use threshold overrides when provided, falling back to `IOptions<ReportSettings>` when null
12. **DO keep the command method thin** -- parse/validate args → call service → map result to exit code

### Anti-Patterns to Avoid

- DO NOT use `Console.WriteLine` for output -- use Serilog `_logger.LogInformation`
- DO NOT use string interpolation in Serilog messages -- use structured templates
- DO NOT modify `IOptions<ReportSettings>` at runtime to inject threshold overrides -- pass as parameters instead
- DO NOT create a new service instance manually -- use DI-injected service
- DO NOT add new DI registrations in `Program.cs` -- everything is already wired
- DO NOT catch exceptions without logging them
- DO NOT add interactive prompts (FR24)
- DO NOT use `Task.Run()` for async wrapping -- use native async

### Performance Considerations

- This is a CLI adapter layer -- performance is dominated by `ReadinessReportService.GenerateAsync` which was already validated at < 1 second in Story 5.1
- Threshold parsing is negligible overhead
- No additional I/O beyond what the service already performs

### References

- [Source: architecture.md#ADR-5: Report Generation Architecture]
- [Source: architecture.md#ADR-8: Exit Code Contract]
- [Source: architecture.md#DI Registration Pattern]
- [Source: architecture.md#Folder & Namespace Organization]
- [Source: architecture.md#Serilog Message Template Pattern]
- [Source: architecture.md#Test Naming & Style Pattern]
- [Source: epics.md#Story 5.2: Readiness Report CLI Command]
- [Source: prd.md#FR13-FR18: Readiness reporting]
- [Source: prd.md#FR24: Fully unattended execution]
- [Source: 5-1-readiness-report-generation.md (service layer, test patterns, ReportSettings)]
- [Source: 4-3-error-comparison-report-and-cli-integration.md (compare-errors CLI command pattern)]
- [Source: CommandHandler.cs:145-177 (RunCompareErrorsAsync -- closest command pattern)]
- [Source: CommandHandler.cs:14-36 (constructor injection pattern)]
- [Source: Reporting/IReadinessReportService.cs (service interface to extend)]
- [Source: Reporting/ReadinessReportService.cs (service implementation to update)]
- [Source: Settings/ReportSettings.cs (threshold configuration POCO)]
- [Source: Integration/Pipeline/ExitCodeTests.cs:79-91 (CreateCommandHandler pattern to update)]
- [Source: Integration/Comparison/CompareErrorsCommandTests.cs (test pattern for CLI commands)]
- [Source: Program.cs:77-78 (existing DI registration for IReadinessReportService)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.5 (claude-opus-4-5-20251101)

### Debug Log References

### Completion Notes List

- Implemented `readiness-report` / `rr` CLI command as thin adapter in CommandHandler following `compare-errors` pattern
- Extended `IReadinessReportService.GenerateAsync` with optional `successThreshold` and `warningThreshold` parameters (backward-compatible)
- Updated `ReadinessReportService.BuildReport` to accept settings override for threshold parameters
- Added `ParseThresholdConfig` static helper for parsing `"success:N,warning:M"` format strings
- Validates cycles > 0 (exit code 2), threshold format (exit code 2), and handles cancellation/exceptions (exit code 1)
- Updated `ExitCodeTests` and `CompareErrorsCommandTests` constructors to include `IReadinessReportService` mock
- Created 12 new integration tests covering all exit code scenarios, parameter passing, and threshold parsing
- All 218 tests pass (206 existing + 12 new), zero build errors
- **Code review fixes:** Replaced `int.Parse` with `int.TryParse` + `CultureInfo.InvariantCulture` for threshold parsing with descriptive error messages (H1). Added 3 tests for partial thresholds and non-numeric values replacing 1 redundant test (M1, M2, M4). Removed dead code path in `BuildReport` method signature (M3). All 220 tests pass (206 existing + 14 new).

### Change Log

- 2026-02-02: Story 5.2 implementation complete - readiness-report CLI command with threshold override support
- 2026-02-02: Code review fixes applied - culture-invariant threshold parsing, additional test coverage, dead code cleanup

### File List

**Modified:**
- Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs
- Dynamics365ImportData/Dynamics365ImportData/Reporting/IReadinessReportService.cs
- Dynamics365ImportData/Dynamics365ImportData/Reporting/ReadinessReportService.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Pipeline/ExitCodeTests.cs
- Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Comparison/CompareErrorsCommandTests.cs

**Created:**
- Dynamics365ImportData/Dynamics365ImportData.Tests/Integration/Reporting/ReadinessReportCommandTests.cs
