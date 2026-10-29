namespace ClusterSim.Core.Features.ClusterNode;

/// <summary>
/// Параметры шаблонного узла для быстрой сборки однородного кластера.
/// </summary>
public sealed record ClusterNodeOptions(int CpuCores = 4, int RamMb = 8192)
{
    /// <summary>Создает описание узла по шаблону с указанным именем.</summary>
    public ClusterNodeDefinition ToDefinition(string name) => new(name, CpuCores, RamMb);
}