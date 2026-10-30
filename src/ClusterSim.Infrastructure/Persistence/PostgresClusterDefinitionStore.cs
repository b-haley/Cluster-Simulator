using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Simulation.Scheduling;
using Npgsql;
using NpgsqlTypes;

namespace ClusterSim.Infrastructure.Persistence;

/// <summary>
/// Реализация хранилища конфигураций кластеров на PostgreSQL,
/// включая характеристики узлов и конфигурацию планировщика.
/// </summary>
public sealed class PostgresClusterDefinitionStore : IClusterDefinitionStore
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PostgresClusterDefinitionStore(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>Сохраняет конфигурацию: полное замещение предыдущей версии в одной транзакции.</summary>
    public void Save(ClusterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var delete = new NpgsqlCommand(
                   "DELETE FROM cluster_definitions WHERE id = @id", connection, transaction))
        {
            delete.Parameters.AddWithValue("id", definition.Id);
            delete.ExecuteNonQuery();
        }

        using (var insertDefinition = new NpgsqlCommand(
                   """
                   INSERT INTO cluster_definitions
                       (id, name, description, created_utc, modified_utc,
                        policy, strict_fifo, allow_preemption,
                        checkpoint_overhead_seconds, min_run_time_before_preemption)
                   VALUES
                       (@id, @name, @description, @createdUtc, @modifiedUtc,
                        @policy, @strictFifo, @allowPreemption,
                        @checkpointOverhead, @minRunTime)
                   """, connection, transaction))
        {
            insertDefinition.Parameters.AddWithValue("id", definition.Id);
            insertDefinition.Parameters.AddWithValue("name", definition.Name);
            insertDefinition.Parameters.AddWithValue("description", definition.Description);
            insertDefinition.Parameters.AddWithValue("createdUtc", ToUtc(definition.CreatedUtc));
            insertDefinition.Parameters.AddWithValue(
                "modifiedUtc",
                definition.ModifiedUtc.HasValue ? ToUtc(definition.ModifiedUtc.Value) : DBNull.Value);
            insertDefinition.Parameters.AddWithValue("policy", (int)definition.Policy);
            insertDefinition.Parameters.AddWithValue("strictFifo", definition.StrictFifo);
            insertDefinition.Parameters.AddWithValue("allowPreemption", definition.AllowPreemption);
            insertDefinition.Parameters.AddWithValue("checkpointOverhead", definition.CheckpointOverheadSeconds);
            insertDefinition.Parameters.AddWithValue("minRunTime", definition.MinRunTimeBeforePreemption);
            insertDefinition.ExecuteNonQuery();
        }

        using (var insertNode = new NpgsqlCommand(
                   """
                   INSERT INTO cluster_nodes (definition_id, ordinal, name, cpu_cores, ram_mb, disk_gb)
                   VALUES (@definitionId, @ordinal, @name, @cpuCores, @ramMb, @diskGb)
                   """, connection, transaction))
        {
            var definitionId = insertNode.Parameters.Add("definitionId", NpgsqlDbType.Uuid);
            var ordinal = insertNode.Parameters.Add("ordinal", NpgsqlDbType.Integer);
            var name = insertNode.Parameters.Add("name", NpgsqlDbType.Text);
            var cpuCores = insertNode.Parameters.Add("cpuCores", NpgsqlDbType.Integer);
            var ramMb = insertNode.Parameters.Add("ramMb", NpgsqlDbType.Integer);
            var diskGb = insertNode.Parameters.Add("diskGb", NpgsqlDbType.Integer);

            for (var index = 0; index < definition.Nodes.Count; index++)
            {
                var node = definition.Nodes[index];

                definitionId.Value = definition.Id;
                ordinal.Value = index;
                name.Value = node.Name;
                cpuCores.Value = node.CpuCores;
                ramMb.Value = node.RamMb;
                diskGb.Value = node.DiskGb;

                insertNode.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    /// <summary>Загружает конфигурацию вместе с узлами или возвращает null.</summary>
    public ClusterDefinition? Load(Guid id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = new NpgsqlCommand(SelectDefinitionSql + " WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        var definition = ReadDefinition(reader);
        reader.Close();

        return definition with { Nodes = ReadNodes(connection, definition.Id) };
    }

    /// <summary>Все сохраненные конфигурации с узлами, по возрастанию даты создания.</summary>
    public IReadOnlyList<ClusterDefinition> ListAll()
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        var definitions = new List<ClusterDefinition>();

        using (var command = new NpgsqlCommand(SelectDefinitionSql + " ORDER BY created_utc", connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                definitions.Add(ReadDefinition(reader));
            }
        }

        return definitions
            .Select(definition => definition with { Nodes = ReadNodes(connection, definition.Id) })
            .ToList();
    }

    /// <summary>Удаляет конфигурацию; узлы удаляются каскадно.</summary>
    public bool Delete(Guid id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = new NpgsqlCommand("DELETE FROM cluster_definitions WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);

        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>Проверяет существование конфигурации.</summary>
    public bool Exists(Guid id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        using var command = new NpgsqlCommand("SELECT 1 FROM cluster_definitions WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);

        return command.ExecuteScalar() is not null;
    }

    private const string SelectDefinitionSql =
        """
        SELECT id, name, description, created_utc, modified_utc,
               policy, strict_fifo, allow_preemption,
               checkpoint_overhead_seconds, min_run_time_before_preemption
        FROM cluster_definitions
        """;

    /// <summary>Читает определение кластера из текущей строки результата.</summary>
    private static ClusterDefinition ReadDefinition(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Name = reader.GetString(1),
        Description = reader.GetString(2),
        CreatedUtc = reader.GetDateTime(3),
        ModifiedUtc = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
        Policy = (SchedulingPolicyKind)reader.GetInt32(5),
        StrictFifo = reader.GetBoolean(6),
        AllowPreemption = reader.GetBoolean(7),
        CheckpointOverheadSeconds = reader.GetDouble(8),
        MinRunTimeBeforePreemption = reader.GetDouble(9)
    };

    /// <summary>Читает узлы конфигурации в порядке их следования в кластере.</summary>
    private static List<ClusterNodeDefinition> ReadNodes(NpgsqlConnection connection, Guid definitionId)
    {
        using var command = new NpgsqlCommand(
            "SELECT name, cpu_cores, ram_mb, disk_gb FROM cluster_nodes WHERE definition_id = @id ORDER BY ordinal",
            connection);
        command.Parameters.AddWithValue("id", definitionId);

        using var reader = command.ExecuteReader();

        var nodes = new List<ClusterNodeDefinition>();

        while (reader.Read())
        {
            nodes.Add(new ClusterNodeDefinition(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3)));
        }

        return nodes;
    }

    /// <summary>Npgsql требует Kind=Utc для timestamptz; значения без метки считаем UTC.</summary>
    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}