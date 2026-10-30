using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Metrics;

/// <summary>Метрики одного задания.</summary>
public sealed record JobMetrics(
    int JobId,
    string Owner,
    int Priority,
    double SubmitTime,
    double? StartTime,
    double? FinishTime,
    double? WaitTime,
    double? Turnaround,
    double? Slowdown,
    int RequiredCpuCores,
    int RequiredRamMb,
    int RequiredNodeCount,
    JobState State,
    int PreemptionCount);

/// <summary>Точка временного ряда загрузки кластера.</summary>
public sealed record UtilizationPoint(
    double Time,
    int RunningJobs,
    int PendingJobs,
    int UsedCpuCores,
    int TotalCpuCores,
    int UsedRamMb,
    int TotalRamMb);

/// <summary>Интегральная загрузка одного узла за прогон.</summary>
public sealed record NodeUtilization(
    int NodeId,
    string NodeName,
    int TotalCpuCores,
    double CpuUtilization,
    int TotalRamMb,
    double RamUtilization);

/// <summary>Итоговые метрики одного прогона симуляции.</summary>
public sealed record SimulationMetrics(
    string PolicyName,
    double Makespan,
    double AverageWaitTime,
    double AverageTurnaround,
    double AverageSlowdown,
    double CpuUtilization,
    double RamUtilization,
    double ThroughputPerHour,
    int TotalJobs,
    int CompletedJobs,
    int UnfinishedJobs,
    int RejectedJobs,
    int PreemptionCount,
    double AverageQueueLength,
    int MaxQueueLength,
    IReadOnlyList<JobMetrics> Jobs,
    IReadOnlyList<UtilizationPoint> TimeSeries,
    IReadOnlyList<NodeUtilization> NodeUtilizations);