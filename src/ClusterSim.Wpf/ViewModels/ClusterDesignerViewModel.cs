using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Wpf.Services;

namespace ClusterSim.Wpf.ViewModels;

/// <summary>
/// Редактируемая строка узла в дизайнере кластера.
/// </summary>
public sealed partial class DesignerNodeRow : ObservableObject
{
    /// <summary>Имя узла.</summary>
    [ObservableProperty]
    private string _name = "node";

    /// <summary>Количество ядер CPU.</summary>
    [ObservableProperty]
    private int _cpuCores = 4;

    /// <summary>Объем RAM в мегабайтах.</summary>
    [ObservableProperty]
    private int _ramMb = 8192;
}

/// <summary>
/// ViewModel конструктора кластеров: редактирование узлов,
/// сохранение и загрузка конфигураций через хранилище.
/// </summary>
public sealed partial class ClusterDesignerViewModel : ObservableObject
{
    private readonly ClusterDesignerService _service;

    public ClusterDesignerViewModel(ClusterDesignerService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        Nodes = new ObservableCollection<DesignerNodeRow>();
        SavedClusters = new ObservableCollection<ClusterDefinition>();

        AddNode();
        AddNode();
        RefreshSaved();
    }

    /// <summary>Редактируемый список узлов будущего кластера.</summary>
    public ObservableCollection<DesignerNodeRow> Nodes { get; }

    /// <summary>Список сохраненных конфигураций кластеров.</summary>
    public ObservableCollection<ClusterDefinition> SavedClusters { get; }

    /// <summary>Название конфигурации.</summary>
    [ObservableProperty]
    private string _clusterName = "My cluster";

    /// <summary>Описание конфигурации.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>Выбранная строка узла.</summary>
    [ObservableProperty]
    private DesignerNodeRow? _selectedNode;

    /// <summary>Выбранная сохраненная конфигурация.</summary>
    [ObservableProperty]
    private ClusterDefinition? _selectedSaved;

    /// <summary>Сообщение о результате последнего действия.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Добавляет узел в конструктор.</summary>
    [RelayCommand]
    private void AddNode()
    {
        Nodes.Add(new DesignerNodeRow { Name = $"node-{Nodes.Count:D2}" });
    }

    /// <summary>Удаляет выбранный узел.</summary>
    [RelayCommand]
    private void RemoveSelectedNode()
    {
        if (SelectedNode is null)
        {
            StatusMessage = "Выберите узел для удаления.";
            return;
        }

        Nodes.Remove(SelectedNode);
        SelectedNode = null;
    }

    /// <summary>Сохраняет текущую конфигурацию в хранилище.</summary>
    [RelayCommand]
    private void SaveCluster()
    {
        try
        {
            var definition = BuildDefinition();
            _service.Save(definition);
            StatusMessage = $"Кластер \"{definition.Name}\" сохранен.";
            RefreshSaved();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Ошибка сохранения: {exception.Message}";
        }
    }

    /// <summary>Загружает выбранную сохраненную конфигурацию в редактор.</summary>
    [RelayCommand]
    private void LoadSelectedCluster()
    {
        if (SelectedSaved is null)
        {
            StatusMessage = "Выберите сохраненный кластер.";
            return;
        }

        var definition = _service.Load(SelectedSaved.Id);

        if (definition is null)
        {
            StatusMessage = "Сохраненный кластер не найден.";
            return;
        }

        ApplyDefinition(definition);
        StatusMessage = $"Кластер \"{definition.Name}\" загружен в редактор.";
    }

    /// <summary>Удаляет выбранную сохраненную конфигурацию.</summary>
    [RelayCommand]
    private void DeleteSelectedCluster()
    {
        if (SelectedSaved is null)
        {
            StatusMessage = "Выберите сохраненный кластер.";
            return;
        }

        var name = SelectedSaved.Name;
        _service.Delete(SelectedSaved.Id);
        RefreshSaved();
        StatusMessage = $"Кластер \"{name}\" удален.";
    }

    /// <summary>Обновляет список сохраненных конфигураций.</summary>
    [RelayCommand]
    private void RefreshSaved()
    {
        SavedClusters.Clear();

        foreach (var definition in _service.ListSaved())
        {
            SavedClusters.Add(definition);
        }
    }

    /// <summary>Строит сохраняемое определение из текущего состояния редактора.</summary>
    public ClusterDefinition BuildDefinition()
    {
        if (Nodes.Count == 0)
        {
            throw new InvalidOperationException("Кластер должен содержать хотя бы один узел.");
        }

        return new ClusterDefinition
        {
            Name = string.IsNullOrWhiteSpace(ClusterName) ? "Без названия" : ClusterName,
            Description = Description,
            Nodes = Nodes
                .Select(node => new ClusterNodeDefinition(node.Name, node.CpuCores, node.RamMb))
                .ToList()
        };
    }

    /// <summary>Заполняет редактор из загруженного определения.</summary>
    private void ApplyDefinition(ClusterDefinition definition)
    {
        ClusterName = definition.Name;
        Description = definition.Description;

        Nodes.Clear();

        foreach (var node in definition.Nodes)
        {
            Nodes.Add(new DesignerNodeRow
            {
                Name = node.Name,
                CpuCores = node.CpuCores,
                RamMb = node.RamMb
            });
        }
    }
}