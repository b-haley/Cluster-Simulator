using ClusterSim.Core.Features.ClusterNode;

namespace ClusterSim.Core.Features.Cluster;

/// <summary>
/// Сериализуемое описание пользовательского кластера:
/// то, что можно сохранить в файл или базу и загрузить в другом запуске программы.
/// </summary>
public sealed record ClusterDefinition
{
    /// <summary>Уникальный идентификатор конфигурации.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Название конфигурации кластера.</summary>
    public string Name { get; init; } = "Без названия";

    /// <summary>Произвольное описание.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Дата создания конфигурации.</summary>
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Дата последнего изменения конфигурации.</summary>
    public DateTime? ModifiedUtc { get; init; }

    /// <summary>Список узлов пользовательского кластера.</summary>
    public List<ClusterNodeDefinition> Nodes { get; init; } = new();
}