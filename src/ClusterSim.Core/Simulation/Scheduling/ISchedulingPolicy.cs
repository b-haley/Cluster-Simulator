namespace ClusterSim.Core.Simulation.Scheduling;

/// <summary>
/// Политика планирования: решает, какие задания запускать и какие прерывать.
/// Политика не изменяет кластер напрямую, а возвращает план для движка.
/// </summary>
public interface ISchedulingPolicy
{
    /// <summary>Название политики.</summary>
    string Name { get; }

    /// <summary>Формирует план планирования для текущего состояния симуляции.</summary>
    SchedulingPlan Plan(SchedulingContext context);
}