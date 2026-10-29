using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Scheduling.Policies;

/// <summary>
/// Fair-Share: ресурсы отдаются владельцу с наименьшей нормированной долей потребления.
/// Внутри одного владельца задания выбираются по FIFO.
/// </summary>
public sealed class FairSharePolicy : ISchedulingPolicy
{
    public string Name => "Fair-Share";

    public SchedulingPlan Plan(SchedulingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var plan = new SchedulingPlan();
        var queued = context.PendingJobs.Where(job => job.IsQueued).ToList();

        var owners = queued
            .Select(job => job.Owner)
            .Distinct()
            .OrderBy(owner => GetShare(context, owner))
            .ThenBy(owner => owner, StringComparer.Ordinal)
            .ToList();

        foreach (var owner in owners)
        {
            var ownerJobs = queued
                .Where(job => job.Owner == owner)
                .OrderBy(job => job.SubmitTime)
                .ThenBy(job => job.Id)
                .ToList();

            foreach (var job in ownerJobs)
            {
                var nodeIds = context.TryPlace(job);

                if (nodeIds is null)
                {
                    continue;
                }

                plan.AddStart(job, nodeIds);
                context.Commit(job, nodeIds);
            }
        }

        return plan;
    }

    /// <summary>Нормированная доля владельца: потребление, деленное на вес.</summary>
    private static double GetShare(SchedulingContext context, string owner)
    {
        var usage = context.UserUsage.TryGetValue(owner, out var value) ? value : 0.0;
        return usage / context.GetWeight(owner);
    }
}