# D365FO Data Migration

A .NET 8 command-line tool for migrating data from SQL Server databases to Microsoft Dynamics 365 Finance and Operations (D365FO). The tool extracts data using SQL queries, transforms it into XML format compatible with D365FO Data Management Framework, and supports direct import via the Data Management REST API.

## Features

- **SQL to XML Export**: Execute SQL queries against source databases and generate XML files in D365FO-compatible format
- **Package Generation**: Create complete data packages (ZIP) with manifest files ready for D365FO import
- **Direct D365FO Import**: Upload and import data directly to D365FO using the Data Management API
- **Dependency Management**: Automatic topological sorting of entities based on dependencies
- **Parallel Processing**: Configurable parallelism for improved performance
- **Large Dataset Support**: Split large datasets into multiple files based on record count
- **Azure Blob Integration**: Upload packages directly to D365FO Azure Blob storage

## Prerequisites

- .NET 8.0 SDK or Runtime
- Access to source SQL Server database
- For D365FO import: Azure AD application with appropriate permissions

## Installation

```bash
git clone https://github.com/Itaneo/d365fo-data-migration.git
cd d365fo-data-migration/Dynamics365ImportData
dotnet build
```

## Configuration

Configure the application using `appsettings.json`:

### Source Settings

```json
"SourceSettings": {
  "SourceConnectionString": "Server=localhost;Database=AxDb;Trusted_Connection=True;"
}
```

### Destination Settings

```json
"DestinationSettings": {
  "OutputDirectory": "C:\\DataMigration\\Output\\",
  "OutputBlobStorage": ""
}
```

### Dynamics 365 Settings

```json
"Dynamics365Settings": {
  "Tenant": "yourtenant.onmicrosoft.com",
  "Url": "https://your-environment.operations.dynamics.com",
  "ClientId": "your-azure-ad-app-id",
  "Secret": "in secret store",
  "LegalEntityId": "USMF",
  "ImportTimeout": 60
}
```

> **Note**: Store sensitive values like `Secret` in User Secrets or environment variables.

### Process Settings

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

### Query Configuration Options

| Property | Description |
|----------|-------------|
| `EntityName` | D365FO entity name |
| `DefinitionGroupId` | Data Management definition group ID in D365FO |
| `RecordsPerFile` | Maximum records per output file (for splitting large datasets) |
| `QueryFileName` | Path to SQL query file (defaults to `Definition/{EntityName}/{EntityName}.sql`) |
| `ManifestFileName` | Path to manifest XML template |
| `PackageHeaderFileName` | Path to package header XML template |
| `SourceConnectionString` | Override source connection string for specific entity |
| `Dependencies` | List of entity names that must be imported first |

## Usage

### Export to XML Files

Export data to individual XML files:

```bash
dotnet run -- export-file
# or
dotnet run -- f
```

### Export to Data Packages

Export data to ZIP packages with manifest files:

```bash
dotnet run -- export-package
# or
dotnet run -- p
```

### Import Directly to D365FO

Export and import data directly to Dynamics 365:

```bash
dotnet run -- import-d365
# or
dotnet run -- i
```

## Definition Files

Each entity requires a definition folder under `DefinitionDirectory` containing:

```
Definition/
└── CustCustomerV3Entity/
    ├── CustCustomerV3Entity.sql    # SQL query to extract data
    ├── Manifest.xml                 # D365FO manifest template
    └── PackageHeader.xml            # D365FO package header template
```

### SQL Query Example

```sql
SELECT
    CustomerAccount,
    CustomerGroupId,
    OrganizationName
FROM CustTable
WHERE DataAreaId = 'USMF'
```

## Dependency Management

The tool automatically resolves entity dependencies using topological sorting. Define dependencies in the query configuration to ensure entities are imported in the correct order:

```json
{
  "EntityName": "CustomerBankAccountEntity",
  "Dependencies": ["CustCustomerV3Entity"]
}
```

## Security

- Use Azure AD application credentials for D365FO authentication
- Store secrets using .NET User Secrets or environment variables:

```bash
dotnet user-secrets set "Dynamics365Settings:Secret" "your-client-secret"
```

## Logging

The application uses Serilog for structured logging. Configure log levels in `appsettings.json`:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "System": "Error"
    }
  }
}
```

## License

This project is proprietary software. All rights reserved.

## Contributing

Please contact the repository maintainers for contribution guidelines.
