namespace Dynamics365ImportData.Erp;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using global::Azure.Storage.Blobs;

using Microsoft.Extensions.Logging;

internal class Dynamics365Container
{
    private readonly IDynamics365FinanceDataManagementGroups _client;
    private readonly ILogger<Dynamics365Container> _logger;

    public Dynamics365Container(IDynamics365FinanceDataManagementGroups client, ILogger<Dynamics365Container> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(Stream, string)> GetBlockBlobSteam(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting writable Azure Blob from Dynamics with name : {BlobName}", name);
        BlobDefinition response = await _client.GetAzureWriteUrl(name + ".zip", cancellationToken);
        if (string.IsNullOrWhiteSpace(response.BlobUrl))
        {
            throw new InvalidOperationException($"Could not get the writable Blob from Dynamics. Name : {name}.");
        }
        BlobClient blobClient = new(new Uri(response.BlobUrl));
        return (await blobClient.OpenWriteAsync(true, null, cancellationToken), response.BlobUrl);
    }
}