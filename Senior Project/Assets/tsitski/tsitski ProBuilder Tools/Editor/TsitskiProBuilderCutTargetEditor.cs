using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderCutTarget))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderCutTargetEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            using (new EditorGUI.DisabledScope(true))
                DrawDefaultInspector();

            if (!targets.Cast<TsitskiProBuilderCutTarget>().All(target => target.TryGetComponent(out ProBuilderMesh _)))
                return;

            EditorGUILayout.Space();
            if (!GUILayout.Button("Bake to ProBuilder Mesh"))
                return;

            Undo.SetCurrentGroupName("Bake Cuts");
            int group = Undo.GetCurrentGroup();
            foreach (TsitskiProBuilderCutTarget target in targets.Cast<TsitskiProBuilderCutTarget>().ToArray()) {
                ProBuilderMesh baked = target.Bake();
                if (baked)
                    baked.Optimize();
            }
            Undo.CollapseUndoOperations(group);
            GUIUtility.ExitGUI();
        }
    }
}
