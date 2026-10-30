using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Simulation.Scheduling;

namespace ClusterSim.Wpf.ViewModels;

/// <summary>
/// Редактируемая строка узла в дизайнере кластера.
/// RAM и диск указываются в ГБ, как в реальных спецификациях.
/// </summary>
public sealed partial class DesignerNodeRow : ObservableObject
{
    /// <summary>Имя узла.</summary>
    [ObservableProperty]
    private string _name = "node";

    /// <summary>Количество ядер CPU.</summary>
    [ObservableProperty]
    private int _cpuCores = 8;

    /// <summary>Объем RAM, ГБ.</summary>
    [ObservableProperty]
    private int _ramGb = 32;

    /// <summary>Объем диска, ГБ.</summary>
    [ObservableProperty]
    private int _diskGb = 512;
}

/// <summary>
/// ViewModel конструктора кластеров: паспорт конфигурации, шаблонная генерация узлов,
/// ручная правка неоднородных узлов, выбор планировщика и сводка характеристик.
/// </summary>
public sealed partial class ClusterDesignerViewModel : ObservableObject
{
    public ClusterDesignerViewModel()
    {
        Nodes = new ObservableCollection<DesignerNodeRow>();
        GenerateNodesFromTemplate();
    }

    /// <summary>Редактируемый список узлов будущего кластера.</summary>
    public ObservableCollection<DesignerNodeRow> Nodes { get; }

    /// <summary>Политики, доступные для выбора планировщика кластера.</summary>
    public IReadOnlyList<SchedulingPolicyKind> AvailablePolicies { get; } = Enum.GetValues<SchedulingPolicyKind>();

    // ---------------- Паспорт конфигурации ----------------

    /// <summary>Название кластера.</summary>
    [ObservableProperty]
    private string _clusterName = "Новый кластер";

    /// <summary>Описание кластера.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>Выбранная строка узла.</summary>
    [ObservableProperty]
    private DesignerNodeRow? _selectedNode;

    /// <summary>Сообщение о результате последнего действия.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ---------------- Планировщик кластера ----------------

    /// <summary>Политика планирования по умолчанию для этого кластера.</summary>
    [ObservableProperty]
    private SchedulingPolicyKind _policy = SchedulingPolicyKind.Fifo;

    /// <summary>Строгий FIFO.</summary>
    [ObservableProperty]
    private bool _strictFifo = true;

    /// <summary>Разрешить вытеснение с контрольной точкой.</summary>
    [ObservableProperty]
    private bool _allowPreemption;

    // ---------------- Шаблон быстрой генерации ----------------

    /// <summary>Сколько узлов сгенерировать.</summary>
    [ObservableProperty]
    private int _templateNodeCount = 4;

    /// <summary>Ядер на узел в шаблоне.</summary>
    [ObservableProperty]
    private int _templateCpuCores = 8;

    /// <summary>RAM на узел в шаблоне, ГБ.</summary>
    [ObservableProperty]
    private int _templateRamGb = 32;

    /// <summary>Диск на узел в шаблоне, ГБ.</summary>
    [ObservableProperty]
    private int _templateDiskGb = 512;

    // ---------------- Сводка характеристик ----------------

    /// <summary>Суммарное число узлов.</summary>
    public int TotalNodes => Nodes.Count;

    /// <summary>Суммарное число ядер.</summary>
    public int TotalCores => Nodes.Sum(node => node.CpuCores);

    /// <summary>Суммарная RAM, ГБ.</summary>
    public int TotalRamGb => Nodes.Sum(node => node.RamGb);

    /// <summary>Суммарный диск, ГБ.</summary>
    public int TotalDiskGb => Nodes.Sum(node => node.DiskGb);

    // ---------------- Команды ----------------

    /// <summary>Добавляет один узел с характеристиками текущего шаблона.</summary>
    [RelayCommand]
    private void AddNode()
    {
        var row = new DesignerNodeRow
        {
            Name = $"node-{Nodes.Count:D2}",
            CpuCores = TemplateCpuCores,
            RamGb = TemplateRamGb,
            DiskGb = TemplateDiskGb
        };

        Attach(row);
        Nodes.Add(row);
        NotifyTotals();
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

        Detach(SelectedNode);
        Nodes.Remove(SelectedNode);
        SelectedNode = null;
        NotifyTotals();
    }

    /// <summary>Полностью пересобирает список узлов по шаблону.</summary>
    [RelayCommand]
    private void GenerateNodesFromTemplate()
    {
        foreach (var row in Nodes.ToList())
        {
            Detach(row);
        }

        Nodes.Clear();

        var count = Math.Clamp(TemplateNodeCount, 1, 512);

        for (var index = 0; index < count; index++)
        {
            var row = new DesignerNodeRow
            {
                Name = $"node-{index:D2}",
                CpuCores = TemplateCpuCores,
                RamGb = TemplateRamGb,
                DiskGb = TemplateDiskGb
            };

            Attach(row);
            Nodes.Add(row);
        }

        NotifyTotals();
        StatusMessage = $"Сгенерировано узлов: {count}.";
    }

    // ---------------- Сборка и применение определения ----------------

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
            Policy = Policy,
            StrictFifo = StrictFifo,
            AllowPreemption = AllowPreemption,
            Nodes = Nodes
                .Select(node => new ClusterNodeDefinition(node.Name, node.CpuCores, node.RamGb * 1024, node.DiskGb))
                .ToList()
        };
    }

    /// <summary>Заполняет редактор из загруженного определения.</summary>
    public void ApplyDefinition(ClusterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ClusterName = definition.Name;
        Description = definition.Description;
        Policy = definition.Policy;
        StrictFifo = definition.StrictFifo;
        AllowPreemption = definition.AllowPreemption;

        foreach (var row in Nodes.ToList())
        {
            Detach(row);
        }

        Nodes.Clear();

        foreach (var node in definition.Nodes)
        {
            var row = new DesignerNodeRow
            {
                Name = node.Name,
                CpuCores = node.CpuCores,
                RamGb = Math.Max(1, node.RamMb / 1024),
                DiskGb = node.DiskGb
            };

            Attach(row);
            Nodes.Add(row);
        }

        NotifyTotals();
        StatusMessage = $"Кластер \"{definition.Name}\" загружен в редактор.";
    }

    // ---------------- Вспомогательное ----------------

    private void Attach(DesignerNodeRow row) => row.PropertyChanged += OnRowChanged;

    private void Detach(DesignerNodeRow row) => row.PropertyChanged -= OnRowChanged;

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => NotifyTotals();

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalNodes));
        OnPropertyChanged(nameof(TotalCores));
        OnPropertyChanged(nameof(TotalRamGb));
        OnPropertyChanged(nameof(TotalDiskGb));
    }
}