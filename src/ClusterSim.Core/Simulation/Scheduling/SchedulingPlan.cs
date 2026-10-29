using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Scheduling;

/// <summary>Решение запустить задание на конкретном наборе узлов.</summary>
public sealed record JobStartAssignment(Job Job, IReadOnlyList<int> NodeIds);

/// <summary>
/// План планирования: что запустить и что прервать.
/// Применяется движком симуляции, а не самой политикой.
/// </summary>
public sealed class SchedulingPlan
{
    private readonly List<JobStartAssignment> _starts = new();
    private readonly List<Job> _preemptions = new();

    /// <summary>Пустой план.</summary>
    public static SchedulingPlan Empty => new();

    /// <summary>Задания к запуску с назначенными узлами.</summary>
    public IReadOnlyList<JobStartAssignment> Starts => _starts;

    /// <summary>Задания к прерыванию.</summary>
    public IReadOnlyList<Job> Preemptions => _preemptions;

    /// <summary>Признак пустого плана.</summary>
    public bool IsEmpty => _starts.Count == 0 && _preemptions.Count == 0;

    /// <summary>Добавляет задание к запуску.</summary>
    public void AddStart(Job job, IReadOnlyList<int> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(nodeIds);
        _starts.Add(new JobStartAssignment(job, nodeIds));
    }

    /// <summary>Добавляет задание к прерыванию.</summary>
    public void AddPreemption(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        _preemptions.Add(job);
    }
}