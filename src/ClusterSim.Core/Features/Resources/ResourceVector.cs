namespace ClusterSim.Core.Features.Resources;

/// <summary>
/// Неизменяемый вектор ресурсов узла или задания: ядра CPU и RAM в мегабайтах.
/// </summary>
public readonly record struct ResourceVector(int CpuCores, int RamMb)
{
    /// <summary>Нулевой вектор ресурсов.</summary>
    public static ResourceVector Zero => default;

    /// <summary>Признак пустого вектора.</summary>
    public bool IsZero => CpuCores == 0 && RamMb == 0;

    /// <summary>Проверяет, помещается ли текущий набор ресурсов в указанный объем.</summary>
    public bool FitsInto(ResourceVector capacity) =>
        CpuCores <= capacity.CpuCores && RamMb <= capacity.RamMb;

    /// <summary>Складывает два вектора ресурсов.</summary>
    public ResourceVector Add(ResourceVector other) =>
        new(CpuCores + other.CpuCores, RamMb + other.RamMb);

    /// <summary>Вычитает другой вектор; бросает исключение при нехватке ресурсов.</summary>
    public ResourceVector Subtract(ResourceVector other)
    {
        if (!other.FitsInto(this))
        {
            throw new InvalidOperationException(
                $"Нельзя освободить больше ресурсов, чем занято: требуется {other}, доступно {this}.");
        }

        return new ResourceVector(CpuCores - other.CpuCores, RamMb - other.RamMb);
    }

    public override string ToString() => $"{CpuCores} CPU, {RamMb} MB RAM";
}