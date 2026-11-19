using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    internal static class ProBuilderTrimSplit {
        internal static bool CanSplit(TsitskiProBuilderTrim trim, ICollection<int> selected) =>
            trim && !trim.pathInStats && TsitskiPathSplit.CanSplit(trim.path.Count, trim.closed && trim.path.Count >= 3, selected);

        internal static void Split(TsitskiProBuilderTrim trim, ICollection<int> selected) {
            if (!CanSplit(trim, selected))
                return;

            List<float> radii = PointRounding.Synced(trim.pathRadii, trim.path.Count);
            TsitskiPathSplit.Split(trim, trim.path, radii, trim.closed && trim.path.Count >= 3, selected, CreateTrim, Assign);
        }

        private static void Assign(TsitskiProBuilderTrim trim, List<Vector3> path, List<float> radii) {
            trim.path = path;
            trim.pathRadii = radii;
            trim.closed = false;
            trim.Rebuild();
            EditorUtility.SetDirty(trim);
        }

        private static TsitskiProBuilderTrim CreateTrim(TsitskiProBuilderTrim original) {
            GameObject source = original.gameObject;
            GameObject child = new($"{source.name} Split");
            Undo.RegisterCreatedObjectUndo(child, TsitskiPathSplit.UndoName);

            Transform transform = child.transform;
            transform.SetParent(source.transform.parent, false);
            transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
            transform.localScale = source.transform.localScale;
            transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

            child.layer = source.layer;
            child.tag = source.tag;
            GameObjectUtility.SetStaticEditorFlags(child, GameObjectUtility.GetStaticEditorFlags(source));

            child.AddComponent<ProBuilderMesh>();
            if (source.TryGetComponent(out MeshRenderer sourceRenderer) && child.TryGetComponent(out MeshRenderer renderer)) {
                EditorUtility.CopySerialized(sourceRenderer, renderer);
                renderer.enabled = true;
            }

            TsitskiProBuilderTrim trim = child.AddComponent<TsitskiProBuilderTrim>();
            EditorUtility.CopySerialized(original, trim);
            ((IGenerated)trim).generator = null;

            if (source.TryGetComponent(out MeshCollider sourceCollider)) {
                if (!child.TryGetComponent(out MeshCollider collider))
                    collider = child.AddComponent<MeshCollider>();
                collider.sharedMaterial = sourceCollider.sharedMaterial;
                collider.convex = sourceCollider.convex;
                collider.isTrigger = sourceCollider.isTrigger;
                collider.enabled = true;
            }

            return trim;
        }
    }
}
