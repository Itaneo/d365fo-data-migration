# Configuration Reference

This document provides a complete reference for all configuration options in the D365FO Data Migration tool. The primary configuration file is `appsettings.json`, located in the `Dynamics365ImportData/Dynamics365ImportData/` directory.

## SourceSettings

SQL Server connection configuration for source data extraction.

| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `SourceConnectionString` | string | *(none)* | Yes | SQL Server connection string for the source database. Supports Trusted Connection (Windows auth) or SQL authentication. |

**Example:**

```json
"SourceSettings": {
  "SourceConnectionString": "Server=your-server;Database=AxDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

> **Note:** The tool uses `Microsoft.Data.SqlClient`, which defaults to `Encrypt=true`. For development environments without a trusted TLS certificate, add `TrustServerCertificate=True` to the connection string. See the [Setup Guide troubleshooting section](setup-guide.md#encrypttrue-default-microsoftdatasqlclient) for details.

## DestinationSettings

Output location configuration for generated files and packages.

| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `OutputDirectory` | string | *(none)* | Yes | Local filesystem path where output XML files and packages are written. The `export-file` and `export-package` commands delete top-level files in this directory before starting; `import-d365` does not. |
| `OutputBlobStorage` | string | *(empty)* | No | Azure Blob Storage container URL. Used by the `import-d365` command to upload packages to D365FO. Leave empty to disable. |

**Example:**

```json
"DestinationSettings": {
  "OutputDirectory": "C:\\DataMigration\\Output\\",
  "OutputBlobStorage": ""
}
```

## Dynamics365Settings

Azure AD and D365FO environment configuration for direct import operations.

| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `Tenant` | string | *(none)* | Yes | Azure AD tenant domain (e.g., `contoso.onmicrosoft.com`). |
| `Url` | URI | *(none)* | Yes | D365FO environment base URL (e.g., `https://your-env.operations.dynamics.com`). |
| `ClientId` | string | *(none)* | Yes | Azure AD application (client) ID from App Registration. |
| `Secret` | string | *(none)* | Yes | Azure AD client secret. **Store in User Secrets or environment variable -- do not put in appsettings.json.** |
| `LegalEntityId` | string | *(none)* | Yes | D365FO legal entity / company code (e.g., `USMF`). |
| `ImportTimeout` | integer | `60` | No | Minutes to wait for a D365FO import job to complete before timing out. |

**Example:**

```json
"Dynamics365Settings": {
  "Tenant": "contoso.onmicrosoft.com",
  "Url": "https://your-env.operations.dynamics.com",
  "ClientId": "your-azure-ad-app-client-id",
  "Secret": "in secret store",
  "LegalEntityId": "USMF",
  "ImportTimeout": 60
}
```

> **Security:** The `Secret` value should always be stored securely. Use [.NET User Secrets](#user-secrets) for development or [Environment Variables](#environment-variable-overrides) for deployment. See the [Setup Guide credential management section](setup-guide.md#credential-management) for step-by-step instructions.

## ProcessSettings

Entity processing configuration controlling which entities to extract and how.

| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `DefinitionDirectory` | string | *(none)* | Yes | Filesystem path to the root folder containing entity definition subfolders. Each entity must have its own folder with SQL, Manifest, and PackageHeader files. |
| `MaxDegreeOfParallelism` | integer | *(none)* | Yes | Maximum number of concurrent entity processing threads. Set to `1` for sequential processing. |
| `Queries` | array of [QuerySettings](#querysettings) | *(none)* | Yes | List of entity configurations to process. At least one entry is required. |

**Example:**

```json
"ProcessSettings": {
  "DefinitionDirectory": "C:\\DataMigration\\Definition",
  "MaxDegreeOfParallelism": 32,
  "Queries": [
    {
      "EntityName": "CustCustomerV3Entity",
      "DefinitionGroupId": "Datamig_CustCustomerV3Entity",
      "RecordsPerFile": 200000,
      "QueryFileName": "",
      "Dependencies": ["smmContactPersonV2Entity"]
    }
  ]
}
```

## QuerySettings

Each item in the `ProcessSettings.Queries` array defines a single entity to process.

| Property | Type | Default | Required | Description |
|----------|------|---------|----------|-------------|
| `EntityName` | string | *(none)* | Yes | D365FO data entity name (e.g., `CustCustomerV3Entity`). Converted to uppercase internally and used as the folder name under `DefinitionDirectory` (e.g., `CUSTCUSTOMERV3ENTITY/`). |
| `DefinitionGroupId` | string | *(none)* | Yes | DMF definition group ID in D365FO (e.g., `Datamig_CustCustomerV3Entity`). Must match the definition group configured in the target D365FO environment. |
| `ManifestFileName` | string | *(empty)* | No | Custom path to the manifest XML file. If empty, auto-resolves to `{DefinitionDirectory}/{ENTITYNAME}/Manifest.xml`. |
| `PackageHeaderFileName` | string | *(empty)* | No | Custom path to the package header XML file. If empty, auto-resolves to `{DefinitionDirectory}/{ENTITYNAME}/PackageHeader.xml`. |
| `QueryFileName` | string | *(empty)* | No | Custom path to the SQL query file. If empty, auto-resolves to `{DefinitionDirectory}/{ENTITYNAME}/{ENTITYNAME}.sql` (the tool uppercases entity names internally). |
| `RecordsPerFile` | integer | *(none)* | Yes | Maximum number of records per output XML file. Use this to split large datasets across multiple files. |
| `SourceConnectionString` | string | *(empty)* | No | Per-entity SQL connection string override. If empty, uses the global `SourceSettings.SourceConnectionString`. |
| `Dependencies` | array of strings | *(empty)* | No | List of entity names that must be processed before this entity. The tool uses topological sorting to resolve the processing order. |

**Example with dependencies:**

```json
{
  "EntityName": "CustomerBankAccountEntity",
  "DefinitionGroupId": "Datamig_CustomerBankAccountEntity",
  "ManifestFileName": "",
  "PackageHeaderFileName": "",
  "RecordsPerFile": 200000,
  "QueryFileName": "",
  "SourceConnectionString": "",
  "Dependencies": ["CustCustomerV3Entity"]
}
```

## Serilog

The tool uses [Serilog](https://serilog.net/) for structured logging. Logging is configured in the `Serilog` section of `appsettings.json`.

| Property | Description |
|----------|-------------|
| `Using` | Serilog sink assemblies to load (e.g., `["Serilog.Sinks.Console"]`). |
| `MinimumLevel.Default` | Default minimum log level (`Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`). |
| `MinimumLevel.Override` | Per-namespace log level overrides (e.g., `"Microsoft": "Warning"`). |
| `WriteTo` | Sink configuration array defining where log output is written. |

**Default configuration:**

```json
"Serilog": {
  "Using": ["Serilog.Sinks.Console"],
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "System": "Error"
    }
  },
  "WriteTo": [
    {
      "Name": "Async",
      "Args": {
        "configure": [
          {
            "Name": "Console",
            "Args": {
              "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
              "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            }
          }
        ]
      }
    }
  ]
}
```

For advanced Serilog configuration options, see the [Serilog documentation](https://github.com/serilog/serilog/wiki/Configuration-Basics).

## Configuration Precedence

The tool loads configuration from multiple sources. Each source overrides the previous one, with later sources taking highest priority:

| Priority | Source | Description |
|----------|--------|-------------|
| 1 (lowest) | `appsettings.json` | Base configuration file (required, loaded from working directory). |
| 2 | `appsettings.{Environment}.json` | Environment-specific override (optional). The environment is determined by the `ASPNETCORE_ENVIRONMENT` variable, defaulting to `Production`. |
| 3 | .NET User Secrets | Development-time secret storage (see [User Secrets](#user-secrets)). |
| 4 | Environment variables | System or process environment variables (see [Environment Variable Overrides](#environment-variable-overrides)). |
| 5 (highest) | Command-line arguments | Arguments passed via `dotnet run -- --key=value` take the highest priority. |

This means a value set via a command-line argument will always override the same value set in `appsettings.json`, User Secrets, or environment variables.

**Configuration loading code** (from `Program.cs`):

```csharp
configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .AddCommandLine(args);
```

## Environment Variable Overrides

Any configuration value can be overridden using environment variables. Use double underscores (`__`) as separators for nested settings, matching the JSON hierarchy.

**Naming convention:**

```
{SectionName}__{PropertyName}
```

**Examples:**

| Environment Variable | Overrides |
|----------------------|-----------|
| `SourceSettings__SourceConnectionString` | `SourceSettings.SourceConnectionString` |
| `DestinationSettings__OutputDirectory` | `DestinationSettings.OutputDirectory` |
| `Dynamics365Settings__Secret` | `Dynamics365Settings.Secret` |
| `Dynamics365Settings__Tenant` | `Dynamics365Settings.Tenant` |
| `Dynamics365Settings__ImportTimeout` | `Dynamics365Settings.ImportTimeout` |
| `ProcessSettings__MaxDegreeOfParallelism` | `ProcessSettings.MaxDegreeOfParallelism` |
| `ProcessSettings__DefinitionDirectory` | `ProcessSettings.DefinitionDirectory` |

**Setting environment variables:**

```bash
# Windows (PowerShell)
$env:Dynamics365Settings__Secret = "your-client-secret"
$env:SourceSettings__SourceConnectionString = "Server=prod;Database=AxDb;Trusted_Connection=True;"

# Windows (Command Prompt)
set Dynamics365Settings__Secret=your-client-secret

# Linux/macOS
export Dynamics365Settings__Secret="your-client-secret"
```

> **Note:** Array-type settings (like `ProcessSettings.Queries`) are not practical to override via environment variables. Use `appsettings.json` or environment-specific JSON files for array configurations.

## User Secrets

.NET User Secrets provide a secure way to store sensitive configuration values during development. Secrets are stored outside the project directory (in your user profile) and are never committed to source control.

**Initialize User Secrets** (one-time, from the main project directory):

```bash
cd Dynamics365ImportData/Dynamics365ImportData
dotnet user-secrets init
```

**Set secret values:**

```bash
dotnet user-secrets set "Dynamics365Settings:Secret" "your-client-secret-value"
dotnet user-secrets set "Dynamics365Settings:ClientId" "your-client-id"
dotnet user-secrets set "SourceSettings:SourceConnectionString" "Server=dev;Database=AxDb;User Id=sa;Password=secret;TrustServerCertificate=True;"
```

> **Note:** User Secrets use `:` (colon) as the separator, not `__` (double underscore). This is the standard .NET User Secrets convention.

**List stored secrets:**

```bash
dotnet user-secrets list
```

**Remove a secret:**

```bash
dotnet user-secrets remove "Dynamics365Settings:Secret"
```

**When to use User Secrets:**

- Development environments where you need real credentials to test against actual databases or D365FO environments
- Any value you do not want to risk committing to source control (passwords, client secrets, connection strings with credentials)

## Complete Example

Below is a complete `appsettings.json` with all sections populated using realistic placeholder values:

```json
{
  "SourceSettings": {
    "SourceConnectionString": "Server=your-sql-server;Database=AxDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "DestinationSettings": {
    "OutputDirectory": "C:\\DataMigration\\Output\\",
    "OutputBlobStorage": ""
  },
  "Dynamics365Settings": {
    "Tenant": "contoso.onmicrosoft.com",
    "Url": "https://your-env.operations.dynamics.com",
    "ClientId": "00000000-0000-0000-0000-000000000000",
    "Secret": "in secret store",
    "LegalEntityId": "USMF",
    "ImportTimeout": 60
  },
  "ProcessSettings": {
    "DefinitionDirectory": "C:\\DataMigration\\Definition",
    "MaxDegreeOfParallelism": 4,
    "Queries": [
      {
        "EntityName": "smmContactPersonV2Entity",
        "DefinitionGroupId": "Datamig_smmContactPersonV2Entity",
        "ManifestFileName": "",
        "PackageHeaderFileName": "",
        "RecordsPerFile": 200000,
        "QueryFileName": "",
        "SourceConnectionString": "",
        "Dependencies": []
      },
      {
        "EntityName": "CustCustomerV3Entity",
        "DefinitionGroupId": "Datamig_CustCustomerV3Entity",
        "ManifestFileName": "",
        "PackageHeaderFileName": "",
        "RecordsPerFile": 200000,
        "QueryFileName": "",
        "SourceConnectionString": "",
        "Dependencies": ["smmContactPersonV2Entity"]
      },
      {
        "EntityName": "CustomerBankAccountEntity",
        "DefinitionGroupId": "Datamig_CustomerBankAccountEntity",
        "ManifestFileName": "",
        "PackageHeaderFileName": "",
        "RecordsPerFile": 200000,
        "QueryFileName": "",
        "SourceConnectionString": "",
        "Dependencies": ["CustCustomerV3Entity"]
      }
    ]
  },
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Error"
      }
    },
    "WriteTo": [
      {
        "Name": "Async",
        "Args": {
          "configure": [
            {
              "Name": "Console",
              "Args": {
                "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
                "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
              }
            }
          ]
        }
      }
    ]
  }
}
```

This example shows three entities with a dependency chain: `smmContactPersonV2Entity` is processed first, then `CustCustomerV3Entity` (which depends on contacts), and finally `CustomerBankAccountEntity` (which depends on customers). The tool resolves this order automatically using topological sorting.

## See Also

- [Setup Guide](setup-guide.md) -- Installation, first-run instructions, and troubleshooting
- [Entity Authoring Guide](entity-authoring-guide.md) -- How to add new entity definitions (SQL queries, manifests, dependencies)
- [Developer Guide](developer-guide.md) -- Codebase architecture, key components, and extension points
