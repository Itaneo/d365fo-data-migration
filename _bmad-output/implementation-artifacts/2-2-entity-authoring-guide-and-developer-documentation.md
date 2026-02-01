# Story 2.2: Entity Authoring Guide & Developer Documentation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a new team member,
I want an entity authoring guide and developer documentation,
So that I can create new entity definitions and understand the codebase well enough to extend it.

## Acceptance Criteria

1. **Given** a consultant needing to add a new D365FO entity to the migration, **When** they follow `docs/entity-authoring-guide.md`, **Then** the guide explains: SQL extraction query structure, manifest XML format, package header XML format, dependency declaration, and entity naming conventions (FR35)
2. **And** the guide includes a complete worked example for adding a new entity from scratch
3. **Given** a developer needing to understand or modify the codebase, **When** they read `docs/developer-guide.md`, **Then** the guide covers: solution structure, key components and their responsibilities, data flow (SQL extraction → XML transformation → ZIP packaging → D365FO API import), DI registration patterns, pipeline service architecture, and extension points (FR36)
4. **And** the guide references the test suite as living documentation for expected behavior
5. **And** the guide documents the folder structure conventions for new code (per architecture patterns)

## Tasks / Subtasks

- [x] Task 1: Create `docs/entity-authoring-guide.md` (AC: #1, #2)
  - [x] 1.1 Write Introduction section: purpose, audience (Functional Consultant / Migration Engineer adding new entities), prerequisites (access to D365FO Data Entity documentation, SQL Server source database, understanding of target entity schema)
  - [x] 1.2 Write Entity Naming Conventions section:
    - Entity name in `appsettings.json` matches the D365FO data entity name exactly (e.g., `CustCustomerV3Entity`)
    - Folder name in `Definition/` uses the entity name as provided in config (e.g., `CustCustomerBaseEntity/`)
    - Internal resolution converts to UPPERCASE for file lookups (e.g., `CUSTCUSTOMERBASEENTITY.sql`)
    - `DefinitionGroupId` follows project convention (e.g., `Datamig_CustCustomerV3Entity` or `IFG_CUSTCUSTOMERBASEENTITY`)
  - [x] 1.3 Write Entity Definition Directory Structure section:
    - Three required files per entity: SQL query, Manifest.xml, PackageHeader.xml
    - Path resolution: `{DefinitionDirectory}/{EntityName}/` with auto-resolution to `{ENTITYNAME}.sql`, `Manifest.xml`, `PackageHeader.xml`
    - Custom path overrides via `QueryFileName`, `ManifestFileName`, `PackageHeaderFileName` in config
  - [x] 1.4 Write SQL Extraction Query section:
    - File naming: `{ENTITYNAME}.sql` (uppercase)
    - Query must return columns that match D365FO entity field names exactly
    - Column aliasing for name mapping (source column → D365FO field name)
    - Support for JOINs (e.g., code conversion lookups, cross-reference tables)
    - NULL handling with ISNULL/COALESCE for required D365FO fields
    - WHERE clauses for data filtering (company, status, active records)
    - `TOP N` for development/testing with smaller datasets
    - RecordsPerFile setting and how it interacts with query results (automatic file splitting)
    - Reference existing entity SQL files as examples
  - [x] 1.5 Write Manifest XML Format section:
    - Root element: `<DataManagementPackageManifest>` with D365FO namespace
    - `<DefinitionGroupName>` element matching `DefinitionGroupId` from config
    - `<Description>` element for human-readable description
    - `<PackageEntityList>` → `<DataManagementPackageEntityData>` structure
    - `<DefaultRefreshType>FullPush</DefaultRefreshType>` for full data push
    - `<EntityMapList>` with `<EntityMap>` entries mapping `<EntityField>` to `<XMLField>`
    - Field conversion lists and default value handling
    - Reference existing Manifest.xml files as examples
  - [x] 1.6 Write Package Header XML Format section:
    - Root element: `<DataManagementPackageHeader>` with D365FO namespace
    - Required elements: `<Description>`, `<ManifestType>`, `<PackageType>`, `<PackageVersion>`
    - Standard values: `PackageType` = `DefinitionGroup`, `PackageVersion` = `2`
    - `ManifestType` = `Microsoft.Dynamics.AX.Framework.Tools.DataManagement.Serialization.DataManagementPackageManifest`
    - Reference existing PackageHeader.xml files as examples
  - [x] 1.7 Write Dependency Declaration section:
    - How to specify dependencies in `appsettings.json` (`Dependencies` array in `QuerySettings`)
    - Topological sorting: entities processed in dependency order, independent entities run in parallel
    - Common dependency patterns (e.g., customers before customer bank accounts, contact persons before customers)
    - Circular dependency detection and error messaging
  - [x] 1.8 Write Complete Worked Example section:
    - Walk through adding a fictional but realistic entity (e.g., `VendVendorGroupEntity`) from scratch
    - Step 1: Identify D365FO target entity and its fields
    - Step 2: Write SQL extraction query mapping source columns to D365FO fields
    - Step 3: Create Manifest.xml with field mappings
    - Step 4: Create PackageHeader.xml
    - Step 5: Add entity to `appsettings.json` with dependencies
    - Step 6: Test with `export-file` command to verify XML output
    - Step 7: Validate with `export-package` to verify ZIP package
  - [x] 1.9 Write Troubleshooting section:
    - Common issues: SQL query errors, column name mismatches, missing entity definition files, incorrect dependency ordering, D365FO import failures from manifest field mapping errors

- [x] Task 2: Create `docs/developer-guide.md` (AC: #3, #4, #5)
  - [x] 2.1 Write Introduction section: purpose (developer-facing architecture and codebase guide), audience (developer modifying or extending the tool)
  - [x] 2.2 Write Solution Structure section:
    - `Dynamics365ImportData.sln` with 2 projects
    - Main project: `Dynamics365ImportData/Dynamics365ImportData/` targeting `net10.0`
    - Test project: `Dynamics365ImportData/Dynamics365ImportData.Tests/` targeting `net10.0`
    - Top-level folder organization: Azure/, DependencySorting/, Erp/, Pipeline/, Services/, Settings/, XmlOutput/, Definition/
    - Each folder = one capability area, namespace matches folder path
  - [x] 2.3 Write Key Components section documenting each component's responsibility:
    - **Program.cs** — Entry point, DI container setup, configuration loading, Serilog initialization
    - **CommandHandler.cs** — Thin CLI adapter using Cocona `[Command]` attributes; delegates to `IMigrationPipelineService`; commands: `export-file`/`f`, `export-package`/`p`, `import-d365`/`i`
    - **Pipeline/MigrationPipelineService.cs** — Orchestrates entity processing: resolves output factory by mode, iterates dependency levels, processes entities in parallel per level, monitors D365FO import status
    - **Pipeline/IMigrationPipelineService.cs** — Interface: `ExecuteAsync(PipelineMode, string[]?, CancellationToken)` returning `CycleResult`
    - **Pipeline/PipelineMode.cs** — Enum: File, Package, D365
    - **Pipeline/CycleResult.cs** — Result model: TotalEntities, Succeeded, Failed
    - **DependencySorting/SourceQueryCollection.cs** — Loads entity configuration, builds dependency graph, performs topological sort, produces `SortedQueries` (grouped by execution level)
    - **DependencySorting/SourceQueryItem.cs** — Immutable record: entity name, definition file paths, connection string, dependencies
    - **DependencySorting/TopologicalSort.cs** — Kahn's algorithm for topological ordering
    - **DependencySorting/DependencyGraph.cs** — Directed acyclic graph representation
    - **Services/SqlToXmlService.cs** — Opens SQL connection, executes query from file, streams results into XML via `XmlWriter`, splits output by `RecordsPerFile`
    - **XmlOutput/IXmlOutputFactory.cs** — Factory interface: `CreateAsync(SourceQueryItem, int, CancellationToken)` returning `IXmlOutputPart`
    - **XmlOutput/IXmlOutputPart.cs** — Output part interface: `XmlWriter`, `PartName`, `Open()`, `Close()`, `PostWriteProcessAsync()`, `GetStateAsync()`
    - **XmlOutput/XmlFileOutputFactory.cs** — Writes individual XML files to output directory
    - **XmlOutput/XmlPackageFileOutputFactory.cs** — Creates ZIP packages with Manifest.xml, PackageHeader.xml, and entity data
    - **XmlOutput/XmlD365FnoOutputFactory.cs** — Uploads ZIP to Azure Blob Storage, triggers D365FO import via DMF API, polls for completion
    - **Erp/Dynamics365FnoClient.cs** — Base HTTP client with Azure AD OAuth2 (MSAL) authentication
    - **Erp/DataManagementDefinitionGroups/** — D365FO DMF REST API operations: `GetAzureWriteUrl`, `ImportFromPackage`, `GetExecutionSummaryStatus`
    - **Azure/AzureContainer.cs** — Azure Blob Storage upload operations
    - **Settings/*.cs** — Configuration POCOs bound via `IOptions<T>` pattern
  - [x] 2.4 Write Data Flow section:
    - End-to-end flow diagram (text-based): Configuration → Entity Loading & Dependency Sorting → Parallel Entity Processing → SQL Extraction → XML Streaming → Output Factory → Output Target (File / ZIP / D365FO)
    - Detailed description of each stage with component references
    - How `RecordsPerFile` splits output across multiple files/parts
    - How parallel processing works within dependency levels (`Parallel.ForEachAsync` with `MaxDegreeOfParallelism`)
    - D365FO import flow: ZIP → Azure Blob → DMF ImportFromPackage → Status Polling (15-second intervals)
  - [x] 2.5 Write DI Registration Patterns section:
    - Configuration loading order from `Program.cs` (appsettings.json → environment → User Secrets → env vars → CLI args)
    - Service lifetime strategy: Transient for `SqlToXmlService` and `IMigrationPipelineService`, Singleton for `SourceQueryCollection` and output factories
    - `IOptions<T>` binding: `AddOptions<T>().Bind(configuration.GetSection(...))`
    - `IHttpClientFactory` usage for D365FO API client
    - How to add new services: create interface + implementation, register in `Program.cs` with appropriate lifetime
  - [x] 2.6 Write Pipeline Service Architecture section:
    - ADR-9 context: extracted from `CommandHandler` to enable testability and future extensibility
    - `CommandHandler` as thin CLI adapter (~10 lines per command): parse args → call pipeline → return exit code
    - Factory resolution by `PipelineMode`
    - Execution loop: iterate sorted query levels → parallel process per level → collect results
    - Status monitoring for D365FO imports (polling loop with timeout)
    - Fault isolation: per-entity exception handling, continues processing remaining entities (NFR5)
  - [x] 2.7 Write Extension Points section:
    - **Adding a new output mode:** Implement `IXmlOutputFactory` and `IXmlOutputPart`, add to `PipelineMode` enum, register in DI, add case to factory resolution in `MigrationPipelineService`
    - **Adding new CLI commands:** Add method to `CommandHandler` with `[Command]` attribute, delegate to appropriate service
    - **Adding new configuration sections:** Create settings POCO in `Settings/`, bind in `Program.cs`, inject via `IOptions<T>`
    - **Adding new entity processing logic:** Modify `SqlToXmlService` for extraction changes, or `MigrationPipelineService` for orchestration changes
    - **Future extensibility per architecture doc:** Persistence/, Sanitization/, Fingerprinting/, Comparison/, Reporting/ folders planned for Phase 2-3 features
  - [x] 2.8 Write Folder Structure Conventions section (per architecture document):
    - Existing horizontal-slice folder structure: each folder = one capability
    - Namespace matches folder path: `Dynamics365ImportData.Pipeline`, `Dynamics365ImportData.DependencySorting`, etc.
    - One interface + one implementation per file
    - Interface file named `I{ServiceName}.cs`, implementation named `{ServiceName}.cs`
    - New capabilities get their own folder at project root level
    - Settings POCOs go in `Settings/` folder
    - Test project mirrors main project structure with categorization: `Unit/`, `Integration/`, `Snapshot/`, `Audit/`, `TestHelpers/`
  - [x] 2.9 Write Test Suite as Living Documentation section (AC: #4):
    - Test project location and how to run: `dotnet test` from solution directory
    - 54 tests covering critical paths
    - Test categories: Unit (algorithm correctness), Integration (pipeline wiring, packaging), Snapshot (golden-file output validation), Audit (credential leak prevention)
    - Test naming convention: `{Method}_{Scenario}_{ExpectedResult}`
    - Arrange/Act/Assert structure with Shouldly assertions
    - Key test files as documentation:
      - `TopologicalSortTests.cs` — demonstrates dependency resolution behavior
      - `XmlGenerationTests.cs` — shows expected XML output format
      - `ZipPackageTests.cs` — documents package structure requirements
      - `PipelineServiceTests.cs` — documents pipeline contract (modes, filtering, fault isolation)
      - `CredentialLeakTests.cs` — demonstrates credential safety requirements
    - How to add new tests: follow existing patterns, place in appropriate category folder
  - [x] 2.10 Write Coding Conventions section:
    - Inherited patterns: PascalCase classes/methods, `I` prefix interfaces, camelCase locals, async suffix, nullable annotations enabled
    - Serilog structured message templates (NOT string interpolation)
    - `CancellationToken` propagation through all async chains
    - Error handling: per-entity catch-and-continue, never swallow exceptions silently
    - No `Console.WriteLine` — use Serilog
    - No static helper classes — use DI-injected services
    - No `new HttpClient()` — use `IHttpClientFactory`

- [x] Task 3: Update existing docs with cross-references (AC: #3)
  - [x] 3.1 Add "See Also" link in `docs/setup-guide.md` footer referencing entity authoring guide and developer guide
  - [x] 3.2 Add "See Also" link in `docs/configuration-reference.md` footer referencing entity authoring guide and developer guide
  - [x] 3.3 Update `README.md` Documentation section to include links to all four docs

## Dev Notes

### Purpose

This is the second and final documentation story in Epic 2, completing the onboarding documentation suite. It creates two documents: an entity authoring guide for consultants/engineers who need to add new D365FO entities (FR35), and a developer guide for anyone modifying or extending the codebase (FR36). Together with Story 2.1's setup guide and configuration reference, these four documents enable full self-service onboarding (Journey 5: Codebase Onboarding).

### Previous Story Intelligence

**Story 2.1 (done):**
- Created `docs/setup-guide.md` (FR33) and `docs/configuration-reference.md` (FR34)
- Updated README.md with .NET 10 references and documentation links
- Code review found 6 issues (2 HIGH, 4 MEDIUM):
  - **HIGH:** Entity folder/file naming must reflect UPPERCASE resolution from `SourceQueryCollection.cs` — the entity authoring guide MUST document this clearly
  - **HIGH:** Output directory clearing scope (only export commands clear output) — developer guide should document this behavior
  - **MEDIUM:** Config precedence code snippet must match actual `Program.cs` — developer guide must use exact code from source
  - Other fixes: TrustServerCertificate in examples, OutputDirectory description accuracy
- Key pattern: docs reference actual source code snippets and configuration, not generic .NET descriptions

**Epic 1 Summary (all 5 stories done):**
- Story 1.1: Test project created (`Dynamics365ImportData.Tests.csproj`), `IMigrationPipelineService` extracted from `CommandHandler` (ADR-9)
- Story 1.2: 20 characterization tests on .NET 8 baseline
- Story 1.3: .NET 10 upgrade — both `.csproj` files target `net10.0`
- Story 1.4: `System.Data.SqlClient` replaced with `Microsoft.Data.SqlClient` 6.1.4
- Story 1.5: Test suite expanded to 54 tests (Unit, Integration, Snapshot, Audit)

### Git Intelligence

**Recent commits:**
- `2410cd4` - Story 2.1: Setup guide and configuration reference (#9)
- `76def33` - Story 1.5: Full test suite expansion (#8)
- `8081a06` - Story 1.4: SqlClient migration (#7)
- `710e620` - Story 1.3: .NET 10 upgrade (#6)
- `79e26d1` - Story 1.2: Characterization tests on .NET 8 baseline (#5)

**Current project state post-Story 2.1:**
- Solution: `Dynamics365ImportData.sln` with 2 projects (both `net10.0`)
- Main project: `Dynamics365ImportData/Dynamics365ImportData/`
- Test project: `Dynamics365ImportData/Dynamics365ImportData.Tests/`
- Docs: `docs/setup-guide.md`, `docs/configuration-reference.md`
- 54 tests passing via `dotnet test`

### Project Structure Notes

**Current structure (relevant to this story):**
```
d365fo-data-migration/
├── Dynamics365ImportData/
│   ├── Dynamics365ImportData.sln
│   ├── Dynamics365ImportData/          (main project, net10.0)
│   │   ├── Program.cs                 (entry point, DI, config)
│   │   ├── CommandHandler.cs          (CLI: export-file, export-package, import-d365)
│   │   ├── Pipeline/                  (IMigrationPipelineService, PipelineMode, CycleResult)
│   │   ├── DependencySorting/         (SourceQueryCollection, TopologicalSort, DependencyGraph)
│   │   ├── Services/                  (SqlToXmlService)
│   │   ├── XmlOutput/                 (IXmlOutputFactory, 3 factory implementations)
│   │   ├── Erp/                       (D365FO API client, DMF operations)
│   │   ├── Azure/                     (Blob storage)
│   │   ├── Settings/                  (config POCOs: Source, Destination, Process, Query, Dynamics365)
│   │   ├── Definition/                (entity definitions: SQL, Manifest.xml, PackageHeader.xml)
│   │   └── appsettings.json
│   └── Dynamics365ImportData.Tests/   (54 tests: Unit, Integration, Snapshot, Audit)
├── README.md
└── docs/
    ├── setup-guide.md                 (FR33, from Story 2.1)
    ├── configuration-reference.md     (FR34, from Story 2.1)
    ├── entity-authoring-guide.md      (FR35, THIS STORY — NEW)
    └── developer-guide.md             (FR36, THIS STORY — NEW)
```

**No conflicts with existing structure.** New docs go into the existing `docs/` directory created by Story 2.1.

### Architecture Compliance

**This is a documentation-only story. No code changes to production or test projects.**

**Architecture references:**
- FR35: "An entity definition authoring guide explains how to create SQL queries, manifest files, and package headers for new entities"
- FR36: "Developer documentation explains the codebase architecture, key components, data flow, and extension points"
- Architecture doc Section "Project Structure & Boundaries" — full directory tree with `[EXISTING]` and `[NEW]` markers
- Architecture doc Section "Implementation Patterns & Consistency Rules" — coding conventions to document
- Architecture doc Section "Data Flow" — migration execution flow and report generation flow diagrams
- Architecture doc Section "Architectural Boundaries" — service boundary diagram and data boundaries

**Key codebase facts to document accurately:**

1. **Entity name resolution:** `SourceQueryCollection` converts entity names to UPPERCASE for file lookups (`GetEntityName` method). The folder name in `Definition/` uses the entity name as-is from config (e.g., `CustCustomerBaseEntity/`), but SQL file is `{ENTITYNAME}.sql` (uppercase).
2. **Configuration loading:** Exact 5-layer precedence from `Program.cs` — use actual code, not generic .NET description.
3. **DI lifetimes:** Transient for `SqlToXmlService`, `MigrationPipelineService`; Singleton for `SourceQueryCollection`, output factories; HttpClient via `IHttpClientFactory`.
4. **Parallel processing:** `Parallel.ForEachAsync` within each dependency level, controlled by `MaxDegreeOfParallelism`.
5. **Output clearing:** `CommandHandler.ClearOutputDirectory()` called at start of export commands only.
6. **XML format:** Row → element with entity name, columns → attributes. Root element `<Document>`.
7. **ZIP package structure:** Contains Manifest.xml, PackageHeader.xml, and entity data XML file.
8. **D365FO import flow:** ZIP → Azure Blob → DMF `ImportFromPackage` → status polling (15-second intervals) → timeout per `ImportTimeout` setting.
9. **Test structure:** Unit/, Integration/, Snapshot/, Audit/, TestHelpers/ with naming convention `{Method}_{Scenario}_{ExpectedResult}`.

### File Structure Requirements

**New files to create:**
```
docs/
├── entity-authoring-guide.md    (FR35 — NEW)
└── developer-guide.md           (FR36 — NEW)
```

**Existing files to modify:**
```
docs/setup-guide.md              (add cross-reference links)
docs/configuration-reference.md  (add cross-reference links)
README.md                        (add links to new docs)
```

**DO NOT create or modify:**
- Any `.cs` files
- Any `.csproj` files
- `appsettings.json`
- Test files

### Library & Framework Requirements

No library or framework changes. This is a documentation-only story.

### Testing Requirements

No new tests required. Verify existing 54 tests still pass after any README/docs changes (no code changes, so this is a sanity check only).

### Critical Guardrails

1. **DO NOT modify any production code** — this story creates documentation files only
2. **DO NOT modify any test code** — no test changes needed
3. **DO NOT modify `appsettings.json`** — document the existing configuration, don't change it
4. **DO NOT include real credentials** in any documentation examples — use clear placeholders
5. **DO NOT duplicate content** from existing docs (setup-guide.md, configuration-reference.md) — cross-reference instead
6. **DO reference the correct .NET version** — the project targets .NET 10 (not .NET 8)
7. **DO document the UPPERCASE file resolution** in `SourceQueryCollection` — this was a HIGH-priority code review finding in Story 2.1
8. **DO use actual code snippets from the source files** — not generic .NET examples. Reference specific files and line patterns
9. **DO document the actual DI registrations** from `Program.cs` — not theoretical patterns
10. **DO reference existing entity definitions** in `Definition/` as concrete examples
11. **DO reference the test suite** as living documentation (AC #4) — point to specific test files showing expected behavior
12. **DO document folder structure conventions** from the architecture document (AC #5)
13. **DO write documentation in English** per `document_output_language` config
14. **DO cross-reference** between the entity authoring guide (for SQL/manifest details) and the developer guide (for architecture context), and vice versa

### Anti-Patterns to Avoid

- DO NOT write vague, generic documentation — be specific to this project's actual codebase and behavior
- DO NOT copy-paste from the PRD or architecture doc — write user-facing documentation in plain language
- DO NOT add placeholder "TODO" sections for Phase 2/3 features — keep docs focused on current state
- DO NOT over-document internal implementation details in the entity authoring guide — keep it practical and task-oriented
- DO NOT under-document the developer guide — it should give a developer enough context to modify any component
- DO NOT include Phase 2/3 CLI flags (`--entities`, `--compare`) — those don't exist yet
- DO NOT describe planned folders (Persistence/, Sanitization/, Fingerprinting/, Comparison/, Reporting/) as existing — mention them only as "planned for future phases" if relevant to extension points

### References

- [Source: epics.md#Story 2.2: Entity Authoring Guide & Developer Documentation]
- [Source: architecture.md#Project Structure & Boundaries]
- [Source: architecture.md#Implementation Patterns & Consistency Rules]
- [Source: architecture.md#Architectural Boundaries]
- [Source: architecture.md#Data Flow]
- [Source: architecture.md#DI Registration Pattern]
- [Source: architecture.md#Folder & Namespace Organization]
- [Source: architecture.md#Test Naming & Style Pattern]
- [Source: prd.md#Journey 5: New Developer -- Codebase Onboarding]
- [Source: prd.md#FR35: Entity definition authoring guide]
- [Source: prd.md#FR36: Developer documentation]
- [Source: 2-1-setup-guide-and-configuration-reference.md (previous story learnings)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Program.cs (DI setup, config loading)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs (CLI commands)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Pipeline/MigrationPipelineService.cs (orchestration)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Pipeline/IMigrationPipelineService.cs (pipeline interface)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/DependencySorting/SourceQueryCollection.cs (entity loading, UPPERCASE resolution)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/DependencySorting/TopologicalSort.cs (dependency ordering)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Services/SqlToXmlService.cs (SQL-to-XML conversion)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/XmlOutput/IXmlOutputFactory.cs (factory interface)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/XmlOutput/XmlFileOutputFactory.cs (file output)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/XmlOutput/XmlPackageFileOutputFactory.cs (ZIP package output)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/XmlOutput/XmlD365FnoOutputFactory.cs (D365FO import)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Erp/DataManagementDefinitionGroups/ (D365FO API)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Settings/*.cs (configuration POCOs)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Definition/CustCustomerBaseEntity/ (example entity definition)]
- [Source: Dynamics365ImportData/Dynamics365ImportData.Tests/ (test structure and patterns)]
- [Source: docs/setup-guide.md (cross-reference target)]
- [Source: docs/configuration-reference.md (cross-reference target)]
- [Source: README.md (update with new doc links)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.5 (claude-opus-4-5-20251101)

### Debug Log References

- All 54 existing tests pass (0 failures, 0 skipped) -- verified via `dotnet test`

### Completion Notes List

- Created `docs/entity-authoring-guide.md` (FR35) covering: entity naming conventions (including UPPERCASE resolution from SourceQueryCollection), directory structure, SQL extraction queries (aliasing, JOINs, NULL handling, filtering, TOP N, RecordsPerFile), Manifest XML format with EntityMap field mappings, PackageHeader XML format, dependency declaration with topological sorting, a complete worked example using VendVendorGroupEntity, and troubleshooting for common issues
- Created `docs/developer-guide.md` (FR36) covering: solution structure, all key components with responsibilities (Program.cs, CommandHandler, Pipeline, DependencySorting, Services, XmlOutput, Erp, Azure, Settings), end-to-end data flow with text-based diagram, DI registration patterns (exact code from Program.cs, service lifetimes, IOptions binding, IHttpClientFactory), pipeline service architecture (ADR-9, factory resolution, execution loop, status monitoring, fault isolation per NFR5), extension points (new output modes, CLI commands, configuration, processing logic, future phases), folder structure conventions (horizontal-slice, namespace matching, file naming, test project structure), test suite as living documentation (54 tests, categories, naming convention, key test files, how to add tests), and coding conventions (naming, Serilog structured logging, async patterns, error handling, prohibited patterns)
- Added "See Also" cross-reference sections to `docs/setup-guide.md` and `docs/configuration-reference.md`
- Updated `README.md` Documentation section to include links to all four docs
- All documentation uses actual code snippets and references from the source files, not generic examples
- UPPERCASE entity name resolution documented prominently per HIGH-priority code review finding from Story 2.1
- Output directory clearing behavior documented per HIGH-priority code review finding from Story 2.1
- No production code, test code, or configuration files were modified

### Change Log

- 2026-02-01: Created entity authoring guide and developer documentation, updated cross-references in existing docs (Story 2.2)
- 2026-02-01: Code review fixes -- 2 HIGH, 4 MEDIUM, 3 LOW issues found and fixed: corrected folder path casing in entity authoring guide directory tree, clarified ILogger<T> vs Serilog static API in developer guide, fixed README definition files casing, clarified DefinitionGroupId auto-generation uses uppercase, clarified CycleResult.TotalEntities counts output parts not entities, corrected fault isolation documentation to reflect actual behavior, fixed AzureContainer/Dynamics365Container descriptions, added Dynamics365Container to Erp section, clarified MaxDegreeOfParallelism is not yet wired to Parallel.ForEachAsync

### File List

**New files:**
- docs/entity-authoring-guide.md
- docs/developer-guide.md

**Modified files:**
- docs/setup-guide.md (added See Also cross-references)
- docs/configuration-reference.md (added See Also cross-references)
- README.md (added links to entity authoring guide and developer guide)
