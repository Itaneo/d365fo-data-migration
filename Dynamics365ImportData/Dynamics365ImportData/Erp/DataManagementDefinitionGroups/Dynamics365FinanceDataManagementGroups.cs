namespace Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class Dynamics365FinanceDataManagementGroups : Dynamics365FnoClient, IDynamics365FinanceDataManagementGroups
{
	public Dynamics365FinanceDataManagementGroups(IOptions<Dynamics365Settings> settings, HttpClient client, ILogger<Dynamics365FinanceDataManagementGroups> logger)
		: base(nameof(DataManagementDefinitionGroups), settings, client, logger)

	{
	}

	public async Task<BlobDefinition> GetAzureWriteUrl(string uniqueFileName, CancellationToken cancellationToken = default) => await PostRequestAsync<UniqueFileNameRequest, BlobDefinition>(nameof(GetAzureWriteUrl),
																				new UniqueFileNameRequest(uniqueFileName),
																				cancellationToken);

	public async Task<ExecutionStatus> GetExecutionSummaryStatus(string executionId, CancellationToken cancellationToken = default)
	{
		string status = await PostRequestAsync<ExecutionIdRequest, string>(nameof(GetExecutionSummaryStatus),
																				new ExecutionIdRequest(executionId),
																				cancellationToken);
		return Enum.Parse<ExecutionStatus>(status);
	}

	public async Task<string> ImportFromPackage(ImportFromPackageRequest parameters, CancellationToken cancellationToken = default)
	{
		string executionId = await PostRequestAsync<ImportFromPackageRequest, string>(nameof(ImportFromPackage),
																				parameters,
																				cancellationToken);
		return executionId != parameters.ExecutionId
			? throw new Exception($"The returned execution id ({executionId}), is not the same as the one defined in the parameters : {parameters.ExecutionId}.\nParameters : {JsonSerializer.Serialize(parameters)}")
			: executionId;
	}
}