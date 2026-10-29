namespace ClusterSim.Core.Simulation.Engine;

/// <summary>
/// Приоритетная очередь событий: сначала по времени, затем по порядковому номеру.
/// </summary>
public sealed class EventQueue
{
    private sealed class EventTimeComparer : IComparer<SimulationEvent>
    {
        public int Compare(SimulationEvent? x, SimulationEvent? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var byTime = x.Time.CompareTo(y.Time);
            return byTime != 0 ? byTime : x.Sequence.CompareTo(y.Sequence);
        }
    }

    /// <summary>
    /// Удаляет из очереди все события, удовлетворяющие предикату,
    /// сохраняя порядок остальных. Используется для отмены устаревших событий.
    /// </summary>
    public int RemoveAll(Func<SimulationEvent, bool> shouldRemove)
    {
        ArgumentNullException.ThrowIfNull(shouldRemove);

        var kept = new List<SimulationEvent>(_queue.Count);

        while (_queue.Count > 0)
        {
            var item = _queue.Dequeue();

            if (!shouldRemove(item))
            {
                kept.Add(item);
            }
        }

        foreach (var item in kept)
        {
            _queue.Enqueue(item, item);
        }

        return kept.Count;
    }

    private readonly PriorityQueue<SimulationEvent, SimulationEvent> _queue = new(new EventTimeComparer());

    /// <summary>Количество событий в очереди.</summary>
    public int Count => _queue.Count;

    /// <summary>Признак пустой очереди.</summary>
    public bool IsEmpty => _queue.Count == 0;

    /// <summary>Добавляет событие в очередь.</summary>
    public void Enqueue(SimulationEvent simulationEvent)
    {
        ArgumentNullException.ThrowIfNull(simulationEvent);
        _queue.Enqueue(simulationEvent, simulationEvent);
    }

    /// <summary>Извлекает ближайшее событие.</summary>
    public SimulationEvent Dequeue() => _queue.Dequeue();

    /// <summary>Пробует посмотреть ближайшее событие без извлечения.</summary>
    public bool TryPeek(out SimulationEvent? simulationEvent)
    {
        if (_queue.TryPeek(out var element, out _))
        {
            simulationEvent = element;
            return true;
        }

        simulationEvent = null;
        return false;
    }

    /// <summary>Очищает очередь.</summary>
    public void Clear() => _queue.Clear();
}