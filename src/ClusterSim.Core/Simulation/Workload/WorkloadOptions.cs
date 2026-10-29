namespace ClusterSim.Core.Simulation.Workload;

/// <summary>
/// Параметры генерации потока заданий.
/// </summary>
public sealed record WorkloadOptions
{
    /// <summary>Количество заданий в потоке.</summary>
    public int JobCount { get; init; } = 100;

    /// <summary>Зерно генератора: одинаковый Seed дает одинаковый поток.</summary>
    public int Seed { get; init; } = 42;

    /// <summary>Интенсивность пуассоновского потока, заданий в час.</summary>
    public double ArrivalRatePerHour { get; init; } = 60;

    /// <summary>Минимальная фактическая длительность задания, сек.</summary>
    public double MinDurationSeconds { get; init; } = 60;

    /// <summary>Максимальная фактическая длительность задания, сек.</summary>
    public double MaxDurationSeconds { get; init; } = 3600;

    /// <summary>Минимальное число запрашиваемых ядер.</summary>
    public int MinCpuCores { get; init; } = 1;

    /// <summary>Максимальное число запрашиваемых ядер.</summary>
    public int MaxCpuCores { get; init; } = 4;

    /// <summary>Минимальный запрашиваемый объем RAM, МБ.</summary>
    public int MinRamMb { get; init; } = 512;

    /// <summary>Максимальный запрашиваемый объем RAM, МБ.</summary>
    public int MaxRamMb { get; init; } = 4096;

    /// <summary>Максимальное значение приоритета (диапазон 0..MaxPriority).</summary>
    public int MaxPriority { get; init; } = 10;

    /// <summary>Список владельцев заданий.</summary>
    public IReadOnlyList<string> Owners { get; init; } = new[] { "alice", "bob", "carol" };

    /// <summary>Вероятность того, что задание параллельное (аналог MPI).</summary>
    public double ParallelJobProbability { get; init; }

    /// <summary>Максимальное число узлов для параллельного задания.</summary>
    public int MaxParallelNodes { get; init; } = 4;

    /// <summary>
    /// Относительная погрешность оценки длительности (0..1);
    /// 0 означает, что оценка равна фактической длительности.
    /// </summary>
    public double EstimationError { get; init; }
}