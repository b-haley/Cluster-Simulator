using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Simulation.Workload;

namespace ClusterSim.Tests.Helpers;

/// <summary>
/// Статический источник заданий: движок получает готовый список без случайности.
/// Позволяет детерминированно проверять сценарии планирования.
/// </summary>
public sealed class StaticJobSource : IJobSource
{
    private readonly IReadOnlyList<Job> _jobs;

    public StaticJobSource(params Job[] jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        _jobs = jobs.ToList();
    }

    public StaticJobSource(IEnumerable<Job> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        _jobs = jobs.ToList();
    }

    public IReadOnlyList<Job> GetJobs() => _jobs;
}