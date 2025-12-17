namespace Dynamics365ImportData.XmlOutput;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Threading.Tasks;

internal class XmlFileOutputFactory : IXmlOutputFactory
{
    private readonly IDynamics365FinanceDataManagementGroups _client;

    // private readonly int _importTimeout;
    private readonly string? _legalEntityId;

    private readonly ILogger<XmlFileOutputFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _timeout;

    public XmlFileOutputFactory(
            IDynamics365FinanceDataManagementGroups client,
        IOptions<Dynamics365Settings> settings,
        IServiceProvider serviceProvider,
        ILogger<XmlFileOutputFactory> logger)
    {
        _timeout = settings.Value.ImportTimeout;
        _legalEntityId = settings.Value.LegalEntityId ?? throw new ArgumentNullException(nameof(settings), $"The setting {nameof(Dynamics365Settings)}:{nameof(Dynamics365Settings.LegalEntityId)} is not defined.");
        _client = client;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task<IXmlOutputPart> CreateAsync(SourceQueryItem queryItem, int part, CancellationToken cancellationToken = default)
    {
        string partName = Path.Combine(
           queryItem.OutputDirectory,
           queryItem.DefinitionGroupId + (part > 0 ? $"_Part{part}_" : "_") + DateTime.Now.ToString("yyddMM_HHmmss_FFFF") + ".xml");
        return Task.FromResult<IXmlOutputPart>(new XmlOutputPart(
            GetFileSteam(partName),
            partName,
             (_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ExecutionStatus.Succeeded),
            _serviceProvider.GetRequiredService<ILogger<XmlOutputPart>>()));
    }

    public Stream GetFileSteam(string name)
    {
        _logger.LogInformation("Creating File : {FileName}", name);
        return new FileStream(name,
                              FileMode.Create,
                              FileAccess.Write,
                              FileShare.None,
                              8192);
    }
}