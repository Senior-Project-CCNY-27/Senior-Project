using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Tsitski.Core;
using Tsitski.ProBuilderTools.Csg;
using ProBuilderMesh = UnityEngine.ProBuilder.ProBuilderMesh;
using ProBuilderFace = UnityEngine.ProBuilder.Face;
using SharedVertex = UnityEngine.ProBuilder.SharedVertex;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    public struct TunnelCap {
        public bool valid;
        public Vector3 normal;
        public Vector3 point;
    }

    [AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder Cut Target")]
    [ExecuteAlways]
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public class TsitskiProBuilderCutTarget : MonoBehaviour {
        [SerializeField] private List<TsitskiProBuilderCutter> cutters = new();
        [SerializeField] private MeshFilter resultFilter;
        [SerializeField] [HideInInspector] private List<Collider> disabledColliders = new();
        [SerializeField] [HideInInspector] private int buildHash;
        [SerializeField] [HideInInspector] private int buildHashVersion;
        [SerializeField] [HideInInspector] private List<Material> builtMaterials = new();
        [SerializeField] [HideInInspector] private List<Material> builtSourceMaterials = new();

        private const int CurrentBuildHashVersion = 9;

        private const float TouchTolerance = 0.001f;
        private const float SolidProbeDistance = 0.005f;

        private bool _editing;

        private static readonly Dictionary<Mesh, TsitskiProBuilderCutTarget> MeshOwners = new();
        private static readonly Dictionary<Mesh, TsitskiProBuilderCutter> CutterFaceOwners = new();
        private static readonly List<Material> MaterialBuffer = new();

        public static event System.Action<ProBuilderMesh> ResultRebuilt;

        public IReadOnlyList<TsitskiProBuilderCutter> appliedCutters => cutters;
        public bool lastRebuildSkipped { get; private set; }
        public Transform resultTransform => resultFilter ? resultFilter.transform : null;

        private const float SmoothNormalCos = 0.9998f;

        public static bool IsResult(Component mesh) =>
            mesh && mesh.transform.parent && mesh.transform.parent.TryGetComponent(out TsitskiProBuilderCutTarget target) &&
            target.resultFilter && target.resultFilter.gameObject == mesh.gameObject;

        private void OnEnable() {
            if (resultFilter)
                LockResult();

            RepairCollider();
            ApplyVisibility();
        }

        private void LockResult() {
            GameObject resultObject = resultFilter.gameObject;
            resultObject.hideFlags = HideFlags.None;
            foreach (Component component in resultObject.GetComponents<Component>())
                if (component)
                    component.hideFlags = component is Transform or MeshFilter or ProBuilderMesh or MeshCollider
                        ? HideFlags.NotEditable
                        : HideFlags.None;
        }

        public bool SyncMaterials() {
            if (!resultFilter || !TryGetComponent(out MeshRenderer sourceRenderer) ||
                !resultFilter.TryGetComponent(out MeshRenderer resultRenderer))
                return false;

            if (builtSourceMaterials.Count == 0) {
                sourceRenderer.GetSharedMaterials(builtSourceMaterials);
                resultRenderer.GetSharedMaterials(builtMaterials);
                return false;
            }

            PushResultMaterials(sourceRenderer, resultRenderer);

            sourceRenderer.GetSharedMaterials(MaterialBuffer);
            return !MaterialBuffer.SequenceEqual(builtSourceMaterials);
        }

        private void PushResultMaterials(MeshRenderer sourceRenderer, MeshRenderer resultRenderer) {
            resultRenderer.GetSharedMaterials(MaterialBuffer);
            int count = Mathf.Min(MaterialBuffer.Count, builtMaterials.Count);
            Material[] source = sourceRenderer.sharedMaterials;
            Material[] pushed = (Material[])source.Clone();
            bool push = false, restore = false;

            for (int i = 0; i < count; i++) {
                Material previous = builtMaterials[i];
                Material replacement = MaterialBuffer[i];
                if (previous == replacement)
                    continue;

                bool found = false;
                for (int s = 0; s < source.Length; s++)
                    if (source[s] == previous) {
                        pushed[s] = replacement;
                        found = true;
                    }

                if (found) {
                    builtMaterials[i] = replacement;
                    push = true;
                }
                else {
                    MaterialBuffer[i] = previous;
                    restore = true;
                }
            }

            if (restore) {
                resultRenderer.sharedMaterials = MaterialBuffer.ToArray();
#if UNITY_EDITOR
                EditorUtility.SetDirty(resultRenderer);
#endif
            }

            if (!push)
                return;

#if UNITY_EDITOR
            Undo.RecordObject(sourceRenderer, "Change Cut Material");
#endif
            sourceRenderer.sharedMaterials = pushed;
        }

        public bool RepairCollider() {
            if (!resultFilter || !resultFilter.TryGetComponent(out MeshCollider resultCollider))
                return false;

            Mesh mesh = resultFilter.sharedMesh;
            if (!mesh || mesh.vertexCount == 0 || resultCollider.sharedMesh == mesh)
                return false;

            resultCollider.sharedMesh = mesh;
            return true;
        }

        public void SetEditing(bool editing) {
            if (_editing == editing)
                return;

            _editing = editing;
            ApplyVisibility();
        }

        public bool Rebuild(List<TsitskiProBuilderCutter> newCutters, ICollection<TsitskiProBuilderCutter> insideCutters,
            IReadOnlyDictionary<TsitskiProBuilderCutter, TunnelCap[]> tunnels, out bool needsLightmapUvs,
            bool trustExistingResult = false, bool force = false) {
            needsLightmapUvs = false;
            lastRebuildSkipped = false;

            MeshFilter sourceFilter = GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
            Mesh source = sourceFilter ? sourceFilter.sharedMesh : null;
            if (!source || !sourceRenderer) {
                cutters = new List<TsitskiProBuilderCutter>();
                return false;
            }

            Material[] materials = sourceRenderer.sharedMaterials;
            builtSourceMaterials = materials.ToList();

            int hash = BuildHash(source, materials, newCutters, insideCutters, tunnels);
            bool hashCurrent = hash == buildHash && buildHashVersion == CurrentBuildHashVersion;
            bool hashUnknown = buildHash == 0 || buildHashVersion != CurrentBuildHashVersion;

            if (!force && (hashCurrent || trustExistingResult && hashUnknown) && ResultIsCurrent()) {
                buildHash = hash;
                buildHashVersion = CurrentBuildHashVersion;
                lastRebuildSkipped = true;
                return true;
            }

            Mesh debugResult = resultFilter ? resultFilter.sharedMesh : null;
            string debugOwner = "none";
            if (debugResult && MeshOwners.TryGetValue(debugResult, out TsitskiProBuilderCutTarget debugOwnerTarget))
                debugOwner = debugOwnerTarget ? debugOwnerTarget.name : "destroyed";

            cutters = new List<TsitskiProBuilderCutter>();
            List<Polygon> polygons = SourcePolygons(source, materials, SourceSmoothingGroups(source), out Vertex template);

            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            foreach (TsitskiProBuilderCutter cutter in newCutters) {
                if (polygons.Count == 0)
                    break;

                Material reveal = cutter.revealMaterial
                    ? cutter.revealMaterial
                    : materials.Length > 0 ? materials[0] : null;
                Matrix4x4 boxMatrix = worldToLocal * cutter.boxMatrix;
                Matrix4x4 toBox = boxMatrix.inverse;
                List<Vector4[]> cells = ClipCells(cutter, boxMatrix, TouchTolerance);
                Bounds boxBounds = BoxBounds(cutter, boxMatrix);
                List<Polygon> boxPolygons = ShapePolygons(cutter, boxMatrix, reveal, template);
                if (boxPolygons.Count == 0)
                    continue;

                List<Polygon> near = new();
                List<Polygon> far = new();
                List<Polygon> overlapping = new();
                bool penetrates = false;

                foreach (Polygon polygon in polygons) {
                    if (!boxBounds.Intersects(PolygonBounds(polygon))) {
                        far.Add(polygon);
                        continue;
                    }

                    overlapping.Add(polygon);

                    if (Penetrates(polygon, toBox, cells)) {
                        penetrates = true;
                        near.Add(polygon);
                    }
                    else if (TouchesFromOutside(polygon, boxPolygons)) {
                        far.Add(polygon);
                    }
                    else {
                        near.Add(polygon);
                    }
                }

                if (!penetrates)
                    continue;

                cutters.Add(cutter);
                Node box = new(boxPolygons);
                bool inside = cutter.createMesh && insideCutters.Contains(cutter);
                if (inside && tunnels.TryGetValue(cutter, out TunnelCap[] caps)) {
                    List<Polygon> clipped = new();
                    List<Polygon> offFace = new();
                    foreach (Polygon polygon in near)
                        if (!TrimOnCutterFace(polygon, boxPolygons, clipped))
                            offFace.Add(polygon);

                    clipped.AddRange(box.ClipPolygons(offFace));
                    far.AddRange(clipped.Where(p => !IsSliver(p)));
                    far.AddRange(GeneratedPolygons(cutter, boxMatrix, caps, new List<List<Polygon>> { overlapping }, reveal, template)
                        .Where(p => !IsSliver(p)));
                }
                else if (inside) {
                    far.AddRange(Node.Subtract(new Node(near), box).AllPolygons()
                        .Where(p => p.cutterFace < 0 || GeneratesFace(cutter, p.cutterFace)));
                }
                else {
                    far.AddRange(box.ClipPolygons(near));
                }
                polygons = far;
            }

            if (cutters.Count == 0)
                return false;

            buildHash = hash;
            buildHashVersion = CurrentBuildHashVersion;

            EnsureResult();
            List<Material> resultMaterials = BuildMesh(resultFilter, polygons, template);
            MeshOwners[resultFilter.sharedMesh] = this;
            CopyRendererSettings(gameObject, sourceRenderer, resultFilter.gameObject, resultMaterials);
            builtMaterials = resultMaterials;
            UpdateColliders();
            ApplyVisibility();
            ResultRebuilt?.Invoke(resultFilter.GetComponent<ProBuilderMesh>());
#if UNITY_EDITOR
            needsLightmapUvs = resultFilter.sharedMesh.vertexCount > 0 &&
                GameObjectUtility.AreStaticEditorFlagsSet(gameObject, StaticEditorFlags.ContributeGI);
#endif
            return true;
        }

        private bool ResultIsCurrent() {
            if (!resultFilter || cutters.Count == 0 || cutters.Any(cutter => !cutter) ||
                !resultFilter.TryGetComponent(out ProBuilderMesh _))
                return false;

            Mesh mesh = resultFilter.sharedMesh;
            if (!mesh || mesh.vertexCount == 0)
                return false;

            if (MeshOwners.TryGetValue(mesh, out TsitskiProBuilderCutTarget owner) && owner && owner != this)
                return false;

            MeshOwners[mesh] = this;
            return true;
        }

        private int BuildHash(Mesh source, Material[] materials, List<TsitskiProBuilderCutter> newCutters,
            ICollection<TsitskiProBuilderCutter> insideCutters, IReadOnlyDictionary<TsitskiProBuilderCutter, TunnelCap[]> tunnels) {
            StableHash hash = new();

            hash.Add(source.vertexCount);
            hash.Add(source.subMeshCount);
            foreach (Vector3 vertex in source.vertices)
                hash.AddRounded(vertex);
            foreach (Vector3 normal in source.normals)
                hash.AddRounded(normal);
            foreach (Vector2 uv in source.uv)
                hash.AddRounded(uv);
            foreach (Color color in source.colors) {
                hash.AddRounded(color.r);
                hash.AddRounded(color.g);
                hash.AddRounded(color.b);
                hash.AddRounded(color.a);
            }
            for (int i = 0; i < source.subMeshCount; i++)
                foreach (int index in source.GetTriangles(i))
                    hash.Add(index);

            foreach (Material material in materials)
                hash.Add(StableId(material));

            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            foreach (TsitskiProBuilderCutter cutter in newCutters) {
                if (!cutter) {
                    hash.Add(0);
                    continue;
                }

                hash.Add(cutter.ShapeHash());
                hash.Add(cutter.createMesh);
                hash.Add(cutter.generateFaces.x);
                hash.Add(cutter.generateFaces.y);
                hash.Add(cutter.generateFaces.z);
                hash.Add(cutter.clipToTunnel.x);
                hash.Add(cutter.clipToTunnel.y);
                hash.Add(cutter.clipToTunnel.z);
                hash.Add(cutter.smoothingAngle);
                hash.Add(StableId(cutter.revealMaterial));
                hash.Add(insideCutters.Contains(cutter));

                Matrix4x4 boxMatrix = worldToLocal * cutter.boxMatrix;
                for (int i = 0; i < 16; i++)
                    hash.AddRounded(boxMatrix[i]);

                if (tunnels.TryGetValue(cutter, out TunnelCap[] caps))
                    foreach (TunnelCap cap in caps) {
                        hash.Add(cap.valid);
                        hash.AddRounded(cap.normal);
                        hash.AddRounded(cap.point);
                    }
            }

            return hash.ToHashCode();
        }

        private static string StableId(Object asset) {
            if (!asset)
                return "";

#if UNITY_EDITOR
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                return guid + localId;
#endif
            return asset.name;
        }

        public void GenerateLightmapUvs() {
#if UNITY_EDITOR
            if (!resultFilter || !resultFilter.sharedMesh || resultFilter.sharedMesh.vertexCount == 0)
                return;

            Mesh mesh = resultFilter.sharedMesh;
            if (resultFilter.TryGetComponent(out ProBuilderMesh result))
                CutLightmapUnwrap.ApplyToProBuilderMesh(result);

            if (resultFilter.TryGetComponent(out MeshCollider resultCollider) && resultCollider.sharedMesh == mesh) {
                resultCollider.sharedMesh = null;
                resultCollider.sharedMesh = mesh;
            }
#endif
        }

        private static bool GeneratesFace(TsitskiProBuilderCutter cutter, int face) => (face / 2) switch {
            0 => cutter.generateFaces.x,
            1 => cutter.generateFaces.y,
            _ => cutter.generateFaces.z
        };

        private static bool ClipsToTunnel(TsitskiProBuilderCutter cutter, int axis) => axis switch {
            0 => cutter.clipToTunnel.x,
            1 => cutter.clipToTunnel.y,
            _ => cutter.clipToTunnel.z
        };

        private static List<Polygon> SolidPolygons(MeshFilter mesh, Matrix4x4 toLocal, Bounds bounds) {
            Material[] materials = mesh.TryGetComponent(out MeshRenderer meshRenderer) ? meshRenderer.sharedMaterials : new Material[0];
            bool mirrored = toLocal.determinant < 0f;
            List<Polygon> polygons = new();

            foreach (Polygon polygon in SourcePolygons(mesh.sharedMesh, materials, null, out _)) {
                List<Vertex> vertices = new(polygon.vertices.Count);
                foreach (Vertex vertex in polygon.vertices) {
                    Vertex moved = vertex;
                    moved.position = toLocal.MultiplyPoint3x4(vertex.position);
                    vertices.Add(moved);
                }

                if (mirrored)
                    vertices.Reverse();

                Polygon transformed = new(vertices, polygon.material);
                if (transformed.plane.Valid() && bounds.Intersects(PolygonBounds(transformed)))
                    polygons.Add(transformed);
            }

            return polygons;
        }

        private static List<Polygon> GeneratedPolygons(TsitskiProBuilderCutter cutter, Matrix4x4 boxMatrix, TunnelCap[] caps,
            List<List<Polygon>> solids, Material material, Vertex template) {
            Matrix4x4 normalMatrix = boxMatrix.inverse.transpose;
            List<Csg.Plane> planes = new();

            if (caps != null)
                foreach (TunnelCap cap in caps) {
                    if (!cap.valid)
                        continue;

                    Vector3 normal = normalMatrix.MultiplyVector(cap.normal).normalized;
                    planes.Add(new Csg.Plane {
                        normal = normal,
                        w = Vector3.Dot(normal, boxMatrix.MultiplyPoint3x4(cap.point))
                    });
                }

            List<Polygon> polygons = new();
            List<Node> emptySpaces = new();
            List<Node> solidSpaces = new();
            for (int i = 0; i < solids.Count; i++) {
                Node emptySpace = new(solids[i].Select(p => Shifted(p, Vector3.zero)).ToList());
                emptySpace.Invert();
                emptySpaces.Add(emptySpace);
                if (i < solids.Count - 1)
                    solidSpaces.Add(new Node(solids[i].Select(p => Shifted(p, Vector3.zero)).ToList()));
            }

            foreach (Polygon polygon in ShapePolygons(cutter, boxMatrix, material, template)) {
                if (ClipsToTunnel(cutter, polygon.cutterFace / 2) || !GeneratesFace(cutter, polygon.cutterFace))
                    continue;

                Vector3 outward = polygon.plane.normal * SolidProbeDistance;
                polygon.Flip();
                List<Polygon> pieces = new() { polygon };

                foreach (Csg.Plane plane in planes) {
                    List<Polygon> kept = new();
                    List<Polygon> discarded = new();
                    foreach (Polygon piece in pieces)
                        plane.SplitPolygon(piece, kept, kept, discarded, kept);
                    pieces = kept;
                }

                List<Polygon> probes = pieces.Select(p => Shifted(p, outward)).ToList();
                for (int i = 0; i < emptySpaces.Count && probes.Count > 0; i++) {
                    List<Polygon> inside = emptySpaces[i].ClipPolygons(probes.Select(p => Shifted(p, Vector3.zero)).ToList());
                    polygons.AddRange(inside.Select(p => Shifted(p, -outward)));
                    if (i < solidSpaces.Count)
                        probes = solidSpaces[i].ClipPolygons(probes);
                }
            }

            return polygons;
        }

        private static Polygon Shifted(Polygon polygon, Vector3 offset) {
            List<Vertex> vertices = new(polygon.vertices.Count);
            foreach (Vertex vertex in polygon.vertices) {
                Vertex shifted = vertex;
                shifted.position += offset;
                vertices.Add(shifted);
            }

            return new Polygon(vertices, polygon.material) { cutterFace = polygon.cutterFace, smoothingGroup = polygon.smoothingGroup, smoothingAngle = polygon.smoothingAngle };
        }

        private static bool TrimOnCutterFace(Polygon polygon, List<Polygon> cutterPolygons, List<Polygon> output) {
            List<Polygon> faces = cutterPolygons.Where(f =>
                Vector3.Dot(polygon.plane.normal, f.plane.normal) > 0.999f &&
                polygon.vertices.All(v => Mathf.Abs(Vector3.Dot(f.plane.normal, v.position) - f.plane.w) <= TouchTolerance)).ToList();

            if (faces.Count == 0)
                return false;

            List<Polygon> outside = new() { polygon };

            foreach (Polygon face in faces) {
                List<Polygon> remaining = new();

                foreach (Polygon piece in outside) {
                    List<Polygon> inside = new() { piece };

                    foreach (Csg.Plane edge in EdgePlanes(face)) {
                        List<Polygon> back = new();
                        foreach (Polygon part in inside)
                            edge.SplitPolygon(part, remaining, back, remaining, back);
                        inside = back;
                    }
                }

                outside = remaining;
            }

            output.AddRange(outside);
            return true;
        }

        private static List<Csg.Plane> EdgePlanes(Polygon face) {
            List<Csg.Plane> planes = new();
            Vector3 centroid = Vector3.zero;
            foreach (Vertex vertex in face.vertices)
                centroid += vertex.position;
            centroid /= face.vertices.Count;

            for (int i = 0; i < face.vertices.Count; i++) {
                Vector3 a = face.vertices[i].position;
                Vector3 b = face.vertices[(i + 1) % face.vertices.Count].position;
                Vector3 normal = Vector3.Cross(b - a, face.plane.normal).normalized;
                if (normal == Vector3.zero)
                    continue;

                float w = Vector3.Dot(normal, a);
                if (Vector3.Dot(normal, centroid) > w) {
                    normal = -normal;
                    w = -w;
                }

                planes.Add(new Csg.Plane { normal = normal, w = w });
            }

            return planes;
        }

        private static bool IsSliver(Polygon polygon) {
            List<Vertex> vertices = polygon.vertices;
            Vector3 area = Vector3.zero;
            float longestEdge = 0f;

            for (int i = 0; i < vertices.Count; i++) {
                Vector3 a = vertices[i].position;
                Vector3 b = vertices[(i + 1) % vertices.Count].position;
                longestEdge = Mathf.Max(longestEdge, Vector3.Distance(a, b));

                if (i >= 2)
                    area += Vector3.Cross(vertices[i - 1].position - vertices[0].position, vertices[i].position - vertices[0].position);
            }

            return longestEdge <= 0f || area.magnitude / longestEdge < TouchTolerance;
        }

        public static bool TunnelCaps(Mesh mesh, Matrix4x4 localToWorld, TsitskiProBuilderCutter cutter, TunnelCap[] caps) {
            bool found = false;
            foreach ((Vector3 normal, Vector3 centroid, List<Vector3> _) in PenetratingTriangles(mesh, localToWorld, cutter)) {
                found = true;

                for (int axis = 0; axis < 3; axis++) {
                    if (!ClipsToTunnel(cutter, axis))
                        continue;

                    UpdateCap(ref caps[axis * 2], normal, centroid, axis, 1f);
                    UpdateCap(ref caps[axis * 2 + 1], normal, centroid, axis, -1f);
                }
            }

            return found;
        }

        public static bool AxisRange(Mesh mesh, Matrix4x4 localToWorld, TsitskiProBuilderCutter cutter, int axis, out Vector2 range) {
            range = new Vector2(float.MaxValue, float.MinValue);
            foreach ((Vector3 _, Vector3 _, List<Vector3> points) in PenetratingTriangles(mesh, localToWorld, cutter))
                foreach (Vector3 point in points)
                    range = new Vector2(Mathf.Min(range.x, point[axis]), Mathf.Max(range.y, point[axis]));

            return range.x <= range.y;
        }

        private static IEnumerable<(Vector3 normal, Vector3 centroid, List<Vector3> points)> PenetratingTriangles(Mesh mesh,
            Matrix4x4 localToWorld, TsitskiProBuilderCutter cutter) {
            Matrix4x4 boxMatrix = localToWorld.inverse * cutter.boxMatrix;
            Matrix4x4 toBox = boxMatrix.inverse;
            float winding = toBox.determinant < 0f ? -1f : 1f;
            List<Vector4[]> cells = ClipCells(cutter, boxMatrix, TouchTolerance);
            List<Vector4[]> fullCells = ClipCells(cutter, boxMatrix, 0f);
            Vector3[] positions = mesh.vertices;
            Bounds boxBounds = BoxBounds(cutter, boxMatrix);

            for (int s = 0; s < mesh.subMeshCount; s++) {
                if (mesh.GetTopology(s) != MeshTopology.Triangles)
                    continue;

                int[] triangles = mesh.GetTriangles(s);
                for (int i = 0; i < triangles.Length; i += 3) {
                    if (!TriangleOverlaps(boxBounds, positions[triangles[i]], positions[triangles[i + 1]], positions[triangles[i + 2]]))
                        continue;

                    List<Vector3> points = new() {
                        toBox.MultiplyPoint3x4(positions[triangles[i]]),
                        toBox.MultiplyPoint3x4(positions[triangles[i + 1]]),
                        toBox.MultiplyPoint3x4(positions[triangles[i + 2]])
                    };

                    if (ClippedArea(points, cells) <= 5e-7f)
                        continue;

                    Vector3 normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized * winding;
                    List<Vector3> clipped = fullCells.SelectMany(c => ClipCell(points, c)).ToList();
                    if (clipped.Count < 3 || normal == Vector3.zero)
                        continue;

                    Vector3 centroid = Vector3.zero;
                    foreach (Vector3 point in clipped)
                        centroid += point;
                    centroid /= clipped.Count;

                    yield return (normal, centroid, clipped);
                }
            }
        }

        private static void UpdateCap(ref TunnelCap cap, Vector3 normal, Vector3 centroid, int axis, float side) {
            if (normal[axis] * side < 0.1f)
                return;

            if (cap.valid && centroid[axis] * side <= cap.point[axis] * side)
                return;

            cap = new TunnelCap { valid = true, normal = normal, point = centroid };
        }

        private static List<Vector4[]> ClipCells(TsitskiProBuilderCutter cutter, Matrix4x4 boxMatrix, float tolerance) {
            Matrix4x4 normalMatrix = boxMatrix.inverse.transpose;
            List<Vector4[]> cells = new();

            Vector4 HalfSpace(Vector3 normal, float distance) {
                float shrink = tolerance * normalMatrix.MultiplyVector(normal).magnitude;
                return new Vector4(normal.x, normal.y, normal.z, distance - shrink);
            }

            foreach (Vector4[] cell in CutterVolume.Cells(cutter))
                cells.Add(cell.Select(p => HalfSpace(p, p.w)).ToArray());

            return cells;
        }

        public static float PenetrationArea(Mesh mesh, Matrix4x4 localToWorld, TsitskiProBuilderCutter cutter) {
            Matrix4x4 boxMatrix = localToWorld.inverse * cutter.boxMatrix;
            Matrix4x4 toBox = boxMatrix.inverse;
            List<Vector4[]> cells = ClipCells(cutter, boxMatrix, TouchTolerance);
            Vector3[] positions = mesh.vertices;
            Bounds boxBounds = BoxBounds(cutter, boxMatrix);
            float area = 0f;

            for (int s = 0; s < mesh.subMeshCount; s++) {
                if (mesh.GetTopology(s) != MeshTopology.Triangles)
                    continue;

                int[] triangles = mesh.GetTriangles(s);
                for (int i = 0; i < triangles.Length; i += 3)
                    if (TriangleOverlaps(boxBounds, positions[triangles[i]], positions[triangles[i + 1]], positions[triangles[i + 2]]))
                        area += ClippedArea(new List<Vector3> {
                        toBox.MultiplyPoint3x4(positions[triangles[i]]),
                        toBox.MultiplyPoint3x4(positions[triangles[i + 1]]),
                        toBox.MultiplyPoint3x4(positions[triangles[i + 2]])
                    }, cells);
            }

            return area;
        }

        private static bool TriangleOverlaps(Bounds bounds, Vector3 a, Vector3 b, Vector3 c) {
            Vector3 min = Vector3.Min(a, Vector3.Min(b, c)), max = Vector3.Max(a, Vector3.Max(b, c));
            return min.x <= bounds.max.x && max.x >= bounds.min.x &&
                min.y <= bounds.max.y && max.y >= bounds.min.y &&
                min.z <= bounds.max.z && max.z >= bounds.min.z;
        }

        private static bool Penetrates(Polygon polygon, Matrix4x4 toBox, List<Vector4[]> cells) {
            List<Vector3> points = new();
            foreach (Vertex vertex in polygon.vertices)
                points.Add(toBox.MultiplyPoint3x4(vertex.position));

            return ClippedArea(points, cells) > 5e-7f;
        }

        private static float ClippedArea(List<Vector3> points, List<Vector4[]> cells) {
            float total = 0f;

            foreach (Vector4[] cell in cells) {
                List<Vector3> clipped = ClipCell(points, cell);

                Vector3 area = Vector3.zero;
                for (int i = 2; i < clipped.Count; i++)
                    area += Vector3.Cross(clipped[i - 1] - clipped[0], clipped[i] - clipped[0]);

                total += area.magnitude * 0.5f;
            }

            return total;
        }

        private static bool TouchesFromOutside(Polygon polygon, List<Polygon> boxPolygons) {
            foreach (Polygon face in boxPolygons) {
                if (Vector3.Dot(polygon.plane.normal, face.plane.normal) > -0.999f)
                    continue;

                bool coplanar = true;
                foreach (Vertex vertex in polygon.vertices)
                    if (Mathf.Abs(Vector3.Dot(face.plane.normal, vertex.position) - face.plane.w) > TouchTolerance) {
                        coplanar = false;
                        break;
                    }

                if (coplanar)
                    return true;
            }

            return false;
        }

        private static List<Vector3> ClipCell(List<Vector3> points, Vector4[] cell) {
            for (int i = 0; i < cell.Length && points.Count >= 3; i++)
                points = ClipPlane(points, cell[i]);

            return points.Count >= 3 ? points : new List<Vector3>();
        }

        private static List<Vector3> ClipPlane(List<Vector3> points, Vector4 plane) {
            List<Vector3> clipped = new();
            Vector3 normal = plane;

            for (int i = 0; i < points.Count; i++) {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Count];
                float da = plane.w - Vector3.Dot(normal, a);
                float db = plane.w - Vector3.Dot(normal, b);

                if (da >= 0f)
                    clipped.Add(a);
                if (da >= 0f != db >= 0f)
                    clipped.Add(Vector3.Lerp(a, b, da / (da - db)));
            }

            return clipped;
        }

        public void Remove() {
            foreach (Collider disabledCollider in disabledColliders)
                if (disabledCollider)
                    disabledCollider.enabled = true;

            if (TryGetComponent(out MeshRenderer sourceRenderer))
                sourceRenderer.enabled = true;

            if (resultFilter)
                DestroyImmediate(resultFilter.gameObject);

            DestroyImmediate(this);
        }

        private void ApplyVisibility() {
            if (!resultFilter)
                return;

            if (TryGetComponent(out MeshRenderer sourceRenderer))
                sourceRenderer.enabled = _editing;

            if (resultFilter.TryGetComponent(out MeshRenderer resultRenderer))
                resultRenderer.enabled = !_editing;

            if (resultFilter.TryGetComponent(out MeshCollider resultCollider))
                resultCollider.enabled = !_editing;
        }

        private Dictionary<(int, int, int), int> SourceSmoothingGroups(Mesh source) {
            if (!TryGetComponent(out ProBuilderMesh proBuilderMesh) || proBuilderMesh.vertexCount != source.vertexCount)
                return null;

            Dictionary<(int, int, int), int> groups = new();
            foreach (ProBuilderFace face in proBuilderMesh.faces) {
                IReadOnlyList<int> indexes = face.indexes;
                for (int i = 0; i + 2 < indexes.Count; i += 3)
                    groups[(indexes[i], indexes[i + 1], indexes[i + 2])] = face.smoothingGroup;
            }

            return groups;
        }

        private static List<Polygon> SourcePolygons(Mesh source, Material[] materials,
            Dictionary<(int, int, int), int> smoothingGroups, out Vertex template) {
            List<Polygon> polygons = new();
            Vertex[] vertices = VertexUtility.GetVertices(source);
            template = vertices.Length > 0 ? vertices[0] : new Vertex();

            for (int s = 0; s < source.subMeshCount; s++) {
                if (source.GetTopology(s) != MeshTopology.Triangles)
                    continue;

                Material material = materials.Length > 0 ? materials[Mathf.Min(s, materials.Length - 1)] : null;
                int[] triangles = source.GetTriangles(s);

                for (int i = 0; i < triangles.Length; i += 3) {
                    Vertex a = vertices[triangles[i]];
                    Vertex b = vertices[triangles[i + 1]];
                    Vertex c = vertices[triangles[i + 2]];

                    Polygon polygon = new(new List<Vertex> { a, b, c }, material);
                    if (smoothingGroups != null &&
                        smoothingGroups.TryGetValue((triangles[i], triangles[i + 1], triangles[i + 2]), out int group))
                        polygon.smoothingGroup = group;
                    if (polygon.plane.Valid())
                        polygons.Add(polygon);
                }
            }

            return polygons;
        }

        private static Bounds BoxBounds(TsitskiProBuilderCutter cutter, Matrix4x4 matrix) {
            Bounds local = CutterVolume.LocalBounds(cutter);
            Bounds bounds = new(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f))));
            bounds.Expand(CSG.epsilon * 4f);
            return bounds;
        }

        private static Bounds PolygonBounds(Polygon polygon) {
            Bounds bounds = new(polygon.vertices[0].position, Vector3.zero);
            for (int i = 1; i < polygon.vertices.Count; i++)
                bounds.Encapsulate(polygon.vertices[i].position);
            return bounds;
        }

        private static List<Polygon> ShapePolygons(TsitskiProBuilderCutter cutter, Matrix4x4 matrix, Material material, Vertex template) {
            List<Polygon> polygons = new();
            foreach (CutterFace face in CutterVolume.Faces(cutter))
                AddShapePolygon(polygons, face.corners, face.face, matrix, material, template);
            foreach (Polygon polygon in polygons)
                polygon.smoothingAngle = cutter.smoothingAngle;
            return polygons;
        }

        private static void AddShapePolygon(List<Polygon> polygons, List<Vector3> corners, int face, Matrix4x4 matrix,
            Material material, Vertex template) {
            List<Vector3> points = corners.Select(matrix.MultiplyPoint3x4).ToList();
            if (matrix.determinant < 0f)
                points.Reverse();

            if (points.Count < 3)
                return;

            points = CutterVolume.StartAtCorner(points);
            Vector3 normal = CutterVolume.Normal(points).normalized;
            if (normal == Vector3.zero)
                return;

            List<Vertex> vertices = new();
            foreach (Vector3 point in points)
                vertices.Add(BoxVertex(point, normal, template));

            polygons.Add(new Polygon(vertices, material) { cutterFace = face });
        }

        private static Vertex BoxVertex(Vector3 position, Vector3 normal, Vertex template) {
            Vertex vertex = new() { position = position };

            if (template.hasNormal)
                vertex.normal = normal;
            if (template.hasColor)
                vertex.color = template.color;
            if (template.hasTangent)
                vertex.tangent = new Vector4(1f, 0f, 0f, 1f);
            if (template.hasUV0)
                vertex.uv0 = PlanarUv(position, normal);
            if (template.hasUV2)
                vertex.uv2 = Vector2.zero;
            if (template.hasUV3)
                vertex.uv3 = Vector4.zero;
            if (template.hasUV4)
                vertex.uv4 = Vector4.zero;

            return vertex;
        }

        private static Vector2 PlanarUv(Vector3 position, Vector3 normal) {
            float x = Mathf.Abs(normal.x), y = Mathf.Abs(normal.y), z = Mathf.Abs(normal.z);

            if (x >= y && x >= z)
                return new Vector2(position.z, position.y);
            if (y >= z)
                return new Vector2(position.x, position.z);
            return new Vector2(position.x, position.y);
        }

        private void EnsureResult() {
            if (resultFilter && !resultFilter.TryGetComponent(out ProBuilderMesh _)) {
                DestroyImmediate(resultFilter.gameObject);
                resultFilter = null;
            }

            if (!resultFilter) {
                GameObject resultObject = new("Cut Result");
                resultObject.transform.SetParent(transform, false);
                resultObject.AddComponent<ProBuilderMesh>().Clear();
                resultFilter = resultObject.GetComponent<MeshFilter>();
            }

            LockResult();
            resultFilter.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            resultFilter.transform.localScale = Vector3.one;

            Mesh mesh = resultFilter.sharedMesh;
            if (mesh && MeshOwners.TryGetValue(mesh, out TsitskiProBuilderCutTarget owner) && owner && owner != this)
                ProBuilderCompat.MakeUniqueMesh(resultFilter.GetComponent<ProBuilderMesh>());
        }

        public static void BuildCutterFaces(TsitskiProBuilderCutter cutter, List<MeshFilter> meshes, TunnelCap[] caps) {
            MeshFilter first = meshes.FirstOrDefault(m => m && m.sharedMesh);
            if (!cutter.createMesh || !first) {
                ClearCutterFaces(cutter);
                return;
            }

            MeshFilter filter = EnsureCutterFaces(cutter);
            Matrix4x4 worldToLocal = filter.transform.worldToLocalMatrix;
            Matrix4x4 boxMatrix = worldToLocal * cutter.boxMatrix;
            Bounds boxBounds = BoxBounds(cutter, boxMatrix);

            MeshRenderer firstRenderer = first.GetComponent<MeshRenderer>();
            Material[] materials = firstRenderer ? firstRenderer.sharedMaterials : new Material[0];
            Material material = cutter.revealMaterial ? cutter.revealMaterial : materials.Length > 0 ? materials[0] : null;
            Vertex[] vertices = VertexUtility.GetVertices(first.sharedMesh);
            Vertex template = vertices.Length > 0 ? vertices[0] : new Vertex();

            List<List<Polygon>> solids = meshes
                .Where(m => m && m.sharedMesh)
                .Select(m => SolidPolygons(m, worldToLocal * m.transform.localToWorldMatrix, boxBounds))
                .Where(s => s.Count > 0)
                .ToList();
            List<Polygon> polygons = solids.Count == 0
                ? new List<Polygon>()
                : GeneratedPolygons(cutter, boxMatrix, caps, solids, material, template).Where(p => !IsSliver(p)).ToList();

            List<Material> built = BuildMesh(filter, polygons, template);
            CutterFaceOwners[filter.sharedMesh] = cutter;
            if (firstRenderer)
                CopyRendererSettings(first.gameObject, firstRenderer, filter.gameObject, built);

            if (filter.TryGetComponent(out MeshCollider collider)) {
                collider.sharedMesh = null;
                collider.sharedMesh = filter.sharedMesh;
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(filter.GetComponent<ProBuilderMesh>());
#endif
            ResultRebuilt?.Invoke(filter.GetComponent<ProBuilderMesh>());
        }

        public static void ClearCutterFaces(TsitskiProBuilderCutter cutter) {
            if (!cutter || !cutter.facesFilter)
                return;

            DestroyImmediate(cutter.facesFilter.gameObject);
            cutter.facesFilter = null;
#if UNITY_EDITOR
            EditorUtility.SetDirty(cutter);
#endif
        }

        private static MeshFilter EnsureCutterFaces(TsitskiProBuilderCutter cutter) {
            if (cutter.facesFilter && !cutter.facesFilter.TryGetComponent(out ProBuilderMesh _)) {
                DestroyImmediate(cutter.facesFilter.gameObject);
                cutter.facesFilter = null;
            }

            if (!cutter.facesFilter) {
                GameObject facesObject = new("Cutter Faces");
                facesObject.transform.SetParent(cutter.transform, false);
                facesObject.AddComponent<ProBuilderMesh>().Clear();
                facesObject.AddComponent<MeshCollider>();
                cutter.facesFilter = facesObject.GetComponent<MeshFilter>();
#if UNITY_EDITOR
                EditorUtility.SetDirty(cutter);
#endif
            }

            MeshFilter filter = cutter.facesFilter;
            foreach (Component component in filter.GetComponents<Component>())
                if (component)
                    component.hideFlags = component is Transform or MeshFilter or ProBuilderMesh or MeshCollider
                        ? HideFlags.NotEditable
                        : HideFlags.None;
            filter.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            filter.transform.localScale = Vector3.one;

            Mesh mesh = filter.sharedMesh;
            if (mesh && CutterFaceOwners.TryGetValue(mesh, out TsitskiProBuilderCutter owner) && owner && owner != cutter)
                ProBuilderCompat.MakeUniqueMesh(filter.GetComponent<ProBuilderMesh>());

            return filter;
        }

        private static List<Material> BuildMesh(MeshFilter resultFilter, List<Polygon> polygons, Vertex template) {
            List<Material> materials = new();
            List<Vector3> positions = new();
            List<Vector2> uvs = new();
            List<Color> colors = new();
            List<ProBuilderFace> faces = new();
            Dictionary<float, List<(ProBuilderFace face, Vector3 normal)>> smoothed = new();

            foreach (Polygon polygon in polygons) {
                int submesh = materials.IndexOf(polygon.material);
                if (submesh < 0) {
                    submesh = materials.Count;
                    materials.Add(polygon.material);
                }

                int start = positions.Count;
                foreach (Vertex vertex in polygon.vertices) {
                    positions.Add(vertex.position);
                    uvs.Add(vertex.uv0);
                    if (template.hasColor)
                        colors.Add(vertex.color);
                }

                List<int> indexes = new();
                for (int j = 2; j < polygon.vertices.Count; j++) {
                    indexes.Add(start);
                    indexes.Add(start + j - 1);
                    indexes.Add(start + j);
                }

                ProBuilderFace face = new(indexes) {
                    submeshIndex = submesh,
                    manualUV = true,
                    smoothingGroup = polygon.smoothingGroup >= 0 ? polygon.smoothingGroup :
                        template.hasNormal && IsSmooth(polygon) ? 1 : 0
                };
                faces.Add(face);

                if (polygon.cutterFace >= 0 && polygon.smoothingAngle > 0f) {
                    if (!smoothed.TryGetValue(polygon.smoothingAngle, out List<(ProBuilderFace, Vector3)> group))
                        smoothed[polygon.smoothingAngle] = group = new List<(ProBuilderFace, Vector3)>();
                    group.Add((face, polygon.plane.normal));
                }
            }

            ApplySmoothingAngles(positions, faces, smoothed);

            resultFilter.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();

            ProBuilderMesh result = resultFilter.GetComponent<ProBuilderMesh>();
            result.Clear();
            if (faces.Count > 0) {
                result.positions = positions;
                result.faces = faces;
                result.textures = uvs;
                if (template.hasColor)
                    result.colors = colors;
                result.sharedVertices = SharedVertex.GetSharedVerticesWithPositions(positions);
            }

            result.ToMesh();
            result.Refresh();
            return materials;
        }

        private const float SharedCornerResolution = 1000f;

        private static void ApplySmoothingAngles(List<Vector3> positions, List<ProBuilderFace> allFaces,
            Dictionary<float, List<(ProBuilderFace face, Vector3 normal)>> smoothed) {
            if (smoothed.Count == 0)
                return;

            HashSet<int> used = new(allFaces.Select(f => f.smoothingGroup));
            int next = 1;

            foreach ((float angle, List<(ProBuilderFace face, Vector3 normal)> group) in smoothed) {
                int[] parent = Enumerable.Range(0, group.Count).ToArray();

                int Find(int i) {
                    while (parent[i] != i)
                        i = parent[i] = parent[parent[i]];
                    return i;
                }

                Dictionary<Vector3Int, List<int>> byCorner = new();
                for (int i = 0; i < group.Count; i++) {
                    foreach (int index in group[i].face.distinctIndexes) {
                        Vector3 p = positions[index] * SharedCornerResolution;
                        Vector3Int key = new(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                        if (!byCorner.TryGetValue(key, out List<int> sharing))
                            byCorner[key] = sharing = new List<int>();
                        if (sharing.Count == 0 || sharing[^1] != i)
                            sharing.Add(i);
                    }
                }

                foreach (List<int> sharing in byCorner.Values)
                    for (int a = 0; a < sharing.Count; a++)
                        for (int b = a + 1; b < sharing.Count; b++)
                            if (Vector3.Angle(group[sharing[a]].normal, group[sharing[b]].normal) < angle)
                                parent[Find(sharing[a])] = Find(sharing[b]);

                foreach (IGrouping<int, int> component in Enumerable.Range(0, group.Count).GroupBy(Find)) {
                    if (component.Count() < 2)
                        continue;

                    while (used.Contains(next) || !UnityEngine.ProBuilder.Smoothing.IsSmooth(next))
                        next++;
                    used.Add(next);

                    foreach (int i in component)
                        group[i].face.smoothingGroup = next;
                }
            }
        }

        private static bool IsSmooth(Polygon polygon) {
            Vector3 normal = polygon.plane.normal;
            foreach (Vertex vertex in polygon.vertices)
                if (Vector3.Dot(vertex.normal.normalized, normal) < SmoothNormalCos)
                    return true;
            return false;
        }

#if UNITY_EDITOR
        public ProBuilderMesh Bake() {
            ProBuilderMesh source = GetComponent<ProBuilderMesh>();
            ProBuilderMesh result = resultFilter ? resultFilter.GetComponent<ProBuilderMesh>() : null;
            if (!source || !result || result.faceCount == 0)
                return null;

            MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
            Mesh sourceMesh = GetComponent<MeshFilter>().sharedMesh;
            List<Object> recorded = new() { source, sourceRenderer };
            if (sourceMesh)
                recorded.Add(sourceMesh);
            recorded.AddRange(disabledColliders.Where(c => c));
            Undo.RegisterCompleteObjectUndo(recorded.ToArray(), "Bake Cuts");

            if (!TryGetComponent(out TsitskiProBuilderBakedCuts baked))
                baked = Undo.AddComponent<TsitskiProBuilderBakedCuts>(gameObject);
            else
                Undo.RecordObject(baked, "Bake Cuts");
            baked.cutters.RemoveAll(c => !c);
            baked.cutters.AddRange(cutters.Where(c => c && !baked.cutters.Contains(c)));

            foreach (Collider disabledCollider in disabledColliders)
                if (disabledCollider)
                    disabledCollider.enabled = true;

            sourceRenderer.sharedMaterials = resultFilter.GetComponent<MeshRenderer>().sharedMaterials;
            sourceRenderer.enabled = true;

            source.Clear();
            source.positions = result.positions;
            source.faces = result.faces.Select(face => new ProBuilderFace(face)).ToList();
            source.textures = result.textures;
            if (result.colors is { Count: > 0 } colors)
                source.colors = colors;
            source.sharedVertices = result.sharedVertices;
            source.ToMesh();
            source.Refresh();

            Undo.DestroyObjectImmediate(resultFilter.gameObject);
            Undo.DestroyObjectImmediate(this);
            return source;
        }
#endif

        private static void CopyRendererSettings(GameObject sourceObject, MeshRenderer source, GameObject resultObject, List<Material> materials) {
            resultObject.layer = sourceObject.layer;
            resultObject.tag = sourceObject.tag;

#if UNITY_EDITOR
            GameObjectUtility.SetStaticEditorFlags(resultObject, GameObjectUtility.GetStaticEditorFlags(sourceObject));
#endif

            MeshRenderer resultRenderer = resultObject.GetComponent<MeshRenderer>();
            resultRenderer.sharedMaterials = materials.ToArray();
            resultRenderer.shadowCastingMode = source.shadowCastingMode;
            resultRenderer.receiveShadows = source.receiveShadows;
            resultRenderer.staticShadowCaster = source.staticShadowCaster;
            resultRenderer.lightProbeUsage = source.lightProbeUsage;
            resultRenderer.reflectionProbeUsage = source.reflectionProbeUsage;
            resultRenderer.renderingLayerMask = source.renderingLayerMask;
#if UNITY_EDITOR
            resultRenderer.scaleInLightmap = source.scaleInLightmap;
            resultRenderer.receiveGI = source.receiveGI;
            resultRenderer.stitchLightmapSeams = source.stitchLightmapSeams;
#endif
        }

        private void UpdateColliders() {
            foreach (Collider sourceCollider in GetComponents<Collider>()) {
                if (!sourceCollider.enabled)
                    continue;

                sourceCollider.enabled = false;
                if (!disabledColliders.Contains(sourceCollider))
                    disabledColliders.Add(sourceCollider);
            }

            disabledColliders.RemoveAll(c => !c);

            GameObject resultObject = resultFilter.gameObject;
            bool hasCollider = resultObject.TryGetComponent(out MeshCollider resultCollider);

            if (disabledColliders.Count == 0) {
                if (hasCollider)
                    DestroyImmediate(resultCollider);
                return;
            }

            if (!hasCollider)
                resultCollider = resultObject.AddComponent<MeshCollider>();

            resultCollider.sharedMaterial = disabledColliders[0].sharedMaterial;
            resultCollider.sharedMesh = null;
            if (resultFilter.sharedMesh.vertexCount > 0)
                resultCollider.sharedMesh = resultFilter.sharedMesh;
        }
    }

    internal static class CutLightmapUnwrap {
        private const float WeldPrecision = 10000f;
        private const float HardAngleCos = 0.766f;
        private const float ChartAngleCos = 0.5f;
        private const float CoplanarCos = 0.9999f;
        private const float CoplanarDistance = 0.0005f;
        private const float PlacementTolerance = 0.001f;
        private const float DegenerateArea = 1e-9f;
        private const int MaxRotationCandidates = 24;

        private class Chart {
            public readonly List<int> triangles = new();
            public readonly Dictionary<int, Vector2> points = new();
            public Vector2 min;
            public Vector2 size;
            public Vector2 offset;
        }

        private class Solution {
            public int[] corners;
            public int[] chartOf;
            public List<Chart> charts;
            public float scale;

            public Vector2 Uv(int t, int k) {
                Chart chart = charts[chartOf[t]];
                return (chart.points[corners[t * 3 + k]] + chart.offset) * scale;
            }
        }

        public static void ApplyToProBuilderMesh(UnityEngine.ProBuilder.ProBuilderMesh proBuilderMesh) {
            Mesh mesh = proBuilderMesh.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (!mesh || mesh.vertexCount != proBuilderMesh.vertexCount || proBuilderMesh.faceCount == 0)
                return;

            Matrix4x4 localToWorld = proBuilderMesh.transform.localToWorldMatrix;
            Vector3[] world = proBuilderMesh.positions.Select(localToWorld.MultiplyPoint3x4).ToArray();
            List<int> triangleVertices = new();
            List<int> triangleGroups = new();
            IList<UnityEngine.ProBuilder.Face> faces = proBuilderMesh.faces;
            for (int f = 0; f < faces.Count; f++) {
                IReadOnlyList<int> indexes = faces[f].indexes;
                for (int i = 0; i < indexes.Count; i++)
                    triangleVertices.Add(indexes[i]);
                for (int i = 0; i < indexes.Count / 3; i++)
                    triangleGroups.Add(f);
            }

            if (triangleVertices.Count == 0)
                return;

            Solution solution = Solve(world, triangleVertices, triangleGroups.ToArray(),
                LightmapPadding(proBuilderMesh.GetComponent<MeshRenderer>()));

            Vector2[] uvs = new Vector2[mesh.vertexCount];
            for (int t = 0; t < triangleVertices.Count / 3; t++)
                for (int k = 0; k < 3; k++)
                    uvs[triangleVertices[t * 3 + k]] = solution.Uv(t, k);

            mesh.SetUVs(1, uvs);
        }

        private static Solution Solve(Vector3[] world, List<int> triangleVertices, int[] triangleGroups, float padding) {
            int triangleCount = triangleVertices.Count / 3;

            Dictionary<Vector3Int, int> weldIds = new();
            List<Vector3> weldPositions = new();
            int[] weldOf = new int[world.Length];
            for (int i = 0; i < world.Length; i++) {
                Vector3Int key = Vector3Int.RoundToInt(world[i] * WeldPrecision);
                if (!weldIds.TryGetValue(key, out int weld)) {
                    weld = weldPositions.Count;
                    weldPositions.Add(world[i]);
                    weldIds.Add(key, weld);
                }
                weldOf[i] = weld;
            }

            Mesher mesher = new() {
                corners = new int[triangleCount * 3],
                normals = new Vector3[triangleCount],
                areas = new float[triangleCount],
                weldPositions = weldPositions,
                edgeTriangles = new Dictionary<long, List<int>>(),
                weldTriangles = new List<int>[weldPositions.Count],
                chartOf = Enumerable.Repeat(-1, triangleCount).ToArray(),
                groups = triangleGroups
            };

            if (triangleGroups != null) {
                mesher.groupTriangles = new Dictionary<int, List<int>>();
                for (int t = 0; t < triangleCount; t++) {
                    if (!mesher.groupTriangles.TryGetValue(triangleGroups[t], out List<int> members))
                        mesher.groupTriangles.Add(triangleGroups[t], members = new List<int>());
                    members.Add(t);
                }
            }

            for (int t = 0; t < triangleCount; t++) {
                for (int k = 0; k < 3; k++)
                    mesher.corners[t * 3 + k] = weldOf[triangleVertices[t * 3 + k]];

                Vector3 a = weldPositions[mesher.corners[t * 3]];
                Vector3 cross = Vector3.Cross(weldPositions[mesher.corners[t * 3 + 1]] - a,
                    weldPositions[mesher.corners[t * 3 + 2]] - a);
                mesher.areas[t] = cross.magnitude * 0.5f;
                mesher.normals[t] = mesher.areas[t] > DegenerateArea ? cross.normalized : Vector3.zero;

                for (int k = 0; k < 3; k++) {
                    int weld = mesher.corners[t * 3 + k];
                    (mesher.weldTriangles[weld] ??= new List<int>()).Add(t);

                    long edge = EdgeKey(weld, mesher.corners[t * 3 + (k + 1) % 3]);
                    if (!mesher.edgeTriangles.TryGetValue(edge, out List<int> list))
                        mesher.edgeTriangles.Add(edge, list = new List<int>());
                    list.Add(t);
                }
            }

            List<Chart> charts = new();

            foreach (int seed in Enumerable.Range(0, triangleCount).Where(t => mesher.areas[t] > DegenerateArea)
                         .OrderByDescending(t => mesher.areas[t]))
                if (mesher.chartOf[seed] < 0)
                    charts.Add(mesher.Grow(seed, charts.Count));

            for (int t = 0; t < triangleCount; t++) {
                if (mesher.chartOf[t] >= 0)
                    continue;

                if (triangleGroups == null)
                    foreach (int neighbour in mesher.weldTriangles[mesher.corners[t * 3]]) {
                        int chart = mesher.chartOf[neighbour];
                        if (chart < 0 || !ContainsCorners(charts[chart], mesher.corners, t))
                            continue;

                        mesher.chartOf[t] = chart;
                        charts[chart].triangles.Add(t);
                        break;
                    }

                if (mesher.chartOf[t] < 0)
                    charts.Add(mesher.Grow(t, charts.Count));
            }

            foreach (Chart chart in charts)
                Orient(chart, mesher.corners);

            return new Solution {
                corners = mesher.corners,
                chartOf = mesher.chartOf,
                charts = charts,
                scale = Pack(charts, padding)
            };
        }

        private class Mesher {
            public int[] corners;
            public Vector3[] normals;
            public float[] areas;
            public List<Vector3> weldPositions;
            public Dictionary<long, List<int>> edgeTriangles;
            public List<int>[] weldTriangles;
            public int[] chartOf;
            public int[] groups;
            public Dictionary<int, List<int>> groupTriangles;

            private Chart _chart;
            private int _chartIndex;
            private Vector3 _normal;
            private float _planeDistance;
            private Vector3 _tangent;
            private Vector3 _bitangent;
            private readonly Dictionary<int, Vector2> _pending = new();
            private readonly List<int> _placedTriangles = new();
            private readonly Queue<(int triangle, int from)> _queue = new();
            private readonly Vector2[] _placed = new Vector2[3];

            public Chart Grow(int seed, int chartIndex) {
                _chart = new Chart();
                _chartIndex = chartIndex;
                _normal = normals[seed] != Vector3.zero ? normals[seed] : Vector3.up;
                _planeDistance = Vector3.Dot(_normal, weldPositions[corners[seed * 3]]);
                _tangent = Vector3.Cross(_normal, Mathf.Abs(_normal.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
                _bitangent = Vector3.Cross(_normal, _tangent);
                _queue.Clear();

                _pending.Clear();
                _placedTriangles.Clear();
                foreach (int t in Members(seed)) {
                    for (int k = 0; k < 3; k++)
                        _pending.TryAdd(corners[t * 3 + k], Project(corners[t * 3 + k]));
                    _placedTriangles.Add(t);
                }
                Commit();

                while (_queue.Count > 0) {
                    (int t, int from) = _queue.Dequeue();
                    if (chartOf[t] < 0 && TryGroup(t, from))
                        Commit();
                }

                return _chart;
            }

            private IEnumerable<int> Members(int t) =>
                groups != null ? groupTriangles[groups[t]] : Enumerable.Repeat(t, 1);

            private Vector2 Project(int weld) {
                Vector3 position = weldPositions[weld];
                return new Vector2(Vector3.Dot(position, _tangent), Vector3.Dot(position, _bitangent));
            }

            private bool TryPoint(int weld, out Vector2 point) =>
                _chart.points.TryGetValue(weld, out point) || _pending.TryGetValue(weld, out point);

            private bool TryGroup(int t, int from) {
                _pending.Clear();
                _placedTriangles.Clear();

                if (!TryTriangle(t, from))
                    return false;

                if (groups == null)
                    return true;

                List<int> remaining = groupTriangles[groups[t]].Where(member => member != t).ToList();
                bool progress = true;
                while (remaining.Count > 0 && progress) {
                    progress = false;
                    for (int i = 0; i < remaining.Count && !progress; i++) {
                        int member = remaining[i];
                        int mate = -1;
                        foreach (int placed in _placedTriangles)
                            if (SharesEdge(placed, member)) {
                                mate = placed;
                                break;
                            }

                        if (!TryTriangle(member, mate))
                            continue;

                        remaining.RemoveAt(i);
                        progress = true;
                    }
                }

                return remaining.Count == 0;
            }

            private bool TryTriangle(int t, int from) {
                bool degenerate = areas[t] <= DegenerateArea;
                bool coplanar = false;

                if (!degenerate) {
                    float chartDot = Vector3.Dot(normals[t], _normal);
                    if (chartDot < ChartAngleCos)
                        return false;

                    coplanar = chartDot > CoplanarCos &&
                               Mathf.Abs(Vector3.Dot(_normal, weldPositions[corners[t * 3]]) - _planeDistance) < CoplanarDistance;
                }

                if (coplanar) {
                    for (int k = 0; k < 3; k++)
                        _placed[k] = Project(corners[t * 3 + k]);
                }
                else {
                    if (from < 0 || !degenerate && Vector3.Dot(normals[t], normals[from]) < HardAngleCos ||
                        !Unfold(t, from) || !degenerate && Overlaps(_placed, _chart, corners))
                        return false;
                }

                for (int k = 0; k < 3; k++)
                    if (TryPoint(corners[t * 3 + k], out Vector2 existing) &&
                        (existing - _placed[k]).sqrMagnitude >= PlacementTolerance * PlacementTolerance)
                        return false;

                for (int k = 0; k < 3; k++)
                    _pending.TryAdd(corners[t * 3 + k], _placed[k]);
                _placedTriangles.Add(t);
                return true;
            }

            private void Commit() {
                foreach (KeyValuePair<int, Vector2> point in _pending)
                    _chart.points.TryAdd(point.Key, point.Value);

                foreach (int t in _placedTriangles) {
                    chartOf[t] = _chartIndex;
                    _chart.triangles.Add(t);
                }

                foreach (int t in _placedTriangles)
                    for (int k = 0; k < 3; k++) {
                        int weld = corners[t * 3 + k];
                        foreach (int other in edgeTriangles[EdgeKey(weld, corners[t * 3 + (k + 1) % 3])])
                            if (chartOf[other] < 0)
                                _queue.Enqueue((other, t));
                        foreach (int other in weldTriangles[weld])
                            if (chartOf[other] < 0)
                                _queue.Enqueue((other, -1));
                    }
            }

            private bool SharesEdge(int a, int b) {
                int shared = 0;
                for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    if (corners[a * 3 + i] == corners[b * 3 + j])
                        shared++;
                return shared >= 2;
            }

            private bool Unfold(int t, int from) {
                for (int k = 0; k < 3; k++) {
                    int a = corners[t * 3 + k];
                    int b = corners[t * 3 + (k + 1) % 3];
                    int c = corners[t * 3 + (k + 2) % 3];

                    int opposite = -1;
                    bool hasA = false, hasB = false;
                    for (int j = 0; j < 3; j++) {
                        int weld = corners[from * 3 + j];
                        if (weld == a) hasA = true;
                        else if (weld == b) hasB = true;
                        else opposite = weld;
                    }

                    if (!hasA || !hasB || opposite < 0)
                        continue;

                    if (!TryPoint(a, out Vector2 a2) || !TryPoint(b, out Vector2 b2) || !TryPoint(opposite, out Vector2 opposite2))
                        return false;

                    Vector3 edge = weldPositions[b] - weldPositions[a];
                    float length = edge.magnitude;
                    Vector2 direction = b2 - a2;
                    if (length < 1e-6f || direction.sqrMagnitude < 1e-12f)
                        return false;

                    direction.Normalize();
                    Vector3 toC = weldPositions[c] - weldPositions[a];
                    float along = Vector3.Dot(toC, edge) / length;
                    float height = (toC - edge * (along / length)).magnitude;
                    Vector2 perpendicular = new(-direction.y, direction.x);
                    float side = Cross(direction, opposite2 - a2) > 0f ? -1f : 1f;

                    _placed[k] = a2;
                    _placed[(k + 1) % 3] = b2;
                    _placed[(k + 2) % 3] = a2 + direction * along + perpendicular * (height * side);
                    return true;
                }

                return false;
            }
        }

        private static bool Overlaps(Vector2[] triangle, Chart chart, int[] corners) {
            Vector2 min = Vector2.Min(triangle[0], Vector2.Min(triangle[1], triangle[2]));
            Vector2 max = Vector2.Max(triangle[0], Vector2.Max(triangle[1], triangle[2]));
            Vector2[] other = new Vector2[3];

            foreach (int t in chart.triangles) {
                for (int k = 0; k < 3; k++)
                    other[k] = chart.points[corners[t * 3 + k]];

                Vector2 otherMin = Vector2.Min(other[0], Vector2.Min(other[1], other[2]));
                Vector2 otherMax = Vector2.Max(other[0], Vector2.Max(other[1], other[2]));
                if (otherMin.x >= max.x || otherMin.y >= max.y || otherMax.x <= min.x || otherMax.y <= min.y)
                    continue;

                if (!Separated(triangle, other) && !Separated(other, triangle))
                    return true;
            }

            return false;
        }

        private static bool Separated(Vector2[] a, Vector2[] b) {
            const float epsilon = 1e-5f;
            for (int k = 0; k < 3; k++) {
                Vector2 edge = a[(k + 1) % 3] - a[k];
                Vector2 axis = new(-edge.y, edge.x);
                if (axis.sqrMagnitude < 1e-12f)
                    continue;

                axis.Normalize();
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                for (int j = 0; j < 3; j++) {
                    float projectedA = Vector2.Dot(a[j], axis);
                    float projectedB = Vector2.Dot(b[j], axis);
                    minA = Mathf.Min(minA, projectedA);
                    maxA = Mathf.Max(maxA, projectedA);
                    minB = Mathf.Min(minB, projectedB);
                    maxB = Mathf.Max(maxB, projectedB);
                }

                if (maxA <= minB + epsilon || maxB <= minA + epsilon)
                    return true;
            }

            return false;
        }

        private static void Orient(Chart chart, int[] corners) {
            List<(float length, float angle)> edges = new();
            foreach (int t in chart.triangles)
                for (int k = 0; k < 3; k++) {
                    Vector2 edge = chart.points[corners[t * 3 + (k + 1) % 3]] - chart.points[corners[t * 3 + k]];
                    edges.Add((edge.sqrMagnitude, Mathf.Repeat(Mathf.Atan2(edge.y, edge.x), Mathf.PI * 0.5f)));
                }

            List<float> candidates = new() { 0f };
            foreach ((float _, float angle) in edges.OrderByDescending(edge => edge.length)) {
                if (candidates.Count >= MaxRotationCandidates)
                    break;
                if (candidates.All(existing => Mathf.Abs(existing - angle) > 0.01f))
                    candidates.Add(angle);
            }

            List<int> keys = chart.points.Keys.ToList();
            float bestArea = float.MaxValue;
            float bestAngle = 0f;
            foreach (float angle in candidates) {
                Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                foreach (int key in keys) {
                    Vector2 rotated = Rotate(chart.points[key], -angle);
                    min = Vector2.Min(min, rotated);
                    max = Vector2.Max(max, rotated);
                }

                float area = (max.x - min.x) * (max.y - min.y);
                if (area < bestArea - 1e-9f) {
                    bestArea = area;
                    bestAngle = angle;
                }
            }

            Vector2 chartMin = Vector2.positiveInfinity, chartMax = Vector2.negativeInfinity;
            foreach (int key in keys) {
                Vector2 rotated = Rotate(chart.points[key], -bestAngle);
                chart.points[key] = rotated;
                chartMin = Vector2.Min(chartMin, rotated);
                chartMax = Vector2.Max(chartMax, rotated);
            }

            chart.min = chartMin;
            chart.size = chartMax - chartMin;
            if (chart.size.y > chart.size.x) {
                foreach (int key in keys) {
                    Vector2 point = chart.points[key];
                    chart.points[key] = new Vector2(-point.y, point.x);
                }
                chart.min = new Vector2(-chartMax.y, chartMin.x);
                chart.size = new Vector2(chart.size.y, chart.size.x);
            }
        }

        public static float LightmapPadding(Renderer renderer) {
            float texelsPerUnit = DefaultLightmapResolution;
#if UNITY_EDITOR
            try {
                texelsPerUnit = Lightmapping.lightingSettings.lightmapResolution;
            }
            catch (System.Exception) {
            }

            if (renderer is MeshRenderer meshRenderer)
                texelsPerUnit *= meshRenderer.scaleInLightmap;
#endif
            return LightmapPaddingTexels / Mathf.Max(texelsPerUnit, 0.01f);
        }

        private const float DefaultLightmapResolution = 40f;
        private const float LightmapPaddingTexels = 2f;

        private static float Pack(List<Chart> charts, float padding) {
            Vector2[] positions = new Vector2[charts.Count];
            float scale = PackRects(charts.Select(chart => chart.size).ToList(), padding, positions);
            for (int i = 0; i < charts.Count; i++)
                charts[i].offset = positions[i] - charts[i].min;
            return scale;
        }

        public static float PackRects(IList<Vector2> sizes, float padding, Vector2[] positions) {
            List<int> sorted = Enumerable.Range(0, sizes.Count).OrderByDescending(i => sizes[i].y).ToList();
            float totalArea = 0f, widest = 0f;
            foreach (int i in sorted) {
                Vector2 padded = sizes[i] + Vector2.one * (padding * 2f);
                totalArea += padded.x * padded.y;
                widest = Mathf.Max(widest, padded.x);
            }

            float width = Mathf.Max(Mathf.Sqrt(totalArea), widest);
            float usedWidth = 0f, height = 0f;
            for (int iteration = 0; iteration < 64; iteration++) {
                float x = 0f, y = 0f, row = 0f;
                usedWidth = 0f;
                foreach (int i in sorted) {
                    Vector2 padded = sizes[i] + Vector2.one * (padding * 2f);
                    if (x > 0f && x + padded.x > width) {
                        y += row;
                        x = 0f;
                        row = 0f;
                    }

                    positions[i] = new Vector2(x + padding, y + padding);
                    x += padded.x;
                    row = Mathf.Max(row, padded.y);
                    usedWidth = Mathf.Max(usedWidth, x);
                }

                height = y + row;
                if (height <= width * 1.05f)
                    break;
                width *= 1.05f;
            }

            float side = Mathf.Max(usedWidth, height);
            return side > 0f ? 1f / side : 1f;
        }

        private static bool ContainsCorners(Chart chart, int[] corners, int t) =>
            chart.points.ContainsKey(corners[t * 3]) && chart.points.ContainsKey(corners[t * 3 + 1]) &&
            chart.points.ContainsKey(corners[t * 3 + 2]);

        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2 Rotate(Vector2 point, float angle) {
            float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
            return new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
        }
    }
}
