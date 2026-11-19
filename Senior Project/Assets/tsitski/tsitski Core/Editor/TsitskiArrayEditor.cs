using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class TsitskiArrayEditorHooks {
        public static event Action<TsitskiArray> InspectorGUI;
        public static event Action<TsitskiArray, bool> Suspend;
        public static event Action<TsitskiArray> StatsAssigned;
        public static event Action<TsitskiArray, TsitskiArrayStats> SavingStats;
        public static event Action<TsitskiArray> CopiedStats;

        internal static void DrawInspector(TsitskiArray array) => InspectorGUI?.Invoke(array);
        internal static void SetSuspended(TsitskiArray array, bool suspended) => Suspend?.Invoke(array, suspended);
        internal static void OnStatsAssigned(TsitskiArray array) => StatsAssigned?.Invoke(array);
        internal static void OnSavingStats(TsitskiArray array, TsitskiArrayStats stats) => SavingStats?.Invoke(array, stats);
        internal static void OnCopiedStats(TsitskiArray array) => CopiedStats?.Invoke(array);
    }

    [CustomEditor(typeof(TsitskiArray))]
    [CanEditMultipleObjects]
    public class TsitskiArrayEditor : UnityEditor.Editor, ITsitskiPathSplitter {
        private static readonly int RectSelectHash = "TsitskiArrayRectSelect".GetHashCode();

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PathPointHandles _path;

        private void OnEnable() => _path = new PathPointHandles(Repaint);

        bool ITsitskiPathSplitter.CanSplitPath() =>
            _path != null && target is TsitskiArray array && !array.pathInStats && TsitskiPathSplit.CanSplit(array.path.Count, Closed(array), _path.selected);

        void ITsitskiPathSplitter.SplitPath() {
            TsitskiArray array = (TsitskiArray)target;
            TsitskiPathSplit.Split(array, array.path, array.pathRadii, Closed(array), _path.selected.ToList(), TsitskiPathSplit.Duplicate, (owner, path, radii) => {
                owner.path = path;
                owner.pathRadii = radii;
                owner.closed = false;
                owner.Rebuild();
                EditorUtility.SetDirty(owner);
            });
            _path.selected.Clear();
        }

        private static bool Closed(TsitskiArray array) => array.closed && array.path.Count >= 3;

        private void OnDisable() {
            Tools.hidden = false;
            if (target is TsitskiArray array)
                TsitskiArrayEditorHooks.SetSuspended(array, false);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(TsitskiArray.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            TsitskiArray array = (TsitskiArray)target;
            bool pathInStats = usesStats && array.pathInStats;
            bool pieceInStats = usesStats && array.stats.piece;
            bool usesPillar = !serializedObject.isEditingMultipleObjects && array.activePiece is ScriptableObject;
            bool spanEnds = !serializedObject.isEditingMultipleObjects && array.activePlacement == ArrayPlacementMode.SpanEnds;
            TsitskiArrayStats[] previousStats = targets.Cast<TsitskiArray>().Select(a => a.stats).ToArray();

            TsitskiInspector.Headers headers = new();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                headers.Next(property);
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name is nameof(TsitskiArray.spacing) or nameof(TsitskiArray.offset)
                        or nameof(TsitskiArray.fitSpacing) or nameof(TsitskiArray.alignToPath)
                        or nameof(TsitskiArray.splitAtCorners) or nameof(TsitskiArray.divisionLength)
                        or nameof(TsitskiArray.skipCorners) or nameof(TsitskiArray.skipDivisions)
                        or nameof(TsitskiArray.roundingSegments) or nameof(TsitskiArray.material)
                        or nameof(TsitskiArray.placement) or nameof(TsitskiArray.pieceRotation) && usesStats)
                    continue;
                if (property.name == nameof(TsitskiArray.alignToPath) && spanEnds)
                    continue;
                if (property.name == nameof(TsitskiArray.piece) && pieceInStats)
                    continue;
                if (property.name is nameof(TsitskiArray.pillarHeight) or nameof(TsitskiArray.mergeIntoMesh) && (usesStats || !usesPillar))
                    continue;
                if (property.name == nameof(TsitskiArray.cornerSide) && !usesPillar)
                    continue;
                if (property.name is nameof(TsitskiArray.path) or nameof(TsitskiArray.pathRadii)
                        or nameof(TsitskiArray.closed) && pathInStats)
                    continue;

                if (TsitskiInspector.HiddenByCondition(property))
                    continue;

                headers.BeforePropertyField(property);
                EditorGUILayout.PropertyField(property, true);
            }

            if (serializedObject.ApplyModifiedProperties())
                for (int i = 0; i < targets.Length; i++) {
                    TsitskiArray changed = (TsitskiArray)targets[i];
                    if (changed.stats != previousStats[i] && changed.stats)
                        TsitskiArrayEditorHooks.OnStatsAssigned(changed);
                }

            if (serializedObject.isEditingMultipleObjects)
                return;

            ArraySpacingPreview.Draw(array.activeSpacingSettings);

            TsitskiArrayEditorHooks.DrawInspector(array);

            if (GUILayout.Button("Rebuild"))
                array.Rebuild();

            using (new EditorGUILayout.HorizontalScope()) {
                if (!array.stats && GUILayout.Button("Save As Array Stats…"))
                    SaveStats(array);
                if (array.stats && GUILayout.Button("Copy Stats Into Array"))
                    CopyStats(array);
            }
        }

        private static void SaveStats(TsitskiArray array) {
            string path = StatsSavePanel.Show("Save Array Stats", "Array Stats", "Save this array's settings as a reusable Array Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            TsitskiArrayStats asset = CreateInstance<TsitskiArrayStats>();
            asset.spacing = array.spacing;
            asset.offset = array.offset;
            asset.fitSpacing = array.fitSpacing;
            asset.splitAtCorners = array.splitAtCorners;
            asset.divisionLength = array.divisionLength;
            asset.skipCorners = array.skipCorners;
            asset.skipDivisions = array.skipDivisions;
            asset.alignToPath = array.alignToPath;
            asset.roundingSegments = array.roundingSegments;
            asset.pillarHeight = array.pillarHeight;
            asset.mergeIntoMesh = array.mergeIntoMesh;
            asset.placement = array.placement;
            asset.pieceRotation = array.pieceRotation;
            asset.material = array.material;
            asset.path = new List<Vector3>(array.path);
            asset.pathRadii = new List<float>(array.pathRadii);
            asset.closed = array.closed;
            if (array.piece && EditorUtility.IsPersistent(array.piece))
                asset.piece = array.piece;
            TsitskiArrayEditorHooks.OnSavingStats(array, asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(array, "Assign Array Stats");
            array.stats = asset;
            EditorUtility.SetDirty(array);
        }

        private static void CopyStats(TsitskiArray array) {
            TsitskiArrayStats stats = array.stats;
            Undo.RecordObject(array, "Copy Array Stats");
            array.spacing = stats.spacing;
            array.offset = stats.offset;
            array.fitSpacing = stats.fitSpacing;
            array.splitAtCorners = stats.splitAtCorners;
            array.divisionLength = stats.divisionLength;
            array.skipCorners = stats.skipCorners;
            array.skipDivisions = stats.skipDivisions;
            array.alignToPath = stats.alignToPath;
            array.roundingSegments = stats.roundingSegments;
            array.pillarHeight = stats.pillarHeight;
            array.mergeIntoMesh = stats.mergeIntoMesh;
            array.placement = stats.placement;
            array.pieceRotation = stats.pieceRotation;
            array.material = stats.material;
            if (stats.piece)
                array.piece = stats.piece;
            if (stats.storePath) {
                array.path = new List<Vector3>(stats.path);
                array.pathRadii = new List<float>(stats.pathRadii);
                array.closed = stats.closed;
            }
            TsitskiArrayEditorHooks.OnCopiedStats(array);
            array.stats = null;
            array.Rebuild();
        }

        private void OnSceneGUI() {
            TsitskiArray array = (TsitskiArray)target;
            bool editing = array && array.activePath != null && Selection.gameObjects.Length == 1;

            if (!editing) {
                _path.selected.Clear();
                Tools.hidden = false;
                TsitskiArrayEditorHooks.SetSuspended(array, false);
                return;
            }

            if (_path.selected.Count > 0)
                TsitskiArrayEditorHooks.SetSuspended(array, true);

            Matrix4x4 local = array.transform.localToWorldMatrix;
            using (new Handles.DrawingScope(Color.white, local)) {
                PathEdit edit = _path.Do(array.activePath, array.activePathRadii, array.activeClosed, array.transform.rotation);
                if (edit != null) {
                    Undo.RecordObject(array.pathInStats ? array.stats : array, $"{edit.undoName} (Array)");
                    array.activePath = edit.points;
                    array.activePathRadii = edit.radii;

                    TsitskiArrayStats stats = array.pathInStats ? array.stats : null;
                    if (stats)
                        EditorUtility.SetDirty(stats);

                    TsitskiArrayEditorHooks.SetSuspended(array, _path.selected.Count > 0);
                    if (stats)
                        RebuildQueue.Run(stats, stats.NotifyChanged);
                    RebuildQueue.Run(array, array.Rebuild);
                    return;
                }

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    _path.SelectInRect(rect, array.activePath);
            }

            Tools.hidden = _path.selected.Count > 0;
            TsitskiArrayEditorHooks.SetSuspended(array, Tools.hidden);
        }
    }
}
