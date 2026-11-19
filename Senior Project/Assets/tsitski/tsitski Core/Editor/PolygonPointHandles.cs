using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public class PointEdit {
        public List<Vector2> points;
        public List<float> radii;
        public string undoName;
    }

    public class PolygonPointHandles {
        public Color pointColor = HandleTheme.Path;
        private static Color SelectedColor => HandleTheme.Selected;

        public readonly HashSet<int> selected = new();
        public int minimumPoints = 3;
        public event Action Selected;

        private readonly Action _repaint;
        private int _pointCount = -1;
        private PastedPoints? _pasted;
        private bool _pasteDragged;
        private Quaternion _rotation = Quaternion.identity;
        private Vector3 _scale = Vector3.one;

        public PolygonPointHandles(Action repaint) => _repaint = repaint;

        public PointEdit Do(IReadOnlyList<Vector2> points, IReadOnlyList<float> sourceRadii) {
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
            PointEdit edit = Commands(points, radii, e);
            if (edit != null)
                return edit;

            for (int i = 0; i < points.Count; i++) {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                Handles.color = new Color(pointColor.r, pointColor.g, pointColor.b, 0.6f);
                Handles.DrawLine(a, b);

                Vector2 mid = (a + b) * 0.5f;
                float size = HandleUtility.GetHandleSize(mid) * 0.04f;
                if (!Handles.Button(mid, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap))
                    continue;

                List<Vector2> inserted = new(points);
                inserted.Insert(i + 1, mid);
                radii.Insert(i + 1, 0f);
                SetSelection(inserted.Count, i + 1);
                return new PointEdit { points = inserted, radii = radii, undoName = "Insert Point" };
            }

            for (int i = 0; i < points.Count; i++) {
                Vector2 position = points[i];
                bool isSelected = selected.Contains(i);
                float size = HandleUtility.GetHandleSize(position) * (isSelected ? 0.075f : 0.06f);
                Handles.color = isSelected ? SelectedColor : pointColor;

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

            if (points.Count >= 3) {
                foreach (int i in selected.OrderBy(i => i)) {
                    Vector2 previous = points[(i - 1 + points.Count) % points.Count];
                    Vector2 next = points[(i + 1) % points.Count];
                    if (!RoundingHandle.Do(points[i], previous, next, radii[i], out float radius))
                        continue;

                    foreach (int j in selected)
                        radii[j] = radius;
                    return new PointEdit { points = new List<Vector2>(points), radii = radii, undoName = "Round Points" };
                }
            }

            List<Vector2> transformed = TransformHandles(points);
            if (transformed == null)
                return null;

            if (_pasted is { } pasted && pasted.Matches(selected, transformed.Count)) {
                PasteOrder.Reorder(transformed, radii, ref pasted, true, Vector2.Distance, selected);
                _pasted = pasted;
                _pasteDragged = true;
            }
            else {
                _pasted = null;
            }

            return new PointEdit { points = transformed, radii = radii, undoName = "Transform Points" };
        }

        public void SelectInRect(Rect rect, IReadOnlyList<Vector2> points) {
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
        }

        public void SelectAll(int count) {
            selected.Clear();
            for (int i = 0; i < count; i++)
                selected.Add(i);
            if (count > 0)
                Selected?.Invoke();
            SceneView.RepaintAll();
        }

        public void Clear() {
            selected.Clear();
            SceneView.RepaintAll();
        }

        private void SetSelection(int count, params int[] indices) {
            _pasted = null;
            selected.Clear();
            selected.UnionWith(indices);
            _pointCount = count;
            _repaint();
        }

        private PointEdit Commands(IReadOnlyList<Vector2> points, List<float> radii, Event e) {
            bool command = e.type is EventType.ValidateCommand or EventType.ExecuteCommand;
            bool execute = e.type == EventType.ExecuteCommand;

            if (command && selected.Count > 0 && PointClipboard.IsPaste(e) && PointClipboard.has2D) {
                e.Use();
                if (!execute)
                    return null;

                List<Vector2> pasted = PointClipboard.Paste2D();
                if (pasted.Count == 0)
                    return null;

                int insertAt = selected.Max() + 1;
                List<Vector2> result = new(points);
                result.InsertRange(insertAt, pasted);
                radii.InsertRange(insertAt, Enumerable.Repeat(0f, pasted.Count));
                SetSelection(result.Count, Enumerable.Range(insertAt, pasted.Count).ToArray());
                _pasted = new PastedPoints { anchor = insertAt - 1, start = insertAt, count = pasted.Count };
                _pasteDragged = false;
                Selected?.Invoke();
                return new PointEdit { points = result, radii = radii, undoName = "Paste Points" };
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
            else if (e.type is EventType.ValidateCommand or EventType.ExecuteCommand &&
                     e.commandName is "Delete" or "SoftDelete") {
                delete = e.type == EventType.ExecuteCommand;
                e.Use();
            }
            else {
                return null;
            }

            if (!delete || points.Count - selected.Count < minimumPoints)
                return null;

            List<Vector2> remaining = points.Where((_, i) => !selected.Contains(i)).ToList();
            List<float> remainingRadii = radii.Where((_, i) => !selected.Contains(i)).ToList();
            SetSelection(remaining.Count);
            return new PointEdit { points = remaining, radii = remainingRadii, undoName = "Delete Points" };
        }

        private List<Vector2> TransformHandles(IReadOnlyList<Vector2> points) {
            Vector2 pivot = Vector2.zero;
            foreach (int i in selected)
                pivot += points[i];
            pivot /= selected.Count;

            Vector3 pivot3 = pivot;
            float size = HandleUtility.GetHandleSize(pivot3);

            if (GUIUtility.hotControl == 0) {
                _rotation = Quaternion.identity;
                _scale = Vector3.one;
            }

            Func<Vector2, Vector2> transform = null;

            switch (Tools.current) {
                case Tool.Rotate: {
                    Handles.color = Handles.zAxisColor;
                    EditorGUI.BeginChangeCheck();
                    Quaternion rotation = Handles.Disc(_rotation, pivot3, Vector3.forward, size, false, 0f);
                    if (EditorGUI.EndChangeCheck()) {
                        Quaternion delta = rotation * Quaternion.Inverse(_rotation);
                        _rotation = rotation;
                        transform = p => pivot + (Vector2)(delta * (p - pivot));
                    }
                    break;
                }
                case Tool.Scale: {
                    EditorGUI.BeginChangeCheck();
                    Vector3 scale = Handles.ScaleHandle(_scale, pivot3, Quaternion.identity, size);
                    if (EditorGUI.EndChangeCheck() && Mathf.Abs(scale.x) > 1e-4f && Mathf.Abs(scale.y) > 1e-4f) {
                        Vector2 ratio = new(scale.x / _scale.x, scale.y / _scale.y);
                        _scale = scale;
                        transform = p => pivot + Vector2.Scale(p - pivot, ratio);
                    }
                    break;
                }
                default: {
                    EditorGUI.BeginChangeCheck();
                    Vector3 position = pivot3;
                    Handles.color = Handles.xAxisColor;
                    position = Handles.Slider(position, Vector3.right);
                    Handles.color = Handles.yAxisColor;
                    position = Handles.Slider(position, Vector3.up);
                    Handles.color = Handles.zAxisColor;
                    position = Handles.Slider2D(position, Vector3.forward, Vector3.right, Vector3.up, size * 0.12f,
                        Handles.RectangleHandleCap, Vector2.zero);
                    if (EditorGUI.EndChangeCheck()) {
                        Vector2 delta = (Vector2)position - pivot;
                        transform = p => p + delta;
                    }
                    break;
                }
            }

            if (transform == null)
                return null;

            List<Vector2> edited = new(points);
            foreach (int i in selected)
                edited[i] = transform(edited[i]);
            return edited;
        }
    }
}
