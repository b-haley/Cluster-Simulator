using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Features.Resources;

namespace ClusterSim.Core.Simulation.Workload;

/// <summary>
/// Детерминированный генератор потока заданий:
/// при одинаковом Seed поток полностью воспроизводим.
/// </summary>
public sealed class WorkloadGenerator : IJobSource
{
    private readonly WorkloadOptions _options;

    public WorkloadGenerator(WorkloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.JobCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Количество заданий не может быть отрицательным.");
        }

        if (options.Owners.Count == 0)
        {
            throw new ArgumentException("Список владельцев не может быть пустым.", nameof(options));
        }

        if (options.MinDurationSeconds > options.MaxDurationSeconds)
        {
            throw new ArgumentException("Минимальная длительность больше максимальной.", nameof(options));
        }

        if (options.MinCpuCores > options.MaxCpuCores)
        {
            throw new ArgumentException("Минимум CPU больше максимума.", nameof(options));
        }

        if (options.MinRamMb > options.MaxRamMb)
        {
            throw new ArgumentException("Минимум RAM больше максимума.", nameof(options));
        }

        _options = options;
    }

    public IReadOnlyList<Job> GetJobs()
    {
        var random = new Random(_options.Seed);
        var jobs = new List<Job>(_options.JobCount);
        var submitTime = 0.0;

        for (var id = 0; id < _options.JobCount; id++)
        {
            submitTime += NextInterArrival(random);

            var totalDuration = NextDouble(random, _options.MinDurationSeconds, _options.MaxDurationSeconds);

            jobs.Add(new Job(
                id,
                _options.Owners[random.Next(_options.Owners.Count)],
                submitTime,
                random.Next(0, _options.MaxPriority + 1),
                new ResourceVector(
                    random.Next(_options.MinCpuCores, _options.MaxCpuCores + 1),
                    random.Next(_options.MinRamMb, _options.MaxRamMb + 1)),
                NextNodeCount(random),
                Estimate(totalDuration, random),
                totalDuration));
        }

        return jobs;
    }

    /// <summary>Экспоненциальный интервал между поступлениями (пуассоновский поток).</summary>
    private double NextInterArrival(Random random)
    {
        if (_options.ArrivalRatePerHour <= 0)
        {
            return 0;
        }

        var meanSeconds = 3600.0 / _options.ArrivalRatePerHour;
        return -Math.Log(1.0 - random.NextDouble()) * meanSeconds;
    }

    /// <summary>Число узлов задания: 1 либо случайное для параллельных.</summary>
    private int NextNodeCount(Random random)
    {
        if (_options.ParallelJobProbability <= 0 || _options.MaxParallelNodes <= 1)
        {
            return 1;
        }

        return random.NextDouble() < _options.ParallelJobProbability
            ? random.Next(2, _options.MaxParallelNodes + 1)
            : 1;
    }

    /// <summary>Оценка длительности с возможной погрешностью.</summary>
    private double Estimate(double totalDuration, Random random)
    {
        if (_options.EstimationError <= 0)
        {
            return totalDuration;
        }

        var factor = 1.0 + (random.NextDouble() * 2 - 1) * _options.EstimationError;
        return Math.Max(1.0, totalDuration * factor);
    }

    private static double NextDouble(Random random, double min, double max) =>
        min + (max - min) * random.NextDouble();
}