using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.Job;

namespace ClusterSim.Core.Simulation.Metrics;

/// <summary>
/// Сборщик метрик: интегрирует загрузку кластера в целом и каждого узла
/// отдельно, агрегирует показатели заданий на конце прогона.
/// </summary>
public sealed class MetricsCollector
{
    private readonly double _timeSeriesStep;
    private readonly List<UtilizationPoint> _timeSeries = new();

    private readonly Dictionary<int, (string Name, int Cpu, int Ram)> _nodeInfo = new();
    private readonly Dictionary<int, double> _nodeUsedCpuSeconds = new();
    private readonly Dictionary<int, double> _nodeCapacityCpuSeconds = new();
    private readonly Dictionary<int, double> _nodeUsedRamSeconds = new();
    private readonly Dictionary<int, double> _nodeCapacityRamSeconds = new();

    private double _lastTime;
    private double _lastPointTime;
    private double _usedCpuSeconds;
    private double _usedRamSeconds;
    private double _capacityCpuSeconds;
    private double _capacityRamSeconds;
    private double _queueSeconds;
    private int _maxQueueLength;

    public MetricsCollector(double timeSeriesStep = 60)
    {
        _timeSeriesStep = timeSeriesStep > 0 ? timeSeriesStep : double.MaxValue;
    }

    /// <summary>Начинает учет с момента первой подачи задания.</summary>
    public void Begin(double startTime, Cluster cluster, int pendingCount, int runningCount)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        foreach (var node in cluster.Nodes)
        {
            _nodeInfo[node.Id] = (node.Name, node.TotalCapacity.CpuCores, node.TotalCapacity.RamMb);
            _nodeUsedCpuSeconds[node.Id] = 0;
            _nodeCapacityCpuSeconds[node.Id] = 0;
            _nodeUsedRamSeconds[node.Id] = 0;
            _nodeCapacityRamSeconds[node.Id] = 0;
        }

        _lastTime = startTime;
        _lastPointTime = startTime;
        _timeSeries.Add(CreatePoint(startTime, cluster, pendingCount, runningCount));
    }

    /// <summary>Продвигает виртуальное время: накопляет площади загрузки кластера и узлов.</summary>
    public void AdvanceTime(double newTime, Cluster cluster, int pendingCount, int runningCount)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        var delta = newTime - _lastTime;

        if (delta > 0)
        {
            var usedCpu = 0;
            var usedRam = 0;
            var totalCpu = 0;
            var totalRam = 0;

            foreach (var node in cluster.Nodes)
            {
                if (!node.IsOnline)
                {
                    continue;
                }

                var nodeUsedCpu = node.TotalCapacity.CpuCores - node.AvailableResources.CpuCores;
                var nodeUsedRam = node.TotalCapacity.RamMb - node.AvailableResources.RamMb;

                usedCpu += nodeUsedCpu;
                usedRam += nodeUsedRam;
                totalCpu += node.TotalCapacity.CpuCores;
                totalRam += node.TotalCapacity.RamMb;

                _nodeUsedCpuSeconds[node.Id] += nodeUsedCpu * delta;
                _nodeCapacityCpuSeconds[node.Id] += node.TotalCapacity.CpuCores * delta;
                _nodeUsedRamSeconds[node.Id] += nodeUsedRam * delta;
                _nodeCapacityRamSeconds[node.Id] += node.TotalCapacity.RamMb * delta;
            }

            _usedCpuSeconds += usedCpu * delta;
            _usedRamSeconds += usedRam * delta;
            _capacityCpuSeconds += totalCpu * delta;
            _capacityRamSeconds += totalRam * delta;
            _queueSeconds += pendingCount * delta;
            _maxQueueLength = Math.Max(_maxQueueLength, pendingCount);
            _lastTime = newTime;
        }

        if (newTime - _lastPointTime >= _timeSeriesStep)
        {
            _timeSeries.Add(CreatePoint(newTime, cluster, pendingCount, runningCount));
            _lastPointTime = newTime;
        }
    }

    /// <summary>Формирует итоговый набор метрик прогона, включая загрузку каждого узла.</summary>
    public SimulationMetrics Build(
        string policyName,
        double endTime,
        Cluster cluster,
        int pendingCount,
        int runningCount,
        IReadOnlyList<Job> allJobs)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        ArgumentNullException.ThrowIfNull(allJobs);

        if (endTime > _lastTime)
        {
            AdvanceTime(endTime, cluster, pendingCount, runningCount);
        }

        if (_timeSeries.Count == 0 || Math.Abs(_timeSeries[^1].Time - endTime) > 1e-9)
        {
            _timeSeries.Add(CreatePoint(endTime, cluster, pendingCount, runningCount));
        }

        var finished = allJobs.Where(job => job.IsFinished).ToList();
        var completed = finished.Count;
        var rejected = allJobs.Count(job => job.State == JobState.Unschedulable);
        var firstSubmit = allJobs.Count > 0 ? allJobs.Min(job => job.SubmitTime) : 0.0;
        var span = Math.Max(endTime - firstSubmit, 1e-9);

        var makespan = completed > 0
            ? finished.Max(job => job.FinishTime!.Value) - firstSubmit
            : 0.0;

        var nodeUtilizations = _nodeInfo.Keys
            .OrderBy(id => id)
            .Select(id => new NodeUtilization(
                id,
                _nodeInfo[id].Name,
                _nodeInfo[id].Cpu,
                _nodeCapacityCpuSeconds[id] > 0 ? _nodeUsedCpuSeconds[id] / _nodeCapacityCpuSeconds[id] : 0.0,
                _nodeInfo[id].Ram,
                _nodeCapacityRamSeconds[id] > 0 ? _nodeUsedRamSeconds[id] / _nodeCapacityRamSeconds[id] : 0.0))
            .ToList();

        return new SimulationMetrics(
            policyName,
            makespan,
            completed > 0 ? finished.Average(job => job.GetWaitTime()) : 0.0,
            completed > 0 ? finished.Average(job => job.GetTurnaround()) : 0.0,
            completed > 0 ? finished.Average(job => job.GetSlowdown()) : 0.0,
            _capacityCpuSeconds > 0 ? _usedCpuSeconds / _capacityCpuSeconds : 0.0,
            _capacityRamSeconds > 0 ? _usedRamSeconds / _capacityRamSeconds : 0.0,
            completed / (span / 3600.0),
            allJobs.Count,
            completed,
            allJobs.Count - completed - rejected,
            rejected,
            allJobs.Sum(job => job.PreemptionCount),
            _queueSeconds / span,
            _maxQueueLength,
            allJobs.Select(ToJobMetrics).ToList(),
            _timeSeries.ToList(),
            nodeUtilizations);
    }

    private static UtilizationPoint CreatePoint(double time, Cluster cluster, int pendingCount, int runningCount)
    {
        var usedCpu = 0;
        var usedRam = 0;
        var totalCpu = 0;
        var totalRam = 0;

        foreach (var node in cluster.Nodes)
        {
            if (!node.IsOnline)
            {
                continue;
            }

            totalCpu += node.TotalCapacity.CpuCores;
            totalRam += node.TotalCapacity.RamMb;
            usedCpu += node.TotalCapacity.CpuCores - node.AvailableResources.CpuCores;
            usedRam += node.TotalCapacity.RamMb - node.AvailableResources.RamMb;
        }

        return new UtilizationPoint(time, runningCount, pendingCount, usedCpu, totalCpu, usedRam, totalRam);
    }

    private static JobMetrics ToJobMetrics(Job job) => new(
        job.Id,
        job.Owner,
        job.Priority,
        job.SubmitTime,
        job.StartTime,
        job.FinishTime,
        job.StartTime is null ? null : job.GetWaitTime(),
        job.IsFinished ? job.GetTurnaround() : null,
        job.IsFinished ? job.GetSlowdown() : null,
        job.RequiredResources.CpuCores,
        job.RequiredResources.RamMb,
        job.RequiredNodeCount,
        job.State,
        job.PreemptionCount);
}