namespace ClusterSim.Core.Features.Cluster;

/// <summary>
/// Абстракция хранилища конфигураций кластера.
/// Реализации: файловая система (Core), PostgreSQL (Infrastructure), память (тесты).
/// </summary>
public interface IClusterDefinitionStore
{
    /// <summary>Сохраняет или перезаписывает конфигурацию.</summary>
    void Save(ClusterDefinition definition);

    /// <summary>Загружает конфигурацию по идентификатору или возвращает null.</summary>
    ClusterDefinition? Load(Guid id);

    /// <summary>Возвращает все сохраненные конфигурации.</summary>
    IReadOnlyList<ClusterDefinition> ListAll();

    /// <summary>Удаляет конфигурацию; возвращает признак успешности.</summary>
    bool Delete(Guid id);

    /// <summary>Проверяет существование конфигурации.</summary>
    bool Exists(Guid id);
}