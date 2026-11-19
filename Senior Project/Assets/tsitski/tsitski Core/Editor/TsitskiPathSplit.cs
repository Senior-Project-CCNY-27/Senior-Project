using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tsitski.Core {
    public interface ITsitskiPathSplitter {
        bool CanSplitPath();
        void SplitPath();
    }

    public static class TsitskiPathSplit {
        private const string MenuPath = "Tools/tsitski/Core/Split Path";
        public const string UndoName = "Split Path";

        [MenuItem(MenuPath, false, 200)]
        private static void SplitSelected() {
            ITsitskiPathSplitter splitter = ActiveSplitter();
            if (splitter == null) {
                Debug.Log("[tsitski] Split Path needs path points selected on a fence, mesh decal, trim, array or other path.");
                return;
            }

            splitter.SplitPath();
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool SplitSelectedValidate() => ActiveSplitter() != null;

        private static ITsitskiPathSplitter ActiveSplitter() =>
            ActiveEditorTracker.sharedTracker.activeEditors.OfType<ITsitskiPathSplitter>().FirstOrDefault(s => s.CanSplitPath());

        public static List<(List<int> indices, bool chosen)> Pieces(int count, bool closed, ICollection<int> selected) {
            List<(List<int>, bool)> pieces = new();
            int segments = closed ? count : count - 1;
            if (segments <= 0)
                return pieces;

            bool Chosen(int segment) => selected.Contains(segment % count) && selected.Contains((segment + 1) % count);

            if (!Enumerable.Range(0, segments).Any(Chosen))
                return PointPieces(count, closed, selected);

            int start = 0;
            if (closed) {
                start = -1;
                for (int k = 0; k < segments; k++)
                    if (Chosen(k) != Chosen((k - 1 + segments) % segments)) {
                        start = k;
                        break;
                    }

                if (start < 0) {
                    pieces.Add((Enumerable.Range(0, count).Append(0).ToList(), Chosen(0)));
                    return pieces;
                }
            }

            List<int> current = null;
            bool currentChosen = false;

            for (int n = 0; n < segments; n++) {
                int k = (start + n) % segments;
                bool chosen = Chosen(k);

                if (current == null || chosen != currentChosen) {
                    current = new List<int> { k % count };
                    currentChosen = chosen;
                    pieces.Add((current, chosen));
                }

                current.Add((k + 1) % count);
            }

            return pieces;
        }

        private static List<(List<int> indices, bool chosen)> PointPieces(int count, bool closed, ICollection<int> selected) {
            List<(List<int>, bool)> pieces = new();
            List<int> cuts = selected.Where(i => i >= 0 && i < count && (closed || i > 0 && i < count - 1)).Distinct().OrderBy(i => i).ToList();
            if (cuts.Count == 0)
                return pieces;

            if (!closed)
                cuts = cuts.Prepend(0).Append(count - 1).ToList();
            else
                cuts.Add(cuts[0] + count);

            for (int c = 0; c < cuts.Count - 1; c++) {
                List<int> indices = new();
                for (int i = cuts[c]; i <= cuts[c + 1]; i++)
                    indices.Add(i % count);
                pieces.Add((indices, c > 0));
            }

            return pieces;
        }

        public static bool CanSplit(int count, bool closed, ICollection<int> selected) => Splits(Pieces(count, closed, selected), closed);

        private static bool Splits(List<(List<int> indices, bool chosen)> pieces, bool closed) =>
            pieces.Count >= 2 || closed && pieces.Count == 1 && !pieces[0].chosen;

        public static List<float> PieceRadii(List<int> indices, List<float> radii) =>
            indices.Select((i, k) => k == 0 || k == indices.Count - 1 || radii == null || i >= radii.Count ? 0f : radii[i]).ToList();

        public static void Split<T>(T source, List<Vector3> path, List<float> radii, bool closed, ICollection<int> selected,
            Func<T, T> duplicate, Action<T, List<Vector3>, List<float>> assign) where T : Component {
            List<(List<int> indices, bool chosen)> pieces = Pieces(path.Count, closed, selected);
            if (!Splits(pieces, closed))
                return;

            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();

            List<Vector3> points = new(path);
            List<int> keep = pieces.First(p => !p.chosen).indices;
            List<Object> split = new();

            foreach ((List<int> indices, bool chosen) in pieces) {
                if (indices == keep)
                    continue;

                T created = duplicate(source);
                assign(created, indices.Select(i => points[i]).ToList(), PieceRadii(indices, radii));
                if (chosen)
                    split.Add(created.gameObject);
            }

            Undo.RecordObject(source, UndoName);
            assign(source, keep.Select(i => points[i]).ToList(), PieceRadii(keep, radii));
            EditorUtility.SetDirty(source);

            Selection.objects = split.Count > 0 ? split.ToArray() : new Object[] { source.gameObject };
            Undo.CollapseUndoOperations(group);
        }

        private static bool AllSelected(int count, ICollection<int> selected) => count > 0 && Enumerable.Range(0, count).All(selected.Contains);

        public static bool CanSplitPieces(int count, bool closed, ICollection<int> selected, int loopCount, int minimumPoints) {
            if (count == 0 || selected.Count == 0)
                return false;
            if (AllSelected(count, selected))
                return loopCount > 1;

            List<(List<int> indices, bool chosen)> pieces = Pieces(count, closed, selected);
            return Splits(pieces, closed) && pieces.All(p => p.indices.Count >= minimumPoints);
        }

        public static void SplitPieces<T>(T source, int count, bool closed, ICollection<int> selected, int loopCount, int minimumPoints,
            Func<T, T> duplicate, Action<T, List<int>> keepOnly, Action<T, List<int>> replace, Action<T> remove) where T : Component {
            if (!CanSplitPieces(count, closed, selected, loopCount, minimumPoints))
                return;

            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            List<Object> split = new();

            if (AllSelected(count, selected)) {
                T created = duplicate(source);
                keepOnly(created, Enumerable.Range(0, count).ToList());
                EditorUtility.SetDirty(created);
                split.Add(created.gameObject);

                Undo.RecordObject(source, UndoName);
                remove(source);
            }
            else {
                List<(List<int> indices, bool chosen)> pieces = Pieces(count, closed, selected);
                List<int> keep = pieces.First(p => !p.chosen).indices;
                foreach ((List<int> indices, bool chosen) in pieces) {
                    if (indices == keep)
                        continue;

                    T created = duplicate(source);
                    keepOnly(created, indices);
                    EditorUtility.SetDirty(created);
                    if (chosen)
                        split.Add(created.gameObject);
                }

                Undo.RecordObject(source, UndoName);
                replace(source, keep);
            }

            EditorUtility.SetDirty(source);
            Selection.objects = split.Count > 0 ? split.ToArray() : new Object[] { source.gameObject };
            Undo.CollapseUndoOperations(group);
        }

        public static List<T> Pick<T>(IReadOnlyList<T> values, List<int> indices, T fallback = default) =>
            indices.Select(i => values != null && i < values.Count ? values[i] : fallback).ToList();

        public static T Duplicate<T>(T source) where T : Component {
            GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = $"{source.gameObject.name} Split";
            copy.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
            copy.transform.localScale = source.transform.localScale;
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(copy, UndoName);
            MakeMeshesUnique(source.gameObject, copy);
            T component = copy.GetComponent<T>();
            if (component is IGenerated generated)
                generated.generator = null;
            return component;
        }

        private static readonly HashSet<string> PlainElements = new() {
            "float", "double", "int", "bool", "char", "string", "Vector2", "Vector3", "Vector4", "Vector2Int", "Vector3Int",
            "Quaternion", "Color", "Color32", "Rect", "Bounds", "UInt8", "SInt8", "UInt16", "SInt16", "UInt32", "SInt32", "UInt64", "SInt64"
        };

        public static void MakeMeshesUnique(GameObject source, GameObject copy) {
            HashSet<Mesh> owned = new();
            foreach (Component component in source.GetComponentsInChildren<Component>(true))
                ForEachMesh(component, (property, mesh) => {
                    if (!EditorUtility.IsPersistent(mesh))
                        owned.Add(mesh);
                });
            if (owned.Count == 0)
                return;

            Dictionary<Mesh, Mesh> clones = new();
            foreach (Component component in copy.GetComponentsInChildren<Component>(true))
                ForEachMesh(component, (property, mesh) => {
                    if (!owned.Contains(mesh))
                        return;
                    if (!clones.TryGetValue(mesh, out Mesh clone)) {
                        clone = Object.Instantiate(mesh);
                        clone.name = mesh.name;
                        clones[mesh] = clone;
                    }
                    property.objectReferenceValue = clone;
                });
        }

        private static void ForEachMesh(Component component, Action<SerializedProperty, Mesh> visit) {
            if (!component)
                return;

            SerializedObject serialized = new(component);
            SerializedProperty property = serialized.GetIterator();
            bool enter = true;
            while (property.Next(enter)) {
                enter = !(property.isArray && PlainElements.Contains(property.arrayElementType)) && property.propertyType != SerializedPropertyType.String;
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Mesh mesh)
                    visit(property, mesh);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
