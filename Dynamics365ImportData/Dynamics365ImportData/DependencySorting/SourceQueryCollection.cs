namespace Dynamics365ImportData.DependencySorting;

using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.IO;

public class SourceQueryCollection
{
    private readonly DestinationSettings _destinationSettings;
    private readonly ILogger<SourceQueryCollection> _logger;
    private readonly ProcessSettings _processSettings;
    private readonly SourceSettings _sourceSettings;

    public SourceQueryCollection(
        IOptions<SourceSettings> sourceSettings,
        IOptions<DestinationSettings> destinationSettings,
        IOptions<ProcessSettings> processSettings,
        ILogger<SourceQueryCollection> logger)
    {
        _logger = logger;
        _sourceSettings = sourceSettings.Value;
        _destinationSettings = destinationSettings.Value;
        _processSettings = processSettings.Value;
        if (_processSettings.Queries == null || _processSettings.Queries.Count == 0)
        {
            throw new ArgumentException("The settings does not contain any source query parameters. Please check the application settings file.", nameof(processSettings));
        }
        OutputDirectory = GetOutputDirectory(_destinationSettings.OutputDirectory);
        DependencyGraph dependency = new();
        Dictionary<string, SourceQueryItem> items = new();

        int count = 0;
        foreach (QuerySettings querySettings in _processSettings.Queries)
        {
            count++;
            string entityName = GetEntityName(count, querySettings.EntityName, items);
            string definitionDirectory = GetDefinitionDirectory(count, entityName);
            string definitionGroupId = GetDefinitionGroupId(querySettings.DefinitionGroupId, entityName);
            items.Add(entityName,
                new SourceQueryItem(
                    entityName,
                    definitionGroupId,
                    GetManifestFileName(count, definitionDirectory, querySettings.ManifestFileName),
                    OutputDirectory,
                    _destinationSettings.OutputBlobStorage ?? string.Empty,
                    GetPackageHeaderFileName(count, definitionDirectory, querySettings.PackageHeaderFileName),
                    GetQueryFileName(count, definitionDirectory, querySettings.QueryFileName, entityName),
                    querySettings.RecordsPerFile,
                    GetSourceConnectionString(count, querySettings.SourceConnectionString),
                    querySettings.Dependencies ?? new List<string>())
                    );
            _ = new OrderedProcess(dependency, entityName);
        }
        foreach (OrderedProcess process in dependency.Processes)
        {
            SourceQueryItem source = items[process.Name]
                    ?? throw new Exception($"Source query '{process.Name}' not found.");
            List<OrderedProcess> parents = new();
            foreach (string dependsOn in source.Dependencies.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                OrderedProcess parent =
                dependency.Processes
                    .FirstOrDefault(p => p.Name.Equals(dependsOn, StringComparison.InvariantCultureIgnoreCase))
                    ?? throw new Exception($"Process with name '{dependsOn}' not found in dependency graph.");
                _ = process.Before(parent);
            }
        }
        SortedQueries = new();
        IEnumerable<IEnumerable<OrderedProcess>> ordered = dependency.CalculateSort();
        foreach (IEnumerable<OrderedProcess> orderedItemSet in ordered.Reverse())
        {
            SortedQueries.Add(items
                .Where(p => orderedItemSet
                     .Select(n => n.Name)
                     .Contains(p.Key)
                     ).Select(o => o.Value)
                     .ToList());
        }
        _logger.LogInformation(
            "Process Order : \n{ProcessOrder}",
            string.Join(
                "\n",
                SortedQueries.Select(s => string.Join(
                    " - ", s.Select(p => p.EntityName)))));
    }

    public int MaxDegreeOfParallelism => _processSettings.MaxDegreeOfParallelism;

    public string? OutputBlobStorage => _destinationSettings.OutputBlobStorage;

    public string OutputDirectory { get; }

    public List<List<SourceQueryItem>> SortedQueries { get; }

    private static string GetDefinitionGroupId(string? definitionGroupId, string entityName)
    {
        return string.IsNullOrWhiteSpace(definitionGroupId) ? $"DMF_{entityName}" : definitionGroupId;
    }

    private static string GetEntityName(int queryNumber, string? name, Dictionary<string, SourceQueryItem> items)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException($"Source Query {queryNumber} : Entity name not defined.");
        }
        string entityName = name.ToUpper();
        return items.ContainsKey(entityName)
            ? throw new ArgumentException($"Source Query {queryNumber} : Duplicate entity name.")
            : entityName;
    }

    private static string GetManifestFileName(int count, string directory, string? name)
    {
        name = string.IsNullOrWhiteSpace(name)
            ? Path.Combine(directory, "Manifest.xml")
            : Path.Combine(directory, name);
        return File.Exists(name)
            ? name
            : throw new Exception($"The manifest file not found in query {count} : {name}");
    }

    private static string GetPackageHeaderFileName(int count, string directory, string? name)
    {
        name = string.IsNullOrWhiteSpace(name)
            ? Path.Combine(directory, "PackageHeader.xml")
            : Path.Combine(directory, name);
        return File.Exists(name)
        ? name
            : throw new Exception($"The package header file not found in query {count} : {name}");
    }

    private static string GetQueryFileName(int count, string directory, string? name, string entityName)
    {
        name = string.IsNullOrWhiteSpace(name)
            ? Path.Combine(directory, entityName + ".sql")
            : Path.Combine(directory, name);
        return File.Exists(name)
        ? name
            : throw new Exception($"The query file not found in query {count} : {name}");
    }

    private string GetDefinitionDirectory(int count, string entityName)
    {
        string directory = string.IsNullOrWhiteSpace(_processSettings.DefinitionDirectory)
            ? Directory.GetCurrentDirectory()
            : _processSettings.DefinitionDirectory;
        directory = Path.Combine(directory, entityName);
        return !Directory.Exists(directory)
            ? throw new Exception($"The definition directory does not exist for query {count} : {directory}")
            : directory;
    }

    private string GetOutputDirectory(string? name)
    {
        string directory = string.IsNullOrWhiteSpace(_destinationSettings.OutputDirectory)
            ? Path.Combine(Directory.GetCurrentDirectory(), "Output")
            : _destinationSettings.OutputDirectory;
        if (!Directory.Exists(directory))
        {
            _logger.LogInformation("The output directory does not exist, creating directory : {OutputDirectory}", name);
            _ = Directory.CreateDirectory(directory);
        }
        return directory;
    }

    private string GetSourceConnectionString(int count, string? sourceConnectionString)
    {
        string? connection = string.IsNullOrWhiteSpace(sourceConnectionString)
            ? _sourceSettings.SourceConnectionString
            : sourceConnectionString;
        return string.IsNullOrWhiteSpace(connection)
            ? throw new Exception($"The connection string is not defined in query {count} settings and neither in the global settings.")
            : connection;
    }
}