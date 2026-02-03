namespace Dynamics365ImportData.Tests.Unit.Sanitization;

using Dynamics365ImportData.Sanitization;

using Shouldly;

using Xunit;

public class RegexResultSanitizerTests
{
    private readonly RegexResultSanitizer _sanitizer = new();

    [Fact]
    public void Sanitize_ConnectionString_ReplacesWithRedacted()
    {
        // Arrange
        var input = "Error connecting: Server=myserver.database.windows.net;Database=mydb;User=admin;Password=secret";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("myserver");
        result.ShouldNotContain("mydb");
    }

    [Fact]
    public void Sanitize_DataSourceConnectionString_ReplacesWithRedacted()
    {
        // Arrange
        var input = "Failed: Data Source=tcp:myserver.database.windows.net,1433;Initial Catalog=mydb";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("myserver");
    }

    [Fact]
    public void Sanitize_BearerToken_ReplacesWithRedacted()
    {
        // Arrange
        var input = "Auth failed: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abc123def456";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("eyJhbGci");
    }

    [Fact]
    public void Sanitize_SasToken_ReplacesWithRedacted()
    {
        // Arrange
        var input = "URL: https://storage.blob.core.windows.net/container?sig=abc123def456&sv=2021-06-08&se=2026-01-01";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("abc123def456");
        result.ShouldNotContain("2021-06-08");
    }

    [Fact]
    public void Sanitize_ClientSecret_ReplacesWithRedacted()
    {
        // Arrange
        var input = "Auth error: client_secret=some-secret-value-here";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("some-secret-value-here");
    }

    [Fact]
    public void Sanitize_TenantId_ReplacesWithRedacted()
    {
        // Arrange
        var input = "AADSTS error: tenant_id=12345678-1234-1234-1234-123456789012";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("12345678-1234-1234-1234-123456789012");
    }

    [Fact]
    public void Sanitize_ClientId_ReplacesWithRedacted()
    {
        // Arrange
        var input = "Auth error: client_id=12345678-abcd-1234-abcd-123456789012";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("12345678-abcd-1234-abcd-123456789012");
    }

    [Fact]
    public void Sanitize_PasswordInConnectionString_ReplacesWithRedacted()
    {
        // Arrange
        var input = "Error: Server=myserver;Database=mydb;User=admin;Password=SuperSecret123";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("SuperSecret123");
    }

    [Fact]
    public void Sanitize_PwdAlias_ReplacesWithRedacted()
    {
        // Arrange
        var input = "Connection failed: Pwd=MyP@ssw0rd;";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("MyP@ssw0rd");
    }

    [Fact]
    public void Sanitize_MultiplePatterns_ReplacesAll()
    {
        // Arrange
        var input = "Error: Server=myserver;Database=mydb; token=Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abc123; client_secret=mysecret";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldNotContain("myserver");
        result.ShouldNotContain("eyJhbGci");
        result.ShouldNotContain("mysecret");
    }

    [Fact]
    public void Sanitize_NoCredentials_ReturnsInputUnchanged()
    {
        // Arrange
        var input = "Entity 'Customers' failed: The specified data source table does not exist.";

        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.ShouldBe(input);
    }

    [Fact]
    public void Sanitize_NullInput_ReturnsNull()
    {
        // Arrange & Act
        var result = _sanitizer.Sanitize(null!);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    {
        // Arrange & Act
        var result = _sanitizer.Sanitize(string.Empty);

        // Assert
        result.ShouldBe(string.Empty);
    }
}
