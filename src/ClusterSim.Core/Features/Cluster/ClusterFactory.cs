namespace ClusterSim.Core.Features.Cluster
{
    using ClusterNode = ClusterSim.Core.Features.ClusterNode.ClusterNode;
    using ClusterNodeDefinition = ClusterSim.Core.Features.ClusterNode.ClusterNodeDefinition;

    /// <summary>
    /// Фабрика рантайм-кластеров из опций или сохраненных определений.
    /// </summary>
    public static class ClusterFactory
    {
        /// <summary>Создает кластер из опций: пользовательские узлы или однородный шаблон.</summary>
        public static Cluster Create(ClusterOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (options.CustomNodes is { Count: > 0 } customNodes)
            {
                return new Cluster(customNodes.Select((definition, index) =>
                    new ClusterNode(index, definition.Name, definition.ToResourceVector())));
            }

            if (options.NodeCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), "Количество узлов должно быть положительным.");
            }

            return new Cluster(Enumerable.Range(0, options.NodeCount).Select(index =>
                new ClusterNode(
                    index,
                    $"node-{index:D2}",
                    new Resources.ResourceVector(options.DefaultNode.CpuCores, options.DefaultNode.RamMb))));
        }

        /// <summary>Создает кластер из сохраненного пользовательского определения.</summary>
        public static Cluster Create(ClusterDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (definition.Nodes.Count == 0)
            {
                throw new ArgumentException("Определение кластера не содержит узлов.", nameof(definition));
            }

            return new Cluster(definition.Nodes.Select((node, index) =>
                new ClusterNode(index, node.Name, node.ToResourceVector())));
        }

        /// <summary>Преобразует опции кластера в сохраняемое определение.</summary>
        public static ClusterDefinition ToDefinition(ClusterOptions options, string name, string description = "")
        {
            ArgumentNullException.ThrowIfNull(options);

            var nodes = options.CustomNodes is { Count: > 0 } customNodes
                ? customNodes.ToList()
                : Enumerable.Range(0, Math.Max(1, options.NodeCount))
                    .Select(index => new ClusterNodeDefinition(
                        $"node-{index:D2}",
                        options.DefaultNode.CpuCores,
                        options.DefaultNode.RamMb))
                    .ToList();

            return new ClusterDefinition
            {
                Name = name,
                Description = description,
                Nodes = nodes
            };
        }
    }
}