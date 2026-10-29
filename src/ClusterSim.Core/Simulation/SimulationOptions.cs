using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Workload;

namespace ClusterSim.Core.Simulation;

/// <summary>
/// Корневая конфигурация одного прогона симуляции:
/// кластер, поток заданий, политика и служебные ограничения.
/// </summary>
public sealed record SimulationOptions
{
    /// <summary>Параметры кластера.</summary>
    public ClusterOptions Cluster { get; init; } = new();

    /// <summary>Параметры потока заданий.</summary>
    public WorkloadOptions Workload { get; init; } = new();

    /// <summary>Параметры политики планирования.</summary>
    public PolicyOptions Policy { get; init; } = new();

    /// <summary>Ограничение виртуального времени прогона в секундах; 0 — без ограничения.</summary>
    public double MaxSimulationTime { get; init; }

    /// <summary>Шаг записи временных рядов загрузки в секундах.</summary>
    public double TimeSeriesStep { get; init; } = 60;
}