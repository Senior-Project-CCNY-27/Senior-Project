using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderPolygon))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderPolygonEditor : UnityEditor.Editor, ITsitskiPathSplitter {
        private static readonly int RectSelectHash = "ProBuilderPolygonRectSelect".GetHashCode();
        private static readonly Color QuadLines = new(1f, 1f, 1f, 0.3f);
        private static readonly Color SubdivisionLines = new(1f, 1f, 1f, 0.12f);
        private const float AddedPathSize = 2f;

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private readonly List<PathPointHandles> _paths = new();
        private readonly List<PathPointHandles> _holes = new();

        private IEnumerable<PathPointHandles> allHandles => _paths.Concat(_holes);
        private bool anySelected => allHandles.Any(h => h.selected.Count > 0);

        private void OnDisable() {
            UnityEditor.Tools.hidden = false;
            SetSuspended(target as TsitskiProBuilderPolygon, false);
        }

        private int SelectedPath(TsitskiProBuilderPolygon polygon) {
            if (_holes.Any(h => h.selected.Count > 0))
                return -1;
            for (int i = 0; i < _paths.Count && i < polygon.paths.Count; i++)
                if (_paths[i].selected.Count > 0 && polygon.paths[i]?.path != null)
                    return i;
            return -1;
        }

        bool ITsitskiPathSplitter.CanSplitPath() {
            if (target is not TsitskiProBuilderPolygon polygon || polygon.pathInStats)
                return false;
            int index = SelectedPath(polygon);
            return index >= 0 && TsitskiPathSplit.CanSplitPieces(polygon.paths[index].path.Count, true, _paths[index].selected, polygon.paths.Count, 3);
        }

        void ITsitskiPathSplitter.SplitPath() {
            TsitskiProBuilderPolygon polygon = (TsitskiProBuilderPolygon)target;
            int index = SelectedPath(polygon);
            if (index < 0)
                return;

            List<Vector3> points = new(polygon.paths[index].path);
            List<float> radii = PointRounding.Synced(polygon.paths[index].pathRadii, points.Count);
            List<PolygonHole> holes = polygon.holes.Select(h => h?.Copy()).ToList();
            PolygonPath Piece(List<int> indices) => new() {
                path = TsitskiPathSplit.Pick(points, indices),
                pathRadii = indices.Count == points.Count ? new List<float>(radii) : TsitskiPathSplit.PieceRadii(indices, radii)
            };

            TsitskiPathSplit.SplitPieces(polygon, points.Count, true, _paths[index].selected.ToList(), polygon.paths.Count, 3, TsitskiPathSplit.Duplicate,
                (created, indices) => {
                    created.paths = new List<PolygonPath> { Piece(indices) };
                    created.holes = holes.Select(h => h?.Copy()).ToList();
                    created.Rebuild();
                },
                (source, indices) => {
                    source.paths[index] = Piece(indices);
                    source.Rebuild();
                },
                source => {
                    source.paths.RemoveAt(index);
                    source.Rebuild();
                });

            foreach (PathPointHandles handles in allHandles)
                handles.selected.Clear();
        }

        private PathPointHandles CreateHandles(Color color) {
            PathPointHandles handles = new(Repaint) { pathColor = color, deletesPath = true };
            handles.Selected += () => {
                foreach (PathPointHandles other in allHandles)
                    if (other != handles)
                        other.selected.Clear();
            };
            return handles;
        }

        private PathPointHandles LoopHandles(bool hole, int index) {
            List<PathPointHandles> list = hole ? _holes : _paths;
            while (list.Count <= index)
                list.Add(CreateHandles(hole ? HandleTheme.Secondary : HandleTheme.Path));
            return list[index];
        }

        private static void SetSuspended(TsitskiProBuilderPolygon polygon, bool suspended) {
            if (polygon && polygon.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderPolygon.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            bool pathInStats = usesStats && ((TsitskiProBuilderPolygon)target).pathInStats;

            TsitskiInspector.Headers headers = new();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                headers.Next(property);
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name is nameof(TsitskiProBuilderPolygon.thickness) or nameof(TsitskiProBuilderPolygon.subdivisionStep)
                        or nameof(TsitskiProBuilderPolygon.applyCutsToProBuilderMesh) or nameof(TsitskiProBuilderPolygon.topMaterial) or nameof(TsitskiProBuilderPolygon.bottomMaterial) && usesStats)
                    continue;
                if (property.name is nameof(TsitskiProBuilderPolygon.paths) or nameof(TsitskiProBuilderPolygon.holes) && pathInStats)
                    continue;

                if (TsitskiInspector.HiddenByCondition(property))
                    continue;

                headers.BeforePropertyField(property);
                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (serializedObject.isEditingMultipleObjects) {
                if (GUILayout.Button("Rebuild"))
                    foreach (TsitskiProBuilderPolygon each in targets.Cast<TsitskiProBuilderPolygon>())
                        each.Rebuild();
                return;
            }

            TsitskiProBuilderPolygon polygon = (TsitskiProBuilderPolygon)target;
            if (polygon.skippedHoles > 0)
                EditorGUILayout.HelpBox($"{polygon.skippedHoles} piece(s) of the polygon couldn't be built after cutting the holes. Try moving the holes slightly.",
                    MessageType.Warning);

            DrawPreview(polygon.PathLoops(), polygon.HoleLoops(), polygon.activeSubdivisionStep,
                EditablePolygon(polygon.pathInStats ? polygon.stats : polygon, polygon.activePaths, polygon.activeHoles, () => {
                    if (polygon.pathInStats) {
                        EditorUtility.SetDirty(polygon.stats);
                        RebuildQueue.Run(polygon.stats, polygon.stats.NotifyChanged);
                    }
                    RebuildQueue.Run(polygon, polygon.Rebuild);
                }));

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Add Path"))
                    AddPath(polygon);
                if (GUILayout.Button("Add Hole"))
                    AddHole(polygon);
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (!polygon.stats && GUILayout.Button("Save As Polygon Stats…"))
                    SaveStats(polygon);
                if (polygon.stats && GUILayout.Button("Copy Stats Into Polygon")) {
                    Undo.RecordObject(polygon, "Copy Polygon Stats");
                    polygon.thickness = polygon.stats.thickness;
                    polygon.subdivisionStep = polygon.stats.subdivisionStep;
                    polygon.applyCutsToProBuilderMesh = polygon.stats.applyCutsToProBuilderMesh;
                    polygon.topMaterial = polygon.stats.topMaterial;
                    polygon.bottomMaterial = polygon.stats.bottomMaterial;
                    if (polygon.stats.savePolygon) {
                        polygon.paths = polygon.stats.paths.Select(p => p?.Copy()).ToList();
                        polygon.holes = polygon.stats.holes.Select(h => h?.Copy()).ToList();
                    }
                    polygon.stats = null;
                    polygon.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    polygon.Rebuild();
            }
        }

        internal static List<GraphPoints> EditablePolygon(Object owner, List<PolygonPath> paths, List<PolygonHole> holes, System.Action changed) {
            GraphPoints Loop(List<Vector3> points, System.Action<List<Vector3>> set, List<float> radii, System.Action<List<float>> setRadii, Color color,
                System.Action remove) => new() {
                points = points.Select(p => new Vector2(p.x, p.z)).ToList(),
                color = color,
                apply = edited => {
                    Undo.RecordObject(owner, "Edit Polygon");
                    set(edited.Select((p, i) => new Vector3(p.x, i < points.Count ? points[i].y : 0f, p.y)).ToList());
                    changed();
                },
                radii = radii,
                applyRadii = edited => {
                    Undo.RecordObject(owner, "Polygon Radius");
                    setRadii(edited);
                    changed();
                },
                minimumPoints = 3,
                deleteKeeping = keep => {
                    Undo.RecordObject(owner, "Delete Polygon Points");
                    List<float> synced = PointRounding.Synced(radii, points.Count);
                    set(keep.Select(i => points[i]).ToList());
                    setRadii(keep.Select(i => synced[i]).ToList());
                    changed();
                },
                deletePath = () => {
                    Undo.RecordObject(owner, "Delete Polygon Path");
                    remove();
                    changed();
                }
            };

            List<GraphPoints> editable = new();
            foreach (PolygonPath path in paths.Where(p => p?.path != null))
                editable.Add(Loop(path.path, points => path.path = points, path.pathRadii, radii => path.pathRadii = radii, ProfilePreview.Outline,
                    () => paths.Remove(path)));
            foreach (PolygonHole hole in holes.Where(h => h?.path != null))
                editable.Add(Loop(hole.path, points => hole.path = points, hole.pathRadii, radii => hole.pathRadii = radii, HandleTheme.Secondary,
                    () => holes.Remove(hole)));
            return editable;
        }

        internal static void DrawPreview(IEnumerable<List<Vector2>> outlines, IEnumerable<List<Vector2>> holes, float subdivisionStep,
            IReadOnlyList<GraphPoints> editable = null) {
            List<List<Vector2>> outlineLoops = outlines.ToList();
            List<List<Vector2>> holeLoops = holes.ToList();
            List<Vector2[]> pieces = TsitskiProBuilderPolygon.CapPieces(outlineLoops, holeLoops);
            List<ProfileLine> lines = pieces.SelectMany(p => TsitskiProBuilderPolygon.PieceGridLines(p, subdivisionStep))
                .Select(l => new ProfileLine { points = l.ToList(), color = SubdivisionLines }).ToList();
            lines.AddRange(pieces.Select(p => new ProfileLine { points = p.ToList(), color = QuadLines, closed = true }));
            lines.AddRange(outlineLoops.Where(l => l.Count >= 3).Select(l => new ProfileLine { points = l, color = ProfilePreview.Outline, closed = true }));
            lines.AddRange(holeLoops.Where(h => h.Count >= 3).Select(h => new ProfileLine { points = h, color = HandleTheme.Secondary, closed = true }));

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                ProfilePreview.Graph(lines, null, 160f, editable);
        }

        private static List<Vector3> Square(Vector2 center, float y, float half) => new() {
            new(center.x - half, y, center.y - half), new(center.x + half, y, center.y - half),
            new(center.x + half, y, center.y + half), new(center.x - half, y, center.y + half)
        };

        private static bool Bounds(TsitskiProBuilderPolygon polygon, out Vector2 min, out Vector2 max) {
            List<Vector2> points = polygon.PathLoops().SelectMany(l => l).ToList();
            min = points.Count > 0 ? points.Aggregate(Vector2.Min) : Vector2.zero;
            max = points.Count > 0 ? points.Aggregate(Vector2.Max) : Vector2.zero;
            return points.Count > 0;
        }

        private static void AddPath(TsitskiProBuilderPolygon polygon) {
            Vector2 center = Bounds(polygon, out Vector2 min, out Vector2 max)
                ? new Vector2(max.x + AddedPathSize * 0.5f, (min.y + max.y) * 0.5f)
                : Vector2.zero;
            InsertPath(polygon, "Add Polygon Path", new PolygonPath { path = Square(center, polygon.baseY, AddedPathSize * 0.5f) });
        }

        private static void AddHole(TsitskiProBuilderPolygon polygon) {
            if (!Bounds(polygon, out Vector2 min, out Vector2 max))
                return;

            InsertHole(polygon, "Add Polygon Hole", new PolygonHole { path = Square((min + max) * 0.5f, polygon.baseY, 1f) });
        }

        private static void InsertPath(TsitskiProBuilderPolygon polygon, string undoName, PolygonPath path) =>
            Insert(polygon, undoName, () => polygon.activePaths = new List<PolygonPath>(polygon.activePaths) { path });

        private static void InsertHole(TsitskiProBuilderPolygon polygon, string undoName, PolygonHole hole) =>
            Insert(polygon, undoName, () => polygon.activeHoles = new List<PolygonHole>(polygon.activeHoles) { hole });

        private static void Insert(TsitskiProBuilderPolygon polygon, string undoName, System.Action insert) {
            Object owner = polygon.pathInStats ? polygon.stats : polygon;
            Undo.RecordObject(owner, undoName);
            insert();

            if (polygon.pathInStats) {
                EditorUtility.SetDirty(polygon.stats);
                polygon.stats.NotifyChanged();
            }
            polygon.Rebuild();
        }

        private static void SaveStats(TsitskiProBuilderPolygon polygon) {
            string path = StatsSavePanel.Show("Save Polygon Stats", "Polygon Stats",
                "Save this polygon's settings as a reusable Polygon Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            TsitskiProBuilderPolygonStats asset = CreateInstance<TsitskiProBuilderPolygonStats>();
            asset.thickness = polygon.thickness;
            asset.subdivisionStep = polygon.subdivisionStep;
            asset.applyCutsToProBuilderMesh = polygon.applyCutsToProBuilderMesh;
            asset.topMaterial = polygon.topMaterial;
            asset.bottomMaterial = polygon.bottomMaterial;
            asset.paths = polygon.paths.Select(p => p?.Copy()).ToList();
            asset.holes = polygon.holes.Select(h => h?.Copy()).ToList();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(polygon, "Assign Polygon Stats");
            polygon.stats = asset;
            EditorUtility.SetDirty(polygon);
        }

        private void OnSceneGUI() {
            TsitskiProBuilderPolygon polygon = (TsitskiProBuilderPolygon)target;
            bool editing = polygon && Selection.gameObjects.Length == 1;

            if (!editing) {
                foreach (PathPointHandles handles in allHandles)
                    handles.selected.Clear();
                UnityEditor.Tools.hidden = false;
                SetSuspended(polygon, false);
                return;
            }

            if (anySelected)
                SetSuspended(polygon, true);

            DoPathClipboard(polygon);

            int pathCount = polygon.activePaths.Count, holeCount = polygon.activeHoles.Count;
            if (_paths.Count > pathCount)
                _paths.RemoveRange(pathCount, _paths.Count - pathCount);
            if (_holes.Count > holeCount)
                _holes.RemoveRange(holeCount, _holes.Count - holeCount);

            List<(bool hole, int index)> loops = Enumerable.Range(0, pathCount).Select(i => (false, i))
                .Concat(Enumerable.Range(0, holeCount).Select(i => (true, i))).ToList();

            using (new Handles.DrawingScope(Color.white, polygon.transform.localToWorldMatrix)) {
                foreach ((bool hole, int index) in loops.OrderBy(l => LoopHandles(l.hole, l.index).selected.Count > 0))
                    if (DoLoop(polygon, hole, index))
                        return;

                if (DoThickness(polygon))
                    return;

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    foreach ((bool hole, int index) in loops) {
                        List<Vector3> points = LoopPoints(polygon, hole, index);
                        if (points != null && LoopHandles(hole, index).SelectInRect(rect, points))
                            break;
                    }
            }

            UnityEditor.Tools.hidden = anySelected;
            SetSuspended(polygon, UnityEditor.Tools.hidden);
        }

        private static List<Vector3> LoopPoints(TsitskiProBuilderPolygon polygon, bool hole, int index) =>
            hole ? polygon.activeHoles[index]?.path : polygon.activePaths[index]?.path;

        private static List<float> LoopRadii(TsitskiProBuilderPolygon polygon, bool hole, int index) =>
            hole ? polygon.activeHoles[index]?.pathRadii : polygon.activePaths[index]?.pathRadii;

        private bool DoThickness(TsitskiProBuilderPolygon polygon) {
            if (!Bounds(polygon, out Vector2 min, out Vector2 max))
                return false;

            Vector2 center = (min + max) * 0.5f;
            float value = polygon.activeThickness;
            if (!SliderHandles.SignedLength(new Vector3(center.x, polygon.baseY, center.y), Vector3.up, ref value, HandleTheme.Secondary,
                    TsitskiProBuilderPolygon.MinThickness))
                return false;

            TsitskiProBuilderPolygonStats stats = polygon.stats;
            Undo.RecordObject(stats ? stats : polygon, "Polygon Thickness");
            if (stats) {
                stats.thickness = value;
                EditorUtility.SetDirty(stats);
                RebuildQueue.Run(stats, stats.NotifyChanged);
            }
            else
                polygon.thickness = value;

            RebuildQueue.Run(polygon, polygon.Rebuild);
            return true;
        }

        private void DoPathClipboard(TsitskiProBuilderPolygon polygon) {
            Event e = Event.current;
            bool execute = e.type == EventType.ExecuteCommand;
            if (!execute && e.type != EventType.ValidateCommand)
                return;

            if (PointClipboard.IsCopy(e)) {
                foreach (bool hole in new[] { false, true })
                for (int index = 0; index < (hole ? polygon.activeHoles.Count : polygon.activePaths.Count); index++) {
                    List<Vector3> points = LoopPoints(polygon, hole, index);
                    int selected = LoopHandles(hole, index).selected.Count;
                    if (points == null || selected == 0 || selected != points.Count)
                        continue;

                    e.Use();
                    if (!execute)
                        return;

                    List<float> radii = LoopRadii(polygon, hole, index);
                    if (hole)
                        PointClipboard.CopyPath(points, new PolygonHole { path = points, pathRadii = radii });
                    else
                        PointClipboard.CopyPath(points, new PolygonPath { path = points, pathRadii = radii });
                    return;
                }
                return;
            }

            if (!PointClipboard.IsPaste(e))
                return;

            bool pastePath = PointClipboard.HasPath<PolygonPath>();
            if (!pastePath && !PointClipboard.HasPath<PolygonHole>())
                return;

            e.Use();
            if (!execute)
                return;

            float y = polygon.baseY;
            if (pastePath) {
                PolygonPath path = PointClipboard.PastePath<PolygonPath>();
                if (path?.path is not { Count: >= 3 })
                    return;

                path.path = path.path.Select(p => new Vector3(p.x, y, p.z)).ToList();
                InsertPath(polygon, "Paste Polygon Path", path);
                SelectOnly(false, polygon.activePaths.Count - 1, path.path.Count);
                return;
            }

            PolygonHole pastedHole = PointClipboard.PastePath<PolygonHole>();
            if (pastedHole?.path is not { Count: >= 3 })
                return;

            pastedHole.path = pastedHole.path.Select(p => new Vector3(p.x, y, p.z)).ToList();
            InsertHole(polygon, "Paste Polygon Hole", pastedHole);
            SelectOnly(true, polygon.activeHoles.Count - 1, pastedHole.path.Count);
        }

        private void SelectOnly(bool hole, int index, int count) {
            foreach (PathPointHandles handles in allHandles)
                handles.selected.Clear();
            LoopHandles(hole, index).SelectAll(count);
        }

        private bool DoLoop(TsitskiProBuilderPolygon polygon, bool hole, int index) {
            List<Vector3> points = LoopPoints(polygon, hole, index);
            if (points is not { Count: > 0 })
                return false;

            PathEdit edit = LoopHandles(hole, index).Do(points, LoopRadii(polygon, hole, index), true, polygon.transform.rotation);
            if (edit == null)
                return false;

            float y = polygon.baseY;
            edit.points = edit.points.Select(p => new Vector3(p.x, y, p.z)).ToList();

            TsitskiProBuilderPolygonStats stats = polygon.pathInStats ? polygon.stats : null;
            Undo.RecordObject(stats ? stats : polygon, $"{edit.undoName} (Polygon)");

            if (edit.deletePath) {
                if (hole) {
                    polygon.activeHoles = polygon.activeHoles.Where((_, i) => i != index).ToList();
                    _holes.RemoveAt(index);
                }
                else {
                    polygon.activePaths = polygon.activePaths.Where((_, i) => i != index).ToList();
                    _paths.RemoveAt(index);
                }
            }
            else if (hole) {
                PolygonHole loop = polygon.activeHoles[index];
                loop.path = edit.points;
                loop.pathRadii = edit.radii;
            }
            else {
                PolygonPath loop = polygon.activePaths[index];
                loop.path = edit.points;
                loop.pathRadii = edit.radii;
            }

            if (stats)
                EditorUtility.SetDirty(stats);

            SetSuspended(polygon, anySelected);
            if (stats)
                RebuildQueue.Run(stats, stats.NotifyChanged);
            RebuildQueue.Run(polygon, polygon.Rebuild);
            return true;
        }
    }

    [CustomEditor(typeof(TsitskiProBuilderPolygonStats))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderPolygonStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            SerializedProperty savePolygon = serializedObject.FindProperty(nameof(TsitskiProBuilderPolygonStats.savePolygon));
            bool hidePolygon = !savePolygon.hasMultipleDifferentValues && !savePolygon.boolValue;

            TsitskiInspector.DrawProperties(serializedObject);

            if (serializedObject.isEditingMultipleObjects || hidePolygon)
                return;

            TsitskiProBuilderPolygonStats stats = (TsitskiProBuilderPolygonStats)target;
            List<PolygonPath> paths = stats.paths ?? new List<PolygonPath>();
            List<PolygonHole> holes = stats.holes ?? new List<PolygonHole>();
            TsitskiProBuilderPolygonEditor.DrawPreview(TsitskiProBuilderPolygon.PathLoops(paths), TsitskiProBuilderPolygon.HoleLoops(holes), stats.subdivisionStep,
                TsitskiProBuilderPolygonEditor.EditablePolygon(stats, paths, holes, () => {
                    EditorUtility.SetDirty(stats);
                    stats.NotifyChanged();
                }));
        }
    }

    [InitializeOnLoad]
    internal static class ProBuilderPolygonWatcher {
        static ProBuilderPolygonWatcher() => Undo.undoRedoPerformed += () => {
            foreach (TsitskiProBuilderPolygon polygon in TsitskiProBuilderPolygon.All.ToArray())
                if (polygon)
                    RebuildQueue.Queue(polygon, polygon.Rebuild, true);
        };
    }
}
