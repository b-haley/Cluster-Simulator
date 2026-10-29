using ClusterSim.Core.Features.ClusterNode;

namespace ClusterSim.Core.Features.Cluster;

/// <summary>
/// Параметры сборки кластера для симуляции:
/// либо однородный кластер из N шаблонных узлов, либо готовый пользовательский набор.
/// </summary>
public sealed record ClusterOptions
{
    /// <summary>Количество узлов для однородного кластера.</summary>
    public int NodeCount { get; init; } = 4;

    /// <summary>Шаблон узла для однородного кластера.</summary>
    public ClusterNodeOptions DefaultNode { get; init; } = new();

    /// <summary>Пользовательский набор узлов; имеет приоритет над NodeCount.</summary>
    public IReadOnlyList<ClusterNodeDefinition>? CustomNodes { get; init; }
}