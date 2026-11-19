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
    public enum ProfileMirror {
        None,
        [InspectorName("Across Y Axis")] AcrossY,
        [InspectorName("Across X Axis")] AcrossX,
        Both
    }

    [AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder Trim")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class TsitskiProBuilderTrim : MonoBehaviour, IPathOwner, IGenerated, ITsitskiPath, IForcedMaterials, IInputHashed, IPivotShiftable {
        bool IPivotShiftable.CanShiftPivot(out string reason) {
            reason = "its path is stored in its stats asset and shared with other objects.";
            return !pathInStats;
        }

        void IPivotShiftable.ShiftLocalPoints(Vector3 offset) => path = PivotShift.Shifted(path, offset);

        void IPivotShiftable.ApplyPivotShift() => Rebuild();

        private const float Epsilon = 1e-5f;
        private const int NoSmoothing = 0;
        private const float MiterLimit = 4f;
        private const float MirrorSnap = 0.01f;
        public const float MinLoopCutStep = 0.1f;
        private const float FlatCornerTurn = 20f;
        private const float FlatCornerPitch = 1f;
        private const float FlatCornerGap = 0.01f;

        [Tooltip(Tooltips.TrimStats)] public TsitskiProBuilderTrimStats stats;

        [Header("Material")]
        [Tooltip(Tooltips.GeneratorMaterial)] public Material material;

        [Header("Profile")]
        [Tooltip(Tooltips.Profile)] public List<Vector2> profile = DefaultProfile();
        [Tooltip(Tooltips.Radii)] public List<float> profileRadii = new();
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.ProfileMirror)] public ProfileMirror profileMirror;

        [Header("Path")]
        [Tooltip(Tooltips.TrimPath)] public List<Vector3> path = new() { Vector3.zero };
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.Closed)] public bool closed;
        [Tooltip(Tooltips.CapEnds)] public bool capEnds = true;

        [Header("Mesh")]
        [Tooltip(Tooltips.SmoothingAngle)] [Range(0f, 180f)] public float smoothingAngle = 30f;
        [Tooltip(Tooltips.LoopCutStep)] [Min(0f)] public float loopCutStep;
        [Tooltip(Tooltips.ApplyCutsToProBuilderMesh)] public bool applyCutsToProBuilderMesh;
        [Tooltip(Tooltips.RoundBevel)] [Min(0)] public int roundBevel;
        [Tooltip(Tooltips.FlattenAngledEnds)] public bool flattenAngledEnds;

        [SerializeField] [HideInInspector] internal MonoBehaviour generator;
        MonoBehaviour IGenerated.generator { get => generator; set => generator = value; }
        [SerializeField] [HideInInspector] internal bool interior;

        public bool? generatedInterior => generator is TsitskiProBuilderFloorPlan ? interior : null;

        public static event Action<ProBuilderMesh> Rebuilt;

        public Material activeMaterial => stats ? stats.material : material;
        Material[] IForcedMaterials.forcedMaterials => new[] { activeMaterial };

#if UNITY_EDITOR
#endif

        public List<Vector2> activeProfile => stats ? stats.profile : profile;
        public float activeSmoothingAngle => stats ? stats.smoothingAngle : smoothingAngle;
        public float activeLoopCutStep => stats ? stats.loopCutStep : loopCutStep;
        public bool activeApplyCutsToProBuilderMesh => stats ? stats.applyCutsToProBuilderMesh : applyCutsToProBuilderMesh;
        public int activeRoundBevel => Mathf.Max(0, stats ? stats.roundBevel : roundBevel);
        public bool activeFlattenAngledEnds => stats ? stats.flattenAngledEnds : flattenAngledEnds;
        public List<float> activeProfileRadii => stats ? stats.profileRadii : profileRadii;
        public int activeRoundingSegments => Mathf.Max(1, stats ? stats.roundingSegments : roundingSegments);
        public ProfileMirror activeProfileMirror => stats ? stats.profileMirror : profileMirror;
        public bool pathInStats => stats && stats.storePath;
        public bool activeClosed => pathInStats ? stats.closed : closed;

        List<Vector3> IPathOwner.localPath { get => path; set => path = value; }

        public List<Vector3> activePath {
            get => pathInStats ? stats.path : path;
            set {
                if (pathInStats)
                    stats.path = value;
                else
                    path = value;
            }
        }

        public List<float> activePathRadii {
            get => pathInStats ? stats.pathRadii : pathRadii;
            set {
                if (pathInStats)
                    stats.pathRadii = value;
                else
                    pathRadii = value;
            }
        }

        public List<Vector2> RoundedProfile() =>
            BuildProfile(activeProfile, activeProfileRadii, activeProfileMirror, activeRoundingSegments);

        public static List<Vector2> BuildProfile(List<Vector2> source, List<float> sourceRadii, ProfileMirror mirror, int segments) {
            if (source == null)
                return new List<Vector2>();

            List<Vector2> points = new(source);
            List<float> radii = PointRounding.Synced(sourceRadii, points.Count);

            const int acrossY = 0;
            const int acrossX = 1;
            float extent = 0f;
            foreach (Vector2 p in points)
                extent = Mathf.Max(extent, Mathf.Abs(p.x), Mathf.Abs(p.y));
            float tolerance = Mathf.Max(Epsilon, extent * MirrorSnap);

            if (mirror == ProfileMirror.Both && points.Count > 0) {
                bool xFirst = Mathf.Abs(points[^1].y) <= Mathf.Abs(points[^1].x);
                Mirror(points, radii, xFirst ? acrossX : acrossY, tolerance);
                Mirror(points, radii, xFirst ? acrossY : acrossX, tolerance);
            }
            else if (mirror == ProfileMirror.AcrossY) {
                Mirror(points, radii, acrossY, tolerance);
            }
            else if (mirror == ProfileMirror.AcrossX) {
                Mirror(points, radii, acrossX, tolerance);
            }

            return CleanProfile(PointRounding.Round(points, radii, true, Mathf.Max(1, segments)));
        }

        private static void Mirror(List<Vector2> points, List<float> radii, int axis, float tolerance) {
            float furthest = 0f;
            foreach (Vector2 p in points)
                if (Mathf.Abs(p[axis]) > Mathf.Abs(furthest))
                    furthest = p[axis];

            if (Mathf.Abs(furthest) <= tolerance)
                return;

            float side = Mathf.Sign(furthest);
            for (int i = 0; i < points.Count; i++) {
                Vector2 p = points[i];
                if (p[axis] * side < tolerance) {
                    p[axis] = 0f;
                    points[i] = p;
                }
            }

            bool OnAxis(int index) => points[index][axis] == 0f;

            while (points.Count >= 2 && OnAxis(0) && OnAxis(1)) {
                points.RemoveAt(0);
                radii.RemoveAt(0);
            }

            while (points.Count >= 2 && OnAxis(points.Count - 1) && OnAxis(points.Count - 2)) {
                points.RemoveAt(points.Count - 1);
                radii.RemoveAt(radii.Count - 1);
            }

            Vector2 scale = axis == 0 ? new Vector2(-1f, 1f) : new Vector2(1f, -1f);
            for (int i = points.Count - 1; i >= 0; i--) {
                Vector2 mirrored = Vector2.Scale(points[i], scale);
                if (points.Count > 0 && (points[^1] - mirrored).sqrMagnitude <= Epsilon * Epsilon)
                    continue;

                points.Add(mirrored);
                radii.Add(radii[i]);
            }

            if (points.Count > 1 && (points[0] - points[^1]).sqrMagnitude <= Epsilon * Epsilon) {
                points.RemoveAt(points.Count - 1);
                radii.RemoveAt(radii.Count - 1);
            }
        }

        public List<Vector3> GetWorldPath(out bool closed) {
            List<Vector3> points = RoundedPath(out closed);

            for (int i = 0; i < points.Count; i++)
                points[i] = transform.TransformPoint(points[i]);

            return points;
        }

        private List<Vector3> RoundedPath(out bool loop) {
            FlatCorners(activePath, activePathRadii, activeClosed, out List<Vector3> points, out List<float> radii);
            return PathUtility.Rounded(points, radii, activeClosed, activeRoundingSegments, out loop);
        }

        private void FlatCorners(List<Vector3> source, List<float> sourceRadii, bool closed, out List<Vector3> points, out List<float> radii) {
            points = new List<Vector3>();
            radii = new List<float>();
            if (source == null)
                return;

            float maxX = 0f, maxY = 0f;
            foreach (Vector2 p in RoundedProfile()) {
                maxX = Mathf.Max(maxX, Mathf.Abs(p.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(p.y));
            }

            int count = source.Count;
            bool wraps = closed && count >= 3;
            bool flattenEnds = !wraps && count >= 2 && activeFlattenAngledEnds;

            for (int i = 0; i < count; i++) {
                Vector3 point = source[i];
                float radius = PointRounding.Radius(sourceRadii, i);
                bool corner = radius <= 0f && (wraps || (i > 0 && i < count - 1));

                if (flattenEnds && i == 0) {
                    points.Add(point);
                    radii.Add(radius);
                    if (FlatEnd(point, source[1], maxY, out Vector3 stub)) {
                        points.Add(stub);
                        radii.Add(0f);
                    }
                    continue;
                }

                if (flattenEnds && i == count - 1) {
                    if (FlatEnd(point, source[i - 1], maxY, out Vector3 stub)) {
                        points.Add(stub);
                        radii.Add(0f);
                    }
                    points.Add(point);
                    radii.Add(radius);
                    continue;
                }

                if (!corner || !FlatCorner(source[(i - 1 + count) % count], point, source[(i + 1) % count], radius, maxX, maxY,
                        out Vector3 before, out Vector3 after)) {
                    points.Add(point);
                    radii.Add(radius);
                    continue;
                }

                if (before != point) {
                    points.Add(before);
                    radii.Add(0f);
                }

                points.Add(point);
                radii.Add(radius);

                if (after != point) {
                    points.Add(after);
                    radii.Add(0f);
                }
            }
        }

        private static bool FlatEnd(Vector3 end, Vector3 toward, float maxY, out Vector3 stub) {
            stub = end;
            Vector3 offset = toward - end;
            Vector3 flat = new(offset.x, 0f, offset.z);
            float length = flat.magnitude;
            if (length < Epsilon)
                return false;

            float pitch = Mathf.Atan2(Mathf.Abs(offset.y), length) * Mathf.Rad2Deg;
            if (pitch <= FlatCornerPitch * 0.5f)
                return false;

            float extent = Mathf.Min(maxY * Mathf.Tan(pitch * 0.5f * Mathf.Deg2Rad) + FlatCornerGap, length * 0.45f);
            stub = end + flat / length * extent;
            return true;
        }

        private static bool FlatCorner(Vector3 previous, Vector3 point, Vector3 next, float radius, float maxX, float maxY,
            out Vector3 before, out Vector3 after) {
            before = after = point;
            Vector3 into = point - previous, outOf = next - point;
            Vector3 flatIn = new(into.x, 0f, into.z), flatOut = new(outOf.x, 0f, outOf.z);
            float lengthIn = flatIn.magnitude, lengthOut = flatOut.magnitude;
            if (lengthIn < Epsilon || lengthOut < Epsilon)
                return false;

            float turn = Vector3.Angle(flatIn, flatOut);
            float pitchIn = Mathf.Atan2(into.y, lengthIn) * Mathf.Rad2Deg, pitchOut = Mathf.Atan2(outOf.y, lengthOut) * Mathf.Rad2Deg;
            bool slopedIn = Mathf.Abs(pitchIn) > FlatCornerPitch * 0.5f, slopedOut = Mathf.Abs(pitchOut) > FlatCornerPitch * 0.5f;

            if (turn < 0.5f || !slopedIn && !slopedOut)
                return false;
            if (Mathf.Abs(pitchIn - pitchOut) < FlatCornerPitch && turn < FlatCornerTurn)
                return false;

            float halfTurn = Mathf.Min(Mathf.Tan(turn * 0.5f * Mathf.Deg2Rad), MiterLimit);
            float turnExtent = Mathf.Max(radius * halfTurn, maxX * halfTurn);

            float Extent(float pitch, float length) =>
                Mathf.Min(turnExtent + maxY * Mathf.Tan(Mathf.Abs(pitch) * 0.5f * Mathf.Deg2Rad) + FlatCornerGap, length * 0.45f);

            if (slopedIn)
                before = point - flatIn / lengthIn * Extent(pitchIn, lengthIn);
            if (slopedOut)
                after = point + flatOut / lengthOut * Extent(pitchOut, lengthOut);
            return true;
        }

        public static List<Vector2> DefaultProfile() => new() {
            new(0f, 0f), new(0.175f, 0f), new(0.175f, 0.125f), new(0.1f, 0.25f), new(0f, 0.25f)
        };

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

            TsitskiProBuilderTrimStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() => TsitskiProBuilderTrimStats.Changed -= OnStatsChanged;

        private void OnValidate() => QueueRebuild();

        private void OnStatsChanged(TsitskiProBuilderTrimStats changed) {
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
            TsitskiSettings.AutoName(this, stats, "ProBuilder Trim");

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            KeptFaceMaterials kept = KeptFaceMaterials.Capture(mesh);

            TrimMesh data = new();
            Build(data);
            if (!activeApplyCutsToProBuilderMesh)
                data.MergeCuts();

            if (data.faces.Count == 0) {
                mesh.Clear();
            }
            else {
                ManualUvs.Rebuild(mesh, data.positions, data.faces, data.uvs);
                kept?.Apply(mesh);
                ForcedMaterials.Apply(mesh, null, activeMaterial);
            }
            mesh.ToMesh();
            mesh.Refresh();

            if (data.faces.Count > 0)
                ApplyLightmapUvs(data);

            if (TryGetComponent(out MeshRenderer meshRenderer) && !meshRenderer.sharedMaterial)
                meshRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
            Rebuilt?.Invoke(mesh);
            TsitskiPathEvents.NotifyChanged(this);
        }

        private class TrimMesh {
            public readonly List<Vector3> positions = new();
            public readonly List<Face> faces = new();
            public readonly List<Vector2> uvs = new();
            public readonly List<Vector2> unwrap = new();
            public readonly List<int> charts = new();
            public readonly List<int> groups = new();
            public int chartCount;

            public void MergeCuts() {
                List<int> kept = FaceGroups.Merge(positions, faces, groups,
                    (a, b) => uvs[a] == uvs[b] && unwrap[a] == unwrap[b] && charts[a] == charts[b]);
                List<Vector2> keptUvs = kept.Select(i => uvs[i]).ToList();
                List<Vector2> keptUnwrap = kept.Select(i => unwrap[i]).ToList();
                List<int> keptCharts = kept.Select(i => charts[i]).ToList();
                uvs.Clear();
                uvs.AddRange(keptUvs);
                unwrap.Clear();
                unwrap.AddRange(keptUnwrap);
                charts.Clear();
                charts.AddRange(keptCharts);
            }
        }

        private void ApplyLightmapUvs(TrimMesh data) {
            Mesh unityMesh = TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (!unityMesh || unityMesh.vertexCount != data.positions.Count)
                return;

            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z), Epsilon);
            float padding = CutLightmapUnwrap.LightmapPadding(GetComponent<MeshRenderer>()) / maxScale;

            Vector2[] mins = Enumerable.Repeat(Vector2.positiveInfinity, data.chartCount).ToArray();
            Vector2[] maxs = Enumerable.Repeat(Vector2.negativeInfinity, data.chartCount).ToArray();
            for (int i = 0; i < data.unwrap.Count; i++) {
                int chart = data.charts[i];
                mins[chart] = Vector2.Min(mins[chart], data.unwrap[i]);
                maxs[chart] = Vector2.Max(maxs[chart], data.unwrap[i]);
            }

            List<Vector2> sizes = new();
            for (int c = 0; c < data.chartCount; c++)
                sizes.Add(maxs[c].x >= mins[c].x ? maxs[c] - mins[c] : Vector2.zero);

            Vector2[] packed = new Vector2[data.chartCount];
            float uvScale = CutLightmapUnwrap.PackRects(sizes, padding, packed);

            List<Vector2> lightmapUvs = new(data.unwrap.Count);
            for (int i = 0; i < data.unwrap.Count; i++) {
                int chart = data.charts[i];
                lightmapUvs.Add((data.unwrap[i] - mins[chart] + packed[chart]) * uvScale);
            }

            unityMesh.SetUVs(1, lightmapUvs);
        }

        private void Build(TrimMesh data) {
            List<Vector2> shape = RoundedProfile();
            List<Vector3> points = RoundedPath(out bool loop);

            if (shape.Count < 3 || points.Count < 2)
                return;

            PathUtility.Segments(points, loop, out Vector3[] directions, out float[] lengths, out Vector3[] rights, out Vector3[] ups);
            int segmentCount = directions.Length;

            float radius = Epsilon;
            foreach (Vector2 p in shape)
                radius = Mathf.Max(radius, p.magnitude);
            float miterLength = MiterLimit * radius;

            Vector3 Offset(int s, Vector2 p) => rights[s] * p.x + ups[s] * p.y;

            Vector3[][] starts = new Vector3[segmentCount][];
            Vector3[][] ends = new Vector3[segmentCount][];

            for (int s = 0; s < segmentCount; s++) {
                starts[s] = new Vector3[shape.Count];
                ends[s] = new Vector3[shape.Count];
            }

            if (!loop) {
                for (int k = 0; k < shape.Count; k++) {
                    starts[0][k] = points[0] + Offset(0, shape[k]);
                    ends[segmentCount - 1][k] = points[^1] + Offset(segmentCount - 1, shape[k]);
                }
            }

            int cornerStart = loop ? 0 : 1;
            int cornerEnd = loop ? points.Count : points.Count - 1;
            int bevel = activeRoundBevel;
            List<Vector3[]>[] cornerRings = new List<Vector3[]>[points.Count];

            for (int v = cornerStart; v < cornerEnd; v++) {
                int a = (v - 1 + segmentCount) % segmentCount;
                int b = v % segmentCount;
                Vector3 corner = points[v];
                Vector3 bisector = directions[a] + directions[b];
                bisector = bisector.sqrMagnitude > 1e-6f ? bisector.normalized : directions[a];

                float backA = loop || a > 0 ? lengths[a] * 0.5f : lengths[a];
                float backB = loop || b < segmentCount - 1 ? lengths[b] * 0.5f : lengths[b];
                float dotA = Mathf.Max(Vector3.Dot(directions[a], bisector), Epsilon);
                float dotB = Vector3.Dot(directions[b], bisector);
                if (Mathf.Abs(dotB) < Epsilon)
                    dotB = Epsilon;

                List<Vector3[]> rings = new();
                for (int j = 0; j <= Mathf.Max(1, bevel); j++)
                    rings.Add(new Vector3[shape.Count]);
                Quaternion turn = Quaternion.LookRotation(directions[b], ups[b]) *
                                  Quaternion.Inverse(Quaternion.LookRotation(directions[a], ups[a]));

                for (int k = 0; k < shape.Count; k++) {
                    Vector3 offsetA = Offset(a, shape[k]);
                    Vector3 offsetB = Offset(b, shape[k]);
                    float rawA = -Vector3.Dot(offsetA, bisector) / dotA;
                    float tA = Mathf.Clamp(rawA, -backA, miterLength);
                    float tB = Mathf.Clamp(-Vector3.Dot(offsetB, bisector) / dotB, -miterLength, backB);
                    Vector3 mitreA = corner + offsetA + directions[a] * tA;
                    Vector3 mitreB = corner + offsetB + directions[b] * tB;
                    if (bevel == 0)
                        mitreA = mitreB = (mitreA + mitreB) * 0.5f;

                    for (int j = 0; j < rings.Count; j++) {
                        float t = (float)j / (rings.Count - 1);
                        rings[j][k] = bevel > 0 && rawA > 0f
                            ? corner + Quaternion.Slerp(Quaternion.identity, turn, t) * offsetA
                            : Vector3.Lerp(mitreA, mitreB, t);
                    }

                    ends[a][k] = rings[0][k];
                    starts[b][k] = rings[^1][k];
                }

                cornerRings[v] = rings;
            }

            int[] runs = SmoothingRuns(shape);
            int runCount = runs.Length == 0 ? 0 : Mathf.Max(runs) + 1;
            int[] chains = SegmentChains(directions, loop, bevel > 0);

            List<Vector3[]> sequence = new();
            List<int> intervalChains = new();
            List<int> intervalSegments = new();

            for (int s = 0; s < segmentCount; s++) {
                List<Vector3[]> rings = SegmentRings(points[s], directions[s], lengths[s], starts[s], ends[s]);
                if (sequence.Count == 0)
                    sequence.Add(rings[0]);
                for (int r = 1; r < rings.Count; r++) {
                    sequence.Add(rings[r]);
                    intervalChains.Add(chains[s]);
                    intervalSegments.Add(s);
                }

                int v = loop ? (s + 1) % points.Count : s + 1;
                if (v < cornerStart || v >= cornerEnd || cornerRings[v] == null)
                    continue;

                int b = v % segmentCount;
                int cornerChain = chains[s] == chains[b] ? chains[b] : -1;
                List<Vector3[]> corner = cornerRings[v];
                for (int r = 1; r < corner.Count; r++) {
                    sequence.Add(corner[r]);
                    intervalChains.Add(cornerChain);
                    intervalSegments.Add(-1);
                }
            }

            float[][] rails = new float[sequence.Count][];
            float[] railMeans = new float[sequence.Count];
            rails[0] = new float[shape.Count];
            for (int i = 1; i < sequence.Count; i++) {
                rails[i] = new float[shape.Count];
                for (int k = 0; k < shape.Count; k++)
                    rails[i][k] = rails[i - 1][k] + Vector3.Distance(sequence[i][k], sequence[i - 1][k]);
                railMeans[i] = rails[i].Average();
            }

            float[] around = new float[shape.Count + 1];
            for (int k = 0; k < shape.Count; k++)
                around[k + 1] = around[k] + Vector2.Distance(shape[k], shape[(k + 1) % shape.Count]);

            float pathLength = railMeans[^1];
            int chunkCount = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(pathLength / Mathf.Max(around[^1], Epsilon))));
            float chunkLength = Mathf.Max(pathLength / chunkCount, Epsilon);
            data.chartCount = chunkCount;

            float[] ringU = new float[shape.Count];
            int ringSegment = -1;
            float segmentU = 0f;

            for (int i = 0; i < sequence.Count - 1; i++) {
                int chain = intervalChains[i];
                int chart = Mathf.Min(chunkCount - 1, Mathf.FloorToInt((railMeans[i] + railMeans[i + 1]) * 0.5f / chunkLength));
                int segment = intervalSegments[i];

                float[] fromU = ringU, toU = new float[shape.Count];
                if (segment >= 0) {
                    float Along(Vector3 p) => Vector3.Dot(p - points[segment], directions[segment]);
                    if (segment != ringSegment) {
                        fromU = new float[shape.Count];
                        float projected = 0f;
                        for (int k = 0; k < shape.Count; k++)
                            projected += Along(sequence[i][k]);
                        segmentU = ringU.Average() - projected / shape.Count;
                    }
                    for (int k = 0; k < shape.Count; k++) {
                        fromU[k] = segmentU + Along(sequence[i][k]);
                        toU[k] = segmentU + Along(sequence[i + 1][k]);
                    }
                }
                else {
                    for (int k = 0; k < shape.Count; k++)
                        toU[k] = fromU[k] + railMeans[i + 1] - railMeans[i];
                }
                ringSegment = segment;
                ringU = toU;

                for (int k = 0; k < shape.Count; k++) {
                    int next = (k + 1) % shape.Count;
                    int group = chain >= 0 && activeSmoothingAngle > 0f && runCount > 0 ? 1 + chain * 8 + runs[k] % 8 : NoSmoothing;
                    int cutGroup = segment < 0 ? -1 : (segment * shape.Count + k) * chunkCount + chart;
                    AddFace(data, group, chart, cutGroup,
                        new[] { sequence[i][k], sequence[i][next], sequence[i + 1][next], sequence[i + 1][k] },
                        new[] {
                            new Vector2(fromU[k], around[k]), new Vector2(fromU[next], around[k + 1]),
                            new Vector2(toU[next], around[k + 1]), new Vector2(toU[k], around[k])
                        },
                        new[] {
                            new Vector2(rails[i][k], around[k]), new Vector2(rails[i][next], around[k + 1]),
                            new Vector2(rails[i + 1][next], around[k + 1]), new Vector2(rails[i + 1][k], around[k])
                        });
                }
            }

            if (loop || !capEnds)
                return;

            int endChart = data.chartCount++;
            int startChart = data.chartCount++;

            foreach (List<Vector2> piece in CutterOutline.ConvexPieces(shape)) {
                Vector3[] end = new Vector3[piece.Count];
                Vector3[] start = new Vector3[piece.Count];
                Vector2[] endUvs = new Vector2[piece.Count];
                Vector2[] startUvs = new Vector2[piece.Count];
                for (int i = 0; i < piece.Count; i++) {
                    end[i] = points[^1] + Offset(segmentCount - 1, piece[i]);
                    endUvs[i] = piece[i];
                    start[piece.Count - 1 - i] = points[0] + Offset(0, piece[i]);
                    startUvs[piece.Count - 1 - i] = new Vector2(-piece[i].x, piece[i].y);
                }

                AddFace(data, NoSmoothing, endChart, -1, end, endUvs);
                AddFace(data, NoSmoothing, startChart, -1, start, startUvs);
            }
        }

        private int[] SegmentChains(Vector3[] directions, bool loop, bool allSmooth) {
            int count = directions.Length;
            int[] chains = new int[count];
            if (allSmooth || count == 0)
                return chains;

            bool Smooth(int from, int to) => Vector3.Angle(directions[from], directions[to]) < activeSmoothingAngle;

            int[] ids = new int[count];
            for (int s = 1; s < count; s++)
                ids[s] = Smooth(s - 1, s) ? ids[s - 1] : ids[s - 1] + 1;

            int last = ids[count - 1];
            bool wrapSmooth = loop && count > 1 && Smooth(count - 1, 0);

            for (int s = 0; s < count; s++) {
                int id = wrapSmooth && ids[s] == last ? 0 : ids[s];
                chains[s] = id % 3;

                if (loop && !wrapSmooth && id == last && last > 0 && chains[s] == 0)
                    chains[s] = (last - 1) % 3 == 1 ? 2 : 1;
            }

            return chains;
        }

        private List<Vector3[]> SegmentRings(Vector3 origin, Vector3 direction, float length, Vector3[] start, Vector3[] end) {
            List<Vector3[]> rings = new() { start };
            float step = activeLoopCutStep;
            int cuts = step >= MinLoopCutStep ? Mathf.RoundToInt(length / step) - 1 : 0;

            for (int c = 1; c <= cuts; c++) {
                float distance = length * c / (cuts + 1);
                Vector3[] ring = new Vector3[start.Length];

                for (int k = 0; k < start.Length; k++) {
                    float from = Vector3.Dot(start[k] - origin, direction);
                    float to = Vector3.Dot(end[k] - origin, direction);
                    ring[k] = start[k] + direction * (Mathf.Clamp(distance, from, Mathf.Max(from, to)) - from);
                }

                rings.Add(ring);
            }

            rings.Add(end);
            return rings;
        }

        public Matrix4x4 ProfileFrame() => PathUtility.StartFrame(RoundedPath(out bool loop), loop);

        private static void AddFace(TrimMesh data, int smoothingGroup, int chart, int cutGroup, Vector3[] corners, Vector2[] uvs,
            Vector2[] unwrap = null) {
            unwrap ??= uvs;
            List<Vector3> unique = new();
            List<Vector2> uniqueUvs = new();
            List<Vector2> uniqueUnwrap = new();
            for (int i = 0; i < corners.Length; i++)
                if (unique.Count == 0 || (unique[^1] - corners[i]).sqrMagnitude > Epsilon * Epsilon) {
                    unique.Add(corners[i]);
                    uniqueUvs.Add(uvs[i]);
                    uniqueUnwrap.Add(unwrap[i]);
                }
            if (unique.Count > 1 && (unique[0] - unique[^1]).sqrMagnitude <= Epsilon * Epsilon) {
                unique.RemoveAt(unique.Count - 1);
                uniqueUvs.RemoveAt(uniqueUvs.Count - 1);
                uniqueUnwrap.RemoveAt(uniqueUnwrap.Count - 1);
            }

            if (unique.Count < 3)
                return;

            Vector3 normal = Vector3.zero;
            for (int i = 2; i < unique.Count; i++)
                normal += Vector3.Cross(unique[i - 1] - unique[0], unique[i] - unique[0]);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            int start = data.positions.Count;
            data.positions.AddRange(unique);
            data.uvs.AddRange(uniqueUvs);
            data.unwrap.AddRange(uniqueUnwrap);
            for (int i = 0; i < unique.Count; i++)
                data.charts.Add(chart);

            int[] indices = new int[(unique.Count - 2) * 3];
            for (int i = 2; i < unique.Count; i++) {
                indices[(i - 2) * 3] = start;
                indices[(i - 2) * 3 + 1] = start + i - 1;
                indices[(i - 2) * 3 + 2] = start + i;
            }

            data.faces.Add(new Face(indices) { smoothingGroup = smoothingGroup, manualUV = true });
            data.groups.Add(cutGroup);
        }

        private int[] SmoothingRuns(List<Vector2> shape) {
            int count = shape.Count;
            int[] runs = new int[count];
            if (activeSmoothingAngle <= 0f)
                return runs;

            bool Smooth(int edge) {
                Vector2 previous = shape[(edge + count) % count] - shape[(edge - 1 + count) % count];
                Vector2 current = shape[(edge + 1) % count] - shape[(edge + count) % count];
                return Vector2.Angle(previous, current) < activeSmoothingAngle;
            }

            int first = 0;
            while (first < count && Smooth(first))
                first++;
            if (first == count)
                return runs;

            int run = 0;
            for (int i = 0; i < count; i++) {
                int edge = (first + i) % count;
                if (i > 0 && !Smooth(edge))
                    run++;
                runs[edge] = run;
            }

            return runs;
        }

        private static List<Vector2> CleanProfile(List<Vector2> source) {
            List<Vector2> shape = new();
            if (source == null)
                return shape;

            foreach (Vector2 p in source)
                if (shape.Count == 0 || (shape[^1] - p).sqrMagnitude > Epsilon * Epsilon)
                    shape.Add(p);
            if (shape.Count > 1 && (shape[0] - shape[^1]).sqrMagnitude <= Epsilon * Epsilon)
                shape.RemoveAt(shape.Count - 1);

            if (shape.Count < 3 || Mathf.Abs(CutterOutline.SignedArea(shape)) < 1e-10f)
                return new List<Vector2>();

            if (CutterOutline.SignedArea(shape) < 0f)
                shape.Reverse();

            return shape;
        }
    }
}
