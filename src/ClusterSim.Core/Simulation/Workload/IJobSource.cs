using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Workload;

/// <summary>
/// Источник заданий для движка: генератор, статический список, файл и т.п.
/// Движок зависит только от этого интерфейса, что упрощает тестирование.
/// </summary>
public interface IJobSource
{
    /// <summary>Возвращает полный поток заданий для прогона.</summary>
    IReadOnlyList<Job> GetJobs();
}