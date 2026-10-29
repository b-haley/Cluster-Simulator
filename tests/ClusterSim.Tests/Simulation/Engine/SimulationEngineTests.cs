using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Scheduling.Policies;
using ClusterSim.Tests.Helpers;
using Xunit;

namespace ClusterSim.Tests.Simulation.Engine;

public class SimulationEngineTests
{
    [Fact]
    public void SingleJob_CompletesAtSubmitPlusDuration()
    {
        var job = TestScenarios.CreateJob(0, submit: 10, duration: 60);
        var engine = TestScenarios.CreateEngine(new[] { job }, new FifoPolicy());

        var metrics = engine.Run();

        Assert.Equal(JobState.Finished, job.State);
        Assert.Equal(10, job.StartTime);
        Assert.Equal(70, job.FinishTime);
        Assert.Equal(1, metrics.CompletedJobs);
        Assert.Equal(60, metrics.Makespan);
    }

    [Fact]
    public void ResourcesAreReleasedAfterCompletion()
    {
        var job = TestScenarios.CreateJob(0, submit: 0, cpu: 4, ram: 8192, duration: 30);
        var options = TestScenarios.CreateOptions(
            cluster: new ClusterOptions { NodeCount = 1, DefaultNode = new ClusterNodeOptions(4, 8192) });
        var engine = TestScenarios.CreateEngine(new[] { job }, new FifoPolicy(), options);

        engine.Run();

        Assert.Equal(engine.Cluster.TotalCapacity, engine.Cluster.TotalAvailable);
    }

    [Fact]
    public void JobLargerThanAnyNode_IsRejected()
    {
        var job = TestScenarios.CreateJob(0, submit: 0, cpu: 99, duration: 10);
        var engine = TestScenarios.CreateEngine(new[] { job }, new FifoPolicy());

        var metrics = engine.Run();

        Assert.Equal(JobState.Unschedulable, job.State);
        Assert.Equal(1, metrics.RejectedJobs);
        Assert.Equal(0, metrics.CompletedJobs);
    }

    [Fact]
    public void SecondJobWaitsForFirst_UnderFifo()
    {
        var options = TestScenarios.CreateOptions(
            cluster: new ClusterOptions { NodeCount = 1, DefaultNode = new ClusterNodeOptions(2, 1024) });
        var first = TestScenarios.CreateJob(0, submit: 0, cpu: 2, duration: 50);
        var second = TestScenarios.CreateJob(1, submit: 0, cpu: 2, duration: 10);
        var engine = TestScenarios.CreateEngine(new[] { first, second }, new FifoPolicy(), options);

        engine.Run();

        Assert.Equal(50, second.StartTime);
        Assert.Equal(60, second.FinishTime);
    }

    [Fact]
    public void MaxSimulationTime_LeavesLongJobUnfinished()
    {
        var job = TestScenarios.CreateJob(0, submit: 0, duration: 1000);
        var options = TestScenarios.CreateOptions() with { MaxSimulationTime = 100 };
        var engine = TestScenarios.CreateEngine(new[] { job }, new FifoPolicy(), options);

        var metrics = engine.Run();

        Assert.Equal(JobState.Running, job.State);
        Assert.Equal(0, metrics.CompletedJobs);
        Assert.Equal(1, metrics.UnfinishedJobs);
    }

    [Fact]
    public void Preemption_ResumesPreemptedJobWithRemainingTime()
    {
        var options = TestScenarios.CreateOptions(
            cluster: new ClusterOptions { NodeCount = 1, DefaultNode = new ClusterNodeOptions(4, 4096) },
            policy: new PolicyOptions
            {
                Kind = SchedulingPolicyKind.Priority,
                AllowPreemption = true,
                MinRunTimeBeforePreemption = 0
            });

        var low = TestScenarios.CreateJob(0, submit: 0, cpu: 4, duration: 100, priority: 1);
        var high = TestScenarios.CreateJob(1, submit: 10, cpu: 4, duration: 10, priority: 5);

        var engine = TestScenarios.CreateEngine(new[] { low, high }, new PriorityPolicy(), options);
        var metrics = engine.Run();

        Assert.Equal(1, low.PreemptionCount);
        Assert.Equal(JobState.Finished, high.State);
        Assert.Equal(JobState.Finished, low.State);
        Assert.Equal(10, high.StartTime);
        Assert.Equal(20, high.FinishTime);
        Assert.Equal(110, low.FinishTime);
        Assert.Equal(1, metrics.PreemptionCount);
    }
}