using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Scheduling.Policies;
using ClusterSim.Tests.Helpers;
using Xunit;

namespace ClusterSim.Tests.Simulation.Scheduling;

public class PriorityPolicyTests
{
    [Fact]
    public void HigherPriorityStartsFirst()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 8, ram: 8192);
        var low = TestScenarios.CreateJob(0, submit: 0, cpu: 1, priority: 1);
        var high = TestScenarios.CreateJob(1, submit: 1, cpu: 1, priority: 9);
        var context = TestScenarios.CreateContext(5, cluster, new[] { low, high }, Array.Empty<Job>());

        var plan = new PriorityPolicy().Plan(context);

        Assert.Equal(1, plan.Starts[0].Job.Id);
    }

    [Fact]
    public void WithoutPreemption_RunningJobIsNotStopped()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 4, ram: 4096);
        var running = TestScenarios.CreateJob(0, submit: 0, cpu: 4, priority: 1);
        cluster.Allocate(running, new[] { 0 });
        running.Start(0, new[] { 0 });

        var waiting = TestScenarios.CreateJob(1, submit: 5, cpu: 4, priority: 9);
        var context = TestScenarios.CreateContext(
            10, cluster, new[] { waiting }, new[] { running },
            new PolicyOptions { AllowPreemption = false });

        var plan = new PriorityPolicy().Plan(context);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void WithPreemption_LowPriorityRunningJobIsStopped()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 4, ram: 4096);
        var running = TestScenarios.CreateJob(0, submit: 0, cpu: 4, priority: 1);
        cluster.Allocate(running, new[] { 0 });
        running.Start(0, new[] { 0 });

        var waiting = TestScenarios.CreateJob(1, submit: 5, cpu: 4, priority: 9);
        var context = TestScenarios.CreateContext(
            10, cluster, new[] { waiting }, new[] { running },
            new PolicyOptions { AllowPreemption = true, MinRunTimeBeforePreemption = 5 });

        var plan = new PriorityPolicy().Plan(context);

        Assert.Single(plan.Preemptions);
        Assert.Equal(0, plan.Preemptions[0].Id);
        Assert.Single(plan.Starts);
        Assert.Equal(1, plan.Starts[0].Job.Id);
        Assert.Equal(new[] { 0 }, plan.Starts[0].NodeIds);
    }
}