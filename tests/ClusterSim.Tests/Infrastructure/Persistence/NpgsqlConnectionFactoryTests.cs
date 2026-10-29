using System.Data;
using ClusterSim.Infrastructure.Persistence;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace ClusterSim.Tests.Infrastructure.Persistence;

public class NpgsqlConnectionFactoryTests
{
    private readonly ITestOutputHelper _output;

    public NpgsqlConnectionFactoryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CreateOpenConnection_ReturnsOpenConnectionToPostgres()
    {
        if (!PostgresTestContext.TryCreateFactory(out var factory, out var skipReason))
        {
            _output.WriteLine(skipReason);
            return;
        }

        using var connection = factory!.CreateOpenConnection();

        Assert.Equal(ConnectionState.Open, connection.State);

        using var command = new NpgsqlCommand("SELECT 1", connection);
        Assert.Equal(1, command.ExecuteScalar());
    }
}