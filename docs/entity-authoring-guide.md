# Entity Authoring Guide

This guide walks you through adding a new D365FO data entity to the migration tool. By the end, you will have a working entity definition that can extract data from SQL Server and produce XML output compatible with the D365FO Data Management Framework.

## Audience

- **Functional Consultants** configuring data migrations for D365FO projects
- **Migration Engineers** adding new entities to an existing migration setup

## Prerequisites

- Access to the D365FO Data Entity documentation for your target environment (to identify entity names and field mappings)
- Access to the source SQL Server database (to write extraction queries)
- Understanding of the target D365FO entity schema (field names, required fields, data types)
- A working installation of the migration tool (see [Setup Guide](setup-guide.md))

## Entity Naming Conventions

The tool uses specific naming conventions that you must follow when adding entities.

### Entity Name in Configuration

The `EntityName` value in `appsettings.json` should match the D365FO data entity name exactly as it appears in the Data Management Framework. For example: `CustCustomerV3Entity`, `smmContactPersonV2Entity`, `CustomerBankAccountEntity`.

### Folder Name in Definition Directory

Each entity requires its own folder under the `DefinitionDirectory` path. The folder name uses the entity name as provided in the configuration. For example, if your `DefinitionDirectory` is `C:\DataMigration\Definition` and your entity is `CustCustomerBaseEntity`, the folder would be:

```
C:\DataMigration\Definition\CustCustomerBaseEntity\
```

### Internal UPPERCASE Resolution

The tool converts entity names to **UPPERCASE** internally for all file lookups. This happens in `SourceQueryCollection`, which calls `name.ToUpper()` on every entity name during initialization. As a result:

- The entity folder path is resolved using the uppercased name: `{DefinitionDirectory}/{ENTITYNAME}/`
- The SQL file is looked up as `{ENTITYNAME}.sql` (e.g., `CUSTCUSTOMERBASEENTITY.sql`)
- Manifest and PackageHeader files are looked up by their fixed names (`Manifest.xml`, `PackageHeader.xml`) within the uppercased folder

On **Windows**, folder and file name casing does not matter due to case-insensitive file paths. On **Linux/macOS** or in Docker containers, you must use uppercase folder and file names to match the tool's internal resolution.

### DefinitionGroupId

The `DefinitionGroupId` identifies the DMF definition group in the target D365FO environment. Common conventions include:

- `Datamig_CustCustomerV3Entity` -- prefixed with a project identifier
- `IFG_CUSTCUSTOMERBASEENTITY` -- prefixed with a company/project code

If you leave `DefinitionGroupId` empty in the configuration, the tool auto-generates one using the uppercased entity name: `DMF_{ENTITYNAME}` (e.g., `DMF_CUSTCUSTOMERBASEENTITY`).

## Entity Definition Directory Structure

Each entity requires three files in its definition folder:

```
{DefinitionDirectory}/{ENTITYNAME}/
├── {ENTITYNAME}.sql       # SQL extraction query (uppercase filename)
├── Manifest.xml            # D365FO data package manifest
└── PackageHeader.xml       # D365FO data package header
```

### Path Resolution

The tool resolves file paths as follows:

1. **Entity folder:** `{DefinitionDirectory}/{ENTITYNAME}/` (entity name converted to uppercase)
2. **SQL query file:** `{ENTITYNAME}.sql` inside the entity folder (uppercase)
3. **Manifest file:** `Manifest.xml` inside the entity folder
4. **PackageHeader file:** `PackageHeader.xml` inside the entity folder

### Custom Path Overrides

You can override the default file paths using these configuration properties in `QuerySettings`:

| Property | Default | Override |
|----------|---------|----------|
| `QueryFileName` | `{ENTITYNAME}.sql` | Custom SQL file name within the entity folder |
| `ManifestFileName` | `Manifest.xml` | Custom manifest file name within the entity folder |
| `PackageHeaderFileName` | `PackageHeader.xml` | Custom package header file name within the entity folder |

When these properties are empty (the default), the tool uses the automatic path resolution described above. When set, the value is combined with the entity's definition directory to form the full path.

For a complete reference of all configuration properties, see the [Configuration Reference](configuration-reference.md).

## SQL Extraction Query

The SQL query file defines how data is extracted from the source database.

### File Naming

The SQL file must be named `{ENTITYNAME}.sql` using the uppercased entity name. For example, for entity `CustCustomerBaseEntity`, the file is named `CUSTCUSTOMERBASEENTITY.sql`.

### Query Structure

The query must return columns whose names match the D365FO entity field names. The tool reads each row from the SQL result set and writes it as an XML element, with each column becoming an XML attribute.

**Basic example:**

```sql
SELECT
    CUSTTABLE.ACCOUNTNUM AS CUSTOMERACCOUNT,
    CUSTTABLE.CUSTGROUP AS CUSTOMERGROUPID,
    CUSTTABLE.NAME AS ORGANIZATIONNAME
FROM CUSTTABLE
WHERE CUSTTABLE.DATAAREAID = N'USMF'
```

### Column Aliasing

Use `AS` aliases to map source database column names to D365FO entity field names:

```sql
SELECT
    CUSTTABLE.ACCOUNTNUM AS CUSTOMERACCOUNT,    -- Source column → D365FO field
    CUSTTABLE.CUSTGROUP AS CUSTOMERGROUPID,
    CUSTTABLE.DLVMODE AS DELIVERYMODE,
    CUSTTABLE.DLVTERM AS DELIVERYTERMS,
    CUSTTABLE.PAYMTERMID AS PAYMENTTERMS
FROM CUSTTABLE
```

### JOINs for Lookups

Use JOINs when you need data from related tables or code conversion lookups:

```sql
SELECT
    CUSTTABLE.ACCOUNTNUM AS CUSTOMERACCOUNT,
    CUSTTABLE.NAME AS ORGANIZATIONNAME,
    LOOKUPTABLE.TARGETCODE AS PARTYCOUNTRY
FROM CUSTTABLE
LEFT JOIN LOOKUPTABLE
    ON LOOKUPTABLE.SOURCECODE = CUSTTABLE.COUNTRYCODE
    AND LOOKUPTABLE.DATAAREAID = CUSTTABLE.DATAAREAID
WHERE CUSTTABLE.DATAAREAID = N'USMF'
```

### NULL Handling

Use `ISNULL` or `COALESCE` for fields that are required in D365FO but may be NULL in the source:

```sql
SELECT
    CUSTTABLE.ACCOUNTNUM AS CUSTOMERACCOUNT,
    CASE
        WHEN CUSTTABLE.LANGUAGEID IS NULL THEN 'EN-US'
        ELSE CUSTTABLE.LANGUAGEID
    END AS LANGUAGEID,
    ISNULL(CUSTTABLE.CURRENCY, 'USD') AS SALESCURRENCYCODE
FROM CUSTTABLE
```

You can also use empty string literals for fields that need a value but have no source data:

```sql
SELECT
    CUSTTABLE.ACCOUNTNUM AS CUSTOMERACCOUNT,
    '' AS ADDRESSCITY,
    '' AS ADDRESSZIPCODE
FROM CUSTTABLE
```

### Data Filtering

Use `WHERE` clauses to filter records by company, status, or other criteria:

```sql
SELECT ...
FROM CUSTTABLE
WHERE
    CUSTTABLE.DATAAREAID = N'USMF'          -- Filter by legal entity
    AND CUSTTABLE.BLOCKED = 0                -- Only active records
    AND CUSTTABLE.STATUSFLAG NOT IN (N'CLOSED', N'ARCHIVED')
```

### TOP N for Testing

Use `TOP N` to limit results during development and testing:

```sql
SELECT TOP 100
    CUSTTABLE.ACCOUNTNUM AS CUSTOMERACCOUNT,
    CUSTTABLE.NAME AS ORGANIZATIONNAME
FROM CUSTTABLE
WHERE CUSTTABLE.DATAAREAID = N'USMF'
```

Remove or increase the `TOP` limit for production runs.

### RecordsPerFile and File Splitting

The `RecordsPerFile` setting in the entity configuration controls how many records are written to each output XML file. When the record count exceeds this threshold, the tool automatically creates additional output files (parts).

For example, with `RecordsPerFile: 200000` and 500,000 records in the query result:
- Part 0: records 1--200,000
- Part 1: records 200,001--400,000
- Part 2: records 400,001--500,000

Each part is a self-contained XML document with its own `<Document>` root element.

### Reference Examples

See the existing SQL file in the repository for a real-world example:

- `Definition/CustCustomerBaseEntity/CUSTCUSTOMERBASEENTITY.sql` -- demonstrates column aliasing, JOINs, NULL handling, CASE expressions, and WHERE filtering

## Manifest XML Format

The manifest file tells D365FO how to map XML fields to entity fields during import.

### Structure

```xml
<DataManagementPackageManifest
    xmlns:i="http://www.w3.org/2001/XMLSchema-instance"
    xmlns="http://schemas.microsoft.com/dynamics/2015/01/DataManagement">
  <DefinitionGroupName>YOUR_DEFINITION_GROUP_ID</DefinitionGroupName>
  <Description>Human-readable description of this import</Description>
  <PackageEntityList>
    <DataManagementPackageEntityData>
      <DefaultRefreshType>FullPush</DefaultRefreshType>
      <Disable>false</Disable>
      <EntityMapList>
        <!-- Field mappings go here -->
      </EntityMapList>
    </DataManagementPackageEntityData>
  </PackageEntityList>
</DataManagementPackageManifest>
```

### Key Elements

| Element | Description |
|---------|-------------|
| `DefinitionGroupName` | Must match the `DefinitionGroupId` from the entity configuration in `appsettings.json` |
| `Description` | Human-readable description of the import definition |
| `DefaultRefreshType` | Set to `FullPush` for full data replacement |
| `Disable` | Set to `false` to enable the entity |
| `EntityMapList` | Contains `EntityMap` entries for each field mapping |

### Field Mapping

Each field requires an `EntityMap` entry inside `EntityMapList`:

```xml
<EntityMap>
  <ArrayIndex>0</ArrayIndex>
  <EntityField>CUSTOMERACCOUNT</EntityField>
  <EntityFieldConversionList i:nil="true" />
  <IsAutoDefault>false</IsAutoDefault>
  <IsAutoGenerated>false</IsAutoGenerated>
  <IsDefaultValueEqualNull>false</IsDefaultValueEqualNull>
  <UseTextQualifier>false</UseTextQualifier>
  <XMLField>CUSTOMERACCOUNT</XMLField>
</EntityMap>
```

| Element | Description |
|---------|-------------|
| `EntityField` | The D365FO entity field name (uppercase) |
| `XMLField` | The corresponding field name in the XML data file (must match the SQL column alias) |
| `EntityFieldConversionList` | Optional conversion rules; set to `i:nil="true"` when not needed |
| `IsAutoDefault` / `IsAutoGenerated` | Set to `false` for manually mapped fields |
| `IsDefaultValueEqualNull` | Set to `false` unless the default value should be treated as NULL |
| `UseTextQualifier` | Set to `false` for standard field handling |

You must create an `EntityMap` entry for every field that your SQL query returns and that you want mapped in D365FO.

### Reference Examples

See the existing manifest in the repository:

- `Definition/CustCustomerBaseEntity/Manifest.xml` -- demonstrates field mapping for a customer entity with many fields

## Package Header XML Format

The package header provides metadata for the D365FO data package.

### Structure

```xml
<DataManagementPackageHeader
    xmlns:i="http://www.w3.org/2001/XMLSchema-instance"
    xmlns="http://schemas.microsoft.com/dynamics/2015/01/DataManagement">
  <Description>YOUR_DEFINITION_GROUP_ID</Description>
  <ManifestType>Microsoft.Dynamics.AX.Framework.Tools.DataManagement.Serialization.DataManagementPackageManifest</ManifestType>
  <PackageType>DefinitionGroup</PackageType>
  <PackageVersion>2</PackageVersion>
</DataManagementPackageHeader>
```

### Required Elements

| Element | Value | Description |
|---------|-------|-------------|
| `Description` | Your definition group ID | Identifies this package |
| `ManifestType` | `Microsoft.Dynamics.AX.Framework.Tools.DataManagement.Serialization.DataManagementPackageManifest` | Fixed value -- do not change |
| `PackageType` | `DefinitionGroup` | Fixed value -- do not change |
| `PackageVersion` | `2` | Fixed value -- do not change |

The `Description` element typically matches the `DefinitionGroupId` from the entity configuration.

### Reference Examples

See the existing package header in the repository:

- `Definition/CustCustomerBaseEntity/PackageHeader.xml`

## Dependency Declaration

Entities often need to be imported in a specific order. For example, customer bank accounts depend on customers existing first.

### Configuring Dependencies

Add entity names to the `Dependencies` array in the entity's `QuerySettings` within `appsettings.json`:

```json
{
  "EntityName": "CustomerBankAccountEntity",
  "DefinitionGroupId": "Datamig_CustomerBankAccountEntity",
  "RecordsPerFile": 200000,
  "QueryFileName": "",
  "Dependencies": ["CustCustomerV3Entity"]
}
```

The dependency value must match the `EntityName` of another entity in the `Queries` array. The comparison is case-insensitive.

### How Dependency Sorting Works

The tool uses topological sorting (Kahn's algorithm) to determine the processing order:

1. Entities with no dependencies are processed first
2. Entities are grouped into **dependency levels** -- all entities in the same level can run in parallel
3. Each level must complete before the next level starts
4. Within a level, entities execute in parallel (controlled by `MaxDegreeOfParallelism`)

**Example dependency chain:**

```json
"Queries": [
  { "EntityName": "smmContactPersonV2Entity",  "Dependencies": [] },
  { "EntityName": "CustCustomerV3Entity",       "Dependencies": ["smmContactPersonV2Entity"] },
  { "EntityName": "CustomerBankAccountEntity",   "Dependencies": ["CustCustomerV3Entity"] },
  { "EntityName": "CustDirectDebitMandateEntity","Dependencies": ["CustomerBankAccountEntity"] }
]
```

Processing order:
- **Level 1:** `smmContactPersonV2Entity` (no dependencies)
- **Level 2:** `CustCustomerV3Entity` (depends on contacts)
- **Level 3:** `CustomerBankAccountEntity` (depends on customers)
- **Level 4:** `CustDirectDebitMandateEntity` (depends on bank accounts)

Entities without dependencies between each other run in the same level. For instance, if you also had `AssetFixedAssetV2Entity` with no dependencies, it would run in Level 1 alongside `smmContactPersonV2Entity`.

### Circular Dependency Detection

The tool detects circular dependencies at startup and throws an `InvalidOperationException` with the message `"Cannot order this set of processes"`. If you see this error, review your `Dependencies` arrays for cycles (e.g., A depends on B, B depends on A).

## Complete Worked Example

This section walks through adding a fictional entity `VendVendorGroupEntity` from scratch.

### Step 1: Identify the Target Entity

Look up `VendVendorGroupEntity` in the D365FO Data Management Framework to determine:
- The entity name: `VendVendorGroupEntity`
- Required fields: `VendorGroupId`, `Description`, `DefaultPaymentTerms`, `ClearingPeriod`

### Step 2: Write the SQL Extraction Query

Create the entity folder and SQL file:

```
{DefinitionDirectory}/VendVendorGroupEntity/VENDVENDORGROUPENTITY.sql
```

Write the SQL query:

```sql
SELECT
    VENDGROUP.VENDGROUP AS VENDORGROUPID,
    VENDGROUP.NAME AS DESCRIPTION,
    VENDGROUP.PAYMTERMID AS DEFAULTPAYMENTTERMS,
    VENDGROUP.CLEARINGPERIOD AS CLEARINGPERIOD
FROM VENDGROUP
WHERE VENDGROUP.DATAAREAID = N'USMF'
```

### Step 3: Create the Manifest

Create `{DefinitionDirectory}/VendVendorGroupEntity/Manifest.xml`:

```xml
<DataManagementPackageManifest
    xmlns:i="http://www.w3.org/2001/XMLSchema-instance"
    xmlns="http://schemas.microsoft.com/dynamics/2015/01/DataManagement">
  <DefinitionGroupName>Datamig_VendVendorGroupEntity</DefinitionGroupName>
  <Description>Vendor Group Import</Description>
  <PackageEntityList>
    <DataManagementPackageEntityData>
      <DefaultRefreshType>FullPush</DefaultRefreshType>
      <Disable>false</Disable>
      <EntityMapList>
        <EntityMap>
          <ArrayIndex>0</ArrayIndex>
          <EntityField>VENDORGROUPID</EntityField>
          <EntityFieldConversionList i:nil="true" />
          <IsAutoDefault>false</IsAutoDefault>
          <IsAutoGenerated>false</IsAutoGenerated>
          <IsDefaultValueEqualNull>false</IsDefaultValueEqualNull>
          <UseTextQualifier>false</UseTextQualifier>
          <XMLField>VENDORGROUPID</XMLField>
        </EntityMap>
        <EntityMap>
          <ArrayIndex>0</ArrayIndex>
          <EntityField>DESCRIPTION</EntityField>
          <EntityFieldConversionList i:nil="true" />
          <IsAutoDefault>false</IsAutoDefault>
          <IsAutoGenerated>false</IsAutoGenerated>
          <IsDefaultValueEqualNull>false</IsDefaultValueEqualNull>
          <UseTextQualifier>false</UseTextQualifier>
          <XMLField>DESCRIPTION</XMLField>
        </EntityMap>
        <EntityMap>
          <ArrayIndex>0</ArrayIndex>
          <EntityField>DEFAULTPAYMENTTERMS</EntityField>
          <EntityFieldConversionList i:nil="true" />
          <IsAutoDefault>false</IsAutoDefault>
          <IsAutoGenerated>false</IsAutoGenerated>
          <IsDefaultValueEqualNull>false</IsDefaultValueEqualNull>
          <UseTextQualifier>false</UseTextQualifier>
          <XMLField>DEFAULTPAYMENTTERMS</XMLField>
        </EntityMap>
        <EntityMap>
          <ArrayIndex>0</ArrayIndex>
          <EntityField>CLEARINGPERIOD</EntityField>
          <EntityFieldConversionList i:nil="true" />
          <IsAutoDefault>false</IsAutoDefault>
          <IsAutoGenerated>false</IsAutoGenerated>
          <IsDefaultValueEqualNull>false</IsDefaultValueEqualNull>
          <UseTextQualifier>false</UseTextQualifier>
          <XMLField>CLEARINGPERIOD</XMLField>
        </EntityMap>
      </EntityMapList>
    </DataManagementPackageEntityData>
  </PackageEntityList>
</DataManagementPackageManifest>
```

### Step 4: Create the Package Header

Create `{DefinitionDirectory}/VendVendorGroupEntity/PackageHeader.xml`:

```xml
<DataManagementPackageHeader
    xmlns:i="http://www.w3.org/2001/XMLSchema-instance"
    xmlns="http://schemas.microsoft.com/dynamics/2015/01/DataManagement">
  <Description>Datamig_VendVendorGroupEntity</Description>
  <ManifestType>Microsoft.Dynamics.AX.Framework.Tools.DataManagement.Serialization.DataManagementPackageManifest</ManifestType>
  <PackageType>DefinitionGroup</PackageType>
  <PackageVersion>2</PackageVersion>
</DataManagementPackageHeader>
```

### Step 5: Add the Entity to Configuration

Add the entity to the `Queries` array in `appsettings.json`:

```json
{
  "EntityName": "VendVendorGroupEntity",
  "DefinitionGroupId": "Datamig_VendVendorGroupEntity",
  "ManifestFileName": "",
  "PackageHeaderFileName": "",
  "RecordsPerFile": 200000,
  "QueryFileName": "",
  "SourceConnectionString": "",
  "Dependencies": []
}
```

If this entity depends on other entities, list them in the `Dependencies` array. Vendor groups typically have no dependencies, so the array is empty.

### Step 6: Test with export-file

Run the file export to verify XML output:

```bash
cd Dynamics365ImportData/Dynamics365ImportData
dotnet run -- export-file
```

Check the output directory for the generated XML file. The output will be an XML document with this structure:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Document>
  <VENDVENDORGROUPENTITY VENDORGROUPID="10" DESCRIPTION="Trade vendors" DEFAULTPAYMENTTERMS="Net30" CLEARINGPERIOD="30" />
  <VENDVENDORGROUPENTITY VENDORGROUPID="20" DESCRIPTION="Service vendors" DEFAULTPAYMENTTERMS="Net60" CLEARINGPERIOD="60" />
</Document>
```

Each row becomes an XML element named after the entity (uppercased), with column values as attributes.

### Step 7: Validate with export-package

Run the package export to verify the complete ZIP package:

```bash
dotnet run -- export-package
```

The output directory will contain a `.zip` file. Inside the ZIP:
- `Manifest.xml` -- copied from your entity definition
- `PackageHeader.xml` -- copied from your entity definition
- Entity data XML file -- the extracted data

This ZIP file is ready for manual upload to D365FO via the Data Management workspace, or for automated import using the `import-d365` command.

## Troubleshooting

### SQL Query Errors

**Symptom:** `SqlException` during export

**Common causes:**
- **Column name typo:** Verify that all column names in the SQL query exist in the source table. Run the query directly in SQL Server Management Studio first.
- **Missing table or schema:** Check that the table names are correct and accessible with the configured connection credentials.
- **Timeout:** The tool sets a 1-hour command timeout by default. For very large queries, consider adding indexes or using `TOP N` to narrow results.

### Column Name Mismatches

**Symptom:** D365FO import fails with field mapping errors

**Common causes:**
- The `AS` alias in your SQL query does not match the `EntityField`/`XMLField` values in `Manifest.xml`. These must be identical (case-insensitive in D365FO, but be consistent).
- A required D365FO field is missing from your SQL query and manifest. Check the entity schema in D365FO to identify all required fields.

### Missing Entity Definition Files

**Symptom:** Exception at startup: `"The definition directory does not exist for query N"` or `"The query file not found in query N"`

**Common causes:**
- The `DefinitionDirectory` path in `appsettings.json` is incorrect or does not exist.
- The entity folder name does not match the uppercased entity name. Remember: the tool resolves `{DefinitionDirectory}/{ENTITYNAME}/`, so ensure the folder exists at that path.
- The SQL file is not named `{ENTITYNAME}.sql` (uppercase). For example, `CustCustomerBaseEntity` requires `CUSTCUSTOMERBASEENTITY.sql`.
- `Manifest.xml` or `PackageHeader.xml` is missing from the entity folder.

### Incorrect Dependency Ordering

**Symptom:** D365FO import fails because a referenced record does not exist yet

**Common causes:**
- The entity's `Dependencies` array is missing a required parent entity. For example, `CustomerBankAccountEntity` must depend on `CustCustomerV3Entity`.
- The dependency entity name in the `Dependencies` array does not match any `EntityName` in the `Queries` list (comparison is case-insensitive).

**Symptom:** `InvalidOperationException: "Cannot order this set of processes"`

**Cause:** Circular dependency detected. Review your `Dependencies` arrays to find and break the cycle.

### D365FO Import Failures from Manifest Field Mapping Errors

**Symptom:** Import job in D365FO shows errors for specific records or fields

**Common causes:**
- `EntityField` in the manifest does not match the actual D365FO entity field name. Verify field names in the D365FO entity schema.
- `XMLField` in the manifest does not match the SQL query column alias. These must be identical.
- A required field in D365FO has no mapping in the manifest and no default value configured in D365FO.
- Data type mismatch: the source data format does not match what D365FO expects (e.g., date formats, numeric precision).
- The `DefinitionGroupName` in the manifest does not match the definition group configured in the D365FO environment.

## See Also

- [Setup Guide](setup-guide.md) -- Installation and first-run instructions
- [Configuration Reference](configuration-reference.md) -- Complete reference for all `appsettings.json` options
- [Developer Guide](developer-guide.md) -- Codebase architecture and extension points
