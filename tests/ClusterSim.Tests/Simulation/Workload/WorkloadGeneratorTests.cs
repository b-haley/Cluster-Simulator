using ClusterSim.Core.Simulation.Workload;
using Xunit;

namespace ClusterSim.Tests.Simulation.Workload;

public class WorkloadGeneratorTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalFlow()
    {
        var options = new WorkloadOptions { Seed = 7, JobCount = 50 };
        var first = new WorkloadGenerator(options).GetJobs();
        var second = new WorkloadGenerator(options).GetJobs();

        Assert.Equal(first.Count, second.Count);

        for (var index = 0; index < first.Count; index++)
        {
            Assert.Equal(first[index].SubmitTime, second[index].SubmitTime);
            Assert.Equal(first[index].TotalDuration, second[index].TotalDuration);
            Assert.Equal(first[index].EstimatedDuration, second[index].EstimatedDuration);
            Assert.Equal(first[index].RequiredResources, second[index].RequiredResources);
            Assert.Equal(first[index].Owner, second[index].Owner);
            Assert.Equal(first[index].Priority, second[index].Priority);
        }
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentFlow()
    {
        var first = new WorkloadGenerator(new WorkloadOptions { Seed = 1, JobCount = 50 }).GetJobs();
        var second = new WorkloadGenerator(new WorkloadOptions { Seed = 2, JobCount = 50 }).GetJobs();

        var hasDifference = first
            .Zip(second, (a, b) => a.SubmitTime != b.SubmitTime || a.TotalDuration != b.TotalDuration)
            .Any(differs => differs);

        Assert.True(hasDifference);
    }

    [Fact]
    public void JobsRespectConfiguredBounds()
    {
        var options = new WorkloadOptions
        {
            Seed = 11,
            JobCount = 200,
            MinDurationSeconds = 30,
            MaxDurationSeconds = 300,
            MinCpuCores = 2,
            MaxCpuCores = 6,
            MinRamMb = 1024,
            MaxRamMb = 2048,
            MaxPriority = 3
        };

        var jobs = new WorkloadGenerator(options).GetJobs();

        Assert.All(jobs, job =>
        {
            Assert.InRange(job.TotalDuration, 30, 300);
            Assert.InRange(job.RequiredResources.CpuCores, 2, 6);
            Assert.InRange(job.RequiredResources.RamMb, 1024, 2048);
            Assert.InRange(job.Priority, 0, 3);
            Assert.True(job.SubmitTime >= 0);
        });

        var submits = jobs.Select(job => job.SubmitTime).ToList();

        for (var index = 1; index < submits.Count; index++)
        {
            Assert.True(submits[index] >= submits[index - 1]);
        }
    }

    [Fact]
    public void ZeroParallelProbability_AllJobsSingleNode()
    {
        var jobs = new WorkloadGenerator(new WorkloadOptions
        {
            Seed = 5,
            JobCount = 50,
            ParallelJobProbability = 0
        }).GetJobs();

        Assert.All(jobs, job => Assert.Equal(1, job.RequiredNodeCount));
    }

    [Fact]
    public void ParallelProbabilityOne_AllJobsParallel()
    {
        var jobs = new WorkloadGenerator(new WorkloadOptions
        {
            Seed = 5,
            JobCount = 50,
            ParallelJobProbability = 1,
            MaxParallelNodes = 3
        }).GetJobs();

        Assert.All(jobs, job => Assert.InRange(job.RequiredNodeCount, 2, 3));
    }

    [Fact]
    public void ZeroEstimationError_EstimateEqualsActual()
    {
        var jobs = new WorkloadGenerator(new WorkloadOptions
        {
            Seed = 9,
            JobCount = 30,
            EstimationError = 0
        }).GetJobs();

        Assert.All(jobs, job => Assert.Equal(job.TotalDuration, job.EstimatedDuration));
    }
}