using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using Xunit;

namespace ClusterSim.Tests.Features.Cluster;

/// <summary>
/// Проверки файловой реализации хранилища во временной папке.
/// </summary>
public class ClusterDefinitionStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly FileSystemClusterDefinitionStore _store;

    public ClusterDefinitionStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "clustersim-tests", Guid.NewGuid().ToString("N"));
        _store = new FileSystemClusterDefinitionStore(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static ClusterDefinition CreateDefinition(string name) => new()
    {
        Name = name,
        Description = "test cluster",
        Nodes =
        {
            new ClusterNodeDefinition("node-00", 4, 8192),
            new ClusterNodeDefinition("node-01", 8, 16384)
        }
    };

    [Fact]
    public void SaveAndLoad_RoundTripsDefinition()
    {
        var definition = CreateDefinition("round-trip");

        _store.Save(definition);
        var loaded = _store.Load(definition.Id);

        Assert.NotNull(loaded);
        Assert.Equal(definition.Id, loaded!.Id);
        Assert.Equal("round-trip", loaded.Name);
        Assert.Equal("test cluster", loaded.Description);
        Assert.Equal(2, loaded.Nodes.Count);
        Assert.Equal("node-00", loaded.Nodes[0].Name);
        Assert.Equal(4, loaded.Nodes[0].CpuCores);
        Assert.Equal(8192, loaded.Nodes[0].RamMb);
        Assert.Equal(8, loaded.Nodes[1].CpuCores);
        Assert.Equal(16384, loaded.Nodes[1].RamMb);
    }

    [Fact]
    public void Save_OverwritesPreviousVersion()
    {
        var definition = CreateDefinition("v1");
        _store.Save(definition);

        _store.Save(new ClusterDefinition
        {
            Id = definition.Id,
            Name = "v2",
            Nodes = { new ClusterNodeDefinition("solo", 2, 4096) }
        });

        var loaded = _store.Load(definition.Id);

        Assert.Equal("v2", loaded!.Name);
        Assert.Single(loaded.Nodes);
        Assert.Single(_store.ListAll());
    }

    [Fact]
    public void ListAll_ReturnsSavedDefinitions()
    {
        _store.Save(CreateDefinition("a"));
        _store.Save(CreateDefinition("b"));

        var names = _store.ListAll().Select(definition => definition.Name).ToList();

        Assert.Equal(2, names.Count);
        Assert.Contains("a", names);
        Assert.Contains("b", names);
    }

    [Fact]
    public void Delete_RemovesDefinition()
    {
        var definition = CreateDefinition("to-delete");
        _store.Save(definition);

        Assert.True(_store.Exists(definition.Id));
        Assert.True(_store.Delete(definition.Id));
        Assert.False(_store.Exists(definition.Id));
        Assert.Null(_store.Load(definition.Id));
        Assert.False(_store.Delete(definition.Id));
    }

    [Fact]
    public void Load_UnknownId_ReturnsNull()
    {
        Assert.Null(_store.Load(Guid.NewGuid()));
    }
}