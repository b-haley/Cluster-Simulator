using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Simulation;
using ClusterSim.Core.Simulation.Scheduling.Policies;
using ClusterSim.Tests.Helpers;
using Xunit;

namespace ClusterSim.Tests.Simulation.Metrics;

/// <summary>
/// Метрики проверяются через прогон движка на сценариях,
/// где все значения можно посчитать вручную.
/// </summary>
public class MetricsCollectorTests
{
    private static SimulationOptions SingleNodeOptions() => TestScenarios.CreateOptions(
        cluster: new ClusterOptions { NodeCount = 1, DefaultNode = new ClusterNodeOptions(4, 4096) });

    [Fact]
    public void FullyLoadedNode_GivesUtilizationOne()
    {
        var job = TestScenarios.CreateJob(0, submit: 0, cpu: 4, ram: 4096, duration: 60);
        var metrics = TestScenarios.Run(new[] { job }, new FifoPolicy(), SingleNodeOptions());

        Assert.Equal(1, metrics.CompletedJobs);
        Assert.Equal(1.0, metrics.CpuUtilization, 9);
        Assert.Equal(1.0, metrics.RamUtilization, 9);
        Assert.Equal(60.0, metrics.Makespan, 9);
        Assert.Equal(0.0, metrics.AverageWaitTime, 9);
        Assert.Equal(60.0, metrics.AverageTurnaround, 9);
        Assert.Equal(1.0, metrics.AverageSlowdown, 9);
        Assert.Equal(60.0, metrics.ThroughputPerHour, 9);
    }

    [Fact]
    public void QueuedJob_ProducesWaitTurnaroundAndQueueLength()
    {
        var first = TestScenarios.CreateJob(0, submit: 0, cpu: 4, duration: 60);
        var second = TestScenarios.CreateJob(1, submit: 0, cpu: 4, duration: 10);
        var metrics = TestScenarios.Run(new[] { first, second }, new FifoPolicy(), SingleNodeOptions());

        // second: wait = 60, turnaround = 70, slowdown = 7; first: 0 / 60 / 1.
        Assert.Equal(30.0, metrics.AverageWaitTime, 9);
        Assert.Equal(65.0, metrics.AverageTurnaround, 9);
        Assert.Equal(4.0, metrics.AverageSlowdown, 9);
        Assert.Equal(70.0, metrics.Makespan, 9);
        Assert.Equal(1, metrics.MaxQueueLength);
        Assert.Equal(60.0 / 70.0, metrics.AverageQueueLength, 9);
    }

    [Fact]
    public void TimeSeries_CoversWholeRun()
    {
        var first = TestScenarios.CreateJob(0, submit: 0, cpu: 4, duration: 60);
        var second = TestScenarios.CreateJob(1, submit: 0, cpu: 4, duration: 10);
        var metrics = TestScenarios.Run(new[] { first, second }, new FifoPolicy(), SingleNodeOptions());

        Assert.True(metrics.TimeSeries.Count >= 2);
        Assert.Equal(0.0, metrics.TimeSeries[0].Time, 9);
        Assert.Equal(70.0, metrics.TimeSeries[^1].Time, 9);
        Assert.Equal(4, metrics.TimeSeries[0].TotalCpuCores);
    }

    [Fact]
    public void RejectedJob_IsCountedSeparatelyFromUnfinished()
    {
        var tooBig = TestScenarios.CreateJob(0, submit: 0, cpu: 99, duration: 10);
        var normal = TestScenarios.CreateJob(1, submit: 0, cpu: 1, duration: 10);
        var metrics = TestScenarios.Run(new[] { tooBig, normal }, new FifoPolicy(), SingleNodeOptions());

        Assert.Equal(1, metrics.RejectedJobs);
        Assert.Equal(1, metrics.CompletedJobs);
        Assert.Equal(0, metrics.UnfinishedJobs);
        Assert.Equal(2, metrics.TotalJobs);
    }
}