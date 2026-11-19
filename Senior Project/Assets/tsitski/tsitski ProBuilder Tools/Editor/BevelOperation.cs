using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using Math = UnityEngine.ProBuilder.Math;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [System.Serializable]
    internal class BevelOperation : MeshSceneOperation {
        private const string MenuPath = "Tools/tsitski/ProBuilder/Modelling/Bevel &b";
        private const int MaxEdgeLoops = 16;
        private const float WeldDistance = 1e-4f;

        [SerializeField] private float _amount = 0.1f;
        [SerializeField] private int _edgeLoops;
        private float _maxAmount = 1f;

        protected override string title => "Bevel";
        protected override int fieldCount => 2;

        [MenuItem(MenuPath)]
        private static void Bevel() => Run(new BevelOperation());

        [MenuItem(MenuPath, true)]
        private static bool BevelValidate() =>
            ProBuilderEditor.selectMode == SelectMode.Edge && MeshSelection.selectedEdgeCount > 0 ||
            ProBuilderEditor.selectMode == SelectMode.Face && MeshSelection.selectedFaceCount > 0;

        protected override bool Accepts(ProBuilderMesh mesh) =>
            ProBuilderEditor.selectMode == SelectMode.Face ? mesh.selectedFaceCount > 0 : mesh.selectedEdgeCount > 0;

        protected override void DrawFields() {
            _amount = EditorGUILayout.Slider("Amount", _amount, 0.001f, _maxAmount);
            _edgeLoops = EditorGUILayout.IntSlider("Edge Loops", _edgeLoops, 0, MaxEdgeLoops);
        }

        protected override void Prepare() {
            float limit = float.MaxValue;
            foreach (Target target in targets) {
                ProBuilderMesh mesh = target.mesh;
                Dictionary<int, int> shared = SharedLookup(mesh);
                HashSet<int> ends = new(Edges(target).SelectMany(e => new[] { shared[e.a], shared[e.b] }));
                foreach (Face face in mesh.faces)
                foreach (Edge edge in face.edges)
                    if (ends.Contains(shared[edge.a]) || ends.Contains(shared[edge.b]))
                        limit = Mathf.Min(limit, Vector3.Distance(mesh.positions[edge.a], mesh.positions[edge.b]) - 0.001f);
            }

            _maxAmount = limit < float.MaxValue ? Mathf.Max(0.002f, limit) : 1f;
            _amount = Mathf.Clamp(_amount, 0.001f, _maxAmount);
        }

        private static IList<Edge> Edges(Target target) {
            ProBuilderMesh mesh = target.mesh;
            return target.faces.Length > 0 && ProBuilderEditor.selectMode == SelectMode.Face
                ? PerimeterEdges(mesh, target.faces.Select(i => mesh.faces[i]))
                : target.edges;
        }

        protected override void ApplyTo(Target target) {
            ProBuilderMesh mesh = target.mesh;
            IList<Edge> edges = Edges(target);
            if (edges.Count == 0)
                return;

            List<(Vector3 a, Vector3 b)> originals = edges.Select(e => (mesh.positions[e.a], mesh.positions[e.b])).ToList();
            List<Face> created = UnityEngine.ProBuilder.MeshOperations.Bevel.BevelEdges(mesh, edges, _amount);
            if (created == null)
                return;

            if (_edgeLoops > 0)
                created = Round(mesh, created, originals.SelectMany(o => new[] { o.a, o.b }).Distinct().ToList(), _amount, _edgeLoops);
            mesh.SetSelectedFaces(created);
        }

        private static float ArcWeight(Vector3 a, Vector3 corner, Vector3 b) {
            Vector3 into = corner - a, outOf = b - corner;
            if (into.sqrMagnitude < 1e-12f || outOf.sqrMagnitude < 1e-12f)
                return 1f;
            return Mathf.Cos(Vector3.Angle(into, outOf) * 0.5f * Mathf.Deg2Rad);
        }

        private static Vector3 ArcPoint(Vector3 a, Vector3 corner, Vector3 b, float weight, float t) {
            float wa = (1f - t) * (1f - t), wc = 2f * weight * t * (1f - t), wb = t * t;
            return (a * wa + corner * wc + b * wb) / (wa + wc + wb);
        }

        private static bool SphereThrough(Vector3 a, Vector3 b, Vector3 d, Vector3 onArc, out Vector3 center, out float radius) {
            center = Vector3.zero;
            radius = 0f;
            Vector3 normal = Vector3.Cross(b - a, d - a);
            if (normal.sqrMagnitude < 1e-12f)
                return false;

            Vector3 ab = b - a, ad = d - a;
            Vector3 circumcenter = a + (Vector3.Cross(normal, ab) * ad.sqrMagnitude + Vector3.Cross(ad, normal) * ab.sqrMagnitude) /
                (2f * normal.sqrMagnitude);
            Vector3 axis = normal.normalized;

            float along = Vector3.Dot(onArc - a, axis);
            if (Mathf.Abs(along) < 1e-6f)
                return false;

            float planar = (onArc - circumcenter - axis * Vector3.Dot(onArc - circumcenter, axis)).sqrMagnitude;
            float ring = (a - circumcenter).sqrMagnitude;
            float offset = Vector3.Dot(onArc - circumcenter, axis);
            float t = (planar + offset * offset - ring) / (2f * offset);
            center = circumcenter + axis * t;
            radius = (a - center).magnitude;
            return radius > 1e-6f && !float.IsNaN(radius);
        }

        private static List<Face> Round(ProBuilderMesh mesh, List<Face> bevelFaces, List<Vector3> corners, float amount, int loops) {
            if (mesh.colors == null || mesh.colors.Count != mesh.vertexCount)
                mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
            if (mesh.textures == null || mesh.textures.Count != mesh.vertexCount)
                mesh.textures = new Vector2[mesh.vertexCount];

            Dictionary<int, int> shared = SharedLookup(mesh);
            IList<Vector3> positions = mesh.positions;
            Vector3 Position(int handle) => positions[mesh.sharedVertices[handle][0]];

            List<int> Loop(Face face) => WingedEdge.SortEdgesByAdjacency(face).Select(e => shared[e.a]).ToList();

            Dictionary<(int, int), Vector3[]> arcs = new();
            int CornerOf(Vector3 point) {
                int best = -1;
                float error = float.MaxValue;
                for (int c = 0; c < corners.Count; c++) {
                    float distance = Vector3.Distance(point, corners[c]);
                    float e = Mathf.Abs(distance - amount) + distance * 0.01f;
                    if (e < error) {
                        error = e;
                        best = c;
                    }
                }
                return best;
            }

            Vector3[] Arc(int from, int to, Vector3 corner) {
                (int, int) key = Key(from, to);
                if (!arcs.TryGetValue(key, out Vector3[] arc)) {
                    Vector3 a = Position(key.Item1), b = Position(key.Item2);
                    float weight = ArcWeight(a, corner, b);
                    arc = new Vector3[loops + 2];
                    for (int k = 0; k <= loops + 1; k++)
                        arc[k] = ArcPoint(a, corner, b, weight, (float)k / (loops + 1));
                    arc[0] = a;
                    arc[^1] = b;
                    arcs[key] = arc;
                }

                return from == key.Item1 ? arc : arc.Reverse().ToArray();
            }

            List<(Face source, List<Vector3[]> polygons)> rebuilds = new();
            HashSet<Face> bevelQuads = new();

            foreach (Face face in bevelFaces) {
                List<int> loop = Loop(face);
                if (loop.Count != 4)
                    continue;

                int[] cornerIndex = loop.Select(h => CornerOf(Position(h))).ToArray();
                int s = -1;
                for (int option = 0; option < 2; option++)
                    if (cornerIndex[option] >= 0 && cornerIndex[option] == cornerIndex[(option + 1) % 4] &&
                        cornerIndex[(option + 2) % 4] >= 0 && cornerIndex[(option + 2) % 4] == cornerIndex[(option + 3) % 4]) {
                        s = option;
                        break;
                    }

                if (s < 0)
                    continue;

                Vector3[] near = Arc(loop[s], loop[(s + 1) % 4], corners[cornerIndex[s]]);
                Vector3[] far = Arc(loop[(s + 3) % 4], loop[(s + 2) % 4], corners[cornerIndex[(s + 2) % 4]]);

                List<Vector3[]> quads = new();
                for (int k = 0; k <= loops; k++)
                    quads.Add(new[] { near[k], near[k + 1], far[k + 1], far[k] });

                rebuilds.Add((face, quads));
                bevelQuads.Add(face);
            }

            if (rebuilds.Count == 0)
                return bevelFaces;

            List<Vector3[]> CornerPatch(List<int> loop) {
                if (!arcs.ContainsKey(Key(loop[0], loop[1])) || !arcs.ContainsKey(Key(loop[1], loop[2])) ||
                    !arcs.ContainsKey(Key(loop[2], loop[0])))
                    return null;

                int corner = CornerOf(Position(loop[0]));
                if (corner < 0 || CornerOf(Position(loop[1])) != corner || CornerOf(Position(loop[2])) != corner)
                    return null;

                Vector3[] Edge(int from, int to) {
                    Vector3[] arc = arcs[Key(from, to)];
                    return from == Key(from, to).Item1 ? arc : arc.Reverse().ToArray();
                }

                int n = loops + 1, h = n / 2;
                bool odd = n % 2 == 1;
                Vector3 c = corners[corner];
                Vector3[] ends = { Position(loop[0]), Position(loop[1]), Position(loop[2]) };
                Vector3[][] arc = { Edge(loop[0], loop[1]), Edge(loop[1], loop[2]), Edge(loop[2], loop[0]) };

                bool sphere = SphereThrough(ends[0], ends[1], ends[2], arc[0][Mathf.Max(1, n / 2)], out Vector3 center, out float radius);
                Vector3 Project(Vector3 point) => sphere ? center + (point - center).normalized * radius : point;

                float w0 = ArcWeight(ends[0], c, ends[1]), w1 = ArcWeight(ends[1], c, ends[2]), w2 = ArcWeight(ends[2], c, ends[0]);
                float third = 1f / 9f;
                Vector3 middle = Project(((ends[0] + ends[1] + ends[2]) * third + c * (2f * third * (w0 + w1 + w2))) /
                                         (3f * third + 2f * third * (w0 + w1 + w2)));

                Vector3[] inner = new Vector3[3];
                for (int x = 0; x < 3; x++)
                    inner[x] = odd ? Project(Vector3.Lerp(middle, ends[x], 1f / n)) : middle;

                Vector3[] Curve(Vector3 from, Vector3 to) {
                    Vector3[] curve = new Vector3[h + 1];
                    for (int t = 0; t <= h; t++)
                        curve[t] = t == 0 ? from : t == h ? to : Project(Vector3.Lerp(from, to, (float)t / h));
                    return curve;
                }

                List<Vector3[]> patch = new();
                Vector3[][] toNext = new Vector3[3][], toPrevious = new Vector3[3][];

                for (int x = 0; x < 3; x++) {
                    Vector3[] next = arc[x], previous = arc[(x + 2) % 3].Reverse().ToArray();
                    toNext[x] = Curve(next[h], inner[x]);
                    toPrevious[x] = Curve(previous[h], inner[x]);

                    Vector3[,] grid = new Vector3[h + 1, h + 1];
                    for (int i = 0; i <= h; i++) {
                        grid[i, 0] = next[i];
                        grid[0, i] = previous[i];
                        grid[h, i] = toNext[x][i];
                        grid[i, h] = toPrevious[x][i];
                    }

                    for (int i = 1; i < h; i++)
                    for (int j = 1; j < h; j++) {
                        float u = (float)i / h, v = (float)j / h;
                        Vector3 coons = (1f - v) * grid[i, 0] + v * grid[i, h] + (1f - u) * grid[0, j] + u * grid[h, j]
                                        - ((1f - u) * (1f - v) * grid[0, 0] + u * (1f - v) * grid[h, 0] + (1f - u) * v * grid[0, h] + u * v * grid[h, h]);
                        grid[i, j] = Project(coons);
                    }

                    for (int i = 0; i < h; i++)
                    for (int j = 0; j < h; j++)
                        patch.Add(new[] { grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1] });
                }

                if (odd) {
                    for (int x = 0; x < 3; x++) {
                        Vector3[] near = toNext[x], far = toPrevious[(x + 1) % 3];
                        for (int t = 0; t < h; t++)
                            patch.Add(new[] { near[t], far[t], far[t + 1], near[t + 1] });
                    }

                    patch.Add(new[] { inner[0], inner[1], inner[2] });
                }

                return patch;
            }

            foreach (Face face in mesh.faces) {
                if (bevelQuads.Contains(face))
                    continue;

                List<int> loop = Loop(face);
                if (loop.Count == 3 && CornerPatch(loop) is { } patch) {
                    rebuilds.Add((face, patch));
                    continue;
                }

                List<Vector3> outline = new();
                bool changed = false;

                for (int i = 0; i < loop.Count; i++) {
                    int a = loop[i], b = loop[(i + 1) % loop.Count];
                    outline.Add(Position(a));
                    if (!arcs.ContainsKey(Key(a, b)))
                        continue;

                    Vector3[] arc = arcs[Key(a, b)];
                    if (a != Key(a, b).Item1)
                        arc = arc.Reverse().ToArray();
                    for (int k = 1; k < arc.Length - 1; k++)
                        outline.Add(arc[k]);
                    changed = true;
                }

                if (changed)
                    rebuilds.Add((face, new List<Vector3[]> { outline.ToArray() }));
            }

            HashSet<Face> bevelSet = new(bevelFaces);
            HashSet<Face> rebuilt = new(rebuilds.Select(r => r.source));
            List<Vector3[]> polygonPositions = new();
            List<Face> polygonFaces = new();
            List<bool> isStrip = new();

            foreach ((Face source, List<Vector3[]> polygons) in rebuilds) {
                Vector3 normal = Math.Normal(mesh, source);
                foreach (Vector3[] polygon in polygons) {
                    int[] triangles = new int[(polygon.Length - 2) * 3];
                    for (int i = 2; i < polygon.Length; i++) {
                        triangles[(i - 2) * 3] = 0;
                        triangles[(i - 2) * 3 + 1] = i - 1;
                        triangles[(i - 2) * 3 + 2] = i;
                    }

                    Face face = new(triangles) {
                        submeshIndex = source.submeshIndex,
                        smoothingGroup = source.smoothingGroup,
                        uv = source.uv,
                        textureGroup = -1,
                        manualUV = false
                    };

                    Vector3 polygonNormal = Vector3.zero;
                    for (int i = 2; i < polygon.Length; i++)
                        polygonNormal += Vector3.Cross(polygon[i - 1] - polygon[0], polygon[i] - polygon[0]);
                    if (Vector3.Dot(polygonNormal, normal) < 0f)
                        face.Reverse();

                    polygonPositions.Add(polygon);
                    polygonFaces.Add(face);
                    isStrip.Add(bevelSet.Contains(source));
                }
            }

            Dictionary<Vector3, int> handles = new();
            for (int h = 0; h < mesh.sharedVertices.Count; h++)
                handles.TryAdd(Position(h), h);

            Face[] appended = mesh.AppendFaces(
                polygonPositions.ToArray(),
                polygonPositions.Select(p => Enumerable.Repeat(Color.white, p.Length).ToArray()).ToArray(),
                polygonPositions.Select(p => new Vector2[p.Length]).ToArray(),
                polygonFaces.ToArray(),
                polygonPositions.Select(p => p.Select(point => handles.TryGetValue(point, out int h) ? h : -1).ToArray()).ToArray());
            if (appended == null)
                return bevelFaces;

            List<Face> strips = appended.Where((_, i) => isStrip[i]).ToList();
            List<Face> remainingBevel = bevelFaces.Where(f => !rebuilt.Contains(f)).ToList();

            mesh.DeleteFaces(rebuilds.Select(r => r.source).ToList());

            IList<Vector3> current = mesh.positions;
            List<int> loose = appended.SelectMany(f => f.distinctIndexes).Where(i => !handles.ContainsKey(current[i])).ToList();
            if (loose.Count > 0)
                mesh.WeldVertices(loose, WeldDistance);
            return strips.Concat(remainingBevel).ToList();
        }
    }
}
