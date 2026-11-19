using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Serialization;

using Tsitski.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    public enum WallAlignment {
        Inside,
        Center,
        Outside
    }

    [AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder Floor Plan")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class TsitskiProBuilderFloorPlan : MonoBehaviour, IPathOwner, IForcedMaterials, IInputHashed, IHeightOwner, IPivotShiftable {
        bool IPivotShiftable.CanShiftPivot(out string reason) {
            reason = "its outline is read from its Floor mesh.";
            return !floor;
        }

        void IPivotShiftable.ShiftLocalPoints(Vector3 offset) => path = PivotShift.Shifted(path, offset);

        void IPivotShiftable.ApplyPivotShift() => Rebuild();

        private const float Epsilon = 1e-4f;
        private const float HeightTolerance = 1e-3f;
        private const float UpDot = 0.999f;
        private const float CollinearTolerance = 1e-4f;
        private const float MinMiterDot = 0.25f;
        private const float MinSquareCornerSine = 0.05f;
        private const int NoSmoothing = 0;

        [Tooltip(Tooltips.FloorPlanStats)] public TsitskiProBuilderFloorPlanStats stats;

        [Header("Materials")]
        [Tooltip(Tooltips.InsideMaterial)] public Material insideMaterial;
        [Tooltip(Tooltips.OutsideMaterial)] public Material outsideMaterial;

        [Header("Path")]
        [Tooltip(Tooltips.Floor)] public ProBuilderMesh floor;
        [Tooltip(Tooltips.FloorPlanPath)] public List<Vector3> path = new() { Vector3.zero, new(4f, 0f, 0f), new(4f, 0f, 4f), new(0f, 0f, 4f) };
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.Closed)] public bool closed = true;

        [Header("Walls")]
        [Tooltip(Tooltips.WallHeight)] [Min(0.01f)] public float height = 3f;
        [Tooltip(Tooltips.WallThickness)] [Min(0.01f)] public float thickness = 0.2f;
        [Tooltip(Tooltips.Alignment)] public WallAlignment alignment;
        [Tooltip(Tooltips.WallsAroundHoles)] public bool wallsAroundHoles;
        [Tooltip(Tooltips.SquareCorners)] public bool squareCorners;

        [Header("Mesh")]
        [Tooltip(Tooltips.LoopCutStep)] [Min(0f)] public float loopCutStep;
        [Tooltip(Tooltips.VerticalLoopCutStep)] [Min(0f)] public float verticalLoopCutStep;
        [Tooltip(Tooltips.ApplyCutsToProBuilderMesh)] public bool applyCutsToProBuilderMesh;
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;

        [Header("Roof")]
        [Tooltip(Tooltips.FloorPlanRoofStats)] public TsitskiProBuilderRoofStats roofStats;

        [Header("Layers")]
        [Tooltip(Tooltips.FloorPlanLayers)] [FormerlySerializedAs("floorPlanOutlineSettings")] [FormerlySerializedAs("arrays")]
        [TsitskiPathLayers(true, typeof(TsitskiArrayStats), typeof(TsitskiProBuilderTrimStats))]
        public List<TsitskiPathLayer> layers = new();

        [SerializeField] [HideInInspector] private List<TsitskiProBuilderPillar> generatedPillars = new();
        [SerializeField] [HideInInspector] private List<TsitskiArray> generatedArrays = new();
        [SerializeField] [HideInInspector] private List<TsitskiProBuilderTrim> generatedTrims = new();
        [SerializeField] [HideInInspector] private List<TsitskiProBuilderRoof> generatedRoofs = new();
        [SerializeField] [HideInInspector] private List<string> generatedArrayKeys = new();
        [SerializeField] [HideInInspector] private List<string> generatedTrimKeys = new();
        [SerializeField] [HideInInspector] private List<string> generatedRoofKeys = new();
        [HideInInspector] public List<string> removedParts = new();

        public const string RoofKey = "Roof";

        private struct ArrayPlacement {
            public string key;
            public List<Vector3> path;
            public bool loop;
            public CornerSide side;
            public TsitskiArrayStats stats;
            public float pillarHeight; // 0 leaves the array's own height
        }

        private struct TrimPlacement {
            public string key;
            public List<Vector3> path;
            public bool loop;
            public bool interior;
            public TsitskiProBuilderTrimStats stats;
        }

        private readonly List<ArrayPlacement> _arrays = new();
        private readonly List<TrimPlacement> _trims = new();
        private readonly Dictionary<string, int> _keyCounts = new();
        private readonly List<(List<Vector3> points, bool loop)> _wallCenters = new();
        private const int NoRegion = -1;
        private const int InsideRegion = 0;
        private const int OutsideRegion = 1;
        private readonly List<int> _faceGroups = new();
        private readonly List<int> _faceRegions = new();
        private readonly List<(List<Vector2> points, float top)> _roofOutlines = new();
        private int _nextGroup;

        public IReadOnlyList<(List<Vector3> points, bool loop)> wallCenters => _wallCenters;

        public static readonly HashSet<TsitskiProBuilderFloorPlan> All = new();
        public static event Action<ProBuilderMesh> Rebuilt;

        public Material activeInsideMaterial => stats ? stats.insideMaterial : insideMaterial;
        public Material activeOutsideMaterial => stats ? stats.outsideMaterial : outsideMaterial;
        Material[] IForcedMaterials.forcedMaterials => new[] { activeInsideMaterial, activeOutsideMaterial };

#if UNITY_EDITOR
#endif

        List<Vector3> IPathOwner.localPath { get => path; set => path = value; }
        bool IPathOwner.pathInStats => false;

        public bool heightInStats => stats && stats.storeHeight;
        public bool thicknessInStats => stats && stats.storeThickness;
        public float activeHeight => Mathf.Max(0.01f, heightInStats ? stats.height : height);
        public float activeThickness => Mathf.Max(0.01f, thicknessInStats ? stats.thickness : thickness);
        public WallAlignment activeAlignment => stats ? stats.alignment : alignment;
        public bool activeWallsAroundHoles => stats ? stats.wallsAroundHoles : wallsAroundHoles;
        public bool activeSquareCorners => stats ? stats.squareCorners : squareCorners;
        public float activeLoopCutStep => stats ? stats.loopCutStep : loopCutStep;
        public float activeVerticalLoopCutStep => stats ? stats.verticalLoopCutStep : verticalLoopCutStep;
        public bool activeApplyCutsToProBuilderMesh => stats ? stats.applyCutsToProBuilderMesh : applyCutsToProBuilderMesh;
        public int activeRoundingSegments => Mathf.Max(1, stats ? stats.roundingSegments : roundingSegments);
        public TsitskiProBuilderRoofStats activeRoofStats => stats ? stats.roofStats : roofStats;
        public List<TsitskiPathLayer> activeLayers => (stats ? stats.layers : layers) ?? new List<TsitskiPathLayer>();

        UnityEngine.Object IHeightOwner.heightHolder => heightInStats ? stats : this;

        float IHeightOwner.ownerHeight {
            get => activeHeight;
            set {
                if (heightInStats)
                    stats.height = value;
                else
                    height = value;
            }
        }

        void IHeightOwner.ApplyHeight() {
            if (heightInStats)
                stats.NotifyChanged();
            else
                Rebuild();
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
            TsitskiPathLayer.Upgrade(this, layers);
            if (TryGetComponent(out ProBuilderMesh mesh) && mesh.faceCount == 0)
                mesh.Clear();

            All.Add(this);
            TsitskiProBuilderFloorPlanStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() {
            All.Remove(this);
            TsitskiProBuilderFloorPlanStats.Changed -= OnStatsChanged;
        }

        private void OnValidate() {
            TsitskiPathLayer.Upgrade(this, layers);
            QueueRebuild();
        }

        private void OnStatsChanged(TsitskiProBuilderFloorPlanStats changed) {
            if (changed == stats)
                QueueRebuild();
        }

        public void QueueRebuild() => RebuildQueue.Queue(this, Rebuild);

        [SerializeField] [HideInInspector] private int inputHash;
        int IInputHashed.inputHash { get => inputHash; set => inputHash = value; }

        public void Rebuild() {
            BuildGenerated();
            GeneratorInputs.Store(this);
        }

        private void BuildGenerated() {
            TsitskiSettings.AutoName(this, stats, "ProBuilder Floor Plan");

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            KeptFaceMaterials kept = KeptFaceMaterials.Capture(mesh);

            List<Vector3> positions = new();
            List<Face> faces = new();
            List<List<Vector2>> extraLoops = null;
            float extraBottom = 0f;
            CollectRemoved();
            _arrays.Clear();
            _keyCounts.Clear();
            _faceGroups.Clear();
            _faceRegions.Clear();
            _roofOutlines.Clear();
            _nextGroup = 0;
            _trims.Clear();
            _wallCenters.Clear();
            if (floor && floor != mesh)
                SyncPathFromFloor(out extraLoops, out extraBottom);
            Build(positions, faces, extraLoops, extraBottom);
            if (!activeApplyCutsToProBuilderMesh)
                FaceGroups.Merge(positions, faces, _faceGroups, faceData: _faceRegions);

            if (faces.Count == 0)
                mesh.Clear();
            else {
                mesh.RebuildWithPositionsAndFaces(positions, faces);
                kept?.Apply(mesh);
                ForcedMaterials.Apply(mesh, _faceRegions, activeInsideMaterial, activeOutsideMaterial);
            }
            mesh.ToMesh();
            mesh.Refresh();

            if (TryGetComponent(out MeshRenderer meshRenderer) && !meshRenderer.sharedMaterial)
                meshRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
            SyncOutlines();
            Rebuilt?.Invoke(mesh);
        }

        public bool CollectRemoved() =>
            GeneratedChildren.CollectRemoved(generatedArrays, generatedArrayKeys, removedParts) |
            GeneratedChildren.CollectRemoved(generatedTrims, generatedTrimKeys, removedParts) |
            GeneratedChildren.CollectRemoved(generatedRoofs, generatedRoofKeys, removedParts);

        private string NextKey(int layer, string kind) {
            string prefix = $"{layer}:{kind}";
            int count = _keyCounts.GetValueOrDefault(prefix);
            _keyCounts[prefix] = count + 1;
            return $"{prefix}:{count}";
        }

        public static string DescribeKey(string key, List<TsitskiPathLayer> layers) {
            if (key == RoofKey)
                return "Roof";

            string[] parts = key.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int layer) || !int.TryParse(parts[2], out int index))
                return key;

            TsitskiPathStats stats = layer < layers.Count ? layers[layer]?.stats : null;
            return $"Layer {layer} {parts[1]} {index + 1}{(stats ? $" ({stats.name})" : "")}";
        }

        private void SyncOutlines() {
            GeneratedChildren.Resize(this, generatedPillars, 0, "Corner Pillar");
            _arrays.RemoveAll(placement => removedParts.Contains(placement.key));
            _trims.RemoveAll(placement => removedParts.Contains(placement.key));
            generatedArrayKeys = _arrays.Select(placement => placement.key).ToList();
            generatedTrimKeys = _trims.Select(placement => placement.key).ToList();

            GeneratedChildren.Resize(this, generatedArrays, _arrays.Count, "Wall Array");
            for (int i = 0; i < _arrays.Count; i++) {
                ArrayPlacement placement = _arrays[i];
                TsitskiArray array = generatedArrays[i];
                GeneratedChildren.Place(this, array, Vector3.zero, Quaternion.identity);

                array.stats = placement.stats;
                array.path = placement.path;
                array.pathRadii = new List<float>();
                array.closed = placement.loop;
                array.cornerSide = placement.side;
                array.pillarHeightOverride = placement.pillarHeight;
                array.Rebuild();
                GeneratedChildren.MarkDirty(array);
            }

            TsitskiProBuilderRoofStats roof = activeRoofStats;
            bool hasRoof = roof && _roofOutlines.Count > 0 && !removedParts.Contains(RoofKey);
            generatedRoofKeys = hasRoof ? new List<string> { RoofKey } : new List<string>();
            GeneratedChildren.Resize(this, generatedRoofs, hasRoof ? 1 : 0, "Roof");
            if (generatedRoofs.Count > 0) {
                TsitskiProBuilderRoof generated = generatedRoofs[0];
                GeneratedChildren.Place(this, generated, Vector3.up * _roofOutlines.Max(o => o.top), Quaternion.identity);
                generated.stats = roof;
                generated.generatedOutlines = _roofOutlines.Select(o => new RoofOutline { points = new List<Vector2>(o.points) }).ToList();
                generated.Rebuild();
                GeneratedChildren.MarkDirty(generated);
            }

            GeneratedChildren.Resize(this, generatedTrims, _trims.Count, "Wall Trim");
            for (int i = 0; i < _trims.Count; i++) {
                TrimPlacement placement = _trims[i];
                TsitskiProBuilderTrim trim = generatedTrims[i];
                GeneratedChildren.Place(this, trim, Vector3.zero, Quaternion.identity);

                trim.stats = placement.stats;
                trim.path = placement.path;
                trim.pathRadii = new List<float>();
                trim.closed = placement.loop;
                trim.interior = placement.interior;
                trim.Rebuild();
                GeneratedChildren.MarkDirty(trim);
            }
        }

        private void CollectOutlines(List<Vector2> line, bool loop, float bottom, float outside, float inside) {
            if (line.Count < 2)
                return;

            float top = bottom + activeHeight;
            List<TsitskiPathLayer> layers = activeLayers;
            for (int layer = 0; layer < layers.Count; layer++) {
                TsitskiPathLayer setting = layers[layer];
                TsitskiArrayStats arrayStats = setting?.stats as TsitskiArrayStats;
                TsitskiProBuilderTrimStats trimStats = setting?.stats as TsitskiProBuilderTrimStats;
                if (!arrayStats && !trimStats)
                    continue;

                float y = setting.heightFromTop ? top - setting.height : bottom + setting.height;

                void Add(float offset, bool interior) {
                    List<Vector3> path = Offset(line, offset, loop).Select(p => new Vector3(p.x, y, p.y)).ToList();

                    if (arrayStats)
                        _arrays.Add(new ArrayPlacement {
                            key = NextKey(layer, "Array"),
                            path = path,
                            loop = loop,
                            side = interior ? CornerSide.Inside : CornerSide.Outside,
                            stats = arrayStats,
                            pillarHeight = setting.matchHeight ? Mathf.Max(0.01f, top - y) : 0f
                        });

                    if (trimStats) {
                        List<Vector3> trimPath = new(path);
                        if (interior)
                            trimPath.Reverse();
                        _trims.Add(new TrimPlacement { key = NextKey(layer, "Trim"), path = trimPath, loop = loop, interior = interior, stats = trimStats });
                    }
                }

                if (setting.wallSide != WallSide.Inside)
                    Add(Mathf.Min(outside, inside), false);
                if (setting.wallSide != WallSide.Outside)
                    Add(Mathf.Max(outside, inside), true);
            }
        }

        private void SyncPathFromFloor(out List<List<Vector2>> extraLoops, out float extraBottom) {
            extraLoops = new List<List<Vector2>>();
            extraBottom = 0f;

            List<List<Vector2>> loops = FloorOutlines(floor, out float top);
            if (loops.Count == 0)
                return;

            int outer = 0;
            for (int i = 1; i < loops.Count; i++)
                if (Mathf.Abs(CutterOutline.SignedArea(loops[i])) > Mathf.Abs(CutterOutline.SignedArea(loops[outer])))
                    outer = i;
            float outerSign = Mathf.Sign(CutterOutline.SignedArea(loops[outer]));

            Matrix4x4 toLocal = transform.worldToLocalMatrix * floor.transform.localToWorldMatrix;
            List<Vector3> ToLocal(List<Vector2> loop) => loop.Select(p => toLocal.MultiplyPoint3x4(new Vector3(p.x, top, p.y))).ToList();

            List<Vector3> outerPath = ToLocal(loops[outer]);
            extraBottom = outerPath[0].y;

            if (!closed || !SamePath(outerPath)) {
                path = outerPath;
                pathRadii.Clear();
                closed = true;
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }

            for (int i = 0; i < loops.Count; i++) {
                if (i == outer)
                    continue;

                bool hole = Mathf.Sign(CutterOutline.SignedArea(loops[i])) != outerSign;
                if (hole && !activeWallsAroundHoles)
                    continue;

                List<Vector2> loop = ToLocal(loops[i]).Select(p => new Vector2(p.x, p.z)).ToList();
                if (CutterOutline.SignedArea(loop) > 0f == hole)
                    loop.Reverse();
                extraLoops.Add(loop);
            }
        }

        private bool SamePath(List<Vector3> other) =>
            path != null && path.Count == other.Count && path.Zip(other, (p, q) => (p - q).sqrMagnitude).All(d => d <= Epsilon * Epsilon);

        private void Build(List<Vector3> positions, List<Face> faces, List<List<Vector2>> extraLoops, float extraBottom) {
            List<Vector3> rounded = PathUtility.Rounded(path, pathRadii, closed, activeRoundingSegments, out bool loop);
            if (rounded.Count >= 2) {
                List<Vector2> outline = rounded.Select(p => new Vector2(p.x, p.z)).ToList();
                if (loop) {
                    outline = Clean(outline);
                    loop = outline.Count >= 3;
                    if (loop && CutterOutline.SignedArea(outline) < 0f)
                        outline.Reverse();
                }

                if (outline.Count >= 2)
                    BuildWall(positions, faces, outline, loop, rounded[0].y);
            }

            if (extraLoops != null)
                foreach (List<Vector2> extra in extraLoops)
                    BuildWall(positions, faces, extra, true, extraBottom);
        }

        private void BuildWall(List<Vector3> positions, List<Face> faces, List<Vector2> line, bool loop, float bottom) {
            float height = activeHeight, thickness = activeThickness, loopCutStep = activeLoopCutStep;
            bool squareCorners = activeSquareCorners;
            float verticalStep = activeVerticalLoopCutStep;
            int bands = verticalStep >= TsitskiProBuilderTrim.MinLoopCutStep ? Mathf.Max(1, Mathf.RoundToInt(height / verticalStep)) : 1;

            (float outside, float inside) = activeAlignment switch {
                WallAlignment.Inside => (0f, thickness),
                WallAlignment.Center => (-thickness * 0.5f, thickness * 0.5f),
                _ => (-thickness, 0f)
            };

            CollectOutlines(line, loop, bottom, outside, inside);
            _wallCenters.Add((Offset(line, (outside + inside) * 0.5f, loop).Select(p => new Vector3(p.x, bottom, p.y)).ToList(), loop));

            Vector3 up = Vector3.up;
            float top = bottom + height;
            Vector3 Point(Vector2 p, float y) => new(p.x, y, p.y);

            void AddSide(Vector2 from, Vector2 to, Vector2 facing, int region, int group = -1) {
                Vector3 normal = new(facing.x, 0f, facing.y);
                if (group < 0)
                    group = _nextGroup++;
                for (int band = 0; band < bands; band++) {
                    float y0 = bottom + height * band / bands, y1 = bottom + height * (band + 1) / bands;
                    AddFace(positions, faces, normal, group, region, Point(from, y0), Point(to, y0), Point(to, y1), Point(from, y1));
                }
            }

            int count = line.Count;
            List<Vector2> a = Offset(line, outside, loop), b = Offset(line, inside, loop);
            if (loop)
                _roofOutlines.Add((a, top));
            Vector2[] startOuter = a.ToArray(), startInner = b.ToArray(), endOuter = a.ToArray(), endInner = b.ToArray();

            for (int i = 0; i < count && squareCorners; i++) {
                if (!loop && (i == 0 || i == count - 1))
                    continue;

                Vector2 d0 = (line[i] - line[(i - 1 + count) % count]).normalized;
                Vector2 d1 = (line[(i + 1) % count] - line[i]).normalized;
                if (Mathf.Abs(Cross(d0, d1)) < MinSquareCornerSine)
                    continue;

                Vector2 n0 = Left(d0), n1 = Left(d1);
                Vector2 outer0 = line[i] + n0 * outside, inner0 = line[i] + n0 * inside;
                Vector2 outer1 = line[i] + n1 * outside, inner1 = line[i] + n1 * inside;

                Vector2 qOO = Intersect(outer0, d0, outer1, d1);
                Vector2 qII = Intersect(inner0, d0, inner1, d1);
                Vector2 qOI = Intersect(outer0, d0, inner1, d1);
                Vector2 qIO = Intersect(inner0, d0, outer1, d1);

                bool previousEndsOnInner = Vector2.Dot(qOI - qOO, d0) < 0f;
                bool nextStartsOnInner = Vector2.Dot(qIO - qOO, d1) > 0f;

                (endOuter[i], endInner[i]) = previousEndsOnInner ? (qOI, qII) : (qOO, qIO);
                (startOuter[i], startInner[i]) = nextStartsOnInner ? (qIO, qII) : (qOO, qOI);

                AddFace(positions, faces, up, -1, NoRegion, Point(qOO, top), Point(qOI, top), Point(qII, top), Point(qIO, top));
                AddFace(positions, faces, -up, -1, NoRegion, Point(qOO, bottom), Point(qOI, bottom), Point(qII, bottom), Point(qIO, bottom));

                if (previousEndsOnInner)
                    AddSide(qIO, qOO, -n1, OutsideRegion);
                else
                    AddSide(qOI, qII, n1, InsideRegion);

                if (nextStartsOnInner)
                    AddSide(qOO, qOI, -n0, OutsideRegion);
                else
                    AddSide(qIO, qII, n0, InsideRegion);
            }

            int segments = loop ? count : count - 1;
            for (int i = 0; i < segments; i++) {
                int next = (i + 1) % count;
                Vector2 left = Left((line[next] - line[i]).normalized);

                int outerGroup = _nextGroup++, innerGroup = _nextGroup++, topGroup = _nextGroup++, bottomGroup = _nextGroup++;
                int pieces = loopCutStep >= TsitskiProBuilderTrim.MinLoopCutStep
                    ? Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(line[i], line[next]) / loopCutStep))
                    : 1;

                for (int p = 0; p < pieces; p++) {
                    float t0 = (float)p / pieces, t1 = (float)(p + 1) / pieces;
                    Vector2 o0 = Vector2.Lerp(startOuter[i], endOuter[next], t0), o1 = Vector2.Lerp(startOuter[i], endOuter[next], t1);
                    Vector2 i0 = Vector2.Lerp(startInner[i], endInner[next], t0), i1 = Vector2.Lerp(startInner[i], endInner[next], t1);
                    AddSide(o0, o1, -left, OutsideRegion, outerGroup);
                    AddSide(i0, i1, left, InsideRegion, innerGroup);
                    AddFace(positions, faces, up, topGroup, NoRegion, Point(o0, top), Point(o1, top), Point(i1, top), Point(i0, top));
                    AddFace(positions, faces, -up, bottomGroup, NoRegion, Point(o0, bottom), Point(o1, bottom), Point(i1, bottom), Point(i0, bottom));
                }
            }

            if (!loop) {
                AddSide(startOuter[0], startInner[0], -(line[1] - line[0]).normalized, NoRegion);
                AddSide(endOuter[count - 1], endInner[count - 1], (line[count - 1] - line[count - 2]).normalized, NoRegion);
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2 Intersect(Vector2 p, Vector2 d, Vector2 q, Vector2 e) => p + d * (Cross(q - p, e) / Cross(d, e));

        private static Vector2 Left(Vector2 direction) => new(-direction.y, direction.x);

        private static List<Vector2> Offset(List<Vector2> line, float distance, bool loop) {
            List<Vector2> result = new(line.Count);

            for (int i = 0; i < line.Count; i++) {
                bool hasPrevious = loop || i > 0, hasNext = loop || i < line.Count - 1;
                Vector2 n0 = hasPrevious ? Left((line[i] - line[(i - 1 + line.Count) % line.Count]).normalized) : Vector2.zero;
                Vector2 n1 = hasNext ? Left((line[(i + 1) % line.Count] - line[i]).normalized) : n0;
                if (!hasPrevious)
                    n0 = n1;

                Vector2 miter = n0 + n1;
                miter = miter.sqrMagnitude > Epsilon * Epsilon ? miter.normalized : n0;
                float dot = Mathf.Max(Vector2.Dot(miter, n0), MinMiterDot);
                result.Add(line[i] + miter * (distance / dot));
            }

            return result;
        }

        public static List<List<Vector2>> FloorOutlines(ProBuilderMesh floor, out float top) {
            top = 0f;
            List<List<Vector2>> loops = new();
            IList<Vector3> positions = floor.positions;

            Vector3 FaceNormal(Face face) {
                IReadOnlyList<int> indexes = face.indexes;
                Vector3 sum = Vector3.zero;
                for (int t = 0; t + 2 < indexes.Count; t += 3)
                    sum += Vector3.Cross(positions[indexes[t + 1]] - positions[indexes[t]], positions[indexes[t + 2]] - positions[indexes[t]]);
                return sum;
            }

            List<Face> upFaces = floor.faces.Where(f => {
                Vector3 normal = FaceNormal(f);
                return normal.sqrMagnitude > 1e-12f && normal.normalized.y > UpDot;
            }).ToList();
            if (upFaces.Count == 0)
                return loops;

            float highest = upFaces.Max(f => f.distinctIndexes.Average(i => positions[i].y));
            top = highest;
            List<Face> topFaces = upFaces.Where(f => f.distinctIndexes.All(i => Mathf.Abs(positions[i].y - highest) <= HeightTolerance)).ToList();

            List<(Vector2 a, Vector2 b)> edges = new();
            foreach (Face face in topFaces) {
                IReadOnlyList<int> indexes = face.indexes;
                for (int t = 0; t + 2 < indexes.Count; t += 3) {
                    for (int k = 0; k < 3; k++) {
                        Vector3 a = positions[indexes[t + k]], b = positions[indexes[t + (k + 1) % 3]];
                        Vector2 a2 = new(a.x, a.z), b2 = new(b.x, b.z);
                        if ((b2 - a2).sqrMagnitude > Epsilon * Epsilon)
                            edges.Add((a2, b2));
                    }
                }
            }

            Dictionary<(long, long), List<(Vector2 a, Vector2 b)>> outgoing = new();
            foreach ((Vector2 a, Vector2 b) in BoundaryPieces(edges)) {
                (long, long) key = Key(a);
                if (!outgoing.TryGetValue(key, out List<(Vector2, Vector2)> list))
                    outgoing[key] = list = new List<(Vector2, Vector2)>();
                list.Add((a, b));
            }

            while (outgoing.Count > 0) {
                (long, long) start = outgoing.Keys.First();
                (long, long) current = start;
                List<Vector2> loop = new();
                bool closed = false;

                while (TryNearKey(outgoing, current, out (long, long) found)) {
                    List<(Vector2 a, Vector2 b)> pieces = outgoing[found];
                    (Vector2 a, Vector2 b) piece = pieces[0];
                    pieces.RemoveAt(0);
                    if (pieces.Count == 0)
                        outgoing.Remove(found);

                    loop.Add(piece.a);
                    current = Key(piece.b);
                    if (System.Math.Abs(current.Item1 - start.Item1) <= 1 && System.Math.Abs(current.Item2 - start.Item2) <= 1) {
                        closed = true;
                        break;
                    }
                }

                if (!closed)
                    continue;

                List<Vector2> outline = Clean(loop);
                if (outline.Count >= 3 && Mathf.Abs(CutterOutline.SignedArea(outline)) > Epsilon * Epsilon)
                    loops.Add(outline);
            }

            return loops;
        }

        private static (long, long) Key(Vector2 p) => ((long)System.Math.Round(p.x / Epsilon), (long)System.Math.Round(p.y / Epsilon));

        private static bool TryNearKey<T>(Dictionary<(long, long), T> map, (long, long) key, out (long, long) found) {
            found = key;
            if (map.ContainsKey(found))
                return true;

            for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++) {
                    found = (key.Item1 + dx, key.Item2 + dy);
                    if (map.ContainsKey(found))
                        return true;
                }

            found = key;
            return false;
        }

        private class EdgeLine {
            public Vector2 direction, normal;
            public float offset;
            public readonly List<(float from, float to, int sign)> spans = new();

            public Vector2 Point(float t) => direction * t + normal * offset;
        }

        private static List<(Vector2 a, Vector2 b)> BoundaryPieces(List<(Vector2 a, Vector2 b)> edges) {
            List<EdgeLine> lines = new();

            foreach ((Vector2 a, Vector2 b) in edges) {
                Vector2 direction = (b - a).normalized;
                int sign = 1;
                if (direction.x < -1e-9f || Mathf.Abs(direction.x) <= 1e-9f && direction.y < 0f) {
                    direction = -direction;
                    sign = -1;
                }

                Vector2 normal = new(-direction.y, direction.x);
                float offset = Vector2.Dot(normal, a);

                EdgeLine line = lines.FirstOrDefault(l => Mathf.Abs(Cross(l.direction, direction)) < Epsilon &&
                                                          Vector2.Dot(l.direction, direction) > 0f && Mathf.Abs(l.offset - offset) < Epsilon);
                if (line == null)
                    lines.Add(line = new EdgeLine { direction = direction, normal = normal, offset = offset });

                float ta = Vector2.Dot(line.direction, a), tb = Vector2.Dot(line.direction, b);
                line.spans.Add((Mathf.Min(ta, tb), Mathf.Max(ta, tb), sign));
            }

            List<(Vector2, Vector2)> pieces = new();
            foreach (EdgeLine line in lines) {
                List<float> breaks = new();
                foreach (float t in line.spans.SelectMany(s => new[] { s.from, s.to }).OrderBy(t => t))
                    if (breaks.Count == 0 || t - breaks[^1] > Epsilon)
                        breaks.Add(t);

                for (int i = 0; i + 1 < breaks.Count; i++) {
                    float mid = (breaks[i] + breaks[i + 1]) * 0.5f;
                    int net = line.spans.Where(s => s.from - Epsilon <= mid && mid <= s.to + Epsilon).Sum(s => s.sign);
                    if (net > 0)
                        pieces.Add((line.Point(breaks[i]), line.Point(breaks[i + 1])));
                    else if (net < 0)
                        pieces.Add((line.Point(breaks[i + 1]), line.Point(breaks[i])));
                }
            }

            return pieces;
        }

        internal static List<Vector2> Clean(List<Vector2> points) {
            List<Vector2> result = new();
            foreach (Vector2 p in points)
                if (result.Count == 0 || (result[^1] - p).sqrMagnitude > Epsilon * Epsilon)
                    result.Add(p);
            if (result.Count > 1 && (result[0] - result[^1]).sqrMagnitude <= Epsilon * Epsilon)
                result.RemoveAt(result.Count - 1);

            bool removed = true;
            while (removed && result.Count >= 3) {
                removed = false;
                for (int i = 0; i < result.Count; i++) {
                    Vector2 into = (result[i] - result[(i - 1 + result.Count) % result.Count]).normalized;
                    Vector2 outOf = (result[(i + 1) % result.Count] - result[i]).normalized;
                    if (Vector2.Dot(into, outOf) > 0f && Mathf.Abs(into.x * outOf.y - into.y * outOf.x) < CollinearTolerance) {
                        result.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
            }

            return result;
        }

        private void AddFace(List<Vector3> positions, List<Face> faces, Vector3 facing, int group, int region, params Vector3[] corners) {
            Vector3 normal = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]) +
                             Vector3.Cross(corners[2] - corners[0], corners[3] - corners[0]);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            if (Vector3.Dot(normal, facing) < 0f)
                Array.Reverse(corners);

            int start = positions.Count;
            positions.AddRange(corners);
            faces.Add(new Face(new[] { start, start + 1, start + 2, start, start + 2, start + 3 }) { smoothingGroup = NoSmoothing });
            _faceGroups.Add(group);
            _faceRegions.Add(region);
        }
    }
}
