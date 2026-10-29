using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Features.Resources;

namespace ClusterSim.Core.Simulation.Scheduling;

/// <summary>
/// Снимок доступности узла для планирования:
/// позволяет политикам считать размещение, не изменяя реальный кластер.
/// </summary>
public sealed record NodeAvailability(int NodeId, ResourceVector Available, bool IsOnline);

/// <summary>
/// Стратегия размещения задания по узлам: отвечает на вопрос «на каких узлах можно запустить задание».
/// </summary>
public interface IPlacementStrategy
{
    /// <summary>Название стратегии.</summary>
    string Name { get; }

    /// <summary>Возвращает идентификаторы узлов или null, если разместить задание нельзя.</summary>
    IReadOnlyList<int>? TryPlace(Job job, IReadOnlyList<NodeAvailability> nodes);
}