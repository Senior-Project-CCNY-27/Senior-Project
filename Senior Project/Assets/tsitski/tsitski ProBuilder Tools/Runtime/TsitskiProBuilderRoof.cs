using System;
using System.Collections.Generic;
using System.Linq;
using Tsitski.Core;
using UnityEngine;
using UnityEngine.ProBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder Roof")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class TsitskiProBuilderRoof : MonoBehaviour, IGenerated, IForcedMaterials, IInputHashed, IPivotShiftable {
        bool IPivotShiftable.CanShiftPivot(out string reason) {
            reason = "its paths are stored in its stats asset and shared with other objects.";
            return !pathsInStats;
        }

        void IPivotShiftable.ShiftLocalPoints(Vector3 offset) {
            foreach (RoofPath roofPath in paths.Where(p => p?.path != null))
                roofPath.path = PivotShift.Shifted(roofPath.path, offset);
        }

        void IPivotShiftable.ApplyPivotShift() => Rebuild();

        private const int NoSmoothing = 0;
        private const float MinTriangleCross = 2e-5f;

        [Tooltip(Tooltips.RoofStats)] public TsitskiProBuilderRoofStats stats;

        [Header("Material")]
        [Tooltip(Tooltips.GeneratorMaterial)] public Material material;

        [Header("Profile")]
        [Tooltip(Tooltips.RoofProfile)] public List<Vector2> profile = DefaultProfile();
        [Tooltip(Tooltips.Radii)] public List<float> profileRadii = new();
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.RoofThickness)] [Min(0.01f)] public float thickness = 0.25f;
        [Tooltip(Tooltips.SmoothingAngle)] [Range(0f, 180f)] public float smoothingAngle = 30f;

        [Header("Fill & Gables")]
        [Tooltip(Tooltips.FillRoof)] public bool fillRoof;
        [Tooltip(Tooltips.GenerateGableWalls)] public bool generateGableWalls;
        [Tooltip(Tooltips.GableWallIndent)] [Min(0f)] public float gableWallIndent = 0.25f;
        [Tooltip(Tooltips.GableWallThickness)] [Min(0.01f)] public float gableWallThickness = 0.25f;

        [Header("Paths")]
        [Tooltip(Tooltips.RoofPaths)] public List<RoofPath> paths = DefaultPaths();

        [SerializeField] [HideInInspector] internal MonoBehaviour generator;
        MonoBehaviour IGenerated.generator { get => generator; set => generator = value; }
        [SerializeField] [HideInInspector] internal List<RoofOutline> generatedOutlines = new();

        public bool isGenerated => generator && generatedOutlines is { Count: > 0 };

        [NonSerialized] public List<List<Vector2>> footprint = new();
        [NonSerialized] public int failedRegions;
        [NonSerialized] public List<int> separatedPaths = new();

        public static readonly HashSet<TsitskiProBuilderRoof> All = new();
        public static event Action<ProBuilderMesh> Rebuilt;

        public Material activeMaterial => stats ? stats.material : material;
        Material[] IForcedMaterials.forcedMaterials => new[] { activeMaterial };

        public static List<Vector2> DefaultProfile() => new() { Vector2.zero, Vector2.one };

        public static List<RoofPath> DefaultPaths() => new() {
            new RoofPath { width = 4f, height = 1.5f, path = new List<Vector3> { Vector3.zero, new(0f, 0f, 8f) } }
        };

        public bool pathsInStats => stats && stats.savePaths;
        public UnityEngine.Object profileOwner => stats ? stats : this;
        public UnityEngine.Object pathsOwner => pathsInStats ? stats : this;

        public List<Vector2> activeProfile {
            get => stats ? stats.profile : profile;
            set {
                if (stats)
                    stats.profile = value;
                else
                    profile = value;
            }
        }

        public List<float> activeProfileRadii {
            get => stats ? stats.profileRadii : profileRadii;
            set {
                if (stats)
                    stats.profileRadii = value;
                else
                    profileRadii = value;
            }
        }

        public int activeRoundingSegments => Mathf.Max(1, stats ? stats.roundingSegments : roundingSegments);
        public float activeThickness => Mathf.Max(0.01f, stats ? stats.thickness : thickness);
        public float activeSmoothingAngle => stats ? stats.smoothingAngle : smoothingAngle;
        public bool activeFillRoof => stats ? stats.fillRoof : fillRoof;
        public bool activeGenerateGableWalls => stats ? stats.generateGableWalls : generateGableWalls;
        public float activeGableWallIndent => Mathf.Max(0f, stats ? stats.gableWallIndent : gableWallIndent);
        public float activeGableWallThickness => Mathf.Max(0.01f, stats ? stats.gableWallThickness : gableWallThickness);

        public List<RoofPath> activePaths {
            get => (pathsInStats ? stats.paths : paths) ?? new List<RoofPath>();
            set {
                if (pathsInStats)
                    stats.paths = value;
                else
                    paths = value;
            }
        }

        public float baseHeight {
            get {
                List<Vector3> points = activePaths.Where(p => p?.path != null).SelectMany(p => p.path).ToList();
                return points.Count > 0 ? points.Average(p => p.y) : 0f;
            }
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
            TsitskiProBuilderRoofStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() {
            All.Remove(this);
            TsitskiProBuilderRoofStats.Changed -= OnStatsChanged;
        }

        private void OnValidate() => QueueRebuild();

        private void OnStatsChanged(TsitskiProBuilderRoofStats changed) {
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
            TsitskiSettings.AutoName(this, stats, "ProBuilder Roof");

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            KeptFaceMaterials kept = KeptFaceMaterials.Capture(mesh);

            List<Vector3> positions = new();
            List<Face> faces = new();
            Build(positions, faces);
            SmoothingAngle.Apply(positions, faces, activeSmoothingAngle);

            if (faces.Count == 0)
                mesh.Clear();
            else {
                mesh.RebuildWithPositionsAndFaces(positions, faces);
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

        public List<Vector3> Centerline(RoofPath roofPath, out bool loop) =>
            PathUtility.Rounded(roofPath.path, roofPath.pathRadii, roofPath.closed, activeRoundingSegments, out loop);

        public List<Vector2> ProfileCurve() => ProfileCurve(activeProfile, activeProfileRadii, activeRoundingSegments);

        public static List<Vector2> ProfileCurve(List<Vector2> source, List<float> radii, int segments) {
            List<Vector2> rounded = source is { Count: >= 2 } ? PointRounding.Round(source, radii, false, segments) : DefaultProfile();
            return new RoofCurve(rounded).points.Select(p => p.ToVector2()).ToList();
        }

        private void Build(List<Vector3> positions, List<Face> faces) {
            List<RoofWing> wings = new();
            List<int> pathIndices = new();
            List<RoofPath> roofPaths = activePaths;
            for (int index = 0; index < roofPaths.Count; index++) {
                RoofPath roofPath = roofPaths[index];
                if (roofPath?.path is not { Count: >= 2 })
                    continue;

                List<Vector3> line = Centerline(roofPath, out bool loop);
                wings.Add(new RoofWing {
                    centerline = line.Select(p => new D2(p.x, p.z)).ToList(), closed = loop,
                    gableStart = roofPath.gableStart, gableEnd = roofPath.gableEnd,
                    halfWidth = Mathf.Max(0.01f, roofPath.width) * 0.5, height = Mathf.Max(0.01f, roofPath.height)
                });
                pathIndices.Add(index);
            }

            RoofCurve curve = new(PointRounding.Round(activeProfile is { Count: >= 2 } ? activeProfile : DefaultProfile(), activeProfileRadii, false,
                activeRoundingSegments));

            if (isGenerated) {
                BuildGenerated(curve, positions, faces);
                return;
            }

            List<FootprintLoop> loops = RoofGeometry.Footprint(wings);
            footprint = loops.Select(l => l.points.Select(p => p.ToVector2()).ToList()).ToList();
            failedRegions = 0;
            separatedPaths.Clear();

            List<RoofPart> parts = new();
            foreach (List<FootprintLoop> region in RoofGeometry.Regions(loops)) {
                List<int> group = region.SelectMany(l => l.wings).Distinct().OrderBy(i => i).ToList();
                if (TryBuild(group, wings, curve, parts))
                    continue;

                int culprit = -1;
                if (group.Count > 1)
                    foreach (int id in group) {
                        List<RoofPart> attempt = new();
                        if (!TryBuild(group.Where(other => other != id).ToList(), wings, curve, attempt) ||
                            !TryBuild(new List<int> { id }, wings, curve, attempt))
                            continue;
                        parts.AddRange(attempt);
                        culprit = id;
                        break;
                    }

                if (culprit >= 0) {
                    separatedPaths.Add(pathIndices[culprit]);
                    continue;
                }

                foreach (int id in group)
                    if (!TryBuild(new List<int> { id }, wings, curve, parts))
                        failedRegions++;
                if (group.Count > 1)
                    separatedPaths.AddRange(group.Select(id => pathIndices[id]));
            }

            AddParts(parts, curve, baseHeight, positions, faces);
        }

        private void BuildGenerated(RoofCurve curve, List<Vector3> positions, List<Face> faces) {
            RoofPath slope = activePaths.FirstOrDefault(p => p != null) ?? DefaultPaths()[0];
            List<RoofWing> wings = new() {
                new RoofWing { centerline = new List<D2>(), halfWidth = Mathf.Max(0.01f, slope.width) * 0.5, height = Mathf.Max(0.01f, slope.height) }
            };

            List<FootprintLoop> loops = new();
            foreach (RoofOutline outline in generatedOutlines) {
                if (outline?.points is not { Count: >= 3 })
                    continue;

                List<D2> points = outline.points.Select(D2.From).ToList();
                bool hole = generatedOutlines.Count(other => other != outline && other?.points is { Count: >= 3 } && Contains(other.points, outline.points[0])) % 2 == 1;
                if (RoofGeometry.SignedArea(points) > 0 == hole)
                    points.Reverse();
                loops.Add(new FootprintLoop {
                    points = points, wings = Enumerable.Repeat(0, points.Count).ToList(), gables = Enumerable.Repeat(false, points.Count).ToList()
                });
            }

            footprint = loops.Select(l => l.points.Select(p => p.ToVector2()).ToList()).ToList();
            failedRegions = 0;
            separatedPaths.Clear();

            List<RoofPart> parts = new();
            foreach (List<FootprintLoop> region in RoofGeometry.Regions(loops)) {
                List<RoofFace> roofFaces = RoofGeometry.Skeleton(region, wings, curve, out _, out bool complete);
                if (complete && Valid(region, roofFaces, wings, curve))
                    parts.Add(new RoofPart { wings = wings, region = region, faces = roofFaces });
                else
                    failedRegions++;
            }

            AddParts(parts, curve, 0f, positions, faces);
        }

        private static bool Contains(List<Vector2> loop, Vector2 point) {
            bool inside = false;
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
                if (loop[i].y > point.y != loop[j].y > point.y &&
                    point.x < (loop[j].x - loop[i].x) * (point.y - loop[i].y) / (loop[j].y - loop[i].y) + loop[i].x)
                    inside = !inside;
            return inside;
        }

        private void AddParts(List<RoofPart> parts, RoofCurve curve, float y, List<Vector3> positions, List<Face> faces) {
            float shell = activeThickness;
            bool solid = activeFillRoof;
            bool walls = activeGenerateGableWalls && !solid;
            foreach (RoofPart part in parts) {
                foreach (RoofFace face in part.faces) {
                    if (face.gable) {
                        AddGable(face, y, shell, solid, positions, faces);
                        if (walls)
                            AddGableWall(face, y, shell, activeGableWallIndent, activeGableWallThickness, positions, faces);
                    } else {
                        AddSlope(face, part.wings[face.wing], curve, y, shell, solid, positions, faces);
                    }
                }

                foreach (FootprintLoop loop in part.region)
                    for (int i = 0; i < loop.points.Count; i++)
                        if (!loop.gables[i])
                            AddFascia(loop.points[i], loop.points[(i + 1) % loop.points.Count], y, shell, positions, faces);
            }

            RemoveUnusedPositions(positions, faces);
        }

        private static void RemoveUnusedPositions(List<Vector3> positions, List<Face> faces) {
            int[] map = new int[positions.Count];
            Array.Fill(map, -1);
            List<Vector3> kept = new();
            for (int f = 0; f < faces.Count; f++) {
                int[] indexes = faces[f].indexes.ToArray();
                for (int i = 0; i < indexes.Length; i++) {
                    if (map[indexes[i]] < 0) {
                        map[indexes[i]] = kept.Count;
                        kept.Add(positions[indexes[i]]);
                    }

                    indexes[i] = map[indexes[i]];
                }

                faces[f] = new Face(indexes) { smoothingGroup = faces[f].smoothingGroup };
            }

            positions.Clear();
            positions.AddRange(kept);
        }

        private sealed class RoofPart {
            public List<RoofWing> wings;
            public List<FootprintLoop> region;
            public List<RoofFace> faces;
        }

        private static bool TryBuild(List<int> group, List<RoofWing> all, RoofCurve curve, List<RoofPart> parts) {
            if (group.Count == 0)
                return false;

            List<RoofWing> wings = group.Select(i => all[i]).ToList();
            List<RoofPart> built = new();
            foreach (List<FootprintLoop> region in RoofGeometry.Regions(RoofGeometry.Footprint(wings))) {
                List<RoofFace> roofFaces = RoofGeometry.Skeleton(region, wings, curve, out _, out bool complete);
                if (!complete || !Valid(region, roofFaces, wings, curve))
                    return false;
                built.Add(new RoofPart { wings = wings, region = region, faces = roofFaces });
            }

            parts.AddRange(built);
            return true;
        }

        private static double TriangulatedArea(RoofFace face) {
            List<int> triangles = RoofGeometry.Triangulate(face.polygon);
            double area = 0;
            for (int t = 0; t < triangles.Count; t += 3)
                area += System.Math.Abs(RoofGeometry.SignedArea(new[] { face.polygon[triangles[t]], face.polygon[triangles[t + 1]], face.polygon[triangles[t + 2]] }));
            return area;
        }

        private static bool Valid(List<FootprintLoop> region, List<RoofFace> roofFaces, List<RoofWing> wings, RoofCurve curve) {
            double area = region.Sum(l => RoofGeometry.SignedArea(l.points));
            double covered = roofFaces.Where(f => !f.gable).Sum(TriangulatedArea);
            if (System.Math.Abs(covered - area) > 1e-3 * System.Math.Max(1, area))
                return false;

            Dictionary<(long, long), double> heights = new();
            foreach (RoofFace face in roofFaces.Where(f => !f.gable)) {
                RoofWing wing = wings[face.wing];
                foreach (D2 p in face.polygon) {
                    double height = wing.height * curve.Height(face.Inset(p) / wing.halfWidth);
                    if (height < -1e-3)
                        return false;

                    (long, long) key = ((long)System.Math.Round(p.x * 1000), (long)System.Math.Round(p.y * 1000));
                    if (heights.TryGetValue(key, out double other) && System.Math.Abs(other - height) > 0.01)
                        return false;
                    heights[key] = height;
                }
            }

            return true;
        }

        private static void AddSlope(RoofFace face, RoofWing wing, RoofCurve curve, float y, float shell, bool solid,
            List<Vector3> positions, List<Face> faces) {
            List<int> triangles = RoofGeometry.Triangulate(face.polygon);
            double[] breaks = curve.inner.Select(p => p.x * wing.halfWidth).ToArray();

            for (int band = 0; band <= breaks.Length; band++) {
                double low = band == 0 ? double.NegativeInfinity : breaks[band - 1];
                double high = band == breaks.Length ? double.PositiveInfinity : breaks[band];
                List<int> top = new(), bottom = new();

                for (int t = 0; t < triangles.Count; t += 3) {
                    List<D2> piece = new() { face.polygon[triangles[t]], face.polygon[triangles[t + 1]], face.polygon[triangles[t + 2]] };
                    piece = RoofGeometry.Simplify(Clip(Clip(piece, face, low, true), face, high, false));
                    if (piece.Count < 3)
                        continue;

                    int topStart = positions.Count;
                    foreach (D2 p in piece)
                        positions.Add(new Vector3((float)p.x, y + (float)(wing.height * curve.Height(face.Inset(p) / wing.halfWidth)), (float)p.y));
                    int bottomStart = positions.Count;
                    for (int i = 0; i < piece.Count; i++)
                        positions.Add(positions[topStart + i] + Vector3.down * shell);

                    for (int i = 1; i + 1 < piece.Count; i++) {
                        AddTriangle(positions, top, topStart, topStart + i, topStart + i + 1, Vector3.up);
                        if (!solid)
                            AddTriangle(positions, bottom, bottomStart, bottomStart + i, bottomStart + i + 1, Vector3.down);
                    }
                }

                if (top.Count > 0)
                    faces.Add(new Face(top) { smoothingGroup = NoSmoothing });
                if (bottom.Count > 0)
                    faces.Add(new Face(bottom) { smoothingGroup = NoSmoothing });
            }

            if (!solid)
                return;

            int start = positions.Count;
            positions.AddRange(face.polygon.Select(p => new Vector3((float)p.x, y - shell, (float)p.y)));
            List<int> underside = new();
            for (int t = 0; t < triangles.Count; t += 3)
                AddTriangle(positions, underside, start + triangles[t], start + triangles[t + 1], start + triangles[t + 2], Vector3.down);
            if (underside.Count > 0)
                faces.Add(new Face(underside) { smoothingGroup = NoSmoothing });
        }

        private static List<D2> GableTop(RoofFace face) {
            D2 along = (face.edgeEnd - face.edgeStart).Normalized;
            return face.polygon.Select((p, i) => new D2(along.Dot(p - face.edgeStart), face.heights[i])).ToList();
        }

        private static List<D2> DownToEaves(List<D2> top, double drop, double floor) {
            List<D2> outline = top.Select(p => new D2(p.x, p.y - drop)).ToList();
            outline.Add(new D2(top[top.Count - 1].x, floor));
            outline.Add(new D2(top[0].x, floor));
            return RoofGeometry.Simplify(outline);
        }

        private static void AddGable(RoofFace face, float y, float shell, bool solid, List<Vector3> positions, List<Face> faces) {
            if (face.polygon.Count < 2)
                return;

            List<D2> top = GableTop(face);
            List<D2> band;
            if (solid) {
                band = DownToEaves(top, 0, -shell);
            } else {
                band = new List<D2>(top);
                for (int i = top.Count - 1; i >= 0; i--)
                    band.Add(new D2(top[i].x, top[i].y - shell));
                band = RoofGeometry.Simplify(band);
            }

            Vector3 outward = -new Vector3((float)face.normal.x, 0f, (float)face.normal.y);
            AddGableOutline(face, band, y, 0f, outward, positions, faces);
        }

        private static void AddGableWall(RoofFace face, float y, float shell, float indent, float thickness,
            List<Vector3> positions, List<Face> faces) {
            if (face.polygon.Count < 2)
                return;

            List<D2> outline = DownToEaves(GableTop(face), shell, -shell);
            if (outline.Count < 3 || System.Math.Abs(RoofGeometry.SignedArea(outline)) < 1e-6)
                return;

            Vector3 inward = new((float)face.normal.x, 0f, (float)face.normal.y);
            int front = AddGableOutline(face, outline, y, indent, -inward, positions, faces);
            int back = AddGableOutline(face, outline, y, indent + thickness, inward, positions, faces);
            if (front < 0 || back < 0)
                return;

            D2 along = (face.edgeEnd - face.edgeStart).Normalized;
            Vector3 alongWorld = new((float)along.x, 0f, (float)along.y);
            double winding = System.Math.Sign(RoofGeometry.SignedArea(outline));
            List<int> sides = new();
            for (int i = 0; i < outline.Count; i++) {
                int j = (i + 1) % outline.Count;
                D2 edge = outline[j] - outline[i];
                Vector3 facing = alongWorld * (float)(edge.y * winding) - Vector3.up * (float)(edge.x * winding);
                int start = positions.Count;
                positions.Add(positions[front + i]);
                positions.Add(positions[front + j]);
                positions.Add(positions[back + j]);
                positions.Add(positions[back + i]);
                AddTriangle(positions, sides, start, start + 1, start + 2, facing);
                AddTriangle(positions, sides, start, start + 2, start + 3, facing);
            }

            if (sides.Count > 0)
                faces.Add(new Face(sides) { smoothingGroup = NoSmoothing });
        }

        private static int AddGableOutline(RoofFace face, List<D2> outline, float y, float offset, Vector3 facing,
            List<Vector3> positions, List<Face> faces) {
            if (outline.Count < 3)
                return -1;

            D2 along = (face.edgeEnd - face.edgeStart).Normalized;
            Vector3 alongWorld = new((float)along.x, 0f, (float)along.y);
            Vector3 inward = new((float)face.normal.x, 0f, (float)face.normal.y);
            Vector3 origin = new Vector3((float)face.edgeStart.x, y, (float)face.edgeStart.y) + inward * offset;

            int start = positions.Count;
            positions.AddRange(outline.Select(p => origin + alongWorld * (float)p.x + Vector3.up * (float)p.y));

            List<int> triangles = RoofGeometry.Triangulate(outline);
            List<int> indices = new();
            for (int t = 0; t < triangles.Count; t += 3)
                AddTriangle(positions, indices, start + triangles[t], start + triangles[t + 1], start + triangles[t + 2], facing);
            if (indices.Count > 0)
                faces.Add(new Face(indices) { smoothingGroup = NoSmoothing });
            return start;
        }

        private static List<D2> Clip(List<D2> polygon, RoofFace face, double limit, bool keepAbove) {
            if (double.IsInfinity(limit))
                return polygon;

            List<D2> result = new();
            for (int i = 0; i < polygon.Count; i++) {
                D2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                double da = face.Inset(a) - limit, db = face.Inset(b) - limit;
                if (!keepAbove) {
                    da = -da;
                    db = -db;
                }

                if (da >= 0)
                    result.Add(a);
                if (da >= 0 != db >= 0)
                    result.Add(a + (b - a) * (da / (da - db)));
            }

            return result;
        }

        private static void AddFascia(D2 a, D2 b, float y, float shell, List<Vector3> positions, List<Face> faces) {
            D2 outward = (b - a).Normalized.Left * -1;
            int start = positions.Count;
            positions.Add(new Vector3((float)a.x, y, (float)a.y));
            positions.Add(new Vector3((float)b.x, y, (float)b.y));
            positions.Add(new Vector3((float)b.x, y - shell, (float)b.y));
            positions.Add(new Vector3((float)a.x, y - shell, (float)a.y));

            Vector3 facing = new((float)outward.x, 0f, (float)outward.y);
            List<int> indices = new();
            AddTriangle(positions, indices, start, start + 1, start + 2, facing);
            AddTriangle(positions, indices, start, start + 2, start + 3, facing);
            if (indices.Count > 0)
                faces.Add(new Face(indices) { smoothingGroup = NoSmoothing });
        }

        private static void AddTriangle(List<Vector3> positions, List<int> indices, int a, int b, int c, Vector3 facing) {
            Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            if (normal.magnitude < MinTriangleCross)
                return;

            if (Vector3.Dot(normal, facing) < 0f)
                (b, c) = (c, b);
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }
    }
}
