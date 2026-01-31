---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - prd.md
  - product-brief-d365fo-data-migration-2026-01-31.md
workflowType: 'architecture'
lastStep: 8
status: 'complete'
completedAt: '2026-02-01'
project_name: 'd365fo-data-migration'
user_name: 'Jerome'
date: '2026-01-31'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**
36 functional requirements across 8 categories:

| Category | FRs | Architectural Impact |
|---|---|---|
| Entity Selection & Execution Control | FR1-FR5 | CLI argument parsing, dependency graph subsetting, entity name validation |
| Migration Result Persistence | FR6-FR8 | New data layer -- structured storage, cycle identification, file I/O |
| Error Comparison & Analysis | FR9-FR12 | Diff engine, error fingerprinting/normalization, classification logic, markdown generation |
| Readiness Reporting | FR13-FR18 | Multi-cycle aggregation, trend calculation, configurable thresholds |
| CLI Interface & Configuration | FR19-FR24 | Argument precedence, runtime overrides, exit codes, unattended execution |
| Platform Modernization | FR25-FR27 | .NET 10 migration, behavioral equivalence verification |
| Test Coverage | FR28-FR32 | xUnit project structure, test isolation, mock strategies for external dependencies |
| Documentation | FR33-FR36 | Content deliverables -- no architectural impact |

**Non-Functional Requirements:**
12 NFRs shaping architectural decisions:

- **Security (NFR1-4):** Credential exclusion from all output paths. Specific threat: D365FO API error responses may embed connection URIs, tenant IDs, or token fragments -- result persistence must sanitize error details before writing. .NET User Secrets / environment variable support required.
- **Reliability (NFR5-9):** Entity-level fault isolation (one failure doesn't kill the run). Atomic/recoverable result writes. Graceful degradation when historical data is missing (first cycle, corrupted files).
- **Performance (NFR10-12):** Report generation in seconds. Negligible persistence overhead. Selective runs proportionally faster -- no unnecessary initialization of unselected entities.

**Scale & Complexity:**

- Primary domain: CLI / Data Integration / REST API
- Complexity level: Medium
- Architectural components: 6-8 confirmed (pipeline core + observability/control layers) -- **validated against codebase**

### Architectural Core Insight

The tool's fundamental architecture is a **pipeline**: `SQL extraction → XML transformation → ZIP packaging → D365FO API import`. All Phase 2-3 features are **observability** (result persistence, error comparison, readiness reporting) and **control** (selective entity execution) layers wrapped around this existing pipeline nucleus. Architecture decisions should preserve the pipeline and layer new capabilities around it without invasive restructuring.

### Critical Architectural Decisions Identified

5 ADRs required (to be resolved in subsequent steps):

1. **Result Persistence Format** -- Foundation for Phases 2-3. JSON files behind repository abstraction recommended (start simple, evolve if needed).
2. **Testability Strategy** -- How to introduce tests without breaking production code. **Unblocked** -- DI + interfaces already in place.
3. **.NET 10 Migration Sequencing** -- Pre-mortem reveals characterization tests should precede upgrade, contradicting PRD ordering. Needs resolution.
4. **CLI Argument Extension** -- How to add flags without breaking existing scripts. **Unblocked** -- Cocona framework handles natively.
5. **Report Generation Architecture** -- Inline vs. post-hoc report generation.

### Technical Constraints & Dependencies

**Known constraints:**
- Existing codebase is production-deployed -- all changes must preserve backward compatibility
- Solo developer -- architecture must favor simplicity; thin abstractions only where they prevent rework
- .NET ecosystem -- constrained to .NET patterns, NuGet packages, `appsettings.json` configuration
- D365FO DMF REST API -- external dependency with fixed interface; Azure AD authentication
- SQL Server -- source data dependency; extraction queries already authored per-entity
- Serilog -- existing logging infrastructure
- No external runtime dependencies for unit tests (FR32)

**Codebase structure -- RESOLVED:**
Codebase examined. Single-project solution with full DI, interface abstractions at key boundaries, Cocona CLI framework, Options pattern configuration, and streaming data pipeline. See Technology Stack Evaluation for full details.

### Cross-Cutting Concerns Identified

1. **Result Persistence** -- Foundation for error comparison and readiness reporting. Touches every command's execution path. Must be designed for Phase 3 queries even if Phase 2 implementation is simple.
2. **Error Fingerprinting** -- D365FO error messages contain run-specific data (timestamps, record IDs, batch IDs). Without normalization, error comparison will classify every error as "new." This is a hidden complexity risk requiring dedicated design.
3. **Credential Sanitization** -- NFR1 requires a defined boundary between raw API responses and persisted/reported data. D365FO error responses are the primary leak vector.
4. **CLI Argument Architecture** -- New flags must integrate with existing command-verb structure without breaking automation scripts.
5. **Testability Retrofit** -- Introducing tests into existing code. DI and interfaces already in place; focus on adding test project and mocking at existing boundaries.
6. **Phase 1 Sequencing** -- Pre-mortem analysis reveals characterization tests should precede .NET 10 upgrade to catch regressions. This contradicts the PRD ordering and needs architectural resolution.
7. **Exit Code Contract** -- FR23 requires consistent exit codes across all commands including new features.
8. **Unattended Execution** -- FR24 prohibits interactive prompts. All error handling must be silent-but-logged.

### Phase Asymmetry

Phase 1 (Hardening/Modernization) and Phases 2-3 (Feature Development) are architecturally different:
- **Phase 1** = infrastructure decisions: test framework, upgrade strategy, project structure
- **Phases 2-3** = feature modeling decisions: data persistence schema, comparison algorithms, report templates

The architecture document should treat these as distinct architectural domains rather than a uniform feature list.

## Technology Stack Evaluation

### Existing Technology Stack

| Layer | Technology | Current Version | Target Version | .NET 10 Compatible |
|---|---|---|---|---|
| **Runtime** | .NET | 8.0 | **10.0** (10.0.2) | Target upgrade |
| **CLI Framework** | Cocona | 2.2.0 | 2.2.0 | Yes (.NET Standard 2.0) |
| **DI Container** | Microsoft.Extensions.DependencyInjection | 8.0.0 | 10.x | Yes |
| **HTTP Client** | Microsoft.Extensions.Http + MSAL | 8.0.0 / 4.59.0 | 10.x / latest | Yes |
| **JSON** | System.Text.Json | 8.0.1 | 10.x (native) | Yes |
| **Logging** | Serilog (7 sinks + 4 enrichers) | 3.1.1 | 3.x | Yes |
| **Azure** | Azure.Storage.Blobs | 12.19.1 | latest | Yes |
| **OData** | Microsoft.OData.Client | 7.20.0 | latest | Yes |
| **SQL** | System.Data.SqlClient | 4.8.6 | **Microsoft.Data.SqlClient** | Recommended migration |
| **Config** | appsettings.json + User Secrets | 8.0.0 | 10.x | Yes |
| **Code Analysis** | Roslynator + SerilogAnalyzer | 4.9.0 / 0.15.0 | latest | Yes |

### .NET 10 Upgrade Compatibility Assessment

**.NET 10 status:** GA since November 11, 2025. Latest patch: 10.0.2 (January 2026). LTS support through November 2028. .NET 8 EOL: November 10, 2026.

**Risk areas for upgrade:**
- `System.Text.Json` -- behavioral changes between major versions (serialization defaults, naming policies). Codebase uses `PropertyNameCaseInsensitive = true` and `[DataMember]` + `[JsonPropertyOrder]` dual attributes. Must verify serialization output is identical post-upgrade.
- `System.Data.SqlClient` -- legacy package receiving security fixes only. Recommend migration to `Microsoft.Data.SqlClient` (actively maintained replacement) during .NET 10 upgrade.
- `Microsoft.Extensions.*` packages -- all need version bump to 10.x. API surface is stable; breaking changes unlikely.

**Low-risk areas:**
- Serilog ecosystem -- mature, broad .NET version support
- Azure.Storage.Blobs -- actively maintained, .NET 10 support confirmed
- Cocona 2.2.0 -- .NET Standard 2.0 target ensures backward compatibility

### New Technology Additions Required

| Addition | Package | Version | Purpose | Phase |
|---|---|---|---|---|
| **Test Framework** | xunit.v3 | 3.2.2 | Unit and integration testing | Phase 1 |
| **Assertions** | Shouldly | 4.3.0 | Readable assertion syntax (per PRD) | Phase 1 |
| **Mocking** | NSubstitute or Moq | Latest | Mock interfaces for isolated unit tests | Phase 1 |
| **Test Project** | New .csproj | -- | Separate test project in solution | Phase 1 |

### Codebase Architecture Assessment

**Solution structure:** Single solution (`Dynamics365ImportData.sln`) with single project (`Dynamics365ImportData.csproj`).

**Project layout:**
```
Dynamics365ImportData/
├── Azure/                    → Azure Blob Storage client
├── DependencySorting/        → Dependency graph + topological sort
├── Erp/                      → D365FO API client (OAuth2, OData)
│   └── DataManagementDefinitionGroups/  → DMF API operations
├── Services/                 → SQL-to-XML conversion service
├── Settings/                 → Configuration POCOs (Options pattern)
├── XmlOutput/                → Output factories (file, package, D365)
├── Definition/               → Entity definitions (SQL, manifests)
├── CommandHandler.cs         → CLI commands (Cocona [Command] attributes)
├── Program.cs                → Entry point, DI setup, Serilog config
└── appsettings.json          → Configuration
```

**Existing patterns that support architecture goals:**

1. **Constructor injection everywhere** -- Services, command handlers, output factories all use DI. No refactoring needed for testability at service boundaries.
2. **Interface abstractions at key boundaries:**
   - `IDynamics365FinanceDataManagementGroups` -- D365FO API client (mockable)
   - `IXmlOutputFactory` / `IXmlOutputPart` -- Output pipeline (mockable)
3. **Factory pattern for output modes** -- `XmlFileOutputFactory`, `XmlPackageFileOutputFactory`, `XmlD365FnoOutputFactory`. Clean separation of output strategies.
4. **Options pattern for configuration** -- `IOptions<T>` throughout. Test-friendly.
5. **Cocona command handler** -- `[Command]` attributes with aliases. Adding `--entities` parameter is a single attribute addition.
6. **Streaming data pipeline** -- SQL → DataReader → XmlWriter → output. Memory-efficient, no large in-memory collections.

**Gaps identified:**

1. **No test project** -- Solution has a single .csproj. Need to add test project and restructure solution.
2. **`SourceQueryCollection` complexity** -- Parses config, builds dependency graph, AND performs topological sorting. Hardest class to test in isolation; may benefit from responsibility extraction.
3. **Service locator in `CommandHandler`** -- Uses `IServiceProvider.GetRequiredService<T>()` in command methods rather than pure constructor injection. Slightly harder to test.
4. **No result persistence layer** -- Entirely new capability needed (greenfield design within brownfield project).
5. **`System.Data.SqlClient` is legacy** -- Should migrate to `Microsoft.Data.SqlClient` during upgrade.

### Updated ADR Status

| ADR | Status | Key Finding |
|---|---|---|
| ADR-1: Result Persistence Format | Ready to decide | No existing persistence -- greenfield design within brownfield project |
| ADR-2: Testability Strategy | **Unblocked** | DI + interfaces already in place. Focus on test project + mocking at existing boundaries. Minimal restructuring. |
| ADR-3: .NET 10 Migration Sequencing | Ready to decide | All dependencies compatible. `System.Data.SqlClient` → `Microsoft.Data.SqlClient` recommended. |
| ADR-4: CLI Argument Extension | **Unblocked** | Cocona framework handles natively. `--entities` is a parameter attribute addition. |
| ADR-5: Report Generation Architecture | Ready to decide | Output factory pattern provides model for report generation. |

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
- ADR-1: Result Persistence Format -- JSON files behind repository interface
- ADR-2: Testability Strategy -- Unit + integration + snapshot tests with NSubstitute
- ADR-3: .NET 10 Migration Sequencing -- Hybrid: characterization tests → upgrade → SqlClient migration → full suite
- ADR-9: Pipeline Service Extraction -- Extract `IMigrationPipelineService` from `CommandHandler`

**Important Decisions (Shape Architecture):**
- ADR-4: CLI Argument Extension -- Cocona `--entities` parameter with comma-separated names
- ADR-5: Report Generation Architecture -- Hybrid: auto-generate after run + standalone commands

**Cross-Cutting Decisions:**
- ADR-6: Error Fingerprinting -- Entity-scoped normalization and hashing
- ADR-7: Credential Sanitization -- Regex-based sanitizer at persistence boundary + CI audit
- ADR-8: Exit Code Contract -- 0/1/2 convention across all commands

### ADR-1: Result Persistence Format

**Decision:** JSON files behind a thin `IMigrationResultRepository` interface.

**Rationale:** Start with the simplest viable storage (JSON files) while isolating consumers from the storage mechanism. If Phase 3 readiness reporting requires aggregation performance beyond what file-loading can provide, swap the implementation to SQLite without changing any consumer code. The abstraction cost is 1 interface + 1 class -- negligible for a solo developer, significant insurance against rework.

**Specification:**
- Storage location: `{OutputDirectory}/results/` subfolder
- File naming: `cycle-{yyyy-MM-ddTHHmmss}.json` (one file per migration run)
- Schema per file:
  ```json
  {
    "cycleId": "cycle-2026-01-31T220000",
    "timestamp": "2026-01-31T22:00:00Z",
    "command": "import-d365",
    "entitiesRequested": ["all"],
    "results": [
      {
        "entityName": "CustCustomerV3Entity",
        "status": "Success|Failed|Warning|Skipped",
        "recordCount": 15000,
        "durationMs": 45000,
        "errors": [
          {
            "message": "sanitized error message",
            "fingerprint": "abc123...",
            "category": "DataQuality|Technical|Dependency"
          }
        ]
      }
    ],
    "summary": {
      "totalEntities": 100,
      "succeeded": 95,
      "failed": 3,
      "warnings": 2,
      "skipped": 0,
      "totalDurationMs": 3600000
    }
  }
  ```
- **Implementation note:** Per-entity `durationMs` requires adding `Stopwatch` instrumentation around the per-entity processing loop in the pipeline service. This is new instrumentation work not present in the current codebase.
- Interface: `IMigrationResultRepository` with methods:
  - `SaveCycleResultAsync(CycleResult result)`
  - `GetCycleResultAsync(string cycleId)`
  - `GetLatestCycleResultsAsync(int count)`
  - `ListCycleIdsAsync()`
- Implementation: `JsonFileMigrationResultRepository`
- Write behavior: Atomic write via temp file + rename (NFR8)

**Affects:** FR6-FR8, FR9-FR12, FR13-FR18, NFR8

### ADR-2: Testability Strategy

**Decision:** Three-level testing -- unit tests for algorithms, integration tests for pipeline flow, snapshot tests for output validation. NSubstitute for mocking.

**Rationale:** The codebase already has DI and interfaces at key boundaries, making it naturally testable. Unit tests provide fast, precise coverage of algorithms. Integration tests verify command handler → pipeline wiring. Snapshot/golden-file tests validate that XML output and package structure match D365FO expectations -- this is not a mocking scenario but a correctness assertion against known-good output.

**Specification:**
- New project: `Dynamics365ImportData.Tests.csproj` targeting `net10.0`
- Packages: `xunit.v3` 3.2.2, `Shouldly` 4.3.0, `NSubstitute` latest
- Project structure:
  ```
  Dynamics365ImportData.Tests/
  ├── Unit/
  │   ├── DependencySorting/
  │   │   ├── TopologicalSortTests.cs
  │   │   └── DependencyGraphTests.cs
  │   ├── XmlOutput/
  │   │   └── XmlGenerationTests.cs
  │   └── Services/
  │       └── SqlToXmlServiceTests.cs
  ├── Integration/
  │   ├── Pipeline/
  │   │   └── PipelineServiceTests.cs
  │   └── Packaging/
  │       └── ZipPackageTests.cs
  ├── Snapshot/
  │   ├── GoldenFiles/            → Expected XML output files
  │   └── XmlOutputSnapshotTests.cs
  ├── Audit/
  │   └── CredentialLeakTests.cs  → CI audit for credential patterns
  └── TestHelpers/
      ├── TestFixtures.cs
      └── MockBuilders.cs
  ```
- Mock boundaries:
  - `IDynamics365FinanceDataManagementGroups` -- mock D365 API calls
  - `IXmlOutputFactory` / `IXmlOutputPart` -- mock output pipeline
  - `IOptions<T>` -- inject test configuration
  - SQL connection -- use in-memory data or test doubles
- Snapshot tests: Generate XML for known entity data, compare against golden files in `Snapshot/GoldenFiles/`
- CI audit test: Read sample result JSON, assert no credential patterns (`Bearer`, `sig=`, `Server=...;Database=...`) found
- Test execution: `dotnet test` with no external dependencies (FR32)

**Affects:** FR28-FR32, NFR1-NFR2

### ADR-3: .NET 10 Migration Sequencing

**Decision:** Hybrid approach -- characterization tests first, then .NET 10 upgrade, then SqlClient migration (as distinct step), then full test suite.

**Rationale:** `Microsoft.Data.SqlClient` migration is a behavioral change (e.g., `Encrypt=true` default, TLS behavior, connection string format differences) and must be treated separately from the .NET 10 TFM upgrade. Each migration step gets its own characterization test validation pass.

**Specification -- Phase 1 Implementation Order:**
1. **Add test project** to solution (infrastructure setup)
2. **Extract `IMigrationPipelineService`** from `CommandHandler` (ADR-9 -- enables testability)
3. **Write characterization tests** against .NET 8 baseline (15-20 tests):
   - Topological sort: linear chain, diamond dependency, cycle detection (3+ tests)
   - XML generation: single entity, special characters, null fields, golden-file snapshots (4+ tests)
   - JSON serialization: API payload format verification (2+ tests)
   - Package ZIP: manifest structure, header validation (2+ tests)
   - Configuration binding: `IOptions<T>` expected values (2+ tests)
   - SQL connection behavior: connection string handling, error response format (2+ tests)
4. **Upgrade TFM** to `net10.0`, bump all `Microsoft.Extensions.*` packages to 10.x
5. **Run characterization tests** -- verify identical output
6. **Migrate `System.Data.SqlClient` → `Microsoft.Data.SqlClient`** as distinct step
   - Add characterization tests specific to SqlClient behavior before swap
   - Verify `Encrypt=true` default behavior against target SQL Server
   - Run all characterization tests again post-migration
7. **Expand test suite** to full coverage per FR28-FR32
8. **Documentation** -- document final Phase 1 state

**Affects:** FR25-FR27, FR28-FR32

### ADR-4: CLI Argument Extension

**Decision:** Cocona native parameter attributes for all new CLI arguments.

**Rationale:** Cocona already handles command parsing via `[Command]` attributes. New parameters are added as method parameters with `[Option]` attributes. Backward compatible by design (all new parameters are optional with defaults preserving current behavior).

**Specification:**
- `--entities` parameter: `[Option("entities")] string? entities = null`
  - Comma-separated entity names: `--entities CustCustomerV3Entity,VendVendorV2Entity`
  - Applies to all three commands (`export-file`, `export-package`, `import-d365`)
  - When omitted: all configured entities processed (current behavior)
  - Validation: entity names checked against configured entity list before execution (FR5). Case-insensitive matching using `StringComparer.OrdinalIgnoreCase`. Fail with exit code 2 and clear message listing valid entity names.
  - Dependency behavior: resolve dependencies within selected subset only. Selecting entity A does not auto-include its dependency B.
- `--compare` flag: `[Option("compare")] bool compare = false`
  - When true: auto-generate error comparison report after migration run
- Report output overrides: `[Option("output")] string? outputPath = null`

**Affects:** FR1-FR5, FR19-FR24

### ADR-5: Report Generation Architecture

**Decision:** Hybrid -- auto-generate after run (opt-in) + standalone commands.

**Rationale:** The Functional Consultant needs error comparison immediately after a migration run (Journey 3). The Project Director needs readiness reports on demand (Journey 4). Standalone commands allow both use cases.

**Specification:**
- New Cocona commands:
  - `compare-errors` / `ce` -- Generate error comparison report from two most recent cycles
    - `[Option("cycle")] string? cycleId = null` -- compare against specific cycle instead of latest
    - Output: markdown file in output directory
  - `readiness-report` / `rr` -- Generate readiness report across multiple cycles
    - `[Option("cycles")] int cycles = 5` -- number of cycles to include
    - `[Option("threshold")] string? thresholdConfig = null` -- override threshold settings
    - Output: markdown file in configurable output path
- `--compare` flag on existing commands: triggers error comparison automatically after run
- Report generation services:
  - `IErrorComparisonService` -- compares two cycle results, classifies new vs. carry-over
  - `IReadinessReportService` -- aggregates N cycles, calculates trends, generates markdown
- Both services consume `IMigrationResultRepository` for data access

**Affects:** FR9-FR18, FR21-FR22

### ADR-9: Pipeline Service Extraction

**Decision:** Extract migration pipeline orchestration from `CommandHandler` into `IMigrationPipelineService`.

**Rationale:** `CommandHandler` currently resolves services via `IServiceProvider` in each command method and orchestrates the pipeline inline. This creates a testability bottleneck and couples CLI argument handling to pipeline logic. Extracting a pipeline service creates a clean seam where Phase 2 features attach: result persistence hooks into the pipeline output, selective entity execution filters the pipeline input, and the pipeline becomes independently testable with constructor-injected dependencies.

**Specification:**
```csharp
public interface IMigrationPipelineService
{
    Task<CycleResult> ExecuteAsync(
        PipelineMode mode,        // File, Package, D365
        string[]? entityFilter,   // null = all configured entities
        CancellationToken cancellationToken);
}

public enum PipelineMode { File, Package, D365 }
```

- `CommandHandler` becomes a thin CLI adapter (~10 lines per command): parse args → call `IMigrationPipelineService.ExecuteAsync()` → return exit code
- Pipeline service owns: entity filtering → dependency resolution → execution → result capture
- Constructor-injected dependencies: `SourceQueryCollection`, output factories, `IMigrationResultRepository`, `IResultSanitizer`, logger
- `CycleResult` is the same type written to result persistence (ADR-1)

**Affects:** ADR-1, ADR-2, ADR-4, FR1-FR5

### Cross-Cutting Decisions

#### ADR-6: Error Fingerprinting

**Decision:** Entity-scoped normalization with stable hashing for cross-cycle comparison.

**Rationale:** D365FO error messages contain run-specific data. Without normalization, the same logical error appears "new" every cycle. Entity-scoping prevents false matches where two different entities produce similar error text.

**Specification:**
- Fingerprint formula: `SHA256(entityName + "|" + normalizedMessage)` truncated to 16 hex chars
- Normalization pipeline:
  1. Strip GUIDs (regex: `[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-...`)
  2. Strip timestamps (ISO 8601 and common datetime formats)
  3. Strip numeric record IDs (sequences of 5+ digits)
  4. Collapse whitespace
  5. Lowercase
- Fingerprint stored alongside original (sanitized) error message in result JSON
- Classification: fingerprint exists in previous cycle = carry-over; not found = new

**Affects:** FR9-FR10

#### ADR-7: Credential Sanitization

**Decision:** Regex-based sanitizer at persistence boundary + CI audit test for defense in depth.

**Rationale:** D365FO API error responses may embed connection URIs, tenant IDs, client secrets, or token fragments. Single-layer regex sanitization is brittle; defense in depth catches what regex misses.

**Specification:**
- `IResultSanitizer` interface: `string Sanitize(string rawErrorMessage)`
- Patterns to strip:
  - Connection strings (`Server=...;Database=...`)
  - Azure AD tenant IDs and client IDs (GUID patterns in auth contexts)
  - Bearer tokens (`Bearer eyJ...`)
  - SAS tokens (URL parameters: `sig=`, `sv=`, `se=`)
  - Client secrets (configurable pattern)
- Replacement: `[REDACTED]`
- Applied in `IMigrationResultRepository.SaveCycleResultAsync()` before writing
- Defense in depth: CI audit test (`CredentialLeakTests.cs`) reads sample result JSON files and asserts zero matches against known credential patterns

**Affects:** NFR1-NFR2

#### ADR-8: Exit Code Contract

**Decision:** Three-tier exit code convention applied consistently across all commands.

| Exit Code | Meaning | When |
|---|---|---|
| `0` | Success | All entities succeeded / report generated successfully |
| `1` | Partial failure | One or more entities failed / report generated with warnings |
| `2` | Configuration error | Invalid arguments, missing config, validation failure -- didn't start |

**Affects:** FR23

### Decision Impact Analysis

**Implementation Sequence:**
1. Test project setup (ADR-2 infrastructure)
2. Pipeline service extraction (ADR-9)
3. Characterization tests on .NET 8 baseline -- 15-20 tests (ADR-3 step 3)
4. .NET 10 TFM upgrade + Microsoft.Extensions.* bump (ADR-3 step 4)
5. Run characterization tests -- verify identical output (ADR-3 step 5)
6. `System.Data.SqlClient` → `Microsoft.Data.SqlClient` migration with own test pass (ADR-3 step 6)
7. Full test suite expansion including snapshot tests (ADR-2 + ADR-3 step 7)
8. Documentation (Phase 1 complete)
9. Result persistence layer + credential sanitizer (ADR-1, ADR-7)
10. Error fingerprinting (ADR-6)
11. `--entities` CLI flag (ADR-4)
12. Error comparison service + `compare-errors` command (ADR-5)
13. Readiness report service + `readiness-report` command (ADR-5)
14. Exit code standardization across all commands (ADR-8)

**Cross-Component Dependencies:**
- ADR-9 (pipeline extraction) enables ADR-1 (persistence) and ADR-2 (testability)
- ADR-1 (persistence) blocks ADR-5 (reporting) and ADR-6 (fingerprinting)
- ADR-6 (fingerprinting) is consumed by ADR-5 error comparison service
- ADR-7 (sanitization) is consumed by ADR-1 persistence layer
- ADR-2 (test strategy) and ADR-3 (upgrade sequencing) are tightly coupled
- ADR-4 (CLI extension) depends on ADR-9 (pipeline service accepts entity filter)
- ADR-8 (exit codes) applies to ADR-4 and ADR-5

## Implementation Patterns & Consistency Rules

### Purpose

These patterns ensure that any AI agent implementing stories for d365fo-data-migration produces code consistent with the existing codebase and compatible with code produced by other agents. The brownfield context means most patterns are inherited from existing code -- new patterns are defined only where new capabilities introduce ambiguity.

### Inherited Patterns (Follow Existing Code)

These patterns are already established in the codebase. All agents MUST follow them:

| Pattern | Convention | Source |
|---|---|---|
| **Class naming** | PascalCase | C# standard, existing code |
| **Interface naming** | `I` prefix + PascalCase (`IXmlOutputFactory`) | Existing code |
| **Method naming** | PascalCase, async methods suffixed with `Async` | Existing code (`RunExportToFileAsync`) |
| **Local variables** | camelCase | C# standard |
| **Private fields** | camelCase with no prefix | Existing code |
| **Nullable annotations** | Enabled, respect `<Nullable>enable</Nullable>` | `.csproj` setting |
| **Async/await** | All I/O operations async, propagate `CancellationToken` through all async chains | Existing code |
| **DI registration** | Register in `Program.cs` using `builder.Services` | Existing pattern |
| **Configuration** | Options pattern with `IOptions<T>`, bound to `appsettings.json` sections | Existing code |
| **Logging** | Serilog with structured message templates (NOT string interpolation) | Existing code |
| **XML output** | `XmlWriter` with streaming writes, no in-memory DOM | Existing pattern |
| **HTTP calls** | `IHttpClientFactory` with typed clients | Existing pattern |

### New Patterns for Phase 2-3 Code

#### Folder & Namespace Organization

New code MUST follow the existing horizontal-slice folder structure. Each new capability gets its own folder at the project root level:

```
Dynamics365ImportData/
├── Azure/                    → (existing)
├── DependencySorting/        → (existing)
├── Erp/                      → (existing)
├── Pipeline/                 → NEW: IMigrationPipelineService, PipelineMode, CycleResult
├── Persistence/              → NEW: IMigrationResultRepository, JsonFileMigrationResultRepository
├── Sanitization/             → NEW: IResultSanitizer, RegexResultSanitizer
├── Fingerprinting/           → NEW: IErrorFingerprinter, ErrorFingerprinter
├── Comparison/               → NEW: IErrorComparisonService, ErrorComparisonService
├── Reporting/                → NEW: IReadinessReportService, ReadinessReportService
├── Services/                 → (existing)
├── Settings/                 → (existing) + new ReportSettings.cs, PersistenceSettings.cs
├── XmlOutput/                → (existing)
├── CommandHandler.cs         → (existing, simplified per ADR-9)
├── Program.cs                → (existing, extended with new DI registrations)
└── appsettings.json          → (existing, extended with new sections)
```

**Rule:** One interface + one implementation per file. Interface file named `I{ServiceName}.cs`, implementation named `{ServiceName}.cs`. Both in the same folder.

**Rule:** Namespace matches folder path: `Dynamics365ImportData.Persistence`, `Dynamics365ImportData.Comparison`, etc.

#### DI Registration Pattern

New services MUST follow these lifetime rules:

| Service Type | Lifetime | Rationale |
|---|---|---|
| `IMigrationPipelineService` | **Transient** | Stateful per-execution (holds entity filter, mode) |
| `IMigrationResultRepository` | **Singleton** | Stateless file I/O, thread-safe |
| `IResultSanitizer` | **Singleton** | Stateless regex operations |
| `IErrorFingerprinter` | **Singleton** | Stateless hashing |
| `IErrorComparisonService` | **Transient** | May hold per-comparison state |
| `IReadinessReportService` | **Transient** | May hold per-report state |

**Registration order in `Program.cs`:** Group by capability with comment headers:

```csharp
// Pipeline
builder.Services.AddTransient<IMigrationPipelineService, MigrationPipelineService>();

// Result Persistence
builder.Services.AddSingleton<IMigrationResultRepository, JsonFileMigrationResultRepository>();
builder.Services.AddSingleton<IResultSanitizer, RegexResultSanitizer>();

// Error Analysis
builder.Services.AddSingleton<IErrorFingerprinter, ErrorFingerprinter>();
builder.Services.AddTransient<IErrorComparisonService, ErrorComparisonService>();

// Reporting
builder.Services.AddTransient<IReadinessReportService, ReadinessReportService>();
```

#### JSON Serialization for Result Files

All result persistence JSON MUST use these `System.Text.Json` options:

```csharp
public static readonly JsonSerializerOptions ResultJsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};
```

**Rationale:** Human-readable (indented), consistent casing (camelCase), clean nulls, readable enums ("success" not "0").

**Rule:** Define these options as a static readonly field in a shared `JsonDefaults` class in the `Persistence/` folder. All result serialization/deserialization MUST use this shared instance.

#### Error Handling Pattern

New services MUST follow this error handling hierarchy:

1. **Entity-level processing:** Catch exceptions per-entity, log with Serilog, record in `CycleResult` as failed entity. Do NOT terminate the pipeline.
   ```csharp
   catch (Exception ex)
   {
       _logger.Error(ex, "Entity {EntityName} failed during {Phase}", entity.EntityName, phase);
       result.Status = EntityStatus.Failed;
       result.Errors.Add(new EntityError
       {
           Message = _sanitizer.Sanitize(ex.Message),
           Category = ErrorCategory.Technical
       });
   }
   ```

2. **Service-level operations (persistence, reports):** Let exceptions propagate to the command handler. Log at the command handler level. Return appropriate exit code.

3. **Configuration validation:** Fail fast with exit code 2. Validate before any processing begins.

**Rule:** NEVER use `catch (Exception) { }` (swallow silently). Always log. Always record in results if entity-level.

#### Serilog Message Template Pattern

All log messages MUST use Serilog structured templates with named properties:

```csharp
// CORRECT - structured template with named properties
_logger.Information("Processing entity {EntityName} ({Index}/{Total})", entity.Name, i, count);
_logger.Error(ex, "Entity {EntityName} failed during {Phase}", entity.Name, "import");

// WRONG - string interpolation (loses structured logging)
_logger.Information($"Processing entity {entity.Name} ({i}/{count})");

// WRONG - positional parameters (loses named context)
_logger.Information("Processing entity {0} ({1}/{2})", entity.Name, i, count);
```

**Log levels for new code:**
- `Debug` -- Detailed per-entity processing steps
- `Information` -- Cycle start/complete, entity start/complete, report generation
- `Warning` -- Entity completed with warnings, missing historical data for comparison
- `Error` -- Entity failed, persistence write failed
- `Fatal` -- Configuration invalid, unable to start

#### Configuration Section Pattern

New `appsettings.json` sections MUST follow the existing naming and binding pattern:

```json
{
  "PersistenceSettings": {
    "ResultsDirectory": "",
    "MaxCyclesToRetain": 50
  },
  "ReportSettings": {
    "DefaultCycleRange": 5,
    "SuccessThreshold": 0,
    "WarningThreshold": 5,
    "OutputDirectory": ""
  }
}
```

**Rules:**
- Section names: PascalCase, matching the settings class name
- Empty strings for path defaults (resolved at runtime to output directory)
- Corresponding `{SectionName}Settings.cs` POCO in `Settings/` folder
- Bound via `.AddOptions<T>().Bind(builder.Configuration.GetSection(...))` in `Program.cs`

#### Test Naming & Style Pattern

All tests MUST follow this naming convention:

```csharp
// Class naming: {ClassUnderTest}Tests.cs
public class TopologicalSortTests
{
    // Method naming: {Method}_{Scenario}_{ExpectedResult}
    [Fact]
    public void Sort_LinearDependencyChain_ReturnsCorrectOrder()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.AddDependency("B", "A");
        graph.AddDependency("C", "B");

        // Act
        var sorted = TopologicalSort.Sort(graph);

        // Assert (Shouldly)
        sorted.ShouldBe(new[] { "A", "B", "C" });
    }
}
```

**Rules:**
- Arrange/Act/Assert structure with comment markers
- Shouldly assertions exclusively (no `Assert.Equal`)
- One assertion concept per test (multiple `ShouldBe` calls OK if testing the same concept)
- Test data defined inline, not in external files (except golden files for snapshots)
- Golden files stored in `Snapshot/GoldenFiles/` with descriptive names

#### Markdown Report Format Pattern

Both error comparison and readiness reports MUST follow this structure:

```markdown
# {Report Title}

**Generated:** {ISO 8601 timestamp}
**Cycle:** {cycle ID}

## Summary

{Key metrics table}

## Details

{Per-entity breakdown}

---
*Generated by d365fo-data-migration v{version}*
```

**Rules:**
- Reports are self-contained markdown files (no external CSS/assets)
- Tables for summary data, lists for details
- Entity names in code formatting: `` `CustCustomerV3Entity` ``
- Status indicators: `pass` / `FAIL` / `warn` (lowercase for pass/warn, uppercase FAIL for visibility)

### Enforcement Guidelines

**All AI Agents MUST:**
1. Read the existing codebase patterns before writing new code -- match the style of adjacent files
2. Run `dotnet build` with zero warnings before considering code complete
3. Register all new services in `Program.cs` with appropriate lifetimes
4. Propagate `CancellationToken` through all async call chains
5. Use Serilog structured message templates, never string interpolation for logging
6. Apply `IResultSanitizer` before any error data reaches persistence or reports
7. Follow the folder structure defined above -- no ad-hoc folder creation
8. Use the shared `JsonDefaults.ResultJsonOptions` for all result serialization

**Anti-Patterns to Reject:**
- Static helper classes with static methods (use DI-injected services)
- `Task.Run()` for async wrapping (use native async all the way)
- `Console.WriteLine()` for output (use Serilog)
- Hardcoded file paths (use `IOptions<T>` configuration)
- Catching exceptions without logging them
- Creating new `HttpClient` instances directly (use `IHttpClientFactory`)

## Project Structure & Boundaries

### Complete Project Directory Structure

```
Dynamics365ImportData/
├── Dynamics365ImportData.sln
│
├── Dynamics365ImportData/                    [Main project]
│   ├── Dynamics365ImportData.csproj
│   ├── Program.cs                           → Entry point, DI setup, Serilog config
│   ├── CommandHandler.cs                    → Thin CLI adapter (Cocona commands)
│   ├── appsettings.json                     → Configuration (existing + new sections)
│   │
│   ├── Azure/                               → [EXISTING] Azure Blob Storage
│   │   └── AzureContainer.cs
│   │
│   ├── DependencySorting/                   → [EXISTING] Dependency graph + topological sort
│   │   ├── DependencyGraph.cs
│   │   ├── OrderedProcess.cs
│   │   ├── Resource.cs
│   │   ├── SourceQueryCollection.cs         → Main orchestrator
│   │   ├── SourceQueryItem.cs
│   │   ├── TopologicalSort.cs
│   │   └── IEnumerableExtensions.cs
│   │
│   ├── Erp/                                 → [EXISTING] D365FO integration
│   │   ├── DataManagementDefinitionGroups/
│   │   │   ├── BlobDefinition.cs
│   │   │   ├── Dynamics365FinanceDataManagementGroups.cs
│   │   │   ├── ExecutionIdRequest.cs
│   │   │   ├── ExecutionStatus.cs
│   │   │   ├── IDynamics365FinanceDataManagementGroups.cs
│   │   │   ├── ImportFromPackageRequest.cs
│   │   │   └── UniqueFileNameRequest.cs
│   │   ├── Dynamics365Container.cs
│   │   ├── Dynamics365FnoClient.cs          → Base HTTP client with OAuth2
│   │   └── ODataResponse.cs
│   │
│   ├── Pipeline/                            → [NEW - ADR-9] Pipeline orchestration
│   │   ├── IMigrationPipelineService.cs     → Pipeline interface
│   │   ├── MigrationPipelineService.cs      → Pipeline implementation
│   │   ├── PipelineMode.cs                  → Enum: File, Package, D365
│   │   └── CycleResult.cs                   → Result model (shared with persistence)
│   │
│   ├── Persistence/                         → [NEW - ADR-1] Result persistence
│   │   ├── IMigrationResultRepository.cs    → Repository interface
│   │   ├── JsonFileMigrationResultRepository.cs → JSON file implementation
│   │   ├── JsonDefaults.cs                  → Shared JsonSerializerOptions
│   │   └── Models/
│   │       ├── EntityResult.cs              → Per-entity result model
│   │       ├── EntityError.cs               → Error with fingerprint
│   │       ├── EntityStatus.cs              → Enum: Success, Failed, Warning, Skipped
│   │       ├── ErrorCategory.cs             → Enum: DataQuality, Technical, Dependency
│   │       └── CycleSummary.cs              → Aggregate metrics
│   │
│   ├── Sanitization/                        → [NEW - ADR-7] Credential sanitization
│   │   ├── IResultSanitizer.cs
│   │   └── RegexResultSanitizer.cs
│   │
│   ├── Fingerprinting/                      → [NEW - ADR-6] Error fingerprinting
│   │   ├── IErrorFingerprinter.cs
│   │   └── ErrorFingerprinter.cs
│   │
│   ├── Comparison/                          → [NEW - ADR-5] Error comparison
│   │   ├── IErrorComparisonService.cs
│   │   ├── ErrorComparisonService.cs
│   │   └── Models/
│   │       ├── ComparisonResult.cs
│   │       └── ErrorClassification.cs       → Enum: New, CarryOver
│   │
│   ├── Reporting/                           → [NEW - ADR-5] Readiness reporting
│   │   ├── IReadinessReportService.cs
│   │   ├── ReadinessReportService.cs
│   │   └── Models/
│   │       ├── ReadinessReport.cs
│   │       ├── EntityTrend.cs
│   │       └── TrendDirection.cs            → Enum: Improving, Stable, Degrading
│   │
│   ├── Services/                            → [EXISTING] Business logic services
│   │   └── SqlToXmlService.cs
│   │
│   ├── Settings/                            → [EXISTING + EXTENDED] Configuration POCOs
│   │   ├── DestinationSettings.cs           → (existing)
│   │   ├── Dynamics365Settings.cs           → (existing)
│   │   ├── ProcessSettings.cs               → (existing)
│   │   ├── QuerySettings.cs                 → (existing)
│   │   ├── SourceSettings.cs                → (existing)
│   │   ├── PersistenceSettings.cs           → [NEW] Result storage config
│   │   └── ReportSettings.cs                → [NEW] Report generation config
│   │
│   ├── XmlOutput/                           → [EXISTING] Output factories
│   │   ├── IXmlOutputFactory.cs
│   │   ├── IXmlOutputPart.cs
│   │   ├── XmlD365FnoOutputFactory.cs
│   │   ├── XmlD365FnoOutputPart.cs
│   │   ├── XmlFileOutputFactory.cs
│   │   ├── XmlOutputPart.cs
│   │   ├── XmlPackageFileOutputFactory.cs
│   │   ├── XmlPackageOutputFactoryBase.cs
│   │   └── XmlZipOutputPart.cs
│   │
│   └── Definition/                          → [EXISTING] Entity definitions
│       └── {EntityName}/
│           ├── {ENTITYNAME}.sql
│           ├── Manifest.xml
│           └── PackageHeader.xml
│
├── Dynamics365ImportData.Tests/             [NEW - Test project]
│   ├── Dynamics365ImportData.Tests.csproj
│   ├── Unit/
│   │   ├── DependencySorting/
│   │   │   ├── TopologicalSortTests.cs      → FR28
│   │   │   └── DependencyGraphTests.cs      → FR28
│   │   ├── XmlOutput/
│   │   │   └── XmlGenerationTests.cs        → FR29
│   │   ├── Pipeline/
│   │   │   └── EntityFilteringTests.cs      → FR3, FR5
│   │   ├── Fingerprinting/
│   │   │   └── ErrorFingerprinterTests.cs   → FR9-FR10
│   │   ├── Sanitization/
│   │   │   └── RegexResultSanitizerTests.cs → NFR1-NFR2
│   │   └── Services/
│   │       └── SqlToXmlServiceTests.cs      → FR29
│   ├── Integration/
│   │   ├── Pipeline/
│   │   │   └── PipelineServiceTests.cs      → FR31
│   │   ├── Packaging/
│   │   │   └── ZipPackageTests.cs           → FR30
│   │   ├── Persistence/
│   │   │   └── JsonFileRepositoryTests.cs   → FR6-FR8
│   │   └── Comparison/
│   │       └── ErrorComparisonTests.cs      → FR9-FR12
│   ├── Snapshot/
│   │   ├── GoldenFiles/
│   │   │   ├── expected-entity-output.xml
│   │   │   ├── expected-manifest.xml
│   │   │   └── expected-package-header.xml
│   │   └── XmlOutputSnapshotTests.cs        → FR29
│   ├── Audit/
│   │   └── CredentialLeakTests.cs           → NFR1
│   └── TestHelpers/
│       ├── TestFixtures.cs
│       └── MockBuilders.cs
│
└── docs/                                    → [NEW - Phase 1]
    ├── setup-guide.md                       → FR33
    ├── configuration-reference.md           → FR34
    ├── entity-authoring-guide.md            → FR35
    └── developer-guide.md                   → FR36
```

### Architectural Boundaries

#### Service Boundaries

```
┌─────────────────────────────────────────────────────────┐
│                    CommandHandler                         │
│  (CLI adapter - parses args, delegates, returns exit code)│
└──────────────────────┬──────────────────────────────────┘
                       │ calls
┌──────────────────────▼──────────────────────────────────┐
│              IMigrationPipelineService                    │
│  (orchestrator - filtering, execution, result capture)    │
├──────────────┬────────────────┬──────────────────────────┤
│              │                │                           │
│  SourceQuery │  IXmlOutput    │  IDynamics365Finance      │
│  Collection  │  Factory       │  DataManagementGroups     │
│  (dependency │  (output       │  (D365 API client)        │
│   resolution)│   strategy)    │                           │
└──────────────┴────────────────┴──────────────────────────┘
                       │ produces
┌──────────────────────▼──────────────────────────────────┐
│                    CycleResult                            │
└──────────────────────┬──────────────────────────────────┘
                       │ consumed by
         ┌─────────────┼─────────────┐
         ▼             ▼             ▼
┌────────────┐ ┌──────────────┐ ┌──────────────┐
│ IMigration │ │ IError       │ │ IReadiness   │
│ Result     │ │ Comparison   │ │ Report       │
│ Repository │ │ Service      │ │ Service      │
│ (persist)  │ │ (diff)       │ │ (aggregate)  │
└─────┬──────┘ └──────┬───────┘ └──────┬───────┘
      │               │                │
      ▼               ▼                ▼
  JSON files     MD report        MD report
  (results/)     (comparison)     (readiness)
```

**Boundary rules:**
- `CommandHandler` ONLY calls `IMigrationPipelineService` and report services
- `IMigrationPipelineService` owns the execution flow: entity filtering → dependency resolution → execution → result capture
- Report services ONLY access data through `IMigrationResultRepository`
- `IResultSanitizer` is called by `IMigrationResultRepository` before writing
- `IErrorFingerprinter` is called by `IMigrationPipelineService` when building `CycleResult`

#### Data Boundaries

| Data Type | Boundary | Access Pattern |
|---|---|---|
| Source SQL data | SQL Server → `SqlToXmlService` | Read-only, streaming via `DataReader` |
| Entity definitions | File system → `SourceQueryCollection` | Read-only, loaded at startup |
| Configuration | `appsettings.json` → `IOptions<T>` | Read-only, injected via DI |
| Migration output (XML/ZIP) | Pipeline → file system / Azure Blob | Write-only during execution |
| Cycle results (JSON) | Pipeline → `IMigrationResultRepository` → file system | Write after execution, read for reports |
| Reports (Markdown) | Report services → file system | Write-only |
| Credentials | `IOptions<Dynamics365Settings>` → `Dynamics365FnoClient` | In-memory only, never persisted |

#### External Integration Points

| Integration | Protocol | Authentication | Direction |
|---|---|---|---|
| SQL Server | ADO.NET (`Microsoft.Data.SqlClient`) | Connection string (Windows auth or SQL auth) | Inbound (read) |
| D365FO DMF API | HTTPS REST/OData | Azure AD OAuth2 client credentials (MSAL) | Outbound (write) |
| Azure Blob Storage | HTTPS | SAS token (from D365FO API) | Outbound (write) |

### Requirements to Structure Mapping

#### Phase 1: Hardening & Modernization

| Requirement | Files/Directories |
|---|---|
| FR25-FR27: .NET 10 upgrade | `*.csproj` (TFM change), `Program.cs` (package updates) |
| FR28: Dependency sort tests | `Tests/Unit/DependencySorting/` |
| FR29: XML generation tests | `Tests/Unit/XmlOutput/`, `Tests/Snapshot/` |
| FR30: Package creation tests | `Tests/Integration/Packaging/` |
| FR31: API integration tests | `Tests/Integration/Pipeline/` |
| FR32: No external dependencies | `Tests/TestHelpers/` (mock builders) |
| FR33: Setup guide | `docs/setup-guide.md` |
| FR34: Config reference | `docs/configuration-reference.md` |
| FR35: Entity authoring guide | `docs/entity-authoring-guide.md` |
| FR36: Developer docs | `docs/developer-guide.md` |
| ADR-9: Pipeline extraction | `Pipeline/` (new folder) |

#### Phase 2: Operational Improvements

| Requirement | Files/Directories |
|---|---|
| FR1-FR5: Entity selection | `CommandHandler.cs`, `Pipeline/MigrationPipelineService.cs` |
| FR6-FR8: Result persistence | `Persistence/`, `Settings/PersistenceSettings.cs` |
| FR9-FR12: Error comparison | `Comparison/`, `Fingerprinting/`, `Sanitization/` |
| NFR1-NFR2: Credential safety | `Sanitization/`, `Tests/Audit/` |
| NFR8: Atomic writes | `Persistence/JsonFileMigrationResultRepository.cs` |

#### Phase 3: Visibility & Reporting

| Requirement | Files/Directories |
|---|---|
| FR13-FR18: Readiness reporting | `Reporting/`, `Settings/ReportSettings.cs` |
| FR19-FR24: CLI & config | `CommandHandler.cs`, `appsettings.json` |
| FR23: Exit codes | `CommandHandler.cs`, `Pipeline/MigrationPipelineService.cs` |

### Data Flow

**Migration execution flow:**
```
appsettings.json → IOptions<T> → SourceQueryCollection
    → DependencyGraph → TopologicalSort → ordered entity list
    → (entity filter applied if --entities specified)
    → per entity: SqlToXmlService (SQL → XML via streaming)
    → IXmlOutputFactory (file / package / D365 mode)
    → CycleResult captured with per-entity status + sanitized errors
    → IMigrationResultRepository writes JSON to results/
    → (if --compare) IErrorComparisonService reads latest 2 cycles → MD report
```

**Report generation flow:**
```
compare-errors command:
    → IMigrationResultRepository.GetLatestCycleResultsAsync(2)
    → IErrorComparisonService.CompareAsync(current, previous)
    → classify errors by fingerprint (new vs carry-over)
    → write markdown report

readiness-report command:
    → IMigrationResultRepository.GetLatestCycleResultsAsync(N)
    → IReadinessReportService.GenerateAsync(cycles)
    → calculate trends, entity status, convergence
    → write markdown report
```

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:**
All 9 ADRs form a consistent dependency chain with no contradictions. ADR-9 (pipeline extraction) creates the testable seam that ADR-2 (testability) and ADR-1 (persistence) attach to. ADR-6 (fingerprinting) → ADR-7 (sanitization) → ADR-1 (persistence) form a clean data processing pipeline. ADR-4 (CLI extension) feeds entity filter into ADR-9; ADR-8 (exit codes) applies to the return path. ADR-3 and ADR-2 are tightly coupled and correctly sequenced. Technology compatibility verified: Cocona 2.2.0 (.NET Standard 2.0) + .NET 10 + xUnit v3 3.2.2 + Shouldly 4.3.0 + NSubstitute — no version conflicts.

**Pattern Consistency:**
Inherited patterns (12 conventions) and new patterns (8 areas) do not contradict each other. Folder naming → namespace convention is consistent. DI lifetime assignments align with service statefulness analysis. Error handling hierarchy directly supports NFR5. JSON serialization centralized in `JsonDefaults.ResultJsonOptions` prevents drift. Test naming convention is unambiguous.

**Structure Alignment:**
Every ADR has corresponding folder(s) in the project structure. Test project mirrors main project with appropriate categorization. Settings/ extension pattern matches existing codebase. docs/ mapped to FR33-FR36.

### Requirements Coverage Validation ✅

**Functional Requirements Coverage: 36/36**

| Category | FRs | Architectural Support | Status |
|---|---|---|---|
| Entity Selection & Execution Control | FR1-FR5 | ADR-4, ADR-9 (pipeline entityFilter), case-insensitive matching | COVERED |
| Migration Result Persistence | FR6-FR8 | ADR-1 (JSON files, `IMigrationResultRepository`, cycle ID) | COVERED |
| Error Comparison & Analysis | FR9-FR12 | ADR-5 (`compare-errors`), ADR-6 (fingerprinting), `IErrorComparisonService` | COVERED |
| Readiness Reporting | FR13-FR18 | ADR-5 (`readiness-report`), `IReadinessReportService`, `ReportSettings` | COVERED |
| CLI Interface & Configuration | FR19-FR24 | ADR-4, ADR-8 (exit codes), ADR-5 (report flags), no interactive prompts | COVERED |
| Platform Modernization | FR25-FR27 | ADR-3 (sequenced upgrade), SqlClient migration step | COVERED |
| Test Coverage | FR28-FR32 | ADR-2 (three-level testing), test project structure | COVERED |
| Documentation | FR33-FR36 | `docs/` folder with 4 defined documents | COVERED |

**Non-Functional Requirements Coverage: 12/12**

| NFR | Requirement | Architectural Support | Status |
|---|---|---|---|
| NFR1-NFR2 | Credential exclusion | ADR-7 (regex sanitizer), CI audit test | COVERED |
| NFR3-NFR4 | Secret management | Existing .NET User Secrets capability | COVERED |
| NFR5 | Entity fault isolation | Error handling pattern: per-entity catch, continue | COVERED |
| NFR6 | Failure detail | `EntityError` model (message + fingerprint + category) | COVERED |
| NFR7 | Completion summary | `CycleSummary` in `CycleResult` | COVERED |
| NFR8 | Atomic writes | ADR-1: temp file + rename | COVERED |
| NFR9 | Graceful degradation | Design supports it; see Gap #1 for specifics | COVERED |
| NFR10 | Report speed | Lightweight JSON reads + markdown generation | COVERED |
| NFR11 | Negligible persistence overhead | Single JSON write per cycle | COVERED |
| NFR12 | Selective run speed | Entity filter applied before processing | COVERED |

### Implementation Readiness Validation ✅

**Decision Completeness:**
All 9 ADRs documented with rationale, specification, code examples, and affected requirements. 14-step implementation sequence defined. Cross-component dependency graph explicitly documented. Technology versions pinned.

**Structure Completeness:**
Full directory tree with `[EXISTING]` and `[NEW]` markers. Every new interface and implementation class named. Test structure mapped to requirements. Models/ subfolders with specific class names.

**Pattern Completeness:**
12 inherited + 8 new patterns with code examples. 8 enforcement guidelines. 6 anti-patterns rejected. Markdown report format standardized.

### Gap Analysis Results

**Critical Gaps: None**

**Important Gaps:**

1. **NFR9 graceful degradation specifics** -- `compare-errors` with no previous cycle should generate a report stating "First cycle -- no comparison available" and return exit code 0. `readiness-report` with fewer cycles than requested should report on available cycles with a note.

2. **`SourceQueryCollection` decomposition** -- Complex class (config parsing + dependency graph + topological sorting) identified but no refactoring ADR. Acceptable: test as-is in Phase 1, consider extraction in Phase 2 if entity filtering logic becomes entangled.

3. **`durationMs` instrumentation boundary** -- Per-entity timing requires `Stopwatch` but exact boundaries unspecified. Recommendation: start immediately before `SqlToXmlService` call, stop after output factory completes. Wall-clock time for entity's full pipeline pass.

**Nice-to-Have Gaps:**

1. No CI/CD pipeline configuration defined. `dotnet test` is CI-friendly by design; pipeline definition outside architecture scope.
2. No package versioning strategy (SemVer, assembly versioning). Low risk for single deployment artifact.
3. No `--verbose`/`--quiet` CLI flags. Existing Serilog MinimumLevel via `appsettings.json` is sufficient.

### Validation Issues Addressed

The 3 important gaps are documented with inline recommendations. None are blocking. They are implementation-level details resolvable during story execution.

### Architecture Completeness Checklist

**✅ Requirements Analysis**

- [x] Project context thoroughly analyzed (enhanced with 5 elicitation methods)
- [x] Scale and complexity assessed (medium, 6-8 components)
- [x] Technical constraints identified (brownfield, solo dev, .NET ecosystem)
- [x] Cross-cutting concerns mapped (8 concerns identified and resolved)

**✅ Architectural Decisions**

- [x] Critical decisions documented with versions (9 ADRs)
- [x] Technology stack fully specified (existing + new additions)
- [x] Integration patterns defined (SQL, D365 API, Azure Blob)
- [x] Performance considerations addressed (NFR10-12)

**✅ Implementation Patterns**

- [x] Naming conventions established (inherited + new)
- [x] Structure patterns defined (folder, namespace, DI registration)
- [x] Communication patterns specified (Serilog templates, exit codes)
- [x] Process patterns documented (error handling, JSON serialization, config)

**✅ Project Structure**

- [x] Complete directory structure defined (existing + new)
- [x] Component boundaries established (service boundary diagram)
- [x] Integration points mapped (3 external, internal data flows)
- [x] Requirements to structure mapping complete (Phase 1, 2, 3)

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION

**Confidence Level:** High -- brownfield context with working production code, comprehensive ADR coverage (9 decisions), validated requirements mapping (36 FRs + 12 NFRs all covered).

**Key Strengths:**
- Brownfield advantage: existing DI + interfaces + factory patterns mean minimal refactoring
- Phase-sequenced implementation: each phase delivers standalone value
- ADR-9 (pipeline extraction) is the keystone decision unlocking testability, persistence, and selective execution
- Characterization-tests-first approach (ADR-3) reduces .NET 10 upgrade risk
- Defense-in-depth credential protection (sanitizer + CI audit)

**Areas for Future Enhancement:**
- `SourceQueryCollection` decomposition if entity filtering logic grows complex
- SQLite swap for `IMigrationResultRepository` if Phase 3 aggregation queries prove slow on JSON files
- CI/CD pipeline definition once test suite is established

### Implementation Handoff

**AI Agent Guidelines:**

- Follow all architectural decisions exactly as documented in the 9 ADRs
- Use implementation patterns consistently across all components
- Respect project structure and boundaries -- `[EXISTING]` folders preserve structure, new code goes in `[NEW]` folders
- Refer to this document for all architectural questions
- When in doubt, match the style of adjacent existing code

**First Implementation Priority:**
Phase 1 Step 1: Add test project (`Dynamics365ImportData.Tests.csproj`) to solution, install xUnit v3 + Shouldly + NSubstitute, verify `dotnet test` runs with a single placeholder test. Then proceed to ADR-9 pipeline extraction.
