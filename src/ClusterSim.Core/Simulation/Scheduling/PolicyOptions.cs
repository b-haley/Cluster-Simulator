namespace ClusterSim.Core.Simulation.Scheduling;

/// <summary>Доступные политики планирования.</summary>
public enum SchedulingPolicyKind
{
    Fifo,
    Priority,
    EasyBackfill,
    FairShare
}

/// <summary>
/// Настройки поведения политик планирования.
/// </summary>
public sealed record PolicyOptions
{
    /// <summary>Выбранная политика.</summary>
    public SchedulingPolicyKind Kind { get; init; } = SchedulingPolicyKind.Fifo;

    /// <summary>Строгий FIFO: голова очереди блокирует очередь, если не помещается.</summary>
    public bool StrictFifo { get; init; } = true;

    /// <summary>Разрешено ли вытеснение выполняющихся заданий.</summary>
    public bool AllowPreemption { get; init; }

    /// <summary>Минимальное время выполнения задания до возможного вытеснения.</summary>
    public double MinRunTimeBeforePreemption { get; init; } = 60;

    /// <summary>Накладные расходы контрольной точки при прерывании (секунды).</summary>
    public double CheckpointOverheadSeconds { get; init; }

    /// <summary>Веса владельцев для Fair-Share; отсутствующий владелец имеет вес 1.</summary>
    public IReadOnlyDictionary<string, double> UserWeights { get; init; } = new Dictionary<string, double>();
}