using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderCutter))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderCutterEditor : UnityEditor.Editor, ITsitskiPathSplitter {
        bool ITsitskiPathSplitter.CanSplitPath() =>
            _path != null && target is TsitskiProBuilderCutter cutter && !cutter.stats && cutter.pathPoints != null &&
            TsitskiPathSplit.CanSplitPieces(cutter.pathPoints.Count, false, _path.selected, 1, 2);

        void ITsitskiPathSplitter.SplitPath() {
            TsitskiProBuilderCutter cutter = (TsitskiProBuilderCutter)target;
            List<Vector3> points = new(cutter.pathPoints);
            List<float> radii = PointRounding.Synced(cutter.pathRadii, points.Count);
            List<Quaternion> rotations = new(cutter.pathRotations ?? new List<Quaternion>());
            void Assign(TsitskiProBuilderCutter owner, List<int> indices) {
                owner.pathPoints = TsitskiPathSplit.Pick(points, indices);
                owner.pathRadii = TsitskiPathSplit.PieceRadii(indices, radii);
                owner.pathRotations = TsitskiPathSplit.Pick(rotations, indices, Quaternion.identity);
                owner.NotifyChanged();
            }

            TsitskiPathSplit.SplitPieces(cutter, points.Count, false, _path.selected.ToList(), 1, 2, TsitskiPathSplit.Duplicate, Assign, Assign, _ => { });
            _path.selected.Clear();
        }

        private static readonly Color HandleColor = new(1f, 0.4f, 0.1f, 1f);
        private static readonly int RectSelectHash = "ProBuilderCutterRectSelect".GetHashCode();
        private static readonly int PathRectSelectHash = "ProBuilderCutterPathRectSelect".GetHashCode();

        private static readonly ProBuilderCutterShape[] ShapeOrder = {
            ProBuilderCutterShape.Box, ProBuilderCutterShape.Cylinder, ProBuilderCutterShape.Polygon,
            ProBuilderCutterShape.Path, ProBuilderCutterShape.Explosion, ProBuilderCutterShape.Mesh
        };

        private readonly BoxBoundsHandle _boxHandle = new();
        private PolygonPointHandles _polygon;
        private PathPointHandles _path;
        private Vector2 _rectStart;
        private Vector2 _rectEnd;

        private void OnEnable() {
            _polygon = new PolygonPointHandles(Repaint);
            _path = new PathPointHandles(Repaint);
        }

        private void OnDisable() => Tools.hidden = false;

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty shape = serializedObject.FindProperty(nameof(TsitskiProBuilderCutter.shape));
            bool mixed = shape.hasMultipleDifferentValues;
            ProBuilderCutterShape value = (ProBuilderCutterShape)shape.intValue;

            DrawProperties(serializedObject);

            serializedObject.ApplyModifiedProperties();

            if (!mixed && value == ProBuilderCutterShape.Explosion && GUILayout.Button("Randomize Explosion")) {
                foreach (Object t in targets) {
                    TsitskiProBuilderCutter c = (TsitskiProBuilderCutter)t;
                    Undo.RecordObject(c, "Randomize Explosion");
                    c.explosionSeed = Random.Range(int.MinValue, int.MaxValue);
                    c.NotifyChanged();
                    PushStats(c, "Randomize Explosion");
                }
            }

            if (!serializedObject.isEditingMultipleObjects) {
                TsitskiProBuilderCutter single = (TsitskiProBuilderCutter)target;
                using (new EditorGUILayout.HorizontalScope()) {
                    if (!single.stats && GUILayout.Button("Save As Cutter Stats…"))
                        SaveStats(single);
                    if (single.stats && GUILayout.Button("Copy Stats Into Cutter")) {
                        Undo.RecordObject(single, "Copy Cutter Stats");
                        single.PullStats();
                        single.stats = null;
                        single.NotifyChanged();
                    }
                }
            }

            if (serializedObject.isEditingMultipleObjects)
                return;

            TsitskiProBuilderCutter cutter = (TsitskiProBuilderCutter)target;
            DrawProfile(CutterShapeSettings.From(cutter), cutter.pathPoints, cutter.pathRadii,
                path => EditCutter(cutter, "Edit Cutter Path", () => cutter.pathPoints = path),
                polygon => EditCutter(cutter, "Edit Cutter Polygon", () => cutter.polygonPoints = polygon),
                radii => EditCutter(cutter, "Cutter Path Radius", () => cutter.pathRadii = radii),
                radii => EditCutter(cutter, "Cutter Polygon Radius", () => cutter.polygonRadii = radii));

            if (value != ProBuilderCutterShape.Polygon)
                return;

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField($"Selected Points: {_polygon.selected.Count}");
                if (GUILayout.Button("Select All") && cutter.polygonPoints != null)
                    _polygon.SelectAll(cutter.polygonPoints.Count);
                if (GUILayout.Button("Clear"))
                    _polygon.Clear();
            }
        }

        internal static void DrawProperties(SerializedObject serializedObject, params string[] hidden) {
            SerializedProperty shape = serializedObject.FindProperty(nameof(TsitskiProBuilderCutter.shape));
            bool mixed = shape.hasMultipleDifferentValues;
            ProBuilderCutterShape value = (ProBuilderCutterShape)shape.intValue;
            SerializedProperty createMesh = serializedObject.FindProperty(nameof(TsitskiProBuilderCutter.createMesh));
            bool noMesh = !createMesh.hasMultipleDifferentValues && !createMesh.boolValue;
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderCutter.stats));
            bool usesStats = statsProperty is { hasMultipleDifferentValues: false } && statsProperty.objectReferenceValue;

            TsitskiInspector.Headers headers = new();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                headers.Next(property);
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (usesStats && property.name != nameof(TsitskiProBuilderCutter.stats))
                    continue;

                if (hidden.Contains(property.name) || !mixed && HiddenForShape(property.name, value))
                    continue;

                if (noMesh && property.name is nameof(TsitskiProBuilderCutter.generateFaces) or nameof(TsitskiProBuilderCutter.clipToTunnel)
                        or nameof(TsitskiProBuilderCutter.revealMaterial) or nameof(TsitskiProBuilderCutter.facesOn)
                        or nameof(TsitskiProBuilderCutter.smoothingAngle))
                    continue;

                if (property.name == nameof(TsitskiProBuilderCutter.shape)) {
                    headers.BeforeCustomField();
                    ShapePopup(property);
                    continue;
                }

                if (TsitskiInspector.HiddenByCondition(property))
                    continue;

                headers.BeforePropertyField(property);
                EditorGUILayout.PropertyField(property, true);
            }
        }

        private static bool HiddenForShape(string name, ProBuilderCutterShape shape) => name switch {
            nameof(TsitskiProBuilderCutter.center) or nameof(TsitskiProBuilderCutter.size) => shape is ProBuilderCutterShape.Path or ProBuilderCutterShape.Mesh,
            nameof(TsitskiProBuilderCutter.shapeMesh) => shape != ProBuilderCutterShape.Mesh,
            nameof(TsitskiProBuilderCutter.pathPoints) or nameof(TsitskiProBuilderCutter.pathRadii) or nameof(TsitskiProBuilderCutter.pathWidth)
                or nameof(TsitskiProBuilderCutter.pathHeight) => shape != ProBuilderCutterShape.Path,
            nameof(TsitskiProBuilderCutter.pathRotations) => true,
            nameof(TsitskiProBuilderCutter.cornerRadius) => shape != ProBuilderCutterShape.Box,
            nameof(TsitskiProBuilderCutter.cornerSegments) => shape is ProBuilderCutterShape.Cylinder or ProBuilderCutterShape.Explosion,
            nameof(TsitskiProBuilderCutter.cylinderSegments) => shape != ProBuilderCutterShape.Cylinder,
            nameof(TsitskiProBuilderCutter.explosionSeed) or nameof(TsitskiProBuilderCutter.explosionSpikes) or nameof(TsitskiProBuilderCutter.explosionMinorSpikes)
                or nameof(TsitskiProBuilderCutter.explosionJaggedness) => shape != ProBuilderCutterShape.Explosion,
            nameof(TsitskiProBuilderCutter.polygonPoints) or nameof(TsitskiProBuilderCutter.polygonRadii) => shape != ProBuilderCutterShape.Polygon,
            _ => false
        };

        private static void EditCutter(TsitskiProBuilderCutter cutter, string undoName, System.Action change) {
            Undo.RecordObject(cutter, undoName);
            change();
            cutter.NotifyChanged();
            PushStats(cutter, undoName);
        }

        internal static void DrawProfile(CutterShapeSettings settings, List<Vector3> pathPoints, List<float> pathRadii,
            System.Action<List<Vector3>> setPath = null, System.Action<List<Vector2>> setPolygon = null, System.Action<List<float>> setPathRadii = null,
            System.Action<List<float>> setPolygonRadii = null) {
            ProfileLine line;
            GraphPoints editable = null;
            if (settings.shape == ProBuilderCutterShape.Path) {
                List<Vector3> rounded = pathPoints is { Count: > 1 }
                    ? PointRounding.Round(pathPoints, pathRadii, false, settings.cornerSegments)
                    : new List<Vector3>();
                line = new ProfileLine { points = rounded.Select(p => new Vector2(p.x, p.z)).ToList(), color = ProfilePreview.Outline };
                if (setPath != null && pathPoints != null)
                    editable = new GraphPoints {
                        points = pathPoints.Select(p => new Vector2(p.x, p.z)).ToList(),
                        apply = edited => setPath(edited.Select((p, i) => new Vector3(p.x, i < pathPoints.Count ? pathPoints[i].y : 0f, p.y)).ToList()),
                        radii = pathRadii,
                        applyRadii = setPathRadii,
                        deleteKeeping = keep => {
                            List<float> synced = PointRounding.Synced(pathRadii, pathPoints.Count);
                            setPath(keep.Select(i => pathPoints[i]).ToList());
                            setPathRadii?.Invoke(keep.Select(i => synced[i]).ToList());
                        }
                    };
            }
            else {
                Vector2 size = new(Mathf.Abs(settings.size.x), Mathf.Abs(settings.size.y));
                line = new ProfileLine {
                    points = CutterOutline.Build(settings).Select(p => Vector2.Scale(p, size)).ToList(),
                    color = ProfilePreview.Outline,
                    closed = true
                };
                if (setPolygon != null && settings.shape == ProBuilderCutterShape.Polygon && settings.polygonPoints != null) {
                    Vector2 safe = Vector2.Max(size, Vector2.one * 1e-4f);
                    editable = new GraphPoints {
                        points = settings.polygonPoints,
                        toGraph = p => Vector2.Scale(p, safe),
                        fromGraph = (p, _) => new Vector2(Mathf.Clamp(p.x / safe.x, -0.5f, 0.5f), Mathf.Clamp(p.y / safe.y, -0.5f, 0.5f)),
                        apply = setPolygon,
                        radii = settings.polygonRadii,
                        applyRadii = setPolygonRadii,
                        minimumPoints = 3
                    };
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                ProfilePreview.Graph(new List<ProfileLine> { line }, null, 140f, editable == null ? null : new[] { editable });
        }

        private static void PushStats(TsitskiProBuilderCutter cutter, string undoName) {
            if (!cutter.stats)
                return;

            Undo.RecordObject(cutter.stats, undoName);
            cutter.PushStats();
            EditorUtility.SetDirty(cutter.stats);
        }

        private static void SaveStats(TsitskiProBuilderCutter cutter) {
            string path = StatsSavePanel.Show("Save Cutter Stats", "Cutter Stats",
                "Save this cutter's settings as a reusable Cutter Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            TsitskiProBuilderCutterStats asset = CreateInstance<TsitskiProBuilderCutterStats>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(cutter), asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(cutter, "Assign Cutter Stats");
            cutter.stats = asset;
            EditorUtility.SetDirty(cutter);
        }

        internal static void ShapePopup(SerializedProperty property) {
            int current = System.Array.IndexOf(ShapeOrder, (ProBuilderCutterShape)property.intValue);
            string[] names = System.Array.ConvertAll(ShapeOrder, s => ObjectNames.NicifyVariableName(s.ToString()));

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int chosen = EditorGUILayout.Popup(new GUIContent(property.displayName, property.tooltip), current, names.Select(n => new GUIContent(n)).ToArray());
            if (EditorGUI.EndChangeCheck() && chosen >= 0)
                property.intValue = (int)ShapeOrder[chosen];
            EditorGUI.showMixedValue = false;
        }

        private void OnSceneGUI() {
            TsitskiProBuilderCutter cutter = (TsitskiProBuilderCutter)target;
            if (!cutter)
                return;

            if (Selection.gameObjects.Length != 1) {
                _polygon.selected.Clear();
                _path.selected.Clear();
                Tools.hidden = false;
                return;
            }

            if (cutter.shape == ProBuilderCutterShape.Path) {
                _polygon.selected.Clear();
                if (cutter.pathPoints != null)
                    PathHandles(cutter);
                Tools.hidden = _path.selected.Count > 0;
                return;
            }

            if (cutter.shape == ProBuilderCutterShape.Mesh) {
                _polygon.selected.Clear();
                _path.selected.Clear();
                Tools.hidden = false;
                return;
            }

            _path.selected.Clear();

            using (new Handles.DrawingScope(Color.white, cutter.transform.localToWorldMatrix)) {
                _boxHandle.center = cutter.center;
                _boxHandle.size = cutter.size;
                _boxHandle.wireframeColor = cutter.shape == ProBuilderCutterShape.Box ? Color.clear : new Color(1f, 0.4f, 0.1f, 0.25f);
                _boxHandle.handleColor = HandleColor;

                EditorGUI.BeginChangeCheck();
                _boxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(cutter, "Resize ProBuilder Cutter");
                    cutter.SetBox(_boxHandle.center, _boxHandle.size);
                    PushStats(cutter, "Resize ProBuilder Cutter");
                }
            }

            bool editingPolygon = cutter.shape == ProBuilderCutterShape.Polygon && cutter.polygonPoints != null;

            if (editingPolygon) {
                Matrix4x4 plane = cutter.transform.localToWorldMatrix *
                    Matrix4x4.Translate(new Vector3(0f, 0f, cutter.center.z + cutter.size.z * 0.5f));

                using (new Handles.DrawingScope(Color.white, plane))
                    PolygonHandles(cutter);
            }
            else {
                _polygon.selected.Clear();
            }

            Tools.hidden = editingPolygon && _polygon.selected.Count > 0;
        }

        private void PolygonHandles(TsitskiProBuilderCutter cutter) {
            List<Vector2> local = LocalPoints(cutter);
            PointEdit edit = _polygon.Do(local, cutter.polygonRadii);

            if (edit != null) {
                Undo.RecordObject(cutter, $"{edit.undoName} (Cutter)");
                cutter.polygonRadii = edit.radii;
                ApplyLocalPoints(cutter, edit.points);
                PushStats(cutter, $"{edit.undoName} (Cutter)");
                return;
            }

            if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                _polygon.SelectInRect(rect, local);
        }

        private void PathHandles(TsitskiProBuilderCutter cutter) {
            using (new Handles.DrawingScope(Color.white, cutter.transform.localToWorldMatrix)) {
                PathEdit edit = _path.Do(cutter.pathPoints, cutter.pathRadii, false, cutter.transform.rotation, cutter.pathRotations);
                if (edit != null) {
                    Undo.RecordObject(cutter, $"{edit.undoName} (Cutter)");
                    cutter.pathPoints = edit.points;
                    cutter.pathRadii = edit.radii;
                    cutter.pathRotations = edit.rotations;
                    cutter.NotifyChanged();
                    PushStats(cutter, $"{edit.undoName} (Cutter)");
                    return;
                }

                if (SceneRectSelection.Handle(PathRectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    _path.SelectInRect(rect, cutter.pathPoints);
            }
        }

        private static List<Vector2> LocalPoints(TsitskiProBuilderCutter cutter) {
            List<Vector2> local = new();
            foreach (Vector2 p in cutter.polygonPoints)
                local.Add(new Vector2(cutter.center.x + p.x * cutter.size.x, cutter.center.y + p.y * cutter.size.y));
            return local;
        }

        private static void ApplyLocalPoints(TsitskiProBuilderCutter cutter, List<Vector2> local) {
            Vector2 min = local[0], max = local[0];
            foreach (Vector2 p in local) {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Vector2 extent = Vector2.Max(max - min, Vector2.one * 1e-4f);
            Vector2 middle = (min + max) * 0.5f;

            List<Vector2> normalized = new();
            foreach (Vector2 p in local)
                normalized.Add(new Vector2((p.x - middle.x) / extent.x, (p.y - middle.y) / extent.y));

            cutter.polygonPoints = normalized;
            cutter.center = new Vector3(middle.x, middle.y, cutter.center.z);
            cutter.size = new Vector3(extent.x, extent.y, cutter.size.z);
            cutter.NotifyChanged();
        }
    }
}

