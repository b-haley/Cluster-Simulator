using Npgsql;
using ClusterSim.Infrastructure.Configuration;

namespace ClusterSim.Infrastructure.Persistence;

/// <summary>
/// Абстракция фабрики подключений: позволяет подменить источник соединений в тестах.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Создает и открывает подключение к PostgreSQL.</summary>
    NpgsqlConnection CreateOpenConnection();
}

/// <summary>
/// Реализация фабрики подключений на Npgsql.
/// </summary>
public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly PostgresOptions _options;

    public NpgsqlConnectionFactory(PostgresOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public NpgsqlConnection CreateOpenConnection()
    {
        var connection = new NpgsqlConnection(_options.GetConnectionString());
        connection.Open();
        return connection;
    }
}