namespace ClusterSim.Core.Simulation.Engine;

/// <summary>Типы событий дискретно-событийной симуляции.</summary>
public enum SimulationEventType
{
    /// <summary>Задание поступило в очередь.</summary>
    JobSubmitted,

    /// <summary>Задание начало выполняться.</summary>
    JobStarted,

    /// <summary>Задание завершилось и освободило ресурсы.</summary>
    JobFinished,

    /// <summary>Задание прервано с контрольной точкой.</summary>
    JobPreempted,

    /// <summary>Симуляция завершена.</summary>
    SimulationFinished
}