using Npgsql;

namespace ClusterSim.Infrastructure.Persistence;

/// <summary>
/// Создает схему БД при старте приложения, если ее еще нет.
/// ALTER ... ADD COLUMN IF NOT EXISTS обеспечивает мягкую миграцию старых баз.
/// </summary>
public sealed class PostgresSchemaInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PostgresSchemaInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>DDL схемы: сохраненные кластеры, их узлы и миграции.</summary>
    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS cluster_definitions (
            id            uuid PRIMARY KEY,
            name          text NOT NULL,
            description   text NOT NULL DEFAULT '',
            created_utc   timestamptz NOT NULL,
            modified_utc  timestamptz NULL,
            policy        integer NOT NULL DEFAULT 0,
            strict_fifo   boolean NOT NULL DEFAULT true,
            allow_preemption boolean NOT NULL DEFAULT false,
            checkpoint_overhead_seconds double precision NOT NULL DEFAULT 0,
            min_run_time_before_preemption double precision NOT NULL DEFAULT 60
        );

        CREATE TABLE IF NOT EXISTS cluster_nodes (
            id             integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            definition_id  uuid NOT NULL REFERENCES cluster_definitions (id) ON DELETE CASCADE,
            ordinal        integer NOT NULL,
            name           text NOT NULL,
            cpu_cores      integer NOT NULL CHECK (cpu_cores > 0),
            ram_mb         integer NOT NULL CHECK (ram_mb > 0),
            disk_gb        integer NOT NULL DEFAULT 512 CHECK (disk_gb > 0),
            UNIQUE (definition_id, ordinal)
        );

        CREATE INDEX IF NOT EXISTS ix_cluster_nodes_definition
            ON cluster_nodes (definition_id, ordinal);

        ALTER TABLE cluster_definitions ADD COLUMN IF NOT EXISTS policy integer NOT NULL DEFAULT 0;
        ALTER TABLE cluster_definitions ADD COLUMN IF NOT EXISTS strict_fifo boolean NOT NULL DEFAULT true;
        ALTER TABLE cluster_definitions ADD COLUMN IF NOT EXISTS allow_preemption boolean NOT NULL DEFAULT false;
        ALTER TABLE cluster_definitions ADD COLUMN IF NOT EXISTS checkpoint_overhead_seconds double precision NOT NULL DEFAULT 0;
        ALTER TABLE cluster_definitions ADD COLUMN IF NOT EXISTS min_run_time_before_preemption double precision NOT NULL DEFAULT 60;
        ALTER TABLE cluster_nodes ADD COLUMN IF NOT EXISTS disk_gb integer NOT NULL DEFAULT 512;
        """;

    /// <summary>Применяет схему к базе данных.</summary>
    public void Initialize()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = new NpgsqlCommand(SchemaSql, connection);
        command.ExecuteNonQuery();
    }
}