namespace Dynamics365ImportData.XmlOutput;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using global::Azure.Storage.Blobs;

using Microsoft.Extensions.Logging;

using System.IO.Compression;

public sealed class XmlD365FnoOutputPart : XmlZipOutputPart
{
    public XmlD365FnoOutputPart(
        Stream stream,
        Stream fileStream,
        ZipArchive zip,
        BlobClient blobClient,
        string partName,
        Func<IXmlOutputPart, CancellationToken, Task> postProcess,
        Func<IXmlOutputPart, CancellationToken, Task<ExecutionStatus>> getState,
        ILogger<XmlD365FnoOutputPart> logger)
        : base(stream, fileStream, zip, partName, postProcess, getState, logger) => BlobClient = blobClient;

    public BlobClient BlobClient { get; }
    public Uri BlobUrl => BlobClient.Uri;
}