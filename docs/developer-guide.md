# Developer Guide

This guide covers the codebase architecture, key components, data flow, and extension points for the D365FO Data Migration tool. It is intended for developers who need to understand, modify, or extend the tool.

## Audience

- Developers modifying or extending the migration tool
- Engineers adding new output modes, CLI commands, or processing logic

## Solution Structure

The repository contains a single Visual Studio solution with two projects:

```
d365fo-data-migration/
├── Dynamics365ImportData/
│   ├── Dynamics365ImportData.sln
│   ├── Dynamics365ImportData/           # Main project (net10.0)
│   │   ├── Program.cs
│   │   ├── CommandHandler.cs
│   │   ├── Azure/
│   │   ├── DependencySorting/
│   │   ├── Erp/
│   │   ├── Pipeline/
│   │   ├── Services/
│   │   ├── Settings/
│   │   ├── XmlOutput/
│   │   ├── Definition/
│   │   └── appsettings.json
│   └── Dynamics365ImportData.Tests/     # Test project (net10.0)
│       ├── Unit/
│       ├── Integration/
│       ├── Snapshot/
│       ├── Audit/
│       └── TestHelpers/
├── docs/
└── README.md
```

Both projects target `net10.0`. Each top-level folder in the main project represents one capability area, and the namespace matches the folder path (e.g., `Dynamics365ImportData.Pipeline`, `Dynamics365ImportData.DependencySorting`).

## Key Components

### Program.cs -- Entry Point

Responsibilities:
- Initializes Serilog bootstrap logger
- Builds the Cocona CLI application
- Loads configuration from multiple sources (see [DI Registration Patterns](#di-registration-patterns))
- Registers all services in the DI container
- Registers `CommandHandler` as the Cocona command source
- Runs the application

### CommandHandler.cs -- CLI Adapter

A thin CLI adapter class using Cocona `[Command]` attributes. Each method delegates to `IMigrationPipelineService` and performs minimal work:

| Command | Alias | Description |
|---------|-------|-------------|
| `export-file` | `f` | Exports SQL data to individual XML files |
| `export-package` | `p` | Exports SQL data to ZIP packages with manifests |
| `import-d365` | `i` | Exports data and imports directly into D365FO via DMF API |

The `export-file` and `export-package` commands call `ClearOutputDirectory()` before processing, which deletes top-level files in the output directory. The `import-d365` command does **not** clear the output directory.

Each command method follows the same pattern:
1. Log the operation start
2. Clear output directory (export commands only)
3. Call `_pipelineService.ExecuteAsync(mode, entityFilter, cancellationToken)`
4. Log completion

### Pipeline/

**`IMigrationPipelineService`** -- Interface defining the pipeline contract:

```csharp
Task<CycleResult> ExecuteAsync(
    PipelineMode mode,
    string[]? entityFilter,
    CancellationToken cancellationToken);
```

**`MigrationPipelineService`** -- Orchestrates entity processing:
1. Resolves the output factory based on `PipelineMode`
2. Iterates through dependency-sorted entity levels
3. Processes entities in parallel within each level using `Parallel.ForEachAsync`
4. Monitors D365FO import status for async operations (polling every 15 seconds)
5. Collects results into `CycleResult` (succeeded/failed counts)

**`PipelineMode`** -- Enum with values: `File`, `Package`, `D365`

**`CycleResult`** -- Result model with properties: `Command`, `TotalEntities` (counts output parts, not entities -- an entity split by `RecordsPerFile` produces multiple parts), `Succeeded`, `Failed`

### DependencySorting/

**`SourceQueryCollection`** -- Loads entity configuration from `ProcessSettings.Queries`, builds the dependency graph, performs topological sort, and produces `SortedQueries` (a `List<List<SourceQueryItem>>` where each inner list is a dependency level that can run in parallel).

Key behavior:
- Converts all entity names to **UPPERCASE** via `name.ToUpper()` in `GetEntityName()`
- Validates that all definition files exist at startup (fails fast on missing files)
- Detects duplicate entity names
- Falls back to current directory if `DefinitionDirectory` is not set
- Creates the output directory if it does not exist

**`SourceQueryItem`** -- Immutable record containing all resolved paths and settings for a single entity:

```csharp
public record SourceQueryItem(
    string EntityName,
    string DefinitionGroupId,
    string ManifestFileName,
    string OutputDirectory,
    string OutputBlobStorage,
    string PackageHeaderFileName,
    string QueryFileName,
    int RecordsPerFile,
    string SourceConnectionString,
    List<string> Dependencies);
```

**`TopologicalSort`** -- Represents a sorting solution as an enumerable of process sets (each set can execute concurrently).

**`DependencyGraph`** -- Directed acyclic graph representation. The `CalculateSort()` method implements Kahn's algorithm: iteratively selects processes with no unresolved predecessors, groups them into sets that can run concurrently, and resolves resource dependencies within each set.

### Services/SqlToXmlService.cs -- SQL to XML Conversion

Opens a SQL connection, reads the query file, executes it, and streams results into XML via `XmlWriter`:

1. Opens `SqlConnection` using the entity's connection string
2. Reads the SQL file from disk
3. Executes the query with a 1-hour command timeout
4. For each row: writes an XML element named after the entity, with each column as an attribute
5. Splits output into parts when `RecordsPerFile` threshold is reached
6. Logs progress every 100,000 records

The XML output structure:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Document>
  <ENTITYNAME FIELD1="value1" FIELD2="value2" />
  <ENTITYNAME FIELD1="value3" FIELD2="value4" />
</Document>
```

Row data is written as XML attributes, not child elements.

### XmlOutput/

**`IXmlOutputFactory`** -- Factory interface:

```csharp
Task<IXmlOutputPart> CreateAsync(SourceQueryItem queryItem, int part, CancellationToken cancellationToken);
```

**`IXmlOutputPart`** -- Output part interface providing `XmlWriter`, `PartName`, lifecycle methods (`Open()`, `Close()`, `PostWriteProcessAsync()`), and status querying (`GetStateAsync()`).

**`XmlFileOutputFactory`** -- Writes individual XML files to the output directory. File naming: `{DefinitionGroupId}_{timestamp}.xml` (with `_PartN_` suffix for multi-part files).

**`XmlPackageFileOutputFactory`** -- Creates ZIP packages containing `Manifest.xml`, `PackageHeader.xml`, and entity data XML. Extends `XmlPackageOutputFactoryBase`. File naming: `{DefinitionGroupId}_{timestamp}.zip`.

**`XmlD365FnoOutputFactory`** -- Uploads ZIP packages to Azure Blob Storage, triggers D365FO import via `ImportFromPackage` API, then polls `GetExecutionSummaryStatus` for completion. Polling occurs every 15 seconds with an iteration limit.

### Erp/

**`Dynamics365FnoClient`** -- Base HTTP client with Azure AD OAuth2 (MSAL) authentication. Uses `IHttpClientFactory` for HTTP client management.

**`Erp/DataManagementDefinitionGroups/`** -- D365FO DMF REST API operations:
- `GetAzureWriteUrl` -- Gets a writable Azure Blob URL from D365FO
- `ImportFromPackage` -- Triggers a DMF import job
- `GetExecutionSummaryStatus` -- Polls import job status
- Supporting models: `BlobDefinition`, `ExecutionIdRequest`, `ExecutionStatus`, `ImportFromPackageRequest`, `UniqueFileNameRequest`

**`Dynamics365Container`** -- Gets a writable Azure Blob URL from D365FO via the DMF API (`GetAzureWriteUrl`), then opens a write stream to that blob. Used by `XmlD365FnoOutputFactory` to upload ZIP packages.

**`ExecutionStatus`** -- Enum tracking import job states: `NotRun`, `Executing`, `Succeeded`, `Failed`, `PartiallySucceeded`, `Canceled`, `Unknown`

### Azure/

**`AzureContainer`** -- Azure Blob Storage operations using a SAS URL from `DestinationSettings.OutputBlobStorage`. Provides blob upload and delete capabilities.

### Settings/

Configuration POCOs bound via `IOptions<T>`:

| Class | Section | Key Properties |
|-------|---------|----------------|
| `SourceSettings` | `SourceSettings` | `SourceConnectionString` |
| `DestinationSettings` | `DestinationSettings` | `OutputDirectory`, `OutputBlobStorage` |
| `ProcessSettings` | `ProcessSettings` | `DefinitionDirectory`, `MaxDegreeOfParallelism`, `Queries` |
| `QuerySettings` | (per item in `Queries`) | `EntityName`, `DefinitionGroupId`, `RecordsPerFile`, `Dependencies`, `QueryFileName`, `ManifestFileName`, `PackageHeaderFileName`, `SourceConnectionString` |
| `Dynamics365Settings` | `Dynamics365Settings` | `Tenant`, `Url`, `ClientId`, `Secret`, `LegalEntityId`, `ImportTimeout` |

## Data Flow

### End-to-End Flow

```
Configuration Loading
        │
        ▼
Entity Loading & Dependency Sorting
  (SourceQueryCollection: load config, build graph, topological sort)
        │
        ▼
Parallel Entity Processing (per dependency level)
  (MigrationPipelineService: iterate sorted levels, Parallel.ForEachAsync)
        │
        ▼
SQL Extraction (per entity)
  (SqlToXmlService: open connection, execute query, stream rows)
        │
        ▼
XML Streaming (per entity)
  (SqlToXmlService → XmlWriter: row → element, columns → attributes)
        │
        ▼
Output Factory (per entity)
  (IXmlOutputFactory → IXmlOutputPart: write to target)
        │
        ├──► File Mode: XML files to OutputDirectory
        ├──► Package Mode: ZIP file (Manifest + PackageHeader + XML) to OutputDirectory
        └──► D365 Mode: ZIP → Azure Blob → DMF ImportFromPackage → Status Polling
```

### Detailed Stage Descriptions

**1. Configuration Loading** -- `Program.cs` loads configuration from five sources in priority order (see [Configuration Precedence](configuration-reference.md#configuration-precedence)).

**2. Entity Loading & Dependency Sorting** -- `SourceQueryCollection` constructor runs at DI resolution time. It reads all `QuerySettings`, converts entity names to uppercase, validates that definition files exist, builds a `DependencyGraph`, and produces `SortedQueries` -- a list of lists, where each inner list is a set of entities at the same dependency level.

**3. Parallel Entity Processing** -- `MigrationPipelineService.RunQueriesWithDependenciesAsync()` iterates through each dependency level in `SortedQueries`. Within each level, entities are processed concurrently using `Parallel.ForEachAsync` (currently using the default thread pool concurrency). The `MaxDegreeOfParallelism` property is exposed on `SourceQueryCollection` from configuration but is not yet passed as a `ParallelOptions` constraint to `Parallel.ForEachAsync`. Each level must complete before the next begins.

**4. SQL Extraction** -- `SqlToXmlService.ExportToOutput()` opens a `SqlConnection`, reads the SQL file, executes it with a `SqlDataReader`, and iterates through results.

**5. XML Streaming** -- For each row, the service writes an XML element (named after the entity) with each column as an attribute. The root element is `<Document>`. When the row count reaches `RecordsPerFile`, the current output part is closed and a new part starts.

**6. Output Target** -- The `IXmlOutputFactory` resolved by `PipelineMode` determines the output target:
- **File:** Writes `.xml` files directly to `OutputDirectory`
- **Package:** Creates `.zip` files containing `Manifest.xml`, `PackageHeader.xml`, and the entity data XML
- **D365:** Creates ZIP in Azure Blob Storage, calls `ImportFromPackage`, then polls `GetExecutionSummaryStatus` every 15 seconds until the import completes or times out (controlled by `ImportTimeout` in minutes)

## DI Registration Patterns

### Configuration Loading Order

From `Program.cs`:

```csharp
configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .AddCommandLine(args);
```

Sources are applied in order, with later sources overriding earlier ones:
1. `appsettings.json` (required)
2. `appsettings.{Environment}.json` (optional)
3. .NET User Secrets
4. Environment variables
5. Command-line arguments (highest priority)

### Service Lifetime Strategy

| Lifetime | Services | Reason |
|----------|----------|--------|
| **Transient** | `SqlToXmlService`, `MigrationPipelineService` | New instance per request; holds per-operation state |
| **Singleton** | `SourceQueryCollection`, `XmlFileOutputFactory`, `XmlPackageFileOutputFactory`, `XmlD365FnoOutputFactory` | Stateless or initialized once at startup |
| **HttpClient** | `IDynamics365FinanceDataManagementGroups` via `IHttpClientFactory` | Managed HTTP client lifecycle |

### IOptions\<T\> Binding

Settings POCOs are bound to configuration sections:

```csharp
services.AddOptions<SourceSettings>()
    .Bind(builder.Configuration.GetSection(nameof(SourceSettings)));
services.AddOptions<DestinationSettings>()
    .Bind(builder.Configuration.GetSection(nameof(DestinationSettings)));
services.AddOptions<ProcessSettings>()
    .Bind(builder.Configuration.GetSection(nameof(ProcessSettings)));
services.AddOptions<Dynamics365Settings>()
    .Bind(builder.Configuration.GetSection(nameof(Dynamics365Settings)));
```

### IHttpClientFactory Usage

The D365FO API client uses `IHttpClientFactory` for proper HTTP connection management:

```csharp
services.AddHttpClient<IDynamics365FinanceDataManagementGroups, Dynamics365FinanceDataManagementGroups>(
    (services, httpClient) => httpClient.Timeout = new TimeSpan(0,
        services.GetRequiredService<IOptions<Dynamics365Settings>>().Value.ImportTimeout,
        0));
```

### Adding New Services

To add a new service:
1. Create an interface in the appropriate folder (e.g., `IMyService.cs`)
2. Create the implementation (e.g., `MyService.cs`)
3. Register in `Program.cs` with the appropriate lifetime:

```csharp
services.AddTransient<IMyService, MyService>();
```

If the service needs configuration, create a settings POCO in `Settings/`, bind it in `Program.cs`, and inject via `IOptions<T>`.

## Pipeline Service Architecture

### ADR-9 Context

The `IMigrationPipelineService` interface was extracted from `CommandHandler` in Story 1.1 to enable testability and future extensibility. This separation means:

- `CommandHandler` is a thin CLI adapter with approximately 10 lines per command: parse arguments, call pipeline, handle errors
- All processing logic lives in `MigrationPipelineService`
- The pipeline can be tested independently via its interface

### Factory Resolution

`MigrationPipelineService.ResolveFactory()` maps `PipelineMode` to the correct output factory:

```csharp
return mode switch
{
    PipelineMode.File    => _provider.GetRequiredService<XmlFileOutputFactory>(),
    PipelineMode.Package => _provider.GetRequiredService<XmlPackageFileOutputFactory>(),
    PipelineMode.D365    => _provider.GetRequiredService<XmlD365FnoOutputFactory>(),
    _ => throw new ArgumentOutOfRangeException(...)
};
```

### Execution Loop

1. Resolve the `IXmlOutputFactory` based on the pipeline mode
2. Iterate through `SortedQueries` (dependency levels)
3. Within each level, `Parallel.ForEachAsync` processes entities concurrently
4. Each entity passes through `SqlToXmlService.ExportToOutput()`
5. After each level completes, `CheckTasksStatus()` monitors async operations

### Status Monitoring

For D365FO imports, after each dependency level completes, the pipeline enters a polling loop:
- Checks `GetStateAsync()` on each output part every **15 seconds**
- Terminal states: `Succeeded`, `Failed`, `PartiallySucceeded`, `Canceled`
- Non-terminal states: `Executing`, `NotRun`, `Unknown` (continue polling)
- Timeout: controlled by `ImportTimeout` setting (minutes)

### Fault Isolation

The pipeline provides fault isolation for **asynchronous D365FO import operations**: `CheckTasksStatus()` tracks individual part outcomes (succeeded, failed, partially succeeded, canceled) without aborting the overall run. Failed parts are counted in `CycleResult.Failed`.

**Current limitation:** For **synchronous failures** during SQL extraction or XML generation, exceptions thrown by `SqlToXmlService.ExportToOutput()` propagate through `Parallel.ForEachAsync` and may abort other entities within the same dependency level. Full per-entity catch-and-continue within the parallel loop (per NFR5) is planned for Phase 2 when result persistence is added.

The `CycleResult` provides accounting: `TotalEntities` (output parts), `Succeeded`, `Failed`.

## Extension Points

### Adding a New Output Mode

1. Create a new class implementing `IXmlOutputFactory` (and a corresponding `IXmlOutputPart` implementation)
2. Add a new value to the `PipelineMode` enum
3. Register the factory as Singleton in `Program.cs`
4. Add a case to `MigrationPipelineService.ResolveFactory()` for the new mode
5. Add a new command method in `CommandHandler` with a `[Command]` attribute

### Adding New CLI Commands

1. Add a method to `CommandHandler` with the `[Command("name", Aliases = new[] { "alias" })]` attribute
2. Inject any required services via the constructor
3. Delegate to the appropriate service (e.g., `_pipelineService.ExecuteAsync(...)`)
4. Follow the existing pattern: log start, execute, log completion

### Adding New Configuration Sections

1. Create a settings POCO in `Settings/` (e.g., `MyFeatureSettings.cs`)
2. Add the corresponding section to `appsettings.json`
3. Bind in `Program.cs`:

```csharp
services.AddOptions<MyFeatureSettings>()
    .Bind(builder.Configuration.GetSection(nameof(MyFeatureSettings)));
```

4. Inject via `IOptions<MyFeatureSettings>` in the consuming service

### Adding New Entity Processing Logic

- **Extraction changes:** Modify `SqlToXmlService` (e.g., different query execution, XML format changes)
- **Orchestration changes:** Modify `MigrationPipelineService` (e.g., pre/post-processing hooks, different parallelism strategies)
- **Output format changes:** Implement a new `IXmlOutputFactory` / `IXmlOutputPart` pair

### Future Extensibility

The architecture document identifies planned folders for Phase 2-3 features:
- `Persistence/` -- Result persistence and credential sanitization
- `Fingerprinting/` -- Error fingerprinting
- `Comparison/` -- Error comparison engine
- `Reporting/` -- Readiness report generation

These do not exist yet. New capabilities should follow the existing pattern: create a folder at the project root level, with matching namespace.

## Folder Structure Conventions

### Horizontal-Slice Organization

The project uses horizontal-slice folder structure where each folder represents one capability area:

| Folder | Responsibility |
|--------|---------------|
| `Azure/` | Azure Blob Storage operations |
| `DependencySorting/` | Entity loading, dependency graph, topological sort |
| `Erp/` | D365FO API client, DMF REST operations |
| `Pipeline/` | Pipeline service, mode enum, result model |
| `Services/` | SQL extraction and XML transformation |
| `Settings/` | Configuration POCOs |
| `XmlOutput/` | Output factories and output part implementations |
| `Definition/` | Entity definition files (SQL, Manifest, PackageHeader) |

### Namespace Convention

Namespace matches folder path:
- `Dynamics365ImportData.Pipeline`
- `Dynamics365ImportData.DependencySorting`
- `Dynamics365ImportData.Services`
- `Dynamics365ImportData.XmlOutput`
- `Dynamics365ImportData.Settings`
- `Dynamics365ImportData.Erp.DataManagementDefinitionGroups`

### File Naming

- One interface + one implementation per file
- Interface file: `I{ServiceName}.cs` (e.g., `IMigrationPipelineService.cs`)
- Implementation file: `{ServiceName}.cs` (e.g., `MigrationPipelineService.cs`)
- Settings POCO: `{SectionName}Settings.cs` (e.g., `Dynamics365Settings.cs`)

### Adding New Capabilities

New capabilities get their own folder at the project root level. Follow the existing patterns:
1. Create the folder (e.g., `Reporting/`)
2. Use the matching namespace (e.g., `Dynamics365ImportData.Reporting`)
3. Define an interface and implementation
4. Register in `Program.cs`

### Test Project Structure

The test project mirrors the main project with categorization:

```
Dynamics365ImportData.Tests/
├── Unit/                    # Algorithm correctness, isolated logic
│   ├── DependencySorting/
│   ├── Erp/
│   ├── Services/
│   ├── Settings/
│   └── XmlOutput/
├── Integration/             # Pipeline wiring, packaging
│   ├── Packaging/
│   └── Pipeline/
├── Snapshot/                # Golden-file output validation
├── Audit/                   # Credential leak prevention
└── TestHelpers/             # Shared fixtures and utilities
```

## Test Suite as Living Documentation

The test project at `Dynamics365ImportData/Dynamics365ImportData.Tests/` contains 54 tests that serve as living documentation for expected behavior.

### Running Tests

From the solution directory:

```bash
cd Dynamics365ImportData
dotnet test
```

All tests run without external service dependencies.

### Test Categories

| Category | Purpose | Example |
|----------|---------|---------|
| **Unit** | Algorithm correctness and isolated logic | Topological sort ordering, XML field type handling, configuration binding |
| **Integration** | Component interactions and wiring | Pipeline service contract, ZIP package structure |
| **Snapshot** | Golden-file output validation | XML output format verification |
| **Audit** | Security and compliance checks | Credential leak detection in output data |

### Test Naming Convention

Tests follow the pattern: `{Method}_{Scenario}_{ExpectedResult}`

Examples:
- `ExecuteAsync_FileMode_ReturnsExpectedEntityCounts`
- `ExecuteAsync_SingleEntityFailure_ContinuesProcessingRemaining`
- `SampleResultData_NoCredentialPatterns_ZeroMatches`
- `KnownCredentialPatterns_DetectedByRegex_AllPatternsMatch`

### Test Structure

All tests use Arrange/Act/Assert structure with [Shouldly](https://docs.shouldly.org/) assertions:

```csharp
[Fact]
public async Task ExecuteAsync_FileMode_ReturnsExpectedEntityCounts()
{
    // Arrange
    var mockPipeline = Substitute.For<IMigrationPipelineService>();
    mockPipeline.ExecuteAsync(PipelineMode.File, null, Arg.Any<CancellationToken>())
        .Returns(Task.FromResult(new CycleResult { ... }));

    // Act
    var result = await mockPipeline.ExecuteAsync(PipelineMode.File, null, CancellationToken.None);

    // Assert
    result.Command.ShouldBe("File");
    result.TotalEntities.ShouldBe(3);
}
```

### Key Test Files as Documentation

| Test File | Documents |
|-----------|-----------|
| `Unit/DependencySorting/TopologicalSortTests.cs` | Dependency resolution behavior, ordering guarantees |
| `Unit/XmlOutput/XmlGenerationTests.cs` | Expected XML output format |
| `Integration/Packaging/ZipPackageTests.cs` | Package structure requirements |
| `Integration/Pipeline/PipelineServiceTests.cs` | Pipeline contract: modes, filtering, fault isolation, cancellation |
| `Audit/CredentialLeakTests.cs` | Credential safety requirements and detection patterns |

### Adding New Tests

1. Place the test file in the appropriate category folder (`Unit/`, `Integration/`, `Snapshot/`, `Audit/`)
2. Follow the naming convention: `{Method}_{Scenario}_{ExpectedResult}`
3. Use `Shouldly` for assertions
4. Use Arrange/Act/Assert structure
5. Use `NSubstitute` for mocking (already referenced by the test project)
6. For tests requiring shared setup, use helpers from `TestHelpers/`

## Coding Conventions

### Naming

- **Classes/Methods:** PascalCase (e.g., `MigrationPipelineService`, `ExecuteAsync`)
- **Interfaces:** `I` prefix (e.g., `IMigrationPipelineService`, `IXmlOutputFactory`)
- **Local variables:** camelCase (e.g., `entityName`, `outputDirectory`)
- **Async methods:** `Async` suffix (e.g., `ExecuteAsync`, `CreateAsync`, `PostWriteProcessAsync`)
- **Nullable annotations:** Enabled project-wide; use `?` for nullable properties

### Logging

Use **structured message templates** via the `ILogger<T>` interface (backed by Serilog) -- not string interpolation:

```csharp
// Correct -- structured template with named properties via ILogger<T>
_logger.LogInformation("Processing entity {EntityName} with {RecordCount} records", entityName, count);

// Wrong -- string interpolation loses structured logging context
_logger.LogInformation($"Processing entity {entityName} with {count} records");
```

The codebase uses `Microsoft.Extensions.Logging.ILogger<T>` (not Serilog's static `Log` API). Serilog is wired as the logging provider via `UseSerilog()` in `Program.cs`, so all `ILogger<T>` calls flow through Serilog's structured pipeline.

### Async Patterns

- Propagate `CancellationToken` through all async method chains
- Accept `CancellationToken` as the last parameter in async methods
- Pass tokens to all `await` calls that accept them

### Error Handling

- Per-entity catch-and-continue: individual entity failures do not abort the entire run
- Never swallow exceptions silently -- always log before continuing
- Throw `ArgumentException` or `ArgumentNullException` for invalid inputs at startup
- Use `InvalidOperationException` for runtime state errors

### Prohibited Patterns

- No `Console.WriteLine` -- use Serilog for all output
- No static helper classes -- use DI-injected services
- No `new HttpClient()` -- use `IHttpClientFactory`

## See Also

- [Setup Guide](setup-guide.md) -- Installation and first-run instructions
- [Configuration Reference](configuration-reference.md) -- Complete reference for all `appsettings.json` options
- [Entity Authoring Guide](entity-authoring-guide.md) -- How to add new entity definitions
