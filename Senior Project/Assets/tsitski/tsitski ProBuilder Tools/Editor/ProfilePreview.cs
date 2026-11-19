using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    internal struct ProfileLine {
        public List<Vector2> points;
        public Color color;
        public bool closed;
    }

    internal sealed class GraphPoints {
        public IReadOnlyList<Vector2> points;
        public Color color = ProfilePreview.Outline;
        public Func<Vector2, Vector2> toGraph = p => p;
        public Func<Vector2, int, Vector2> fromGraph = (p, _) => p;
        public Action<List<Vector2>> apply;
        public IReadOnlyList<float> radii;
        public Action<List<float>> applyRadii;
        public int minimumPoints = 2;
        public Func<int, bool> canDelete;
        public Action<List<int>> deleteKeeping;
        public Action deletePath;
    }

    internal static class ProfilePreview {
        public static readonly Color Outline = new(1f, 0.4f, 0.1f, 1f);

        private static readonly Color Background = new(0.16f, 0.16f, 0.16f, 1f);
        private static readonly Color Axis = new(1f, 1f, 1f, 0.15f);
        private static readonly Color Guide = new(1f, 1f, 1f, 0.35f);
        private const string ToleranceKey = "Tsitski.ProBuilderTools.TrimProfileSnapTolerance";

        private static float tolerance {
            get => EditorPrefs.GetFloat(ToleranceKey, 0.005f);
            set => EditorPrefs.SetFloat(ToleranceKey, Mathf.Max(0f, value));
        }

        public static void Draw(List<Vector2> outline, bool closed, List<Vector2> snapSource, Action<List<Vector2>> applySnap,
            IReadOnlyList<GraphPoints> editable = null) =>
            Draw(new List<ProfileLine> { new() { points = outline, color = Outline, closed = closed } }, null, snapSource, applySnap, 140f, editable);

        public static void Draw(List<ProfileLine> lines, IReadOnlyList<float> guides, List<Vector2> snapSource, Action<List<Vector2>> applySnap,
            float height = 140f, IReadOnlyList<GraphPoints> editable = null) {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                Graph(lines, guides, height, editable);

                using (new EditorGUILayout.HorizontalScope()) {
                    tolerance = EditorGUILayout.FloatField("Snap Tolerance", tolerance);
                    if (GUILayout.Button("Snap Profile", GUILayout.Width(100f)) && snapSource is { Count: > 0 })
                        applySnap(Snap(snapSource, tolerance));
                }
            }
        }

        private const float PickRadius = 7f;
        private const float PointSize = 3.5f;
        private static readonly int GraphHash = "TsitskiProfileGraph".GetHashCode();
        private static readonly Color BoxFill = new(1f, 1f, 1f, 0.06f);
        private static readonly Color BoxEdge = new(1f, 1f, 1f, 0.4f);

        private sealed class GraphState {
            public readonly HashSet<(int set, int index)> selected = new();
            public bool dragging, boxing;
            public Vector2 dragStart, boxStart, boxEnd;
            public List<List<Vector2>> original;
            public GraphView frozen;
        }

        private struct GraphView {
            public Vector2 min, max;
            public float scale;
            public Vector2 offset;

            public Vector2 ToGui(Vector2 p) => new(offset.x + (p.x - min.x) * scale, offset.y + (max.y - p.y) * scale);

            public Vector2 ToGraph(Vector2 gui) => new(min.x + (gui.x - offset.x) / scale, max.y - (gui.y - offset.y) / scale);
        }

        public static void Graph(List<ProfileLine> lines, IReadOnlyList<float> guides, float height, IReadOnlyList<GraphPoints> editable = null) {
            Rect rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            int id = GUIUtility.GetControlID(GraphHash, FocusType.Passive, rect);
            GraphState state = (GraphState)GUIUtility.GetStateObject(typeof(GraphState), id);

            List<List<Vector2>> sets = editable?.Select(e => e.points?.Select(e.toGraph).ToList() ?? new List<Vector2>()).ToList()
                                       ?? new List<List<Vector2>>();
            state.selected.RemoveWhere(s => s.set >= sets.Count || s.index >= sets[s.set].Count);

            List<Vector2> all = lines.Where(l => l.points != null).SelectMany(l => l.points).Concat(sets.SelectMany(p => p)).ToList();
            bool drawable = all.Count >= 2;
            GraphView view = state.dragging ? state.frozen : Fit(rect, all);

            Rect field = new(rect.x + 4f, rect.y + 4f, Mathf.Min(rect.width - 8f, 190f), EditorGUIUtility.singleLineHeight * 2f + 2f);
            bool showField = editable != null && state.selected.Count > 0;

            if (showField && Event.current.type != EventType.Repaint)
                SelectionFields(field, editable, sets, state.selected);

            if (editable != null && drawable)
                HandleInput(rect, id, state, view, editable, sets, showField ? field : Rect.zero);

            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, Background);
            if (!drawable)
                return;

            Handles.color = Axis;
            Vector2 origin = view.ToGui(Vector2.zero);
            Handles.DrawLine(new Vector3(rect.x, origin.y), new Vector3(rect.xMax, origin.y));
            Handles.DrawLine(new Vector3(origin.x, rect.y), new Vector3(origin.x, rect.yMax));

            if (guides != null) {
                Handles.color = Guide;
                foreach (float y in guides) {
                    float guiY = view.ToGui(new Vector2(0f, y)).y;
                    Handles.DrawDottedLine(new Vector3(rect.x, guiY), new Vector3(rect.xMax, guiY), 3f);
                }
            }

            foreach (ProfileLine line in lines) {
                if (line.points == null || line.points.Count < 2)
                    continue;

                List<Vector3> gui = line.points.Select(p => (Vector3)view.ToGui(p)).ToList();
                if (line.closed)
                    gui.Add(gui[0]);

                Handles.color = line.color;
                Handles.DrawAAPolyLine(2f, gui.ToArray());
            }

            for (int s = 0; s < sets.Count; s++) {
                for (int i = 0; i < sets[s].Count; i++) {
                    bool selected = state.selected.Contains((s, i));
                    Vector2 gui = view.ToGui(sets[s][i]);
                    float size = selected ? PointSize + 1.5f : PointSize;
                    EditorGUI.DrawRect(new Rect(gui.x - size, gui.y - size, size * 2f, size * 2f), selected ? HandleTheme.Selected : editable[s].color);
                }
            }

            if (state.boxing) {
                Handles.DrawSolidRectangleWithOutline(BoxRect(state), BoxFill, BoxEdge);
            }

            if (showField) {
                EditorGUI.DrawRect(new Rect(field.x - 2f, field.y - 2f, field.width + 4f, field.height + 4f), Background);
                SelectionFields(field, editable, sets, state.selected);
            }
        }

        private static Rect BoxRect(GraphState state) =>
            Rect.MinMaxRect(Mathf.Min(state.boxStart.x, state.boxEnd.x), Mathf.Min(state.boxStart.y, state.boxEnd.y),
                Mathf.Max(state.boxStart.x, state.boxEnd.x), Mathf.Max(state.boxStart.y, state.boxEnd.y));

        private static GraphView Fit(Rect rect, List<Vector2> all) {
            Vector2 min = Vector2.zero, max = Vector2.zero;
            foreach (Vector2 p in all) {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Rect inner = new(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);
            Vector2 extent = Vector2.Max(max - min, Vector2.one * 1e-4f);
            float scale = Mathf.Min(inner.width / extent.x, inner.height / extent.y);

            return new GraphView {
                min = min,
                max = max,
                scale = scale,
                offset = new Vector2(inner.x + (inner.width - extent.x * scale) * 0.5f, inner.y + (inner.height - extent.y * scale) * 0.5f)
            };
        }

        private static void SelectionFields(Rect field, IReadOnlyList<GraphPoints> editable, List<List<Vector2>> sets,
            HashSet<(int set, int index)> selected) {
            List<(int set, int index)> points = selected.OrderBy(p => p.set).ThenBy(p => p.index).ToList();
            bool hasRadii = points.All(p => editable[p.set].applyRadii != null);

            float Radius((int set, int index) p) {
                IReadOnlyList<float> radii = editable[p.set].radii;
                return radii != null && p.index < radii.Count ? radii[p.index] : 0f;
            }

            string label = points.Count == 1 ? $"Point {points[0].index}" : $"{points.Count} Points";
            float line = EditorGUIUtility.singleLineHeight;
            int fields = hasRadii ? 3 : 2;
            float fieldWidth = field.width / fields;

            GUI.Label(new Rect(field.x, field.y, field.width, line), label, EditorStyles.miniBoldLabel);

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 14f;

            Rect Slot(int i) => new(field.x + fieldWidth * i, field.y + line + 2f, fieldWidth - 4f, line);

            if (ComponentField(Slot(0), "X", points.Select(p => sets[p.set][p.index].x).ToList(), out float x))
                MoveSelected(editable, sets, points, p => new Vector2(x, p.y));

            if (ComponentField(Slot(1), "Y", points.Select(p => sets[p.set][p.index].y).ToList(), out float y))
                MoveSelected(editable, sets, points, p => new Vector2(p.x, y));

            if (hasRadii && ComponentField(Slot(2), "R", points.Select(Radius).ToList(), out float r))
                SetRadii(editable, sets, points, Mathf.Max(0f, r));

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private static bool ComponentField(Rect rect, string label, List<float> values, out float value) {
            bool mixed = values.Any(v => Mathf.Abs(v - values[0]) > 1e-5f);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = mixed;
            value = EditorGUI.FloatField(rect, label, values[0]);
            EditorGUI.showMixedValue = false;
            return EditorGUI.EndChangeCheck();
        }

        private static void MoveSelected(IReadOnlyList<GraphPoints> editable, List<List<Vector2>> sets, List<(int set, int index)> points,
            Func<Vector2, Vector2> change) {
            foreach (int set in points.Select(p => p.set).Distinct()) {
                List<Vector2> moved = new(sets[set]);
                foreach ((int s, int index) in points)
                    if (s == set)
                        moved[index] = change(moved[index]);

                Apply(editable[set], moved);
            }
        }

        private static void SetRadii(IReadOnlyList<GraphPoints> editable, List<List<Vector2>> sets, List<(int set, int index)> points, float radius) {
            foreach (int set in points.Select(p => p.set).Distinct()) {
                List<float> radii = PointRounding.Synced(editable[set].radii, sets[set].Count);
                foreach ((int s, int index) in points)
                    if (s == set)
                        radii[index] = radius;

                editable[set].applyRadii(radii);
            }

            GUI.changed = true;
        }

        private static void HandleInput(Rect rect, int id, GraphState state, GraphView view, IReadOnlyList<GraphPoints> editable,
            List<List<Vector2>> sets, Rect field) {
            Event e = Event.current;

            switch (e.GetTypeForControl(id)) {
                case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition) && !field.Contains(e.mousePosition): {
                    if (Pick(view, sets, e.mousePosition) is { } point) {
                        if (e.shift && state.selected.Contains(point)) {
                            state.selected.Remove(point);
                            e.Use();
                            break;
                        }

                        if (!e.shift && !state.selected.Contains(point))
                            state.selected.Clear();

                        state.selected.Add(point);
                        state.dragging = true;
                        state.frozen = view;
                        state.dragStart = view.ToGraph(e.mousePosition);
                        state.original = sets.Select(set => new List<Vector2>(set)).ToList();
                    }
                    else {
                        if (!e.shift)
                            state.selected.Clear();

                        state.boxing = true;
                        state.boxStart = state.boxEnd = e.mousePosition;
                    }

                    GUIUtility.hotControl = id;
                    GUIUtility.keyboardControl = 0;
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when GUIUtility.hotControl == id: {
                    if (state.dragging) {
                        Vector2 delta = state.frozen.ToGraph(e.mousePosition) - state.dragStart;

                        for (int s = 0; s < state.original.Count && s < editable.Count; s++) {
                            if (!state.selected.Any(p => p.set == s))
                                continue;

                            List<Vector2> moved = new(state.original[s]);
                            foreach ((int set, int index) in state.selected)
                                if (set == s && index < moved.Count)
                                    moved[index] = state.original[s][index] + delta;

                            Apply(editable[s], moved);
                        }
                    }
                    else if (state.boxing) {
                        state.boxEnd = e.mousePosition;
                    }

                    e.Use();
                    break;
                }
                case EventType.MouseUp when GUIUtility.hotControl == id: {
                    if (state.boxing) {
                        Rect box = BoxRect(state);
                        for (int s = 0; s < sets.Count; s++)
                            for (int i = 0; i < sets[s].Count; i++)
                                if (box.Contains(view.ToGui(sets[s][i])))
                                    state.selected.Add((s, i));
                    }

                    state.dragging = state.boxing = false;
                    state.original = null;
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
                }
                case EventType.KeyDown when e.keyCode == KeyCode.Escape && state.selected.Count > 0 && rect.Contains(e.mousePosition):
                    state.selected.Clear();
                    e.Use();
                    break;
                case EventType.ValidateCommand or EventType.ExecuteCommand
                    when e.commandName is "Delete" or "SoftDelete" && CanDeleteHere(rect, state): {
                    if (e.type == EventType.ExecuteCommand)
                        DeleteSelected(state, editable, sets);

                    e.Use();
                    break;
                }
                case EventType.KeyDown when e.keyCode is KeyCode.Delete or KeyCode.Backspace && CanDeleteHere(rect, state):
                    DeleteSelected(state, editable, sets);
                    e.Use();
                    break;
            }
        }

        private static bool CanDeleteHere(Rect rect, GraphState state) =>
            state.selected.Count > 0 && rect.Contains(Event.current.mousePosition) && !EditorGUIUtility.editingTextField &&
            GUIUtility.hotControl == 0;

        private static void DeleteSelected(GraphState state, IReadOnlyList<GraphPoints> editable, List<List<Vector2>> sets) {
            foreach (int set in state.selected.Select(p => p.set).Distinct().ToList()) {
                GraphPoints points = editable[set];
                int count = sets[set].Count;
                HashSet<int> remove = state.selected
                    .Where(p => p.set == set && (points.canDelete?.Invoke(p.index) ?? true))
                    .Select(p => p.index)
                    .ToHashSet();

                if (remove.Count == 0)
                    continue;

                if (count - remove.Count < points.minimumPoints) {
                    points.deletePath?.Invoke();
                    continue;
                }

                List<int> keep = Enumerable.Range(0, count).Where(i => !remove.Contains(i)).ToList();

                if (points.deleteKeeping != null) {
                    points.deleteKeeping(keep);
                }
                else {
                    Apply(points, keep.Select(i => sets[set][i]).ToList());

                    if (points.applyRadii != null) {
                        List<float> radii = PointRounding.Synced(points.radii, count);
                        points.applyRadii(keep.Select(i => radii[i]).ToList());
                    }
                }
            }

            state.selected.Clear();
            GUI.changed = true;
        }

        private static (int set, int index)? Pick(GraphView view, List<List<Vector2>> sets, Vector2 mouse) {
            (int, int)? best = null;
            float bestDistance = PickRadius;

            for (int s = 0; s < sets.Count; s++) {
                for (int i = 0; i < sets[s].Count; i++) {
                    float distance = Vector2.Distance(view.ToGui(sets[s][i]), mouse);
                    if (distance > bestDistance)
                        continue;

                    bestDistance = distance;
                    best = (s, i);
                }
            }

            return best;
        }

        private static void Apply(GraphPoints points, List<Vector2> graph) {
            List<Vector2> converted = new(graph.Count);
            for (int i = 0; i < graph.Count; i++)
                converted.Add(points.fromGraph(graph[i], i));

            points.apply(converted);
            GUI.changed = true;
        }

        public static List<Vector2> Snap(List<Vector2> profile, float tolerance) {
            float[] xs = SnapAxis(profile.Select(p => p.x).ToArray(), tolerance);
            float[] ys = SnapAxis(profile.Select(p => p.y).ToArray(), tolerance);
            return xs.Select((x, i) => new Vector2(x, ys[i])).ToList();
        }

        private static float[] SnapAxis(float[] values, float tolerance) {
            int[] order = Enumerable.Range(0, values.Length).OrderBy(i => values[i]).ToArray();
            float[] snapped = (float[])values.Clone();

            int start = 0;
            for (int i = 1; i <= order.Length; i++) {
                if (i < order.Length && values[order[i]] - values[order[i - 1]] <= tolerance)
                    continue;

                float average = 0f;
                for (int j = start; j < i; j++)
                    average += values[order[j]];
                average /= i - start;

                for (int j = start; j < i; j++)
                    snapped[order[j]] = average;

                start = i;
            }

            return snapped;
        }
    }
}
