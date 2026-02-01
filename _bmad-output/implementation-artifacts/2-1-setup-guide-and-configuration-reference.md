# Story 2.1: Setup Guide & Configuration Reference

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a new team member,
I want a setup guide and configuration reference,
So that I can install, configure, and run the tool from scratch without assistance.

## Acceptance Criteria

1. **Given** a new developer with no prior exposure to the tool, **When** they follow `docs/setup-guide.md`, **Then** the guide covers: prerequisites (.NET 10 SDK, SQL Server access, D365FO environment credentials), cloning the repository, building the solution, configuring `appsettings.json`, and running a first migration (FR33)
2. **And** the guide includes troubleshooting for common setup issues (connection failures, authentication errors)
3. **Given** a user needing to configure the tool, **When** they consult `docs/configuration-reference.md`, **Then** every `appsettings.json` option is documented with: description, data type, default value, and example (FR34)
4. **And** environment variable overrides and .NET User Secrets usage are documented (NFR3)
5. **And** CLI argument precedence over configuration file values is explained (FR20)

## Tasks / Subtasks

- [x] Task 1: Create `docs/` directory structure (AC: #1, #3)
  - [x]1.1 Create `docs/` directory at repository root (`D:\d365fo-data-migration\docs\`)
  - [x]1.2 Verify directory is tracked by git (not in `.gitignore`)

- [x] Task 2: Create `docs/setup-guide.md` (AC: #1, #2)
  - [x]2.1 Write Prerequisites section: .NET 10 SDK (10.0.x), SQL Server access, D365FO environment with Azure AD app registration, Git
  - [x]2.2 Write Installation section: clone repo, `cd Dynamics365ImportData`, `dotnet build`, verify build succeeds
  - [x]2.3 Write Configuration section: copy and customize `appsettings.json`, reference `docs/configuration-reference.md` for full details
  - [x]2.4 Write First Run section: step-by-step for `export-file` command with a single entity, verify XML output in OutputDirectory
  - [x]2.5 Write Running Commands section: cover all three commands (`export-file`/`f`, `export-package`/`p`, `import-d365`/`i`) with examples
  - [x]2.6 Write Running Tests section: `dotnet test` from solution directory, expected 54 passing tests
  - [x]2.7 Write Credential Management section: .NET User Secrets setup (`dotnet user-secrets init`, `dotnet user-secrets set`), environment variable overrides (NFR3)
  - [x]2.8 Write Troubleshooting section: common issues with resolutions:
    - SQL Server connection failures (trusted connection vs SQL auth, firewall, named instances)
    - D365FO authentication errors (Azure AD app permissions, tenant mismatch, expired secret)
    - Build failures (wrong SDK version, missing workloads)
    - `Encrypt=true` default in Microsoft.Data.SqlClient (trust server certificate configuration)
    - Entity definition not found (DefinitionDirectory path, entity folder naming convention)

- [x] Task 3: Create `docs/configuration-reference.md` (AC: #3, #4, #5)
  - [x]3.1 Write introduction: explain `appsettings.json` is the primary configuration file, loaded from working directory
  - [x]3.2 Document `SourceSettings` section:
    - `SourceConnectionString` (string, no default, required) -- global SQL Server connection string
  - [x]3.3 Document `DestinationSettings` section:
    - `OutputDirectory` (string, no default, required) -- local filesystem path for output files
    - `OutputBlobStorage` (string, empty = disabled) -- Azure Blob Storage container URL (used by `import-d365` mode)
  - [x]3.4 Document `Dynamics365Settings` section:
    - `Tenant` (string, required) -- Azure AD tenant domain (e.g., `contoso.onmicrosoft.com`)
    - `Url` (URI, required) -- D365FO environment URL (e.g., `https://env.operations.dynamics.com`)
    - `ClientId` (string, required) -- Azure AD application (client) ID
    - `Secret` (string, required) -- Azure AD client secret (**store in User Secrets or env var**)
    - `LegalEntityId` (string, required) -- D365FO legal entity / company code (e.g., `USMF`)
    - `ImportTimeout` (integer, default: 60) -- minutes to wait for D365FO import completion before timeout
  - [x]3.5 Document `ProcessSettings` section:
    - `DefinitionDirectory` (string, required) -- filesystem path to entity definition folders
    - `MaxDegreeOfParallelism` (integer, required) -- maximum concurrent entity processing threads
    - `Queries` (array of QuerySettings, required) -- entity definitions to process
  - [x]3.6 Document `QuerySettings` (each item in `Queries` array):
    - `EntityName` (string, required) -- D365FO data entity name (e.g., `CustCustomerV3Entity`)
    - `DefinitionGroupId` (string, required) -- DMF definition group ID in D365FO (e.g., `Datamig_CustCustomerV3Entity`)
    - `ManifestFileName` (string, default: empty = auto-resolved to `{DefinitionDirectory}/{EntityName}/Manifest.xml`)
    - `PackageHeaderFileName` (string, default: empty = auto-resolved to `{DefinitionDirectory}/{EntityName}/PackageHeader.xml`)
    - `QueryFileName` (string, default: empty = auto-resolved to `{DefinitionDirectory}/{EntityName}/{ENTITYNAME}.sql`)
    - `RecordsPerFile` (integer, required) -- max records per output XML file (for large dataset splitting)
    - `SourceConnectionString` (string, default: empty = uses global `SourceSettings.SourceConnectionString`)
    - `Dependencies` (array of strings, default: empty) -- entity names that must be processed before this entity
  - [x]3.7 Document `Serilog` section: explain log level configuration, console sink, output template; reference Serilog docs for advanced configuration
  - [x]3.8 Write Configuration Precedence section (FR20): explain the layering order:
    1. `appsettings.json` (base)
    2. `appsettings.{Environment}.json` (environment override)
    3. .NET User Secrets (development secrets, NFR3)
    4. Environment variables
    5. Command-line arguments (highest priority)
  - [x]3.9 Write Environment Variable Overrides section: explain `__` (double underscore) separator convention for nested settings (e.g., `SourceSettings__SourceConnectionString`), document key variables
  - [x]3.10 Write User Secrets section: `dotnet user-secrets init`, `dotnet user-secrets set "Dynamics365Settings:Secret" "value"`, explain when and why to use (NFR3, NFR4)
  - [x]3.11 Write Complete Example section: full `appsettings.json` with all sections populated with realistic placeholder values and inline comments explaining each field

- [x] Task 4: Update README.md (AC: #1)
  - [x]4.1 Update Prerequisites to reference .NET 10 SDK (currently says .NET 8)
  - [x]4.2 Add "Documentation" section linking to `docs/setup-guide.md` and `docs/configuration-reference.md`
  - [x]4.3 Keep README concise -- point to docs/ for detailed information rather than duplicating

## Dev Notes

### Purpose

This is the first documentation story in Epic 2. It creates the `docs/` directory structure and the two foundational documents: a setup guide for new users and a comprehensive configuration reference. These documents address FR33 (setup guide) and FR34 (configuration reference), with NFR3 (secret management) and FR20 (CLI precedence) woven into the configuration reference. This is Step 8 in the ADR-3 Phase 1 Implementation Sequence -- the documentation step completing Phase 1.

### Previous Story Intelligence

**Epic 1 Summary (all 5 stories done):**
- Story 1.1: Test project created, `IMigrationPipelineService` extracted from `CommandHandler` (ADR-9)
- Story 1.2: 20 characterization tests on .NET 8 baseline
- Story 1.3: .NET 10 upgrade -- both `.csproj` files target `net10.0`, all `Microsoft.Extensions.*` bumped to 10.x
- Story 1.4: `System.Data.SqlClient` replaced with `Microsoft.Data.SqlClient` 6.1.4 -- **important for docs: `Encrypt=true` is now the default behavior**
- Story 1.5: Test suite expanded to 54 tests (unit, integration, snapshot, audit)

**Key learnings for this story:**
- The project now targets **.NET 10** (not .NET 8 as README currently states) -- setup guide must reference correct SDK
- `Microsoft.Data.SqlClient` has `Encrypt=true` default -- troubleshooting section must cover this (users may need `TrustServerCertificate=true` for dev environments)
- 54 tests pass via `dotnet test` -- setup guide should include test verification step
- `InternalsVisibleTo` is configured for the test project -- developer guide (Story 2.2) topic, not this story
- xUnit v3 3.2.2 requires `xunit.runner.visualstudio` 3.1.5 -- not relevant for user-facing docs

### Git Intelligence

**Recent commits (Epic 1):**
- `76def33` - Story 1.5: Full test suite expansion
- `8081a06` - Story 1.4: SqlClient migration
- `710e620` - Story 1.3: .NET 10 upgrade
- `79e26d1` - Story 1.2: Characterization tests on .NET 8 baseline
- `606478a` - Story 1.1: Project setup and pipeline extraction

**Current project state post-Epic 1:**
- Solution: `Dynamics365ImportData.sln` with 2 projects
- Main project: `Dynamics365ImportData/Dynamics365ImportData/`
- Test project: `Dynamics365ImportData/Dynamics365ImportData.Tests/`
- Both target `net10.0`
- No `docs/` directory exists -- this story creates it

### Project Structure Notes

**Current structure (relevant to docs):**
```
d365fo-data-migration/
├── Dynamics365ImportData/
│   ├── Dynamics365ImportData.sln
│   ├── Dynamics365ImportData/          (main project)
│   │   ├── appSettings.json           (primary config file)
│   │   ├── Program.cs                 (entry point, DI, config loading)
│   │   ├── CommandHandler.cs          (CLI commands: export-file, export-package, import-d365)
│   │   ├── Settings/                  (config POCOs)
│   │   │   ├── SourceSettings.cs
│   │   │   ├── DestinationSettings.cs
│   │   │   ├── Dynamics365Settings.cs
│   │   │   ├── ProcessSettings.cs
│   │   │   └── QuerySettings.cs
│   │   ├── Pipeline/                  (orchestration)
│   │   ├── DependencySorting/         (topological sort)
│   │   ├── Services/                  (SQL-to-XML)
│   │   ├── XmlOutput/                 (output factories)
│   │   ├── Erp/                       (D365FO API client)
│   │   ├── Azure/                     (blob storage)
│   │   └── Definition/                (entity definitions)
│   └── Dynamics365ImportData.Tests/   (test project, 54 tests)
├── README.md                          (needs update: still says .NET 8)
└── docs/                              (TO BE CREATED by this story)
    ├── setup-guide.md                 (FR33)
    └── configuration-reference.md     (FR34)
```

**No conflicts with existing structure.** The `docs/` directory is a new addition at the repository root level, consistent with the architecture document's planned structure.

### Architecture Compliance

**This is a documentation-only story. No code changes to production or test projects.**

**Architecture references:**
- FR33: "A setup guide enables a new user to install, configure, and run the tool from scratch"
- FR34: "A configuration reference documents all appsettings.json options with descriptions, types, defaults, and examples"
- NFR3: "Sensitive configuration values must be supported via .NET User Secrets or environment variables"
- NFR4: "The tool must not introduce new credential storage mechanisms -- continue using .NET's established secret management patterns"
- FR20: "CLI arguments take precedence over appsettings.json values when both are specified"

**Configuration loading order (from `Program.cs`):**
```csharp
configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false)          // 1. Base config (required)
    .AddJsonFile($"appsettings.{env}.json", true)     // 2. Environment override (optional)
    .AddUserSecrets<Program>()                         // 3. User Secrets
    .AddEnvironmentVariables()                         // 4. Environment variables
    .AddCommandLine(args);                             // 5. CLI args (highest priority)
```

This is the actual precedence chain to document in the configuration reference.

### Configuration Schema (Complete Reference)

**SourceSettings:**
| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `SourceConnectionString` | string | null | Yes | SQL Server connection string for source data extraction |

**DestinationSettings:**
| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `OutputDirectory` | string | null | Yes | Local filesystem path where output files are written |
| `OutputBlobStorage` | string | null | No | Azure Blob Storage container URL (used in `import-d365` mode) |

**Dynamics365Settings:**
| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `Tenant` | string | null | Yes | Azure AD tenant domain |
| `Url` | URI | null | Yes | D365FO environment base URL |
| `ClientId` | string | null | Yes | Azure AD application (client) ID |
| `Secret` | string | null | Yes | Azure AD client secret (use User Secrets!) |
| `LegalEntityId` | string | null | Yes | D365FO legal entity / company code |
| `ImportTimeout` | int | 60 | No | Minutes to wait for import completion |

**ProcessSettings:**
| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `DefinitionDirectory` | string | null | Yes | Path to entity definition folders |
| `MaxDegreeOfParallelism` | int | 0 | Yes | Max concurrent entity processing threads |
| `Queries` | List\<QuerySettings\> | null | Yes | Array of entity configurations |

**QuerySettings (each entry in `Queries`):**
| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `EntityName` | string | null | Yes | D365FO data entity name |
| `DefinitionGroupId` | string | null | Yes | DMF definition group ID |
| `ManifestFileName` | string | "" | No | Custom manifest path (auto-resolved if empty) |
| `PackageHeaderFileName` | string | "" | No | Custom package header path (auto-resolved if empty) |
| `QueryFileName` | string | "" | No | Custom SQL query path (auto-resolved if empty) |
| `RecordsPerFile` | int | 0 | Yes | Max records per output XML file |
| `SourceConnectionString` | string | "" | No | Per-entity SQL connection override |
| `Dependencies` | List\<string\> | [] | No | Entity names that must process first |

### File Structure Requirements

**New files to create:**
```
docs/
├── setup-guide.md           (FR33 -- NEW)
└── configuration-reference.md (FR34 -- NEW)
```

**Existing files to modify:**
```
README.md                    (update .NET version, add docs links)
```

**DO NOT create or modify:**
- Any `.cs` files
- Any `.csproj` files
- `appsettings.json`
- Test files

### Library & Framework Requirements

No library or framework changes. This is a documentation-only story.

### Critical Guardrails

1. **DO NOT modify any production code** -- this story creates documentation files only.
2. **DO NOT modify any test code** -- no test changes needed.
3. **DO NOT modify `appsettings.json`** -- document the existing configuration, don't change it.
4. **DO NOT include real credentials** in any documentation examples -- use clear placeholders (e.g., `your-tenant.onmicrosoft.com`, `your-client-secret`).
5. **DO NOT duplicate the full README content** in docs -- the setup guide and config reference should be standalone documents, with the README linking to them.
6. **DO reference the correct .NET version** -- the project targets .NET 10 (not .NET 8 as the current README states).
7. **DO document `Microsoft.Data.SqlClient`'s `Encrypt=true` default** in the troubleshooting section -- this is the active SQL client library after Story 1.4.
8. **DO document the actual configuration precedence** from `Program.cs` -- not a generic .NET description.
9. **DO verify `docs/` path is relative to repository root** (not inside the `Dynamics365ImportData/` solution directory).
10. **DO write documentation in English** per `document_output_language` config.

### Anti-Patterns to Avoid

- DO NOT write vague, generic documentation -- be specific to this project's actual configuration and behavior
- DO NOT copy-paste from the PRD or architecture doc -- write user-facing documentation in plain language
- DO NOT add placeholder "TODO" sections for Story 2.2 content (entity authoring guide, developer guide) -- keep each doc self-contained
- DO NOT over-document internal implementation details (DI registration, service lifetimes) -- those belong in the developer guide (Story 2.2)
- DO NOT include Phase 2/3 CLI flags (`--entities`, `--compare`) -- those don't exist yet

### References

- [Source: epics.md#Story 2.1: Setup Guide & Configuration Reference]
- [Source: architecture.md#Project Structure & Boundaries]
- [Source: architecture.md#Configuration Section Pattern]
- [Source: architecture.md#Implementation Patterns & Consistency Rules]
- [Source: prd.md#Journey 5: New Developer -- Codebase Onboarding]
- [Source: prd.md#CLI Tool Specific Requirements]
- [Source: prd.md#FR33: Setup guide]
- [Source: prd.md#FR34: Configuration reference]
- [Source: prd.md#NFR3: Sensitive configuration via User Secrets / env vars]
- [Source: prd.md#FR20: CLI argument precedence]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Program.cs (configuration loading chain)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/Settings/*.cs (all settings classes)]
- [Source: Dynamics365ImportData/Dynamics365ImportData/CommandHandler.cs (CLI commands)]
- [Source: README.md (current documentation baseline)]
- [Source: 1-3-dotnet-10-upgrade.md (confirmed .NET 10 target)]
- [Source: 1-4-sqlclient-migration.md (Microsoft.Data.SqlClient Encrypt=true default)]
- [Source: 1-5-full-test-suite-expansion.md (54 tests for setup verification)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.5 (claude-opus-4-5-20251101)

### Debug Log References

No debug issues encountered. Documentation-only story with no code changes.

### Completion Notes List

- Task 1: Created `docs/` directory at repository root. Verified not in `.gitignore`.
- Task 2: Created `docs/setup-guide.md` covering all AC requirements: prerequisites (.NET 10 SDK, SQL Server, D365FO credentials), installation (clone, build, test), configuration (appsettings.json with cross-reference to config ref), first run walkthrough (export-file with single entity), all three commands with aliases, test verification (54 tests), credential management (.NET User Secrets + env vars per NFR3), and troubleshooting (SQL connection failures, Encrypt=true default from Microsoft.Data.SqlClient, D365FO auth errors, build failures, entity definition not found).
- Task 3: Created `docs/configuration-reference.md` documenting every `appsettings.json` option with description, type, default, and examples: SourceSettings, DestinationSettings, Dynamics365Settings, ProcessSettings, QuerySettings, Serilog. Includes Configuration Precedence section (FR20) documenting the actual 5-layer loading order from Program.cs, Environment Variable Overrides section with `__` separator convention, User Secrets section (NFR3/NFR4), and a complete example appsettings.json with realistic placeholders.
- Task 4: Updated README.md -- changed .NET 8 references to .NET 10 (description and prerequisites), added "Documentation" section linking to both new docs.
- All 54 existing tests pass with no regressions.

### Change Log

- 2026-02-01: Story 2.1 implementation complete. Created docs/setup-guide.md (FR33), docs/configuration-reference.md (FR34), updated README.md with .NET 10 references and docs links.
- 2026-02-01: Code review fixes applied (2 HIGH, 4 MEDIUM). Fixed entity folder/file naming to reflect uppercase resolution from SourceQueryCollection.cs, corrected output directory clearing scope (only export commands), fixed config precedence code snippet to match actual Program.cs, added TrustServerCertificate=True to README connection string example, corrected OutputDirectory description accuracy.

### File List

- `docs/setup-guide.md` (new)
- `docs/configuration-reference.md` (new)
- `README.md` (modified)
