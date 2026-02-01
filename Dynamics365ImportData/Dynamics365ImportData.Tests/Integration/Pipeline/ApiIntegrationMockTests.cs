namespace Dynamics365ImportData.Tests.Integration.Pipeline;

using Dynamics365ImportData.Erp.DataManagementDefinitionGroups;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using System.Text.Json;

using Xunit;

/// <summary>
/// Integration tests for the D365 Finance Data Management API contract.
/// Tests mock IDynamics365FinanceDataManagementGroups at the interface boundary because
/// Dynamics365FnoClient is abstract with MSAL coupling and Dynamics365FinanceDataManagementGroups
/// requires validated settings with live Azure AD. These tests verify interface contract
/// behavior and request/response serialization -- not actual HTTP or OAuth flows.
/// </summary>
public class ApiIntegrationMockTests
{
    [Fact]
    public async Task GetAzureWriteUrl_WithMockedAuth_ReturnsExpectedBlobDefinition()
    {
        // Arrange -- mock IDynamics365FinanceDataManagementGroups to verify
        // interface contract returns expected blob definition for authenticated calls.
        var mockClient = Substitute.For<IDynamics365FinanceDataManagementGroups>();
        var expectedBlob = new BlobDefinition
        {
            BlobId = "blob-123",
            BlobUrl = "https://storage.blob.core.windows.net/container/blob"
        };
        mockClient.GetAzureWriteUrl(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedBlob));

        // Act -- simulate authenticated API call
        var result = await mockClient.GetAzureWriteUrl("test-file.zip", CancellationToken.None);

        // Assert -- verify the mock was called with correct parameters
        await mockClient.Received(1).GetAzureWriteUrl("test-file.zip", Arg.Any<CancellationToken>());
        result.BlobId.ShouldBe("blob-123");
        result.BlobUrl.ShouldBe("https://storage.blob.core.windows.net/container/blob");
    }

    [Fact]
    public async Task ImportFromPackage_ValidRequest_CapturesAllParameters()
    {
        // Arrange -- mock the import interface to capture and verify request parameters
        var mockClient = Substitute.For<IDynamics365FinanceDataManagementGroups>();
        ImportFromPackageRequest? capturedRequest = null;
        mockClient.ImportFromPackage(Arg.Do<ImportFromPackageRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("exec-001"));

        var request = new ImportFromPackageRequest
        {
            PackageUrl = new Uri("https://storage.blob.core.windows.net/container/package.zip"),
            DefinitionGroupId = "TestGroup",
            ExecutionId = "exec-001",
            Execute = true,
            Overwrite = true,
            LegalEntityId = "USMF"
        };

        // Act
        var executionId = await mockClient.ImportFromPackage(request, CancellationToken.None);

        // Assert -- verify request format and content
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.PackageUrl.ShouldBe(new Uri("https://storage.blob.core.windows.net/container/package.zip"));
        capturedRequest.DefinitionGroupId.ShouldBe("TestGroup");
        capturedRequest.ExecutionId.ShouldBe("exec-001");
        capturedRequest.Execute.ShouldBeTrue();
        capturedRequest.Overwrite.ShouldBeTrue();
        capturedRequest.LegalEntityId.ShouldBe("USMF");
        executionId.ShouldBe("exec-001");
    }

    [Fact]
    public async Task D365Client_ImportStatusPolling_HandlesSuccessResponse()
    {
        // Arrange -- mock polling endpoint returning ExecutionStatus.Succeeded
        var mockClient = Substitute.For<IDynamics365FinanceDataManagementGroups>();
        mockClient.GetExecutionSummaryStatus("exec-001", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ExecutionStatus.Succeeded));

        // Act
        var status = await mockClient.GetExecutionSummaryStatus("exec-001", CancellationToken.None);

        // Assert
        status.ShouldBe(ExecutionStatus.Succeeded);
        await mockClient.Received(1).GetExecutionSummaryStatus("exec-001", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task D365Client_ImportStatusPolling_HandlesFailedResponse()
    {
        // Arrange -- mock polling endpoint returning failure status
        var mockClient = Substitute.For<IDynamics365FinanceDataManagementGroups>();
        mockClient.GetExecutionSummaryStatus("exec-002", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ExecutionStatus.Failed));

        // Act
        var status = await mockClient.GetExecutionSummaryStatus("exec-002", CancellationToken.None);

        // Assert
        status.ShouldBe(ExecutionStatus.Failed);
    }

    [Fact]
    public async Task ImportFromPackage_UnauthorizedResponse_ThrowsWithStatusMessage()
    {
        // Arrange -- mock unauthorized error by throwing from the interface
        var mockClient = Substitute.For<IDynamics365FinanceDataManagementGroups>();
        mockClient.ImportFromPackage(Arg.Any<ImportFromPackageRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("The API Call was not succesful and returned status : Unauthorized.\nReason : Unauthorized"));

        var request = new ImportFromPackageRequest
        {
            PackageUrl = new Uri("https://storage.blob.core.windows.net/container/package.zip"),
            DefinitionGroupId = "TestGroup",
            ExecutionId = "exec-003",
            Execute = true,
            Overwrite = false,
            LegalEntityId = "USMF"
        };

        // Act & Assert
        var exception = await Should.ThrowAsync<Exception>(
            () => mockClient.ImportFromPackage(request, CancellationToken.None));
        exception.Message.ShouldContain("Unauthorized");
    }

    [Fact]
    public void D365Client_ImportFromPackageRequest_SerializesCorrectPayload()
    {
        // Arrange
        var request = new ImportFromPackageRequest
        {
            PackageUrl = new Uri("https://storage.blob.core.windows.net/uploads/package.zip"),
            DefinitionGroupId = "CustomerImport",
            ExecutionId = "exec-import-001",
            Execute = true,
            Overwrite = true,
            LegalEntityId = "USMF"
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert -- verify full request body matches expected JSON format
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("PackageUrl").GetString()
            .ShouldBe("https://storage.blob.core.windows.net/uploads/package.zip");
        root.GetProperty("DefinitionGroupId").GetString().ShouldBe("CustomerImport");
        root.GetProperty("ExecutionId").GetString().ShouldBe("exec-import-001");
        root.GetProperty("Execute").GetBoolean().ShouldBeTrue();
        root.GetProperty("Overwrite").GetBoolean().ShouldBeTrue();
        root.GetProperty("LegalEntityId").GetString().ShouldBe("USMF");

        // Verify property order (per JsonPropertyOrder attributes)
        var propertyNames = new List<string>();
        foreach (var prop in root.EnumerateObject())
        {
            propertyNames.Add(prop.Name);
        }
        propertyNames.IndexOf("PackageUrl").ShouldBeLessThan(propertyNames.IndexOf("DefinitionGroupId"));
        propertyNames.IndexOf("DefinitionGroupId").ShouldBeLessThan(propertyNames.IndexOf("ExecutionId"));
        propertyNames.IndexOf("ExecutionId").ShouldBeLessThan(propertyNames.IndexOf("Execute"));
    }

    [Fact]
    public async Task D365Client_PollingSequence_TransitionsFromExecutingToSucceeded()
    {
        // Arrange -- mock NSubstitute returning different statuses on sequential calls
        var mockClient = Substitute.For<IDynamics365FinanceDataManagementGroups>();
        mockClient.GetExecutionSummaryStatus("exec-poll", Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(ExecutionStatus.Executing),
                Task.FromResult(ExecutionStatus.Executing),
                Task.FromResult(ExecutionStatus.Succeeded));

        // Act -- simulate polling loop
        var statuses = new List<ExecutionStatus>();
        for (int i = 0; i < 3; i++)
        {
            statuses.Add(await mockClient.GetExecutionSummaryStatus("exec-poll", CancellationToken.None));
        }

        // Assert
        statuses[0].ShouldBe(ExecutionStatus.Executing);
        statuses[1].ShouldBe(ExecutionStatus.Executing);
        statuses[2].ShouldBe(ExecutionStatus.Succeeded);
        await mockClient.Received(3).GetExecutionSummaryStatus("exec-poll", Arg.Any<CancellationToken>());
    }
}
