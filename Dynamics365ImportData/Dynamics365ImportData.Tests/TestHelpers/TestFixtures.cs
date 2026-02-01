namespace Dynamics365ImportData.Tests.TestHelpers;

using Dynamics365ImportData.DependencySorting;

public class TestFixtures
{
    public static SourceQueryItem CreateTestQueryItem(
        string entityName = "TestEntity",
        string definitionGroupId = "TestGroup",
        string outputDirectory = "",
        int recordsPerFile = 1000,
        string sourceConnectionString = "Server=test;Database=test;",
        string manifestFileName = "Manifest.xml",
        string packageHeaderFileName = "PackageHeader.xml",
        string queryFileName = "query.sql",
        List<string>? dependencies = null)
    {
        return new SourceQueryItem(
            EntityName: entityName,
            DefinitionGroupId: definitionGroupId,
            ManifestFileName: manifestFileName,
            OutputDirectory: outputDirectory,
            OutputBlobStorage: "",
            PackageHeaderFileName: packageHeaderFileName,
            QueryFileName: queryFileName,
            RecordsPerFile: recordsPerFile,
            SourceConnectionString: sourceConnectionString,
            Dependencies: dependencies ?? new List<string>()
        );
    }
}
