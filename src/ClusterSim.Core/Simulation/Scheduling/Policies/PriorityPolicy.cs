using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Scheduling.Policies;

/// <summary>
/// Приоритетное планирование: выше приоритет — раньше запуск.
/// Без вытеснения по умолчанию; вытеснение включается через PolicyOptions.AllowPreemption.
/// </summary>
public sealed class PriorityPolicy : ISchedulingPolicy
{
    public string Name => "Priority";

    public SchedulingPlan Plan(SchedulingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var plan = new SchedulingPlan();

        var queue = context.PendingJobs
            .Where(job => job.IsQueued)
            .OrderByDescending(job => job.Priority)
            .ThenBy(job => job.SubmitTime)
            .ThenBy(job => job.Id)
            .ToList();

        foreach (var job in queue)
        {
            var nodeIds = context.TryPlace(job);

            if (nodeIds is not null)
            {
                plan.AddStart(job, nodeIds);
                context.Commit(job, nodeIds);
                continue;
            }

            if (context.Policy.AllowPreemption)
            {
                TryPreemptFor(context, plan, job);
            }
        }

        return plan;
    }

    /// <summary>
    /// Освобождает ресурсы менее приоритетных выполняющихся заданий,
    /// пока ожидающее задание не сможет быть размещено.
    /// </summary>
    private static void TryPreemptFor(SchedulingContext context, SchedulingPlan plan, Job job)
    {
        while (true)
        {
            var victim = context.RunningJobs
                .Where(running => running.Priority < job.Priority)
                .Where(running => !plan.Preemptions.Contains(running))
                .Where(running =>
                    context.CurrentTime - (running.CurrentRunStartTime ?? context.CurrentTime)
                    >= context.Policy.MinRunTimeBeforePreemption)
                .OrderBy(running => running.Priority)
                .ThenBy(running => running.CurrentRunStartTime)
                .FirstOrDefault();

            if (victim is null)
            {
                return;
            }

            plan.AddPreemption(victim);
            context.Release(victim);

            var nodeIds = context.TryPlace(job);

            if (nodeIds is not null)
            {
                plan.AddStart(job, nodeIds);
                context.Commit(job, nodeIds);
                return;
            }
        }
    }
}