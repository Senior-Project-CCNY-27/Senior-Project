using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProBuilderMesh = UnityEngine.ProBuilder.ProBuilderMesh;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderTrim))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderTrimEditor : UnityEditor.Editor, ITsitskiPathSplitter {
        private static readonly int RectSelectHash = "ProBuilderTrimRectSelect".GetHashCode();

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PolygonPointHandles _profile;
        private PathPointHandles _path;

        bool ITsitskiPathSplitter.CanSplitPath() => _path != null && ProBuilderTrimSplit.CanSplit(target as TsitskiProBuilderTrim, _path.selected);

        void ITsitskiPathSplitter.SplitPath() {
            ProBuilderTrimSplit.Split((TsitskiProBuilderTrim)target, _path.selected.ToList());
            _path.selected.Clear();
        }

        private void OnEnable() {
            _profile = new PolygonPointHandles(Repaint) { pointColor = HandleTheme.Profile };
            _path = new PathPointHandles(Repaint);
            _profile.Selected += () => _path.selected.Clear();
            _path.Selected += () => _profile.selected.Clear();
        }

        private void OnDisable() {
            Tools.hidden = false;
            SetSuspended(target as TsitskiProBuilderTrim, false);
        }

        private static void SetSuspended(TsitskiProBuilderTrim trim, bool suspended) {
            if (trim && trim.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        private bool anySelected => _path.selected.Count > 0 || _profile.selected.Count > 0;

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderTrim.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            bool pathInStats = usesStats && ((TsitskiProBuilderTrim)target).pathInStats;

            TsitskiInspector.Headers headers = new();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                headers.Next(property);
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name is nameof(TsitskiProBuilderTrim.profile) or nameof(TsitskiProBuilderTrim.profileRadii)
                        or nameof(TsitskiProBuilderTrim.smoothingAngle) or nameof(TsitskiProBuilderTrim.loopCutStep) or nameof(TsitskiProBuilderTrim.applyCutsToProBuilderMesh) or nameof(TsitskiProBuilderTrim.roundBevel) or nameof(TsitskiProBuilderTrim.flattenAngledEnds)
                        or nameof(TsitskiProBuilderTrim.roundingSegments) or nameof(TsitskiProBuilderTrim.profileMirror) or nameof(TsitskiProBuilderTrim.material) && usesStats)
                    continue;
                if (property.name is nameof(TsitskiProBuilderTrim.path) or nameof(TsitskiProBuilderTrim.pathRadii)
                        or nameof(TsitskiProBuilderTrim.closed) && pathInStats)
                    continue;

                if (TsitskiInspector.HiddenByCondition(property))
                    continue;

                headers.BeforePropertyField(property);
                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (serializedObject.isEditingMultipleObjects)
                return;

            TsitskiProBuilderTrim trim = (TsitskiProBuilderTrim)target;
            ProfilePreview.Draw(trim.RoundedProfile(), true, trim.activeProfile, edited => ApplySnap(trim, edited),
                new[] {
                    new GraphPoints {
                        points = trim.activeProfile, apply = edited => ApplySnap(trim, edited, "Edit Trim Profile"),
                        radii = trim.activeProfileRadii, applyRadii = radii => ApplyRadii(trim, radii)
                    }
                });

            using (new EditorGUILayout.HorizontalScope()) {
                if (!trim.stats && GUILayout.Button("Save Profile As Trim Stats…"))
                    SaveStats(trim);
                if (trim.stats && GUILayout.Button("Copy Profile Into Trim")) {
                    Undo.RecordObject(trim, "Copy Trim Profile");
                    trim.profile = new List<Vector2>(trim.stats.profile);
                    trim.profileRadii = new List<float>(trim.stats.profileRadii);
                    trim.roundingSegments = trim.stats.roundingSegments;
                    trim.profileMirror = trim.stats.profileMirror;
                    if (trim.stats.storePath) {
                        trim.path = new List<Vector3>(trim.stats.path);
                        trim.pathRadii = new List<float>(trim.stats.pathRadii);
                        trim.closed = trim.stats.closed;
                    }
                    trim.smoothingAngle = trim.stats.smoothingAngle;
                    trim.loopCutStep = trim.stats.loopCutStep;
                    trim.applyCutsToProBuilderMesh = trim.stats.applyCutsToProBuilderMesh;
                    trim.roundBevel = trim.stats.roundBevel;
                    trim.flattenAngledEnds = trim.stats.flattenAngledEnds;
                    trim.material = trim.stats.material;
                    trim.stats = null;
                    trim.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    trim.Rebuild();
            }
        }


        private static void ApplySnap(TsitskiProBuilderTrim trim, List<Vector2> edited, string undoName = "Snap Trim Profile") {
            if (trim.stats) {
                ApplySnap(trim.stats, edited, undoName);
                return;
            }

            Undo.RecordObject(trim, undoName);
            trim.profile = edited;
            trim.Rebuild();
        }

        private static void ApplyRadii(TsitskiProBuilderTrim trim, List<float> radii) {
            if (trim.stats) {
                ApplyRadii(trim.stats, radii);
                return;
            }

            Undo.RecordObject(trim, "Trim Profile Radius");
            trim.profileRadii = radii;
            trim.Rebuild();
        }

        internal static void ApplyRadii(TsitskiProBuilderTrimStats stats, List<float> radii) {
            Undo.RecordObject(stats, "Trim Profile Radius");
            stats.profileRadii = radii;
            EditorUtility.SetDirty(stats);
            stats.NotifyChanged();
        }

        internal static void ApplySnap(TsitskiProBuilderTrimStats stats, List<Vector2> edited, string undoName = "Snap Trim Profile") {
            Undo.RecordObject(stats, undoName);
            stats.profile = edited;
            EditorUtility.SetDirty(stats);
            stats.NotifyChanged();
        }

        private static void SaveStats(TsitskiProBuilderTrim trim) {
            string path = StatsSavePanel.Show("Save Trim Stats", "Trim Stats",
                "Save this trim's profile as a reusable Trim Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            TsitskiProBuilderTrimStats asset = CreateInstance<TsitskiProBuilderTrimStats>();
            asset.profile = new List<Vector2>(trim.profile);
            asset.profileRadii = new List<float>(trim.profileRadii);
            asset.roundingSegments = trim.roundingSegments;
            asset.profileMirror = trim.profileMirror;
            asset.path = new List<Vector3>(trim.path);
            asset.pathRadii = new List<float>(trim.pathRadii);
            asset.closed = trim.closed;
            asset.smoothingAngle = trim.smoothingAngle;
            asset.loopCutStep = trim.loopCutStep;
            asset.applyCutsToProBuilderMesh = trim.applyCutsToProBuilderMesh;
            asset.roundBevel = trim.roundBevel;
            asset.flattenAngledEnds = trim.flattenAngledEnds;
            asset.material = trim.material;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(trim, "Assign Trim Stats");
            trim.stats = asset;
            EditorUtility.SetDirty(trim);
        }

        private void OnSceneGUI() {
            TsitskiProBuilderTrim trim = (TsitskiProBuilderTrim)target;
            bool editing = trim && trim.activePath != null && Selection.gameObjects.Length == 1;

            if (!editing) {
                _path.selected.Clear();
                _profile.selected.Clear();
                Tools.hidden = false;
                SetSuspended(trim, false);
                return;
            }

            if (anySelected)
                SetSuspended(trim, true);

            Matrix4x4 local = trim.transform.localToWorldMatrix;
            Matrix4x4 profileSpace = local * trim.ProfileFrame();
            List<Vector2> profile = trim.activeProfile;

            bool DoProfile() {
                if (profile == null)
                    return false;

                using (new Handles.DrawingScope(Color.white, profileSpace))
                    return ProfileHandles(trim, profile);
            }

            bool DoPath() {
                using (new Handles.DrawingScope(Color.white, local))
                    return PathHandles(trim);
            }

            bool preferProfile = _profile.selected.Count > 0;
            if (preferProfile ? DoPath() || DoProfile() : DoProfile() || DoPath())
                return;

            if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect)) {
                bool pathHit;
                using (new Handles.DrawingScope(Color.white, local))
                    pathHit = _path.SelectInRect(rect, trim.activePath);

                if (!pathHit && profile != null) {
                    using (new Handles.DrawingScope(Color.white, profileSpace))
                        _profile.SelectInRect(rect, profile);
                }
            }

            Tools.hidden = anySelected;
            SetSuspended(trim, Tools.hidden);
        }

        private bool ProfileHandles(TsitskiProBuilderTrim trim, List<Vector2> profile) {
            PointEdit edit = _profile.Do(profile, trim.activeProfileRadii);
            if (edit == null)
                return false;

            TsitskiProBuilderTrimStats stats = trim.stats;
            if (stats) {
                Undo.RecordObject(stats, $"{edit.undoName} (Trim Profile)");
                stats.profile = edit.points;
                stats.profileRadii = edit.radii;
                EditorUtility.SetDirty(stats);
            }
            else {
                Undo.RecordObject(trim, $"{edit.undoName} (Trim Profile)");
                trim.profile = edit.points;
                trim.profileRadii = edit.radii;
            }

            SetSuspended(trim, anySelected);
            if (stats)
                RebuildQueue.Run(stats, stats.NotifyChanged);
            RebuildQueue.Run(trim, trim.Rebuild);
            return true;
        }

        private bool PathHandles(TsitskiProBuilderTrim trim) {
            PathEdit edit = _path.Do(trim.activePath, trim.activePathRadii, trim.activeClosed, trim.transform.rotation);
            if (edit == null)
                return false;

            Undo.RecordObject(trim.pathInStats ? trim.stats : trim, $"{edit.undoName} (Trim)");
            trim.activePath = edit.points;
            trim.activePathRadii = edit.radii;

            TsitskiProBuilderTrimStats stats = trim.pathInStats ? trim.stats : null;
            if (stats)
                EditorUtility.SetDirty(stats);

            SetSuspended(trim, anySelected);
            if (stats)
                RebuildQueue.Run(stats, stats.NotifyChanged);
            RebuildQueue.Run(trim, trim.Rebuild);
            return true;
        }
    }
}
