---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - prd.md
  - architecture.md
---

# d365fo-data-migration - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for d365fo-data-migration, decomposing the requirements from the PRD and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

- FR1: Migration Engineer can specify a subset of entities to process via CLI argument, without modifying configuration files
- FR2: Migration Engineer can use entity selection with any execution mode (export-file, export-package, import-d365)
- FR3: The system resolves dependencies within the selected entity subset, ensuring correct import order
- FR4: When no entity selection is specified, the system processes all configured entities (existing behavior preserved)
- FR5: The system validates specified entity names against the configured entity list before execution begins
- FR6: The system captures structured migration results after each run, including entity name, status, error details, and timestamp
- FR7: The system stores migration results in a persistent format alongside existing output, accessible for comparison across cycles
- FR8: Migration Engineer can identify which cycle a set of results belongs to
- FR9: The system compares current cycle results against the previous cycle's results automatically
- FR10: The system classifies errors as new (first appearance this cycle) or carry-over (present in previous cycle)
- FR11: The system generates a markdown error comparison report written to the output directory
- FR12: Functional Consultant can review the error comparison report to identify only what changed since last cycle
- FR13: The system aggregates migration results across multiple cycles into a single readiness report
- FR14: The readiness report displays error trends across cycles (total errors per cycle over time)
- FR15: The readiness report displays entity-level status classification (success, warning, failure)
- FR16: The readiness report shows convergence direction (improving, stable, degrading)
- FR17: The system generates the readiness report in markdown format
- FR18: Project Director can configure readiness report parameters (cycle range, thresholds) via settings
- FR19: Migration Engineer can override configuration file settings via CLI arguments at runtime
- FR20: CLI arguments take precedence over appsettings.json values when both are specified
- FR21: Migration Engineer can enable or disable report generation per run via CLI flags
- FR22: Migration Engineer can override output paths for reports via CLI arguments
- FR23: The system returns meaningful exit codes reflecting success or failure for script integration
- FR24: All features operate without interactive prompts, supporting fully unattended execution
- FR25: The system builds and runs on .NET 10 runtime and SDK
- FR26: The system produces identical output on .NET 10 as on .NET 8 for all existing configurations and entity definitions
- FR27: All existing CLI commands, configuration options, and execution modes continue to function without modification after the upgrade
- FR28: The test suite validates dependency topological sorting produces correct entity ordering for known dependency graphs
- FR29: The test suite validates XML generation produces D365FO-compatible output for known entity data
- FR30: The test suite validates package creation produces valid ZIP archives with correct manifest and header files
- FR31: The test suite validates API integration handles authentication, upload, and import operations correctly
- FR32: The test suite can be executed via dotnet test with no external dependencies required for unit-level tests
- FR33: A setup guide enables a new user to install, configure, and run the tool from scratch
- FR34: A configuration reference documents all appsettings.json options with descriptions, types, defaults, and examples
- FR35: An entity definition authoring guide explains how to create SQL queries, manifest files, and package headers for new entities
- FR36: Developer documentation explains the codebase architecture, key components, data flow, and extension points

### NonFunctional Requirements

- NFR1: Azure AD credentials and SQL connection strings must never be written to log files, console output, or result persistence files
- NFR2: Result persistence files must not contain raw source data -- only entity names, statuses, error messages, and metadata
- NFR3: Sensitive configuration values (client secrets, connection strings) must be supported via .NET User Secrets or environment variables, not only plain-text configuration files
- NFR4: The tool must not introduce new credential storage mechanisms -- continue using .NET's established secret management patterns
- NFR5: A failure in one entity's extraction, transformation, or import must not terminate the entire migration run -- remaining entities continue processing
- NFR6: All entity-level failures must be captured in structured results with sufficient detail to diagnose the root cause without re-running
- NFR7: The tool must produce a clear summary at completion indicating total entities processed, succeeded, and failed
- NFR8: Result persistence writes must be atomic or recoverable -- a crash during result writing must not corrupt previously persisted cycle data
- NFR9: The error comparison and readiness report features must handle missing or incomplete historical data gracefully
- NFR10: Report generation must complete within seconds, not minutes -- report generation must not meaningfully extend the total migration runtime
- NFR11: Result persistence must not degrade existing migration throughput -- structured result capture should add negligible overhead
- NFR12: Selective entity runs must start processing faster than a full run proportional to the number of entities selected

### Additional Requirements

- ADR-9: Extract IMigrationPipelineService from CommandHandler before any other work -- this is the keystone architectural change enabling testability, result persistence, and selective execution
- ADR-3: Characterization tests MUST precede .NET 10 upgrade to establish behavioral baseline (architecture overrides PRD ordering)
- ADR-3: System.Data.SqlClient must be migrated to Microsoft.Data.SqlClient as a distinct step during .NET 10 upgrade
- ADR-2: Test stack is xUnit v3 3.2.2 + Shouldly 4.3.0 + NSubstitute (latest)
- ADR-1: Result persistence uses JSON files behind IMigrationResultRepository interface with atomic temp-file+rename writes
- ADR-6: Error fingerprinting uses SHA256(entityName + "|" + normalizedMessage) truncated to 16 hex chars
- ADR-7: Credential sanitization via IResultSanitizer at persistence boundary + CI audit test for defense in depth
- ADR-8: Three-tier exit code contract: 0 (success), 1 (partial failure), 2 (configuration error)
- ADR-5: Hybrid report generation: auto-generate after run (opt-in via --compare flag) + standalone commands (compare-errors, readiness-report)
- ADR-4: Cocona native parameter attributes for --entities (comma-separated, case-insensitive matching)
- Phase sequencing: Phase 1 (hardening) delivers standalone value; Phase 2/3 features layer on top
- New folder structure required: Pipeline/, Persistence/, Sanitization/, Fingerprinting/, Comparison/, Reporting/
- 14-step implementation sequence defined in architecture document

### FR Coverage Map

| FR | Epic | Description |
|----|------|-------------|
| FR1 | Epic 3 | CLI entity subset selection |
| FR2 | Epic 3 | Entity selection across all execution modes |
| FR3 | Epic 3 | Dependency resolution within selected subset |
| FR4 | Epic 3 | Default all-entities behavior preserved |
| FR5 | Epic 3 | Entity name validation before execution |
| FR6 | Epic 4 | Structured migration result capture |
| FR7 | Epic 4 | Persistent result storage alongside output |
| FR8 | Epic 4 | Cycle identification for results |
| FR9 | Epic 4 | Automatic cycle-over-cycle comparison |
| FR10 | Epic 4 | New vs. carry-over error classification |
| FR11 | Epic 4 | Markdown error comparison report |
| FR12 | Epic 4 | Error comparison review for changed errors |
| FR13 | Epic 5 | Multi-cycle result aggregation |
| FR14 | Epic 5 | Error trends across cycles |
| FR15 | Epic 5 | Entity-level status classification |
| FR16 | Epic 5 | Convergence direction tracking |
| FR17 | Epic 5 | Markdown readiness report generation |
| FR18 | Epic 5 | Configurable report parameters |
| FR19 | Epic 3 | CLI argument config overrides |
| FR20 | Epic 3 | CLI precedence over appsettings.json |
| FR21 | Epic 4 | Per-run report generation toggle |
| FR22 | Epic 4 | Report output path overrides |
| FR23 | Epic 3 | Meaningful exit codes |
| FR24 | Epic 3 | Fully unattended execution |
| FR25 | Epic 1 | .NET 10 build and run |
| FR26 | Epic 1 | Identical output on .NET 10 |
| FR27 | Epic 1 | Backward compatibility after upgrade |
| FR28 | Epic 1 | Dependency sorting test coverage |
| FR29 | Epic 1 | XML generation test coverage |
| FR30 | Epic 1 | Package creation test coverage |
| FR31 | Epic 1 | API integration test coverage |
| FR32 | Epic 1 | No external dependencies for unit tests |
| FR33 | Epic 2 | Setup guide |
| FR34 | Epic 2 | Configuration reference |
| FR35 | Epic 2 | Entity definition authoring guide |
| FR36 | Epic 2 | Developer documentation |

## Epic List

### Epic 1: Platform Hardening & Regression Safety Net
The Migration Engineer can trust that the tool runs on a modern, supported runtime with a comprehensive test suite catching regressions -- providing confidence to make future changes safely.
**FRs covered:** FR25, FR26, FR27, FR28, FR29, FR30, FR31, FR32
**NFRs addressed:** NFR5 (fault isolation tested), NFR1-NFR2 (credential audit test)
**ADRs:** ADR-2, ADR-3, ADR-9

### Epic 2: Documentation & Onboarding
A new team member can install, configure, run, and extend the tool using documentation alone -- no hand-holding required.
**FRs covered:** FR33, FR34, FR35, FR36

### Epic 3: Selective Entity Execution
The Migration Engineer can re-run a targeted subset of entities from the command line without editing configuration files -- saving hours and eliminating config-editing risk.
**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR19, FR20, FR23, FR24
**NFRs addressed:** NFR12 (selective run speed)
**ADRs:** ADR-4, ADR-8

### Epic 4: Migration Result Persistence & Error Comparison
The Functional Consultant can immediately see which errors are new vs. carry-overs after each migration cycle -- reducing triage from half a day to 30 minutes.
**FRs covered:** FR6, FR7, FR8, FR9, FR10, FR11, FR12, FR21, FR22
**NFRs addressed:** NFR1, NFR2, NFR5, NFR6, NFR7, NFR8, NFR9, NFR10, NFR11
**ADRs:** ADR-1, ADR-5 (compare-errors), ADR-6, ADR-7

### Epic 5: Readiness Reporting
The Project Director can open a single markdown file and assess migration readiness with trend data across cycles -- making evidence-based go/no-go decisions.
**FRs covered:** FR13, FR14, FR15, FR16, FR17, FR18
**NFRs addressed:** NFR9, NFR10
**ADRs:** ADR-5 (readiness-report)

## Epic 1: Platform Hardening & Regression Safety Net

The Migration Engineer can trust that the tool runs on a modern, supported runtime with a comprehensive test suite catching regressions -- providing confidence to make future changes safely.

### Story 1.1: Test Project Setup & Pipeline Service Extraction

As a Migration Engineer,
I want the codebase to have a test project and a clean pipeline service interface,
So that I have the foundation for regression testing and can safely modify the tool going forward.

**Acceptance Criteria:**

**Given** the existing single-project solution
**When** the test project is added
**Then** `Dynamics365ImportData.Tests.csproj` exists targeting `net8.0` with xUnit v3 3.2.2, Shouldly 4.3.0, and NSubstitute installed
**And** `dotnet test` runs successfully with a single placeholder test
**And** the solution file includes both projects

**Given** the existing `CommandHandler.cs` with inline pipeline orchestration
**When** the pipeline service is extracted per ADR-9
**Then** `IMigrationPipelineService` interface exists in `Pipeline/` folder with `ExecuteAsync(PipelineMode, string[]?, CancellationToken)` signature
**And** `MigrationPipelineService` implementation orchestrates entity filtering, dependency resolution, execution, and result capture
**And** `CommandHandler` is simplified to a thin CLI adapter (~10 lines per command): parse args, call pipeline service, return exit code
**And** `PipelineMode` enum (File, Package, D365) exists in `Pipeline/` folder
**And** all services are registered in `Program.cs` with correct lifetimes per architecture
**And** existing CLI behavior is preserved -- all three commands produce identical output

### Story 1.2: Characterization Tests on .NET 8 Baseline

As a Migration Engineer,
I want characterization tests that capture the current .NET 8 behavior,
So that I can detect any regressions when upgrading to .NET 10.

**Acceptance Criteria:**

**Given** the extracted pipeline service and test project
**When** characterization tests are written
**Then** topological sort tests verify correct ordering for: linear chain, diamond dependency, and cycle detection (3+ tests)
**And** XML generation tests verify output for: single entity, special characters, null fields, and golden-file snapshots (4+ tests)
**And** JSON serialization tests verify API payload format (2+ tests)
**And** package ZIP tests verify manifest structure and header validation (2+ tests)
**And** configuration binding tests verify `IOptions<T>` expected values (2+ tests)
**And** SQL connection behavior tests verify connection string handling and error response format (2+ tests)
**And** all tests pass with `dotnet test` on .NET 8
**And** golden files stored in `Snapshot/GoldenFiles/` with descriptive names
**And** no external dependencies required (SQL Server, D365FO environment) for unit tests (FR32)
**And** minimum 15 characterization tests total

### Story 1.3: .NET 10 Upgrade

As a Migration Engineer,
I want the tool upgraded to .NET 10,
So that I'm on a supported LTS runtime before .NET 8 reaches end-of-support.

**Acceptance Criteria:**

**Given** the .NET 8 baseline with passing characterization tests
**When** the target framework is changed to `net10.0`
**Then** both `.csproj` files target `net10.0`
**And** all `Microsoft.Extensions.*` packages are bumped to 10.x versions
**And** `dotnet build` completes with zero errors and zero warnings
**And** all characterization tests from Story 1.2 pass without modification
**And** existing CLI commands (`export-file`, `export-package`, `import-d365`) function identically (FR25, FR26, FR27)

### Story 1.4: SqlClient Migration

As a Migration Engineer,
I want `System.Data.SqlClient` replaced with `Microsoft.Data.SqlClient`,
So that the tool uses the actively maintained SQL client library with current security patches.

**Acceptance Criteria:**

**Given** the .NET 10 upgraded codebase with passing tests
**When** `System.Data.SqlClient` is replaced with `Microsoft.Data.SqlClient`
**Then** the `System.Data.SqlClient` NuGet package is removed
**And** `Microsoft.Data.SqlClient` NuGet package is added
**And** all `using System.Data.SqlClient` statements are updated to `using Microsoft.Data.SqlClient`
**And** `Encrypt=true` default behavior is verified against target SQL Server configuration
**And** connection string format is validated (backward compatible or documented changes)
**And** all characterization tests pass without modification
**And** `dotnet build` completes with zero warnings

### Story 1.5: Full Test Suite Expansion

As a Migration Engineer,
I want comprehensive test coverage beyond the characterization baseline,
So that I have a regression safety net for all future code changes.

**Acceptance Criteria:**

**Given** the .NET 10 codebase with SqlClient migration complete
**When** the test suite is expanded per ADR-2
**Then** dependency sorting tests cover: edge cases (empty graph, single node, disconnected subgraphs), large graphs, and deterministic ordering (FR28)
**And** XML generation tests cover: all entity field types, encoding edge cases, and D365FO format compliance (FR29)
**And** package creation tests cover: ZIP integrity, manifest completeness, package header correctness, multi-entity packages (FR30)
**And** API integration tests cover: OAuth2 token acquisition mock, upload request format, import status polling, error response handling (FR31)
**And** a credential leak audit test (`CredentialLeakTests.cs`) reads sample result data and asserts zero matches against known credential patterns (Bearer tokens, SAS tokens, connection strings) per ADR-7
**And** all tests run via `dotnet test` with no external dependencies (FR32)
**And** test structure follows ADR-2: `Unit/`, `Integration/`, `Snapshot/`, `Audit/`, `TestHelpers/`
**And** test naming follows `{Method}_{Scenario}_{ExpectedResult}` convention with Arrange/Act/Assert structure

## Epic 2: Documentation & Onboarding

A new team member can install, configure, run, and extend the tool using documentation alone -- no hand-holding required.

### Story 2.1: Setup Guide & Configuration Reference

As a new team member,
I want a setup guide and configuration reference,
So that I can install, configure, and run the tool from scratch without assistance.

**Acceptance Criteria:**

**Given** a new developer with no prior exposure to the tool
**When** they follow `docs/setup-guide.md`
**Then** the guide covers: prerequisites (.NET 10 SDK, SQL Server access, D365FO environment credentials), cloning the repository, building the solution, configuring `appsettings.json`, and running a first migration (FR33)
**And** the guide includes troubleshooting for common setup issues (connection failures, authentication errors)

**Given** a user needing to configure the tool
**When** they consult `docs/configuration-reference.md`
**Then** every `appsettings.json` option is documented with: description, data type, default value, and example (FR34)
**And** environment variable overrides and .NET User Secrets usage are documented (NFR3)
**And** CLI argument precedence over configuration file values is explained (FR20)

### Story 2.2: Entity Authoring Guide & Developer Documentation

As a new team member,
I want an entity authoring guide and developer documentation,
So that I can create new entity definitions and understand the codebase well enough to extend it.

**Acceptance Criteria:**

**Given** a consultant needing to add a new D365FO entity to the migration
**When** they follow `docs/entity-authoring-guide.md`
**Then** the guide explains: SQL extraction query structure, manifest XML format, package header XML format, dependency declaration, and entity naming conventions (FR35)
**And** the guide includes a complete worked example for adding a new entity from scratch

**Given** a developer needing to understand or modify the codebase
**When** they read `docs/developer-guide.md`
**Then** the guide covers: solution structure, key components and their responsibilities, data flow (SQL extraction → XML transformation → ZIP packaging → D365FO API import), DI registration patterns, pipeline service architecture, and extension points (FR36)
**And** the guide references the test suite as living documentation for expected behavior
**And** the guide documents the folder structure conventions for new code (per architecture patterns)

## Epic 3: Selective Entity Execution

The Migration Engineer can re-run a targeted subset of entities from the command line without editing configuration files -- saving hours and eliminating config-editing risk.

### Story 3.1: CLI Entity Selection Flag

As a Migration Engineer,
I want to specify which entities to process via a `--entities` CLI argument,
So that I can re-run targeted entities without editing `appsettings.json`.

**Acceptance Criteria:**

**Given** the existing CLI commands (export-file, export-package, import-d365)
**When** the `--entities` option is added per ADR-4
**Then** each command accepts `[Option("entities")] string? entities = null` parameter via Cocona
**And** the flag accepts comma-separated entity names (e.g., `--entities CustCustomerV3Entity,VendVendorV2Entity`) (FR1)
**And** the flag works identically across all three commands (FR2)
**And** when `--entities` is omitted, all configured entities are processed (current behavior preserved) (FR4)
**And** entity names are validated against the configured entity list using `StringComparer.OrdinalIgnoreCase` before execution begins (FR5)
**And** invalid entity names produce exit code 2 with a clear message listing valid entity names (ADR-8)
**And** no interactive prompts are introduced -- all validation errors are logged and the tool exits (FR24)

**Given** a selected subset of entities with dependencies
**When** the migration runs with `--entities`
**Then** dependency resolution operates within the selected subset only -- selecting entity A does not auto-include its dependency B (FR3)
**And** the selected subset starts processing proportionally faster than a full run (NFR12)

### Story 3.2: Exit Code Standardization & CLI Config Overrides

As a Migration Engineer,
I want consistent exit codes and the ability to override config settings from the command line,
So that I can integrate the tool into batch scripts with reliable flow control.

**Acceptance Criteria:**

**Given** any command execution
**When** the command completes
**Then** exit code 0 is returned when all entities succeed (FR23, ADR-8)
**And** exit code 1 is returned when one or more entities fail but execution completed (FR23, ADR-8)
**And** exit code 2 is returned for configuration errors, invalid arguments, or validation failures before processing starts (FR23, ADR-8)
**And** exit codes are consistent across all commands including future report commands

**Given** CLI arguments and `appsettings.json` both specify a value
**When** the command executes
**Then** CLI arguments take precedence over `appsettings.json` values (FR19, FR20)
**And** all features operate without interactive prompts, supporting fully unattended execution in batch scripts (FR24)

## Epic 4: Migration Result Persistence & Error Comparison

The Functional Consultant can immediately see which errors are new vs. carry-overs after each migration cycle -- reducing triage from half a day to 30 minutes.

### Story 4.1: Result Persistence & Credential Sanitization

As a Migration Engineer,
I want structured migration results captured automatically after each run,
So that I have a reliable record of every cycle's outcomes for analysis and comparison.

**Acceptance Criteria:**

**Given** a migration run completes (any command)
**When** the pipeline service finishes processing
**Then** a JSON result file is written to `{OutputDirectory}/results/` with filename `cycle-{yyyy-MM-ddTHHmmss}.json` (FR6, FR7)
**And** the result file contains: `cycleId`, `timestamp`, `command`, `entitiesRequested`, per-entity results (entityName, status, recordCount, durationMs, errors), and summary (totalEntities, succeeded, failed, warnings, skipped, totalDurationMs) per ADR-1 schema
**And** each cycle is uniquely identifiable by its `cycleId` (FR8)
**And** the result file uses `JsonDefaults.ResultJsonOptions` (camelCase, indented, enum as string) per architecture patterns
**And** writes are atomic via temp file + rename to prevent corruption on crash (NFR8)
**And** the console summary at completion shows total entities processed, succeeded, and failed (NFR7)
**And** result persistence adds negligible overhead to the migration pipeline (NFR11)

**Given** D365FO API error responses may contain credentials
**When** error messages are captured in results
**Then** `IResultSanitizer` strips: connection strings (`Server=...;Database=...`), Azure AD tenant/client IDs in auth contexts, Bearer tokens (`Bearer eyJ...`), SAS tokens (`sig=`, `sv=`, `se=`), and client secrets (ADR-7)
**And** stripped content is replaced with `[REDACTED]`
**And** sanitization is applied before any data reaches the result file (NFR1, NFR2)
**And** result files contain only entity names, statuses, sanitized error messages, and metadata -- no raw source data (NFR2)
**And** per-entity failures are captured with sufficient detail to diagnose root cause without re-running (NFR6)
**And** a failure in one entity does not prevent result capture for other entities (NFR5)

**Given** `IMigrationResultRepository` interface
**When** implemented as `JsonFileMigrationResultRepository`
**Then** the interface provides: `SaveCycleResultAsync`, `GetCycleResultAsync`, `GetLatestCycleResultsAsync`, `ListCycleIdsAsync`
**And** the repository is registered as Singleton in DI (stateless file I/O)
**And** `IResultSanitizer` is registered as Singleton and called within `SaveCycleResultAsync`

### Story 4.2: Error Fingerprinting & Comparison Engine

As a Functional Consultant,
I want errors automatically classified as new or carry-over when comparing cycles,
So that I can focus my triage on what actually changed since the last migration run.

**Acceptance Criteria:**

**Given** error messages from D365FO contain run-specific data (GUIDs, timestamps, record IDs)
**When** errors are captured during a migration run
**Then** each error receives a fingerprint computed as `SHA256(entityName + "|" + normalizedMessage)` truncated to 16 hex chars (ADR-6)
**And** normalization strips: GUIDs, ISO 8601 timestamps, common datetime formats, numeric record IDs (5+ digit sequences), then collapses whitespace and lowercases
**And** the fingerprint is stored alongside the original sanitized error message in the result JSON
**And** the same logical error across different cycles produces the same fingerprint

**Given** two cycle result files exist
**When** the error comparison engine runs
**Then** `IErrorComparisonService` compares the current cycle against the previous cycle
**And** errors are classified as "new" (fingerprint not found in previous cycle) or "carry-over" (fingerprint exists in previous cycle) (FR9, FR10)
**And** the comparison handles the first-cycle case gracefully -- reports "First cycle -- no comparison available" and returns exit code 0 (NFR9)

### Story 4.3: Error Comparison Report & CLI Integration

As a Functional Consultant,
I want a markdown error comparison report generated after each migration cycle,
So that I can immediately see new vs. known errors without manual comparison.

**Acceptance Criteria:**

**Given** error comparison results are available
**When** the report is generated
**Then** a markdown file is written to the output directory following the architecture report format: title, generated timestamp, cycle ID, summary table, per-entity breakdown (FR11)
**And** entity names use code formatting (`` `CustCustomerV3Entity` ``) and status indicators use `pass`/`FAIL`/`warn` convention
**And** the report clearly shows count of new errors, carry-over errors, and resolved errors per entity (FR12)
**And** report generation completes within seconds (NFR10)

**Given** the existing CLI commands
**When** `--compare` flag is added per ADR-5
**Then** `[Option("compare")] bool compare = false` is available on all migration commands (FR21)
**And** when `--compare` is true, error comparison report is auto-generated after the migration run
**And** a standalone `compare-errors` / `ce` command generates the report independently
**And** `compare-errors` accepts `[Option("cycle")] string? cycleId = null` to compare against a specific cycle instead of latest
**And** report output path can be overridden via `[Option("output")] string? outputPath = null` (FR22)
**And** missing historical data is handled gracefully with informative messages (NFR9)

## Epic 5: Readiness Reporting

The Project Director can open a single markdown file and assess migration readiness with trend data across cycles -- making evidence-based go/no-go decisions.

### Story 5.1: Readiness Report Generation

As a Project Director,
I want a readiness report aggregating migration results across multiple cycles,
So that I can assess migration readiness with trend data and make evidence-based go/no-go decisions.

**Acceptance Criteria:**

**Given** multiple cycle result files exist in the results directory
**When** the readiness report is generated
**Then** `IReadinessReportService` aggregates data across the specified number of cycles (default 5) (FR13)
**And** the report displays error trends across cycles: total errors per cycle over time (FR14)
**And** the report displays entity-level status classification: success (0 errors), warning (below threshold), failure (above threshold) (FR15)
**And** the report shows convergence direction per entity: improving (errors decreasing), stable (errors unchanged), degrading (errors increasing) (FR16)
**And** the report is generated in markdown format following the architecture report template: title, generated timestamp, summary table, per-entity breakdown, trend data (FR17)
**And** entity names use code formatting and status indicators use `pass`/`FAIL`/`warn` convention
**And** report generation completes within seconds (NFR10)

**Given** the readiness report configuration
**When** the Project Director configures parameters
**Then** `ReportSettings` in `appsettings.json` supports: `DefaultCycleRange` (default 5), `SuccessThreshold` (0 errors), `WarningThreshold` (5 errors), and `OutputDirectory` (FR18)
**And** settings are bound via `IOptions<ReportSettings>` following existing configuration patterns
**And** settings are registered in `Program.cs` with `AddOptions<ReportSettings>().Bind(...)`

**Given** fewer cycles exist than the requested range
**When** the readiness report is generated
**Then** the report covers all available cycles with a note indicating fewer than requested were available (NFR9)
**And** if only one cycle exists, the report shows current state without trend data

### Story 5.2: Readiness Report CLI Command

As a Project Director,
I want to generate readiness reports on demand via the command line,
So that I can produce updated reports whenever needed for steering committee meetings.

**Acceptance Criteria:**

**Given** the readiness report service is implemented
**When** the `readiness-report` / `rr` command is added per ADR-5
**Then** the command accepts `[Option("cycles")] int cycles = 5` to control how many cycles to include
**And** the command accepts `[Option("threshold")] string? thresholdConfig = null` to override threshold settings
**And** the command accepts `[Option("output")] string? outputPath = null` to override the report output path
**And** the report is written to a predictable file path for downstream consumption
**And** exit code 0 is returned on successful generation
**And** exit code 1 is returned if the report was generated but with warnings (e.g., missing cycle data)
**And** exit code 2 is returned for configuration errors (e.g., invalid threshold format)
**And** the command operates without interactive prompts (FR24)
