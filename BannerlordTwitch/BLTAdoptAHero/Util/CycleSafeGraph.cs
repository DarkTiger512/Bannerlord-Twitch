using System;
using System.Collections.Generic;
using System.Linq;

namespace BLTAdoptAHero.Util
{
    public static class CycleSafeGraph
    {
        public static IReadOnlyList<T> FindTerminals<T>(T root, Func<T, IEnumerable<T>> getChildren,
            IEqualityComparer<T> comparer = null)
        {
            var visited = new HashSet<T>(comparer ?? EqualityComparer<T>.Default);
            var pending = new Stack<T>();
            var terminals = new List<T>();
            if (root != null) pending.Push(root);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!visited.Add(node)) continue;
                var children = (getChildren(node) ?? Array.Empty<T>()).Where(t => t != null).Distinct().ToList();
                if (children.Count == 0) terminals.Add(node);
                // Preserve upgrade order without recursion or repeatedly traversing convergent paths.
                for (int i = children.Count - 1; i >= 0; i--) pending.Push(children[i]);
            }
            return terminals;
        }
    }
}
