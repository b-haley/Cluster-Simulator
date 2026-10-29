using System.Globalization;
using System.Text;
using System.Text.Json;
using ClusterSim.Core.Simulation.Metrics;

namespace ClusterSim.Core.Simulation.Export;

/// <summary>
/// Экспорт результатов прогонов в CSV и JSON.
/// Все числа записываются в инвариантной культуре, чтобы файлы были переносимы.
/// </summary>
public sealed class ReportExporter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>Сводные метрики прогона: одна строка.</summary>
    public string ExportSummary(SimulationMetrics metrics, SimulationOptions options, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);

        var builder = new StringBuilder();
        builder.AppendLine("policy,seed,node_count,job_count,makespan,avg_wait,avg_turnaround,avg_slowdown,cpu_utilization,ram_utilization,throughput_per_hour,completed,unfinished,rejected,preemptions,avg_queue_length,max_queue_length");
        builder.AppendLine(string.Join(',',
            Csv(metrics.PolicyName),
            Csv(options.Workload.Seed),
            Csv(options.Cluster.CustomNodes is { Count: > 0 } custom ? custom.Count : options.Cluster.NodeCount),
            Csv(metrics.TotalJobs),
            Csv(metrics.Makespan),
            Csv(metrics.AverageWaitTime),
            Csv(metrics.AverageTurnaround),
            Csv(metrics.AverageSlowdown),
            Csv(metrics.CpuUtilization),
            Csv(metrics.RamUtilization),
            Csv(metrics.ThroughputPerHour),
            Csv(metrics.CompletedJobs),
            Csv(metrics.UnfinishedJobs),
            Csv(metrics.RejectedJobs),
            Csv(metrics.PreemptionCount),
            Csv(metrics.AverageQueueLength),
            Csv(metrics.MaxQueueLength)));

        return Write(outputDirectory, "summary.csv", builder.ToString());
    }

    /// <summary>Детализация по каждому заданию.</summary>
    public string ExportJobs(SimulationMetrics metrics, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var builder = new StringBuilder();
        builder.AppendLine("job_id,owner,priority,submit_time,start_time,finish_time,wait_time,turnaround,slowdown,cpu,ram_mb,node_count,state,preemptions");

        foreach (var job in metrics.Jobs)
        {
            builder.AppendLine(string.Join(',',
                Csv(job.JobId),
                Csv(job.Owner),
                Csv(job.Priority),
                Csv(job.SubmitTime),
                Csv(job.StartTime),
                Csv(job.FinishTime),
                Csv(job.WaitTime),
                Csv(job.Turnaround),
                Csv(job.Slowdown),
                Csv(job.RequiredCpuCores),
                Csv(job.RequiredRamMb),
                Csv(job.RequiredNodeCount),
                Csv(job.State.ToString()),
                Csv(job.PreemptionCount)));
        }

        return Write(outputDirectory, "jobs.csv", builder.ToString());
    }

    /// <summary>Временной ряд загрузки кластера.</summary>
    public string ExportTimeSeries(SimulationMetrics metrics, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var builder = new StringBuilder();
        builder.AppendLine("time,running_jobs,pending_jobs,used_cpu,total_cpu,used_ram_mb,total_ram_mb");

        foreach (var point in metrics.TimeSeries)
        {
            builder.AppendLine(string.Join(',',
                Csv(point.Time),
                Csv(point.RunningJobs),
                Csv(point.PendingJobs),
                Csv(point.UsedCpuCores),
                Csv(point.TotalCpuCores),
                Csv(point.UsedRamMb),
                Csv(point.TotalRamMb)));
        }

        return Write(outputDirectory, "timeseries.csv", builder.ToString());
    }

    /// <summary>Конфигурация прогона в JSON для воспроизводимости.</summary>
    public string ExportConfig(SimulationOptions options, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Write(outputDirectory, "config.json", JsonSerializer.Serialize(options, JsonOptions));
    }

    /// <summary>Сравнение нескольких прогонов разных политик в одной таблице.</summary>
    public string ExportComparison(IReadOnlyList<SimulationMetrics> metricsList, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(metricsList);

        var builder = new StringBuilder();
        builder.AppendLine("policy,makespan,avg_wait,avg_turnaround,avg_slowdown,cpu_utilization,ram_utilization,throughput_per_hour,completed,preemptions");

        foreach (var metrics in metricsList)
        {
            builder.AppendLine(string.Join(',',
                Csv(metrics.PolicyName),
                Csv(metrics.Makespan),
                Csv(metrics.AverageWaitTime),
                Csv(metrics.AverageTurnaround),
                Csv(metrics.AverageSlowdown),
                Csv(metrics.CpuUtilization),
                Csv(metrics.RamUtilization),
                Csv(metrics.ThroughputPerHour),
                Csv(metrics.CompletedJobs),
                Csv(metrics.PreemptionCount)));
        }

        return Write(outputDirectory, "comparison.csv", builder.ToString());
    }

    /// <summary>Полный комплект файлов одного прогона.</summary>
    public IReadOnlyList<string> ExportAll(SimulationMetrics metrics, SimulationOptions options, string outputDirectory)
    {
        return new[]
        {
            ExportSummary(metrics, options, outputDirectory),
            ExportJobs(metrics, outputDirectory),
            ExportTimeSeries(metrics, outputDirectory),
            ExportConfig(options, outputDirectory)
        };
    }

    private static string Write(string outputDirectory, string fileName, string content)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Каталог экспорта не может быть пустым.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static string Csv(double value) => value.ToString("R", Invariant);

    private static string Csv(int value) => value.ToString(Invariant);

    private static string Csv(double? value) => value.HasValue ? Csv(value.Value) : string.Empty;

    private static string Csv(int? value) => value.HasValue ? Csv(value.Value) : string.Empty;

    private static string Csv(string value)
    {
        value ??= string.Empty;

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}