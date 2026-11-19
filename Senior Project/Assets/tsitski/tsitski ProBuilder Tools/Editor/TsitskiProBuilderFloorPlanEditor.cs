using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderFloorPlan))]
    [CanEditMultipleObjects]
    [InitializeOnLoad]
    public class TsitskiProBuilderFloorPlanEditor : UnityEditor.Editor, ITsitskiPathSplitter {
        private static readonly int RectSelectHash = "ProBuilderFloorPlanRectSelect".GetHashCode();

        static TsitskiProBuilderFloorPlanEditor() => EditorApplication.hierarchyChanged += TrackRemovedParts;

        private static void TrackRemovedParts() {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (TsitskiProBuilderFloorPlan plan in TsitskiProBuilderFloorPlan.All) {
                if (!plan)
                    continue;

                Undo.RecordObject(plan, "Remove Floor Plan Layer");
                if (plan.CollectRemoved())
                    UnityEditor.EditorUtility.SetDirty(plan);
            }
        }

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PathPointHandles _path;

        private void OnEnable() => _path = new PathPointHandles(Repaint);

        private static bool Closed(TsitskiProBuilderFloorPlan plan) => plan.closed && plan.path.Count >= 3;

        bool ITsitskiPathSplitter.CanSplitPath() =>
            _path != null && target is TsitskiProBuilderFloorPlan plan && !plan.floor && plan.path != null &&
            TsitskiPathSplit.CanSplitPieces(plan.path.Count, Closed(plan), _path.selected, 1, 2);

        void ITsitskiPathSplitter.SplitPath() {
            TsitskiProBuilderFloorPlan plan = (TsitskiProBuilderFloorPlan)target;
            List<Vector3> points = new(plan.path);
            List<float> radii = PointRounding.Synced(plan.pathRadii, points.Count);
            void Assign(TsitskiProBuilderFloorPlan owner, List<int> indices) {
                owner.path = TsitskiPathSplit.Pick(points, indices);
                owner.pathRadii = TsitskiPathSplit.PieceRadii(indices, radii);
                owner.closed = false;
                owner.Rebuild();
            }

            TsitskiPathSplit.SplitPieces(plan, points.Count, Closed(plan), _path.selected.ToList(), 1, 2, TsitskiPathSplit.Duplicate, Assign, Assign, _ => { });
            _path.selected.Clear();
        }

        private void OnDisable() {
            UnityEditor.Tools.hidden = false;
            SetSuspended(target as TsitskiProBuilderFloorPlan, false);
        }

        private static void SetSuspended(TsitskiProBuilderFloorPlan plan, bool suspended) {
            if (plan && plan.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderFloorPlan.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            SerializedProperty floorProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderFloorPlan.floor));
            bool usesFloor = floorProperty.hasMultipleDifferentValues || floorProperty.objectReferenceValue;

            TsitskiInspector.Headers headers = new();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                headers.Next(property);
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                TsitskiProBuilderFloorPlan shown = (TsitskiProBuilderFloorPlan)target;
                if (property.name == nameof(TsitskiProBuilderFloorPlan.height) && usesStats && shown.heightInStats ||
                    property.name == nameof(TsitskiProBuilderFloorPlan.thickness) && usesStats && shown.thicknessInStats)
                    continue;
                if (property.name is nameof(TsitskiProBuilderFloorPlan.alignment) or nameof(TsitskiProBuilderFloorPlan.wallsAroundHoles)
                        or nameof(TsitskiProBuilderFloorPlan.squareCorners) or nameof(TsitskiProBuilderFloorPlan.loopCutStep)
                        or nameof(TsitskiProBuilderFloorPlan.verticalLoopCutStep) or nameof(TsitskiProBuilderFloorPlan.applyCutsToProBuilderMesh)
                        or nameof(TsitskiProBuilderFloorPlan.roundingSegments) or nameof(TsitskiProBuilderFloorPlan.roofStats)
                        or nameof(TsitskiProBuilderFloorPlan.layers) or nameof(TsitskiProBuilderFloorPlan.insideMaterial) or nameof(TsitskiProBuilderFloorPlan.outsideMaterial) && usesStats)
                    continue;

                bool generated = usesFloor && property.name is nameof(TsitskiProBuilderFloorPlan.path)
                    or nameof(TsitskiProBuilderFloorPlan.pathRadii) or nameof(TsitskiProBuilderFloorPlan.closed);
                if (TsitskiInspector.HiddenByCondition(property))
                    continue;

                headers.BeforePropertyField(property);
                using (new EditorGUI.DisabledScope(generated))
                    EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (!serializedObject.isEditingMultipleObjects)
                DrawRemoved((TsitskiProBuilderFloorPlan)target);

            using (new EditorGUILayout.HorizontalScope()) {
                if (!serializedObject.isEditingMultipleObjects) {
                    TsitskiProBuilderFloorPlan plan = (TsitskiProBuilderFloorPlan)target;
                    if (!plan.stats && GUILayout.Button("Save As Floor Plan Stats…"))
                        SaveStats(plan);
                    if (plan.stats && GUILayout.Button("Copy Stats Into Floor Plan")) {
                        Undo.RecordObject(plan, "Copy Floor Plan Stats");
                        if (plan.stats.storeHeight)
                            plan.height = plan.stats.height;
                        if (plan.stats.storeThickness)
                            plan.thickness = plan.stats.thickness;
                        plan.alignment = plan.stats.alignment;
                        plan.wallsAroundHoles = plan.stats.wallsAroundHoles;
                        plan.squareCorners = plan.stats.squareCorners;
                        plan.loopCutStep = plan.stats.loopCutStep;
                        plan.verticalLoopCutStep = plan.stats.verticalLoopCutStep;
                        plan.applyCutsToProBuilderMesh = plan.stats.applyCutsToProBuilderMesh;
                        plan.roundingSegments = plan.stats.roundingSegments;
                        plan.roofStats = plan.stats.roofStats;
                        plan.insideMaterial = plan.stats.insideMaterial;
                        plan.outsideMaterial = plan.stats.outsideMaterial;
                        plan.layers = plan.stats.layers.Select(c => c?.Copy()).ToList();
                        plan.stats = null;
                        plan.Rebuild();
                    }
                }

                if (GUILayout.Button("Rebuild"))
                    foreach (TsitskiProBuilderFloorPlan plan in targets.Cast<TsitskiProBuilderFloorPlan>())
                        plan.Rebuild();
            }
        }

        private static void DrawRemoved(TsitskiProBuilderFloorPlan plan) {
            if (plan.removedParts.Count == 0)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Removed Layers", "Generated trims, arrays and roofs deleted from this floor plan. They stay deleted on rebuild without changing the stats."), EditorStyles.boldLabel);
            List<TsitskiPathLayer> layers = plan.activeLayers;
            foreach (string key in plan.removedParts.OrderBy(k => k, System.StringComparer.Ordinal).ToList()) {
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField(TsitskiProBuilderFloorPlan.DescribeKey(key, layers));
                    if (!GUILayout.Button("Restore", GUILayout.Width(70f)))
                        continue;

                    Undo.RecordObject(plan, "Restore Floor Plan Layer");
                    plan.removedParts.Remove(key);
                    plan.Rebuild();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("Restore All")) {
                Undo.RecordObject(plan, "Restore Floor Plan Layers");
                plan.removedParts.Clear();
                plan.Rebuild();
            }
        }

        private static void SaveStats(TsitskiProBuilderFloorPlan plan) {
            string path = StatsSavePanel.Show("Save Floor Plan Stats", "Floor Plan Stats",
                "Save this floor plan's settings as a reusable Floor Plan Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            TsitskiProBuilderFloorPlanStats asset = CreateInstance<TsitskiProBuilderFloorPlanStats>();
            asset.height = plan.height;
            asset.thickness = plan.thickness;
            asset.alignment = plan.alignment;
            asset.wallsAroundHoles = plan.wallsAroundHoles;
            asset.squareCorners = plan.squareCorners;
            asset.loopCutStep = plan.loopCutStep;
            asset.verticalLoopCutStep = plan.verticalLoopCutStep;
            asset.applyCutsToProBuilderMesh = plan.applyCutsToProBuilderMesh;
            asset.roundingSegments = plan.roundingSegments;
            asset.roofStats = plan.roofStats;
            asset.insideMaterial = plan.insideMaterial;
            asset.outsideMaterial = plan.outsideMaterial;
            asset.layers = plan.layers.Select(c => c?.Copy()).ToList();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(plan, "Assign Floor Plan Stats");
            plan.stats = asset;
            UnityEditor.EditorUtility.SetDirty(plan);
        }

        private void OnSceneGUI() {
            TsitskiProBuilderFloorPlan plan = (TsitskiProBuilderFloorPlan)target;
            bool editing = plan && !plan.floor && plan.path != null && UnityEditor.Selection.gameObjects.Length == 1;

            if (plan && plan.floor && plan.path is { Count: > 1 })
                DrawGeneratedPath(plan);

            if (plan && plan.path is { Count: > 0 } && UnityEditor.Selection.gameObjects.Length == 1) {
                using (new Handles.DrawingScope(Color.white, plan.transform.localToWorldMatrix)) {
                    float height = plan.activeHeight;
                    IHeightOwner owner = plan;
                    if (SliderHandles.Length(plan.path[0], Vector3.up, ref height, HandleTheme.Secondary)) {
                        Undo.RecordObject(owner.heightHolder, "Floor Plan Height");
                        owner.ownerHeight = Mathf.Max(0.01f, height);
                        UnityEditor.EditorUtility.SetDirty(owner.heightHolder);
                        RebuildQueue.Run(owner.heightHolder, owner.ApplyHeight);
                        return;
                    }
                }
            }

            if (!editing) {
                _path.selected.Clear();
                UnityEditor.Tools.hidden = false;
                SetSuspended(plan, false);
                return;
            }

            if (_path.selected.Count > 0)
                SetSuspended(plan, true);

            using (new Handles.DrawingScope(Color.white, plan.transform.localToWorldMatrix)) {
                PathEdit edit = _path.Do(plan.path, plan.pathRadii, plan.closed, plan.transform.rotation);
                if (edit != null) {
                    Undo.RecordObject(plan, $"{edit.undoName} (Floor Plan)");
                    plan.path = edit.points;
                    plan.pathRadii = edit.radii;
                    SetSuspended(plan, _path.selected.Count > 0);
                    RebuildQueue.Run(plan, plan.Rebuild);
                    return;
                }

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    _path.SelectInRect(rect, plan.path);
            }

            UnityEditor.Tools.hidden = _path.selected.Count > 0;
            SetSuspended(plan, UnityEditor.Tools.hidden);
        }

        private static void DrawGeneratedPath(TsitskiProBuilderFloorPlan plan) {
            if (Event.current.type != EventType.Repaint)
                return;

            using (new Handles.DrawingScope(HandleTheme.Path, plan.transform.localToWorldMatrix)) {
                List<Vector3> points = plan.path;
                Vector3[] line = plan.closed ? points.Append(points[0]).ToArray() : points.ToArray();
                Handles.DrawAAPolyLine(3f, line);
                foreach (Vector3 point in points)
                    Handles.DotHandleCap(0, point, Quaternion.identity, UnityEditor.HandleUtility.GetHandleSize(point) * 0.04f, EventType.Repaint);
            }
        }
    }

    [CustomEditor(typeof(TsitskiProBuilderFloorPlanStats))]
    public class TsitskiProBuilderFloorPlanStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
        }
    }

    [InitializeOnLoad]
    internal static class ProBuilderFloorPlanWatcher {
        static ProBuilderFloorPlanWatcher() {
            ProBuilderEditor.afterMeshModification += FloorsChanged;
            EditorMeshUtility.meshOptimized += (mesh, _) => FloorsChanged(new[] { mesh });
            TsitskiProBuilderPolygon.Rebuilt += mesh => FloorsChanged(new[] { mesh });
            Undo.undoRedoPerformed += RebuildAll;
        }

        private static void FloorsChanged(IEnumerable<ProBuilderMesh> meshes) {
            HashSet<ProBuilderMesh> changed = new(meshes.Where(m => m));
            foreach (TsitskiProBuilderFloorPlan plan in TsitskiProBuilderFloorPlan.All.ToArray())
                if (plan && changed.Contains(plan.floor))
                    plan.QueueRebuild();
        }

        private static void RebuildAll() {
            foreach (TsitskiProBuilderFloorPlan plan in TsitskiProBuilderFloorPlan.All.ToArray())
                if (plan)
                    RebuildQueue.Queue(plan, plan.Rebuild, true);
        }
    }
}
