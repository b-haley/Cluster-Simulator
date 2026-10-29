using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Simulation.Scheduling.Policies;
using ClusterSim.Tests.Helpers;
using Xunit;

namespace ClusterSim.Tests.Simulation.Scheduling;

public class EasyBackfillPolicyTests
{
    [Fact]
    public void HeadStartsWhenResourcesFree()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 4, ram: 4096);
        var head = TestScenarios.CreateJob(0, submit: 0, cpu: 4, duration: 30);
        var context = TestScenarios.CreateContext(0, cluster, new[] { head }, Array.Empty<Job>());

        var plan = new EasyBackfillPolicy().Plan(context);

        Assert.Single(plan.Starts);
        Assert.Equal(0, plan.Starts[0].Job.Id);
    }

    [Fact]
    public void ShortJobBackfillsIntoWindowWithoutDelayingHead()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 8, ram: 4096);

        var occupant = TestScenarios.CreateJob(0, submit: 0, cpu: 4, duration: 100);
        cluster.Allocate(occupant, new[] { 0 });
        occupant.Start(0, new[] { 0 });

        var head = TestScenarios.CreateJob(1, submit: 1, cpu: 8, duration: 30, estimate: 30);
        var shortJob = TestScenarios.CreateJob(2, submit: 2, cpu: 4, duration: 20, estimate: 20);

        var context = TestScenarios.CreateContext(
            10, cluster, new[] { head, shortJob }, new[] { occupant });

        var plan = new EasyBackfillPolicy().Plan(context);

        Assert.Single(plan.Starts);
        Assert.Equal(2, plan.Starts[0].Job.Id);
    }

    [Fact]
    public void JobDelayingHead_IsNotBackfilled()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 8, ram: 4096);

        var occupant = TestScenarios.CreateJob(0, submit: 0, cpu: 4, duration: 100);
        cluster.Allocate(occupant, new[] { 0 });
        occupant.Start(0, new[] { 0 });

        var head = TestScenarios.CreateJob(1, submit: 1, cpu: 8, duration: 30, estimate: 30);
        var longJob = TestScenarios.CreateJob(2, submit: 2, cpu: 4, duration: 95, estimate: 95);

        var context = TestScenarios.CreateContext(
            10, cluster, new[] { head, longJob }, new[] { occupant });

        var plan = new EasyBackfillPolicy().Plan(context);

        Assert.True(plan.IsEmpty);
    }
}