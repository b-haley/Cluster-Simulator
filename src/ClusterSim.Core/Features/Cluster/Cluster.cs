using ClusterSim.Core.Features.Resources;

namespace ClusterSim.Core.Features.Cluster
{
    using ClusterNode = ClusterSim.Core.Features.ClusterNode.ClusterNode;
    using Job = ClusterSim.Core.Features.Job.Job;

    /// <summary>
    /// Кластер как набор узлов и суммарные ресурсы.
    /// Не принимает решений о планировании: только размещение и освобождение.
    /// </summary>
    public sealed class Cluster
    {
        private readonly List<ClusterNode> _nodes;
        private readonly Dictionary<int, ClusterNode> _nodesById;

        public Cluster(IEnumerable<ClusterNode> nodes)
        {
            ArgumentNullException.ThrowIfNull(nodes);

            _nodes = nodes.ToList();

            if (_nodes.Count == 0)
            {
                throw new ArgumentException("Кластер должен содержать хотя бы один узел.", nameof(nodes));
            }

            if (_nodes.Select(n => n.Id).Distinct().Count() != _nodes.Count)
            {
                throw new ArgumentException("Идентификаторы узлов должны быть уникальными.", nameof(nodes));
            }

            _nodesById = _nodes.ToDictionary(n => n.Id);
            TotalCapacity = _nodes.Aggregate(ResourceVector.Zero, (acc, n) => acc.Add(n.TotalCapacity));
            LargestNodeCapacity = new ResourceVector(
                _nodes.Max(n => n.TotalCapacity.CpuCores),
                _nodes.Max(n => n.TotalCapacity.RamMb));
        }

        /// <summary>Все узлы кластера.</summary>
        public IReadOnlyList<ClusterNode> Nodes => _nodes;

        /// <summary>Количество узлов.</summary>
        public int NodeCount => _nodes.Count;

        /// <summary>Суммарная емкость всех узлов.</summary>
        public ResourceVector TotalCapacity { get; }

        /// <summary>Помоузловой максимум ресурсов: предел для одного узла задания.</summary>
        public ResourceVector LargestNodeCapacity { get; }

        /// <summary>Суммарные свободные ресурсы онлайн-узлов.</summary>
        public ResourceVector TotalAvailable => _nodes
            .Where(n => n.IsOnline)
            .Aggregate(ResourceVector.Zero, (acc, n) => acc.Add(n.AvailableResources));

        /// <summary>Возвращает узел по идентификатору.</summary>
        public ClusterNode GetNode(int id) =>
            _nodesById.TryGetValue(id, out var node)
                ? node
                : throw new KeyNotFoundException($"Узел {id} не найден в кластере.");

        /// <summary>Онлайн-узлы кластера.</summary>
        public IReadOnlyList<ClusterNode> GetOnlineNodes() => _nodes.Where(n => n.IsOnline).ToList();

        /// <summary>Проверяет, можно ли разместить задание на указанном наборе узлов.</summary>
        public bool CanPlace(Job job, IReadOnlyList<int> nodeIds)
        {
            ArgumentNullException.ThrowIfNull(job);
            ArgumentNullException.ThrowIfNull(nodeIds);

            if (nodeIds.Count != job.RequiredNodeCount)
            {
                return false;
            }

            if (nodeIds.Distinct().Count() != nodeIds.Count)
            {
                return false;
            }

            foreach (var id in nodeIds)
            {
                if (!_nodesById.TryGetValue(id, out var node) || !node.CanAccept(job.RequiredResources))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Выделяет ресурсы задания на указанных узлах (все или ничего).</summary>
        public void Allocate(Job job, IReadOnlyList<int> nodeIds)
        {
            if (!CanPlace(job, nodeIds))
            {
                throw new InvalidOperationException(
                    $"Нельзя разместить задание {job.Id} на узлах [{string.Join(", ", nodeIds)}].");
            }

            foreach (var id in nodeIds)
            {
                _nodesById[id].Allocate(job.RequiredResources, job.Id);
            }
        }

        /// <summary>Освобождает ресурсы задания на указанных узлах.</summary>
        public void Release(Job job, IReadOnlyList<int> nodeIds)
        {
            ArgumentNullException.ThrowIfNull(job);
            ArgumentNullException.ThrowIfNull(nodeIds);

            foreach (var id in nodeIds)
            {
                _nodesById[id].Release(job.RequiredResources, job.Id);
            }
        }
    }
}