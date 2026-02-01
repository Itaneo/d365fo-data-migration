namespace Dynamics365ImportData.Tests.Unit.Services;

using Shouldly;

using System.Data.SqlClient;

using Xunit;

public class SqlConnectionBehaviorTests
{
    [Fact]
    public void SqlConnection_InvalidConnectionString_ThrowsExpectedException()
    {
        // Arrange & Act & Assert
        // System.Data.SqlClient throws ArgumentException for truly malformed connection strings
        // with invalid key=value syntax
        Should.Throw<ArgumentException>(() =>
        {
            using var connection = new SqlConnection("invalid key without equals sign");
        });
    }

    [Fact]
    public void SqlConnection_ConnectionStringFormat_ParsesCorrectly()
    {
        // Arrange
        var connectionString = "Server=myserver;Database=mydb;User Id=myuser;Password=mypassword;";

        // Act
        var builder = new SqlConnectionStringBuilder(connectionString);

        // Assert -- verify System.Data.SqlClient connection string builder behavior
        // This captures current .NET 8 baseline for comparison with Microsoft.Data.SqlClient
        builder.DataSource.ShouldBe("myserver");
        builder.InitialCatalog.ShouldBe("mydb");
        builder.UserID.ShouldBe("myuser");
        builder.Password.ShouldBe("mypassword");
    }
}
