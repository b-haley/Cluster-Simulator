using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Scheduling.Policies;
using ClusterSim.Tests.Helpers;
using Xunit;

namespace ClusterSim.Tests.Simulation.Scheduling;

public class FairSharePolicyTests
{
    [Fact]
    public void UnderservedOwnerGoesFirst()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 8, ram: 4096);
        var alice = TestScenarios.CreateJob(0, submit: 0, cpu: 1, owner: "alice");
        var bob = TestScenarios.CreateJob(1, submit: 1, cpu: 1, owner: "bob");
        var usage = new Dictionary<string, double> { ["alice"] = 1000, ["bob"] = 0 };

        var context = TestScenarios.CreateContext(
            5, cluster, new[] { alice, bob }, Array.Empty<Job>(), usage: usage);

        var plan = new FairSharePolicy().Plan(context);

        Assert.Equal("bob", plan.Starts[0].Job.Owner);
    }

    [Fact]
    public void WeightsNormalizeUsage()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 8, ram: 4096);
        var alice = TestScenarios.CreateJob(0, submit: 0, cpu: 1, owner: "alice");
        var bob = TestScenarios.CreateJob(1, submit: 1, cpu: 1, owner: "bob");
        var usage = new Dictionary<string, double> { ["alice"] = 1000, ["bob"] = 300 };
        var policy = new PolicyOptions
        {
            UserWeights = new Dictionary<string, double> { ["alice"] = 5 }
        };

        var context = TestScenarios.CreateContext(
            5, cluster, new[] { alice, bob }, Array.Empty<Job>(), policy, usage);

        var plan = new FairSharePolicy().Plan(context);

        Assert.Equal("alice", plan.Starts[0].Job.Owner);
    }

    [Fact]
    public void WithinOwner_FifoOrder()
    {
        var cluster = TestScenarios.CreateCluster(nodeCount: 1, cpu: 8, ram: 4096);
        var bobLate = TestScenarios.CreateJob(0, submit: 3, cpu: 1, owner: "bob");
        var bobEarly = TestScenarios.CreateJob(1, submit: 1, cpu: 1, owner: "bob");
        var alice = TestScenarios.CreateJob(2, submit: 0, cpu: 1, owner: "alice");
        var usage = new Dictionary<string, double> { ["alice"] = 5000 };

        var context = TestScenarios.CreateContext(
            5, cluster, new[] { bobLate, bobEarly, alice }, Array.Empty<Job>(), usage: usage);

        var plan = new FairSharePolicy().Plan(context);

        Assert.Equal(3, plan.Starts.Count);
        Assert.Equal(1, plan.Starts[0].Job.Id);
        Assert.Equal(0, plan.Starts[1].Job.Id);
        Assert.Equal(2, plan.Starts[2].Job.Id);
    }
}