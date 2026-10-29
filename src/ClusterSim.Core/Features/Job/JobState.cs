namespace ClusterSim.Core.Features.Job;

/// <summary>Состояния задания в симуляции.</summary>
public enum JobState
{
    /// <summary>Задание поступило и ждет в очереди.</summary>
    Pending,

    /// <summary>Задание прервано с контрольной точкой и ждет возобновления.</summary>
    Suspended,

    /// <summary>Задание выполняется на узлах кластера.</summary>
    Running,

    /// <summary>Задание успешно завершено.</summary>
    Finished,

    /// <summary>Задание никогда не может быть размещено на данном кластере.</summary>
    Unschedulable
}