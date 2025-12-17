namespace Dynamics365ImportData.Azure;

using Dynamics365ImportData.Settings;

using global::Azure.Storage.Blobs;
using global::Azure.Storage.Blobs.Models;
using global::Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal class AzureContainer
{
    private readonly BlobContainerClient _containerService;
    private readonly ILogger<AzureContainer> _logger;
    private readonly Uri _sharedAccessSignatureUrl;

    public AzureContainer(IOptions<DestinationSettings> settings, ILogger<AzureContainer> logger)
    {
        _logger = logger;
        if (string.IsNullOrWhiteSpace(settings.Value.OutputBlobStorage))
        {
            throw new ArgumentException("The OutputBlobStorage setting is not defined.", nameof(settings));
        }
        _sharedAccessSignatureUrl = new Uri(settings.Value.OutputBlobStorage);
        _containerService = new BlobContainerClient(_sharedAccessSignatureUrl, new BlobClientOptions());
    }

    public async Task<Stream> GetBlockBlobSteam(string name, CancellationToken cancellationToken = default)
    {
        return await _containerService
            .GetBlockBlobClient(name)
            .OpenWriteAsync(true, null, cancellationToken);
    }

    internal async Task DeleteAll(CancellationToken cancellationToken = default)
    {
        await foreach (BlobHierarchyItem blobItem in _containerService.GetBlobsByHierarchyAsync(BlobTraits.None,
                                                                            BlobStates.None,
                                                                            null,
                                                                            null,
                                                                            cancellationToken))
        {
            if (!blobItem.IsPrefix)
            {
                _logger.LogInformation("Deleting blob {BlobName}", blobItem.Blob.Name);

                _ = await _containerService.DeleteBlobIfExistsAsync(blobItem.Blob.Name,
                                                                   DeleteSnapshotsOption.None,
                                                                   null,
                                                                   cancellationToken);
            }
        }
    }
}