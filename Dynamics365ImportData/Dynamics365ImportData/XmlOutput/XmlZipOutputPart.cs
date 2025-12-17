namespace Dynamics365ImportData.XmlOutput;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using Microsoft.Extensions.Logging;

using System.IO.Compression;

public class XmlZipOutputPart : XmlOutputPart
{
    private Stream? _fileStream;

    private ZipArchive? _zip;

    public XmlZipOutputPart(
        Stream stream,
        Stream fileStream,
        ZipArchive zip,
        string partName,
        Func<IXmlOutputPart, CancellationToken, Task> postProcess,
        Func<IXmlOutputPart, CancellationToken, Task<ExecutionStatus>> getState,
        ILogger<XmlOutputPart> logger)
        : base(stream, partName, postProcess, getState, logger)
    {
        _fileStream = fileStream;
        _zip = zip;
    }

    public override void Close()
    {
        base.Close();
        if (_zip != null)
        {
            _zip.Dispose();
            _zip = null;
        }
        if (_fileStream != null)
        {
            _fileStream.Dispose();
            _fileStream = null;
        }
    }
}