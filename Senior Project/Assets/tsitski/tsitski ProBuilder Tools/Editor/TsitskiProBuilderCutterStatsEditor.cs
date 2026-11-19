using UnityEditor;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderCutterStats))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderCutterStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();
            TsitskiProBuilderCutterEditor.DrawProperties(serializedObject, nameof(TsitskiProBuilderCutterStats.center));
            serializedObject.ApplyModifiedProperties();

            SerializedProperty shape = serializedObject.FindProperty(nameof(TsitskiProBuilderCutterStats.shape));
            bool explosion = !shape.hasMultipleDifferentValues && shape.intValue == (int)ProBuilderCutterShape.Explosion;

            if (explosion && GUILayout.Button("Randomize Explosion")) {
                foreach (Object t in targets) {
                    TsitskiProBuilderCutterStats stats = (TsitskiProBuilderCutterStats)t;
                    Undo.RecordObject(stats, "Randomize Explosion");
                    stats.explosionSeed = Random.Range(int.MinValue, int.MaxValue);
                    EditorUtility.SetDirty(stats);
                    stats.NotifyChanged();
                }
            }

            if (serializedObject.isEditingMultipleObjects)
                return;

            TsitskiProBuilderCutterStats target = (TsitskiProBuilderCutterStats)this.target;
            TsitskiProBuilderCutterEditor.DrawProfile(CutterShapeSettings.From(target), target.pathPoints, target.pathRadii,
                path => EditStats(target, "Edit Cutter Path", () => target.pathPoints = path),
                polygon => EditStats(target, "Edit Cutter Polygon", () => target.polygonPoints = polygon),
                radii => EditStats(target, "Cutter Path Radius", () => target.pathRadii = radii),
                radii => EditStats(target, "Cutter Polygon Radius", () => target.polygonRadii = radii));
        }

        private static void EditStats(TsitskiProBuilderCutterStats stats, string undoName, System.Action change) {
            Undo.RecordObject(stats, undoName);
            change();
            EditorUtility.SetDirty(stats);
            stats.NotifyChanged();
        }
    }
}
