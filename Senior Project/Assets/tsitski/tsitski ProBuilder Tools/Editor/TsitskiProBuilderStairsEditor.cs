using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderStairs))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderStairsEditor : UnityEditor.Editor, ITsitskiPathSplitter {
        private static readonly int RectSelectHash = "ProBuilderStairsRectSelect".GetHashCode();

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PathPointHandles _path;

        private void OnEnable() => _path = new PathPointHandles(Repaint);

        bool ITsitskiPathSplitter.CanSplitPath() =>
            _path != null && target is TsitskiProBuilderStairs { isSpiral: false } stairs && TsitskiPathSplit.CanSplit(stairs.path.Count, false, _path.selected);

        void ITsitskiPathSplitter.SplitPath() {
            TsitskiProBuilderStairs stairs = (TsitskiProBuilderStairs)target;
            TsitskiPathSplit.Split(stairs, stairs.path, stairs.pathRadii, false, _path.selected.ToList(), TsitskiPathSplit.Duplicate, (owner, path, radii) => {
                owner.path = path;
                owner.pathRadii = radii;
                owner.Rebuild();
                EditorUtility.SetDirty(owner);
            });
            _path.selected.Clear();
        }

        private void OnDisable() {
            Tools.hidden = false;
            SetSuspended(target as TsitskiProBuilderStairs, false);
        }

        private static void SetSuspended(TsitskiProBuilderStairs stairs, bool suspended) {
            if (stairs && stairs.TryGetComponent(out UnityEngine.ProBuilder.ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderStairs.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;

            TsitskiInspector.Headers headers = new();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                headers.Next(property);
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name is nameof(TsitskiProBuilderStairs.width) or nameof(TsitskiProBuilderStairs.stepHeight)
                        or nameof(TsitskiProBuilderStairs.solid) or nameof(TsitskiProBuilderStairs.thickness)
                        or nameof(TsitskiProBuilderStairs.generateCutter) or nameof(TsitskiProBuilderStairs.cutterHeadroom)
                        or nameof(TsitskiProBuilderStairs.leftTrim) or nameof(TsitskiProBuilderStairs.rightTrim)
                        or nameof(TsitskiProBuilderStairs.bottomTrimForwardOffset) or nameof(TsitskiProBuilderStairs.bottomTrimUpwardOffset) or nameof(TsitskiProBuilderStairs.topTrimForwardOffset) or nameof(TsitskiProBuilderStairs.topTrimUpwardOffset)
                        or nameof(TsitskiProBuilderStairs.smoothCollider) or nameof(TsitskiProBuilderStairs.topStepMaterial) or nameof(TsitskiProBuilderStairs.forwardStepMaterial)
                        or nameof(TsitskiProBuilderStairs.sideMaterial) && usesStats)
                    continue;

                if (TsitskiInspector.HiddenByCondition(property))
                    continue;

                if (!serializedObject.isEditingMultipleObjects) {
                    bool spiral = ((TsitskiProBuilderStairs)target).isSpiral;
                    if (spiral && property.name is nameof(TsitskiProBuilderStairs.path) or nameof(TsitskiProBuilderStairs.pathRadii))
                        continue;
                    if (!spiral && property.name is nameof(TsitskiProBuilderStairs.spiralHeight) or nameof(TsitskiProBuilderStairs.spiralRadius)
                            or nameof(TsitskiProBuilderStairs.spiralAngle) or nameof(TsitskiProBuilderStairs.spiralClockwise))
                        continue;
                }

                headers.BeforePropertyField(property);
                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (serializedObject.isEditingMultipleObjects)
                return;

            TsitskiProBuilderStairs stairs = (TsitskiProBuilderStairs)target;
            EditorGUILayout.LabelField("Steps", stairs.stepCount.ToString());

            DrawProfile(stairs.ProfileLines());

            using (new EditorGUILayout.HorizontalScope()) {
                if (!stairs.stats && GUILayout.Button("Save As Stairs Stats…"))
                    SaveStats(stairs);
                if (stairs.stats && GUILayout.Button("Copy Stats Into Stairs")) {
                    Undo.RecordObject(stairs, "Copy Stairs Stats");
                    stairs.width = stairs.stats.width;
                    stairs.stepHeight = stairs.stats.stepHeight;
                    stairs.solid = stairs.stats.solid;
                    stairs.thickness = stairs.stats.thickness;
                    stairs.generateCutter = stairs.stats.generateCutter;
                    stairs.cutterHeadroom = stairs.stats.cutterHeadroom;
                    stairs.leftTrim = stairs.stats.leftTrim;
                    stairs.rightTrim = stairs.stats.rightTrim;
                    stairs.bottomTrimForwardOffset = stairs.stats.bottomTrimForwardOffset;
                    stairs.bottomTrimUpwardOffset = stairs.stats.bottomTrimUpwardOffset;
                    stairs.topTrimForwardOffset = stairs.stats.topTrimForwardOffset;
                    stairs.topTrimUpwardOffset = stairs.stats.topTrimUpwardOffset;
                    stairs.smoothCollider = stairs.stats.smoothCollider;
                    stairs.topStepMaterial = stairs.stats.topStepMaterial;
                    stairs.forwardStepMaterial = stairs.stats.forwardStepMaterial;
                    stairs.sideMaterial = stairs.stats.sideMaterial;
                    stairs.stats = null;
                    stairs.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    stairs.Rebuild();
            }
        }

        public static void DrawProfile(IEnumerable<List<Vector2>> lines) {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                ProfilePreview.Graph(lines
                    .Select(points => new ProfileLine { points = points, color = ProfilePreview.Outline, closed = true })
                    .ToList(), null, 140f);
        }

        private static void SaveStats(TsitskiProBuilderStairs stairs) {
            string path = StatsSavePanel.Show("Save Stairs Stats", "Stairs Stats",
                "Save these stairs' settings as a reusable Stairs Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            TsitskiProBuilderStairsStats asset = CreateInstance<TsitskiProBuilderStairsStats>();
            asset.width = stairs.width;
            asset.stepHeight = stairs.stepHeight;
            asset.solid = stairs.solid;
            asset.thickness = stairs.thickness;
            asset.generateCutter = stairs.generateCutter;
            asset.cutterHeadroom = stairs.cutterHeadroom;
            asset.leftTrim = stairs.leftTrim;
            asset.rightTrim = stairs.rightTrim;
            asset.bottomTrimForwardOffset = stairs.bottomTrimForwardOffset;
            asset.bottomTrimUpwardOffset = stairs.bottomTrimUpwardOffset;
            asset.topTrimForwardOffset = stairs.topTrimForwardOffset;
            asset.topTrimUpwardOffset = stairs.topTrimUpwardOffset;
            asset.smoothCollider = stairs.smoothCollider;
            asset.topStepMaterial = stairs.topStepMaterial;
            asset.forwardStepMaterial = stairs.forwardStepMaterial;
            asset.sideMaterial = stairs.sideMaterial;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(stairs, "Assign Stairs Stats");
            stairs.stats = asset;
            EditorUtility.SetDirty(stairs);
        }

        private static void SpiralHandles(TsitskiProBuilderStairs stairs) {
            using (new Handles.DrawingScope(Color.white, stairs.transform.localToWorldMatrix)) {
                float height = stairs.spiralHeight, radius = stairs.spiralRadius, width = stairs.activeWidth;
                Vector3 start = stairs.SpiralDirection(0f), end = stairs.SpiralDirection(stairs.spiralAngle);
                float middle = (stairs.spiralInnerRadius + stairs.spiralOuterRadius) * 0.5f;

                using (new Handles.DrawingScope(HandleTheme.Guide)) {
                    Handles.DrawWireArc(Vector3.zero, Vector3.up, start, stairs.spiralClockwise ? Mathf.Min(stairs.spiralAngle, 360f) : -Mathf.Min(stairs.spiralAngle, 360f), middle);
                    Handles.DrawDottedLine(end * middle, end * middle + Vector3.up * height, 3f);
                }

                if (SliderHandles.Length(Vector3.zero, Vector3.up, ref height, HandleTheme.Secondary)) {
                    Undo.RecordObject(stairs, "Spiral Height");
                    stairs.spiralHeight = height;
                    RebuildQueue.Run(stairs, stairs.Rebuild);
                    return;
                }

                if (SliderHandles.Length(Vector3.zero, start, ref radius, HandleTheme.Secondary, TsitskiProBuilderStairs.MinSize, Handles.DotHandleCap)) {
                    Undo.RecordObject(stairs, "Spiral Radius");
                    stairs.spiralRadius = radius;
                    RebuildQueue.Run(stairs, stairs.Rebuild);
                    return;
                }

                if (SliderHandles.Width(start * stairs.spiralRadius, start, ref width, HandleTheme.Secondary, TsitskiProBuilderStairs.MinSize)) {
                    Object holder = stairs.stats ? stairs.stats : stairs;
                    Undo.RecordObject(holder, "Spiral Width");
                    if (stairs.stats) {
                        stairs.stats.width = width;
                        EditorUtility.SetDirty(stairs.stats);
                        RebuildQueue.Run(stairs.stats, stairs.stats.NotifyChanged);
                    }
                    else
                        stairs.width = width;
                    RebuildQueue.Run(stairs, stairs.Rebuild);
                    return;
                }

                Vector3 tip = end * middle + Vector3.up * height;
                float size = HandleUtility.GetHandleSize(tip) * 0.1f;
                using (new Handles.DrawingScope(HandleTheme.Path)) {
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.Slider2D(tip, Vector3.up, Vector3.right, Vector3.forward, size, Handles.SphereHandleCap, 0f);
                    if (!EditorGUI.EndChangeCheck())
                        return;

                    Vector3 flat = new(moved.x, 0f, moved.z);
                    if (flat.sqrMagnitude < 1e-6f)
                        return;
                    float current = Vector3.SignedAngle(Vector3.forward, end, Vector3.up), wanted = Vector3.SignedAngle(Vector3.forward, flat, Vector3.up);
                    float delta = Mathf.DeltaAngle(current, wanted) * (stairs.spiralClockwise ? 1f : -1f);
                    Undo.RecordObject(stairs, "Spiral Angle");
                    stairs.spiralAngle = Mathf.Clamp(stairs.spiralAngle + delta, 10f, 1440f);
                    RebuildQueue.Run(stairs, stairs.Rebuild);
                }
            }
        }

        private void OnSceneGUI() {
            TsitskiProBuilderStairs stairs = (TsitskiProBuilderStairs)target;
            if (stairs && stairs.isSpiral && Selection.gameObjects.Length == 1) {
                _path.selected.Clear();
                Tools.hidden = false;
                SpiralHandles(stairs);
                return;
            }

            bool editing = stairs && stairs.path != null && Selection.gameObjects.Length == 1;

            if (!editing) {
                _path.selected.Clear();
                Tools.hidden = false;
                SetSuspended(stairs, false);
                return;
            }

            if (_path.selected.Count > 0)
                SetSuspended(stairs, true);

            using (new Handles.DrawingScope(Color.white, stairs.transform.localToWorldMatrix)) {
                PathEdit edit = _path.Do(stairs.path, stairs.pathRadii, false, stairs.transform.rotation);
                if (edit != null) {
                    Undo.RecordObject(stairs, $"{edit.undoName} (Stairs)");
                    stairs.path = edit.points;
                    stairs.pathRadii = edit.radii;
                    SetSuspended(stairs, _path.selected.Count > 0);
                    RebuildQueue.Run(stairs, stairs.Rebuild);
                    return;
                }

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    _path.SelectInRect(rect, stairs.path);
            }

            Tools.hidden = _path.selected.Count > 0;
            SetSuspended(stairs, Tools.hidden);
        }
    }

    [CustomEditor(typeof(TsitskiProBuilderStairsStats))]
    public class TsitskiProBuilderStairsStatsEditor : UnityEditor.Editor {
        private const int PreviewSteps = 6;
        private const float PreviewTreadDepth = 0.28f;

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            TsitskiProBuilderStairsEditor.DrawProfile(new[] {
                TsitskiProBuilderStairs.SampleProfile((TsitskiProBuilderStairsStats)target, PreviewSteps, PreviewTreadDepth)
            });
        }
    }
}
