using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Scheduling;

/// <summary>
/// Контекст планирования: снимок состояния симуляции плюс «черновик» доступности узлов.
/// Черновик позволяет политике разместить несколько заданий за один проход без перерасхода ресурсов.
/// </summary>
public sealed class SchedulingContext
{
    private readonly List<NodeAvailability> _nodesView;

    public SchedulingContext(
        double currentTime,
        Cluster cluster,
        IReadOnlyList<Job> pendingJobs,
        IReadOnlyList<Job> runningJobs,
        IPlacementStrategy placement,
        PolicyOptions policy,
        IReadOnlyDictionary<string, double> userUsage)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        ArgumentNullException.ThrowIfNull(pendingJobs);
        ArgumentNullException.ThrowIfNull(runningJobs);
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(userUsage);

        CurrentTime = currentTime;
        Cluster = cluster;
        PendingJobs = pendingJobs;
        RunningJobs = runningJobs;
        Placement = placement;
        Policy = policy;
        UserUsage = userUsage;

        _nodesView = cluster.Nodes
            .Select(node => new NodeAvailability(node.Id, node.AvailableResources, node.IsOnline))
            .ToList();
    }

    /// <summary>Текущее виртуальное время симуляции.</summary>
    public double CurrentTime { get; }

    /// <summary>Кластер (только для чтения состояния).</summary>
    public Cluster Cluster { get; }

    /// <summary>Задания в очереди ожидания.</summary>
    public IReadOnlyList<Job> PendingJobs { get; }

    /// <summary>Выполняющиеся задания.</summary>
    public IReadOnlyList<Job> RunningJobs { get; }

    /// <summary>Стратегия размещения заданий по узлам.</summary>
    public IPlacementStrategy Placement { get; }

    /// <summary>Параметры политики планирования.</summary>
    public PolicyOptions Policy { get; }

    /// <summary>Накопленное потребление ресурсов владельцами (ядро-секунды).</summary>
    public IReadOnlyDictionary<string, double> UserUsage { get; }

    /// <summary>Текущий черновик доступности узлов.</summary>
    public IReadOnlyList<NodeAvailability> NodesView => _nodesView;

    /// <summary>Возвращает вес владельца для Fair-Share; по умолчанию 1.</summary>
    public double GetWeight(string owner) =>
        Policy.UserWeights.TryGetValue(owner, out var weight) && weight > 0 ? weight : 1.0;

    /// <summary>Пробует разместить задание с учетом уже запланированных в этом проходе стартов.</summary>
    public IReadOnlyList<int>? TryPlace(Job job) => Placement.TryPlace(job, _nodesView);

    /// <summary>Фиксирует размещение задания в черновике доступности.</summary>
    public void Commit(Job job, IReadOnlyList<int> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(nodeIds);

        foreach (var nodeId in nodeIds)
        {
            var index = _nodesView.FindIndex(view => view.NodeId == nodeId);

            if (index < 0)
            {
                throw new InvalidOperationException($"Узел {nodeId} не найден в черновике планирования.");
            }

            _nodesView[index] = _nodesView[index] with
            {
                Available = _nodesView[index].Available.Subtract(job.RequiredResources)
            };
        }
    }

    /// <summary>Возвращает ресурсы прерываемого задания в черновик доступности.</summary>
    public void Release(Job runningJob)
    {
        ArgumentNullException.ThrowIfNull(runningJob);

        foreach (var nodeId in runningJob.AssignedNodeIds)
        {
            var index = _nodesView.FindIndex(view => view.NodeId == nodeId);

            if (index < 0)
            {
                continue;
            }

            _nodesView[index] = _nodesView[index] with
            {
                Available = _nodesView[index].Available.Add(runningJob.RequiredResources)
            };
        }
    }
}