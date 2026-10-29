using ClusterSim.Core.Features.Resources;

namespace ClusterSim.Core.Features.ClusterNode;

/// <summary>
/// Сериализуемое описание одного узла пользовательского кластера.
/// Используется для сохранения и загрузки конфигураций кластера.
/// </summary>
public sealed record ClusterNodeDefinition
{
    public ClusterNodeDefinition(string name, int cpuCores, int ramMb)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Имя узла не может быть пустым.", nameof(name));
        }

        if (cpuCores <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cpuCores), "Число ядер должно быть положительным.");
        }

        if (ramMb <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ramMb), "Объем RAM должен быть положительным.");
        }

        Name = name;
        CpuCores = cpuCores;
        RamMb = ramMb;
    }

    /// <summary>Имя узла.</summary>
    public string Name { get; init; }

    /// <summary>Количество ядер CPU.</summary>
    public int CpuCores { get; init; }

    /// <summary>Объем RAM в мегабайтах.</summary>
    public int RamMb { get; init; }

    /// <summary>Преобразует описание в вектор ресурсов.</summary>
    public ResourceVector ToResourceVector() => new(CpuCores, RamMb);
}