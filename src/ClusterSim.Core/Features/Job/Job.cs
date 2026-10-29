using ClusterSim.Core.Features.Resources;

namespace ClusterSim.Core.Features.Job;

/// <summary>
/// Задание кластера: требования к ресурсам, времена жизненного цикла и состояние.
/// Класс изменяемый: состояние меняет движок симуляции, а не внешние политики напрямую.
/// </summary>
public sealed class Job
{
    private readonly List<int> _assignedNodeIds = new();

    public Job(
        int id,
        string owner,
        double submitTime,
        int priority,
        ResourceVector requiredResources,
        int requiredNodeCount,
        double estimatedDuration,
        double totalDuration)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("Владелец задания не может быть пустым.", nameof(owner));
        }

        if (submitTime < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(submitTime), "Время поступления не может быть отрицательным.");
        }

        if (requiredNodeCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredNodeCount), "Задание требует минимум один узел.");
        }

        if (estimatedDuration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedDuration), "Оценочная длительность должна быть положительной.");
        }

        if (totalDuration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDuration), "Фактическая длительность должна быть положительной.");
        }

        Id = id;
        Owner = owner;
        SubmitTime = submitTime;
        Priority = priority;
        RequiredResources = requiredResources;
        RequiredNodeCount = requiredNodeCount;
        EstimatedDuration = estimatedDuration;
        TotalDuration = totalDuration;
        RemainingDuration = totalDuration;
        State = JobState.Pending;
    }

    /// <summary>Уникальный идентификатор задания.</summary>
    public int Id { get; }

    /// <summary>Владелец или группа, от имени которой подано задание.</summary>
    public string Owner { get; }

    /// <summary>Время поступления задания в очередь (секунды симуляции).</summary>
    public double SubmitTime { get; }

    /// <summary>Приоритет: больше значение — важнее задание.</summary>
    public int Priority { get; }

    /// <summary>Требуемые ресурсы в расчете на один узел.</summary>
    public ResourceVector RequiredResources { get; }

    /// <summary>Сколько узлов нужно занять одновременно (1 — обычное задание, >1 — аналог MPI).</summary>
    public int RequiredNodeCount { get; }

    /// <summary>Оценка длительности, которой пользуется планировщик (например, EASY Backfill).</summary>
    public double EstimatedDuration { get; }

    /// <summary>Полный объем работы задания в секундах.</summary>
    public double TotalDuration { get; }

    /// <summary>Сколько работы осталось выполнить с учетом прерываний.</summary>
    public double RemainingDuration { get; private set; }

    /// <summary>Текущее состояние задания.</summary>
    public JobState State { get; private set; }

    /// <summary>Время первого старта; используется для метрики ожидания.</summary>
    public double? StartTime { get; private set; }

    /// <summary>Время начала текущего запуска; нужно для расчета остатка при прерывании.</summary>
    public double? CurrentRunStartTime { get; private set; }

    /// <summary>Время завершения задания.</summary>
    public double? FinishTime { get; private set; }

    /// <summary>Сколько раз задание прерывали.</summary>
    public int PreemptionCount { get; private set; }

    /// <summary>Узлы, на которых задание выполняется сейчас.</summary>
    public IReadOnlyList<int> AssignedNodeIds => _assignedNodeIds;

    /// <summary>Признак параллельного (многоузлового) задания.</summary>
    public bool IsParallel => RequiredNodeCount > 1;

    /// <summary>Признак того, что задание находится в очереди ожидания.</summary>
    public bool IsQueued => State is JobState.Pending or JobState.Suspended;

    /// <summary>Признак завершенного задания.</summary>
    public bool IsFinished => State == JobState.Finished;

    /// <summary>Запускает задание на указанных узлах.</summary>
    public void Start(double time, IReadOnlyList<int> nodeIds)
    {
        if (!IsQueued)
        {
            throw new InvalidOperationException($"Задание {Id} нельзя запустить из состояния {State}.");
        }

        if (nodeIds.Count != RequiredNodeCount)
        {
            throw new InvalidOperationException(
                $"Заданию {Id} нужно {RequiredNodeCount} узла(ов), передано {nodeIds.Count}.");
        }

        StartTime ??= time;
        CurrentRunStartTime = time;
        _assignedNodeIds.Clear();
        _assignedNodeIds.AddRange(nodeIds);
        State = JobState.Running;
    }

    /// <summary>Завершает выполняющееся задание.</summary>
    public void Finish(double time)
    {
        if (State != JobState.Running)
        {
            throw new InvalidOperationException($"Задание {Id} не выполняется, завершение невозможно.");
        }

        FinishTime = time;
        RemainingDuration = 0;
        CurrentRunStartTime = null;
        _assignedNodeIds.Clear();
        State = JobState.Finished;
    }

    /// <summary>
    /// Прерывает задание с контрольной точкой: сохраняет остаток работы,
    /// добавляет накладные расходы и возвращает задание в очередь.
    /// </summary>
    public void Preempt(double time, double checkpointOverhead)
    {
        if (State != JobState.Running)
        {
            throw new InvalidOperationException($"Задание {Id} не выполняется, прерывание невозможно.");
        }

        var elapsed = time - CurrentRunStartTime!.Value;
        RemainingDuration = Math.Max(0, RemainingDuration - elapsed) + checkpointOverhead;
        PreemptionCount++;
        CurrentRunStartTime = null;
        _assignedNodeIds.Clear();
        State = JobState.Suspended;
    }

    /// <summary>Помечает задание как невыполнимое на данном кластере.</summary>
    public void MarkUnschedulable()
    {
        if (!IsQueued)
        {
            throw new InvalidOperationException($"Задание {Id} нельзя пометить невыполнимым из состояния {State}.");
        }

        State = JobState.Unschedulable;
        _assignedNodeIds.Clear();
    }

    /// <summary>Время ожидания: от поступления до первого старта.</summary>
    public double GetWaitTime() =>
        (StartTime ?? throw new InvalidOperationException($"Задание {Id} еще не начиналось.")) - SubmitTime;

    /// <summary>Время оборота: от поступления до завершения.</summary>
    public double GetTurnaround() =>
        (FinishTime ?? throw new InvalidOperationException($"Задание {Id} еще не завершено.")) - SubmitTime;

    /// <summary>Замедление: оборот относительно чистой длительности.</summary>
    public double GetSlowdown() => GetTurnaround() / TotalDuration;
}