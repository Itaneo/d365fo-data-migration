namespace Dynamics365ImportData.XmlOutput;

using Dynamics365ImportData.DependencySorting;

using System.Threading.Tasks;

public interface IXmlOutputFactory
{
    Task<IXmlOutputPart> CreateAsync(SourceQueryItem queryItem, int part, CancellationToken cancellationToken);
}