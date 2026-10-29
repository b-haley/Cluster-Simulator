using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Engine;

/// <summary>
/// Событие симуляции с меткой виртуального времени и порядковым номером.
/// Номер гарантирует детерминированный порядок событий с одинаковым временем.
/// </summary>
public sealed record SimulationEvent(
    double Time,
    long Sequence,
    SimulationEventType Type,
    Job? Job = null,
    IReadOnlyList<int>? NodeIds = null);