using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Features.Resources;
using ClusterSim.Core.Simulation;
using ClusterSim.Core.Simulation.Engine;
using ClusterSim.Core.Simulation.Metrics;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Scheduling.Placement;
using ClusterSim.Core.Simulation.Workload;

namespace ClusterSim.Tests.Helpers;

/// <summary>
/// Фабрики небольших детерминированных сценариев для тестов.
/// </summary>
public static class TestScenarios
{
    /// <summary>Однородный кластер из nodeCount узлов с указанными ресурсами.</summary>
    public static Cluster CreateCluster(int nodeCount = 2, int cpu = 4, int ram = 8192) =>
        ClusterFactory.Create(new ClusterOptions
        {
            NodeCount = nodeCount,
            DefaultNode = new ClusterNodeOptions(cpu, ram)
        });

    /// <summary>Задание с явно заданными параметрами.</summary>
    public static Job CreateJob(
        int id,
        double submit,
        int cpu = 1,
        int ram = 512,
        double duration = 60,
        int priority = 0,
        string owner = "alice",
        int nodes = 1,
        double? estimate = null) =>
        new(
            id,
            owner,
            submit,
            priority,
            new ResourceVector(cpu, ram),
            nodes,
            estimate ?? duration,
            duration);

    /// <summary>Конфигурация прогона по умолчанию с мелким шагом временного ряда.</summary>
    public static SimulationOptions CreateOptions(
        PolicyOptions? policy = null,
        ClusterOptions? cluster = null,
        WorkloadOptions? workload = null) =>
        new()
        {
            Cluster = cluster ?? new ClusterOptions { NodeCount = 2, DefaultNode = new ClusterNodeOptions(4, 8192) },
            Workload = workload ?? new WorkloadOptions { JobCount = 0 },
            Policy = policy ?? new PolicyOptions(),
            TimeSeriesStep = 10
        };

    /// <summary>Собирает движок поверх статического списка заданий.</summary>
    public static SimulationEngine CreateEngine(
        IReadOnlyList<Job> jobs,
        ISchedulingPolicy policy,
        SimulationOptions? options = null)
    {
        options ??= CreateOptions();

        var cluster = ClusterFactory.Create(options.Cluster);

        return new SimulationEngine(
            cluster,
            new StaticJobSource(jobs),
            policy,
            new FirstFitPlacement(),
            new MetricsCollector(options.TimeSeriesStep),
            options);
    }

    /// <summary>Прогоняет сценарий и возвращает метрики.</summary>
    public static SimulationMetrics Run(
        IReadOnlyList<Job> jobs,
        ISchedulingPolicy policy,
        SimulationOptions? options = null) =>
        CreateEngine(jobs, policy, options).Run();

    /// <summary>Контекст планирования для юнит-тестов политик без движка.</summary>
    public static SchedulingContext CreateContext(
        double time,
        Cluster cluster,
        IReadOnlyList<Job> pending,
        IReadOnlyList<Job> running,
        PolicyOptions? policy = null,
        IReadOnlyDictionary<string, double>? usage = null) =>
        new(
            time,
            cluster,
            pending,
            running,
            new FirstFitPlacement(),
            policy ?? new PolicyOptions(),
            usage ?? new Dictionary<string, double>());
}