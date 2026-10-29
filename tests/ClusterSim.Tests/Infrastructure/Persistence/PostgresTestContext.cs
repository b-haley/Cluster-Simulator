using ClusterSim.Infrastructure.Configuration;
using ClusterSim.Infrastructure.Persistence;
using Npgsql;

namespace ClusterSim.Tests.Infrastructure.Persistence;

/// <summary>
/// Проверяет доступность PostgreSQL из Docker и подготавливает схему.
/// Если контейнер не поднят, тесты инфраструктуры аккуратно пропускаются,
/// чтобы не валить весь прогон dotnet test.
/// </summary>
public static class PostgresTestContext
{
    public static bool TryCreateFactory(out IDbConnectionFactory? factory, out string? skipReason)
    {
        try
        {
            var candidate = new NpgsqlConnectionFactory(new PostgresOptions());

            using var connection = candidate.CreateOpenConnection();
            using var command = new NpgsqlCommand("SELECT 1", connection);
            command.ExecuteScalar();

            factory = candidate;
            skipReason = null;
            return true;
        }
        catch (Exception exception)
        {
            factory = null;
            skipReason = $"PostgreSQL недоступен (выполни: docker compose up -d). {exception.Message}";
            return false;
        }
    }

    public static bool TryCreateStore(out PostgresClusterDefinitionStore? store, out string? skipReason)
    {
        if (!TryCreateFactory(out var factory, out skipReason))
        {
            store = null;
            return false;
        }

        new PostgresSchemaInitializer(factory!).Initialize();
        store = new PostgresClusterDefinitionStore(factory!);
        return true;
    }
}