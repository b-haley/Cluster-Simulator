using ClusterSim.Core.Features.Cluster;
using ClusterSim.Core.Features.ClusterNode;
using Xunit;
using Xunit.Abstractions;

namespace ClusterSim.Tests.Infrastructure.Persistence;

/// <summary>
/// Круговой цикл сохранения конфигураций кластеров в PostgreSQL.
/// Каждый тест использует уникальный Guid, чтобы не пересекаться с другими прогонами.
/// </summary>
public class PostgresClusterDefinitionStoreTests
{
    private readonly ITestOutputHelper _output;

    public PostgresClusterDefinitionStoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static ClusterDefinition CreateDefinition(string name) => new()
    {
        Name = name,
        Description = "integration test",
        Nodes =
        {
            new ClusterNodeDefinition("node-00", 4, 8192),
            new ClusterNodeDefinition("node-01", 8, 16384)
        }
    };

    [Fact]
    public void SaveAndLoad_RoundTripsDefinition()
    {
        if (!PostgresTestContext.TryCreateStore(out var store, out var skipReason))
        {
            _output.WriteLine(skipReason);
            return;
        }

        var definition = CreateDefinition($"round-trip-{Guid.NewGuid():N}");

        try
        {
            store!.Save(definition);
            var loaded = store.Load(definition.Id);

            Assert.NotNull(loaded);
            Assert.Equal(definition.Id, loaded!.Id);
            Assert.Equal(definition.Name, loaded.Name);
            Assert.Equal("integration test", loaded.Description);
            Assert.Equal(2, loaded.Nodes.Count);
            Assert.Equal("node-00", loaded.Nodes[0].Name);
            Assert.Equal(4, loaded.Nodes[0].CpuCores);
            Assert.Equal(8192, loaded.Nodes[0].RamMb);
            Assert.Equal("node-01", loaded.Nodes[1].Name);
            Assert.Equal(16384, loaded.Nodes[1].RamMb);
        }
        finally
        {
            store!.Delete(definition.Id);
        }
    }

    [Fact]
    public void Save_OverwritesPreviousVersion()
    {
        if (!PostgresTestContext.TryCreateStore(out var store, out var skipReason))
        {
            _output.WriteLine(skipReason);
            return;
        }

        var definition = CreateDefinition($"overwrite-{Guid.NewGuid():N}");

        try
        {
            store!.Save(definition);

            store.Save(new ClusterDefinition
            {
                Id = definition.Id,
                Name = definition.Name + "-v2",
                Nodes = { new ClusterNodeDefinition("solo", 2, 4096) }
            });

            var loaded = store.Load(definition.Id);

            Assert.Equal(definition.Name + "-v2", loaded!.Name);
            Assert.Single(loaded.Nodes);
            Assert.Equal("solo", loaded.Nodes[0].Name);
        }
        finally
        {
            store!.Delete(definition.Id);
        }
    }

    [Fact]
    public void Delete_RemovesDefinitionAndNodes()
    {
        if (!PostgresTestContext.TryCreateStore(out var store, out var skipReason))
        {
            _output.WriteLine(skipReason);
            return;
        }

        var definition = CreateDefinition($"delete-{Guid.NewGuid():N}");
        store!.Save(definition);

        Assert.True(store.Exists(definition.Id));
        Assert.True(store.Delete(definition.Id));
        Assert.False(store.Exists(definition.Id));
        Assert.Null(store.Load(definition.Id));
    }
}