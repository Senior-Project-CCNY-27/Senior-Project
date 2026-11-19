using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Tsitski.ProBuilderTools {
    internal static class SmoothingAngle {
        private const float SharedCornerResolution = 1000f;
        private const int SmoothingGroups = 24;

        public static void Apply(List<Vector3> positions, List<Face> faces, float angle) {
            if (angle <= 0f || faces.Count == 0)
                return;

            Vector3Int Corner(int index) {
                Vector3 p = positions[index] * SharedCornerResolution;
                return new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            }

            Vector3[] normals = new Vector3[faces.Count];
            int[] parent = Enumerable.Range(0, faces.Count).ToArray();

            int Find(int i) {
                while (parent[i] != i)
                    i = parent[i] = parent[parent[i]];
                return i;
            }

            Dictionary<(Vector3Int, Vector3Int), List<int>> byEdge = new();
            for (int f = 0; f < faces.Count; f++) {
                IList<int> indexes = faces[f].indexes;
                for (int t = 0; t + 2 < indexes.Count; t += 3) {
                    normals[f] += Vector3.Cross(positions[indexes[t + 1]] - positions[indexes[t]], positions[indexes[t + 2]] - positions[indexes[t]]);
                    for (int e = 0; e < 3; e++) {
                        Vector3Int a = Corner(indexes[t + e]), b = Corner(indexes[t + (e + 1) % 3]);
                        if (a == b)
                            continue;

                        (Vector3Int, Vector3Int) key = Before(a, b) ? (a, b) : (b, a);
                        if (!byEdge.TryGetValue(key, out List<int> sharing))
                            byEdge[key] = sharing = new List<int>();
                        if (!sharing.Contains(f))
                            sharing.Add(f);
                    }
                }
            }

            foreach (List<int> sharing in byEdge.Values)
                for (int a = 0; a < sharing.Count; a++)
                    for (int b = a + 1; b < sharing.Count; b++)
                        if (Vector3.Angle(normals[sharing[a]], normals[sharing[b]]) < angle)
                            parent[Find(sharing[a])] = Find(sharing[b]);

            Dictionary<Vector3Int, HashSet<int>> rootsByCorner = new();
            for (int f = 0; f < faces.Count; f++)
                foreach (int index in faces[f].distinctIndexes) {
                    Vector3Int corner = Corner(index);
                    if (!rootsByCorner.TryGetValue(corner, out HashSet<int> roots))
                        rootsByCorner[corner] = roots = new HashSet<int>();
                    roots.Add(Find(f));
                }

            Dictionary<int, int> groups = new();
            foreach (IGrouping<int, int> component in Enumerable.Range(0, faces.Count).GroupBy(Find)) {
                int group = 0;
                if (component.Count() > 1) {
                    HashSet<int> taken = component.SelectMany(f => faces[f].distinctIndexes)
                        .SelectMany(i => rootsByCorner[Corner(i)])
                        .Where(root => root != component.Key && groups.ContainsKey(root))
                        .Select(root => groups[root]).ToHashSet();
                    for (int candidate = 1; candidate <= SmoothingGroups; candidate++)
                        if (!taken.Contains(candidate)) {
                            group = candidate;
                            break;
                        }
                }

                groups[component.Key] = group;
                foreach (int f in component)
                    faces[f].smoothingGroup = group;
            }
        }

        private static bool Before(Vector3Int a, Vector3Int b) => a.x != b.x ? a.x < b.x : a.y != b.y ? a.y < b.y : a.z < b.z;
    }
}
