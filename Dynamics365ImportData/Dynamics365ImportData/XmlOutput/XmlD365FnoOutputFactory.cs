// Fiveforty S.A. Paris France (2022)
namespace Dynamics365ImportData.XmlOutput;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.Settings;

using global::Azure.Storage.Blobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

internal class XmlD365FnoOutputFactory : XmlPackageOutputFactoryBase
{
    private readonly IDynamics365FinanceDataManagementGroups _client;

    private readonly string? _legalEntityId;

    private readonly int _timeout;

    public XmlD365FnoOutputFactory(
        IDynamics365FinanceDataManagementGroups client,
        IOptions<Dynamics365Settings> settings,
        IServiceProvider serviceProvider,
        ILogger<XmlD365FnoOutputFactory> logger) : base(
            serviceProvider,
            logger)
    {
        _timeout = settings.Value.ImportTimeout;
        _legalEntityId = settings.Value.LegalEntityId ?? throw new ArgumentNullException(nameof(settings), $"The setting {nameof(Dynamics365Settings)}:{nameof(Dynamics365Settings.LegalEntityId)} is not defined.");
        _client = client;
    }

    public Task CheckAsync(IXmlOutputPart part) => throw new NotImplementedException();

    public override async Task<IXmlOutputPart> CreateAsync(SourceQueryItem queryItem, int part, CancellationToken cancellationToken)
    {
        string partName = queryItem.DefinitionGroupId + (part > 0 ? $"_Part{part}_" : "_") + DateTime.Now.ToString("yyddMM_HHmmss_FFFF");
        (Stream stream, BlobClient blobClient) = await GetBlockBlobSteam(partName, cancellationToken);
        return await CreateAsync(stream, partName, queryItem, blobClient);
    }

    public async Task<(Stream, BlobClient)> GetBlockBlobSteam(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting writable Azure Blob from Dynamics with name : {BlobName}", name);
        BlobDefinition response = await _client.GetAzureWriteUrl(name + ".zip", cancellationToken);
        if (string.IsNullOrWhiteSpace(response.BlobUrl))
        {
            throw new InvalidOperationException($"Could not get the writable Blob from Dynamics. Name : {name}.");
        }
        Uri blobUrl = new(response.BlobUrl);
        BlobClient blobClient = new(blobUrl);
        return (await blobClient.OpenWriteAsync(true, null, cancellationToken), blobClient);
    }

    public async Task<ExecutionStatus> GetStateAsync(XmlD365FnoOutputPart part, CancellationToken cancellationToken)
    {
        try
        {
            ExecutionStatus status = await _client.GetExecutionSummaryStatus(part.PartName, cancellationToken);
            _logger.LogTrace("The import Job '{ExecutionId}' status is {ExecutionState}", part.PartName, status);
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retreive the import Job '{part.PartName}' status.\nCheck Azure Blob : {AzureBlobUrl}", part.PartName, part.BlobUrl);
            return ExecutionStatus.Failed;
        }
    }

    public async Task PostWriteAsync(XmlD365FnoOutputPart part, CancellationToken cancellationToken)
    {
        try
        {
            part.Close();
            await Task.Delay(5000, cancellationToken);
            string executionId = await _client.ImportFromPackage(new ImportFromPackageRequest
            {
                Execute = true,
                ExecutionId = part.PartName,
                LegalEntityId = _legalEntityId,
                Overwrite = true,
                PackageUrl = part.BlobUrl
            }, cancellationToken);

            if (executionId != part.PartName)
            {
                throw new Exception($"The import package execution id should be the same as the package name : ExecutionId='{executionId}' PartName='{part.PartName}' ");
            }
            _logger.LogInformation("Imported Dynamics 365 package {PartName}.\nExecutionId={ExecutionId}", part.PartName, executionId);
            ExecutionStatus status;
            int count = 4 * 15;
            do
            {
                await Task.Delay(15000, cancellationToken);
                status = await _client.GetExecutionSummaryStatus(executionId, cancellationToken);
                _logger.LogInformation(
                    "Waiting for import execution {ExecutionId} to complete. Current Status={Status}.",
                    executionId,
                    status);
            }
            while (--count > 0 && status is ExecutionStatus.NotRun or ExecutionStatus.Executing or ExecutionStatus.Unknown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to import the package '{PartName}' to Dynamics 365 for finance and operations.Check Azure Blob : {AzureBlobUrl}",
                part.PartName,
                part.BlobUrl.AbsoluteUri);
            throw;
        }
    }

    protected override IXmlOutputPart CreatePart(Stream zipStream, Stream outputStream, ZipArchive zip, string partName, params object[] parameters)
    {
        return parameters.Length != 1
            ? throw new ArgumentNullException(nameof(parameters), "Should contain one parameter : The Blob client instance.")
            : new XmlD365FnoOutputPart(
            zipStream,
            outputStream,
            zip,
            parameters[0] as BlobClient ?? throw new ArgumentNullException(nameof(parameters), "Blob client instance is null."),
            partName,
            async (part, token) => await PostWriteAsync((XmlD365FnoOutputPart)part, token),
            async (part, token) => await GetStateAsync((XmlD365FnoOutputPart)part, token),
            _serviceProvider.GetRequiredService<ILogger<XmlD365FnoOutputPart>>());
    }
}