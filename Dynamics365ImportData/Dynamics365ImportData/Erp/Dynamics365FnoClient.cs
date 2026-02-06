namespace Dynamics365ImportData.Erp;

using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public abstract class Dynamics365FnoClient
{
	private const string _dataPath = "data";
	private const string _domain = "Microsoft.Dynamics.DataEntities";
	private readonly HttpClient _client;
	private readonly string _clientId;
	private readonly string _entity;
	private readonly ILogger _logger;
	private readonly string _secret;
	private readonly string _tenant;
	private readonly Uri _url;
	private IConfidentialClientApplication? _confidentialApplication;

	protected Dynamics365FnoClient(string entity, IOptions<Dynamics365Settings> settings, HttpClient client, ILogger logger)
	{
		_entity = entity;
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		if (string.IsNullOrWhiteSpace(settings.Value.Url?.OriginalString))
		{
			throw new ArgumentException($"The {nameof(settings.Value.Url)} setting is not defined.",
										nameof(settings));
		}
		if (string.IsNullOrWhiteSpace(settings.Value.Tenant))
		{
			throw new ArgumentException($"The {nameof(settings.Value.Tenant)} setting is not defined.",
										nameof(settings));
		}
		if (string.IsNullOrWhiteSpace(settings.Value.ClientId))
		{
			throw new ArgumentException($"The {nameof(settings.Value.ClientId)} setting is not defined.",
										nameof(settings));
		}
		if (string.IsNullOrWhiteSpace(settings.Value.Secret))
		{
			throw new ArgumentException($"The {nameof(settings.Value.Secret)} setting is not defined.",
										nameof(settings));
		}
		if (string.IsNullOrWhiteSpace(settings.Value.LegalEntityId))
		{
			throw new ArgumentException($"The {nameof(settings.Value.LegalEntityId)} setting is not defined.",
										nameof(settings));
		}
		_url = settings.Value.Url;
		_tenant = settings.Value.Tenant;
		_clientId = settings.Value.ClientId;
		_secret = settings.Value.Secret;
		LegalEntityId = settings.Value.LegalEntityId;
		client.BaseAddress = _url;
	}

	public string LegalEntityId { get; }

	private IConfidentialClientApplication ConfidentialApplication
		=> _confidentialApplication ??= ConfidentialClientApplicationBuilder
			.Create(_clientId)
			.WithAuthority(AzureCloudInstance.AzurePublic, _tenant)
			.WithClientSecret(_secret)
			.Build();

	public async Task<string> AcquireTokenWithSecret(CancellationToken cancellationToken = default)
	{
		string[] scopes = new string[] { $"{_url.OriginalString}/.default" };
		try
		{
			return (await ConfidentialApplication
				  .AcquireTokenForClient(scopes)
				  .ExecuteAsync(cancellationToken))
				  .AccessToken;
		}
		catch
		{
			_logger.LogTrace("Error while acquiring Token.\nScope={Scope}\nAuthority={Authority}\nClientId={ClientId}", _url.OriginalString, ConfidentialApplication.Authority, _clientId);
			throw;
		}
	}

	protected async Task<TResponse> PostRequestAsync<TRequest, TResponse>(string name, TRequest request, CancellationToken cancellationToken = default)
	{
		string url = $"/{_dataPath}/{_entity}/{_domain}.{name}";
		var content = JsonContent.Create(request);

		using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
		{
			Content = content
		};
		requestMessage.Headers.Add("OData-MaxVersion", "4.0");
		requestMessage.Headers.Add("OData-Version", "4.0");
		requestMessage.Headers.Add("Prefer", "odata.include-annotations = *");
		requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
			"Bearer",
			await AcquireTokenWithSecret(cancellationToken)
		);

		HttpResponseMessage response = await _client
			.SendAsync(requestMessage, cancellationToken);
		_logger.LogDebug("POST {Url}\nRequest Body : {RequestBody}",
			url,
			await content.ReadAsStringAsync(cancellationToken));
		if (response.IsSuccessStatusCode)
		{
			_logger.LogDebug("The method call to '{Path}' was succesfull.", url);
			try
			{
				ODataResponse value = (await response
					.Content
					.ReadFromJsonAsync<ODataResponse>
					(options: new JsonSerializerOptions()
					{
						PropertyNameCaseInsensitive = true
					},
					cancellationToken))
					?? throw new Exception($"The API Call '{name}' returned an empty body.");
				return typeof(TResponse) == typeof(string)
					? (TResponse)(object)(value.Value ?? string.Empty)
					: string.IsNullOrWhiteSpace(value.Value)
					? throw new Exception($"The API Call '{name}' returned an empty OData value.")
					: JsonSerializer.Deserialize<TResponse>(
						value.Value)
							?? throw new Exception($"Could not deserialize '{name}' returned OData value.\nType {typeof(TResponse)}\nValue : {value.Value}");
			}
			catch
			{
				_logger.LogError("Failed to read response content :\n{ResponseContent}", await response.Content.ReadAsStringAsync(cancellationToken));
				throw;
			}
		}
		string requestContent = await content.ReadAsStringAsync(cancellationToken);
		string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
		throw new Exception($"The API Call was not succesful and returned status : {response.StatusCode}.\nReason : {response.ReasonPhrase}\nRequestContent : {requestContent}\nResponseContent : {responseContent}");
	}
}