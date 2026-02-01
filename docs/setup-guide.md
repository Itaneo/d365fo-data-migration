# Setup Guide

This guide walks you through installing, configuring, and running the D365FO Data Migration tool from scratch.

## Prerequisites

Before you begin, ensure you have the following:

- **.NET 10 SDK** (10.0.x) -- [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Git** -- for cloning the repository
- **SQL Server access** -- a source database to extract data from (SQL Server with TCP/IP enabled)
- **D365FO environment** (required only for `import-d365` command):
  - Azure AD tenant with an App Registration
  - Client ID and Client Secret for the registered application
  - The application must have appropriate permissions to access the D365FO Data Management API

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/Itaneo/d365fo-data-migration.git
cd d365fo-data-migration
```

### 2. Build the Solution

```bash
cd Dynamics365ImportData
dotnet build
```

You should see `Build succeeded` with 0 errors. If the build fails, see [Troubleshooting](#troubleshooting).

### 3. Verify the Installation

Run the test suite to confirm everything is working:

```bash
dotnet test
```

All 54 tests should pass. If any tests fail, check your .NET SDK version with `dotnet --version` and ensure it is 10.0.x.

## Configuration

The tool uses `appsettings.json` as its primary configuration file, located at:

```
Dynamics365ImportData/Dynamics365ImportData/appsettings.json
```

Copy the existing file and customize the values for your environment. For a complete reference of every configuration option, see [Configuration Reference](configuration-reference.md).

At minimum, you need to configure:

1. **`SourceSettings.SourceConnectionString`** -- your SQL Server connection string
2. **`DestinationSettings.OutputDirectory`** -- where output files will be written
3. **`ProcessSettings.DefinitionDirectory`** -- path to your entity definition folders
4. **`ProcessSettings.Queries`** -- at least one entity to process

For D365FO import, you also need:

5. **`Dynamics365Settings`** -- tenant, URL, client ID, secret, and legal entity

> **Important:** Never store secrets (like `Dynamics365Settings.Secret`) directly in `appsettings.json`. Use .NET User Secrets or environment variables instead. See [Credential Management](#credential-management) below.

## First Run

Follow these steps to verify the tool works with your environment:

### 1. Configure a Single Entity

Edit `appsettings.json` and set up one entity in the `Queries` array. For example:

```json
{
  "SourceSettings": {
    "SourceConnectionString": "Server=your-server;Database=AxDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "DestinationSettings": {
    "OutputDirectory": "C:\\DataMigration\\Output\\"
  },
  "ProcessSettings": {
    "DefinitionDirectory": "C:\\DataMigration\\Definition",
    "MaxDegreeOfParallelism": 1,
    "Queries": [
      {
        "EntityName": "CustCustomerV3Entity",
        "DefinitionGroupId": "Datamig_CustCustomerV3Entity",
        "RecordsPerFile": 200000,
        "QueryFileName": "",
        "Dependencies": []
      }
    ]
  }
}
```

### 2. Prepare the Entity Definition

Create the entity definition folder and files. The tool converts entity names to uppercase internally, so folder and SQL file names must use uppercase:

```
C:\DataMigration\Definition\
└── CUSTCUSTOMERV3ENTITY\
    ├── CUSTCUSTOMERV3ENTITY.sql    # SQL query to extract data
    ├── Manifest.xml                 # D365FO manifest template
    └── PackageHeader.xml            # D365FO package header template
```

> **Note:** On Windows, folder and file name casing does not matter due to case-insensitive paths. On Linux/macOS or in Docker containers, use uppercase names to match the tool's internal resolution.

The SQL file should contain the query to extract data from your source database.

### 3. Run the Export

From the `Dynamics365ImportData/Dynamics365ImportData` directory:

```bash
dotnet run -- export-file
```

Or, using the short alias:

```bash
dotnet run -- f
```

### 4. Verify the Output

Check the `OutputDirectory` you configured. You should see XML files generated for the entity. The console output will show log messages indicating the export progress.

## Running Commands

The tool provides three commands:

### `export-file` (alias: `f`)

Exports data from SQL Server to individual XML files:

```bash
dotnet run -- export-file
```

Use this to generate XML files for inspection or manual import. Output files are written to `DestinationSettings.OutputDirectory`.

### `export-package` (alias: `p`)

Exports data to ZIP packages containing XML and manifest files:

```bash
dotnet run -- export-package
```

The generated packages are compatible with D365FO Data Management Framework for manual upload.

### `import-d365` (alias: `i`)

Exports data and imports it directly into D365FO via the Data Management API:

```bash
dotnet run -- import-d365
```

This requires `Dynamics365Settings` to be configured with valid Azure AD credentials. Entities are processed in dependency order automatically.

> **Note:** The `export-file` and `export-package` commands delete files in the output directory before starting. The `import-d365` command does not clear the output directory. Ensure you have saved any needed files from previous export runs.

## Running Tests

From the solution directory (`Dynamics365ImportData/`):

```bash
dotnet test
```

This runs all 54 tests (unit, integration, snapshot, and audit tests). All tests should pass on a clean build. No external services are required to run the tests.

## Credential Management

Sensitive configuration values (especially `Dynamics365Settings.Secret`) should not be stored in `appsettings.json`. The tool supports two secure alternatives:

### .NET User Secrets

User Secrets store sensitive values on your local machine outside the project directory, preventing accidental commits.

**Initialize User Secrets** (one-time setup, from the main project directory):

```bash
cd Dynamics365ImportData/Dynamics365ImportData
dotnet user-secrets init
```

**Set a secret value:**

```bash
dotnet user-secrets set "Dynamics365Settings:Secret" "your-client-secret-value"
```

**Set other sensitive values:**

```bash
dotnet user-secrets set "SourceSettings:SourceConnectionString" "Server=prod-server;Database=AxDb;User Id=sa;Password=secret;TrustServerCertificate=True;"
```

**List stored secrets:**

```bash
dotnet user-secrets list
```

### Environment Variables

You can override any configuration value using environment variables. Use double underscores (`__`) as separators for nested settings:

```bash
# Windows (PowerShell)
$env:Dynamics365Settings__Secret = "your-client-secret-value"
$env:SourceSettings__SourceConnectionString = "Server=prod;Database=AxDb;Trusted_Connection=True;"

# Windows (Command Prompt)
set Dynamics365Settings__Secret=your-client-secret-value

# Linux/macOS
export Dynamics365Settings__Secret="your-client-secret-value"
```

For the full configuration precedence order (which source overrides which), see the [Configuration Precedence](configuration-reference.md#configuration-precedence) section in the Configuration Reference.

## Troubleshooting

### SQL Server Connection Failures

**Symptom:** `SqlException: A network-related or instance-specific error occurred`

**Possible causes and solutions:**

- **Server not reachable:** Verify the server name/IP and that SQL Server is running. For named instances, use `Server=hostname\InstanceName`.
- **Firewall blocking connections:** Ensure TCP port 1433 (or your custom port) is open.
- **Trusted Connection failing:** If using `Trusted_Connection=True`, ensure your Windows account has access to the database. For SQL authentication, use `User Id=...;Password=...;` instead.
- **Named instances:** Use SQL Server Configuration Manager to verify TCP/IP is enabled for the instance and note the port number.

### Encrypt=true Default (Microsoft.Data.SqlClient)

**Symptom:** `SqlException: A connection was successfully established with the server, but then an error occurred during the login process... The certificate chain was issued by an authority that is not trusted.`

**Cause:** The tool uses `Microsoft.Data.SqlClient`, which defaults to `Encrypt=true`. If your SQL Server does not have a trusted TLS certificate (common in development environments), connections will fail.

**Solution:** Add `TrustServerCertificate=True` to your connection string for development/test environments:

```
Server=localhost;Database=AxDb;Trusted_Connection=True;TrustServerCertificate=True;
```

> **Warning:** Do not use `TrustServerCertificate=True` in production environments. Instead, configure a proper TLS certificate on your SQL Server.

### D365FO Authentication Errors

**Symptom:** `401 Unauthorized` or `AADSTS` error codes when running `import-d365`

**Possible causes and solutions:**

- **Incorrect Azure AD App Registration:** Verify `ClientId` matches the Application (client) ID in Azure Portal > App Registrations.
- **Tenant mismatch:** Ensure `Tenant` matches the Azure AD tenant where the app is registered (e.g., `contoso.onmicrosoft.com`).
- **Expired or invalid secret:** Client secrets expire. Check the expiration date in Azure Portal > App Registrations > Certificates & secrets. Generate a new secret if expired.
- **Missing API permissions:** The app registration needs permissions to access the D365FO environment. Verify the app has the required Dynamics 365 API permissions granted by an admin.
- **Environment URL mismatch:** Ensure `Url` matches your D365FO environment exactly (e.g., `https://your-env.operations.dynamics.com`).

### Build Failures

**Symptom:** `dotnet build` fails with errors

**Possible causes and solutions:**

- **Wrong .NET SDK version:** Run `dotnet --version` and verify it is 10.0.x. Download the correct SDK from the [.NET download page](https://dotnet.microsoft.com/download/dotnet/10.0).
- **Missing workloads:** Run `dotnet workload list` to check installed workloads. The tool does not require any additional workloads beyond the base SDK.
- **NuGet restore issues:** Try `dotnet restore` explicitly before building. If behind a corporate proxy, configure NuGet proxy settings.

### Entity Definition Not Found

**Symptom:** Runtime error indicating entity definition files cannot be found

**Possible causes and solutions:**

- **`DefinitionDirectory` path incorrect:** Verify the path in `ProcessSettings.DefinitionDirectory` exists and is accessible.
- **Entity folder naming:** The tool converts entity names to uppercase internally. Each entity needs a folder matching the uppercased `EntityName`. For example, `CustCustomerV3Entity` resolves to a folder named `CUSTCUSTOMERV3ENTITY/`. On Windows, casing does not matter due to case-insensitive paths.
- **Missing definition files:** Each entity folder must contain:
  - `{ENTITYNAME}.sql` -- the SQL extraction query (uppercase, matching the resolved entity name)
  - `Manifest.xml` -- the D365FO manifest template
  - `PackageHeader.xml` -- the D365FO package header template
- **Custom file paths:** If `QueryFileName`, `ManifestFileName`, or `PackageHeaderFileName` are left empty, they auto-resolve to the default paths above. If set, they must point to valid files.

## See Also

- [Configuration Reference](configuration-reference.md) -- Complete reference for all `appsettings.json` options
- [Entity Authoring Guide](entity-authoring-guide.md) -- How to add new entity definitions (SQL queries, manifests, dependencies)
- [Developer Guide](developer-guide.md) -- Codebase architecture, key components, and extension points
