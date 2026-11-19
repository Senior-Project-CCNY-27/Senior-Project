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
    [AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder Pillar")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class TsitskiProBuilderPillar : MonoBehaviour, IGenerated, IForcedMaterials, IInputHashed, IPivotShiftable {
        bool IPivotShiftable.CanShiftPivot(out string reason) {
            reason = "a pillar is built around its pivot.";
            return false;
        }

        void IPivotShiftable.ShiftLocalPoints(Vector3 offset) { }

        void IPivotShiftable.ApplyPivotShift() { }

        public const int MinSides = 3;
        public const float DefaultBottomHeight = 0.15f;
        public const float DefaultTopHeight = 0.15f;
        public const float DefaultCenterHeight = 0.5f;
        public const float MinCenterHeight = 0.01f;
        private const float Epsilon = 1e-5f;
        private const float MinScale = 0.01f;
        public const float MinCornerLength = 0.01f;
        private const float MinCornerSine = 0.017f;
        private const float MinMiterDenominator = 0.05f;

        [Tooltip(Tooltips.PillarStats)] public TsitskiProBuilderPillarStats stats;

        [Header("Material")]
        [Tooltip(Tooltips.GeneratorMaterial)] public Material material;

        [Header("Shape")]
        [Tooltip(Tooltips.PillarHeight)] [Min(0.01f)] public float height = 3f;
        [Tooltip(Tooltips.PillarWidth)] [Min(0.001f)] public float width = 0.3f;
        [Tooltip(Tooltips.PillarShape)] public Shape shape;

        [Header("Placement")]
        [Tooltip(Tooltips.AutoSnapToPath)] [FormerlySerializedAs("snapToPath")] public bool autoSnapToPath = true;
        [Tooltip(Tooltips.SnapToFlats)] public bool snapToFlats;
        [Tooltip(Tooltips.GenerateDownwards)] public bool generateDownwards;
        [Tooltip(Tooltips.SnapDistance)] [Min(0f)] public float snapDistance = 1f;

        [Header("Corner")]
        [Tooltip(Tooltips.CornerSide)] public CornerSide cornerSide;
        [Tooltip(Tooltips.CornerLength)] [Min(MinCornerLength)] public float cornerLength = 0.5f;
        [Tooltip(Tooltips.PillarRoundBevel)] [Min(0)] public int roundBevel;
        [Tooltip(Tooltips.CornerStart)] public Vector3 cornerStart = new(0f, 0f, 0.5f);
        [Tooltip(Tooltips.CornerEnd)] public Vector3 cornerEnd = new(0.5f, 0f, 0f);

        [Header("Profile")]
        [Tooltip(Tooltips.BottomProfile)] public PillarProfile bottomProfile = PillarProfile.DefaultBottom();
        [Tooltip(Tooltips.BottomHeight)] [Min(0f)] public float bottomHeight = DefaultBottomHeight;
        [Tooltip(Tooltips.CenterProfile)] public PillarProfile centerProfile = PillarProfile.DefaultCenter();
        [Tooltip(Tooltips.StretchCenter)] public bool stretchCenter = true;
        [Tooltip(Tooltips.CenterHeight)] [Min(MinCenterHeight)] public float centerHeight = DefaultCenterHeight;
        [Tooltip(Tooltips.TopProfile)] public PillarProfile topProfile = PillarProfile.DefaultTop();
        [Tooltip(Tooltips.TopHeight)] [Min(0f)] public float topHeight = DefaultTopHeight;

        [Header("Mesh")]
        [Tooltip(Tooltips.PillarSides)] [Min(MinSides)] public int sides = 12;
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.SmoothingAngle)] [Range(0f, 180f)] public float smoothingAngle = 40f;

        [SerializeField] [HideInInspector] [FormerlySerializedAs("profile")] private List<Vector2> legacyProfile = new();
        [SerializeField] [HideInInspector] [FormerlySerializedAs("profileRadii")] private List<float> legacyProfileRadii = new();
        [SerializeField] [HideInInspector] [FormerlySerializedAs("cornerCenter")] private Vector3 legacyCornerCenter;
        [SerializeField] [HideInInspector] private float lastWidth = -1f;

        [SerializeField] [HideInInspector] internal MonoBehaviour generator;
        MonoBehaviour IGenerated.generator { get => generator; set => generator = value; }

        public bool? generatedInterior => generator is TsitskiArray { generatedInterior: not null } ? cornerSide == CornerSide.Inside : null;

        public static event Action<ProBuilderMesh> Rebuilt;

        public Material activeMaterial => stats ? stats.material : material;
        Material[] IForcedMaterials.forcedMaterials => new[] { activeMaterial };

        public Shape activeShape => stats ? stats.shape : shape;
        public bool activeAutoSnapToPath => stats ? stats.autoSnapToPath : autoSnapToPath;
        public bool activeSnapToFlats => stats ? stats.snapToFlats : snapToFlats;
        public bool activeGenerateDownwards => stats ? stats.generateDownwards : generateDownwards;
        public float baseOffset => activeGenerateDownwards ? -height : 0f;
        public float activeCornerLength => Mathf.Max(MinCornerLength, stats ? stats.cornerLength : cornerLength);
        public int activeRoundBevel => Mathf.Max(0, stats ? stats.roundBevel : roundBevel);
        public PillarProfile activeBottomProfile => stats ? stats.bottomProfile : bottomProfile;
        public PillarProfile activeCenterProfile => stats ? stats.centerProfile : centerProfile;
        public PillarProfile activeTopProfile => stats ? stats.topProfile : topProfile;
        public float activeBottomHeight => stats ? stats.bottomHeight : bottomHeight;
        public float activeTopHeight => stats ? stats.topHeight : topHeight;
        public bool activeStretchCenter => stats ? stats.stretchCenter : stretchCenter;
        public float activeCenterHeight => Mathf.Max(MinCenterHeight, stats ? stats.centerHeight : centerHeight);
        public int activeSides => Mathf.Max(MinSides, stats ? stats.sides : sides);
        public int activeRoundingSegments => Mathf.Max(1, stats ? stats.roundingSegments : roundingSegments);
        public float activeSmoothingAngle => stats ? stats.smoothingAngle : smoothingAngle;

        public enum Part {
            Bottom,
            Middle,
            Top
        }

        public enum Shape {
            Round,
            Corner
        }

        public struct Corner {
            public Vector3 center, start, end;
            public Vector3 startDirection, endDirection;
            public Vector3 startNormal, endNormal, miter;
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

            MigrateLegacyProfile();
            MigrateLegacyCornerCenter();
            TsitskiProBuilderPillarStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() => TsitskiProBuilderPillarStats.Changed -= OnStatsChanged;

        private void OnValidate() {
            MigrateLegacyProfile();
            bottomHeight = Mathf.Max(0f, bottomHeight);
            topHeight = Mathf.Max(0f, topHeight);
            centerHeight = Mathf.Max(MinCenterHeight, centerHeight);
            SyncWidth();
            QueueRebuild();
        }

        private void MigrateLegacyCornerCenter() {
            if (legacyCornerCenter == Vector3.zero)
                return;

            transform.position = transform.TransformPoint(legacyCornerCenter);
            cornerStart -= legacyCornerCenter;
            cornerEnd -= legacyCornerCenter;
            legacyCornerCenter = Vector3.zero;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(transform);
#endif
        }

        private void MigrateLegacyProfile() {
            if (legacyProfile is not { Count: >= 2 })
                return;

            if (PillarProfile.Split(legacyProfile, legacyProfileRadii, bottomHeight, topHeight,
                    out bottomProfile, out centerProfile, out topProfile, out bottomHeight, out float center, out topHeight))
                centerHeight = Mathf.Max(MinCenterHeight, center);

            legacyProfile = new List<Vector2>();
            legacyProfileRadii = new List<float>();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        private void OnStatsChanged(TsitskiProBuilderPillarStats changed) {
            if (changed == stats)
                QueueRebuild();
        }

        public void SyncWidth() => PillarProfile.SyncWidth(ref width, ref lastWidth, bottomProfile, centerProfile, topProfile);

        public void QueueRebuild() => RebuildQueue.Queue(this, Rebuild);

        [SerializeField] [HideInInspector] private int inputHash;
        int IInputHashed.inputHash { get => inputHash; set => inputHash = value; }

        public void Rebuild() {
            BuildGenerated();
            GeneratorInputs.Store(this);
        }

        private void BuildGenerated() {
            TsitskiSettings.AutoName(this, stats, "ProBuilder Pillar");
            SyncWidth();

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            KeptFaceMaterials kept = KeptFaceMaterials.Capture(mesh);

            List<Vector3> positions = new();
            List<Vector2> uvs = new();
            List<Face> faces = new();
            Build(positions, uvs, faces);

            if (faces.Count == 0)
                mesh.Clear();
            else {
                ManualUvs.Rebuild(mesh, positions, faces, uvs);
                kept?.Apply(mesh);
                ForcedMaterials.Apply(mesh, null, activeMaterial);
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

        public static List<Vector2> RoundProfile(List<Vector2> source, List<float> radii, int segments) {
            List<Vector2> points = new();
            if (source == null)
                return points;

            foreach (Vector2 p in PointRounding.Round(source, radii, false, Mathf.Max(1, segments)))
                if (points.Count == 0 || (points[^1] - p).sqrMagnitude > Epsilon * Epsilon)
                    points.Add(p);
            return points;
        }

        public struct Layout {
            public float height;
            public float bottomSpan, topSpan, middleStart, topStart, tile;
            public int repeats;
            public float squish;

            public float Start(Part part) => part switch {
                Part.Bottom => 0f,
                Part.Middle => middleStart,
                _ => topStart
            };

            public float Span(Part part) => part switch {
                Part.Bottom => bottomSpan,
                Part.Middle => tile,
                _ => topSpan
            };

            public float ToPillar(Part part, float y) => Start(part) + y * Span(part);
        }

        public static Layout ComputeLayout(float height, PillarProfile bottom, PillarProfile center, PillarProfile top,
            float bottomHeight, float topHeight, float centerHeight, bool stretch) {
            float bottomSize = bottom is { valid: true } ? Mathf.Max(0f, bottomHeight) : 0f;
            float topSize = top is { valid: true } ? Mathf.Max(0f, topHeight) : 0f;
            float sections = bottomSize + topSize;
            float squish = sections > height && sections > Epsilon ? height / sections : 1f;
            float remaining = Mathf.Max(0f, height - sections * squish);

            int repeats = center is not { valid: true } || remaining <= Epsilon ? 0
                : stretch ? 1 : Mathf.Max(1, Mathf.RoundToInt(remaining / Mathf.Max(MinCenterHeight, centerHeight)));

            return new Layout {
                height = height, squish = squish,
                bottomSpan = bottomSize * squish, topSpan = topSize * squish,
                middleStart = bottomSize * squish, topStart = height - topSize * squish,
                repeats = repeats, tile = repeats > 0 ? remaining / repeats : 0f
            };
        }

        public Layout ProfileLayout() => ComputeLayout(height, activeBottomProfile, activeCenterProfile, activeTopProfile,
            activeBottomHeight, activeTopHeight, activeCenterHeight, activeStretchCenter);

        public static List<(List<Vector2> points, Part part, int repeat)> LayoutLines(Layout layout, PillarProfile bottom,
            PillarProfile center, PillarProfile top, int segments) {
            List<(List<Vector2>, Part, int)> lines = new();

            void Add(PillarProfile profile, Part part, int repeat) {
                if (profile is not { valid: true })
                    return;
                float span = layout.Span(part);
                if (span <= Epsilon)
                    return;

                float start = layout.Start(part) + repeat * layout.tile;
                List<Vector2> placed = new();
                foreach (Vector2 p in profile.points)
                    placed.Add(new Vector2(p.x, start + p.y * span));
                lines.Add((RoundProfile(placed, profile.radii, segments), part, repeat));
            }

            Add(bottom, Part.Bottom, 0);
            for (int r = 0; r < layout.repeats; r++)
                Add(center, Part.Middle, r);
            Add(top, Part.Top, 0);
            return lines;
        }

        public List<Vector2> StackedProfile() {
            List<Vector2> stacked = new();
            foreach ((List<Vector2> points, _, _) in LayoutLines(ProfileLayout(), activeBottomProfile, activeCenterProfile, activeTopProfile,
                         activeRoundingSegments))
                foreach (Vector2 p in points)
                    if (stacked.Count == 0 || (stacked[^1] - p).sqrMagnitude > Epsilon * Epsilon)
                        stacked.Add(p);
            return stacked;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private static Vector3 Perpendicular(Vector3 v) => new(-v.z, 0f, v.x);

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2 Left(Vector2 direction) => new(-direction.y, direction.x);


        public bool TryGetCorner(out Corner corner) {
            corner = default;
            Vector3 startDirection = Flat(cornerStart), endDirection = Flat(cornerEnd);
            if (startDirection.sqrMagnitude < Epsilon * Epsilon || endDirection.sqrMagnitude < Epsilon * Epsilon)
                return false;

            startDirection.Normalize();
            endDirection.Normalize();
            if (Vector3.Dot(startDirection, endDirection) > 1f - Epsilon)
                return false;

            float sign = cornerSide == CornerSide.Outside ? 1f : -1f;
            Vector3 startNormal = Perpendicular(startDirection) * sign, endNormal = -Perpendicular(endDirection) * sign;
            float length = activeCornerLength;

            corner = new Corner {
                center = Vector3.zero,
                start = startDirection * length,
                end = endDirection * length,
                startDirection = startDirection,
                endDirection = endDirection,
                startNormal = startNormal,
                endNormal = endNormal,
                miter = (startNormal + endNormal) / Mathf.Max(MinMiterDenominator, 1f + Vector3.Dot(startNormal, endNormal))
            };
            return true;
        }

        public Matrix4x4 ProfileFrame() => Matrix4x4.Translate(Vector3.up * baseOffset) * UnshiftedProfileFrame();

        public Matrix4x4 UnshiftedProfileFrame() {
            if (activeShape != Shape.Corner || !TryGetCorner(out Corner corner))
                return Matrix4x4.identity;

            return Matrix4x4.TRS(corner.start, Quaternion.LookRotation(Vector3.Cross(corner.startNormal, Vector3.up), Vector3.up), Vector3.one);
        }

        public bool SnapToPath(float maxDistance) => SnapTo(maxDistance, SnapPaths().ToList(), true, activeSnapToFlats);

        public bool SnapToPath(float maxDistance, Transform owner, List<Vector3> path, bool closed, bool corners) =>
            SnapTo(maxDistance, new List<(Transform, List<Vector3>, bool, float, float)> { (owner, path, closed, 0f, 0f) },
                corners, activeSnapToFlats);

        private bool SnapTo(float maxDistance, List<(Transform owner, List<Vector3> path, bool closed, float low, float high)> paths,
            bool corners, bool flats) {
            Vector3 worldCenter = transform.position;
            bool corner = activeShape == Shape.Corner;

            float best = maxDistance;
            bool found = false;
            Vector3 bestCenter = default, bestStart = default, bestEnd = default;

            foreach ((Transform owner, List<Vector3> path, bool closed, float low, float high) in paths) {
                int count = path?.Count ?? 0;
                if (!corners || count < 3)
                    continue;

                for (int i = 0; i < count; i++) {
                    if (!closed && (i == 0 || i == count - 1))
                        continue;

                    Vector3 current = path[i];
                    Vector2 previous = new(path[(i - 1 + count) % count].x, path[(i - 1 + count) % count].z);
                    Vector2 next = new(path[(i + 1) % count].x, path[(i + 1) % count].z);
                    Vector2 point = new(current.x, current.z);

                    Vector2 incoming = point - previous, outgoing = next - point;
                    if (incoming.sqrMagnitude < Epsilon * Epsilon || outgoing.sqrMagnitude < Epsilon * Epsilon)
                        continue;

                    incoming.Normalize();
                    outgoing.Normalize();
                    float cross = Cross(incoming, outgoing);
                    if (Mathf.Abs(cross) < MinCornerSine)
                        continue;

                    float side = cornerSide == CornerSide.Inside ? 1f : -1f;
                    float offset = !corner ? 0f : side > 0f ? Mathf.Max(low, high) : Mathf.Min(low, high);
                    Vector2 n0 = Left(incoming), n1 = Left(outgoing);
                    Vector2 placed = point + (n0 + n1) / Mathf.Max(MinMiterDenominator, 1f + Vector2.Dot(n0, n1)) * offset;

                    Vector3 worldCorner = owner.TransformPoint(new Vector3(placed.x, current.y, placed.y));
                    float distance = Vector3.Distance(worldCorner, worldCenter);
                    if (distance >= best)
                        continue;

                    best = distance;
                    found = true;
                    bestCenter = worldCorner;
                    bestStart = owner.TransformDirection(new Vector3(-incoming.x, 0f, -incoming.y));
                    bestEnd = owner.TransformDirection(new Vector3(outgoing.x, 0f, outgoing.y));
                }
            }

            bool flat = false;
            if (!found && flats) {
                foreach ((Transform owner, List<Vector3> path, bool closed, float low, float high) in paths) {
                    int count = path?.Count ?? 0;
                    if (count < 2)
                        continue;

                    Vector3 local = owner.InverseTransformPoint(worldCenter);
                    Vector2 query = new(local.x, local.z);
                    int segments = closed ? count : count - 1;
                    for (int i = 0; i < segments; i++) {
                        Vector3 a3 = path[i], b3 = path[(i + 1) % count];
                        Vector2 a = new(a3.x, a3.z), b = new(b3.x, b3.z);
                        float length = Vector2.Distance(a, b);
                        if (length < Epsilon)
                            continue;

                        Vector2 direction = (b - a) / length;
                        float t = Mathf.Clamp(Vector2.Dot(query - a, direction), 0f, length);
                        float side = cornerSide == CornerSide.Inside ? 1f : -1f;
                        float offset = !corner ? 0f : side > 0f ? Mathf.Max(low, high) : Mathf.Min(low, high);
                        Vector2 placed = a + direction * t + Left(direction) * offset;

                        Vector3 world = owner.TransformPoint(new Vector3(placed.x, Mathf.Lerp(a3.y, b3.y, t / length), placed.y));
                        float distance = Vector3.Distance(world, worldCenter);
                        if (distance >= best)
                            continue;

                        best = distance;
                        found = flat = true;
                        bestCenter = world;
                        bestStart = owner.TransformDirection(new Vector3(direction.x, 0f, direction.y));
                    }
                }
            }

            if (!found)
                return false;

            transform.position = bestCenter;
            if (!corner)
                return true;

            float legLength = activeCornerLength;
            Vector3 startDirection = Flat(transform.InverseTransformDirection(bestStart)).normalized;
            if (flat) {
                cornerStart = -startDirection * legLength;
                cornerEnd = startDirection * legLength;
                return true;
            }

            Vector3 endDirection = Flat(transform.InverseTransformDirection(bestEnd)).normalized;
            cornerStart = startDirection * legLength;
            cornerEnd = endDirection * legLength;
            return true;
        }

        private static IEnumerable<(Transform owner, List<Vector3> path, bool closed, float low, float high)> SnapPaths() {
            foreach (TsitskiProBuilderFloorPlan plan in UnityCompat.FindAll<TsitskiProBuilderFloorPlan>()) {
                float thickness = plan.activeThickness;
                (float outside, float inside) = plan.activeAlignment switch {
                    WallAlignment.Inside => (0f, thickness),
                    WallAlignment.Center => (-thickness * 0.5f, thickness * 0.5f),
                    _ => (-thickness, 0f)
                };

                List<Vector3> path = plan.path;
                if (plan.closed && path is { Count: >= 3 } && SignedAreaXZ(path) < 0f) {
                    path = new List<Vector3>(path);
                    path.Reverse();
                }

                yield return (plan.transform, path, plan.closed, outside, inside);
            }

            foreach (TsitskiProBuilderTrim trim in UnityCompat.FindAll<TsitskiProBuilderTrim>())
                yield return (trim.transform, trim.activePath, trim.activeClosed, 0f, 0f);

            foreach (TsitskiArray array in UnityCompat.FindAll<TsitskiArray>())
                yield return (array.transform, array.activePath, array.activeClosed, 0f, 0f);

            foreach (TsitskiProBuilderStairs stairs in UnityCompat.FindAll<TsitskiProBuilderStairs>())
                yield return (stairs.transform, stairs.path, false, 0f, 0f);
        }

        private static float SignedAreaXZ(List<Vector3> path) {
            float area = 0f;
            for (int i = 0; i < path.Count; i++) {
                Vector3 a = path[i], b = path[(i + 1) % path.Count];
                area += a.x * b.z - b.x * a.z;
            }
            return area * 0.5f;
        }

        private static float[] ArcLengths(List<Vector2> line) {
            float[] lengths = new float[line.Count];
            for (int i = 1; i < line.Count; i++)
                lengths[i] = lengths[i - 1] + Vector2.Distance(line[i - 1], line[i]);
            return lengths;
        }

        // Average radius along the profile, weighted by how much of it sits at each radius.
        private static float MeanRadius(List<Vector2> line, float[] arc) {
            float sum = 0f;
            for (int i = 1; i < line.Count; i++)
                sum += (line[i - 1].x + line[i].x) * 0.5f * (arc[i] - arc[i - 1]);
            return arc[^1] > Epsilon ? sum / arc[^1] : line[0].x;
        }

        private static float ProfileOrientation(List<Vector2> line) {
            float area = 0f;
            List<Vector2> closedLine = new(line) { new Vector2(0f, line[^1].y), new Vector2(0f, line[0].y) };
            for (int i = 0; i < closedLine.Count; i++) {
                Vector2 a = closedLine[i], b = closedLine[(i + 1) % closedLine.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area < 0f ? -1f : 1f;
        }

        private void BuildCorner(List<Vector3> positions, List<Vector2> uvs, List<Face> faces) {
            List<Vector2> line = StackedProfile();
            if (line.Count < 2 || !TryGetCorner(out Corner corner))
                return;

            float orientation = ProfileOrientation(line);
            float smoothing = activeSmoothingAngle;
            Vector3 up = Vector3.up;

            Vector3 travelIn = -corner.startDirection, travelOut = corner.endDirection;
            float turn = travelIn.x * travelOut.z - travelIn.z * travelOut.x;
            bool convex = cornerSide == CornerSide.Outside ? turn > Epsilon : turn < -Epsilon;
            int bevel = convex ? activeRoundBevel : 0;
            List<Vector3> origins = new() { corner.start };
            List<Vector3> offsets = new() { corner.startNormal };
            if (bevel > 0) {
                for (int j = 0; j <= bevel; j++) {
                    origins.Add(corner.center);
                    offsets.Add(Vector3.Slerp(corner.startNormal, corner.endNormal, (float)j / bevel));
                }
            }
            else {
                origins.Add(corner.center);
                offsets.Add(corner.miter);
            }
            origins.Add(corner.end);
            offsets.Add(corner.endNormal);

            int last = origins.Count - 1;
            Vector3 Point(int k, Vector2 p) => origins[k] + offsets[k] * p.x + up * p.y;

            Vector3 SegmentNormal(int k) {
                if (k == 0)
                    return corner.startNormal;
                if (k == last - 1)
                    return corner.endNormal;
                return (offsets[k] + offsets[k + 1]).normalized;
            }

            float[] arc = ArcLengths(line);
            float uSign = Vector3.Dot(-corner.startDirection, Vector3.Cross(up, -corner.startNormal * orientation)) < 0f ? -1f : 1f;

            Vector2 Uv(int i, int k) {
                float u = 0f;
                for (int j = 0; j < k; j++)
                    u += Vector3.Distance(Point(j, line[i]), Point(j + 1, line[i]));
                return new Vector2(u * uSign, arc[i]);
            }

            int run = 0;
            for (int i = 0; i < line.Count - 1; i++) {
                Vector2 a = line[i], b = line[i + 1];
                if (i > 0 && Vector2.Angle(a - line[i - 1], b - a) >= smoothing)
                    run++;

                for (int k = 0; k < last; k++) {
                    Vector3 facing = (SegmentNormal(k) * (b.y - a.y) - up * (b.x - a.x)) * orientation;
                    int group = bevel > 0 ? 1 + run % 2 : 1 + (k == 0 ? 0 : 2) + run % 2;
                    AddFace(positions, uvs, faces, facing, group, new[] { Point(k, a), Point(k + 1, a), Point(k + 1, b), Point(k, b) },
                        new[] { Uv(i, k), Uv(i, k + 1), Uv(i + 1, k + 1), Uv(i + 1, k) });
                }

                Vector2 a0 = new(0f, a.y), b0 = new(0f, b.y);
                AddFace(positions, uvs, faces, corner.startDirection, 0, new[] { Point(0, a), Point(0, b), Point(0, b0), Point(0, a0) });
                AddFace(positions, uvs, faces, corner.endDirection, 0, new[] { Point(last, a), Point(last, b), Point(last, b0), Point(last, a0) });
            }

            void Cap(Vector2 p, Vector3 facing) {
                if (p.x <= Epsilon)
                    return;

                Vector2 wall = new(0f, p.y);
                for (int k = 0; k < last; k++)
                    AddFace(positions, uvs, faces, facing, 0, new[] { Point(k, wall), Point(k + 1, wall), Point(k + 1, p), Point(k, p) });
            }

            Cap(line[0], Vector3.down * orientation);
            Cap(line[^1], Vector3.up * orientation);
        }

        internal void BuildGeometry(List<Vector3> positions, List<Vector2> uvs, List<Face> faces) => Build(positions, uvs, faces);

        private void Build(List<Vector3> positions, List<Vector2> uvs, List<Face> faces) {
            int start = positions.Count;
            BuildShape(positions, uvs, faces);

            float offset = baseOffset;
            if (offset != 0f)
                for (int i = start; i < positions.Count; i++)
                    positions[i] += Vector3.up * offset;
        }

        private void BuildShape(List<Vector3> positions, List<Vector2> uvs, List<Face> faces) {
            if (activeShape == Shape.Corner) {
                BuildCorner(positions, uvs, faces);
                return;
            }

            List<Vector2> line = StackedProfile();
            if (line.Count < 2)
                return;

            int sideCount = activeSides;
            float circumradius = 1f / Mathf.Cos(Mathf.PI / sideCount);
            float smoothing = activeSmoothingAngle;
            bool smoothSides = 360f / sideCount < smoothing;
            float orientation = ProfileOrientation(line);

            Vector3 Corner(Vector2 p, int k) {
                float angle = (k + 0.5f) * Mathf.PI * 2f / sideCount;
                return new Vector3(Mathf.Cos(angle) * p.x * circumradius, p.y, Mathf.Sin(angle) * p.x * circumradius);
            }

            float[] arc = ArcLengths(line);
            float sideWidth = 2f * Mathf.Tan(Mathf.PI / sideCount) * orientation;
            // Every ring shares one radius: per-ring U would shear the quads wherever the profile narrows.
            float uvRadius = MeanRadius(line, arc);
            Vector2 Uv(int i, int k) => new(k * uvRadius * sideWidth, arc[i]);

            int run = 0;
            for (int i = 0; i < line.Count - 1; i++) {
                Vector2 a = line[i], b = line[i + 1];
                if (i > 0 && Vector2.Angle(a - line[i - 1], b - a) >= smoothing)
                    run++;

                int group = smoothSides ? 1 + run % 2 : 0;
                for (int k = 0; k < sideCount; k++) {
                    float middleAngle = (k + 1f) * Mathf.PI * 2f / sideCount;
                    Vector3 radial = new(Mathf.Cos(middleAngle), 0f, Mathf.Sin(middleAngle));
                    Vector3 facing = (radial * (b.y - a.y) - Vector3.up * (b.x - a.x)) * orientation;
                    AddFace(positions, uvs, faces, facing, group, new[] { Corner(a, k), Corner(a, k + 1), Corner(b, k + 1), Corner(b, k) },
                        new[] { Uv(i, k), Uv(i, k + 1), Uv(i + 1, k + 1), Uv(i + 1, k) });
                }
            }

            if (line[0].x > Epsilon)
                AddFace(positions, uvs, faces, Vector3.down * orientation, 0, Ring(line[0]));
            if (line[^1].x > Epsilon)
                AddFace(positions, uvs, faces, Vector3.up * orientation, 0, Ring(line[^1]));

            Vector3[] Ring(Vector2 p) {
                Vector3[] ring = new Vector3[sideCount];
                for (int k = 0; k < sideCount; k++)
                    ring[k] = Corner(p, k);
                return ring;
            }
        }

        private static void AddFace(List<Vector3> positions, List<Vector2> uvs, List<Face> faces, Vector3 facing, int smoothingGroup,
            Vector3[] corners, Vector2[] cornerUvs = null) {
            List<int> unique = new();
            for (int i = 0; i < corners.Length; i++)
                if (unique.Count == 0 || (corners[unique[^1]] - corners[i]).sqrMagnitude > Epsilon * Epsilon)
                    unique.Add(i);
            if (unique.Count > 1 && (corners[unique[0]] - corners[unique[^1]]).sqrMagnitude <= Epsilon * Epsilon)
                unique.RemoveAt(unique.Count - 1);
            if (unique.Count < 3)
                return;

            Vector3 normal = Vector3.zero;
            Vector3 origin = corners[unique[0]];
            for (int i = 2; i < unique.Count; i++)
                normal += Vector3.Cross(corners[unique[i - 1]] - origin, corners[unique[i]] - origin);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            if (Vector3.Dot(normal, facing) < 0f)
                unique.Reverse(1, unique.Count - 1);

            int start = positions.Count;
            foreach (int i in unique) {
                positions.Add(corners[i]);
                uvs.Add(cornerUvs?[i] ?? Vector2.zero);
            }

            int[] indices = new int[(unique.Count - 2) * 3];
            for (int i = 2; i < unique.Count; i++) {
                indices[(i - 2) * 3] = start;
                indices[(i - 2) * 3 + 1] = start + i - 1;
                indices[(i - 2) * 3 + 2] = start + i;
            }

            faces.Add(new Face(indices) { smoothingGroup = smoothingGroup, manualUV = cornerUvs != null });
        }
    }
}
