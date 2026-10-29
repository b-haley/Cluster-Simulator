using System;
using System.Linq;
using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Simulation.Metrics;

namespace ClusterSim.Wpf.Services;

/// <summary>Снимок узла кластера для отображения.</summary>
public sealed record NodeSnapshot(
    int Id,
    string Name,
    int TotalCpu,
    int FreeCpu,
    int TotalRamMb,
    int FreeRamMb,
    int RunningJobCount,
    string State);

/// <summary>Снимок задания для таблиц интерфейса.</summary>
public sealed record JobSnapshot(
    int Id,
    string Owner,
    int Priority,
    string State,
    double SubmitTime,
    double? StartTime,
    double? FinishTime,
    double? WaitTime,
    double? Turnaround,
    int RequiredCpu,
    int RequiredRamMb,
    int NodeCount,
    string AssignedNodes,
    int Preemptions);

/// <summary>Снимок итоговых метрик прогона.</summary>
public sealed record MetricsSnapshot(
    double CurrentTime,
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
    int MaxQueueLength);

/// <summary>Полный снимок состояния симуляции для интерфейса.</summary>
public sealed record SimulationSnapshot(
    double CurrentTime,
    IReadOnlyList<NodeSnapshot> Nodes,
    IReadOnlyList<JobSnapshot> PendingJobs,
    IReadOnlyList<JobSnapshot> RunningJobs,
    IReadOnlyList<JobSnapshot> FinishedJobs,
    MetricsSnapshot Metrics);

/// <summary>Строка таблицы сравнения политик.</summary>
public sealed record ComparisonRow(
    string Policy,
    double Makespan,
    double AverageWaitTime,
    double AverageTurnaround,
    double AverageSlowdown,
    double CpuUtilization,
    double RamUtilization,
    double ThroughputPerHour,
    int CompletedJobs,
    int PreemptionCount);

/// <summary>
/// Преобразование доменных объектов Core в неизменяемые снимки для UI.
/// Интерфейс не привязывается к изменяемым сущностям ядра напрямую.
/// </summary>
public static class SnapshotMapper
{
    public static NodeSnapshot ToSnapshot(ClusterNode node) => new(
        node.Id,
        node.Name,
        node.TotalCapacity.CpuCores,
        node.AvailableResources.CpuCores,
        node.TotalCapacity.RamMb,
        node.AvailableResources.RamMb,
        node.RunningJobIds.Count,
        node.State.ToString());

    public static JobSnapshot ToSnapshot(Job job) => new(
        job.Id,
        job.Owner,
        job.Priority,
        job.State.ToString(),
        job.SubmitTime,
        job.StartTime,
        job.FinishTime,
        job.StartTime is null ? null : job.GetWaitTime(),
        job.IsFinished ? job.GetTurnaround() : null,
        job.RequiredResources.CpuCores,
        job.RequiredResources.RamMb,
        job.RequiredNodeCount,
        string.Join("+", job.AssignedNodeIds),
        job.PreemptionCount);

    public static MetricsSnapshot ToSnapshot(SimulationMetrics metrics, double currentTime) => new(
        currentTime,
        metrics.Makespan,
        metrics.AverageWaitTime,
        metrics.AverageTurnaround,
        metrics.AverageSlowdown,
        metrics.CpuUtilization,
        metrics.RamUtilization,
        metrics.ThroughputPerHour,
        metrics.TotalJobs,
        metrics.CompletedJobs,
        metrics.UnfinishedJobs,
        metrics.RejectedJobs,
        metrics.PreemptionCount,
        metrics.AverageQueueLength,
        metrics.MaxQueueLength);

    /// <summary>Собирает снимок состояния кластера и заданий после прогона.</summary>
    public static SimulationSnapshot ToSnapshot(
        Cluster cluster,
        IReadOnlyList<Job> jobs,
        SimulationMetrics metrics,
        double currentTime)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(metrics);

        return new SimulationSnapshot(
            currentTime,
            cluster.Nodes.Select(ToSnapshot).ToList(),
            jobs.Where(job => job.State == JobState.Pending).Select(ToSnapshot).ToList(),
            jobs.Where(job => job.State == JobState.Running).Select(ToSnapshot).ToList(),
            jobs.Where(job => job.IsFinished).Select(ToSnapshot).ToList(),
            ToSnapshot(metrics, currentTime));
    }
}