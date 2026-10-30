using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Simulation.Scheduling;

namespace ClusterSim.Core.Features.Cluster;

/// <summary>
/// Сериализуемое описание пользовательского кластера («заказ»):
/// состав узлов плюс конфигурация планировщика, которая используется по умолчанию.
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

    /// <summary>Политика планирования по умолчанию для этого кластера.</summary>
    public SchedulingPolicyKind Policy { get; init; } = SchedulingPolicyKind.Fifo;

    /// <summary>Строгий FIFO для политики Fifo.</summary>
    public bool StrictFifo { get; init; } = true;

    /// <summary>Разрешено ли вытеснение с контрольной точкой.</summary>
    public bool AllowPreemption { get; init; }

    /// <summary>Накладные расходы контрольной точки, сек.</summary>
    public double CheckpointOverheadSeconds { get; init; }

    /// <summary>Минимальное время работы до вытеснения, сек.</summary>
    public double MinRunTimeBeforePreemption { get; init; } = 60;

    /// <summary>Список узлов пользовательского кластера.</summary>
    public List<ClusterNodeDefinition> Nodes { get; init; } = new();
}