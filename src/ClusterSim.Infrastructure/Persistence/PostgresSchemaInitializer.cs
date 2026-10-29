using Npgsql;

namespace ClusterSim.Infrastructure.Persistence;

/// <summary>
/// Создает схему БД при старте приложения, если ее еще нет.
/// Благодаря этому docker/initdb-скрипты не обязательны: схема идемпотентна.
/// </summary>
public sealed class PostgresSchemaInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PostgresSchemaInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>DDL схемы: сохраненные кластеры и их узлы.</summary>
    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS cluster_definitions (
            id            uuid PRIMARY KEY,
            name          text NOT NULL,
            description   text NOT NULL DEFAULT '',
            created_utc   timestamptz NOT NULL,
            modified_utc  timestamptz NULL
        );

        CREATE TABLE IF NOT EXISTS cluster_nodes (
            id             integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            definition_id  uuid NOT NULL REFERENCES cluster_definitions (id) ON DELETE CASCADE,
            ordinal        integer NOT NULL,
            name           text NOT NULL,
            cpu_cores      integer NOT NULL CHECK (cpu_cores > 0),
            ram_mb         integer NOT NULL CHECK (ram_mb > 0),
            UNIQUE (definition_id, ordinal)
        );

        CREATE INDEX IF NOT EXISTS ix_cluster_nodes_definition
            ON cluster_nodes (definition_id, ordinal);
        """;

    /// <summary>Применяет схему к базе данных.</summary>
    public void Initialize()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = new NpgsqlCommand(SchemaSql, connection);
        command.ExecuteNonQuery();
    }
}