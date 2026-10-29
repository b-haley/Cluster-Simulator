using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Scheduling.Policies;
using ClusterSim.Tests.Helpers;
using Xunit;

namespace ClusterSim.Tests.Simulation.Scheduling;

public class FifoPolicyTests
{
    [Fact]
    public void StartsJobsInSubmitOrder()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 8, ram: 8192);
        var late = TestScenarios.CreateJob(0, submit: 5, cpu: 1);
        var early = TestScenarios.CreateJob(1, submit: 1, cpu: 1);
        var context = TestScenarios.CreateContext(10, cluster, new[] { late, early }, Array.Empty<Job>());

        var plan = new FifoPolicy().Plan(context);

        Assert.Equal(2, plan.Starts.Count);
        Assert.Equal(1, plan.Starts[0].Job.Id);
        Assert.Equal(0, plan.Starts[1].Job.Id);
    }

    [Fact]
    public void StrictFifo_HeadBlocksQueue()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 12, ram: 8192);
        var occupant = TestScenarios.CreateJob(0, submit: 0, cpu: 8);
        cluster.Allocate(occupant, new[] { 0 });

        var head = TestScenarios.CreateJob(1, submit: 1, cpu: 12);
        var small = TestScenarios.CreateJob(2, submit: 2, cpu: 4);
        var context = TestScenarios.CreateContext(
            10, cluster, new[] { head, small }, Array.Empty<Job>(),
            new PolicyOptions { StrictFifo = true });

        var plan = new FifoPolicy().Plan(context);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void NonStrictFifo_SkipsBlockedHead()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 12, ram: 8192);
        var occupant = TestScenarios.CreateJob(0, submit: 0, cpu: 8);
        cluster.Allocate(occupant, new[] { 0 });

        var head = TestScenarios.CreateJob(1, submit: 1, cpu: 12);
        var small = TestScenarios.CreateJob(2, submit: 2, cpu: 4);
        var context = TestScenarios.CreateContext(
            10, cluster, new[] { head, small }, Array.Empty<Job>(),
            new PolicyOptions { StrictFifo = false });

        var plan = new FifoPolicy().Plan(context);

        Assert.Single(plan.Starts);
        Assert.Equal(2, plan.Starts[0].Job.Id);
    }
}