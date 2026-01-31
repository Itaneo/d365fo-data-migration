---
stepsCompleted:
  - step-01-init
  - step-02-discovery
  - step-03-success
  - step-04-journeys
  - step-05-domain
  - step-06-innovation
  - step-07-project-type
  - step-08-scoping
  - step-09-functional
  - step-10-nonfunctional
  - step-11-polish
  - step-12-complete
inputDocuments:
  - product-brief-d365fo-data-migration-2026-01-31.md
  - README.md
documentCounts:
  briefs: 1
  research: 0
  brainstorming: 0
  projectDocs: 1
classification:
  projectType: cli_tool
  domain: data_integration
  complexity: medium
  projectContext: brownfield
workflowType: 'prd'
---

# Product Requirements Document - d365fo-data-migration

**Author:** Jerome
**Date:** 2026-01-31

## Executive Summary

d365fo-data-migration is a .NET command-line tool that automates D365FO data migration from SQL Server sources through the Data Management Framework REST API. The tool replaces a manual process that consumes approximately one week of senior consultant time per migration cycle, with projects requiring 5-10 cycles across mock runs, testing, and cutover rehearsals. It is production-proven and deployed across multiple D365FO implementations as a proprietary practice accelerator.

**Differentiator:** No competing tool or consultancy capability provides end-to-end automated D365FO data migration. The industry standard is manual, entity-by-entity DMF uploads -- this tool eliminates that entirely.

**Target Users:**
- **Migration Engineer** -- Configures, operates, and refines the tool. Writes SQL extraction queries, defines entity dependencies, triggers migration runs.
- **Functional Consultant** -- Interprets migration results against D365FO business rules. Translates data issues into actionable reports for the source system team.
- **Project Director** -- Monitors data quality progress across cycles. Makes go/no-go cutover decisions.

**Brownfield Context:** The core feature set (SQL extraction, XML transformation, dependency-ordered packaging, parallel processing, D365FO API import) is complete. This PRD defines requirements for three phases of enhancement: platform hardening, operational improvements, and visibility reporting.

## Success Criteria

### User Success

**Migration Engineer:**
- Re-run a subset of entities (e.g., 3 out of 100+) without editing `appsettings.json` or reconfiguring the tool
- Trust that a comprehensive test suite catches regressions before deploying changes to production migration configurations
- Upgrade to .NET 10 with zero behavior changes -- existing configurations and runs produce identical results

**Functional Consultant:**
- Immediately distinguish new errors from known carry-overs when reviewing migration results, without manual comparison across cycle outputs
- Tighter feedback loop with the source team -- daily actionable reports instead of weekly manual analysis

**Project Director:**
- Review a markdown report showing migration readiness trends across cycles -- error counts, entity-level status, convergence direction
- Make evidence-based go/no-go decisions from the report without relying on manually compiled status updates

### Business Success

- **Reduce go-live risk:** Every automated step replaces a human action that could fail during cutover. Fewer manual touchpoints = fewer opportunities for a critical go-live failure
- **Reduce maintenance risk:** .NET 10 upgrade completed before .NET 8 end-of-support; test suite prevents regressions during future changes
- **Faster onboarding:** New team members can set up, configure, and run the tool using documentation alone, without requiring hand-holding from the original developer
- **Competitive positioning:** Enhanced capabilities (selective runs, error comparison, readiness reports) strengthen the practice accelerator advantage when bidding on D365FO engagements

### Technical Success

- .NET 10 upgrade: builds, runs, and produces identical output to .NET 8 version
- Test suite covers critical paths: dependency topological sorting, XML generation, package creation, API integration
- No regressions introduced -- existing migration configurations produce the same results before and after changes
- Documentation sufficient for a new developer to understand the codebase and a new consultant to configure and run the tool

### Measurable Outcomes

| Outcome | Measure |
|---------|---------|
| Test coverage on critical paths | Dependency sorting, XML gen, packaging, API integration all covered by xUnit tests |
| .NET 10 migration | Zero behavior changes, all existing configs produce identical results |
| Selective entity runs | Run N entities from CLI without config file changes |
| Error comparison | New-vs-known errors surfaced automatically per cycle |
| Readiness report | Markdown report generated per cycle with trend data |
| Onboarding time | New team member operational from documentation alone |

## User Journeys

### Journey 1: Migration Engineer -- Targeted Re-Run

The source team fixes customer data overnight. The Migration Engineer needs to re-run just the customer-related entities to validate the fix before tomorrow's status call. Currently, they'd either run the full 100+ entity pipeline (wasting hours) or manually edit `appsettings.json` to temporarily remove entities (error-prone and risks forgetting to restore the config).

With selective entity runs, they specify the entities at the command line, kick off a focused run, and have results in a fraction of the time -- config untouched, no risk of accidentally breaking the next full run.

### Journey 2: Migration Engineer -- Regression Confidence

A new engagement starts. The Migration Engineer needs to adjust XML transformation logic for a client-specific entity structure. Today, they make the change and hope nothing else broke -- there's no test suite to catch regressions. They won't know until the next full migration run, and even then a subtle regression might not surface immediately.

With the test suite in place, they run `dotnet test` after every change. The critical paths -- dependency sorting, XML generation, packaging, API integration -- are validated instantly. They push changes with confidence.

### Journey 3: Functional Consultant -- Error Triage

It's Monday morning after a weekend migration run. The Functional Consultant opens the results and sees 47 entity errors. They need to report to the source team, but first they have to figure out which errors are new (need attention) vs. which are carry-overs from last cycle (already reported, awaiting fixes). Today, they manually compare against last cycle's notes -- spreadsheets, emails, memory.

With cycle-over-cycle error comparison, the tool surfaces this automatically: "12 new errors, 35 carry-overs from previous cycle." The Functional Consultant writes their report in 30 minutes instead of half a day, focusing only on what's changed.

### Journey 4: Project Director -- Go/No-Go Decision

The steering committee meets in two days. The Project Director needs to answer: "Are we ready for cutover?" Today, they ask the Functional Consultant to compile a status summary from memory and scattered notes. The answer is qualitative -- "we're mostly there, a few entities still have issues."

With the markdown readiness report, they open a single file showing error trends across the last 5 cycles: total errors dropping from 200 → 120 → 65 → 22 → 8, entity-level status (green/yellow/red), and which remaining issues are blockers vs. acceptable. They walk into the steering committee with evidence, not opinions.

### Journey 5: New Developer -- Codebase Onboarding

A new developer joins the practice and needs to understand the tool well enough to configure it for a new engagement and potentially modify it. Today, there's a README with configuration examples but no developer documentation explaining the architecture, no tests showing expected behavior, and no setup guide beyond "clone and build."

With the improved documentation and test suite, they read the setup guide, follow the configuration reference to set up their first entity, and explore the test suite to understand how dependency sorting and XML generation work. They're productive within days instead of weeks.

### Journey Requirements Summary

| Journey | Capabilities Revealed |
|---------|----------------------|
| Targeted Re-Run | CLI entity selection, config preservation, partial execution |
| Regression Confidence | xUnit test suite, critical path coverage, CI-friendly test execution |
| Error Triage | Result persistence between cycles, diff engine, new-vs-known classification |
| Go/No-Go Decision | Cycle result aggregation, trend calculation, markdown report generation |
| Codebase Onboarding | Setup guide, configuration reference, entity authoring guide, developer docs, test suite as living documentation |

## CLI Tool Specific Requirements

### Project-Type Overview

d365fo-data-migration is a .NET CLI tool following standard command-verb patterns with shorthand aliases. The existing command structure (`export-file`, `export-package`, `import-d365`) is stable and production-proven. New features extend this structure with additional CLI arguments while maintaining backward compatibility with existing configurations and scripts.

### Command Structure

**Existing commands (unchanged):**
- `export-file` / `f` -- Export data to individual XML files
- `export-package` / `p` -- Export data to ZIP packages with manifests
- `import-d365` / `i` -- Full export and import pipeline to D365FO

**New CLI arguments for selective entity runs:**
- `--entities` flag accepts a comma-separated list of entity names to process
- Applies to all three existing commands
- When omitted, all configured entities are processed (current behavior preserved)
- Example: `dotnet run -- import-d365 --entities CustCustomerV3Entity,VendVendorV2Entity`
- Dependency resolution still applies within the selected subset

**New commands for reporting:**
- Error comparison and readiness report generation as additional commands or flags (exact command names TBD during architecture)

### Output Formats

**Existing outputs (unchanged):**
- XML files per entity (export-file)
- ZIP packages with manifest and package header (export-package)
- Console logging via Serilog

**New outputs:**
- **Error comparison report:** Markdown file comparing current cycle results against previous cycle. Surfaces new errors vs. known carry-overs. Written to the output directory alongside migration results.
- **Readiness report:** Markdown file aggregating data quality metrics across multiple cycles -- error trends, entity-level status, convergence tracking. Written to a configurable output path.

### Configuration Schema

**Dual configuration approach:**
- `appsettings.json` for persistent settings: output directories for reports, default comparison behavior, readiness report configuration (cycle range, threshold definitions)
- CLI arguments for runtime overrides: `--entities` for selective runs, flags to enable/disable report generation per run, output path overrides

**Precedence:** CLI arguments override `appsettings.json` values when both are specified.

### Scripting Support

- All new features designed for unattended execution in batch scripts
- Exit codes reflect success/failure for script flow control
- Reports written to predictable file paths for downstream consumption
- Full pipeline scriptable: run migration, generate error comparison, produce readiness report -- all in a single batch sequence
- No interactive prompts -- all input via configuration and CLI arguments

### Implementation Considerations

- New CLI arguments parsed alongside existing command structure without breaking changes
- Entity name validation against configured entities before execution begins
- Error comparison requires persisting structured results between cycles (format and storage TBD during architecture)
- Readiness report requires access to historical cycle results (storage mechanism TBD during architecture)

## Project Scoping & Phased Development

### MVP Strategy & Philosophy

**MVP Approach:** Hardening-first. The core product works and is deployed. Phase 1 focuses entirely on reducing risk and building a foundation for safe future development -- no new runtime features, no changes to migration behavior.

**Resource Reality:** Solo developer. Phases are sequential, not parallel. Each phase must be completable independently and deliver standalone value.

### Phase 1: Hardening & Modernization (MVP)

**Core User Journeys Supported:**
- Journey 2: Regression Confidence (test suite)
- Journey 5: Codebase Onboarding (documentation)

**Must-Have Capabilities (in order):**

1. **.NET 10 upgrade** -- Runtime and SDK migration. Zero behavior changes. Establishes the baseline for all subsequent work. Must be done before .NET 8 end-of-support.
2. **xUnit test suite** -- Critical path coverage: dependency topological sorting, XML generation, package creation, API integration. Uses Shouldly assertions. Validates the upgraded baseline and provides a regression safety net for all future changes.
3. **Documentation** -- Setup guide, configuration reference, entity definition authoring guide, developer documentation. Documents the final Phase 1 state. Enables onboarding without hand-holding.

**Phase 1 exit criteria:** Tool runs on .NET 10 with identical behavior, critical paths are test-covered, and a new team member can get operational from documentation alone.

### Phase 2: Operational Improvements

**Core User Journeys Supported:**
- Journey 1: Targeted Re-Run (selective entity runs)
- Journey 3: Error Triage (error comparison)

**Capabilities (in order):**

1. **Structured result persistence** -- Foundation capability. Capture migration results (entity name, status, error details, timestamp) in a structured format after each run. Stored alongside existing output. Underpins error comparison and readiness reporting. No user-facing feature yet -- just the data layer.
2. **Selective entity runs** -- `--entities` CLI flag for all three commands. Independent of result persistence but benefits from it.
3. **Cycle-over-cycle error comparison** -- Depends on result persistence. Generates markdown report comparing current cycle against previous cycle: new errors vs. known carry-overs.

**Phase 2 exit criteria:** Migration Engineer can re-run specific entities without config changes. Functional Consultant can immediately see new vs. known errors after each cycle.

### Phase 3: Visibility & Reporting

**Core User Journeys Supported:**
- Journey 4: Go/No-Go Decision (readiness report)

**Capabilities:**

1. **Readiness report** -- Depends on result persistence from Phase 2. Aggregates data quality metrics across multiple cycles into a markdown report: error trends, entity-level status (green/yellow/red), convergence tracking. Configurable via `appsettings.json` (thresholds, cycle range).

**Phase 3 exit criteria:** Project Director can open a single markdown file and assess migration readiness with trend data across cycles.

### Risk Mitigation Strategy

**Technical Risks:**
- **.NET 10 upgrade regression:** Mitigated by building the test suite immediately after upgrade. Run existing migrations before and after to verify identical output.
- **Result persistence design:** Biggest unknown. No structured output exists today. Format and storage mechanism must be decided during Phase 2 architecture. Risk: over-engineering the storage layer. Mitigation: start with JSON files alongside existing output.
- **CLI backward compatibility:** New `--entities` flag must not break existing scripts or configurations. Mitigated by making all new arguments optional with current behavior as default.

**Resource Risks:**
- Solo developer means sequential execution only. Each phase must deliver standalone value in case priorities shift between phases.
- If time is constrained, Phase 1 items can be further prioritized: .NET 10 upgrade is non-negotiable (EOL risk), test suite is high value, documentation can be incremental.

**Go-Live Risk:**
- Every phase reduces the risk of human error during migration. Phase 1 catches regressions. Phase 2 eliminates manual config editing and manual error comparison. Phase 3 replaces manual status compilation. Each phase independently reduces go-live failure surface.

## Functional Requirements

### Entity Selection & Execution Control

- FR1: Migration Engineer can specify a subset of entities to process via CLI argument, without modifying configuration files
- FR2: Migration Engineer can use entity selection with any execution mode (export-file, export-package, import-d365)
- FR3: The system resolves dependencies within the selected entity subset, ensuring correct import order
- FR4: When no entity selection is specified, the system processes all configured entities (existing behavior preserved)
- FR5: The system validates specified entity names against the configured entity list before execution begins

### Migration Result Persistence

- FR6: The system captures structured migration results after each run, including entity name, status, error details, and timestamp
- FR7: The system stores migration results in a persistent format alongside existing output, accessible for comparison across cycles
- FR8: Migration Engineer can identify which cycle a set of results belongs to

### Error Comparison & Analysis

- FR9: The system compares current cycle results against the previous cycle's results automatically
- FR10: The system classifies errors as new (first appearance this cycle) or carry-over (present in previous cycle)
- FR11: The system generates a markdown error comparison report written to the output directory
- FR12: Functional Consultant can review the error comparison report to identify only what changed since last cycle

### Readiness Reporting

- FR13: The system aggregates migration results across multiple cycles into a single readiness report
- FR14: The readiness report displays error trends across cycles (total errors per cycle over time)
- FR15: The readiness report displays entity-level status classification (success, warning, failure)
- FR16: The readiness report shows convergence direction (improving, stable, degrading)
- FR17: The system generates the readiness report in markdown format
- FR18: Project Director can configure readiness report parameters (cycle range, thresholds) via settings

### CLI Interface & Configuration

- FR19: Migration Engineer can override configuration file settings via CLI arguments at runtime
- FR20: CLI arguments take precedence over `appsettings.json` values when both are specified
- FR21: Migration Engineer can enable or disable report generation per run via CLI flags
- FR22: Migration Engineer can override output paths for reports via CLI arguments
- FR23: The system returns meaningful exit codes reflecting success or failure for script integration
- FR24: All features operate without interactive prompts, supporting fully unattended execution

### Platform Modernization

- FR25: The system builds and runs on .NET 10 runtime and SDK
- FR26: The system produces identical output on .NET 10 as on .NET 8 for all existing configurations and entity definitions
- FR27: All existing CLI commands, configuration options, and execution modes continue to function without modification after the upgrade

### Test Coverage

- FR28: The test suite validates dependency topological sorting produces correct entity ordering for known dependency graphs
- FR29: The test suite validates XML generation produces D365FO-compatible output for known entity data
- FR30: The test suite validates package creation produces valid ZIP archives with correct manifest and header files
- FR31: The test suite validates API integration handles authentication, upload, and import operations correctly
- FR32: The test suite can be executed via `dotnet test` with no external dependencies required (D365FO environment, SQL Server) for unit-level tests

### Documentation

- FR33: A setup guide enables a new user to install, configure, and run the tool from scratch
- FR34: A configuration reference documents all `appsettings.json` options with descriptions, types, defaults, and examples
- FR35: An entity definition authoring guide explains how to create SQL queries, manifest files, and package headers for new entities
- FR36: Developer documentation explains the codebase architecture, key components, data flow, and extension points

## Non-Functional Requirements

### Security

- NFR1: Azure AD credentials and SQL connection strings must never be written to log files, console output, or result persistence files
- NFR2: Result persistence files must not contain raw source data -- only entity names, statuses, error messages, and metadata
- NFR3: Sensitive configuration values (client secrets, connection strings) must be supported via .NET User Secrets or environment variables, not only plain-text configuration files
- NFR4: The tool must not introduce new credential storage mechanisms -- continue using .NET's established secret management patterns

### Reliability

- NFR5: A failure in one entity's extraction, transformation, or import must not terminate the entire migration run -- remaining entities continue processing
- NFR6: All entity-level failures must be captured in structured results with sufficient detail to diagnose the root cause without re-running
- NFR7: The tool must produce a clear summary at completion indicating total entities processed, succeeded, and failed
- NFR8: Result persistence writes must be atomic or recoverable -- a crash during result writing must not corrupt previously persisted cycle data
- NFR9: The error comparison and readiness report features must handle missing or incomplete historical data gracefully (e.g., first run with no previous cycle to compare against)

### Performance

- NFR10: Report generation (error comparison, readiness report) must complete within seconds, not minutes -- report generation must not meaningfully extend the total migration runtime
- NFR11: Result persistence must not degrade existing migration throughput -- structured result capture should add negligible overhead to the extraction/import pipeline
- NFR12: Selective entity runs must start processing faster than a full run proportional to the number of entities selected (no unnecessary initialization of unselected entities)
