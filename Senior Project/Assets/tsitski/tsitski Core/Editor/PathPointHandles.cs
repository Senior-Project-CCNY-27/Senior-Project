using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public class PathEdit {
        public List<Vector3> points;
        public List<float> radii;
        public List<Quaternion> rotations;
        public List<float> values;
        public string undoName;
        public bool deletePath;
    }

    public class PathPointHandles {
        public Color pathColor = HandleTheme.Path;
        private static Color SelectedColor => HandleTheme.Selected;
        private Color ButtonColor => new(pathColor.r, pathColor.g, pathColor.b, 0.6f);
        private Color ExtendLineColor => new(pathColor.r, pathColor.g, pathColor.b, 0.35f);

        public readonly HashSet<int> selected = new();
        public bool allowRounding = true;
        public bool editableCount = true;
        public bool transformHandles = true;
        public bool deletesPath;
        public int openMinimum = 1;
        public event Action Selected;

        private readonly Action _repaint;
        private int _pointCount = -1;
        private PastedPoints? _pasted;
        private bool _pasteDragged;
        private Quaternion _rotation = Quaternion.identity;
        private Vector3 _scale = Vector3.one;
        private List<Quaternion> _rotations;
        private List<float> _values;

        public PathPointHandles(Action repaint) => _repaint = repaint;

        public PathEdit Do(IReadOnlyList<Vector3> points, IReadOnlyList<float> sourceRadii, bool closed, Quaternion worldRotation,
            IReadOnlyList<Quaternion> sourceRotations = null, IReadOnlyList<float> sourceValues = null) {
            _rotations = sourceRotations == null
                ? null
                : Enumerable.Range(0, points.Count).Select(i => i < sourceRotations.Count ? sourceRotations[i] : Quaternion.identity).ToList();
            _values = sourceValues == null
                ? null
                : Enumerable.Range(0, points.Count).Select(i => i < sourceValues.Count ? sourceValues[i] : 0f).ToList();
            PathEdit edit = DoEdit(points, sourceRadii, closed, worldRotation);
            if (edit != null) {
                edit.rotations = _rotations;
                edit.values = _values;
            }
            return edit;
        }

        private PathEdit DoEdit(IReadOnlyList<Vector3> points, IReadOnlyList<float> sourceRadii, bool closed, Quaternion worldRotation) {
            if (_pointCount != points.Count) {
                selected.Clear();
                _pasted = null;
                _pointCount = points.Count;
            }

            if (_pasteDragged && GUIUtility.hotControl == 0) {
                _pasted = null;
                _pasteDragged = false;
            }

            List<float> radii = PointRounding.Synced(sourceRadii, points.Count);
            Event e = Event.current;
            int minimum = closed ? 3 : Mathf.Max(1, openMinimum);

            PathEdit command = editableCount ? Commands(points, radii, e, minimum) : null;
            if (command != null)
                return command;

            int segments = closed ? points.Count : points.Count - 1;
            for (int i = 0; i < segments; i++) {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Count];
                Handles.color = pathColor;
                Handles.DrawAAPolyLine(3f, a, b);

                if (!editableCount)
                    continue;

                Vector3 mid = (a + b) * 0.5f;
                float size = HandleUtility.GetHandleSize(mid) * 0.04f;
                Handles.color = ButtonColor;
                if (Handles.Button(mid, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap))
                    return Insert(points, radii, i + 1, mid, "Insert Path Point");
            }

            if (editableCount && !closed && points.Count == 1) {
                if (ExtendButton(points[0], Vector3.forward, out Vector3 position))
                    return Insert(points, radii, 1, position, "Extend Path");
            }
            else if (editableCount && !closed && points.Count >= 2) {
                if (ExtendButton(points[0], points[0] - points[1], out Vector3 start))
                    return Insert(points, radii, 0, start, "Extend Path");
                if (ExtendButton(points[^1], points[^1] - points[^2], out Vector3 end))
                    return Insert(points, radii, points.Count, end, "Extend Path");
            }

            for (int i = 0; i < points.Count; i++) {
                Vector3 position = points[i];
                bool isSelected = selected.Contains(i);
                float size = HandleUtility.GetHandleSize(position) * (isSelected ? 0.075f : 0.06f);
                Handles.color = isSelected ? SelectedColor : pathColor;

                if (!Handles.Button(position, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap))
                    continue;

                if (!e.shift)
                    selected.Clear();

                if (e.shift && isSelected) {
                    selected.Remove(i);
                }
                else {
                    selected.Add(i);
                    Selected?.Invoke();
                }

                _repaint();
            }

            if (selected.Count == 0)
                return null;

            if (allowRounding && points.Count >= 3) {
                foreach (int i in selected.OrderBy(i => i)) {
                    if (!closed && (i == 0 || i == points.Count - 1)) {
                        Handles.Slider(points[i], Vector3.up, 0f, RoundingHandle.NoCap, 0f);
                        continue;
                    }

                    Vector3 previous = points[(i - 1 + points.Count) % points.Count];
                    Vector3 next = points[(i + 1) % points.Count];
                    if (!RoundingHandle.Do(points[i], previous, next, radii[i], out float radius))
                        continue;

                    foreach (int j in selected)
                        radii[j] = radius;
                    return new PathEdit { points = new List<Vector3>(points), radii = radii, undoName = "Round Path Points" };
                }
            }

            if (!transformHandles)
                return null;

            List<Vector3> transformed = TransformHandles(points, worldRotation, out Quaternion turned);
            if (transformed == null)
                return null;

            if (_rotations != null && turned != Quaternion.identity)
                foreach (int i in selected)
                    _rotations[i] = turned * _rotations[i];

            if (_pasted is { } pasted && pasted.Matches(selected, transformed.Count)) {
                if (_values != null) {
                    List<(Vector3 point, float value)> pairs = transformed.Select((p, i) => (p, _values[i])).ToList();
                    PasteOrder.Reorder(pairs, radii, ref pasted, closed, (a, b) => Vector3.Distance(a.point, b.point), selected, _rotations);
                    transformed = pairs.Select(p => p.point).ToList();
                    _values = pairs.Select(p => p.value).ToList();
                }
                else
                    PasteOrder.Reorder(transformed, radii, ref pasted, closed, Vector3.Distance, selected, _rotations);
                _pasted = pasted;
                _pasteDragged = true;
            }
            else {
                _pasted = null;
            }

            return new PathEdit { points = transformed, radii = radii, undoName = "Transform Path Points" };
        }

        public bool SelectInRect(Rect rect, IReadOnlyList<Vector3> points) {
            bool any = false;
            for (int i = 0; i < points.Count; i++) {
                if (!rect.Contains(HandleUtility.WorldToGUIPoint(points[i])))
                    continue;

                selected.Add(i);
                any = true;
            }

            if (any)
                Selected?.Invoke();
            _repaint();
            return any;
        }

        public void SelectAll(int count) {
            SetSelection(count, Enumerable.Range(0, count).ToArray());
            if (count > 0)
                Selected?.Invoke();
        }

        private PathEdit Insert(IReadOnlyList<Vector3> points, List<float> radii, int index, Vector3 position, string undoName) {
            List<Vector3> inserted = new(points);
            inserted.Insert(index, position);
            radii.Insert(index, 0f);
            _values?.Insert(index, 0f);
            _rotations?.Insert(index, _rotations.Count == 0 ? Quaternion.identity : _rotations[Mathf.Clamp(index - 1, 0, _rotations.Count - 1)]);
            SetSelection(inserted.Count, index);
            Selected?.Invoke();
            return new PathEdit { points = inserted, radii = radii, undoName = undoName };
        }

        private bool ExtendButton(Vector3 end, Vector3 direction, out Vector3 position) {
            position = end;
            float length = direction.magnitude;
            if (length < 1e-5f) {
                Handles.Button(end, Quaternion.identity, 0f, 0f, RoundingHandle.NoCap);
                return false;
            }

            position = end + direction / length * HandleUtility.GetHandleSize(end) * 2f;
            float size = HandleUtility.GetHandleSize(position) * 0.04f;

            Handles.color = ExtendLineColor;
            Handles.DrawDottedLine(end, position, 3f);
            Handles.color = ButtonColor;
            return Handles.Button(position, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap);
        }

        public void Select(int count, IEnumerable<int> indices) => SetSelection(count, indices.ToArray());

        private void SetSelection(int count, params int[] indices) {
            _pasted = null;
            selected.Clear();
            selected.UnionWith(indices);
            _pointCount = count;
            _repaint();
        }

        private PathEdit Commands(IReadOnlyList<Vector3> points, List<float> radii, Event e, int minimum) {
            bool command = e.type is EventType.ValidateCommand or EventType.ExecuteCommand;
            bool execute = e.type == EventType.ExecuteCommand;

            if (command && selected.Count > 0 && PointClipboard.IsPaste(e) && PointClipboard.has3D) {
                e.Use();
                if (!execute)
                    return null;

                List<Vector3> pasted = PointClipboard.Paste3D();
                if (pasted.Count == 0)
                    return null;

                int insertAt = selected.Max() + 1;
                List<Vector3> result = new(points);
                result.InsertRange(insertAt, pasted);
                radii.InsertRange(insertAt, Enumerable.Repeat(0f, pasted.Count));
                _values?.InsertRange(insertAt, Enumerable.Repeat(0f, pasted.Count));
                _rotations?.InsertRange(insertAt, Enumerable.Repeat(Quaternion.identity, pasted.Count));
                SetSelection(result.Count, Enumerable.Range(insertAt, pasted.Count).ToArray());
                _pasted = new PastedPoints { anchor = insertAt - 1, start = insertAt, count = pasted.Count };
                _pasteDragged = false;
                Selected?.Invoke();
                return new PathEdit { points = result, radii = radii, undoName = "Paste Path Points" };
            }

            if (selected.Count == 0)
                return null;

            if (command && PointClipboard.IsCopy(e)) {
                e.Use();
                if (execute)
                    PointClipboard.Copy(selected.OrderBy(i => i).Select(i => points[i]));
                return null;
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) {
                selected.Clear();
                _repaint();
                e.Use();
                return null;
            }

            bool delete;
            if (e.type == EventType.KeyDown && e.keyCode is KeyCode.Delete or KeyCode.Backspace) {
                e.Use();
                delete = true;
            }
            else if (command && e.commandName is "Delete" or "SoftDelete") {
                delete = execute;
                e.Use();
            }
            else {
                return null;
            }

            if (!delete)
                return null;

            if (points.Count - selected.Count < minimum) {
                if (!deletesPath)
                    return null;

                selected.Clear();
                _pasted = null;
                return new PathEdit { points = new List<Vector3>(), radii = new List<float>(), deletePath = true, undoName = "Delete Path" };
            }

            List<Vector3> remaining = points.Where((_, i) => !selected.Contains(i)).ToList();
            List<float> remainingRadii = radii.Where((_, i) => !selected.Contains(i)).ToList();
            _rotations = _rotations?.Where((_, i) => !selected.Contains(i)).ToList();
            _values = _values?.Where((_, i) => !selected.Contains(i)).ToList();
            SetSelection(remaining.Count);
            return new PathEdit { points = remaining, radii = remainingRadii, undoName = "Delete Path Points" };
        }

        private List<Vector3> TransformHandles(IReadOnlyList<Vector3> points, Quaternion worldRotation, out Quaternion turned) {
            turned = Quaternion.identity;
            Vector3 pivot = Vector3.zero;
            foreach (int i in selected)
                pivot += points[i];
            pivot /= selected.Count;

            Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Global
                ? Quaternion.Inverse(worldRotation)
                : Quaternion.identity;

            if (GUIUtility.hotControl == 0) {
                _rotation = handleRotation;
                _scale = Vector3.one;
            }

            Func<Vector3, Vector3> transform = null;

            switch (Tools.current) {
                case Tool.Rotate: {
                    EditorGUI.BeginChangeCheck();
                    Quaternion rotation = Handles.RotationHandle(_rotation, pivot);
                    if (EditorGUI.EndChangeCheck()) {
                        Quaternion delta = rotation * Quaternion.Inverse(_rotation);
                        _rotation = rotation;
                        turned = delta;
                        transform = p => pivot + delta * (p - pivot);
                    }
                    break;
                }
                case Tool.Scale: {
                    EditorGUI.BeginChangeCheck();
                    Vector3 scale = Handles.ScaleHandle(_scale, pivot, handleRotation, HandleUtility.GetHandleSize(pivot));
                    if (EditorGUI.EndChangeCheck() && Mathf.Abs(scale.x) > 1e-4f && Mathf.Abs(scale.y) > 1e-4f &&
                        Mathf.Abs(scale.z) > 1e-4f) {
                        Vector3 ratio = new(scale.x / _scale.x, scale.y / _scale.y, scale.z / _scale.z);
                        _scale = scale;
                        Quaternion inverse = Quaternion.Inverse(handleRotation);
                        transform = p => pivot + handleRotation * Vector3.Scale(inverse * (p - pivot), ratio);
                    }
                    break;
                }
                default: {
                    EditorGUI.BeginChangeCheck();
                    Vector3 position = Handles.PositionHandle(pivot, handleRotation);
                    if (EditorGUI.EndChangeCheck()) {
                        Vector3 delta = position - pivot;
                        transform = p => p + delta;
                    }
                    break;
                }
            }

            if (transform == null)
                return null;

            List<Vector3> edited = new(points);
            foreach (int i in selected)
                edited[i] = transform(edited[i]);
            return edited;
        }
    }
}
