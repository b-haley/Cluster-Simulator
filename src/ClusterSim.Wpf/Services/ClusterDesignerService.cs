using ClusterSim.Core.Features.Cluster;

namespace ClusterSim.Wpf.Services;

/// <summary>
/// Тонкий сервис над хранилищем конфигураций кластера.
/// Сейчас используется файловая реализация из Core;
/// позже через DI можно подменить ее на PostgreSQL-реализацию из Infrastructure,
/// не меняя ни ViewModel, ни этот сервис.
/// </summary>
public sealed class ClusterDesignerService
{
    private readonly IClusterDefinitionStore _store;

    public ClusterDesignerService(IClusterDefinitionStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Список сохраненных конфигураций.</summary>
    public IReadOnlyList<ClusterDefinition> ListSaved() => _store.ListAll();

    /// <summary>Сохраняет конфигурацию.</summary>
    public void Save(ClusterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _store.Save(definition);
    }

    /// <summary>Загружает конфигурацию по идентификатору.</summary>
    public ClusterDefinition? Load(Guid id) => _store.Load(id);

    /// <summary>Удаляет конфигурацию.</summary>
    public bool Delete(Guid id) => _store.Delete(id);
}