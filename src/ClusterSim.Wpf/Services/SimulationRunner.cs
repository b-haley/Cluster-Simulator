using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Simulation;
using ClusterSim.Core.Simulation.Engine;
using ClusterSim.Core.Simulation.Metrics;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Scheduling.Placement;
using ClusterSim.Core.Simulation.Scheduling.Policies;
using ClusterSim.Core.Simulation.Workload;

namespace ClusterSim.Wpf.Services;

/// <summary>Результат одного прогона: конфигурация, метрики и итоговый снимок.</summary>
public sealed record RunResult(
    SimulationOptions Options,
    SimulationMetrics Metrics,
    SimulationSnapshot FinalSnapshot);

/// <summary>
/// Сервис запуска симуляции: собирает движок из конфигурации
/// и выполняет прогон в фоновом потоке, не блокируя интерфейс.
/// </summary>
public sealed class SimulationRunner
{
    /// <summary>Одиночный прогон в фоновом потоке.</summary>
    public Task<RunResult> RunSingleAsync(SimulationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => RunSingle(options), cancellationToken);
    }

    /// <summary>Прогоны нескольких конфигураций подряд: для сравнения политик.</summary>
    public Task<IReadOnlyList<RunResult>> RunComparisonAsync(
        IReadOnlyList<SimulationOptions> optionsList,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(optionsList);

        return Task.Run<IReadOnlyList<RunResult>>(
            () => optionsList.Select(RunSingle).ToList(),
            cancellationToken);
    }

    /// <summary>Синхронный прогон одной конфигурации.</summary>
    public RunResult RunSingle(SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var cluster = ClusterFactory.Create(options.Cluster);

        var engine = new SimulationEngine(
            cluster,
            new WorkloadGenerator(options.Workload),
            CreatePolicy(options.Policy.Kind),
            new FirstFitPlacement(),
            new MetricsCollector(options.TimeSeriesStep),
            options);

        var metrics = engine.Run();
        var snapshot = SnapshotMapper.ToSnapshot(cluster, engine.Jobs, metrics, engine.CurrentTime);

        return new RunResult(options, metrics, snapshot);
    }

    /// <summary>Фабрика политик по выбранному виду.</summary>
    public static ISchedulingPolicy CreatePolicy(SchedulingPolicyKind kind) => kind switch
    {
        SchedulingPolicyKind.Fifo => new FifoPolicy(),
        SchedulingPolicyKind.Priority => new PriorityPolicy(),
        SchedulingPolicyKind.EasyBackfill => new EasyBackfillPolicy(),
        SchedulingPolicyKind.FairShare => new FairSharePolicy(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестная политика планирования.")
    };
}