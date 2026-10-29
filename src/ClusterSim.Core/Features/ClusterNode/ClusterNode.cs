using ClusterSim.Core.Features.Resources;

namespace ClusterSim.Core.Features.ClusterNode;

/// <summary>
/// Узел кластера с ограниченной емкостью ресурсов.
/// Отвечает только за выделение и освобождение ресурсов, но не за выбор заданий.
/// </summary>
public sealed class ClusterNode
{
    private readonly List<int> _runningJobIds = new();

    public ClusterNode(int id, string name, ResourceVector totalCapacity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Имя узла не может быть пустым.", nameof(name));
        }

        if (totalCapacity.CpuCores <= 0 || totalCapacity.RamMb <= 0)
        {
            throw new ArgumentException("Емкость узла должна быть положительной по CPU и RAM.", nameof(totalCapacity));
        }

        Id = id;
        Name = name;
        TotalCapacity = totalCapacity;
        AvailableResources = totalCapacity;
    }

    /// <summary>Идентификатор узла в кластере.</summary>
    public int Id { get; }

    /// <summary>Человекочитаемое имя узла.</summary>
    public string Name { get; }

    /// <summary>Полная емкость ресурсов узла.</summary>
    public ResourceVector TotalCapacity { get; }

    /// <summary>Свободные ресурсы узла в текущий момент.</summary>
    public ResourceVector AvailableResources { get; private set; }

    /// <summary>Идентификаторы заданий, выполняющихся на узле.</summary>
    public IReadOnlyList<int> RunningJobIds => _runningJobIds;

    /// <summary>Признак доступности узла для планирования.</summary>
    public bool IsOnline { get; private set; } = true;

    /// <summary>Текущее состояние узла.</summary>
    public ClusterNodeState State => !IsOnline
        ? ClusterNodeState.Offline
        : AvailableResources.IsZero
            ? ClusterNodeState.FullyBusy
            : AvailableResources.Equals(TotalCapacity)
                ? ClusterNodeState.Idle
                : ClusterNodeState.PartiallyBusy;

    /// <summary>Доля занятых ядер CPU от 0 до 1.</summary>
    public double CpuUtilization => 1.0 - (double)AvailableResources.CpuCores / TotalCapacity.CpuCores;

    /// <summary>Проверяет, можно ли разместить на узле заданный набор ресурсов.</summary>
    public bool CanAccept(ResourceVector required) =>
        IsOnline && required.FitsInto(AvailableResources);

    /// <summary>Выделяет ресурсы под задание.</summary>
    public void Allocate(ResourceVector required, int jobId)
    {
        if (!CanAccept(required))
        {
            throw new InvalidOperationException(
                $"Узел {Id} не может принять {required}: свободно {AvailableResources}.");
        }

        AvailableResources = AvailableResources.Subtract(required);
        _runningJobIds.Add(jobId);
    }

    /// <summary>Освобождает ресурсы после задания.</summary>
    public void Release(ResourceVector required, int jobId)
    {
        if (!_runningJobIds.Remove(jobId))
        {
            throw new InvalidOperationException($"Задание {jobId} не выполняется на узле {Id}.");
        }

        AvailableResources = AvailableResources.Add(required);

        if (!AvailableResources.FitsInto(TotalCapacity))
        {
            throw new InvalidOperationException($"Освобождение ресурсов переполнило емкость узла {Id}.");
        }
    }

    /// <summary>Включает или выключает узел.</summary>
    public void SetOnline(bool isOnline) => IsOnline = isOnline;
}