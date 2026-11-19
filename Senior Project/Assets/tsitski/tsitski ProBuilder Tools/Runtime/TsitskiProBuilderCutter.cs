using System;
using System.Collections.Generic;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("tsitski ProBuilder Tools/tsitski ProBuilder Cutter")]
    [ExecuteAlways]
    public class TsitskiProBuilderCutter : MonoBehaviour {
        [Tooltip(Tooltips.CutterStats)] public TsitskiProBuilderCutterStats stats;

        [Header("Shape")]
        [Tooltip(Tooltips.Shape)] public ProBuilderCutterShape shape = ProBuilderCutterShape.Box;
        [Tooltip(Tooltips.CornerRadius)] public BoxCorners cornerRadius;
        [Tooltip(Tooltips.CornerSegments)] [Min(1)] public int cornerSegments = 8;
        [Tooltip(Tooltips.CylinderSegments)] [Min(3)] public int cylinderSegments = 16;
        [Tooltip(Tooltips.ExplosionSeed)] public int explosionSeed = 1;
        [Tooltip(Tooltips.ExplosionSpikes)] [Min(3)] public int explosionSpikes = 12;
        [Tooltip(Tooltips.ExplosionMinorSpikes)] [Min(0)] public int explosionMinorSpikes = 3;
        [Tooltip(Tooltips.ExplosionJaggedness)] [Range(0f, 1f)] public float explosionJaggedness = 0.6f;
        [Tooltip(Tooltips.PolygonPoints)] public List<Vector2> polygonPoints = new() {
            new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f)
        };
        [Tooltip(Tooltips.Radii)] public List<float> polygonRadii = new();
        [Tooltip(Tooltips.CutterPath)] public List<Vector3> pathPoints = new() { Vector3.zero, new(0f, 0f, 2f) };
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.PathRotations)] public List<Quaternion> pathRotations = new();
        [Tooltip(Tooltips.PathWidth)] [Min(0.01f)] public float pathWidth = 1f;
        [Tooltip(Tooltips.PathHeight)] [Min(0.01f)] public float pathHeight = 2f;
        [Tooltip(Tooltips.CutterShapeMesh)] public Mesh shapeMesh;

        [Header("Bounds")]
        [Tooltip(Tooltips.CutterCenter)] public Vector3 center;
        [Tooltip(Tooltips.CutterSize)] public Vector3 size = Vector3.one;

        [Header("Cutting")]
        [Tooltip(Tooltips.CutMode)] public ProBuilderCutMode cutMode = ProBuilderCutMode.CutProBuilder;
        [Tooltip(Tooltips.CutThroughParent)] public bool cutThroughParent = true;
        [Tooltip(Tooltips.IgnorePrefabMeshes)] public bool ignorePrefabMeshes = false;

        [Header("Faces")]
        [Tooltip(Tooltips.CreateMesh)] public bool createMesh = true;
        [Tooltip(Tooltips.FacesOn)] public ProBuilderCutFaces facesOn = ProBuilderCutFaces.First;
        [Tooltip(Tooltips.GenerateFaces)] public AxisToggles generateFaces = new(true, true, true);
        [Tooltip(Tooltips.ClipToTunnel)] public AxisToggles clipToTunnel = new(false, false, false);
        [Tooltip(Tooltips.RevealMaterial)] public Material revealMaterial;
        [Tooltip(Tooltips.CutterSmoothingAngle)] [Range(0f, 89f)] public float smoothingAngle = 30f;
        [SerializeField] [HideInInspector] internal MeshFilter facesFilter;

        public bool clipsToTunnel => clipToTunnel.x || clipToTunnel.y || clipToTunnel.z;
        public MeshFilter generatedFaces => facesFilter;
        public bool cutsProBuilder => cutMode != ProBuilderCutMode.CutMesh;
        public bool cutsMeshes => cutMode != ProBuilderCutMode.CutProBuilder;

        public static event Action<TsitskiProBuilderCutter> Changed;

        [NonSerialized] private Mesh _gizmoMesh;
        [NonSerialized] private int _gizmoHash;
        [NonSerialized] private Vector3[] _gizmoLines;
        [NonSerialized] internal CutterVolume.Tube pathTubeCache;
        [NonSerialized] internal int pathFaceHash;

        public Matrix4x4 boxMatrix => shape is ProBuilderCutterShape.Path or ProBuilderCutterShape.Mesh
            ? transform.localToWorldMatrix
            : transform.localToWorldMatrix * Matrix4x4.TRS(center, Quaternion.identity, size);

        public Bounds worldBounds {
            get {
                Matrix4x4 matrix = boxMatrix;
                Bounds local = CutterVolume.LocalBounds(this);
                Bounds bounds = new(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                    bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f))));
                return bounds;
            }
        }

        public void SetBox(Vector3 newCenter, Vector3 newSize) {
            if (center == newCenter && size == newSize)
                return;

            center = newCenter;
            size = newSize;
            Changed?.Invoke(this);
        }

        public void NotifyChanged() => Changed?.Invoke(this);

        public void SetMesh(Mesh mesh) {
            shape = ProBuilderCutterShape.Mesh;
            shapeMesh = mesh;
            pathTubeCache = null;
            NotifyChanged();
        }

        public void PullStats() {
            if (stats)
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(stats), this);
        }

        public void PushStats() {
            if (!stats)
                return;

            TsitskiProBuilderCutterStats target = stats;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), target);
            target.NotifyChanged();
        }

        private void OnEnable() {
            TsitskiProBuilderCutterStats.Changed += OnStatsChanged;
            PullStats();
            Changed?.Invoke(this);
        }

        private void OnDisable() {
            TsitskiProBuilderCutterStats.Changed -= OnStatsChanged;
            Changed?.Invoke(this);
        }

        private void OnStatsChanged(TsitskiProBuilderCutterStats changed) {
            if (changed != stats)
                return;

            PullStats();
            Changed?.Invoke(this);
            QueueAutoName();
        }

        private void OnDestroy() {
            if (_gizmoMesh)
                DestroyImmediate(_gizmoMesh);
        }

        private void OnValidate() {
            PullStats();
            Changed?.Invoke(this);
            QueueAutoName();
        }

        private void QueueAutoName() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => TsitskiSettings.AutoName(this, stats, "ProBuilder Cutter");
#endif
        }

        private void Start() {
        }

        private void OnDrawGizmos() {
            if (!TsitskiSettings.GizmosOnlyOnSelected)
                DrawGizmo();
        }

        private void OnDrawGizmosSelected() {
            if (TsitskiSettings.GizmosOnlyOnSelected)
                DrawGizmo();
        }

        private void DrawGizmo() {
            if (shape == ProBuilderCutterShape.Box && !cornerRadius.any) {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.06f);
                Gizmos.DrawCube(center, size);
                Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
                Gizmos.DrawWireCube(center, size);
                return;
            }

            Gizmos.matrix = boxMatrix;

            int hash = ShapeHash();
            if (!_gizmoMesh || _gizmoLines == null || hash != _gizmoHash) {
                RebuildGizmo();
                _gizmoHash = hash;
            }

            if (_gizmoMesh.vertexCount > 0) {
                Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.06f);
                Gizmos.DrawMesh(_gizmoMesh);
            }

            if (_gizmoLines.Length > 0) {
                Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
                UnityCompat.DrawGizmoLineList(_gizmoLines);
            }
        }

        public int ShapeHash() {
            StableHash hash = new();
            hash.Add((int)shape);
            hash.Add(size);
            hash.Add(cornerRadius.topLeft);
            hash.Add(cornerRadius.topRight);
            hash.Add(cornerRadius.bottomLeft);
            hash.Add(cornerRadius.bottomRight);
            hash.Add(cornerSegments);
            hash.Add(cylinderSegments);
            hash.Add(explosionSeed);
            hash.Add(explosionSpikes);
            hash.Add(explosionMinorSpikes);
            hash.Add(explosionJaggedness);
            hash.Add(pathWidth);
            hash.Add(pathHeight);
            hash.Add(polygonPoints);
            hash.Add(polygonRadii);
            hash.Add(pathPoints);
            hash.Add(pathRadii);
            hash.Add(pathRotations);
            hash.Add(CutterVolume.MeshHash(shapeMesh));
            return hash.ToHashCode();
        }

        private void RebuildGizmo() {
            RebuildGizmoMesh(CutterVolume.Faces(this));

            List<Vector3> lines = new();

            if (shape == ProBuilderCutterShape.Mesh) {
                foreach (CutterFace face in CutterVolume.Faces(this))
                    for (int k = 0; k < face.corners.Count; k++) {
                        lines.Add(face.corners[k]);
                        lines.Add(face.corners[(k + 1) % face.corners.Count]);
                    }
            }
            else if (shape == ProBuilderCutterShape.Path) {
                IReadOnlyList<Vector3[]> sections = CutterVolume.PathSections(this);
                for (int i = 0; i < sections.Count; i++) {
                    Vector3[] section = sections[i];
                    for (int k = 0; k < 4; k++) {
                        if (i == 0 || i == sections.Count - 1) {
                            lines.Add(section[k]);
                            lines.Add(section[(k + 1) % 4]);
                        }

                        if (i < sections.Count - 1) {
                            lines.Add(section[k]);
                            lines.Add(sections[i + 1][k]);
                        }
                    }
                }
            }
            else {
                List<Vector2> outline = CutterOutline.Build(this);
                int verticalStep = Mathf.Max(1, outline.Count / 8);

                for (int i = 0; i < outline.Count; i++) {
                    Vector2 a = outline[i];
                    Vector2 b = outline[(i + 1) % outline.Count];
                    lines.Add(new Vector3(a.x, a.y, 0.5f));
                    lines.Add(new Vector3(b.x, b.y, 0.5f));
                    lines.Add(new Vector3(a.x, a.y, -0.5f));
                    lines.Add(new Vector3(b.x, b.y, -0.5f));

                    if (shape == ProBuilderCutterShape.Polygon || i % verticalStep == 0) {
                        lines.Add(new Vector3(a.x, a.y, -0.5f));
                        lines.Add(new Vector3(a.x, a.y, 0.5f));
                    }
                }
            }

            _gizmoLines = lines.ToArray();
        }

        private void RebuildGizmoMesh(List<CutterFace> faces) {
            if (!_gizmoMesh)
                _gizmoMesh = new Mesh { name = "Cutter Gizmo", hideFlags = HideFlags.HideAndDontSave };

            List<Vector3> vertices = new();
            List<int> triangles = new();

            void Face(params Vector3[] corners) {
                int start = vertices.Count;
                vertices.AddRange(corners);
                for (int i = 2; i < corners.Length; i++)
                    triangles.AddRange(new[] { start, start + i - 1, start + i });
            }

            foreach (CutterFace face in faces)
                Face(face.corners.ToArray());

            _gizmoMesh.Clear();
            _gizmoMesh.SetVertices(vertices);
            _gizmoMesh.SetTriangles(triangles, 0);
            _gizmoMesh.RecalculateNormals();
            _gizmoMesh.RecalculateBounds();
        }
    }

    public struct StableHash {
        private const float RoundPrecision = 10000f;

        private uint _value;

        public void Add(int value) {
            unchecked {
                _value = (_value + 0x9E3779B9u + (uint)value) * 0x85EBCA6Bu;
                _value ^= _value >> 13;
            }
        }

        public void Add(bool value) => Add(value ? 1 : 0);

        public void Add(float value) => Add(BitConverter.SingleToInt32Bits(value));

        public void Add(string value) {
            if (value == null) {
                Add(-1);
                return;
            }

            Add(value.Length);
            foreach (char character in value)
                Add(character);
        }

        public void Add(Vector2 value) {
            Add(value.x);
            Add(value.y);
        }

        public void Add(Vector3 value) {
            Add(value.x);
            Add(value.y);
            Add(value.z);
        }

        public void Add(Quaternion value) {
            Add(value.x);
            Add(value.y);
            Add(value.z);
            Add(value.w);
        }

        public void AddRounded(float value) => Add(Mathf.RoundToInt(value * RoundPrecision));

        public void AddRounded(Vector2 value) {
            AddRounded(value.x);
            AddRounded(value.y);
        }

        public void AddRounded(Vector3 value) {
            AddRounded(value.x);
            AddRounded(value.y);
            AddRounded(value.z);
        }

        public void Add(List<Vector2> list) {
            Add(list?.Count ?? -1);
            if (list != null)
                foreach (Vector2 value in list)
                    Add(value);
        }

        public void Add(List<Vector3> list) {
            Add(list?.Count ?? -1);
            if (list != null)
                foreach (Vector3 value in list)
                    Add(value);
        }

        public void Add(List<float> list) {
            Add(list?.Count ?? -1);
            if (list != null)
                foreach (float value in list)
                    Add(value);
        }

        public void Add(List<Quaternion> list) {
            Add(list?.Count ?? -1);
            if (list != null)
                foreach (Quaternion value in list)
                    Add(value);
        }

        public int ToHashCode() => (int)_value;
    }
}
