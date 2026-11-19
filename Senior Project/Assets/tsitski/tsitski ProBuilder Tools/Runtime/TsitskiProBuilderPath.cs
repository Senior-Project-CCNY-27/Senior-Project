using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder Path")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class TsitskiProBuilderPath : MonoBehaviour, IPathOwner, ITsitskiPath, IForcedMaterials, IInputHashed, IPivotShiftable {
        bool IPivotShiftable.CanShiftPivot(out string reason) {
            reason = "its branches are stored in its stats asset and shared with other objects.";
            return !pathInStats;
        }

        void IPivotShiftable.ShiftLocalPoints(Vector3 offset) {
            foreach (PathBranch branch in branches.Where(b => b?.path != null))
                branch.path = PivotShift.Shifted(branch.path, offset);
        }

        void IPivotShiftable.ApplyPivotShift() => Rebuild();

        public const float MinWidth = 0.01f;
        public const float MinHeight = 0.01f;
        public const float JunctionTolerance = 1e-3f;
        private const float Epsilon = 1e-5f;
        private const float MinJunctionGap = 30f;
        public const float CurveJoinWidthFraction = 0.25f;
        private const float WeldDistance = 0.01f;
        private const float ConnectorDepth = 0.05f;
        private const int TopRegion = 0;
        private const int SideRegion = 1;
        private const int BottomRegion = 2;

        [Tooltip(Tooltips.PathStats)] public TsitskiProBuilderPathStats stats;

        [Header("Mesh")]
        [Tooltip(Tooltips.StoreWidth)] public bool storeWidth;
        [TsitskiShowIf(nameof(storeWidth))] [Tooltip(Tooltips.PathGeneratorWidth)] [Min(MinWidth)] public float width = 2f;
        [Tooltip(Tooltips.PathGeneratorHeight)] public float height = 0.15f;
        [Tooltip(Tooltips.PathSamplesPerSegment)] [Min(1)] public int samplesPerSegment = 8;
        [Tooltip(Tooltips.SmoothingAngle)] [Range(0f, 180f)] public float smoothingAngle = 30f;

        [Header("Paths")]
        [Tooltip(Tooltips.PathBranches)] public List<PathBranch> branches = DefaultBranches();

        [Header("Materials")]
        [Tooltip(Tooltips.PathTopMaterial)] public Material topMaterial;
        [Tooltip(Tooltips.PathSideMaterial)] public Material sideMaterial;
        [Tooltip(Tooltips.PathBottomMaterial)] public Material bottomMaterial;

        [SerializeField] [HideInInspector] private int inputHash;
        int IInputHashed.inputHash { get => inputHash; set => inputHash = value; }

        public static readonly HashSet<TsitskiProBuilderPath> All = new();
        public static event Action<ProBuilderMesh> Rebuilt;

        private readonly List<int> _faceRegions = new();
        private readonly List<Vector2> _uvs = new();
        [NonSerialized] public int junctionCount;

        public static List<PathBranch> DefaultBranches() => new() {
            new PathBranch { path = new List<Vector3> { Vector3.zero, new(0f, 0f, 4f), new(2f, 0f, 8f) }, widths = new List<float> { 2f, 2f, 2f } }
        };

        public bool pathInStats => stats && stats.savePaths;
        public UnityEngine.Object pathsOwner => pathInStats ? stats : this;
        public bool widthInStats => stats && stats.storeWidth;
        public bool uniformWidth => widthInStats || storeWidth;
        public bool heightInStats => stats && stats.storeHeight;
        public float activeWidth => Mathf.Max(MinWidth, widthInStats ? stats.width : width);
        public float activeHeight => ClampHeight(heightInStats ? stats.height : height);
        public int activeSamplesPerSegment => Mathf.Max(1, stats ? stats.samplesPerSegment : samplesPerSegment);
        public float activeSmoothingAngle => stats ? stats.smoothingAngle : smoothingAngle;
        public Material activeTopMaterial => stats ? stats.topMaterial : topMaterial;
        public Material activeSideMaterial => stats ? stats.sideMaterial : sideMaterial;
        public Material activeBottomMaterial => stats ? stats.bottomMaterial : bottomMaterial;
        Material[] IForcedMaterials.forcedMaterials => new[] { activeTopMaterial, activeSideMaterial, activeBottomMaterial };

        public static float ClampHeight(float value) => Mathf.Abs(value) >= MinHeight ? value : value < 0f ? -MinHeight : MinHeight;

        public List<PathBranch> activeBranches {
            get => (pathInStats ? stats.branches : branches) ?? new List<PathBranch>();
            set {
                if (pathInStats)
                    stats.branches = value;
                else
                    branches = value;
            }
        }

        List<Vector3> IPathOwner.localPath {
            get => branches is { Count: > 0 } ? branches[0]?.path : null;
            set {
                List<Vector3> first = branches is { Count: > 0 } ? branches[0]?.path : null;
                if (first == null)
                    return;

                Vector3 delta = first.Count > 0 && value is { Count: > 0 } ? value[0] - first[0] : Vector3.zero;
                branches[0].path = value;
                foreach (PathBranch other in branches.Skip(1).Where(b => b?.path != null))
                    other.path = other.path.Select(p => p + delta).ToList();
            }
        }

        public List<Vector3> GetWorldPath(out bool closed) {
            closed = false;
            PathBranch first = activeBranches.FirstOrDefault(b => b?.path is { Count: >= 2 });
            return first == null
                ? new List<Vector3>()
                : SampleBranch(first, out _).Select(s => transform.TransformPoint(s.center)).ToList();
        }

        public float PointWidth(PathBranch branch, int index) =>
            Mathf.Max(MinWidth, !uniformWidth && branch.widths != null && index < branch.widths.Count && branch.widths[index] > 0f
                ? branch.widths[index]
                : activeWidth);

        public void SyncWidths(PathBranch branch) {
            branch.widths ??= new List<float>();
            while (branch.widths.Count < branch.path.Count)
                branch.widths.Add(branch.widths.Count > 0 ? branch.widths[^1] : activeWidth);
            if (branch.widths.Count > branch.path.Count)
                branch.widths.RemoveRange(branch.path.Count, branch.widths.Count - branch.path.Count);
        }

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
            TsitskiProBuilderPathStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() {
            All.Remove(this);
            TsitskiProBuilderPathStats.Changed -= OnStatsChanged;
        }

        private void OnValidate() => QueueRebuild();

        private void OnStatsChanged(TsitskiProBuilderPathStats changed) {
            if (changed == stats)
                QueueRebuild();
        }

        public void QueueRebuild() => RebuildQueue.Queue(this, Rebuild);

        public void Rebuild() {
            BuildGenerated();
            GeneratorInputs.Store(this);
        }

        public void GenerateLightmapUvs() {
            if (TryGetComponent(out ProBuilderMesh mesh))
                CutLightmapUnwrap.ApplyToProBuilderMesh(mesh);
        }

        private void BuildGenerated() {
            TsitskiSettings.AutoName(this, stats, "ProBuilder Path");

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            KeptFaceMaterials kept = KeptFaceMaterials.Capture(mesh);

            List<Vector3> positions = new();
            List<Face> faces = new();
            _faceRegions.Clear();
            _uvs.Clear();
            Build(positions, faces);
            Weld(positions, faces, WeldDistance);
            SmoothingAngle.Apply(positions, faces, activeSmoothingAngle);

            if (faces.Count == 0)
                mesh.Clear();
            else {
                mesh.RebuildWithPositionsAndFaces(positions, faces);
                foreach (Face face in mesh.faces)
                    face.manualUV = true;
                mesh.textures = _uvs;
                kept?.Apply(mesh);
                ForcedMaterials.Apply(mesh, _faceRegions, activeTopMaterial, activeSideMaterial, activeBottomMaterial);
            }
            mesh.ToMesh();
            mesh.Refresh();
            CutLightmapUnwrap.ApplyToProBuilderMesh(mesh);

            if (TryGetComponent(out MeshRenderer meshRenderer) && !meshRenderer.sharedMaterial)
                meshRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
            Rebuilt?.Invoke(mesh);
        }

        public struct PathSample {
            public Vector3 center;
            public float width;
        }

        public List<PathSample> SampleBranch(PathBranch branch, out List<int> controlSamples) {
            List<PathSample> samples = new();
            controlSamples = new List<int>();
            List<Vector3> path = branch.path;
            if (path is not { Count: >= 2 })
                return samples;

            int steps = activeSamplesPerSegment;
            float[] slopes = HeightSlopes(path);
            controlSamples.Add(0);
            for (int i = 0; i < path.Count - 1; i++) {
                Vector3 p0 = path[Mathf.Max(i - 1, 0)], p1 = path[i], p2 = path[i + 1], p3 = path[Mathf.Min(i + 2, path.Count - 1)];
                float w1 = PointWidth(branch, i), w2 = PointWidth(branch, i + 1);
                for (int s = i == 0 ? 0 : 1; s <= steps; s++) {
                    float t = (float)s / steps;
                    Vector3 center = CatmullRom(p0, p1, p2, p3, t);
                    center.y = Hermite(p1.y, p2.y, slopes[i], slopes[i + 1], t);
                    samples.Add(new PathSample { center = center, width = Mathf.Lerp(w1, w2, t) });
                }
                controlSamples.Add(samples.Count - 1);
            }

            return samples;
        }

        private void Weld(List<Vector3> positions, List<Face> faces, float distance) {
            Dictionary<Vector3Int, List<Vector3>> cells = new();
            Vector3Int Cell(Vector3 p) => new(Mathf.FloorToInt(p.x / distance), Mathf.FloorToInt(p.y / distance), Mathf.FloorToInt(p.z / distance));

            for (int i = 0; i < positions.Count; i++) {
                Vector3 p = positions[i];
                Vector3Int cell = Cell(p);
                Vector3? found = null;
                for (int x = -1; x <= 1 && found == null; x++)
                for (int y = -1; y <= 1 && found == null; y++)
                for (int z = -1; z <= 1 && found == null; z++)
                    if (cells.TryGetValue(cell + new Vector3Int(x, y, z), out List<Vector3> nearby))
                        foreach (Vector3 other in nearby)
                            if ((other - p).sqrMagnitude <= distance * distance) {
                                found = other;
                                break;
                            }

                if (found is { } snapped) {
                    positions[i] = snapped;
                    continue;
                }

                if (!cells.TryGetValue(cell, out List<Vector3> list))
                    cells[cell] = list = new List<Vector3>();
                list.Add(p);
            }

            for (int f = faces.Count - 1; f >= 0; f--) {
                IList<int> indexes = faces[f].indexes;
                List<int> kept = new();
                for (int t = 0; t + 2 < indexes.Count; t += 3) {
                    Vector3 a = positions[indexes[t]], b = positions[indexes[t + 1]], c = positions[indexes[t + 2]];
                    if (Vector3.Cross(b - a, c - a).sqrMagnitude > 1e-12f)
                        kept.AddRange(new[] { indexes[t], indexes[t + 1], indexes[t + 2] });
                }

                if (kept.Count == indexes.Count)
                    continue;
                if (kept.Count == 0) {
                    faces.RemoveAt(f);
                    if (f < _faceRegions.Count)
                        _faceRegions.RemoveAt(f);
                    continue;
                }

                faces[f] = new Face(kept) { manualUV = true, smoothingGroup = faces[f].smoothingGroup };
            }
        }

        private static float[] HeightSlopes(List<Vector3> path) {
            int count = path.Count;
            float[] rise = new float[count - 1];
            for (int k = 0; k < count - 1; k++)
                rise[k] = path[k + 1].y - path[k].y;

            float[] slopes = new float[count];
            slopes[0] = rise[0];
            slopes[^1] = rise[^1];
            for (int k = 1; k < count - 1; k++)
                slopes[k] = rise[k - 1] * rise[k] <= 0f ? 0f : (rise[k - 1] + rise[k]) * 0.5f;

            for (int k = 0; k < count - 1; k++) {
                if (Mathf.Abs(rise[k]) < Epsilon) {
                    slopes[k] = 0f;
                    slopes[k + 1] = 0f;
                    continue;
                }

                float a = slopes[k] / rise[k], b = slopes[k + 1] / rise[k], length = a * a + b * b;
                if (length <= 9f)
                    continue;
                float scale = 3f / Mathf.Sqrt(length);
                slopes[k] = scale * a * rise[k];
                slopes[k + 1] = scale * b * rise[k];
            }

            return slopes;
        }

        private static float Hermite(float y1, float y2, float m1, float m2, float t) {
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * y1 + (t3 - 2f * t2 + t) * m1 + (-2f * t3 + 3f * t2) * y2 + (t3 - t2) * m2;
        }

        public bool ClosestOnCurve(List<List<PathSample>> sampled, int skip, Vector3 point, out int branch, out int segment, out PathSample hit) =>
            ClosestOnCurve(sampled, new HashSet<int> { skip }, point, out branch, out segment, out hit);

        private static bool ClosestOnCurve(List<List<PathSample>> sampled, HashSet<int> skip, Vector3 point, out int branch, out int segment,
            out PathSample hit) {
            branch = segment = -1;
            hit = default;
            float best = float.MaxValue;
            for (int b = 0; b < sampled.Count; b++) {
                if (skip.Contains(b))
                    continue;
                List<PathSample> samples = sampled[b];
                for (int k = 0; k + 1 < samples.Count; k++) {
                    Vector3 a = samples[k].center, ab = samples[k + 1].center - a;
                    float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                    Vector3 on = a + ab * t;
                    float width = Mathf.Lerp(samples[k].width, samples[k + 1].width, t);
                    float distance = Vector3.Distance(point, on);
                    if (distance >= best || distance > width * CurveJoinWidthFraction)
                        continue;
                    best = distance;
                    branch = b;
                    segment = k;
                    hit = new PathSample { center = on, width = width };
                }
            }

            return branch >= 0;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        public static Vector3Int JunctionKey(Vector3 point) =>
            new(Mathf.RoundToInt(point.x / JunctionTolerance), Mathf.RoundToInt(point.y / JunctionTolerance), Mathf.RoundToInt(point.z / JunctionTolerance));

        public Dictionary<Vector3Int, List<(int branch, int point)>> Junctions() {
            Dictionary<Vector3Int, List<(int branch, int point)>> occurrences = new();
            List<PathBranch> all = activeBranches;
            for (int b = 0; b < all.Count; b++) {
                if (all[b]?.path is not { Count: >= 2 })
                    continue;
                for (int i = 0; i < all[b].path.Count; i++) {
                    Vector3Int key = JunctionKey(all[b].path[i]);
                    if (!occurrences.TryGetValue(key, out List<(int, int)> list))
                        occurrences[key] = list = new List<(int, int)>();
                    list.Add((b, i));
                }
            }

            return occurrences.Where(o => o.Value.Count >= 2).ToDictionary(o => o.Key, o => o.Value);
        }

        private sealed class Arm {
            public List<PathSample> samples;
            public bool atStart;
            public Vector3 direction;
            public Vector3 left, right;
            public float along, radius;
            public int branch;
            public bool built;
            public Vector3[] edgeLeft, edgeRight;
            public Clip clip;
            public readonly HashSet<int> hiddenLeft = new(), hiddenRight = new();
            public readonly Dictionary<int, Vector3> pinLeft = new(), pinRight = new();
        }

        private sealed class Clip {
            public Vector3[] mainEdge;
            public Vector3[] left, right;
            public float[] distance;
            public Vector3 crossLeft, crossRight;
            public float leftAt, rightAt;
            public int mainLeft, mainRight;
            public float mainLeftT, mainRightT;
            public bool rightSide;
        }

        private sealed class Junction {
            public Vector3 point;
            public readonly List<Arm> arms = new();
            public Arm mainEnd, mainStart;
        }

        private sealed class Piece {
            public List<PathSample> samples;
            public float startDistance;
            public Junction start, end;
            public Arm startArm, endArm;
        }

        private void Build(List<Vector3> positions, List<Face> faces) {
            List<List<PathSample>> sampled = activeBranches.Where(b => b?.path is { Count: >= 2 }).Select(b => SampleBranch(b, out _)).ToList();
            List<(int branch, int index)> ends = new();
            for (int b = 0; b < sampled.Count; b++) {
                ends.Add((b, 0));
                ends.Add((b, sampled[b].Count - 1));
            }

            foreach ((int branch, int index) end in ends) {
                Vector3 point = sampled[end.branch][end.index].center;
                Vector3Int key = JunctionKey(point);
                HashSet<int> through = new(Enumerable.Range(0, sampled.Count).Where(b => sampled[b].Any(x => JunctionKey(x.center) == key)));
                if (!ClosestOnCurve(sampled, through, point, out int other, out int segment, out PathSample hit))
                    continue;

                List<PathSample> target = sampled[other];
                int index = (hit.center - target[segment].center).sqrMagnitude < JunctionTolerance * JunctionTolerance ? segment
                    : (hit.center - target[segment + 1].center).sqrMagnitude < JunctionTolerance * JunctionTolerance ? segment + 1
                    : -1;
                if (index < 0) {
                    target.Insert(segment + 1, hit);
                    index = segment + 1;
                }

                Vector3 at = target[index].center;
                for (int b = 0; b < sampled.Count; b++)
                    foreach (int i in new[] { 0, sampled[b].Count - 1 })
                        if (JunctionKey(sampled[b][i].center) == key)
                            sampled[b][i] = new PathSample { center = at, width = sampled[b][i].width };
            }

            HashSet<Vector3Int> keys = new(sampled
                .SelectMany((samples, b) => samples.Select((x, i) => (key: JunctionKey(x.center), b, i)))
                .GroupBy(x => x.key)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key));

            Dictionary<Vector3Int, Junction> junctions = keys.ToDictionary(k => k, _ => new Junction());
            junctionCount = junctions.Count;

            List<Piece> pieces = new();
            for (int branchIndex = 0; branchIndex < sampled.Count; branchIndex++) {
                List<PathSample> samples = sampled[branchIndex];
                float[] distance = Distances(samples);
                List<int> cuts = new();
                for (int i = 0; i < samples.Count; i++)
                    if (i == 0 || i == samples.Count - 1 || junctions.ContainsKey(JunctionKey(samples[i].center)))
                        cuts.Add(i);

                for (int c = 0; c + 1 < cuts.Count; c++) {
                    int from = cuts[c], to = cuts[c + 1];
                    Piece piece = new() { samples = samples.GetRange(from, to - from + 1), startDistance = distance[from] };
                    if (junctions.TryGetValue(JunctionKey(samples[from].center), out Junction startJunction))
                        piece.startArm = AddArm(piece.start = startJunction, samples[from].center, piece.samples, true, branchIndex);
                    if (junctions.TryGetValue(JunctionKey(samples[to].center), out Junction endJunction))
                        piece.endArm = AddArm(piece.end = endJunction, samples[to].center, piece.samples, false, branchIndex);
                    pieces.Add(piece);
                }
            }

            foreach (Junction junction in junctions.Values)
                ArmRadii(junction);

            float top = Mathf.Max(0f, activeHeight), bottom = Mathf.Min(0f, activeHeight);
            foreach (Piece piece in pieces) {
                List<PathSample> trimmed = Trim(piece.samples, piece.startArm?.radius ?? 0f, piece.endArm?.radius ?? 0f);
                if (trimmed == null)
                    continue;

                float startAlong = piece.startDistance + (piece.startArm?.radius ?? 0f);
                Dictionary<int, Vector3> pinLeft = new(), pinRight = new();
                foreach (Arm arm in new[] { piece.startArm, piece.endArm }.Where(a => a != null)) {
                    foreach (KeyValuePair<int, Vector3> pin in arm.pinLeft)
                        pinLeft[pin.Key] = pin.Value;
                    foreach (KeyValuePair<int, Vector3> pin in arm.pinRight)
                        pinRight[pin.Key] = pin.Value;
                }
                HashSet<int> hideLeft = new((piece.startArm?.hiddenLeft ?? new HashSet<int>()).Concat(piece.endArm?.hiddenLeft ?? new HashSet<int>()));
                HashSet<int> hideRight = new((piece.startArm?.hiddenRight ?? new HashSet<int>()).Concat(piece.endArm?.hiddenRight ?? new HashSet<int>()));
                Ribbon(positions, faces, trimmed, startAlong, top, bottom, piece.startArm == null, piece.endArm == null,
                    out (Vector3 left, Vector3 right) startCorners, out (Vector3 left, Vector3 right) endCorners, out Vector3[] edgeLeft,
                    out Vector3[] edgeRight, hideLeft, hideRight, piece.startArm?.radius > 0f || piece.endArm?.radius > 0f ? null : pinLeft,
                    piece.startArm?.radius > 0f || piece.endArm?.radius > 0f ? null : pinRight);
                foreach (Arm arm in new[] { piece.startArm, piece.endArm }.Where(a => a != null)) {
                    arm.edgeLeft = edgeLeft;
                    arm.edgeRight = edgeRight;
                }
                if (piece.startArm != null)
                    Settle(piece.startArm, startCorners, startAlong);
                if (piece.endArm != null)
                    Settle(piece.endArm, endCorners, startAlong + Distances(trimmed)[^1]);
            }

            foreach (Junction junction in junctions.Values)
                JunctionMesh(positions, faces, junction, top, bottom);
        }

        private static Arm AddArm(Junction junction, Vector3 point, List<PathSample> samples, bool atStart, int branch) {
            junction.point = point;
            Vector3 next = atStart ? samples[Mathf.Min(1, samples.Count - 1)].center : samples[Mathf.Max(0, samples.Count - 2)].center;
            Vector3 direction = Flat(next - point);
            Arm arm = new() { samples = samples, atStart = atStart, branch = branch, direction = direction == Vector3.zero ? Vector3.forward : direction };
            junction.arms.Add(arm);
            return arm;
        }

        private static float HalfWidth(Arm arm) => (arm.atStart ? arm.samples[0].width : arm.samples[^1].width) * 0.5f;

        private static float Heading(Vector3 direction) => Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;

        private static void EdgeLines(List<PathSample> samples, out Vector3[] left, out Vector3[] right,
            Dictionary<int, Vector3> pinLeft = null, Dictionary<int, Vector3> pinRight = null) {
            left = new Vector3[samples.Count];
            right = new Vector3[samples.Count];
            for (int i = 0; i < samples.Count; i++) {
                Vector3 offset = Side(samples, i) * (samples[i].width * 0.5f);
                left[i] = samples[i].center - offset;
                right[i] = samples[i].center + offset;
            }
            Unfold(left);
            Unfold(right);
            foreach (KeyValuePair<int, Vector3> pin in pinLeft ?? new Dictionary<int, Vector3>())
                if (pin.Key < left.Length)
                    left[pin.Key] = pin.Value;
            foreach (KeyValuePair<int, Vector3> pin in pinRight ?? new Dictionary<int, Vector3>())
                if (pin.Key < right.Length)
                    right[pin.Key] = pin.Value;
        }

        private static int Nearest(Vector3[] points, Vector3 target) {
            int best = 0;
            for (int i = 1; i < points.Length; i++)
                if ((points[i] - target).sqrMagnitude < (points[best] - target).sqrMagnitude)
                    best = i;
            return best;
        }

        private static Clip ClipArm(Arm arm, Vector3[] mainEdge) {
            EdgeLines(arm.samples, out Vector3[] left, out Vector3[] right);
            List<PathSample> samples = arm.samples;
            if (!arm.atStart) {
                Array.Reverse(left);
                Array.Reverse(right);
                samples = Enumerable.Reverse(samples).ToList();
            }

            float[] distance = Distances(samples);
            Clip clip = new() { mainEdge = mainEdge, left = left, right = right, distance = distance };
            if (!Exit(left, distance, mainEdge, out clip.crossLeft, out clip.leftAt, out clip.mainLeft, out clip.mainLeftT) ||
                !Exit(right, distance, mainEdge, out clip.crossRight, out clip.rightAt, out clip.mainRight, out clip.mainRightT))
                return null;
            return clip;
        }

        private static bool Exit(Vector3[] edge, float[] distance, Vector3[] mainEdge, out Vector3 cross, out float at, out int mainSegment,
            out float mainT) {
            cross = default;
            at = 0f;
            mainSegment = -1;
            mainT = 0f;
            for (int k = 0; k + 1 < edge.Length; k++)
            for (int m = 0; m + 1 < mainEdge.Length; m++) {
                if (!SegmentCross(edge[k], edge[k + 1], mainEdge[m], mainEdge[m + 1], out float t, out float u))
                    continue;
                mainSegment = m;
                mainT = u;
                cross = Vector3.Lerp(mainEdge[m], mainEdge[m + 1], u);
                at = Mathf.Lerp(distance[k], distance[k + 1], t);
                return true;
            }
            return false;
        }

        private static bool SegmentCross(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out float t, out float u) {
            t = u = 0f;
            Vector2 r = new(b.x - a.x, b.z - a.z), q = new(d.x - c.x, d.z - c.z), offset = new(c.x - a.x, c.z - a.z);
            float denominator = r.x * q.y - r.y * q.x;
            if (Mathf.Abs(denominator) < 1e-9f)
                return false;
            t = (offset.x * q.y - offset.y * q.x) / denominator;
            u = (offset.x * r.y - offset.y * r.x) / denominator;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        private static Vector3 PointAlong(Arm arm, float distance) {
            List<PathSample> samples = arm.samples;
            int count = samples.Count;
            Vector3 Sample(int k) => samples[arm.atStart ? k : count - 1 - k].center;

            float walked = 0f;
            for (int k = 1; k < count; k++) {
                float step = Vector3.Distance(Sample(k - 1), Sample(k));
                if (walked + step >= distance && step > Epsilon)
                    return Vector3.Lerp(Sample(k - 1), Sample(k), (distance - walked) / step);
                walked += step;
            }
            return Sample(count - 1);
        }

        private static void ArmRadii(Junction junction) {
            foreach (Arm arm in junction.arms)
                arm.radius = HalfWidth(arm);
            if (junction.arms.Count < 2)
                return;

            (Arm end, Arm start) main = junction.arms
                .Where(a => !a.atStart)
                .SelectMany(a => junction.arms.Where(b => b.atStart && b.branch == a.branch).Select(b => (a, b)))
                .OrderByDescending(pair => HalfWidth(pair.a) + HalfWidth(pair.b))
                .FirstOrDefault();
            if (main.end != null && junction.arms.Count > 2) {
                junction.mainEnd = main.end;
                junction.mainStart = main.start;
                main.end.radius = main.start.radius = 0f;

                Vector3 tangent = Flat(PointAlong(main.start, HalfWidth(main.start)) - PointAlong(main.end, HalfWidth(main.end)));
                if (tangent == Vector3.zero)
                    tangent = -main.end.direction;
                EdgeLines(main.end.samples, out Vector3[] endLeft, out Vector3[] endRight);
                EdgeLines(main.start.samples, out Vector3[] startLeft, out Vector3[] startRight);
                Vector3[] mainLeft = endLeft.Concat(startLeft.Skip(1)).ToArray(), mainRight = endRight.Concat(startRight.Skip(1)).ToArray();
                int endSegments = endLeft.Length - 1;

                List<Arm> sides = junction.arms.Where(a => a != main.end && a != main.start).ToList();
                List<(List<PathSample> samples, int segment, float t)> splits = new();
                List<(Arm arm, bool left, List<PathSample> samples, PathSample sample, Vector3 point)> pins = new();
                foreach (Arm arm in sides) {
                    Vector3 away = Flat(PointAlong(arm, HalfWidth(arm) * 2f) - junction.point);
                    if (away != Vector3.zero)
                        arm.direction = away;
                    bool rightSide = Vector3.Dot(arm.direction, Vector3.Cross(Vector3.up, tangent)) > 0f;
                    arm.clip = ClipArm(arm, rightSide ? mainRight : mainLeft);
                    arm.radius = arm.clip == null ? HalfWidth(arm) : Mathf.Max(arm.clip.leftAt, arm.clip.rightAt) + ConnectorDepth;
                    if (arm.clip == null)
                        continue;

                    arm.clip.rightSide = rightSide;
                    foreach ((int segment, float t, bool isLeft) in new[] { (arm.clip.mainLeft, arm.clip.mainLeftT, true), (arm.clip.mainRight, arm.clip.mainRightT, false) }) {
                        Arm owner = segment < endSegments ? main.end : main.start;
                        int local = segment < endSegments ? segment : segment - endSegments;
                        splits.Add((owner.samples, local, t));
                        PathSample a = owner.samples[local], b = owner.samples[local + 1];
                        PathSample marker = t <= 1e-3f ? a : t >= 1f - 1e-3f ? b
                            : new PathSample { center = Vector3.Lerp(a.center, b.center, t), width = Mathf.Lerp(a.width, b.width, t) };
                        pins.Add((owner, isLeft, owner.samples, marker, isLeft ? arm.clip.crossLeft : arm.clip.crossRight));
                    }
                }

                foreach (var group in splits.GroupBy(x => x.samples))
                foreach ((List<PathSample> samples, int segment, float t) in group.OrderByDescending(x => x.segment).ThenByDescending(x => x.t)) {
                    if (t <= 1e-3f || t >= 1f - 1e-3f)
                        continue;
                    PathSample a = samples[segment], b = samples[segment + 1];
                    samples.Insert(segment + 1, new PathSample { center = Vector3.Lerp(a.center, b.center, t), width = Mathf.Lerp(a.width, b.width, t) });
                }

                foreach ((Arm owner, bool isLeft, List<PathSample> samples, PathSample marker, Vector3 point) in pins) {
                    int index = samples.FindIndex(x => (x.center - marker.center).sqrMagnitude < 1e-12f);
                    if (index < 0)
                        continue;
                    bool rightSide = sides.First(a => a.clip != null && (a.clip.crossLeft == point || a.clip.crossRight == point)).clip.rightSide;
                    (rightSide ? owner.pinRight : owner.pinLeft)[index] = point;
                }

                EdgeLines(main.end.samples, out endLeft, out endRight, main.end.pinLeft, main.end.pinRight);
                EdgeLines(main.start.samples, out startLeft, out startRight, main.start.pinLeft, main.start.pinRight);
                mainLeft = endLeft.Concat(startLeft.Skip(1)).ToArray();
                mainRight = endRight.Concat(startRight.Skip(1)).ToArray();
                endSegments = endLeft.Length - 1;

                foreach (Arm arm in sides.Where(a => a.clip != null)) {
                    Clip clip = arm.clip;
                    clip.mainEdge = clip.rightSide ? mainRight : mainLeft;
                    clip.mainLeft = Nearest(clip.mainEdge, clip.crossLeft);
                    clip.mainRight = Nearest(clip.mainEdge, clip.crossRight);
                    clip.crossLeft = clip.mainEdge[clip.mainLeft];
                    clip.crossRight = clip.mainEdge[clip.mainRight];

                    for (int k = Mathf.Min(clip.mainLeft, clip.mainRight); k < Mathf.Max(clip.mainLeft, clip.mainRight); k++)
                        if (k < endSegments)
                            (clip.rightSide ? main.end.hiddenRight : main.end.hiddenLeft).Add(k);
                        else
                            (clip.rightSide ? main.start.hiddenRight : main.start.hiddenLeft).Add(k - endSegments);
                }
                return;
            }

            for (int pass = 0; pass < 4; pass++) {
                foreach (Arm arm in junction.arms) {
                    Vector3 direction = Flat(PointAlong(arm, arm.radius) - junction.point);
                    if (direction != Vector3.zero)
                        arm.direction = direction;
                }
                SpreadRadii(junction);
            }
        }

        private static void SpreadRadii(Junction junction) {
            List<Arm> arms = junction.arms.OrderBy(a => Heading(a.direction)).ToList();
            foreach (Arm arm in arms)
                arm.radius = HalfWidth(arm);

            for (int i = 0; i < arms.Count; i++) {
                Arm arm = arms[i], next = arms[(i + 1) % arms.Count];
                float gap = Mathf.Repeat(Heading(next.direction) - Heading(arm.direction), 360f);
                if (arms.Count == 2)
                    gap = Mathf.Min(gap, 360f - gap);
                gap = Mathf.Max(gap, MinJunctionGap);
                float needed = Mathf.Max(HalfWidth(arm), HalfWidth(next)) / Mathf.Tan(gap * 0.5f * Mathf.Deg2Rad);
                arm.radius = Mathf.Max(arm.radius, needed);
                next.radius = Mathf.Max(next.radius, needed);
            }
        }

        private static void Settle(Arm arm, (Vector3 left, Vector3 right) corners, float along) {
            arm.left = corners.left;
            arm.right = corners.right;
            arm.along = along;
            arm.built = true;
        }

        private static List<PathSample> Trim(List<PathSample> samples, float startCut, float endCut) {
            float[] distance = new float[samples.Count];
            for (int i = 1; i < samples.Count; i++)
                distance[i] = distance[i - 1] + Vector3.Distance(samples[i - 1].center, samples[i].center);

            float length = distance[^1];
            if (length - startCut - endCut < Epsilon * 10f)
                return null;

            PathSample At(float d) {
                for (int i = 1; i < samples.Count; i++)
                    if (distance[i] >= d) {
                        float span = distance[i] - distance[i - 1];
                        float t = span > Epsilon ? (d - distance[i - 1]) / span : 0f;
                        return new PathSample {
                            center = Vector3.Lerp(samples[i - 1].center, samples[i].center, t),
                            width = Mathf.Lerp(samples[i - 1].width, samples[i].width, t)
                        };
                    }
                return samples[^1];
            }

            float from = startCut, to = length - endCut;
            float gap = Mathf.Max(Epsilon, length / Mathf.Max(1, samples.Count - 1) * 0.33f);
            List<PathSample> result = new() { At(from) };
            for (int i = 0; i < samples.Count; i++)
                if (distance[i] > from + (startCut > 0f ? gap : Epsilon) && distance[i] < to - (endCut > 0f ? gap : Epsilon))
                    result.Add(samples[i]);
            result.Add(At(to));
            return result;
        }

        private static Vector3 Side(List<PathSample> samples, int i) {
            Vector3 forward = Flat(samples[Mathf.Min(i + 1, samples.Count - 1)].center - samples[Mathf.Max(i - 1, 0)].center);
            return forward == Vector3.zero ? Vector3.right : Vector3.Cross(Vector3.up, forward);
        }

        private static float[] Distances(List<PathSample> samples) {
            float[] distance = new float[samples.Count];
            for (int i = 1; i < samples.Count; i++)
                distance[i] = distance[i - 1] + Vector3.Distance(samples[i - 1].center, samples[i].center);
            return distance;
        }

        private void Ribbon(List<Vector3> positions, List<Face> faces, List<PathSample> samples, float startDistance, float top, float bottom,
            bool capStart, bool capEnd, out (Vector3 left, Vector3 right) startCorners, out (Vector3 left, Vector3 right) endCorners,
            out Vector3[] edgeLeft, out Vector3[] edgeRight, HashSet<int> hideLeft, HashSet<int> hideRight, Dictionary<int, Vector3> pinLeft,
            Dictionary<int, Vector3> pinRight) {
            int count = samples.Count;
            float[] along = Distances(samples).Select(d => d + startDistance).ToArray();
            float[] half = samples.Select(x => x.width * 0.5f).ToArray();
            Vector3[] sides = new Vector3[count];
            Vector3[] left = new Vector3[count], right = new Vector3[count];
            for (int i = 0; i < count; i++) {
                sides[i] = Side(samples, i);
                Vector3 offset = sides[i] * (samples[i].width * 0.5f);
                left[i] = samples[i].center - offset;
                right[i] = samples[i].center + offset;
            }

            Unfold(left);
            Unfold(right);
            foreach (KeyValuePair<int, Vector3> pin in pinLeft ?? new Dictionary<int, Vector3>())
                if (pin.Key < count)
                    left[pin.Key] = pin.Value;
            foreach (KeyValuePair<int, Vector3> pin in pinRight ?? new Dictionary<int, Vector3>())
                if (pin.Key < count)
                    right[pin.Key] = pin.Value;

            Vector3[] leftTop = left.Select(p => p + Vector3.up * top).ToArray(), rightTop = right.Select(p => p + Vector3.up * top).ToArray();
            Vector3[] leftBottom = left.Select(p => p + Vector3.up * bottom).ToArray(), rightBottom = right.Select(p => p + Vector3.up * bottom).ToArray();

            for (int i = 0; i < count - 1; i++) {
                int j = i + 1;
                AddFace(positions, faces, Vector3.up, TopRegion,
                    (leftTop[i], new Vector2(-half[i], along[i])), (rightTop[i], new Vector2(half[i], along[i])),
                    (rightTop[j], new Vector2(half[j], along[j])), (leftTop[j], new Vector2(-half[j], along[j])));
                AddFace(positions, faces, Vector3.down, BottomRegion,
                    (leftBottom[i], new Vector2(half[i], along[i])), (rightBottom[i], new Vector2(-half[i], along[i])),
                    (rightBottom[j], new Vector2(-half[j], along[j])), (leftBottom[j], new Vector2(half[j], along[j])));
                if (!hideLeft.Contains(i))
                    AddFace(positions, faces, -(sides[i] + sides[j]), SideRegion,
                        (leftTop[i], new Vector2(-along[i], top)), (leftTop[j], new Vector2(-along[j], top)),
                        (leftBottom[j], new Vector2(-along[j], bottom)), (leftBottom[i], new Vector2(-along[i], bottom)));
                if (!hideRight.Contains(i))
                    AddFace(positions, faces, sides[i] + sides[j], SideRegion,
                        (rightTop[i], new Vector2(along[i], top)), (rightTop[j], new Vector2(along[j], top)),
                        (rightBottom[j], new Vector2(along[j], bottom)), (rightBottom[i], new Vector2(along[i], bottom)));
            }

            Vector3 startForward = Vector3.Cross(sides[0], Vector3.up), endForward = Vector3.Cross(sides[^1], Vector3.up);
            if (capStart)
                AddFace(positions, faces, -startForward, SideRegion,
                    (leftTop[0], new Vector2(half[0], top)), (rightTop[0], new Vector2(-half[0], top)),
                    (rightBottom[0], new Vector2(-half[0], bottom)), (leftBottom[0], new Vector2(half[0], bottom)));
            if (capEnd)
                AddFace(positions, faces, endForward, SideRegion,
                    (leftTop[^1], new Vector2(-half[^1], top)), (rightTop[^1], new Vector2(half[^1], top)),
                    (rightBottom[^1], new Vector2(half[^1], bottom)), (leftBottom[^1], new Vector2(-half[^1], bottom)));

            startCorners = (left[0], right[0]);
            endCorners = (left[^1], right[^1]);
            edgeLeft = left;
            edgeRight = right;
        }

        private static void Unfold(Vector3[] edge) {
            for (int a = 0; a < edge.Length - 1; a++) {
                if ((edge[a + 1] - edge[a]).sqrMagnitude < Epsilon * Epsilon)
                    continue;

                for (int b = edge.Length - 2; b >= a + 2; b--) {
                    if ((edge[b + 1] - edge[b]).sqrMagnitude < Epsilon * Epsilon)
                        continue;
                    if (!Crossing(edge[a], edge[a + 1], edge[b], edge[b + 1], out Vector3 point))
                        continue;

                    for (int k = a + 1; k <= b; k++)
                        edge[k] = point;
                    break;
                }
            }
        }

        private static bool Crossing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 point) {
            point = default;
            Vector2 r = new(b.x - a.x, b.z - a.z), s = new(d.x - c.x, d.z - c.z), qp = new(c.x - a.x, c.z - a.z);
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 1e-9f)
                return false;

            float t = (qp.x * s.y - qp.y * s.x) / denominator, u = (qp.x * r.y - qp.y * r.x) / denominator;
            if (t < -Epsilon || t > 1f + Epsilon || u < -Epsilon || u > 1f + Epsilon)
                return false;

            Vector3 onFirst = Vector3.Lerp(a, b, Mathf.Clamp01(t)), onSecond = Vector3.Lerp(c, d, Mathf.Clamp01(u));
            point = new Vector3(onFirst.x, (onFirst.y + onSecond.y) * 0.5f, onFirst.z);
            return true;
        }

        private void JunctionMesh(List<Vector3> positions, List<Face> faces, Junction junction, float top, float bottom) {
            List<Arm> arms = junction.arms.Where(a => a.built).ToList();
            if (junction.mainEnd is { built: true } && junction.mainStart is { built: true }) {
                foreach (Arm arm in arms.Where(a => a != junction.mainEnd && a != junction.mainStart))
                    Connector(positions, faces, junction, arm, top, bottom);
                return;
            }

            if (arms.Count < 2) {
                foreach (Arm arm in arms)
                    Cap(positions, faces, arm, top, bottom);
                return;
            }

            Vector3 point = junction.point;
            float Angle(Vector3 p) => Mathf.Atan2(p.z - point.z, p.x - point.x) * Mathf.Rad2Deg;

            List<(Vector3 corner, Arm arm)> corners = new();
            foreach (Arm arm in arms.OrderBy(a => Angle(point + a.direction))) {
                float heading = Angle(point + arm.direction);
                bool leftFirst = Mathf.DeltaAngle(heading, Angle(arm.left)) < Mathf.DeltaAngle(heading, Angle(arm.right));
                corners.Add((leftFirst ? arm.left : arm.right, arm));
                corners.Add((leftFirst ? arm.right : arm.left, arm));
            }

            List<(Vector3 corner, Arm arm)> ring = new();
            for (int i = 0; i < corners.Count; i++) {
                ring.Add(corners[i]);
                (Vector3 corner, Arm arm) next = corners[(i + 1) % corners.Count];
                if (corners[i].arm != next.arm && InnerCorner(corners[i], next, point, out Vector3 inner))
                    ring.Add((inner, null));
            }
            Func<Vector3, Vector2> topUv = JunctionUv(arms, point);
            Vector2 BottomUv(Vector3 p) {
                Vector2 uv = topUv(p);
                return new Vector2(-uv.x, uv.y);
            }

            Vector3 Up(Vector3 p, float offset) => p + Vector3.up * offset;
            Vector3[] topRing = ring.Select(c => Up(c.corner, top)).ToArray();
            Vector3[] bottomRing = ring.Select(c => Up(c.corner, bottom)).ToArray();
            AddFan(positions, faces, Up(point, top), topRing, Vector3.up, TopRegion, topUv);
            AddFan(positions, faces, Up(point, bottom), bottomRing, Vector3.down, BottomRegion, BottomUv);

            for (int i = 0; i < ring.Count; i++) {
                int next = (i + 1) % ring.Count;
                if (ring[i].arm != null && ring[i].arm == ring[next].arm)
                    continue;

                Vector3 outward = Vector3.Cross(Vector3.up, ring[next].corner - ring[i].corner);
                if (Vector3.Dot(outward, (ring[i].corner + ring[next].corner) * 0.5f - point) < 0f)
                    outward = -outward;
                float span = Vector2.Distance(new Vector2(ring[i].corner.x, ring[i].corner.z), new Vector2(ring[next].corner.x, ring[next].corner.z));
                AddFace(positions, faces, outward, SideRegion,
                    (topRing[i], new Vector2(0f, top)), (topRing[next], new Vector2(span, top)),
                    (bottomRing[next], new Vector2(span, bottom)), (bottomRing[i], new Vector2(0f, bottom)));
            }
        }

        private void Connector(List<Vector3> positions, List<Face> faces, Junction junction, Arm arm, float top, float bottom) {
            Clip clip = arm.clip;
            if (clip == null) {
                Cap(positions, faces, arm, top, bottom);
                return;
            }

            float cut = arm.radius;
            List<Vector3> leftChain = new() { arm.left };
            for (int k = clip.distance.Length - 1; k >= 0; k--)
                if (clip.distance[k] < cut - 1e-4f && clip.distance[k] > clip.leftAt + 1e-4f)
                    leftChain.Add(clip.left[k]);
            leftChain.Add(clip.crossLeft);

            List<Vector3> rightChain = new() { clip.crossRight };
            for (int k = 0; k < clip.distance.Length; k++)
                if (clip.distance[k] > clip.rightAt + 1e-4f && clip.distance[k] < cut - 1e-4f)
                    rightChain.Add(clip.right[k]);
            rightChain.Add(arm.right);

            List<Vector3> mainPart = new();
            if (clip.mainLeft < clip.mainRight)
                for (int k = clip.mainLeft + 1; k < clip.mainRight; k++)
                    mainPart.Add(clip.mainEdge[k]);
            else
                for (int k = clip.mainLeft - 1; k > clip.mainRight; k--)
                    mainPart.Add(clip.mainEdge[k]);

            List<Vector3> outline = leftChain.Concat(mainPart).Concat(rightChain).ToList();
            Vector3 middle = (arm.left + arm.right) * 0.5f;
            Vector3 across = Flat(arm.right - arm.left);
            Vector3 lengthwise = arm.atStart ? Outward(arm, junction.point) : -Outward(arm, junction.point);
            Vector2 Uv(Vector3 p) => new(Vector3.Dot(Flat3(p - middle), across), arm.along + Vector3.Dot(Flat3(p - middle), lengthwise));

            AddPolygon(positions, faces, Vector3.up, TopRegion, outline.Select(p => (p + Vector3.up * top, Uv(p))).ToList());
            AddPolygon(positions, faces, Vector3.down, BottomRegion, outline.Select(p => {
                Vector2 uv = Uv(p);
                return (p + Vector3.up * bottom, new Vector2(-uv.x, uv.y));
            }).ToList());

            Vector3 centre = outline.Aggregate(Vector3.zero, (sum, p) => sum + p) / outline.Count;
            foreach (List<Vector3> chain in new[] { leftChain, rightChain })
                for (int k = 0; k + 1 < chain.Count; k++) {
                    Vector3 from = chain[k], to = chain[k + 1];
                    Vector3 outward = Vector3.Cross(Vector3.up, to - from);
                    if (Vector3.Dot(outward, (from + to) * 0.5f - centre) < 0f)
                        outward = -outward;
                    float span = FlatDistance(from, to);
                    AddFace(positions, faces, outward, SideRegion,
                        (from + Vector3.up * top, new Vector2(0f, top)), (to + Vector3.up * top, new Vector2(span, top)),
                        (to + Vector3.up * bottom, new Vector2(span, bottom)), (from + Vector3.up * bottom, new Vector2(0f, bottom)));
                }
        }

        private void AddPolygon(List<Vector3> positions, List<Face> faces, Vector3 facing, int region, List<(Vector3 point, Vector2 uv)> corners) {
            List<(Vector3 point, Vector2 uv)> unique = new();
            foreach ((Vector3 point, Vector2 uv) corner in corners)
                if (unique.Count == 0 || (unique[^1].point - corner.point).sqrMagnitude > 1e-10f)
                    unique.Add(corner);
            if (unique.Count > 1 && (unique[0].point - unique[^1].point).sqrMagnitude <= 1e-10f)
                unique.RemoveAt(unique.Count - 1);
            if (unique.Count < 3)
                return;

            List<Vector2> flat = unique.Select(c => new Vector2(c.point.x, c.point.z)).ToList();
            float area = 0f;
            for (int i = 0; i < flat.Count; i++)
                area += flat[i].x * flat[(i + 1) % flat.Count].y - flat[(i + 1) % flat.Count].x * flat[i].y;
            if (Mathf.Abs(area) < 1e-8f)
                return;
            if (area < 0f) {
                unique.Reverse();
                flat.Reverse();
            }

            List<int> remaining = Enumerable.Range(0, flat.Count).ToList();
            List<int> triangles = new();
            int guard = flat.Count * flat.Count;
            while (remaining.Count > 3 && guard-- > 0) {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++) {
                    int a = remaining[(i - 1 + remaining.Count) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    Vector2 ab = flat[b] - flat[a], ac = flat[c] - flat[a];
                    if (ab.x * ac.y - ab.y * ac.x <= 1e-9f)
                        continue;
                    bool inside = false;
                    foreach (int other in remaining) {
                        if (other == a || other == b || other == c)
                            continue;
                        if (InTriangle(flat[other], flat[a], flat[b], flat[c])) {
                            inside = true;
                            break;
                        }
                    }
                    if (inside)
                        continue;
                    triangles.AddRange(new[] { a, b, c });
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped)
                    break;
            }
            if (remaining.Count == 3)
                triangles.AddRange(remaining);
            if (triangles.Count == 0)
                return;

            int start = positions.Count;
            positions.AddRange(unique.Select(c => c.point));
            _uvs.AddRange(unique.Select(c => c.uv));
            List<int> indices = new();
            for (int t = 0; t + 2 < triangles.Count; t += 3) {
                int a = start + triangles[t], b = start + triangles[t + 1], c = start + triangles[t + 2];
                Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                if (Vector3.Dot(normal, facing) < 0f)
                    (b, c) = (c, b);
                indices.AddRange(new[] { a, b, c });
            }

            faces.Add(new Face(indices) { manualUV = true });
            Tag(faces, region);
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) {
            float Cross(Vector2 o, Vector2 x, Vector2 y) => (x.x - o.x) * (y.y - o.y) - (x.y - o.y) * (y.x - o.x);
            float d1 = Cross(a, b, p), d2 = Cross(b, c, p), d3 = Cross(c, a, p);
            return d1 > 1e-9f && d2 > 1e-9f && d3 > 1e-9f;
        }

        private static Vector3 Outward(Arm arm, Vector3 point) {
            Vector3 outward = Vector3.Cross(Flat(arm.right - arm.left), Vector3.up);
            return Vector3.Dot(outward, (arm.left + arm.right) * 0.5f - point) < 0f ? -outward : outward;
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static bool InnerCorner((Vector3 corner, Arm arm) a, (Vector3 corner, Arm arm) b, Vector3 point, out Vector3 inner) {
            inner = default;
            Vector3 da = Outward(a.arm, point), db = Outward(b.arm, point), difference = b.corner - a.corner;
            float denominator = da.x * db.z - da.z * db.x;
            if (Mathf.Abs(denominator) < 1e-4f)
                return false;

            float ta = (difference.x * db.z - difference.z * db.x) / denominator;
            float tb = (difference.x * da.z - difference.z * da.x) / denominator;
            if (ta > -1e-3f || tb > -1e-3f)
                return false;

            Vector3 crossing = a.corner + da * ta;
            if (FlatDistance(crossing, point) > Mathf.Max(FlatDistance(a.corner, point), FlatDistance(b.corner, point)))
                return false;

            inner = new Vector3(crossing.x, (a.corner.y + b.corner.y) * 0.5f, crossing.z);
            return true;
        }

        private static Func<Vector3, Vector2> JunctionUv(List<Arm> arms, Vector3 point) {
            Arm widest = arms.OrderByDescending(a => FlatDistance(a.left, a.right)).First();
            Arm partner = arms.Where(a => a != widest && a.branch == widest.branch && a.atStart != widest.atStart)
                .OrderByDescending(a => FlatDistance(a.left, a.right)).FirstOrDefault();

            (Vector3 middle, Vector3 across, float along) Frame(Arm arm) => ((arm.left + arm.right) * 0.5f, Flat(arm.right - arm.left), arm.along);

            (Vector3 middle, Vector3 across, float along) first = Frame(widest);
            if (partner == null)
                return p => {
                    Vector3 lengthwise = Vector3.Cross(first.across, Vector3.up);
                    return new Vector2(Vector3.Dot(p - first.middle, first.across), first.along + Vector3.Dot(p - first.middle, lengthwise));
                };

            (Vector3 middle, Vector3 across, float along) second = Frame(partner);
            if (second.along < first.along)
                (first, second) = (second, first);

            Vector3 span = second.middle - first.middle;
            span.y = 0f;
            return p => {
                Vector3 flat = p;
                float t = span.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(Flat3(flat - first.middle), span) / span.sqrMagnitude) : 0f;
                Vector3 middle = default, across = default, lengthwise = default;
                for (int iteration = 0; iteration < 6; iteration++) {
                    middle = Vector3.Lerp(first.middle, second.middle, t);
                    across = Flat(Vector3.Lerp(first.across, second.across, t));
                    lengthwise = Vector3.Cross(across, Vector3.up);
                    float along = Vector3.Dot(Flat3(flat - middle), lengthwise);
                    float rate = Vector3.Dot(span, lengthwise);
                    if (Mathf.Abs(rate) < 1e-6f)
                        break;
                    t = Mathf.Clamp01(t + along / rate);
                }

                middle = Vector3.Lerp(first.middle, second.middle, t);
                across = Flat(Vector3.Lerp(first.across, second.across, t));
                lengthwise = Vector3.Cross(across, Vector3.up);
                return new Vector2(Vector3.Dot(Flat3(flat - middle), across),
                    Mathf.Lerp(first.along, second.along, t) + Vector3.Dot(Flat3(flat - middle), lengthwise));
            };
        }

        private static Vector3 Flat3(Vector3 v) => new(v.x, 0f, v.z);

        private void Cap(List<Vector3> positions, List<Face> faces, Arm arm, float top, float bottom) {
            Vector3 left = arm.left, right = arm.right;
            Vector3 outward = -arm.direction;
            float half = Vector2.Distance(new Vector2(left.x, left.z), new Vector2(right.x, right.z)) * 0.5f;
            AddFace(positions, faces, outward, SideRegion,
                (left + Vector3.up * top, new Vector2(-half, top)), (right + Vector3.up * top, new Vector2(half, top)),
                (right + Vector3.up * bottom, new Vector2(half, bottom)), (left + Vector3.up * bottom, new Vector2(-half, bottom)));
        }

        private void AddFan(List<Vector3> positions, List<Face> faces, Vector3 center, Vector3[] ring, Vector3 facing, int region, Func<Vector3, Vector2> uv) {
            int start = positions.Count;
            positions.Add(center);
            positions.AddRange(ring);
            _uvs.Add(uv(center));
            _uvs.AddRange(ring.Select(uv));

            List<int> indices = new();
            for (int i = 0; i < ring.Length; i++) {
                int a = start + 1 + i, b = start + 1 + (i + 1) % ring.Length;
                Vector3 normal = Vector3.Cross(positions[a] - center, positions[b] - center);
                if (normal.sqrMagnitude < 1e-12f)
                    continue;
                if (Vector3.Dot(normal, facing) < 0f)
                    (a, b) = (b, a);
                indices.AddRange(new[] { start, a, b });
            }

            if (indices.Count == 0) {
                positions.RemoveRange(start, ring.Length + 1);
                _uvs.RemoveRange(start, ring.Length + 1);
                return;
            }

            faces.Add(new Face(indices) { manualUV = true });
            Tag(faces, region);
        }

        private void AddFace(List<Vector3> positions, List<Face> faces, Vector3 facing, int region, params (Vector3 point, Vector2 uv)[] corners) {
            List<(Vector3 point, Vector2 uv)> unique = new();
            foreach ((Vector3 point, Vector2 uv) corner in corners)
                if (unique.Count == 0 || (unique[^1].point - corner.point).sqrMagnitude > 1e-10f)
                    unique.Add(corner);
            if (unique.Count > 1 && (unique[0].point - unique[^1].point).sqrMagnitude <= 1e-10f)
                unique.RemoveAt(unique.Count - 1);
            if (unique.Count < 3)
                return;

            (Vector3 point, Vector2 uv)[] points = unique.ToArray();
            Vector3 normal = Vector3.zero;
            for (int i = 2; i < points.Length; i++)
                normal += Vector3.Cross(points[i - 1].point - points[0].point, points[i].point - points[0].point);
            if (normal.sqrMagnitude < 1e-12f)
                return;
            if (Vector3.Dot(normal, facing) < 0f)
                Array.Reverse(points, 1, points.Length - 1);

            int start = positions.Count;
            positions.AddRange(points.Select(c => c.point));
            _uvs.AddRange(points.Select(c => c.uv));
            int[] indices = new int[(points.Length - 2) * 3];
            for (int i = 2; i < points.Length; i++) {
                indices[(i - 2) * 3] = start;
                indices[(i - 2) * 3 + 1] = start + i - 1;
                indices[(i - 2) * 3 + 2] = start + i;
            }

            faces.Add(new Face(indices) { manualUV = true });
            Tag(faces, region);
        }

        private void Tag(List<Face> faces, int region) {
            while (_faceRegions.Count < faces.Count - 1)
                _faceRegions.Add(-1);
            _faceRegions.Add(region);
        }

        private static Vector3 Flat(Vector3 v) {
            v.y = 0f;
            return v.sqrMagnitude > Epsilon * Epsilon ? v.normalized : Vector3.zero;
        }
    }
}
