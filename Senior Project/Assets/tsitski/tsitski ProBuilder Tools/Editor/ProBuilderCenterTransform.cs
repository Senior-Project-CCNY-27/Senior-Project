using System.Collections.Generic;
using System.Linq;
using Tsitski.Core;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    internal class ProBuilderCenterTransform : ICenterTransformHook {
        static ProBuilderCenterTransform() => TsitskiCenterTransform.Register(new ProBuilderCenterTransform());

        public bool TryGetLocalBounds(GameObject gameObject, out Bounds bounds) {
            bounds = default;
            if (!gameObject.TryGetComponent(out ProBuilderMesh mesh) || mesh.positions.Count == 0)
                return false;

            IList<Vector3> positions = mesh.positions;
            bounds = new Bounds(positions[0], Vector3.zero);
            foreach (Vector3 position in positions)
                bounds.Encapsulate(position);
            return true;
        }

        public IEnumerable<Transform> KeepLocal(GameObject gameObject) {
            if (gameObject.TryGetComponent(out TsitskiProBuilderCutTarget target) && target.resultTransform)
                yield return target.resultTransform;
        }

        public void Moved(GameObject gameObject, Vector3 offset, bool handledByGenerator) {
            foreach (Transform kept in KeepLocal(gameObject)) {
                kept.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                kept.localScale = Vector3.one;
            }

            if (handledByGenerator || !gameObject.TryGetComponent(out ProBuilderMesh mesh))
                return;

            Undo.RecordObject(mesh, "Center Transform To Mesh");
            mesh.positions = mesh.positions.Select(p => p + offset).ToArray();
            mesh.ToMesh();
            mesh.Refresh();
            mesh.Optimize();
            UnityEditor.EditorUtility.SetDirty(mesh);
            ProBuilderEditor.Refresh();
        }
    }
}
