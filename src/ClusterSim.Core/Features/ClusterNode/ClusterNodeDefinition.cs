using ClusterSim.Core.Features.Resources;

namespace ClusterSim.Core.Features.ClusterNode;

/// <summary>
/// Сериализуемое описание одного узла пользовательского кластера:
/// вычислительные ресурсы и диск.
/// </summary>
public sealed record ClusterNodeDefinition
{
    public ClusterNodeDefinition(string name, int cpuCores, int ramMb, int diskGb = 512)
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

        if (diskGb <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(diskGb), "Объем диска должен быть положительным.");
        }

        Name = name;
        CpuCores = cpuCores;
        RamMb = ramMb;
        DiskGb = diskGb;
    }

    /// <summary>Имя узла.</summary>
    public string Name { get; init; }

    /// <summary>Количество ядер CPU.</summary>
    public int CpuCores { get; init; }

    /// <summary>Объем RAM в мегабайтах.</summary>
    public int RamMb { get; init; }

    /// <summary>Объем дисковой подсистемы узла в ГБ (справочная характеристика).</summary>
    public int DiskGb { get; init; }

    /// <summary>Преобразует описание в вектор ресурсов для планировщика.</summary>
    public ResourceVector ToResourceVector() => new(CpuCores, RamMb);
}