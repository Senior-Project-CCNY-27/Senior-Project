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
    public enum StairsMode {
        Path,
        Spiral,
    }

    [AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder Stairs")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class TsitskiProBuilderStairs : MonoBehaviour, IPathOwner, IForcedMaterials, IInputHashed, IPivotShiftable {
        bool IPivotShiftable.CanShiftPivot(out string reason) {
            reason = "spiral stairs wind around their pivot.";
            return mode != StairsMode.Spiral;
        }

        void IPivotShiftable.ShiftLocalPoints(Vector3 offset) => path = PivotShift.Shifted(path, offset);

        void IPivotShiftable.ApplyPivotShift() => Rebuild();

        public const float MinSize = 0.01f;
        private const float MinSpiralInnerRadius = 0.005f;
        private const float Epsilon = 1e-4f;
        private const float MaxLandingCorner = 3f;
        private const float MaxWindingTurn = 175f;
        private const float ArcStep = 10f;
        private const float MinTrimRadius = 0.001f;
        private const float CutterTopClearance = 0.01f;
        private const int NoSmoothing = 0;
        private const int UndersideSmoothing = 1;
        private const int TopStepRegion = 0;
        private const int ForwardStepRegion = 1;
        private const int SideRegion = 2;
        private const string CutterName = "Stairs Cutter";
        private const string LeftTrimName = "Stairs Trim Left";
        private const string RightTrimName = "Stairs Trim Right";

        [Tooltip(Tooltips.StairsStats)] public TsitskiProBuilderStairsStats stats;

        [Header("Materials")]
        [Tooltip(Tooltips.StairsTopStepMaterial)] [UnityEngine.Serialization.FormerlySerializedAs("topMaterial")] public Material topStepMaterial;
        [Tooltip(Tooltips.StairsForwardStepMaterial)] public Material forwardStepMaterial;
        [Tooltip(Tooltips.StairsSideMaterial)] [UnityEngine.Serialization.FormerlySerializedAs("bottomMaterial")] public Material sideMaterial;

        [Header("Shape")]
        [Tooltip(Tooltips.StairsMode)] public StairsMode mode = StairsMode.Path;
        [Tooltip(Tooltips.SpiralHeight)] [Min(MinSize)] public float spiralHeight = 3f;
        [Tooltip(Tooltips.SpiralRadius)] [Min(MinSize)] public float spiralRadius = 0.75f;
        [Tooltip(Tooltips.SpiralAngle)] [Range(10f, 1440f)] public float spiralAngle = 360f;
        [Tooltip(Tooltips.SpiralClockwise)] public bool spiralClockwise = true;

        [Header("Path")]
        [Tooltip(Tooltips.StairsPath)] public List<Vector3> path = new() { Vector3.zero, new(0f, 1.5f, 2.5f) };
        [Tooltip(Tooltips.StairsRadii)] public List<float> pathRadii = new();

        [Header("Steps")]
        [Tooltip(Tooltips.StairsWidth)] [Min(MinSize)] public float width = 1f;
        [Tooltip(Tooltips.StepHeight)] [Min(MinSize)] public float stepHeight = 0.15f;
        [Tooltip(Tooltips.Solid)] public bool solid = true;
        [Tooltip(Tooltips.StairsThickness)] [Min(MinSize)] public float thickness = 0.15f;

        [Header("Cutter")]
        [Tooltip(Tooltips.GenerateCutter)] public bool generateCutter;
        [Tooltip(Tooltips.CutterHeadroom)] [Min(MinSize)] public float cutterHeadroom = 2.1f;

        [Header("Trims")]
        [Tooltip(Tooltips.LeftTrim)] public TsitskiProBuilderTrimStats leftTrim;
        [Tooltip(Tooltips.RightTrim)] public TsitskiProBuilderTrimStats rightTrim;
        [Tooltip(Tooltips.BottomTrimForwardOffset)] public float bottomTrimForwardOffset;
        [Tooltip(Tooltips.BottomTrimUpwardOffset)] public float bottomTrimUpwardOffset;
        [Tooltip(Tooltips.TopTrimForwardOffset)] public float topTrimForwardOffset;
        [Tooltip(Tooltips.TopTrimUpwardOffset)] public float topTrimUpwardOffset;

        [Header("Collider")]
        [Tooltip(Tooltips.SmoothCollider)] public bool smoothCollider;

        [SerializeField, HideInInspector] private TsitskiProBuilderCutter generatedCutter;
        [SerializeField, HideInInspector] private TsitskiProBuilderTrim generatedLeftTrim;
        [SerializeField, HideInInspector] private TsitskiProBuilderTrim generatedRightTrim;
        [SerializeField, HideInInspector] private Mesh smoothColliderMesh;

        private static readonly Dictionary<Mesh, TsitskiProBuilderStairs> ColliderOwners = new();
        [NonSerialized] private bool _buildingSmooth;
        private readonly List<int> _faceRegions = new();
        private readonly Dictionary<int, Vector2> _manualUvs = new();
        private readonly HashSet<int> _manualFaces = new();

        public static event Action<ProBuilderMesh> Rebuilt;

        public Material activeTopStepMaterial => stats ? stats.topStepMaterial : topStepMaterial;
        public Material activeForwardStepMaterial => stats ? stats.forwardStepMaterial : forwardStepMaterial;
        public Material activeSideMaterial => stats ? stats.sideMaterial : sideMaterial;
        Material[] IForcedMaterials.forcedMaterials => new[] { activeTopStepMaterial, activeForwardStepMaterial, activeSideMaterial };

#if UNITY_EDITOR
#endif

        List<Vector3> IPathOwner.localPath { get => path; set => path = value; }
        bool IPathOwner.pathInStats => false;

        public float activeWidth => Mathf.Max(MinSize, stats ? stats.width : width);
        public float activeStepHeight => Mathf.Max(MinSize, stats ? stats.stepHeight : stepHeight);
        public bool activeSolid => stats ? stats.solid : solid;
        public float activeThickness => Mathf.Max(MinSize, stats ? stats.thickness : thickness);
        public bool activeGenerateCutter => stats ? stats.generateCutter : generateCutter;
        public float activeCutterHeadroom => Mathf.Max(MinSize, stats ? stats.cutterHeadroom : cutterHeadroom);
        public TsitskiProBuilderTrimStats activeLeftTrim => stats ? stats.leftTrim : leftTrim;
        public TsitskiProBuilderTrimStats activeRightTrim => stats ? stats.rightTrim : rightTrim;
        public bool activeSmoothCollider => stats ? stats.smoothCollider : smoothCollider;
        public float activeBottomTrimForwardOffset => stats ? stats.bottomTrimForwardOffset : bottomTrimForwardOffset;
        public float activeBottomTrimUpwardOffset => stats ? stats.bottomTrimUpwardOffset : bottomTrimUpwardOffset;
        public float activeTopTrimForwardOffset => stats ? stats.topTrimForwardOffset : topTrimForwardOffset;
        public float activeTopTrimUpwardOffset => stats ? stats.topTrimUpwardOffset : topTrimUpwardOffset;

        public bool isSpiral => mode == StairsMode.Spiral;

        public Vector3 SpiralDirection(float angle) => Quaternion.Euler(0f, spiralClockwise ? angle : -angle, 0f) * Vector3.forward;

        public float spiralInnerRadius => Mathf.Max(MinSpiralInnerRadius, spiralRadius - activeWidth * 0.5f);
        public float spiralOuterRadius => Mathf.Max(spiralRadius + activeWidth * 0.5f, spiralInnerRadius + MinSize);

        public int stepCount {
            get {
                if (isSpiral)
                    return Steps(spiralHeight);
                Prepare(out List<Vector3> points, out _, out List<(int start, int end)> runs);
                return runs.Sum(r => Steps(Mathf.Abs(points[r.end].y - points[r.start].y)));
            }
        }

        private struct Station {
            public Vector3 center, left, right;

            public static Station Lerp(Station a, Station b, float t) => new() {
                center = Vector3.Lerp(a.center, b.center, t),
                left = Vector3.Lerp(a.left, b.left, t),
                right = Vector3.Lerp(a.right, b.right, t)
            };

            public static Station Straight(Vector3 center, Vector3 direction, float halfWidth) {
                Vector3 offset = Vector3.Cross(Vector3.up, direction) * halfWidth;
                return new Station { center = center, left = center - offset, right = center + offset };
            }
        }

        private int Steps(float rise) => rise < Epsilon ? 0 : Mathf.Max(1, Mathf.RoundToInt(rise / activeStepHeight));

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

            TsitskiProBuilderStairsStats.Changed += OnStatsChanged;
            RestoreSmoothCollider();
            QueueRebuild();
        }

        private void Start() => RestoreSmoothCollider();

        private void OnDisable() => TsitskiProBuilderStairsStats.Changed -= OnStatsChanged;

        private void RestoreSmoothCollider() {
            if (activeSmoothCollider && smoothColliderMesh && TryGetComponent(out MeshCollider meshCollider) &&
                meshCollider.sharedMesh != smoothColliderMesh)
                meshCollider.sharedMesh = smoothColliderMesh;
        }

        private void OnValidate() => QueueRebuild();

        private void OnStatsChanged(TsitskiProBuilderStairsStats changed) {
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

        public void GenerateLightmapUvs() {
            if (TryGetComponent(out ProBuilderMesh mesh))
                CutLightmapUnwrap.ApplyToProBuilderMesh(mesh);
        }

        private void BuildGenerated() {
            TsitskiSettings.AutoName(this, stats, "ProBuilder Stairs");

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            KeptFaceMaterials kept = KeptFaceMaterials.Capture(mesh);

            List<Vector3> positions = new();
            List<Face> faces = new();
            _faceRegions.Clear();
            _manualUvs.Clear();
            _manualFaces.Clear();
            Build(positions, faces);

            if (faces.Count == 0)
                mesh.Clear();
            else {
                mesh.RebuildWithPositionsAndFaces(positions, faces);
                if (_manualFaces.Count > 0) {
                    Vector2[] uvs = new Vector2[positions.Count];
                    foreach (KeyValuePair<int, Vector2> uv in _manualUvs)
                        if (uv.Key < uvs.Length)
                            uvs[uv.Key] = uv.Value;
                    IList<Face> built = mesh.faces;
                    foreach (int index in _manualFaces)
                        if (index < built.Count)
                            built[index].manualUV = true;
                    mesh.textures = uvs;
                }
                kept?.Apply(mesh);
                ForcedMaterials.Apply(mesh, _faceRegions, activeTopStepMaterial, activeForwardStepMaterial, activeSideMaterial);
            }
            mesh.ToMesh();
            mesh.Refresh();
            CutLightmapUnwrap.ApplyToProBuilderMesh(mesh);

            if (TryGetComponent(out MeshRenderer meshRenderer) && !meshRenderer.sharedMaterial)
                meshRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;

            SyncSmoothCollider();

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
            Rebuilt?.Invoke(mesh);
        }

        private void SyncSmoothCollider() {
            if (!TryGetComponent(out MeshCollider meshCollider))
                return;

            if (!activeSmoothCollider) {
                if (smoothColliderMesh) {
                    if (!ColliderOwners.TryGetValue(smoothColliderMesh, out TsitskiProBuilderStairs previous) || !previous || previous == this) {
                        ColliderOwners.Remove(smoothColliderMesh);
                        DestroyImmediate(smoothColliderMesh);
                    }
                    smoothColliderMesh = null;
                }

                Mesh renderMesh = TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (meshCollider.sharedMesh != renderMesh) {
                    meshCollider.sharedMesh = null;
                    meshCollider.sharedMesh = renderMesh;
                }
                return;
            }

            List<Vector3> positions = new();
            List<Face> faces = new();
            _buildingSmooth = true;
            try {
                Build(positions, faces);
            }
            finally {
                _buildingSmooth = false;
            }

            if (smoothColliderMesh && ColliderOwners.TryGetValue(smoothColliderMesh, out TsitskiProBuilderStairs owner) && owner && owner != this)
                smoothColliderMesh = null;
            if (!smoothColliderMesh)
                smoothColliderMesh = new Mesh();

            ColliderOwners[smoothColliderMesh] = this;
            smoothColliderMesh.name = $"{name} Smooth Collider";
            smoothColliderMesh.Clear();
            smoothColliderMesh.SetVertices(positions);
            smoothColliderMesh.SetTriangles(faces.SelectMany(f => f.indexes).ToList(), 0);
            smoothColliderMesh.RecalculateBounds();

            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = smoothColliderMesh;
        }

        private void EnsureCutter() {
            if (!generatedCutter) {
                GameObject child = new(CutterName);
                child.transform.SetParent(transform, false);
                generatedCutter = child.AddComponent<TsitskiProBuilderCutter>();
            }

            Transform t = generatedCutter.transform;
            if (t.localPosition != Vector3.zero)
                t.localPosition = Vector3.zero;
            if (t.localRotation != Quaternion.identity)
                t.localRotation = Quaternion.identity;
            if (t.localScale != Vector3.one)
                t.localScale = Vector3.one;
        }

        private void SyncSpiralCutter(List<Station> line, float rise) {
            List<Station> stations = StepStations(line, Steps(rise), out List<float> along);
            if (!activeGenerateCutter || stations.Count < 2 || along[^1] < Epsilon) {
                if (generatedCutter)
                    DestroyImmediate(generatedCutter.gameObject);
                return;
            }

            EnsureCutter();
            float length = along[^1];
            float topY = rise + CutterTopClearance;
            float bottomY = Mathf.Max(topY - activeCutterHeadroom, 0.1f);
            float cutoff = topY - 0.1f - activeCutterHeadroom;
            float Height(int k) => rise * along[k] / length;

            List<Station> kept = new() { stations[^1] };
            for (int k = stations.Count - 2; k >= 0; k--) {
                if (Height(k) >= cutoff) {
                    kept.Add(stations[k]);
                    continue;
                }

                float t = (Height(k + 1) - cutoff) / Mathf.Max(Epsilon, Height(k + 1) - Height(k));
                kept.Add(Station.Lerp(stations[k + 1], stations[k], t));
                break;
            }
            kept.Reverse();

            List<Vector3> vertices = new();
            List<int[]> parts = new();
            for (int k = 0; k + 1 < kept.Count; k++) {
                if (Vector3.Distance(kept[k].center, kept[k + 1].center) < 1e-3f)
                    continue;
                Vector3[] floor = { kept[k].left, kept[k].right, kept[k + 1].right, kept[k + 1].left };
                int start = vertices.Count;
                vertices.AddRange(floor.Select(p => new Vector3(p.x, bottomY, p.z)));
                vertices.AddRange(floor.Select(p => new Vector3(p.x, topY, p.z)));
                parts.Add(PrismTriangles(vertices, start));
            }

            Mesh mesh = generatedCutter.shapeMesh;
            if (generatedCutter.shape == ProBuilderCutterShape.Mesh && !generatedCutter.cutThroughParent && mesh &&
                mesh.subMeshCount == parts.Count && mesh.vertices.SequenceEqual(vertices))
                return;

            if (!mesh || EditorAsset(mesh)) {
                mesh = new Mesh { name = $"{name} Cutter" };
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.subMeshCount = parts.Count;
            for (int p = 0; p < parts.Count; p++)
                mesh.SetTriangles(parts[p], p);
            mesh.RecalculateBounds();

            generatedCutter.cutThroughParent = false;
            generatedCutter.SetMesh(mesh);
        }

        private static bool EditorAsset(Mesh mesh) {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.IsPersistent(mesh);
#else
            return false;
#endif
        }

        private static int[] PrismTriangles(List<Vector3> vertices, int start) {
            int[][] quads = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 1, 5, 4, 0 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < 8; i++)
                centroid += vertices[start + i];
            centroid /= 8f;

            List<int> triangles = new();
            foreach (int[] quad in quads) {
                int a = start + quad[0], b = start + quad[1], c = start + quad[2], d = start + quad[3];
                Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]) + Vector3.Cross(vertices[c] - vertices[a], vertices[d] - vertices[a]);
                Vector3 middle = (vertices[a] + vertices[b] + vertices[c] + vertices[d]) * 0.25f;
                if (Vector3.Dot(normal, middle - centroid) < 0f)
                    (b, d) = (d, b);
                triangles.AddRange(new[] { a, b, c, a, c, d });
            }
            return triangles.ToArray();
        }

        private void SyncCutter(List<Vector3> points, List<float> radii, List<(int start, int end)> runs) {
            if (!activeGenerateCutter || points.Count < 2) {
                if (generatedCutter)
                    DestroyImmediate(generatedCutter.gameObject);
                return;
            }

            EnsureCutter();

            float halfWidth = activeWidth * 0.5f;
            float topY = points.Max(p => p.y) + CutterTopClearance;
            float bottomY = Mathf.Max(topY - activeCutterHeadroom, points.Min(p => p.y) + 0.1f);
            float height = Mathf.Max(MinSize, topY - bottomY), centerY = (topY + bottomY) * 0.5f;

            List<Vector3> heights = new(points);
            foreach ((int start, int end) in runs) {
                float total = 0f;
                for (int i = start; i < end; i++)
                    total += Vector3.Distance(FlatPoint(points[i]), FlatPoint(points[i + 1]));

                float along = 0f;
                for (int i = start + 1; i < end; i++) {
                    along += Vector3.Distance(FlatPoint(points[i - 1]), FlatPoint(points[i]));
                    heights[i] = new Vector3(points[i].x, Mathf.Lerp(points[start].y, points[end].y, total > Epsilon ? along / total : 0f), points[i].z);
                }
            }

            List<float> sourceRadii = radii.Select(r => r > 0f ? r + halfWidth : 0f).ToList();
            bool reversed = heights[0].y > heights[^1].y;
            if (reversed) {
                heights.Reverse();
                sourceRadii.Reverse();
            }

            float cutoff = topY - 0.1f - activeCutterHeadroom;
            List<Vector3> kept = new() { heights[^1] };
            List<float> cutterRadii = new() { sourceRadii[^1] };
            bool trimmed = false;

            for (int i = heights.Count - 2; i >= 0; i--) {
                if (heights[i].y >= cutoff) {
                    kept.Add(heights[i]);
                    cutterRadii.Add(sourceRadii[i]);
                    continue;
                }

                Vector3 above = heights[i + 1], below = heights[i];
                kept.Add(Vector3.Lerp(above, below, (above.y - cutoff) / Mathf.Max(Epsilon, above.y - below.y)));
                cutterRadii.Add(0f);
                trimmed = true;
                break;
            }

            kept.Reverse();
            cutterRadii.Reverse();
            if (reversed) {
                kept.Reverse();
                cutterRadii.Reverse();
            }

            List<Vector3> cutterPoints = kept.Select(p => new Vector3(p.x, centerY, p.z)).ToList();
            int startIndex = reversed ? cutterPoints.Count - 1 : 0, nextIndex = reversed ? cutterPoints.Count - 2 : 1;

            if (!trimmed && cutterPoints.Count >= 2) {
                float firstLength = Vector3.Distance(cutterPoints[startIndex], cutterPoints[nextIndex]);
                if (firstLength > Epsilon)
                    cutterPoints[startIndex] = Vector3.Lerp(cutterPoints[startIndex], cutterPoints[nextIndex], Mathf.Min(0.5f, activeThickness / firstLength));
            }

            if (generatedCutter.shape == ProBuilderCutterShape.Path && !generatedCutter.cutThroughParent &&
                Mathf.Approximately(generatedCutter.pathWidth, activeWidth) && Mathf.Approximately(generatedCutter.pathHeight, height) &&
                generatedCutter.pathPoints != null && generatedCutter.pathPoints.SequenceEqual(cutterPoints) &&
                generatedCutter.pathRadii != null && generatedCutter.pathRadii.SequenceEqual(cutterRadii) &&
                (generatedCutter.pathRotations == null || generatedCutter.pathRotations.Count == 0))
                return;

            generatedCutter.shape = ProBuilderCutterShape.Path;
            generatedCutter.cutThroughParent = false;
            generatedCutter.pathPoints = cutterPoints;
            generatedCutter.pathRadii = cutterRadii;
            generatedCutter.pathRotations = new List<Quaternion>();
            generatedCutter.pathWidth = activeWidth;
            generatedCutter.pathHeight = height;
            generatedCutter.NotifyChanged();
        }

        private TsitskiProBuilderTrim SyncTrim(TsitskiProBuilderTrim trim, TsitskiProBuilderTrimStats trimStats, string trimName, List<Vector3> points,
            List<float> radii, List<(int start, int end)> runs, float side, float halfWidth) {
            List<Vector3> sidePath = new();
            List<float> sideRadii = new();
            if (points.Count >= 2)
                SidePath(points, radii, runs, side, halfWidth, sidePath, sideRadii);
            return AssignTrim(trim, trimStats, trimName, sidePath, sideRadii, side);
        }

        private TsitskiProBuilderTrim AssignTrim(TsitskiProBuilderTrim trim, TsitskiProBuilderTrimStats trimStats, string trimName, List<Vector3> sidePath,
            List<float> sideRadii, float side) {
            if (!trimStats || sidePath.Count < 2) {
                if (trim)
                    DestroyImmediate(trim.gameObject);
                return null;
            }

            OffsetTrimEnds(sidePath);

            if (side < 0f) {
                sidePath.Reverse();
                sideRadii.Reverse();
            }

            if (!trim) {
                GameObject child = new(trimName);
                child.transform.SetParent(transform, false);
                trim = child.AddComponent<TsitskiProBuilderTrim>();
                if (!child.TryGetComponent(out MeshCollider _))
                    child.AddComponent<MeshCollider>();
            }

            Transform t = trim.transform;
            if (t.localPosition != Vector3.zero)
                t.localPosition = Vector3.zero;
            if (t.localRotation != Quaternion.identity)
                t.localRotation = Quaternion.identity;
            if (t.localScale != Vector3.one)
                t.localScale = Vector3.one;

            if (trim.stats == trimStats && !trim.closed && trim.path != null && trim.path.SequenceEqual(sidePath) &&
                trim.pathRadii != null && trim.pathRadii.SequenceEqual(sideRadii))
                return trim;

            trim.stats = trimStats;
            trim.path = sidePath;
            trim.pathRadii = sideRadii;
            trim.closed = false;
            trim.Rebuild();
            return trim;
        }

        private void OffsetTrimEnds(List<Vector3> sidePath) {
            int last = sidePath.Count - 1;
            bool startIsBottom = sidePath[0].y <= sidePath[last].y;
            int bottom = startIsBottom ? 0 : last, top = startIsBottom ? last : 0;

            Vector3 EndDirection(int end) {
                for (int step = 1; step <= last; step++) {
                    Vector3 direction = end == 0 ? Flat(sidePath[step] - sidePath[0]) : Flat(sidePath[last] - sidePath[last - step]);
                    if (direction != Vector3.zero)
                        return direction;
                }
                return Vector3.zero;
            }

            Vector3 bottomDirection = EndDirection(bottom), topDirection = EndDirection(top);
            sidePath[bottom] += bottomDirection * activeBottomTrimForwardOffset + Vector3.up * activeBottomTrimUpwardOffset;
            sidePath[top] += topDirection * activeTopTrimForwardOffset + Vector3.up * activeTopTrimUpwardOffset;
        }

        private static void SidePath(List<Vector3> points, List<float> radii, List<(int start, int end)> runs, float side, float halfWidth,
            List<Vector3> result, List<float> resultRadii) {
            int last = points.Count - 1;

            void Add(Vector3 p, float radius) {
                if (result.Count > 0 && (result[^1] - p).sqrMagnitude <= Epsilon * Epsilon) {
                    resultRadii[^1] = Mathf.Max(resultRadii[^1], radius);
                    return;
                }

                result.Add(p);
                resultRadii.Add(radius);
            }

            foreach ((int a, int b) in runs) {
                bool startLanding = a > 0, endLanding = b < last;

                if (startLanding) {
                    Vector3 center = points[a];
                    Vector3 into = Flat(points[a] - points[a - 1]), outOf = Flat(points[a + 1] - points[a]);
                    if (into == Vector3.zero)
                        into = outOf;
                    if (outOf == Vector3.zero)
                        outOf = into;

                    if (into != Vector3.zero) {
                        Vector3 arrive = center - into * halfWidth + Vector3.Cross(Vector3.up, into) * (side * halfWidth);
                        Vector3 leave = center + outOf * halfWidth + Vector3.Cross(Vector3.up, outOf) * (side * halfWidth);
                        float outerSide = Vector3.Cross(into, outOf).y > 0f ? -1f : 1f;

                        Add(arrive, 0f);
                        if (Mathf.Approximately(side, outerSide) && Intersect(arrive, into, leave, outOf, out Vector3 corner) &&
                            (corner - center).sqrMagnitude <= MaxLandingCorner * MaxLandingCorner * halfWidth * halfWidth)
                            Add(new Vector3(corner.x, center.y, corner.z), 0f);
                        Add(leave, 0f);
                    }
                }

                Vector3 firstDirection = Flat(points[a + 1] - points[a]), lastDirection = Flat(points[b] - points[b - 1]);
                if (firstDirection == Vector3.zero || lastDirection == Vector3.zero)
                    continue;

                int count = b - a + 1;
                float[] tangents = CornerTangents(points, radii, a, b, startLanding, endLanding, halfWidth, out float[] turns);
                float[] arcs = new float[count];
                for (int i = 1; i < count - 1; i++)
                    if (tangents[i] > Epsilon)
                        arcs[i] = tangents[i] / Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad) * turns[i] * Mathf.Deg2Rad;

                float[] reach = new float[count];
                float startOffset = startLanding ? halfWidth : 0f, raw = 0f, saved = 0f;
                for (int i = 1; i < count; i++) {
                    raw += Vector3.Distance(FlatPoint(points[a + i - 1]), FlatPoint(points[a + i]));
                    if (i == count - 1)
                        break;

                    reach[i] = raw - startOffset - saved - tangents[i] + arcs[i] * 0.5f;
                    saved += 2f * tangents[i] - arcs[i];
                }

                float total = raw - startOffset - (endLanding ? halfWidth : 0f) - saved;
                float Height(float along) => Mathf.Lerp(points[a].y, points[b].y, total > Epsilon ? Mathf.Clamp01(along / total) : 0f);

                Vector3 Offset(Vector3 p, Vector3 direction) => FlatPoint(p) + Vector3.Cross(Vector3.up, direction) * (side * halfWidth);

                Vector3 start = Offset(points[a], firstDirection) + firstDirection * (startLanding ? halfWidth : 0f);
                Add(new Vector3(start.x, points[a].y, start.z), 0f);

                for (int i = 1; i < count - 1; i++) {
                    int p = a + i;
                    Vector3 into = Flat(points[p] - points[p - 1]), outOf = Flat(points[p + 1] - points[p]);
                    Vector3 rightIn = Vector3.Cross(Vector3.up, into), rightOut = Vector3.Cross(Vector3.up, outOf);
                    float denominator = 1f + Vector3.Dot(rightIn, rightOut);
                    if (denominator < 0.1f)
                        continue;

                    Vector3 mitre = FlatPoint(points[p]) + (rightIn + rightOut) * (side * halfWidth / denominator);
                    float radius = 0f;
                    if (tangents[i] > Epsilon) {
                        float centerRadius = tangents[i] / Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad);
                        float turnSide = Vector3.Cross(into, outOf).y > 0f ? 1f : -1f;

                        if (!Mathf.Approximately(side, turnSide)) {
                            float edgeRadius = centerRadius + halfWidth;
                            float edgeTangent = edgeRadius * Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad);
                            Vector3 arcStart = mitre - into * edgeTangent, arcEnd = mitre + outOf * edgeTangent;

                            Add(new Vector3(arcStart.x, Height(reach[i] - arcs[i] * 0.5f), arcStart.z), 0f);
                            Add(new Vector3(mitre.x, Height(reach[i]), mitre.z), edgeRadius);
                            Add(new Vector3(arcEnd.x, Height(reach[i] + arcs[i] * 0.5f), arcEnd.z), 0f);
                            continue;
                        }

                        radius = Mathf.Max(centerRadius - halfWidth, MinTrimRadius);
                    }

                    Add(new Vector3(mitre.x, Height(reach[i]), mitre.z), radius);
                }

                Vector3 end = Offset(points[b], lastDirection) - lastDirection * (endLanding ? halfWidth : 0f);
                Add(new Vector3(end.x, points[b].y, end.z), 0f);
            }

            for (int i = 1; i < result.Count - 1; i++) {
                if (resultRadii[i] <= MinTrimRadius)
                    continue;

                Vector3 into = result[i] - result[i - 1], outOf = result[i + 1] - result[i];
                Vector3 flatIn = new(into.x, 0f, into.z), flatOut = new(outOf.x, 0f, outOf.z);
                if (flatIn.sqrMagnitude < Epsilon || flatOut.sqrMagnitude < Epsilon)
                    continue;

                float planTurn = Vector3.Angle(flatIn, flatOut), turn = Vector3.Angle(into, outOf);
                if (planTurn < 0.1f || turn < 0.1f)
                    continue;

                Vector3 bisector = (outOf.normalized - into.normalized).normalized;
                float flatBisector = new Vector2(bisector.x, bisector.z).magnitude;
                float planBulge = 1f / Mathf.Cos(planTurn * 0.5f * Mathf.Deg2Rad) - 1f;
                float bulge = 1f / Mathf.Cos(turn * 0.5f * Mathf.Deg2Rad) - 1f;
                if (flatBisector < 0.1f || bulge < Epsilon)
                    continue;

                resultRadii[i] *= planBulge / (bulge * flatBisector);
            }
        }

        private void Prepare(out List<Vector3> points, out List<float> radii, out List<(int start, int end)> runs) {
            points = new List<Vector3>();
            radii = new List<float>();
            runs = new List<(int start, int end)>();

            if (path != null) {
                for (int i = 0; i < path.Count; i++) {
                    float radius = PointRounding.Radius(pathRadii, i);
                    if (points.Count > 0 && (points[^1] - path[i]).sqrMagnitude <= Epsilon * Epsilon) {
                        radii[^1] = Mathf.Max(radii[^1], radius);
                        continue;
                    }

                    points.Add(path[i]);
                    radii.Add(radius);
                }
            }

            int last = points.Count - 1, start = 0;
            for (int i = 1; i <= last; i++) {
                if (i < last && Winds(points, radii, i))
                    continue;

                runs.Add((start, i));
                start = i;
            }
        }

        private static bool Winds(List<Vector3> points, List<float> radii, int i) {
            Vector3 into = Flat(points[i] - points[i - 1]), outOf = Flat(points[i + 1] - points[i]);
            return radii[i] > 0f && into != Vector3.zero && outOf != Vector3.zero && Vector3.Angle(into, outOf) < MaxWindingTurn;
        }

        private List<Station> SpiralLine() {
            float sweep = Mathf.Max(1f, spiralAngle);
            float inner = spiralInnerRadius, outer = spiralOuterRadius, middle = (inner + outer) * 0.5f;
            int segments = Mathf.Max(2, Mathf.CeilToInt(sweep / ArcStep));
            List<Station> line = new();
            for (int k = 0; k <= segments; k++) {
                float angle = sweep * k / segments;
                Vector3 radial = SpiralDirection(angle);
                Vector3 tangent = SpiralDirection(angle + 1f) - SpiralDirection(angle - 1f);
                bool outerOnRight = Vector3.Dot(Vector3.Cross(Vector3.up, tangent), radial) > 0f;
                Vector3 innerPoint = radial * inner, outerPoint = radial * outer;
                line.Add(new Station {
                    center = radial * middle,
                    left = outerOnRight ? innerPoint : outerPoint,
                    right = outerOnRight ? outerPoint : innerPoint
                });
            }
            return line;
        }

        private const float SpiralPathStep = 3f;

        private void SpiralCurve(float radius, float rise, out List<Vector3> points, out List<float> radii) {
            float sweep = Mathf.Max(1f, spiralAngle);
            int segments = Mathf.Max(2, Mathf.CeilToInt(sweep / SpiralPathStep));
            points = new List<Vector3>(segments + 1);
            for (int k = 0; k <= segments; k++) {
                float angle = sweep * k / segments;
                points.Add(SpiralDirection(angle) * radius + Vector3.up * (rise * k / segments));
            }
            radii = Enumerable.Repeat(0f, points.Count).ToList();
        }

        private void BuildSpiral(List<Vector3> positions, List<Face> faces) {
            List<Station> line = SpiralLine();
            float rise = Mathf.Max(MinSize, spiralHeight);
            if (!_buildingSmooth) {
                float inner = spiralInnerRadius, outer = spiralOuterRadius, middle = (inner + outer) * 0.5f, halfWidth = (outer - inner) * 0.5f;
                Vector3 tangent = SpiralDirection(1f) - SpiralDirection(-1f);
                bool outerOnRight = Vector3.Dot(Vector3.Cross(Vector3.up, tangent), SpiralDirection(0f)) > 0f;

                SyncSpiralCutter(line, rise);

                SpiralCurve(outerOnRight ? inner : outer, rise, out List<Vector3> leftPath, out List<float> leftRadii);
                SpiralCurve(outerOnRight ? outer : inner, rise, out List<Vector3> rightPath, out List<float> rightRadii);
                generatedLeftTrim = AssignTrim(generatedLeftTrim, activeLeftTrim, LeftTrimName, leftPath, leftRadii, -1f);
                generatedRightTrim = AssignTrim(generatedRightTrim, activeRightTrim, RightTrimName, rightPath, rightRadii, 1f);
            }
            Winding(positions, faces, line, 0f, rise, false, 0f);
        }

        private void Build(List<Vector3> positions, List<Face> faces) {
            if (isSpiral) {
                BuildSpiral(positions, faces);
                return;
            }

            Prepare(out List<Vector3> points, out List<float> radii, out List<(int start, int end)> runs);
            if (!_buildingSmooth) {
                SyncCutter(points, radii, runs);
                float trimHalfWidth = activeWidth * 0.5f;
                generatedLeftTrim = SyncTrim(generatedLeftTrim, activeLeftTrim, LeftTrimName, points, radii, runs, -1f, trimHalfWidth);
                generatedRightTrim = SyncTrim(generatedRightTrim, activeRightTrim, RightTrimName, points, radii, runs, 1f, trimHalfWidth);
            }
            if (points.Count < 2)
                return;

            float halfWidth = activeWidth * 0.5f;
            float baseY = points.Min(p => p.y);
            int last = points.Count - 1;

            foreach ((int start, int end) in runs)
                Run(positions, faces, points, radii, start, end, start > 0, end < last, baseY, halfWidth);

            foreach ((int start, _) in runs.Skip(1))
                Landing(positions, faces, points[start], Flat(points[start] - points[start - 1]), Flat(points[start + 1] - points[start]), baseY,
                    halfWidth);
        }

        private void Run(List<Vector3> positions, List<Face> faces, List<Vector3> points, List<float> radii, int a, int b,
            bool startLanding, bool endLanding, float baseY, float halfWidth) {
            List<Station> line = Centerline(points, radii, a, b, startLanding, endLanding, halfWidth);
            if (line == null)
                return;

            Vector3 start = line[0].center + Vector3.up * points[a].y;
            Vector3 end = line[^1].center + Vector3.up * points[b].y;

            if (line.Count == 2) {
                if (Vector3.Dot(end - start, Flat(points[a + 1] - points[a])) < Epsilon)
                    return;

                if (start.y <= end.y)
                    Flight(positions, faces, start, end, startLanding, baseY, halfWidth);
                else
                    Flight(positions, faces, end, start, endLanding, baseY, halfWidth);
                return;
            }

            if (start.y <= end.y) {
                Winding(positions, faces, line, start.y, end.y, startLanding, baseY);
                return;
            }

            line.Reverse();
            for (int i = 0; i < line.Count; i++)
                line[i] = new Station { center = line[i].center, left = line[i].right, right = line[i].left };
            Winding(positions, faces, line, end.y, start.y, endLanding, baseY);
        }

        private static float[] CornerTangents(List<Vector3> points, List<float> radii, int a, int b, bool startLanding, bool endLanding,
            float halfWidth, out float[] turns) {
            int count = b - a + 1;
            turns = new float[count];
            float[] tangents = new float[count];
            for (int i = 1; i < count - 1; i++) {
                int p = a + i;
                turns[i] = Vector3.Angle(Flat(points[p] - points[p - 1]), Flat(points[p + 1] - points[p]));
                if (turns[i] > 0.1f)
                    tangents[i] = (halfWidth + radii[p]) * Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad);
            }

            float[] scales = new float[count - 1];
            for (int i = 0; i < count - 1; i++) {
                float available = Vector3.Distance(FlatPoint(points[a + i]), FlatPoint(points[a + i + 1]))
                                  - (i == 0 && startLanding ? halfWidth : 0f) - (i == count - 2 && endLanding ? halfWidth : 0f);
                float demand = tangents[i] + tangents[i + 1];
                scales[i] = demand > available && demand > 0f ? Mathf.Max(0f, available) / demand : 1f;
            }

            float[] scaled = new float[count];
            for (int i = 1; i < count - 1; i++)
                scaled[i] = tangents[i] * Mathf.Min(scales[i - 1], scales[i]);
            return scaled;
        }

        private static List<Station> Centerline(List<Vector3> points, List<float> radii, int a, int b, bool startLanding, bool endLanding,
            float halfWidth) {
            Vector3 firstDirection = Flat(points[a + 1] - points[a]), lastDirection = Flat(points[b] - points[b - 1]);
            if (firstDirection == Vector3.zero || lastDirection == Vector3.zero)
                return null;

            int count = b - a + 1;
            float[] tangents = CornerTangents(points, radii, a, b, startLanding, endLanding, halfWidth, out float[] turns);

            List<Station> line = new() {
                Station.Straight(FlatPoint(points[a]) + firstDirection * (startLanding ? halfWidth : 0f), firstDirection, halfWidth)
            };

            for (int i = 1; i < count - 1; i++) {
                float tangent = tangents[i];
                if (tangent < Epsilon)
                    continue;

                int p = a + i;
                Vector3 into = Flat(points[p] - points[p - 1]), outOf = Flat(points[p + 1] - points[p]);
                float radius = tangent / Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad);
                float side = Vector3.Cross(into, outOf).y > 0f ? 1f : -1f;
                Vector3 arcStart = FlatPoint(points[p]) - into * tangent;
                Vector3 center = arcStart + Vector3.Cross(Vector3.up, into) * (side * radius);
                Vector3 spokeStart = arcStart - center;
                float inner = Mathf.Max(0f, radius - halfWidth) / radius, outer = (radius + halfWidth) / radius;
                int segments = Mathf.Max(1, Mathf.CeilToInt(turns[i] / ArcStep));

                for (int k = 0; k <= segments; k++) {
                    Vector3 spoke = Quaternion.AngleAxis(side * turns[i] * k / segments, Vector3.up) * spokeStart;
                    line.Add(new Station {
                        center = center + spoke,
                        left = center + spoke * (side > 0f ? outer : inner),
                        right = center + spoke * (side > 0f ? inner : outer)
                    });
                }
            }

            line.Add(Station.Straight(FlatPoint(points[b]) - lastDirection * (endLanding ? halfWidth : 0f), lastDirection, halfWidth));
            return line;
        }

        private static List<Station> StepStations(List<Station> line, int steps, out List<float> along) {
            float[] distance = new float[line.Count];
            for (int j = 1; j < line.Count; j++)
                distance[j] = distance[j - 1] + Vector3.Distance(line[j - 1].center, line[j].center);

            float length = distance[^1], depth = length / Mathf.Max(1, steps);
            List<Station> stations = new();
            along = new List<float>();
            int next = 1;
            for (int j = 0; j < line.Count - 1; j++) {
                stations.Add(line[j]);
                along.Add(distance[j]);

                for (; next < steps && next * depth < distance[j + 1]; next++) {
                    float s = next * depth;
                    if (s <= distance[j])
                        continue;

                    stations.Add(Station.Lerp(line[j], line[j + 1], (s - distance[j]) / (distance[j + 1] - distance[j])));
                    along.Add(s);
                }
            }

            stations.Add(line[^1]);
            along.Add(length);
            return stations;
        }

        private void Winding(List<Vector3> positions, List<Face> faces, List<Station> line, float lowY, float highY, bool lowIsLanding, float baseY) {
            float[] distance = new float[line.Count];
            for (int j = 1; j < line.Count; j++)
                distance[j] = distance[j - 1] + Vector3.Distance(line[j - 1].center, line[j].center);

            float length = distance[^1];
            if (length < Epsilon)
                return;

            float rise = highY - lowY;
            int steps = Steps(rise);
            float depth = length / Mathf.Max(1, steps), height = steps > 0 ? rise / steps : 0f;
            float bottomY = BottomY(lowY, rise, lowIsLanding, baseY);
            float slopeOffset = activeThickness * Mathf.Sqrt(1f + height * height / (depth * depth));

            List<Station> stations = StepStations(line, steps, out List<float> along);

            float Bottom(float s) => lowY + (activeSolid ? bottomY : Mathf.Max(bottomY, Mathf.Min(s * height / depth - slopeOffset, rise - activeThickness)));
            Vector3 At(Vector3 p, float y) => new(p.x, y, p.z);

            bool smooth = _buildingSmooth;
            float StationTop(int j) => lowY + along[j] * rise / length;

            float[] tops = new float[stations.Count - 1];
            float underAlong = 0f;
            for (int j = 0; j < tops.Length; j++) {
                int step = Mathf.Clamp(Mathf.FloorToInt((along[j] + along[j + 1]) * 0.5f / depth), 0, Mathf.Max(0, steps - 1));
                tops[j] = lowY + (steps > 0 ? (step + 1) * height : 0f);

                Station a = stations[j], b = stations[j + 1];
                float top = tops[j], ya = Bottom(along[j]), yb = Bottom(along[j + 1]);
                float topA = smooth ? StationTop(j) : top, topB = smooth ? StationTop(j + 1) : top;
                Vector3 middle = (a.center + b.center) * 0.5f;

                int before = faces.Count;
                float treadA = Vector3.Distance(a.left, a.right) * 0.5f, treadB = Vector3.Distance(b.left, b.right) * 0.5f;
                AddMappedStrip(positions, faces, Vector3.up, At(a.left, topA), At(a.right, topA), At(b.right, topB), At(b.left, topB), NoSmoothing,
                    new Vector2(-treadA, along[j]), new Vector2(treadA, along[j]), new Vector2(treadB, along[j + 1]), new Vector2(-treadB, along[j + 1]));
                Tag(faces, before, TopStepRegion);
                before = faces.Count;
                float underNext = underAlong + Vector3.Distance(At(a.center, ya), At(b.center, yb));
                float halfA = Vector3.Distance(a.left, a.right) * 0.5f, halfB = Vector3.Distance(b.left, b.right) * 0.5f;
                AddMappedStrip(positions, faces, Vector3.down, At(a.left, ya), At(a.right, ya), At(b.right, yb), At(b.left, yb),
                    Mathf.Abs(ya - yb) > Epsilon ? UndersideSmoothing : NoSmoothing,
                    new Vector2(halfA, underAlong), new Vector2(-halfA, underAlong), new Vector2(-halfB, underNext), new Vector2(halfB, underNext));
                underAlong = underNext;
                Tag(faces, before, SideRegion);
                List<float> extras = new();
                if (j > 0 && !smooth)
                    extras.Add(tops[j - 1]);
                else if (j == 0 && lowIsLanding)
                    extras.Add(lowY);
                extras = extras.Where(y => y > ya + Epsilon && y < topA - Epsilon).OrderByDescending(y => y).ToList();

                Vector3[] Side(Vector3 from, Vector3 to) => new[] { At(to, topB), At(from, topA) }
                    .Concat(extras.Select(y => At(from, y)))
                    .Concat(new[] { At(from, ya), At(to, yb) })
                    .ToArray();

                before = faces.Count;
                AddFace(positions, faces, (a.left + b.left) * 0.5f - middle, Side(a.left, b.left));
                AddFace(positions, faces, (a.right + b.right) * 0.5f - middle, Side(a.right, b.right));
                Tag(faces, before, SideRegion);
            }

            for (int j = 0; j < stations.Count; j++) {
                float before = j > 0 ? smooth ? StationTop(j) : tops[j - 1] : Bottom(along[j]);
                float after = j < tops.Length ? smooth ? StationTop(j) : tops[j] : Bottom(along[j]);
                if (Mathf.Abs(after - before) < Epsilon)
                    continue;

                Station s = stations[j];
                Vector3 forward = Vector3.Cross(s.right - s.left, Vector3.up);
                int count = faces.Count;
                AddFace(positions, faces, forward * Mathf.Sign(before - after),
                    At(s.left, before), At(s.right, before), At(s.right, after), At(s.left, after));
                Tag(faces, count, before < after ? ForwardStepRegion : SideRegion);
            }
        }

        private static Vector3 Flat(Vector3 v) {
            v.y = 0f;
            return v.sqrMagnitude > Epsilon * Epsilon ? v.normalized : Vector3.zero;
        }

        private static Vector3 FlatPoint(Vector3 v) => new(v.x, 0f, v.z);

        private float BottomY(float lowY, float rise, bool lowIsLanding, float baseY) => activeSolid
            ? Mathf.Min(baseY - lowY, rise < Epsilon ? -activeThickness : 0f)
            : lowIsLanding || rise < Epsilon
                ? -activeThickness
                : 0f;

        private void Flight(List<Vector3> positions, List<Face> faces, Vector3 low, Vector3 high, bool lowIsLanding, float baseY, float halfWidth) {
            Vector3 run = high - low;
            float rise = run.y;
            run.y = 0f;
            float runLength = run.magnitude;

            Vector3 forward = run / runLength;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float bottomY = BottomY(low.y, rise, lowIsLanding, baseY);

            int steps = Steps(rise);
            List<Vector2> profile = Profile(steps, runLength, rise, bottomY);
            List<List<Vector2>> sides = new();

            if (_buildingSmooth && steps > 0) {
                List<Vector2> ramp = new() { profile[0] };
                if (bottomY < -Epsilon)
                    ramp.Add(Vector2.zero);
                ramp.Add(new Vector2(runLength, rise));
                ramp.AddRange(profile.Skip(1 + steps * 2));
                profile = ramp;
                sides.Add(new List<Vector2>(ramp));
            }
            else if (steps == 0) {
                sides.Add(profile);
            }
            else {
                float depth = runLength / steps, height = rise / steps;
                List<Vector2> slab = new() { Vector2.zero };
                for (int i = 0; i < steps; i++) {
                    Vector2 back = new((i + 1) * depth, (i + 1) * height);
                    sides.Add(new List<Vector2> { new(i * depth, i * height), new(i * depth, (i + 1) * height), back });
                    slab.Add(back);
                }

                slab.AddRange(profile.Skip(1 + steps * 2));
                if (bottomY < -Epsilon) {
                    slab.Add(profile[0]);
                    profile.Insert(1, Vector2.zero);
                }
                sides.Add(slab);
            }

            if (CutterOutline.SignedArea(profile) < 0f)
                profile.Reverse();

            Vector3 Point(Vector2 p, float side) => low + forward * p.x + Vector3.up * p.y + right * side;

            for (int k = 0; k < profile.Count; k++) {
                Vector2 a = profile[k], b = profile[(k + 1) % profile.Count];
                Vector3 facing = forward * (b.y - a.y) - Vector3.up * (b.x - a.x);
                int before = faces.Count;
                AddFace(positions, faces, facing, Point(a, -halfWidth), Point(b, -halfWidth), Point(b, halfWidth), Point(a, halfWidth));
                int region = Mathf.Abs(facing.y) > Epsilon
                    ? facing.y > 0f ? TopStepRegion : SideRegion
                    : Vector3.Dot(facing, forward) < 0f ? ForwardStepRegion : SideRegion;
                Tag(faces, before, region);
            }

            foreach (List<Vector2> piece in sides) {
                Vector3[] left = new Vector3[piece.Count], rightSide = new Vector3[piece.Count];
                for (int i = 0; i < piece.Count; i++) {
                    left[i] = Point(piece[i], -halfWidth);
                    rightSide[i] = Point(piece[i], halfWidth);
                }

                int before = faces.Count;
                AddFace(positions, faces, -right, left);
                AddFace(positions, faces, right, rightSide);
                Tag(faces, before, SideRegion);
            }
        }

        public List<List<Vector2>> ProfileLines() {
            if (isSpiral) {
                float rise = Mathf.Max(MinSize, spiralHeight);
                float run = Mathf.Deg2Rad * Mathf.Max(1f, spiralAngle) * (spiralInnerRadius + spiralOuterRadius) * 0.5f;
                return new List<List<Vector2>> { Profile(Steps(rise), run, rise, BottomY(0f, rise, false, 0f)) };
            }

            Prepare(out List<Vector3> points, out _, out _);
            List<List<Vector2>> lines = new();
            if (points.Count < 2)
                return lines;

            float baseY = points.Min(p => p.y), x = 0f;
            int last = points.Count - 1;

            for (int i = 0; i < last; i++) {
                Vector3 a = points[i], b = points[i + 1];
                float runLength = Vector3.Distance(FlatPoint(a), FlatPoint(b));
                if (runLength < Epsilon)
                    continue;

                bool ascending = b.y >= a.y;
                float rise = Mathf.Abs(b.y - a.y), lowY = Mathf.Min(a.y, b.y);
                bool lowIsLanding = ascending ? i > 0 : i + 1 < last;
                float startX = x;

                lines.Add(Profile(Steps(rise), runLength, rise, BottomY(lowY, rise, lowIsLanding, baseY))
                    .Select(p => new Vector2(ascending ? startX + p.x : startX + runLength - p.x, lowY + p.y))
                    .ToList());
                x += runLength;
            }

            return lines;
        }

        public static List<Vector2> SampleProfile(TsitskiProBuilderStairsStats stairsStats, int steps, float treadDepth) {
            float stepHeight = Mathf.Max(MinSize, stairsStats.stepHeight);
            return Profile(steps, steps * treadDepth, steps * stepHeight, 0f, Mathf.Max(MinSize, stairsStats.thickness), stairsStats.solid);
        }

        private List<Vector2> Profile(int steps, float runLength, float rise, float bottomY) =>
            Profile(steps, runLength, rise, bottomY, activeThickness, activeSolid);

        private static List<Vector2> Profile(int steps, float runLength, float rise, float bottomY, float thickness, bool solid) {
            List<Vector2> profile = new() { new Vector2(0f, bottomY) };

            if (steps == 0) {
                profile.Add(new Vector2(0f, rise));
                profile.Add(new Vector2(runLength, rise));
                profile.Add(new Vector2(runLength, bottomY));
                return profile;
            }

            float depth = runLength / steps, height = rise / steps;
            for (int i = 0; i < steps; i++) {
                profile.Add(new Vector2(i * depth, (i + 1) * height));
                profile.Add(new Vector2((i + 1) * depth, (i + 1) * height));
            }

            float slopeOffset = thickness * Mathf.Sqrt(1f + height * height / (depth * depth));
            float underStart = (bottomY + slopeOffset) * depth / height;

            if (solid || underStart >= runLength - Epsilon || rise - slopeOffset <= bottomY + Epsilon) {
                profile.Add(new Vector2(runLength, bottomY));
                return profile;
            }

            profile.Add(new Vector2(runLength, rise - thickness));
            if (underStart > Epsilon)
                profile.Add(new Vector2(underStart, bottomY));
            return profile;
        }

        private void Landing(List<Vector3> positions, List<Face> faces, Vector3 center, Vector3 into, Vector3 outOf, float baseY, float halfWidth) {
            if (into == Vector3.zero)
                into = outOf;
            if (outOf == Vector3.zero)
                outOf = into;
            if (into == Vector3.zero)
                return;

            Vector3 rightIn = Vector3.Cross(Vector3.up, into), rightOut = Vector3.Cross(Vector3.up, outOf);
            List<Vector3> corners = new();

            foreach (float side in new[] { -1f, 1f }) {
                Vector3 arrive = center - into * halfWidth + rightIn * (side * halfWidth);
                Vector3 leave = center + outOf * halfWidth + rightOut * (side * halfWidth);
                corners.Add(arrive);
                corners.Add(leave);

                if (Intersect(arrive, into, leave, outOf, out Vector3 corner) &&
                    (corner - center).sqrMagnitude <= MaxLandingCorner * MaxLandingCorner * halfWidth * halfWidth)
                    corners.Add(corner);
            }

            List<Vector2> outline = ConvexHull(corners.Select(c => new Vector2(c.x, c.z)).ToList());
            if (outline.Count < 3)
                return;

            float topY = center.y;
            float bottomY = activeSolid && baseY < topY - Epsilon ? baseY : topY - activeThickness;
            Vector3 Point(Vector2 p, float y) => new(p.x, y, p.y);

            int before = faces.Count;
            AddFace(positions, faces, Vector3.up, outline.Select(p => Point(p, topY)).ToArray());
            Tag(faces, before, TopStepRegion);
            before = faces.Count;
            AddFace(positions, faces, Vector3.down, outline.Select(p => Point(p, bottomY)).ToArray());

            for (int i = 0; i < outline.Count; i++) {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                Vector3 facing = new(b.y - a.y, 0f, a.x - b.x);
                AddFace(positions, faces, facing, Point(a, bottomY), Point(b, bottomY), Point(b, topY), Point(a, topY));
            }
            Tag(faces, before, SideRegion);
        }

        private void Tag(List<Face> faces, int from, int region) {
            while (_faceRegions.Count < from)
                _faceRegions.Add(-1);
            while (_faceRegions.Count < faces.Count)
                _faceRegions.Add(region);
        }

        private static bool Intersect(Vector3 p, Vector3 d, Vector3 q, Vector3 e, out Vector3 point) {
            point = p;
            float denominator = d.x * e.z - d.z * e.x;
            if (Mathf.Abs(denominator) < 1e-3f)
                return false;

            Vector3 offset = q - p;
            point = p + d * ((offset.x * e.z - offset.z * e.x) / denominator);
            return true;
        }

        private static List<Vector2> ConvexHull(List<Vector2> points) {
            points = points.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            if (points.Count < 3)
                return points;

            float Turn(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

            List<Vector2> hull = new();
            for (int pass = 0; pass < 2; pass++) {
                int floor = hull.Count;
                foreach (Vector2 p in pass == 0 ? points : Enumerable.Reverse(points)) {
                    while (hull.Count >= floor + 2 && Turn(hull[^2], hull[^1], p) <= Epsilon * Epsilon)
                        hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }

                hull.RemoveAt(hull.Count - 1);
            }

            return hull;
        }

        private void AddMappedStrip(List<Vector3> positions, List<Face> faces, Vector3 facing, Vector3 aLeft, Vector3 aRight, Vector3 bRight, Vector3 bLeft,
            int smoothing, Vector2 uvALeft, Vector2 uvARight, Vector2 uvBRight, Vector2 uvBLeft) {
            int start = positions.Count, faceIndex = faces.Count;
            AddStrip(positions, faces, facing, aLeft, aRight, bRight, bLeft, smoothing);
            if (faces.Count == faceIndex || positions.Count - start != 4)
                return;

            Dictionary<Vector3, Vector2> lookup = new() { [aLeft] = uvALeft, [aRight] = uvARight, [bRight] = uvBRight, [bLeft] = uvBLeft };
            for (int i = start; i < positions.Count; i++)
                if (lookup.TryGetValue(positions[i], out Vector2 uv))
                    _manualUvs[i] = uv;
            _manualFaces.Add(faceIndex);
        }

        private static void AddStrip(List<Vector3> positions, List<Face> faces, Vector3 facing, Vector3 aLeft, Vector3 aRight, Vector3 bRight,
            Vector3 bLeft, int smoothing = NoSmoothing) {
            if ((aLeft - aRight).sqrMagnitude < 1e-10f || (bLeft - bRight).sqrMagnitude < 1e-10f ||
                (aLeft - bLeft).sqrMagnitude < 1e-10f || (aRight - bRight).sqrMagnitude < 1e-10f) {
                AddFace(positions, faces, facing, smoothing, aLeft, aRight, bRight, bLeft);
                return;
            }

            Vector3 normal = Vector3.Cross(aRight - aLeft, bRight - aLeft) + Vector3.Cross(bRight - aLeft, bLeft - aLeft);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            int start = positions.Count;
            if (Vector3.Dot(normal, facing) >= 0f)
                positions.AddRange(new[] { aLeft, aRight, bRight, bLeft });
            else
                positions.AddRange(new[] { aLeft, bLeft, bRight, aRight });

            faces.Add(new Face(new[] { start, start + 1, start + 2, start, start + 2, start + 3 }) { smoothingGroup = smoothing });
        }

        private static void AddFace(List<Vector3> positions, List<Face> faces, Vector3 facing, params Vector3[] corners) =>
            AddFace(positions, faces, facing, NoSmoothing, corners);

        private static void AddFace(List<Vector3> positions, List<Face> faces, Vector3 facing, int smoothing, params Vector3[] corners) {
            List<Vector3> unique = new();
            foreach (Vector3 corner in corners)
                if (unique.Count == 0 || (unique[^1] - corner).sqrMagnitude > 1e-10f)
                    unique.Add(corner);
            if (unique.Count > 1 && (unique[0] - unique[^1]).sqrMagnitude <= 1e-10f)
                unique.RemoveAt(unique.Count - 1);
            if (unique.Count < 3)
                return;
            corners = unique.ToArray();

            Vector3 normal = Vector3.zero;
            for (int i = 2; i < corners.Length; i++)
                normal += Vector3.Cross(corners[i - 1] - corners[0], corners[i] - corners[0]);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            if (Vector3.Dot(normal, facing) < 0f)
                Array.Reverse(corners, 1, corners.Length - 1);

            corners = FanFromBestCorner(corners);

            int start = positions.Count;
            positions.AddRange(corners);

            int[] indices = new int[(corners.Length - 2) * 3];
            for (int i = 2; i < corners.Length; i++) {
                indices[(i - 2) * 3] = start;
                indices[(i - 2) * 3 + 1] = start + i - 1;
                indices[(i - 2) * 3 + 2] = start + i;
            }

            faces.Add(new Face(indices) { smoothingGroup = smoothing });
        }

        private static Vector3[] FanFromBestCorner(Vector3[] corners) {
            int count = corners.Length;
            if (count <= 3)
                return corners;

            int apex = 0;
            float best = -1f;
            for (int i = 0; i < count; i++) {
                float score = float.MaxValue;
                for (int k = 0; k < count; k++) {
                    int next = (k + 1) % count;
                    if (k == i || next == i)
                        continue;

                    Vector3 edge = corners[next] - corners[k];
                    float length = edge.magnitude;
                    if (length < 1e-6f)
                        continue;

                    score = Mathf.Min(score, Vector3.Cross(edge / length, corners[i] - corners[k]).magnitude);
                }

                if (score > best) {
                    best = score;
                    apex = i;
                }
            }

            Vector3[] rotated = new Vector3[count];
            for (int i = 0; i < count; i++)
                rotated[i] = corners[(apex + i) % count];
            return rotated;
        }
    }
}
