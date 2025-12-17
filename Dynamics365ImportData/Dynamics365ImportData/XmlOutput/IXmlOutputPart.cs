namespace Dynamics365ImportData.XmlOutput;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using System.Threading.Tasks;
using System.Xml;

public interface IXmlOutputPart : IDisposable
{
    DateTime? EndedTime { get; }
    string PartName { get; }
    DateTime StartedTime { get; }
    XmlWriter Writer { get; }

    void Close();

    Task<ExecutionStatus> GetStateAsync(CancellationToken cancellationToken = default);

    void Open();

    Task PostWriteProcessAsync(CancellationToken cancellationToken = default);
}