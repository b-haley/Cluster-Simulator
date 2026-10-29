using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Scheduling.Policies;

/// <summary>
/// EASY Backfill: голова очереди получает резервирование ресурсов,
/// а короткие задания могут запускаться в «окна», если не задерживают голову.
/// </summary>
public sealed class EasyBackfillPolicy : ISchedulingPolicy
{
    public string Name => "EASY Backfill";

    public SchedulingPlan Plan(SchedulingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var plan = new SchedulingPlan();

        var queue = context.PendingJobs
            .Where(job => job.IsQueued)
            .OrderBy(job => job.SubmitTime)
            .ThenBy(job => job.Id)
            .ToList();

        var index = 0;

        while (index < queue.Count)
        {
            var head = queue[index];
            var headNodes = context.TryPlace(head);

            if (headNodes is not null)
            {
                plan.AddStart(head, headNodes);
                context.Commit(head, headNodes);
                index++;
                continue;
            }

            // Голова не помещается: резервируем под нее ресурсы и заполняем окна.
            var reservationTime = ComputeReservationTime(context, head);

            for (var candidateIndex = index + 1; candidateIndex < queue.Count; candidateIndex++)
            {
                var candidate = queue[candidateIndex];
                var candidateNodes = context.TryPlace(candidate);

                if (candidateNodes is null)
                {
                    continue;
                }

                var candidateFinish = context.CurrentTime + candidate.EstimatedDuration;

                if (candidateFinish <= reservationTime)
                {
                    plan.AddStart(candidate, candidateNodes);
                    context.Commit(candidate, candidateNodes);
                }
            }

            break;
        }

        return plan;
    }

    /// <summary>
    /// Ближайшее время, когда голова очереди могла бы стартовать,
    /// если не запускать ничего нового: учитываются только завершения текущих заданий.
    /// </summary>
    private static double ComputeReservationTime(SchedulingContext context, Job head)
    {
        var candidateTimes = new SortedSet<double> { context.CurrentTime };

        foreach (var running in context.RunningJobs)
        {
            var finish = (running.CurrentRunStartTime ?? context.CurrentTime) + running.RemainingDuration;
            candidateTimes.Add(finish);
        }

        foreach (var time in candidateTimes)
        {
            var predicted = PredictNodesAt(context, time);

            if (context.Placement.TryPlace(head, predicted) is not null)
            {
                return time;
            }
        }

        return double.MaxValue;
    }

    /// <summary>
    /// Прогноз доступности узлов в момент t: задания, еще работающие в момент t,
    /// считаются занимающими свои ресурсы; освободившиеся к t — нет.
    /// </summary>
    private static List<NodeAvailability> PredictNodesAt(SchedulingContext context, double time)
    {
        var predicted = context.NodesView.ToList();

        foreach (var running in context.RunningJobs)
        {
            var finish = (running.CurrentRunStartTime ?? time) + running.RemainingDuration;

            if (finish > time)
            {
                continue;
            }

            foreach (var nodeId in running.AssignedNodeIds)
            {
                var index = predicted.FindIndex(node => node.NodeId == nodeId);

                if (index >= 0)
                {
                    predicted[index] = predicted[index] with
                    {
                        Available = predicted[index].Available.Add(running.RequiredResources)
                    };
                }
            }
        }

        return predicted;
    }
}