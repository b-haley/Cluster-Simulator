namespace ClusterSim.Core.Features.ClusterNode;

/// <summary>Состояние узла кластера.</summary>
public enum ClusterNodeState
{
    /// <summary>Узел полностью свободен.</summary>
    Idle,

    /// <summary>Узел занят частично.</summary>
    PartiallyBusy,

    /// <summary>Свободных ресурсов на узле не осталось.</summary>
    FullyBusy,

    /// <summary>Узел выключен или недоступен.</summary>
    Offline
}