using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Scheduling.Placement;

/// <summary>
/// Стратегия First Fit: узлы просматриваются по возрастанию идентификатора,
/// для параллельного задания берется первые K подходящих узлов (ганг-размещение).
/// </summary>
public sealed class FirstFitPlacement : IPlacementStrategy
{
    public string Name => "FirstFit";

    public IReadOnlyList<int>? TryPlace(Job job, IReadOnlyList<NodeAvailability> nodes)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(nodes);

        var candidates = nodes
            .Where(node => node.IsOnline && job.RequiredResources.FitsInto(node.Available))
            .OrderBy(node => node.NodeId)
            .ToList();

        if (candidates.Count < job.RequiredNodeCount)
        {
            return null;
        }

        return candidates
            .Take(job.RequiredNodeCount)
            .Select(node => node.NodeId)
            .ToList();
    }
}