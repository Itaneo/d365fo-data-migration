namespace Dynamics365ImportData.XmlOutput;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.IO.Compression;
using System.Threading.Tasks;

internal class XmlPackageFileOutputFactory : XmlPackageOutputFactoryBase
{
    public XmlPackageFileOutputFactory(
        IServiceProvider serviceProvider,
        ILogger<XmlPackageFileOutputFactory> logger)
        : base(serviceProvider, logger)
    {
    }

    public override async Task<IXmlOutputPart> CreateAsync(SourceQueryItem queryItem, int part, CancellationToken cancellationToken = default)
    {
        string partName = Path.Combine(
          queryItem.OutputDirectory,
          queryItem.DefinitionGroupId + (part > 0 ? $"_Part{part}_" : "_") + DateTime.Now.ToString("yyddMM_HHmmss_FFFF") + ".zip");
        Stream fileStream = GetFileStream(partName);
        return await CreateAsync(fileStream, partName, queryItem);
    }

    public Stream GetFileStream(string name)
    {
        _logger.LogInformation("Creating compressed file : {FileName}", name);
        return new FileStream(name,
                              FileMode.Create,
                              FileAccess.Write,
                              FileShare.None,
                              8192);
    }

    protected override IXmlOutputPart CreatePart(
        Stream zipStream,
        Stream outputStream,
        ZipArchive zip,
        string partName,
        params object[] parameters)
    {
        return new XmlZipOutputPart(
            zipStream,
            outputStream,
            zip,
            partName,
            async (_, __) => await Task.CompletedTask,
            async (_, __) => await Task.FromResult(ExecutionStatus.Succeeded),
            _serviceProvider.GetRequiredService<ILogger<XmlZipOutputPart>>());
    }
}