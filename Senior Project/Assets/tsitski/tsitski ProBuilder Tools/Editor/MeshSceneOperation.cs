using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using EditorUtility = UnityEditor.EditorUtility;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [System.Serializable]
    internal abstract class MeshSceneOperation : SceneOperation {
        protected class Target {
            public ProBuilderMesh mesh;
            public ProBuilderMesh snapshot;
            public int[] faces;
            public Edge[] edges;
        }

        private readonly List<Target> _targets = new();
        private int _undoGroup;

        protected IReadOnlyList<Target> targets => _targets;

        protected abstract bool Accepts(ProBuilderMesh mesh);
        protected abstract void ApplyTo(Target target);
        protected virtual void Prepare() { }

        protected override bool Begin() {
            foreach (ProBuilderMesh mesh in MeshSelection.top) {
                if (!Accepts(mesh))
                    continue;

                ProBuilderMesh snapshot = new GameObject("Operation Snapshot") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<ProBuilderMesh>();
                snapshot.Clear();
                snapshot.ToMesh();
                snapshot.CopyFrom(mesh);
                _targets.Add(new Target {
                    mesh = mesh,
                    snapshot = snapshot,
                    faces = mesh.GetSelectedFaces().Select(f => mesh.faces.IndexOf(f)).ToArray(),
                    edges = mesh.selectedEdges.ToArray()
                });
            }

            if (_targets.Count == 0)
                return false;

            Undo.SetCurrentGroupName(title);
            _undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(_targets.Select(t => (Object)t.mesh).ToArray(), title);
            Prepare();
            return true;
        }

        protected override void Apply() {
            foreach (Target target in _targets) {
                target.mesh.CopyFrom(target.snapshot);
                ApplyTo(target);
                target.mesh.ToMesh();
                target.mesh.Refresh();
            }

            ProBuilderEditor.Refresh();
        }

        protected override void Cancel() {
            Undo.RevertAllDownToGroup(_undoGroup);
            foreach (Target target in _targets) {
                ProBuilderMesh mesh = target.mesh;
                mesh.CopyFrom(target.snapshot);
                mesh.ToMesh();
                mesh.Refresh();
                mesh.SetSelectedFaces(target.faces.Select(i => mesh.faces[i]));
                if (target.edges.Length > 0)
                    mesh.SetSelectedEdges(target.edges);
            }

            Cleanup();
            ProBuilderEditor.Refresh();
        }

        protected override void Complete() {
            foreach (Target target in _targets) {
                target.mesh.Optimize();
                EditorUtility.SetDirty(target.mesh);
            }

            Undo.CollapseUndoOperations(_undoGroup);
            Cleanup();
            ProBuilderEditor.Refresh();
        }

        protected override void Abandon() => Cleanup();

        private void Cleanup() {
            foreach (Target target in _targets)
                if (target.snapshot)
                    Object.DestroyImmediate(target.snapshot.gameObject);
            _targets.Clear();
        }

        protected static Dictionary<int, int> SharedLookup(ProBuilderMesh mesh) {
            Dictionary<int, int> shared = new();
            IList<SharedVertex> groups = mesh.sharedVertices;
            for (int s = 0; s < groups.Count; s++)
                foreach (int index in groups[s])
                    shared[index] = s;
            return shared;
        }

        protected static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

        protected static List<Edge> PerimeterEdges(ProBuilderMesh mesh, IEnumerable<Face> faces) {
            Dictionary<int, int> shared = SharedLookup(mesh);
            Dictionary<(int, int), List<Edge>> uses = new();
            foreach (Face face in faces)
            foreach (Edge edge in face.edges) {
                (int, int) key = Key(shared[edge.a], shared[edge.b]);
                if (!uses.TryGetValue(key, out List<Edge> list))
                    uses[key] = list = new List<Edge>();
                list.Add(edge);
            }

            return uses.Values.Where(l => l.Count == 1).Select(l => l[0]).ToList();
        }
    }
}
