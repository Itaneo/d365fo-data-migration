namespace Dynamics365ImportData.XmlOutput;

using Dynamics365ImportData.DependencySorting;

using Microsoft.Extensions.Logging;

using System.IO.Compression;
using System.Threading.Tasks;

internal abstract class XmlPackageOutputFactoryBase : IXmlOutputFactory
{
    protected readonly ILogger _logger;
    protected readonly IServiceProvider _serviceProvider;

    public XmlPackageOutputFactoryBase(
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public abstract Task<IXmlOutputPart> CreateAsync(SourceQueryItem queryItem, int part, CancellationToken cancellationToken);

    protected async Task<IXmlOutputPart> CreateAsync(Stream fileStream, string partName, SourceQueryItem queryItem, params object[] parameters)
    {
        if (!File.Exists(queryItem.ManifestFileName))
            throw new FileNotFoundException($"Manifest file not found for entity '{queryItem.EntityName}': {queryItem.ManifestFileName}");
        if (!File.Exists(queryItem.PackageHeaderFileName))
            throw new FileNotFoundException($"Package header file not found for entity '{queryItem.EntityName}': {queryItem.PackageHeaderFileName}");

        var _zip = new ZipArchive(fileStream, ZipArchiveMode.Create);
        _ = _zip.CreateEntryFromFile(queryItem.ManifestFileName, "Manifest.xml", CompressionLevel.SmallestSize);
        _ = _zip.CreateEntryFromFile(queryItem.PackageHeaderFileName, "PackageHeader.xml", CompressionLevel.SmallestSize);
        Stream zipStream = _zip.CreateEntry(queryItem.EntityName + ".xml", CompressionLevel.SmallestSize).Open();
        return await Task.FromResult(CreatePart(
            zipStream,
            fileStream,
            _zip,
            partName,
            parameters));
    }

    protected abstract IXmlOutputPart CreatePart(
        Stream zipStream,
        Stream outputStream,
        ZipArchive zip,
        string partName,
        params object[] parameters);
}