using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.Job;
using ClusterSim.Core.Simulation.Metrics;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Workload;

namespace ClusterSim.Core.Simulation.Engine;

/// <summary>
/// Дискретно-событийный движок симуляции: владеет виртуальным временем,
/// очередью событий, применяет планы планирования и уведомляет сборщик метрик.
/// Движок зависит от источника заданий через IJobSource, а не от конкретного генератора.
/// </summary>
public sealed class SimulationEngine
{
    private readonly Cluster _cluster;
    private readonly IJobSource _jobSource;
    private readonly ISchedulingPolicy _policy;
    private readonly IPlacementStrategy _placement;
    private readonly MetricsCollector _metrics;
    private readonly SimulationOptions _options;
    private readonly EventQueue _queue = new();
    private readonly List<Job> _pending = new();
    private readonly List<Job> _running = new();
    private readonly List<Job> _allJobs = new();
    private readonly Dictionary<string, double> _userUsage = new();
    private readonly List<SimulationEvent> _trace = new();

    private double _clock;
    private long _sequence;

    public SimulationEngine(
        Cluster cluster,
        IJobSource jobSource,
        ISchedulingPolicy policy,
        IPlacementStrategy placement,
        MetricsCollector metrics,
        SimulationOptions options)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _jobSource = jobSource ?? throw new ArgumentNullException(nameof(jobSource));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _placement = placement ?? throw new ArgumentNullException(nameof(placement));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Текущее виртуальное время симуляции.</summary>
    public double CurrentTime => _clock;

    /// <summary>Все задания прогона, включая невыполнимые и незавершенные.</summary>
    public IReadOnlyList<Job> Jobs => _allJobs;

    /// <summary>Кластер прогона.</summary>
    public Cluster Cluster => _cluster;

    /// <summary>Хронология примененных событий: для отладки и экспорта.</summary>
    public IReadOnlyList<SimulationEvent> Trace => _trace;

    /// <summary>Выполняет прогон симуляции до конца и возвращает метрики.</summary>
    public SimulationMetrics Run()
    {
        Initialize();

        while (!_queue.IsEmpty)
        {
            var simulationEvent = _queue.Dequeue();

            if (_options.MaxSimulationTime > 0 && simulationEvent.Time > _options.MaxSimulationTime)
            {
                break;
            }

            AdvanceTo(simulationEvent.Time);
            Process(simulationEvent);
        }

        return FinalizeRun();
    }

    /// <summary>Загружает поток заданий и отбраковывает заведомо невыполнимые.</summary>
    private void Initialize()
    {
        var jobs = _jobSource.GetJobs();

        foreach (var job in jobs)
        {
            _allJobs.Add(job);

            var tooBigForNode = !job.RequiredResources.FitsInto(_cluster.LargestNodeCapacity);
            var tooManyNodes = job.RequiredNodeCount > _cluster.NodeCount;

            if (tooBigForNode || tooManyNodes)
            {
                job.MarkUnschedulable();
                continue;
            }

            _queue.Enqueue(new SimulationEvent(
                job.SubmitTime, _sequence++, SimulationEventType.JobSubmitted, job));
        }

        var startTime = _allJobs.Count > 0 ? _allJobs.Min(job => job.SubmitTime) : 0.0;
        _clock = startTime;
        _metrics.Begin(startTime, _cluster, _pending.Count, _running.Count);
    }

    /// <summary>Продвигает виртуальное время и интегрирует потребление ресурсов.</summary>
    private void AdvanceTo(double time)
    {
        if (time < _clock)
        {
            throw new InvalidOperationException($"Виртуальное время не может идти назад: {time} < {_clock}.");
        }

        IntegrateUserUsage(time);
        _metrics.AdvanceTime(time, _cluster, _pending.Count, _running.Count);
        _clock = time;
    }

    /// <summary>Накапливает ядро-секунды по владельцам для Fair-Share.</summary>
    private void IntegrateUserUsage(double time)
    {
        var delta = time - _clock;

        if (delta <= 0)
        {
            return;
        }

        foreach (var job in _running)
        {
            var consumed = job.RequiredResources.CpuCores * job.RequiredNodeCount * delta;
            _userUsage[job.Owner] = _userUsage.GetValueOrDefault(job.Owner) + consumed;
        }
    }

    private void Process(SimulationEvent simulationEvent)
    {
        switch (simulationEvent.Type)
        {
            case SimulationEventType.JobSubmitted:
                _trace.Add(simulationEvent);
                _pending.Add(simulationEvent.Job!);
                RequestScheduling();
                break;

            case SimulationEventType.JobFinished:
                // Защита от любых устаревших событий: завершать можно только выполняющееся задание.
                if (simulationEvent.Job!.State != JobState.Running)
                {
                    break;
                }

                FinishJob(simulationEvent.Job, simulationEvent.Time);
                RequestScheduling();
                break;

            default:
                throw new InvalidOperationException(
                    $"Тип события {simulationEvent.Type} не помещается движком в очередь.");
        }
    }

    /// <summary>Освобождает ресурсы завершившегося задания.</summary>
    private void FinishJob(Job job, double time)
    {
        var nodeIds = job.AssignedNodeIds.ToList();
        _cluster.Release(job, nodeIds);
        job.Finish(time);
        _running.Remove(job);
        _trace.Add(new SimulationEvent(time, _sequence++, SimulationEventType.JobFinished, job, nodeIds));
    }

    /// <summary>Запрашивает у политики план и применяет его к кластеру.</summary>
    private void RequestScheduling()
    {
        var context = new SchedulingContext(
            _clock,
            _cluster,
            _pending.ToList(),
            _running.ToList(),
            _placement,
            _options.Policy,
            _userUsage);

        var plan = _policy.Plan(context);

        ApplyPreemptions(plan);
        ApplyStarts(plan);
    }

    /// <summary>Прерывает задания из плана: контрольная точка и возврат в очередь.</summary>
    private void ApplyPreemptions(SchedulingPlan plan)
    {
        foreach (var job in plan.Preemptions)
        {
            if (job.State != JobState.Running)
            {
                continue;
            }

            var nodeIds = job.AssignedNodeIds.ToList();
            _cluster.Release(job, nodeIds);
            job.Preempt(_clock, _options.Policy.CheckpointOverheadSeconds);
            _running.Remove(job);
            _pending.Add(job);

            // Отменяем устаревшее событие завершения, поставленное до прерывания:
            // после возобновления будет поставлено новое, с актуальным остатком времени.
            _queue.RemoveAll(simulationEvent =>
                simulationEvent.Type == SimulationEventType.JobFinished &&
                ReferenceEquals(simulationEvent.Job, job));

            _trace.Add(new SimulationEvent(_clock, _sequence++, SimulationEventType.JobPreempted, job, nodeIds));
        }
    }

    /// <summary>Запускает задания из плана и ставит события их завершения в очередь.</summary>
    private void ApplyStarts(SchedulingPlan plan)
    {
        foreach (var start in plan.Starts)
        {
            var job = start.Job;

            if (!job.IsQueued || !_cluster.CanPlace(job, start.NodeIds))
            {
                continue;
            }

            _cluster.Allocate(job, start.NodeIds);
            job.Start(_clock, start.NodeIds);
            _pending.Remove(job);
            _running.Add(job);
            _trace.Add(new SimulationEvent(_clock, _sequence++, SimulationEventType.JobStarted, job, start.NodeIds));

            _queue.Enqueue(new SimulationEvent(
                _clock + job.RemainingDuration, _sequence++, SimulationEventType.JobFinished, job));
        }
    }

    /// <summary>Завершает прогон: финальная точка ряда и сбор метрик.</summary>
    private SimulationMetrics FinalizeRun()
    {
        _trace.Add(new SimulationEvent(_clock, _sequence++, SimulationEventType.SimulationFinished));

        return _metrics.Build(
            _policy.Name,
            _clock,
            _cluster,
            _pending.Count,
            _running.Count,
            _allJobs);
    }
}