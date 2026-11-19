using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using EditorUtility = UnityEditor.EditorUtility;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    public static class TsitskiProBuilderTools {
        const string MenuRoot = "Tools/tsitski/ProBuilder/";
        const float PositionTolerance = 0.00001f;
        const float CollinearTolerance = 0.0001f;
        const float CoplanarDot = 0.9998f;

        [MenuItem(MenuRoot + "Modelling/Collapse Edges or Vertices &DEL")]
        static void CollapseSelection() {
            SelectMode mode = ProBuilderEditor.selectMode;
            if (mode != SelectMode.Edge && mode != SelectMode.Vertex && mode != SelectMode.Face) {
                Debug.Log("[tsitski ProBuilder Tools] Collapse works in Face, Edge or Vertex mode.");
                return;
            }

            foreach (ProBuilderMesh mesh in MeshSelection.top.ToArray()) {
                bool hasSelection = mode switch {
                    SelectMode.Edge => mesh.selectedEdges.Count > 0,
                    SelectMode.Face => mesh.selectedFaceCount > 0,
                    _ => mesh.selectedVertices.Count > 0
                };
                if (!hasSelection)
                    continue;

                Undo.RecordObject(mesh, "Collapse Edges or Vertices");

                List<Vector3> candidates = mode switch {
                    SelectMode.Edge => MergeAcrossEdges(mesh),
                    SelectMode.Face => MergeAroundVertices(mesh, mesh.GetSelectedFaces().SelectMany(f => f.distinctIndexes)),
                    _ => MergeAroundVertices(mesh, mesh.selectedVertices)
                };
                DissolveVertices(mesh, candidates);

                mesh.ClearSelection();
                mesh.ToMesh();
                mesh.Refresh();
                mesh.Optimize();
                EditorUtility.SetDirty(mesh);
            }

            ProBuilderEditor.Refresh();
        }

        [MenuItem(MenuRoot + "Modelling/Bridge Faces")]
        static void BridgeFaces() {
            ProBuilderMesh[] meshes = MeshSelection.top.Where(m => m.selectedFaceCount > 0).ToArray();
            if (meshes.Length != 1 || meshes[0].selectedFaceCount < 2) {
                Debug.Log("[tsitski ProBuilder Tools] Bridge Faces needs two faces, or two separate groups of faces, selected on the same mesh.");
                return;
            }

            ProBuilderMesh mesh = meshes[0];
            Face[] selected = mesh.GetSelectedFaces();
            List<List<Face>> islands = FaceIslands(mesh, selected);

            if (islands.Count != 2) {
                Debug.LogWarning($"[tsitski ProBuilder Tools] Bridge Faces needs exactly two separate groups of faces, found {islands.Count}.", mesh);
                return;
            }

            Face first = islands[0][0];
            List<int> loopA = IslandLoop(mesh, islands[0]), loopB = IslandLoop(mesh, islands[1]);

            if (loopA == null || loopB == null) {
                Debug.LogWarning("[tsitski ProBuilder Tools] Bridge Faces needs each group of faces to have a single outline, with no holes.", mesh);
                return;
            }

            if (loopA.Count != loopB.Count) {
                Debug.LogWarning($"[tsitski ProBuilder Tools] Bridge Faces needs faces with the same number of edges ({loopA.Count} and {loopB.Count}).", mesh);
                return;
            }

            if (loopA.Intersect(loopB).Any()) {
                Debug.LogWarning("[tsitski ProBuilder Tools] Bridge Faces can't bridge faces that share vertices.", mesh);
                return;
            }

            Undo.RecordObject(mesh, "Bridge Faces");

            if (mesh.colors == null || mesh.colors.Count != mesh.vertexCount)
                mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
            if (mesh.textures == null || mesh.textures.Count != mesh.vertexCount)
                mesh.textures = new Vector2[mesh.vertexCount];

            List<Vector3> a = loopA.Select(h => HandlePosition(mesh, h)).ToList();
            List<int> matched = MatchLoop(a, loopB.Select(h => HandlePosition(mesh, h)).ToList(), loopB);
            List<Vector3> b = matched.Select(h => HandlePosition(mesh, h)).ToList();

            Vector3 centerA = Centroid(a), centerB = Centroid(b);
            Vector3 normalA = islands[0].Aggregate(Vector3.zero, (sum, f) => sum + UnityEngine.ProBuilder.Math.Normal(mesh, f));
            Vector3 normalB = islands[1].Aggregate(Vector3.zero, (sum, f) => sum + UnityEngine.ProBuilder.Math.Normal(mesh, f));
            bool tunnel = Vector3.Dot(normalA, centerB - centerA) - Vector3.Dot(normalB, centerB - centerA) < 0f;
            int count = loopA.Count;
            Vector3[][] positions = new Vector3[count][];
            Color[][] colors = new Color[count][];
            Vector2[][] uvs = new Vector2[count][];
            Face[] sides = new Face[count];
            int[][] shared = new int[count][];

            for (int i = 0; i < count; i++) {
                int next = (i + 1) % count;
                positions[i] = new[] { a[i], a[next], b[next], b[i] };
                colors[i] = Enumerable.Repeat(Color.white, 4).ToArray();
                uvs[i] = new Vector2[4];
                shared[i] = new[] { loopA[i], loopA[next], matched[next], matched[i] };
                sides[i] = new Face(new[] { 0, 1, 2, 0, 2, 3 }) {
                    submeshIndex = first.submeshIndex,
                    smoothingGroup = first.smoothingGroup,
                    uv = first.uv,
                    textureGroup = -1,
                    manualUV = false
                };
            }

            Face[] created = mesh.AppendFaces(positions, colors, uvs, sides, shared);
            if (created == null) {
                Debug.LogWarning("[tsitski ProBuilder Tools] Bridge Faces failed to add the new faces.", mesh);
                return;
            }

            foreach (Face face in created) {
                Vector3 center = Centroid(face.distinctIndexes.Select(i => mesh.positions[i]).ToList());
                Vector3 axis = centerB - centerA;
                float along = axis.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector3.Dot(center - centerA, axis) / axis.sqrMagnitude) : 0.5f;
                Vector3 outward = center - (centerA + axis * along);
                if (tunnel)
                    outward = -outward;
                if (Vector3.Dot(UnityEngine.ProBuilder.Math.Normal(mesh, face), outward) < 0f)
                    face.Reverse();
            }

            mesh.DeleteFaces(selected);
            mesh.ToMesh();
            mesh.Refresh();
            mesh.SetSelectedFaces(created);
            mesh.Optimize();
            EditorUtility.SetDirty(mesh);
            ProBuilderEditor.Refresh();
        }

        [MenuItem(MenuRoot + "Modelling/Bridge Faces", true)]
        static bool BridgeFacesValidate() =>
            ProBuilderEditor.instance != null && MeshSelection.top.Any(m => m.selectedFaceCount >= 2);

        static List<List<Face>> FaceIslands(ProBuilderMesh mesh, IList<Face> faces) {
            Dictionary<int, int> lookup = Lookup(mesh);
            Dictionary<(int, int), List<int>> edgeFaces = new();

            for (int i = 0; i < faces.Count; i++) {
                foreach (Edge edge in faces[i].edges) {
                    (int, int) key = SortedPair(lookup[edge.a], lookup[edge.b]);
                    if (!edgeFaces.TryGetValue(key, out List<int> list))
                        edgeFaces[key] = list = new List<int>();
                    list.Add(i);
                }
            }

            int[] island = Enumerable.Repeat(-1, faces.Count).ToArray();
            List<List<Face>> islands = new();

            for (int start = 0; start < faces.Count; start++) {
                if (island[start] >= 0)
                    continue;

                List<Face> members = new();
                Stack<int> open = new();
                open.Push(start);
                island[start] = islands.Count;

                while (open.Count > 0) {
                    int current = open.Pop();
                    members.Add(faces[current]);

                    foreach (Edge edge in faces[current].edges) {
                        foreach (int other in edgeFaces[SortedPair(lookup[edge.a], lookup[edge.b])]) {
                            if (island[other] >= 0)
                                continue;

                            island[other] = islands.Count;
                            open.Push(other);
                        }
                    }
                }

                islands.Add(members);
            }

            return islands;
        }

        static List<int> IslandLoop(ProBuilderMesh mesh, List<Face> island) {
            Dictionary<int, int> lookup = Lookup(mesh);
            Dictionary<(int, int), int> uses = new();

            foreach (Face face in island) {
                foreach (Edge edge in face.edges) {
                    (int, int) key = SortedPair(lookup[edge.a], lookup[edge.b]);
                    uses[key] = uses.TryGetValue(key, out int count) ? count + 1 : 1;
                }
            }

            Dictionary<int, List<int>> neighbours = new();

            foreach (((int a, int b), int count) in uses) {
                if (count != 1)
                    continue;

                if (!neighbours.TryGetValue(a, out List<int> na))
                    neighbours[a] = na = new List<int>();
                if (!neighbours.TryGetValue(b, out List<int> nb))
                    neighbours[b] = nb = new List<int>();
                na.Add(b);
                nb.Add(a);
            }

            if (neighbours.Count < 3 || neighbours.Values.Any(n => n.Count != 2))
                return null;

            int start = neighbours.Keys.First();
            List<int> loop = new() { start };
            int previous = start, current = neighbours[start][0];

            while (current != start) {
                loop.Add(current);
                int next = neighbours[current][0] == previous ? neighbours[current][1] : neighbours[current][0];
                previous = current;
                current = next;

                if (loop.Count > neighbours.Count)
                    return null;
            }

            return loop.Count == neighbours.Count ? loop : null;
        }

        static List<int> MatchLoop(List<Vector3> a, List<Vector3> b, List<int> handles) {
            Vector3 centerA = Centroid(a), centerB = Centroid(b);
            int count = a.Count;
            float best = float.MaxValue;
            List<int> result = handles;

            foreach (bool reversed in new[] { false, true }) {
                for (int offset = 0; offset < count; offset++) {
                    float cost = 0f;
                    for (int i = 0; i < count; i++) {
                        int j = reversed ? (offset - i + count) % count : (offset + i) % count;
                        cost += ((a[i] - centerA) - (b[j] - centerB)).sqrMagnitude;
                    }

                    if (cost >= best)
                        continue;

                    best = cost;
                    result = Enumerable.Range(0, count)
                        .Select(i => handles[reversed ? (offset - i + count) % count : (offset + i) % count]).ToList();
                }
            }

            return result;
        }

        static Vector3 Centroid(List<Vector3> points) => points.Aggregate(Vector3.zero, (sum, p) => sum + p) / points.Count;

        [MenuItem(MenuRoot + "Generate/Walls From Floor Plan")]
        static void GenerateWallsFromFloorPlan() {
            const string undoName = "Generate Walls From Floor Plan";
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            int group = Undo.GetCurrentGroup();

            List<Object> created = new();

            foreach (ProBuilderMesh floor in FloorPlanCandidates()) {
                GameObject walls = new($"{floor.name} Walls") { layer = floor.gameObject.layer };
                GameObjectUtility.SetStaticEditorFlags(walls, GameObjectUtility.GetStaticEditorFlags(floor.gameObject));
                Undo.RegisterCreatedObjectUndo(walls, undoName);
                Undo.SetTransformParent(walls.transform, floor.transform, false, undoName);
                walls.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                walls.transform.localScale = Vector3.one;

                ProBuilderMesh mesh = Undo.AddComponent<ProBuilderMesh>(walls);
                mesh.Clear();
                TsitskiProBuilderFloorPlan plan = Undo.AddComponent<TsitskiProBuilderFloorPlan>(walls);
                plan.floor = floor;
                plan.Rebuild();

                created.Add(walls);
            }

            Undo.CollapseUndoOperations(group);

            if (created.Count > 0)
                Selection.objects = created.ToArray();
        }

        [MenuItem(MenuRoot + "Generate/Walls From Floor Plan", true)]
        static bool GenerateWallsFromFloorPlanValidate() => FloorPlanCandidates().Any();

        [MenuItem(MenuRoot + "Generate/Convert To Polygon")]
        static void ConvertToPolygon() {
            const string undoName = "Convert To Polygon";
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            int group = Undo.GetCurrentGroup();

            foreach (ProBuilderMesh mesh in PolygonCandidates().ToArray()) {
                List<List<Vector2>> loops = TsitskiProBuilderFloorPlan.FloorOutlines(mesh, out float top);
                if (loops.Count == 0) {
                    Debug.LogWarning($"[tsitski ProBuilder Tools] Convert To Polygon skipped '{mesh.name}': it has no faces pointing up.", mesh);
                    continue;
                }

                int outer = 0;
                for (int i = 1; i < loops.Count; i++)
                    if (Mathf.Abs(CutterOutline.SignedArea(loops[i])) > Mathf.Abs(CutterOutline.SignedArea(loops[outer])))
                        outer = i;
                float outerSign = Mathf.Sign(CutterOutline.SignedArea(loops[outer]));

                List<List<Vector2>> holes = loops.Where((l, i) => i != outer && Mathf.Sign(CutterOutline.SignedArea(l)) != outerSign).ToList();
                int islands = loops.Count - 1 - holes.Count;
                if (islands > 0)
                    Debug.LogWarning($"[tsitski ProBuilder Tools] Convert To Polygon on '{mesh.name}' kept the largest outline and dropped {islands} separate piece(s).", mesh);

                float bottom = mesh.positions.Min(p => p.y);
                float thickness = top - bottom >= 0.01f ? top - bottom : 0.25f;
                float y = top - thickness;
                List<Vector3> ToPath(List<Vector2> loop) => loop.Select(p => new Vector3(p.x, y, p.y)).ToList();

                Undo.RecordObject(mesh, undoName);
                TsitskiProBuilderPolygon polygon = Undo.AddComponent<TsitskiProBuilderPolygon>(mesh.gameObject);
                polygon.thickness = thickness;
                polygon.paths = new List<PolygonPath> { new() { path = ToPath(loops[outer]) } };
                polygon.holes = holes.Select(h => new PolygonHole { path = ToPath(h) }).ToList();
                polygon.Rebuild();
                EditorUtility.SetDirty(polygon);
            }

            Undo.CollapseUndoOperations(group);
            ProBuilderEditor.Refresh();
        }

        [MenuItem(MenuRoot + "Generate/Convert To Polygon", true)]
        static bool ConvertToPolygonValidate() => PolygonCandidates().Any();

        static IEnumerable<ProBuilderMesh> PolygonCandidates() =>
            Selection.gameObjects
                .Select(g => g.GetComponent<ProBuilderMesh>())
                .Where(m => m && m.faceCount > 0 && !m.GetComponent<TsitskiProBuilderPolygon>() && !m.GetComponent<TsitskiProBuilderFloorPlan>() &&
                            !m.GetComponent<TsitskiProBuilderTrim>() && !m.GetComponent<TsitskiArray>() && !m.GetComponent<TsitskiProBuilderStairs>() &&
                            !m.GetComponent<TsitskiProBuilderPillar>());

        static IEnumerable<ProBuilderMesh> FloorPlanCandidates() =>
            Selection.gameObjects
                .Select(g => g.GetComponent<ProBuilderMesh>())
                .Where(m => m && !m.GetComponent<TsitskiProBuilderFloorPlan>() && m.faceCount > 0);

        [MenuItem(MenuRoot + "Modelling/Collapse Edges or Vertices &DEL", true)]
        static bool CollapseSelectionValidate() =>
            ProBuilderEditor.instance != null && MeshSelection.top.Any();

        static List<Vector3> MergeAcrossEdges(ProBuilderMesh mesh) {
            Dictionary<int, int> lookup = Lookup(mesh);
            HashSet<(int, int)> selected = new();
            List<Vector3> candidates = new();

            foreach (Edge edge in mesh.selectedEdges) {
                selected.Add(SortedPair(lookup[edge.a], lookup[edge.b]));
                candidates.Add(mesh.positions[edge.a]);
                candidates.Add(mesh.positions[edge.b]);
            }

            Dictionary<Face, Face> parent = new();
            Face Find(Face face) {
                while (parent[face] != face)
                    face = parent[face] = parent[parent[face]];
                return face;
            }

            foreach (WingedEdge wing in WingedEdge.GetWingedEdges(mesh)) {
                if (wing.opposite == null || !selected.Contains(SortedPair(wing.edge.common.a, wing.edge.common.b)))
                    continue;

                Face a = wing.face, b = wing.opposite.face;
                if (Vector3.Dot(UnityEngine.ProBuilder.Math.Normal(mesh, a), UnityEngine.ProBuilder.Math.Normal(mesh, b)) < CoplanarDot) {
                    Debug.LogWarning("[tsitski ProBuilder Tools] Skipped an edge between faces that aren't coplanar.", mesh);
                    continue;
                }

                parent.TryAdd(a, a);
                parent.TryAdd(b, b);
                parent[Find(a)] = Find(b);
            }

            foreach (IGrouping<Face, Face> group in parent.Keys.ToList().GroupBy(Find)) {
                if (group.Count() > 1)
                    MergeElements.Merge(mesh, group);
            }

            return candidates;
        }

        static List<Vector3> MergeAroundVertices(ProBuilderMesh mesh, IEnumerable<int> vertices) {
            List<Vector3> candidates = vertices.Select(i => mesh.positions[i]).ToList();

            foreach (Vector3 position in candidates.Distinct().ToList()) {
                int handle = HandleAt(mesh, position);
                if (handle < 0)
                    continue;

                List<Face> faces = FacesWith(mesh, handle);
                if (faces.All(face => !OnPerimeter(mesh, face, handle) || IsCollinear(mesh, face, handle)))
                    continue;

                Vector3 normal = UnityEngine.ProBuilder.Math.Normal(mesh, faces[0]);
                if (faces.Any(face => Vector3.Dot(UnityEngine.ProBuilder.Math.Normal(mesh, face), normal) < CoplanarDot)) {
                    Debug.LogWarning($"[tsitski ProBuilder Tools] Skipped vertex at {position}: the faces around it aren't coplanar.", mesh);
                    candidates.RemoveAll(p => (p - position).sqrMagnitude <= PositionTolerance * PositionTolerance);
                    continue;
                }

                if (faces.Count > 1)
                    MergeElements.Merge(mesh, faces);
            }

            return candidates;
        }

        static void DissolveVertices(ProBuilderMesh mesh, List<Vector3> candidates) {
            HashSet<int> removable = new();
            foreach (Vector3 position in candidates) {
                int handle = HandleAt(mesh, position);
                if (handle < 0)
                    continue;

                List<Face> faces = FacesWith(mesh, handle);
                if (faces.Count > 0 && faces.All(face => !OnPerimeter(mesh, face, handle) || IsCollinear(mesh, face, handle)))
                    removable.Add(handle);
            }

            HashSet<Face> affected = new();
            foreach (int handle in removable)
                affected.UnionWith(FacesWith(mesh, handle));

            List<(Face face, List<Vector3> loop, Vector3 normal)> rebuilds = new();
            foreach (Face face in affected) {
                List<int> loop = PerimeterLoop(mesh, face);
                if (loop == null)
                    continue;

                List<Vector3> kept = loop.Where(h => !removable.Contains(h)).Select(h => HandlePosition(mesh, h)).ToList();
                if (kept.Count >= 3)
                    rebuilds.Add((face, kept, UnityEngine.ProBuilder.Math.Normal(mesh, face)));
            }

            List<Face> replaced = new();
            foreach ((Face face, List<Vector3> loop, Vector3 normal) in rebuilds) {
                int[] indexes = loop.Select(p => mesh.sharedVertices[HandleAt(mesh, p)][0]).ToArray();
                Face created = mesh.CreatePolygon(indexes, false);
                if (created == null)
                    continue;

                created.submeshIndex = face.submeshIndex;
                created.smoothingGroup = face.smoothingGroup;
                created.uv = face.uv;
                created.textureGroup = face.textureGroup;
                created.manualUV = face.manualUV;

                if (Vector3.Dot(UnityEngine.ProBuilder.Math.Normal(mesh, created), normal) < 0f)
                    created.Reverse();

                replaced.Add(face);
            }

            if (replaced.Count > 0)
                mesh.DeleteFaces(replaced);
        }

        static List<int> PerimeterLoop(ProBuilderMesh mesh, Face face) {
            Dictionary<int, int> lookup = Lookup(mesh);
            Dictionary<int, List<int>> neighbours = new();

            foreach (Edge edge in face.edges) {
                int a = lookup[edge.a], b = lookup[edge.b];
                if (!neighbours.TryGetValue(a, out List<int> na))
                    neighbours[a] = na = new List<int>();
                if (!neighbours.TryGetValue(b, out List<int> nb))
                    neighbours[b] = nb = new List<int>();
                na.Add(b);
                nb.Add(a);
            }

            if (neighbours.Count < 3 || neighbours.Values.Any(n => n.Count != 2))
                return null;

            int start = neighbours.Keys.First();
            List<int> loop = new() { start };
            int previous = start, current = neighbours[start][0];

            while (current != start) {
                loop.Add(current);
                int next = neighbours[current][0] == previous ? neighbours[current][1] : neighbours[current][0];
                previous = current;
                current = next;

                if (loop.Count > neighbours.Count)
                    return null;
            }

            return loop.Count == neighbours.Count ? loop : null;
        }

        static bool OnPerimeter(ProBuilderMesh mesh, Face face, int handle) {
            Dictionary<int, int> lookup = Lookup(mesh);
            return face.edges.Any(edge => lookup[edge.a] == handle || lookup[edge.b] == handle);
        }

        static bool IsCollinear(ProBuilderMesh mesh, Face face, int handle) {
            List<int> loop = PerimeterLoop(mesh, face);
            if (loop == null)
                return false;

            int i = loop.IndexOf(handle);
            if (i < 0)
                return false;

            Vector3 previous = HandlePosition(mesh, loop[(i - 1 + loop.Count) % loop.Count]);
            Vector3 point = HandlePosition(mesh, handle);
            Vector3 next = HandlePosition(mesh, loop[(i + 1) % loop.Count]);

            Vector3 into = (point - previous).normalized;
            Vector3 outOf = (next - point).normalized;
            return Vector3.Dot(into, outOf) > 0f && Vector3.Cross(into, outOf).magnitude < CollinearTolerance;
        }

        static List<Face> FacesWith(ProBuilderMesh mesh, int handle) {
            Dictionary<int, int> lookup = Lookup(mesh);
            return mesh.faces.Where(face => face.distinctIndexes.Any(i => lookup[i] == handle)).ToList();
        }

        static int HandleAt(ProBuilderMesh mesh, Vector3 position) {
            IList<SharedVertex> shared = mesh.sharedVertices;
            IList<Vector3> positions = mesh.positions;
            float tolerance = PositionTolerance * PositionTolerance;

            for (int h = 0; h < shared.Count; h++)
                if ((positions[shared[h][0]] - position).sqrMagnitude <= tolerance)
                    return h;

            return -1;
        }

        static Dictionary<int, int> Lookup(ProBuilderMesh mesh) {
            Dictionary<int, int> lookup = new();
            IList<SharedVertex> shared = mesh.sharedVertices;
            for (int h = 0; h < shared.Count; h++)
                foreach (int index in shared[h])
                    lookup[index] = h;
            return lookup;
        }

        static Vector3 HandlePosition(ProBuilderMesh mesh, int handle) => mesh.positions[mesh.sharedVertices[handle][0]];

        static (int, int) SortedPair(int a, int b) => a < b ? (a, b) : (b, a);
    }
}
