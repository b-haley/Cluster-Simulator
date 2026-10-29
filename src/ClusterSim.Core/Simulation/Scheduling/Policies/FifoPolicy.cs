using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Scheduling.Policies;

/// <summary>
/// FIFO: задания рассматриваются строго в порядке поступления.
/// В режиме StrictFifo голова очереди блокирует очередь, если не может быть размещена.
/// </summary>
public sealed class FifoPolicy : ISchedulingPolicy
{
    public string Name => "FIFO";

    public SchedulingPlan Plan(SchedulingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var plan = new SchedulingPlan();

        var queue = context.PendingJobs
            .Where(job => job.IsQueued)
            .OrderBy(job => job.SubmitTime)
            .ThenBy(job => job.Id)
            .ToList();

        foreach (var job in queue)
        {
            var nodeIds = context.TryPlace(job);

            if (nodeIds is null)
            {
                if (context.Policy.StrictFifo)
                {
                    break;
                }

                continue;
            }

            plan.AddStart(job, nodeIds);
            context.Commit(job, nodeIds);
        }

        return plan;
    }
}