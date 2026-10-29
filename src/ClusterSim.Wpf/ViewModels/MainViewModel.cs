using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using ClusterSim.Core.Simulation;
using ClusterSim.Core.Simulation.Export;
using ClusterSim.Core.Simulation.Metrics;
using ClusterSim.Core.Simulation.Scheduling;
using ClusterSim.Core.Simulation.Workload;
using ClusterSim.Wpf.Services;
using ClusterSim.Wpf.Themes;

namespace ClusterSim.Wpf.ViewModels;

public sealed class WorkloadDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Новый workload";
    public string Description { get; init; } = "";
    public WorkloadOptions Options { get; init; } = new();
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SimulationRunner _runner;
    private readonly ReportExporter _exporter;
    private readonly IThemeService _themeService;
    private readonly ClusterDesignerService _clusterService;
    private IReadOnlyList<RunResult>? _lastResults;

    public MainViewModel(SimulationRunner runner, ReportExporter exporter, ClusterDesignerViewModel designer, IThemeService themeService, ClusterDesignerService clusterService)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        Designer = designer ?? throw new ArgumentNullException(nameof(designer));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _clusterService = clusterService ?? throw new ArgumentNullException(nameof(clusterService));

        _selectedTheme = themeService.Current;
        AvailableClusters = new ObservableCollection<ClusterDefinition>(_clusterService.ListSaved());
        AvailableWorkloads = new ObservableCollection<WorkloadDefinition>();
        SelectedClusters = new ObservableCollection<ClusterDefinition>();
        RunResults = new ObservableCollection<RunResult>();
        Comparison = new ObservableCollection<ComparisonRow>();
        Nodes = new ObservableCollection<NodeSnapshot>();
        AllJobs = new ObservableCollection<JobSnapshot>();
        TimeSeries = new ObservableCollection<UtilizationPoint>();

        LoadWorkloads();
    }

    private void LoadWorkloads()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClusterSim", "workloads.json");
        if (File.Exists(path))
        {
            try
            {
                var definitions = JsonSerializer.Deserialize<WorkloadDefinition[]>(File.ReadAllText(path));
                if (definitions != null) foreach (var def in definitions) AvailableWorkloads.Add(def);
            }
            catch { }
        }
    }

    private void SaveWorkloads()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClusterSim", "workloads.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(AvailableWorkloads.ToArray(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public ClusterDesignerViewModel Designer { get; }
    public IReadOnlyList<SchedulingPolicyKind> AvailablePolicies { get; } = Enum.GetValues<SchedulingPolicyKind>();
    public IReadOnlyList<ThemeKind> AvailableThemes { get; } = Enum.GetValues<ThemeKind>();

    public ObservableCollection<ClusterDefinition> AvailableClusters { get; }
    public ObservableCollection<WorkloadDefinition> AvailableWorkloads { get; }
    public ObservableCollection<ClusterDefinition> SelectedClusters { get; }
    public ObservableCollection<RunResult> RunResults { get; }
    public ObservableCollection<ComparisonRow> Comparison { get; }
    public ObservableCollection<NodeSnapshot> Nodes { get; }
    public ObservableCollection<JobSnapshot> AllJobs { get; }
    public ObservableCollection<UtilizationPoint> TimeSeries { get; }

    [ObservableProperty] private ThemeKind _selectedTheme;
    partial void OnSelectedThemeChanged(ThemeKind value) => _themeService.Apply(value);

    // --- Конструктор кластера ---
    [ObservableProperty] private ClusterDefinition? _selectedCluster;
    [ObservableProperty] private string _clusterStatusMessage = "";

    [RelayCommand]
    private void SaveCluster()
    {
        try
        {
            var def = Designer.BuildDefinition();
            _clusterService.Save(def);
            AvailableClusters.Add(def);
            ClusterStatusMessage = $"Кластер \"{def.Name}\" сохранен.";
        }
        catch (Exception ex) { ClusterStatusMessage = $"Ошибка: {ex.Message}"; }
    }

    [RelayCommand]
    private void DeleteSelectedCluster()
    {
        if (SelectedCluster is null) return;
        _clusterService.Delete(SelectedCluster.Id);
        AvailableClusters.Remove(SelectedCluster);
        SelectedClusters.Remove(SelectedCluster);
        ClusterStatusMessage = $"Кластер \"{SelectedCluster.Name}\" удален.";
    }

    // --- Конструктор заданий ---
    [ObservableProperty] private WorkloadDefinition? _selectedWorkload;
    [ObservableProperty] private string _workloadName = "Новый workload";
    [ObservableProperty] private string _workloadDescription = "";
    [ObservableProperty] private int _jobCount = 100;
    [ObservableProperty] private int _seed = 42;
    [ObservableProperty] private double _arrivalRatePerHour = 60;
    [ObservableProperty] private double _minDurationSeconds = 60;
    [ObservableProperty] private double _maxDurationSeconds = 3600;
    [ObservableProperty] private int _minCpuCores = 1;
    [ObservableProperty] private int _maxCpuCores = 4;
    [ObservableProperty] private int _minRamMb = 512;
    [ObservableProperty] private int _maxRamMb = 4096;
    [ObservableProperty] private double _parallelJobProbability;
    [ObservableProperty] private double _estimationError;
    [ObservableProperty] private string _workloadStatusMessage = "";

    [RelayCommand]
    private void SaveWorkload()
    {
        var def = new WorkloadDefinition
        {
            Name = WorkloadName,
            Description = WorkloadDescription,
            Options = new WorkloadOptions
            {
                JobCount = JobCount,
                Seed = Seed,
                ArrivalRatePerHour = ArrivalRatePerHour,
                MinDurationSeconds = MinDurationSeconds,
                MaxDurationSeconds = MaxDurationSeconds,
                MinCpuCores = MinCpuCores,
                MaxCpuCores = MaxCpuCores,
                MinRamMb = MinRamMb,
                MaxRamMb = MaxRamMb,
                ParallelJobProbability = ParallelJobProbability,
                EstimationError = EstimationError
            }
        };
        AvailableWorkloads.Add(def);
        SaveWorkloads();
        WorkloadStatusMessage = $"Workload \"{def.Name}\" сохранен.";
    }

    [RelayCommand]
    private void DeleteSelectedWorkload()
    {
        if (SelectedWorkload is null) return;
        AvailableWorkloads.Remove(SelectedWorkload);
        SaveWorkloads();
        WorkloadStatusMessage = $"Workload \"{SelectedWorkload.Name}\" удален.";
    }

    [RelayCommand]
    private void LoadWorkload()
    {
        if (SelectedWorkload is null) return;
        WorkloadName = SelectedWorkload.Name; WorkloadDescription = SelectedWorkload.Description;
        JobCount = SelectedWorkload.Options.JobCount; Seed = SelectedWorkload.Options.Seed;
        ArrivalRatePerHour = SelectedWorkload.Options.ArrivalRatePerHour;
        MinDurationSeconds = SelectedWorkload.Options.MinDurationSeconds; MaxDurationSeconds = SelectedWorkload.Options.MaxDurationSeconds;
        MinCpuCores = SelectedWorkload.Options.MinCpuCores; MaxCpuCores = SelectedWorkload.Options.MaxCpuCores;
        MinRamMb = SelectedWorkload.Options.MinRamMb; MaxRamMb = SelectedWorkload.Options.MaxRamMb;
        ParallelJobProbability = SelectedWorkload.Options.ParallelJobProbability; EstimationError = SelectedWorkload.Options.EstimationError;
        WorkloadStatusMessage = $"Workload \"{SelectedWorkload.Name}\" загружен.";
    }

    // --- Симуляция ---
    [ObservableProperty] private SchedulingPolicyKind _selectedPolicy = SchedulingPolicyKind.Fifo;
    [ObservableProperty] private bool _strictFifo = true;
    [ObservableProperty] private bool _allowPreemption;
    [ObservableProperty] private double _checkpointOverheadSeconds;
    [ObservableProperty] private double _minRunTimeBeforePreemption = 60;
    [ObservableProperty] private double _timeSeriesStep = 60;
    [ObservableProperty] private bool _compareAllPolicies;
    [ObservableProperty] private string _outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClusterSim", "reports");
    [ObservableProperty] private string _statusMessage = "Готов к работе.";
    [ObservableProperty] private bool _isRunning;

    [ObservableProperty] private double _currentTime;
    [ObservableProperty] private double _makespan;
    [ObservableProperty] private double _averageWaitTime;
    [ObservableProperty] private double _averageTurnaround;
    [ObservableProperty] private double _averageSlowdown;
    [ObservableProperty] private double _cpuUtilization;
    [ObservableProperty] private double _ramUtilization;
    [ObservableProperty] private double _throughputPerHour;
    [ObservableProperty] private int _totalJobs;
    [ObservableProperty] private int _completedJobs;
    [ObservableProperty] private int _unfinishedJobs;
    [ObservableProperty] private int _rejectedJobs;
    [ObservableProperty] private int _preemptionCount;
    [ObservableProperty] private double _averageQueueLength;
    [ObservableProperty] private int _maxQueueLength;

    public bool HasResults => RunResults.Count > 0;
    public bool HasComparison => Comparison.Count > 0;

    [RelayCommand]
    private async Task RunSimulationAsync()
    {
        if (IsRunning) return;
        if (SelectedClusters.Count == 0) { StatusMessage = "Выберите хотя бы один кластер."; return; }
        if (AvailableWorkloads.Count == 0) { StatusMessage = "Создайте хотя бы один workload."; return; }

        IsRunning = true; StatusMessage = "Симуляция выполняется...";
        try
        {
            var results = new System.Collections.Generic.List<RunResult>();
            var workload = AvailableWorkloads.FirstOrDefault()!;

            foreach (var cluster in SelectedClusters)
            {
                var options = BuildOptions(cluster, workload.Options);
                if (CompareAllPolicies)
                {
                    var optsList = AvailablePolicies.Select(p => BuildOptions(cluster, workload.Options, p)).ToList();
                    results.AddRange(await _runner.RunComparisonAsync(optsList));
                }
                else
                {
                    results.Add(await _runner.RunSingleAsync(options));
                }
            }

            _lastResults = results;
            RunResults.Clear();
            foreach (var r in results) RunResults.Add(r);

            if (results.Count > 0) FillDetails(results.First());
            if (CompareAllPolicies && results.Count > 1) FillComparison(results);
            else Comparison.Clear();

            StatusMessage = $"Симуляция завершена: прогонов — {results.Count}.";
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(HasComparison));
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
        finally { IsRunning = false; }
    }

    private SimulationOptions BuildOptions(ClusterDefinition cluster, WorkloadOptions workload, SchedulingPolicyKind? policy = null) => new()
    {
        Cluster = new ClusterOptions { CustomNodes = cluster.Nodes },
        Workload = workload,
        Policy = new PolicyOptions { Kind = policy ?? SelectedPolicy, StrictFifo = StrictFifo, AllowPreemption = AllowPreemption, CheckpointOverheadSeconds = CheckpointOverheadSeconds, MinRunTimeBeforePreemption = MinRunTimeBeforePreemption },
        TimeSeriesStep = TimeSeriesStep
    };

    [RelayCommand]
    private void ExportResults()
    {
        if (_lastResults is null || _lastResults.Count == 0) { StatusMessage = "Экспортировать нечего."; return; }
        try
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (var r in _lastResults) paths.AddRange(_exporter.ExportAll(r.Metrics, r.Options, OutputDirectory));
            if (_lastResults.Count > 1) paths.Add(_exporter.ExportComparison(_lastResults.Select(r => r.Metrics).ToList(), OutputDirectory));
            StatusMessage = $"Экспортировано файлов: {paths.Count}.";
        }
        catch (Exception ex) { StatusMessage = $"Ошибка экспорта: {ex.Message}"; }
    }

    private void FillDetails(RunResult result)
    {
        var s = result.FinalSnapshot;
        Nodes.Clear(); foreach (var n in s.Nodes) Nodes.Add(n);
        AllJobs.Clear(); foreach (var j in s.PendingJobs.Concat(s.RunningJobs).Concat(s.FinishedJobs).OrderBy(j => j.Id)) AllJobs.Add(j);
        TimeSeries.Clear(); foreach (var p in result.Metrics.TimeSeries) TimeSeries.Add(p);

        var m = s.Metrics;
        CurrentTime = m.CurrentTime; Makespan = m.Makespan; AverageWaitTime = m.AverageWaitTime;
        AverageTurnaround = m.AverageTurnaround; AverageSlowdown = m.AverageSlowdown;
        CpuUtilization = m.CpuUtilization; RamUtilization = m.RamUtilization; ThroughputPerHour = m.ThroughputPerHour;
        TotalJobs = m.TotalJobs; CompletedJobs = m.CompletedJobs; UnfinishedJobs = m.UnfinishedJobs;
        RejectedJobs = m.RejectedJobs; PreemptionCount = m.PreemptionCount; AverageQueueLength = m.AverageQueueLength; MaxQueueLength = m.MaxQueueLength;
    }

    private void FillComparison(System.Collections.Generic.IReadOnlyList<RunResult> results)
    {
        Comparison.Clear();
        foreach (var r in results)
        {
            var m = r.Metrics;
            Comparison.Add(new ComparisonRow(m.PolicyName, m.Makespan, m.AverageWaitTime, m.AverageTurnaround, m.AverageSlowdown, m.CpuUtilization, m.RamUtilization, m.ThroughputPerHour, m.CompletedJobs, m.PreemptionCount));
        }
    }
}