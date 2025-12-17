namespace Dynamics365ImportData.DependencySorting;

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
    List<string> Dependencies
    );