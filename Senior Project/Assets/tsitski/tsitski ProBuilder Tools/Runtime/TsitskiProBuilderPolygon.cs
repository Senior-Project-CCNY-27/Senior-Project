using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

using Tsitski.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder Polygon")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class TsitskiProBuilderPolygon : MonoBehaviour, IPathOwner, IForcedMaterials, IInputHashed, ISerializationCallbackReceiver, IPivotShiftable {
        bool IPivotShiftable.CanShiftPivot(out string reason) {
            reason = "its paths are stored in its stats asset and shared with other objects.";
            return !pathInStats;
        }

        void IPivotShiftable.ShiftLocalPoints(Vector3 offset) {
            foreach (PolygonPath loop in paths.Where(p => p?.path != null))
                loop.path = PivotShift.Shifted(loop.path, offset);
            foreach (PolygonHole hole in holes.Where(h => h?.path != null))
                hole.path = PivotShift.Shifted(hole.path, offset);
        }

        void IPivotShiftable.ApplyPivotShift() => Rebuild();

        private const float Epsilon = 1e-4f;
        private const int RoundingSegments = 8;
        public const float MinThickness = 0.01f;

        [Tooltip(Tooltips.PolygonStats)] public TsitskiProBuilderPolygonStats stats;

        [Header("Materials")]
        [Tooltip(Tooltips.PolygonTopMaterial)] public Material topMaterial;
        [Tooltip(Tooltips.PolygonBottomMaterial)] public Material bottomMaterial;

        [Header("Mesh")]
        [Tooltip(Tooltips.PolygonThickness)] public float thickness = 0.25f;
        [Tooltip(Tooltips.PolygonSubdivisionStep)] [Min(0f)] public float subdivisionStep;
        [Tooltip(Tooltips.ApplyCutsToProBuilderMesh)] public bool applyCutsToProBuilderMesh;

        [Header("Path")]
        [Tooltip(Tooltips.PolygonPaths)] public List<PolygonPath> paths = DefaultPaths();
        [Tooltip(Tooltips.PolygonHoles)] public List<PolygonHole> holes = new();

        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("path")] private List<Vector3> legacyPath = new();
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("pathRadii")] private List<float> legacyPathRadii = new();

        [NonSerialized] public int skippedHoles;

        public static readonly HashSet<TsitskiProBuilderPolygon> All = new();
        public static event Action<ProBuilderMesh> Rebuilt;

        public Material activeTopMaterial => stats ? stats.topMaterial : topMaterial;
        public Material activeBottomMaterial => stats ? stats.bottomMaterial : bottomMaterial;
        Material[] IForcedMaterials.forcedMaterials => new[] { activeTopMaterial, activeBottomMaterial };

        public static List<Vector3> DefaultPath() => new() { Vector3.zero, new(4f, 0f, 0f), new(4f, 0f, 4f), new(0f, 0f, 4f) };
        public static List<PolygonPath> DefaultPaths() => new() { new PolygonPath { path = DefaultPath() } };

        public bool pathInStats => stats && stats.savePolygon;
        public float activeThickness => ClampThickness(stats ? stats.thickness : thickness);

        public static float ClampThickness(float value) =>
            Mathf.Abs(value) >= MinThickness ? value : value < 0f ? -MinThickness : MinThickness;
        public float activeSubdivisionStep => stats ? stats.subdivisionStep : subdivisionStep;
        public bool activeApplyCutsToProBuilderMesh => stats ? stats.applyCutsToProBuilderMesh : applyCutsToProBuilderMesh;

        public List<PolygonPath> activePaths {
            get => (pathInStats ? stats.paths : paths) ?? new List<PolygonPath>();
            set {
                if (pathInStats)
                    stats.paths = value;
                else
                    paths = value;
            }
        }

        public float baseY => BaseY(activePaths);

        public static float BaseY(IEnumerable<PolygonPath> loops) {
            List<Vector3> points = loops.Where(p => p?.path != null).SelectMany(p => p.path).ToList();
            return points.Count > 0 ? points.Average(p => p.y) : 0f;
        }

        public List<PolygonHole> activeHoles {
            get => (pathInStats ? stats.holes : holes) ?? new List<PolygonHole>();
            set {
                if (pathInStats)
                    stats.holes = value;
                else
                    holes = value;
            }
        }

        List<Vector3> IPathOwner.localPath {
            get => paths is { Count: > 0 } ? paths[0]?.path : null;
            set {
                List<Vector3> first = paths is { Count: > 0 } ? paths[0]?.path : null;
                if (first == null)
                    return;

                Vector3 delta = first.Count > 0 && value is { Count: > 0 } ? value[0] - first[0] : Vector3.zero;
                paths[0].path = value;
                foreach (PolygonPath other in paths.Skip(1).Where(p => p?.path != null))
                    other.path = other.path.Select(p => p + delta).ToList();
                foreach (PolygonHole hole in holes.Where(h => h?.path != null))
                    hole.path = hole.path.Select(p => p + delta).ToList();
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() => paths = PolygonPath.Upgrade(paths, ref legacyPath, ref legacyPathRadii);

        private void Reset() {
            TsitskiSettings.ApplyDefaultStaticFlags(gameObject);
            if (TryGetComponent(out MeshCollider _))
                return;

#if UNITY_EDITOR
            Undo.AddComponent<MeshCollider>(gameObject);
#else
            gameObject.AddComponent<MeshCollider>();
#endif
        }

        private void OnEnable() {
            if (TryGetComponent(out ProBuilderMesh mesh) && mesh.faceCount == 0)
                mesh.Clear();

            All.Add(this);
            TsitskiProBuilderPolygonStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() {
            All.Remove(this);
            TsitskiProBuilderPolygonStats.Changed -= OnStatsChanged;
        }

        private void OnValidate() => QueueRebuild();

        private void OnStatsChanged(TsitskiProBuilderPolygonStats changed) {
            if (changed == stats)
                QueueRebuild();
        }

        public void QueueRebuild() => RebuildQueue.Queue(this, Rebuild);

        private static List<int> CapRegions(List<Vector3> positions, List<Face> faces) {
            List<int> regions = new(faces.Count);
            foreach (Face face in faces) {
                Vector3 normal = Vector3.zero;
                IList<int> indexes = face.indexes;
                for (int i = 0; i < indexes.Count; i += 3)
                    normal += Vector3.Cross(positions[indexes[i + 1]] - positions[indexes[i]], positions[indexes[i + 2]] - positions[indexes[i]]);
                float up = normal.normalized.y;
                regions.Add(up > 0.5f ? 0 : up < -0.5f ? 1 : -1);
            }
            return regions;
        }

        [SerializeField] [HideInInspector] private int inputHash;
        int IInputHashed.inputHash { get => inputHash; set => inputHash = value; }

        public void Rebuild() {
            BuildGenerated();
            GeneratorInputs.Store(this);
        }

        private void BuildGenerated() {
            TsitskiSettings.AutoName(this, stats, "ProBuilder Polygon");

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            KeptFaceMaterials kept = KeptFaceMaterials.Capture(mesh);

            float signedThickness = activeThickness;
            float y = baseY + Mathf.Min(0f, signedThickness);
            skippedHoles = 0;

            List<Vector3> positions = new();
            List<Face> faces = new();
            foreach ((List<D2> outline, List<List<D2>> holes) in Regions(PathLoops(), HoleLoops())) {
                IList<IList<Vector3>> holePoints = holes.Select(h => (IList<Vector3>)ToPoints(h, y)).ToList();
                ActionResult result = mesh.CreateShapeFromPolygon(ToPoints(outline, y), Mathf.Abs(signedThickness), false, holePoints.Count > 0 ? holePoints : null);
                if (result.status != ActionResult.Status.Success) {
                    skippedHoles++;
                    continue;
                }

                List<Vector2[]> pieces = QuadFill.Pieces(outline, holes);
                if (pieces != null)
                    AddWithPieceCaps(mesh, pieces, activeSubdivisionStep, activeApplyCutsToProBuilderMesh, positions, faces);
                else
                    AddShape(mesh, positions, faces);
            }

            if (faces.Count == 0)
                mesh.Clear();
            else {
                mesh.RebuildWithPositionsAndFaces(positions, faces);
                kept?.Apply(mesh);
                ForcedMaterials.Apply(mesh, CapRegions(positions, faces), activeTopMaterial, activeBottomMaterial);
            }
            mesh.ToMesh();
            mesh.Refresh();

            if (TryGetComponent(out MeshRenderer meshRenderer) && !meshRenderer.sharedMaterial)
                meshRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
            Rebuilt?.Invoke(mesh);
        }

        private static void AddShape(ProBuilderMesh mesh, List<Vector3> positions, List<Face> faces) {
            int offset = positions.Count;
            positions.AddRange(mesh.positions);
            faces.AddRange(mesh.faces.Select(f => new Face(f.indexes.Select(i => i + offset).ToArray()) {
                smoothingGroup = f.smoothingGroup, submeshIndex = f.submeshIndex
            }));
        }

        private static void AddWithPieceCaps(ProBuilderMesh mesh, List<Vector2[]> pieces, float step, bool separateCells, List<Vector3> positions,
            List<Face> faces) {
            IList<Vector3> shape = mesh.positions;
            Dictionary<int, int> remap = new();
            List<(float y, float side, int smoothingGroup)> caps = new();

            foreach (Face face in mesh.faces) {
                Vector3 normal = UnityEngine.ProBuilder.Math.Normal(mesh, face);
                if (Mathf.Abs(normal.y) > 0.99f) {
                    float y = shape[face.indexes[0]].y;
                    if (!caps.Any(c => Mathf.Abs(c.y - y) < Epsilon && c.side == Mathf.Sign(normal.y)))
                        caps.Add((y, Mathf.Sign(normal.y), face.smoothingGroup));
                    continue;
                }

                int[] indexes = face.indexes.Select(i => {
                    if (!remap.TryGetValue(i, out int index)) {
                        remap[i] = index = positions.Count;
                        positions.Add(shape[i]);
                    }
                    return index;
                }).ToArray();
                faces.Add(new Face(indexes) { smoothingGroup = face.smoothingGroup, submeshIndex = face.submeshIndex });
            }

            foreach ((float y, float side, int smoothingGroup) in caps)
            foreach (Vector2[] piece in pieces) {
                PieceGrid(piece, step, out List<Vector2> points, out List<int> triangles);
                int start = positions.Count;
                positions.AddRange(points.Select(p => new Vector3(p.x, y, p.y)));

                Vector2 a = piece[1] - piece[0], b = piece[2] - piece[0];
                bool flip = Mathf.Sign(a.y * b.x - a.x * b.y) != side;
                if (flip)
                    for (int i = 0; i < triangles.Count; i += 3)
                        (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);

                if (!separateCells) {
                    faces.Add(new Face(triangles.Select(i => start + i).ToArray()) { smoothingGroup = smoothingGroup });
                    continue;
                }

                positions.RemoveRange(start, points.Count);
                int perCell = piece.Length == 4 ? 6 : 3;
                for (int c = 0; c < triangles.Count; c += perCell) {
                    Dictionary<int, int> local = new();
                    int[] cell = triangles.Skip(c).Take(perCell).Select(i => {
                        if (!local.TryGetValue(i, out int index)) {
                            local[i] = index = positions.Count;
                            positions.Add(new Vector3(points[i].x, y, points[i].y));
                        }
                        return index;
                    }).ToArray();
                    faces.Add(new Face(cell) { smoothingGroup = smoothingGroup });
                }
            }
        }

        public static (int u, int v) PieceCells(Vector2[] piece, float step) {
            if (step < TsitskiProBuilderTrim.MinLoopCutStep)
                return (1, 1);

            int Count(float length) => Mathf.Max(1, Mathf.RoundToInt(length / step));
            if (piece.Length == 4)
                return (Count((Vector2.Distance(piece[0], piece[1]) + Vector2.Distance(piece[3], piece[2])) * 0.5f),
                    Count((Vector2.Distance(piece[0], piece[3]) + Vector2.Distance(piece[1], piece[2])) * 0.5f));

            int cells = Count(Mathf.Max(Vector2.Distance(piece[0], piece[1]), Vector2.Distance(piece[1], piece[2]), Vector2.Distance(piece[2], piece[0])));
            return (cells, cells);
        }

        public static void PieceGrid(Vector2[] piece, float step, out List<Vector2> points, out List<int> triangles) {
            (int cellsU, int cellsV) = PieceCells(piece, step);
            int cells = cellsU;
            points = new List<Vector2>();
            triangles = new List<int>();

            if (piece.Length == 4) {
                for (int v = 0; v <= cellsV; v++)
                for (int u = 0; u <= cellsU; u++)
                    points.Add(QuadPoint(piece, (float)u / cellsU, (float)v / cellsV));

                for (int v = 0; v < cellsV; v++)
                for (int u = 0; u < cellsU; u++) {
                    int i = v * (cellsU + 1) + u;
                    triangles.AddRange(new[] { i, i + 1, i + cellsU + 2, i, i + cellsU + 2, i + cellsU + 1 });
                }
                return;
            }

            Dictionary<(int, int), int> index = new();
            for (int j = 0; j <= cells; j++)
            for (int i = 0; i + j <= cells; i++) {
                index[(i, j)] = points.Count;
                points.Add(piece[0] + (piece[1] - piece[0]) * ((float)i / cells) + (piece[2] - piece[0]) * ((float)j / cells));
            }

            for (int j = 0; j < cells; j++)
            for (int i = 0; i + j < cells; i++) {
                triangles.AddRange(new[] { index[(i, j)], index[(i + 1, j)], index[(i, j + 1)] });
                if (i + j < cells - 1)
                    triangles.AddRange(new[] { index[(i + 1, j)], index[(i + 1, j + 1)], index[(i, j + 1)] });
            }
        }

        public static List<Vector2[]> PieceGridLines(Vector2[] piece, float step) {
            (int cellsU, int cellsV) = PieceCells(piece, step);
            List<Vector2[]> lines = new();
            if (piece.Length == 4) {
                for (int k = 1; k < cellsU; k++)
                    lines.Add(new[] { QuadPoint(piece, (float)k / cellsU, 0f), QuadPoint(piece, (float)k / cellsU, 1f) });
                for (int k = 1; k < cellsV; k++)
                    lines.Add(new[] { QuadPoint(piece, 0f, (float)k / cellsV), QuadPoint(piece, 1f, (float)k / cellsV) });
                return lines;
            }

            int cells = cellsU;
            for (int k = 1; k < cells; k++) {
                float t = (float)k / cells;
                lines.Add(new[] { Vector2.Lerp(piece[0], piece[1], t), Vector2.Lerp(piece[0], piece[2], t) });
                lines.Add(new[] { Vector2.Lerp(piece[1], piece[0], t), Vector2.Lerp(piece[1], piece[2], t) });
                lines.Add(new[] { Vector2.Lerp(piece[2], piece[0], t), Vector2.Lerp(piece[2], piece[1], t) });
            }
            return lines;
        }

        private static Vector2 QuadPoint(Vector2[] quad, float u, float v) =>
            Vector2.Lerp(Vector2.Lerp(quad[0], quad[1], u), Vector2.Lerp(quad[3], quad[2], u), v);

        public static List<Vector2> Loop(List<Vector3> points, List<float> radii) {
            List<Vector3> rounded = PathUtility.Rounded(points, radii, true, RoundingSegments, out bool loop);
            if (!loop)
                return new List<Vector2>();

            List<Vector2> outline = TsitskiProBuilderFloorPlan.Clean(rounded.Select(p => new Vector2(p.x, p.z)).ToList());
            return outline.Count >= 3 && Mathf.Abs(CutterOutline.SignedArea(outline)) > Epsilon * Epsilon ? outline : new List<Vector2>();
        }

        public static List<Vector2[]> CapPieces(IEnumerable<List<Vector2>> outlines, IEnumerable<List<Vector2>> holeLoops) {
            List<Vector2[]> quads = new();
            foreach ((List<D2> outline, List<List<D2>> pieceHoles) in Regions(outlines, holeLoops))
                if (QuadFill.Pieces(outline, pieceHoles) is { } pieces)
                    quads.AddRange(pieces);

            return quads;
        }

        private static List<(List<D2> outline, List<List<D2>> holes)> Regions(IEnumerable<List<Vector2>> outlines, IEnumerable<List<Vector2>> holeLoops) =>
            PolygonBoolean.Combine(outlines.Where(l => l.Count >= 3).Select(l => l.Select(D2.From).ToList()),
                holeLoops.Where(h => h.Count >= 3).Select(h => h.Select(D2.From).ToList()));

        public List<List<Vector2>> PathLoops() => PathLoops(activePaths);

        public static List<List<Vector2>> PathLoops(IEnumerable<PolygonPath> loops) =>
            loops.Select(p => p == null ? new List<Vector2>() : Loop(p.path, p.pathRadii)).ToList();

        public List<List<Vector2>> HoleLoops() => HoleLoops(activeHoles);

        public static List<List<Vector2>> HoleLoops(IEnumerable<PolygonHole> loops) =>
            loops.Select(h => h == null ? new List<Vector2>() : Loop(h.path, h.pathRadii)).ToList();

        private static List<Vector3> ToPoints(List<D2> loop, float y) => loop.Select(p => new Vector3((float)p.x, y, (float)p.y)).ToList();
    }
}
