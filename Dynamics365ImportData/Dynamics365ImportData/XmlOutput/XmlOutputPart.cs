namespace Dynamics365ImportData.XmlOutput;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using Microsoft.Extensions.Logging;

using System.Xml;

public class XmlOutputPart : IXmlOutputPart
{
    private readonly Func<IXmlOutputPart, CancellationToken, Task<ExecutionStatus>> _getState;
    private readonly ILogger _logger;
    private readonly Func<IXmlOutputPart, CancellationToken, Task> _postProcess;
    private Stream? _stream;
    private XmlWriter? _writer;

    public XmlOutputPart(
        Stream stream,
        string partName,
        Func<IXmlOutputPart, CancellationToken, Task> postProcess,
        Func<IXmlOutputPart, CancellationToken, Task<ExecutionStatus>> getState,
        ILogger<XmlOutputPart> logger)
    {
        _stream = stream;
        PartName = partName;
        _logger = logger;
        _postProcess = postProcess;
        _getState = getState;
    }

    public DateTime? EndedTime { get; }
    public string PartName { get; }

    public DateTime StartedTime { get; } = DateTime.Now;

    public XmlWriter Writer => _writer
            ?? throw new Exception($"Trying to use the XmlWriter befor openning it. PartName={PartName}");

    public virtual void Close()
    {
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Close();
            _writer.Dispose();
            _writer = null;
        }
        if (_stream != null)
        {
            _stream.Flush();
            _stream.Close();
            _stream.Dispose();
            _stream = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual async Task<ExecutionStatus> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return await _getState(this, cancellationToken);
    }

    public virtual void Open()
    {
        if (_stream == null)
        {
            throw new InvalidOperationException($"The writer has already been desallocated for {PartName}.");
        }
        if (_writer != null)
        {
            throw new InvalidOperationException($"The writer has already been opened for {PartName}.");
        }
        _logger.LogTrace("Creating output : {PackageFileName}", PartName);
        _writer = XmlWriter.Create(_stream, new XmlWriterSettings()
        {
            Async = true,
            Indent = false,
            Encoding = System.Text.Encoding.UTF8
        });
    }

    public virtual async Task PostWriteProcessAsync(CancellationToken cancellationToken = default)
    {
        await _postProcess(this, cancellationToken);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }
    }
}