using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Ordering;

public sealed class ComponentOrdering(WiringEngine wiringEngine)
{
    public ComponentOrderingResult Order(ModelSnapshot snapshot, CatalogVersion catalog)
    {
        var wiring = wiringEngine.Evaluate(snapshot, catalog);
        var components = snapshot.Components.ToDictionary(component => component.Id);
        var dependencies = wiring.Dependencies
            .Where(dependency => components.ContainsKey(dependency.SourceComponentId)
                && components.ContainsKey(dependency.TargetComponentId)
                && dependency.SourceComponentId != dependency.TargetComponentId)
            .DistinctBy(dependency => dependency.LinkId)
            .ToArray();
        var cycles = FindCycles(components, dependencies);
        var dependencyPairs = dependencies
            .Select(dependency => (ProducerId: dependency.TargetComponentId, ConsumerId: dependency.SourceComponentId))
            .Distinct()
            .ToArray();
        var cycleMembers = cycles.SelectMany(cycle => cycle.ComponentIds).ToHashSet();
        var groupsByRepresentative = cycles.ToDictionary(
            cycle => cycle.ComponentIds[0],
            cycle => cycle.ComponentIds.Select(id => components[id])
                .OrderBy(component => component.Code, StringComparer.Ordinal)
                .ThenBy(component => component.Id)
                .ToArray());
        foreach (var component in components.Values.Where(component => !cycleMembers.Contains(component.Id)))
        {
            groupsByRepresentative.Add(component.Id, [component]);
        }

        var groupByComponentId = groupsByRepresentative
            .SelectMany(group => group.Value.Select(component => (ComponentId: component.Id, GroupId: group.Key)))
            .ToDictionary(item => item.ComponentId, item => item.GroupId);
        var groupDependencyPairs = dependencyPairs
            .Select(pair => (ProducerId: groupByComponentId[pair.ProducerId], ConsumerId: groupByComponentId[pair.ConsumerId]))
            .Where(pair => pair.ProducerId != pair.ConsumerId)
            .Distinct()
            .ToArray();
        var inDegree = groupsByRepresentative.Keys.ToDictionary(id => id, _ => 0);
        var dependents = groupsByRepresentative.Keys.ToDictionary(id => id, _ => new List<Guid>());
        foreach (var pair in groupDependencyPairs)
        {
            inDegree[pair.ConsumerId]++;
            dependents[pair.ProducerId].Add(pair.ConsumerId);
        }

        var ready = new SortedSet<ComponentKey>(ComponentKeyComparer.Instance);
        foreach (var (componentId, degree) in inDegree)
        {
            if (degree == 0)
            {
                ready.Add(new ComponentKey(groupsByRepresentative[componentId][0].Code, componentId));
            }
        }

        var orderedGroups = new List<Guid>(groupsByRepresentative.Count);
        while (ready.Count > 0)
        {
            var next = ready.Min!;
            ready.Remove(next);
            orderedGroups.Add(next.Id);

            foreach (var dependentId in dependents[next.Id]
                         .OrderBy(id => groupsByRepresentative[id][0].Code, StringComparer.Ordinal)
                         .ThenBy(id => id))
            {
                inDegree[dependentId]--;
                if (inDegree[dependentId] == 0)
                {
                    ready.Add(new ComponentKey(groupsByRepresentative[dependentId][0].Code, dependentId));
                }
            }
        }

        var ordered = orderedGroups.SelectMany(groupId => groupsByRepresentative[groupId]).ToArray();

        return new ComponentOrderingResult(ordered, cycles, wiring);
    }

    private static ComponentDependencyCycle[] FindCycles(
        IReadOnlyDictionary<Guid, ComponentSnapshot> components,
        IReadOnlyList<WiringDependency> dependencies)
    {
        var targetsBySource = components.Keys.ToDictionary(
            id => id,
            id => dependencies.Where(dependency => dependency.SourceComponentId == id)
                .Select(dependency => dependency.TargetComponentId)
                .Distinct()
                .OrderBy(targetId => components[targetId].Code, StringComparer.Ordinal)
                .ThenBy(targetId => targetId)
                .ToArray());
        var nextIndex = 0;
        var indices = new Dictionary<Guid, int>();
        var lowLinks = new Dictionary<Guid, int>();
        var stack = new Stack<Guid>();
        var onStack = new HashSet<Guid>();
        var componentSets = new List<Guid[]>();

        void Visit(Guid componentId)
        {
            indices[componentId] = nextIndex;
            lowLinks[componentId] = nextIndex;
            nextIndex++;
            stack.Push(componentId);
            onStack.Add(componentId);

            foreach (var targetId in targetsBySource[componentId])
            {
                if (!indices.TryGetValue(targetId, out var targetIndex))
                {
                    Visit(targetId);
                    lowLinks[componentId] = Math.Min(lowLinks[componentId], lowLinks[targetId]);
                }
                else if (onStack.Contains(targetId))
                {
                    lowLinks[componentId] = Math.Min(lowLinks[componentId], targetIndex);
                }
            }

            if (lowLinks[componentId] != indices[componentId])
            {
                return;
            }

            var members = new List<Guid>();
            Guid member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                members.Add(member);
            }
            while (member != componentId);

            if (members.Count > 1 || targetsBySource[componentId].Contains(componentId))
            {
                componentSets.Add(members.ToArray());
            }
        }

        foreach (var componentId in components.Keys
                     .OrderBy(id => components[id].Code, StringComparer.Ordinal)
                     .ThenBy(id => id))
        {
            if (!indices.ContainsKey(componentId))
            {
                Visit(componentId);
            }
        }

        return componentSets
            .Select(memberIds =>
            {
                var members = memberIds.ToHashSet();
                var orderedIds = memberIds
                    .OrderBy(id => components[id].Code, StringComparer.Ordinal)
                    .ThenBy(id => id)
                    .ToArray();
                var cycleDependencies = dependencies
                    .Where(dependency => members.Contains(dependency.SourceComponentId)
                        && members.Contains(dependency.TargetComponentId))
                    .OrderBy(dependency => components[dependency.SourceComponentId].Code, StringComparer.Ordinal)
                    .ThenBy(dependency => components[dependency.TargetComponentId].Code, StringComparer.Ordinal)
                    .ThenBy(dependency => dependency.LinkId)
                    .ToArray();
                return new ComponentDependencyCycle(orderedIds, cycleDependencies);
            })
            .OrderBy(cycle => components[cycle.ComponentIds[0]].Code, StringComparer.Ordinal)
            .ThenBy(cycle => cycle.ComponentIds[0])
            .ToArray();
    }

    private sealed record ComponentKey(string Code, Guid Id);

    private sealed class ComponentKeyComparer : IComparer<ComponentKey>
    {
        public static ComponentKeyComparer Instance { get; } = new();

        public int Compare(ComponentKey? left, ComponentKey? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var codeOrder = StringComparer.Ordinal.Compare(left.Code, right.Code);
            return codeOrder != 0 ? codeOrder : left.Id.CompareTo(right.Id);
        }
    }
}
