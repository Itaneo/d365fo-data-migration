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

    private readonly int _executionStatusInitialDelayMs;
    private readonly int _executionStatusMaxRetries;
    private readonly int _executionStatusRetryDelayMs;
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
        _executionStatusInitialDelayMs = settings.Value.ExecutionStatusInitialDelaySeconds * 1000;
        _executionStatusMaxRetries = settings.Value.ExecutionStatusMaxRetries;
        _executionStatusRetryDelayMs = settings.Value.ExecutionStatusRetryDelaySeconds * 1000;
        _legalEntityId = settings.Value.LegalEntityId ?? throw new ArgumentNullException(nameof(settings), $"The setting {nameof(Dynamics365Settings)}:{nameof(Dynamics365Settings.LegalEntityId)} is not defined.");
        _client = client;
    }

    public Task CheckAsync(IXmlOutputPart part) => throw new NotImplementedException();

    public override async Task<IXmlOutputPart> CreateAsync(SourceQueryItem queryItem, int part, CancellationToken cancellationToken)
    {
        string partName = queryItem.DefinitionGroupId + (part > 0 ? $"_Part{part}_" : "_") + DateTime.Now.ToString("yyddMM_HHmmss_FFFF");
        (Stream stream, BlobClient blobClient) = await GetBlockBlobSteam(partName, cancellationToken);
        return await CreateAsync(stream, partName, queryItem, blobClient, queryItem.DefinitionGroupId);
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
                DefinitionGroupId = part.DefinitionGroupId,
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

            _logger.LogInformation("Waiting {DelayMs}ms before checking execution status for {ExecutionId}.", _executionStatusInitialDelayMs, executionId);
            await Task.Delay(_executionStatusInitialDelayMs, cancellationToken);

            ExecutionStatus status = await GetExecutionSummaryStatusWithRetry(executionId, cancellationToken);
            _logger.LogInformation(
                "Waiting for import execution {ExecutionId} to complete. Current Status={Status}.",
                executionId,
                status);

            int count = 4 * 15;
            while (--count > 0 && status is ExecutionStatus.NotRun or ExecutionStatus.Executing or ExecutionStatus.Unknown)
            {
                await Task.Delay(15000, cancellationToken);
                status = await GetExecutionSummaryStatusWithRetry(executionId, cancellationToken);
                _logger.LogInformation(
                    "Waiting for import execution {ExecutionId} to complete. Current Status={Status}.",
                    executionId,
                    status);
            }
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

    private async Task<ExecutionStatus> GetExecutionSummaryStatusWithRetry(string executionId, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= _executionStatusMaxRetries; attempt++)
        {
            try
            {
                return await _client.GetExecutionSummaryStatus(executionId, cancellationToken);
            }
            catch (Exception ex) when (attempt < _executionStatusMaxRetries && ex.Message.Contains("Execution details were not found for execution id"))
            {
                _logger.LogWarning(
                    "Execution details not found for {ExecutionId} (attempt {Attempt}/{MaxRetries}). Retrying in {DelayMs}ms.",
                    executionId,
                    attempt + 1,
                    _executionStatusMaxRetries,
                    _executionStatusRetryDelayMs);
                await Task.Delay(_executionStatusRetryDelayMs, cancellationToken);
            }
        }

        // This should not be reached, but just in case:
        return await _client.GetExecutionSummaryStatus(executionId, cancellationToken);
    }

    protected override IXmlOutputPart CreatePart(Stream zipStream, Stream outputStream, ZipArchive zip, string partName, params object[] parameters)
    {
        return parameters.Length != 2
            ? throw new ArgumentNullException(nameof(parameters), "Should contain two parameters : The Blob client instance and the DefinitionGroupId.")
            : new XmlD365FnoOutputPart(
            zipStream,
            outputStream,
            zip,
            parameters[0] as BlobClient ?? throw new ArgumentNullException(nameof(parameters), "Blob client instance is null."),
            partName,
            parameters[1] as string ?? throw new ArgumentNullException(nameof(parameters), "DefinitionGroupId is null."),
            async (part, token) => await PostWriteAsync((XmlD365FnoOutputPart)part, token),
            async (part, token) => await GetStateAsync((XmlD365FnoOutputPart)part, token),
            _serviceProvider.GetRequiredService<ILogger<XmlD365FnoOutputPart>>());
    }
}