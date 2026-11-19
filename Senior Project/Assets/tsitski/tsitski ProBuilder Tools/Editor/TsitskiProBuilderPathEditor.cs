using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderPath))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderPathEditor : UnityEditor.Editor, ITsitskiPathSplitter {
        private static readonly int RectSelectHash = "ProBuilderPathRectSelect".GetHashCode();
        private const float SnapScreenSize = 0.25f;
        private const float BranchLength = 4f;

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private readonly List<PathPointHandles> _branches = new();

        private bool anySelected => _branches.Any(h => h.selected.Count > 0);

        private void OnDisable() {
            UnityEditor.Tools.hidden = false;
            SetSuspended(target as TsitskiProBuilderPath, false);
        }

        private PathPointHandles BranchHandles(int index) {
            while (_branches.Count <= index) {
                PathPointHandles handles = new(Repaint) { pathColor = HandleTheme.Path, allowRounding = false, deletesPath = true, openMinimum = 2 };
                handles.Selected += () => {
                    foreach (PathPointHandles other in _branches)
                        if (other != handles)
                            other.selected.Clear();
                };
                _branches.Add(handles);
            }
            return _branches[index];
        }

        private static void SetSuspended(TsitskiProBuilderPath path, bool suspended) {
            if (path && path.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        private int SelectedBranch(TsitskiProBuilderPath path) {
            for (int b = 0; b < _branches.Count && b < path.activeBranches.Count; b++)
                if (_branches[b].selected.Count > 0)
                    return b;
            return -1;
        }

        bool ITsitskiPathSplitter.CanSplitPath() {
            if (target is not TsitskiProBuilderPath path || path.pathInStats)
                return false;
            int b = SelectedBranch(path);
            return b >= 0 && path.branches[b]?.path != null &&
                   TsitskiPathSplit.CanSplitPieces(path.branches[b].path.Count, false, _branches[b].selected, path.branches.Count, 2);
        }

        void ITsitskiPathSplitter.SplitPath() {
            TsitskiProBuilderPath path = (TsitskiProBuilderPath)target;
            int b = SelectedBranch(path);
            if (b < 0)
                return;

            PathBranch branch = path.branches[b];
            path.SyncWidths(branch);
            List<Vector3> points = new(branch.path);
            List<float> widths = new(branch.widths);
            PathBranch Piece(List<int> indices) => new() {
                path = TsitskiPathSplit.Pick(points, indices),
                widths = TsitskiPathSplit.Pick(widths, indices, path.activeWidth)
            };

            TsitskiPathSplit.SplitPieces(path, points.Count, false, _branches[b].selected.ToList(), path.branches.Count, 2, TsitskiPathSplit.Duplicate,
                (created, indices) => {
                    created.branches = new List<PathBranch> { Piece(indices) };
                    created.Rebuild();
                },
                (source, indices) => {
                    source.branches[b] = Piece(indices);
                    source.Rebuild();
                },
                source => {
                    source.branches.RemoveAt(b);
                    source.Rebuild();
                });

            foreach (PathPointHandles handles in _branches)
                handles.selected.Clear();
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderPath.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            bool pathInStats = usesStats && ((TsitskiProBuilderPath)target).pathInStats;

            TsitskiInspector.Headers headers = new();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                headers.Next(property);
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                TsitskiProBuilderPath shown = (TsitskiProBuilderPath)target;
                if (property.name == nameof(TsitskiProBuilderPath.height) && usesStats && shown.heightInStats)
                    continue;
                if (property.name is nameof(TsitskiProBuilderPath.storeWidth) or nameof(TsitskiProBuilderPath.width) && usesStats && shown.widthInStats)
                    continue;
                if (property.name is nameof(TsitskiProBuilderPath.samplesPerSegment) or nameof(TsitskiProBuilderPath.smoothingAngle)
                        or nameof(TsitskiProBuilderPath.topMaterial) or nameof(TsitskiProBuilderPath.sideMaterial)
                        or nameof(TsitskiProBuilderPath.bottomMaterial) && usesStats)
                    continue;
                if (property.name == nameof(TsitskiProBuilderPath.branches) && pathInStats)
                    continue;

                if (TsitskiInspector.HiddenByCondition(property))
                    continue;

                headers.BeforePropertyField(property);
                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (serializedObject.isEditingMultipleObjects) {
                if (GUILayout.Button("Rebuild"))
                    foreach (TsitskiProBuilderPath each in targets.Cast<TsitskiProBuilderPath>())
                        each.Rebuild();
                return;
            }

            TsitskiProBuilderPath path = (TsitskiProBuilderPath)target;
            EditorGUILayout.LabelField("Junctions", path.junctionCount.ToString());
            DrawPreview(path);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Add Branch"))
                    AddBranch(path);
                using (new EditorGUI.DisabledScope(SelectedPoint(path) == null))
                    if (GUILayout.Button("Branch From Selected Point"))
                        BranchFromSelected(path);
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (!path.stats && GUILayout.Button("Save As Path Stats…"))
                    SaveStats(path);
                if (path.stats && GUILayout.Button("Copy Stats Into Path")) {
                    Undo.RecordObject(path, "Copy Path Stats");
                    TsitskiProBuilderPathStats stats = path.stats;
                    if (stats.storeWidth) {
                        path.storeWidth = true;
                        path.width = stats.width;
                    }
                    if (stats.storeHeight)
                        path.height = stats.height;
                    path.samplesPerSegment = stats.samplesPerSegment;
                    path.smoothingAngle = stats.smoothingAngle;
                    path.topMaterial = stats.topMaterial;
                    path.sideMaterial = stats.sideMaterial;
                    path.bottomMaterial = stats.bottomMaterial;
                    if (stats.savePaths)
                        path.branches = stats.branches.Select(b => b?.Copy()).ToList();
                    path.stats = null;
                    path.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    path.Rebuild();
            }
        }

        private static readonly Color EdgeLines = new(1f, 1f, 1f, 0.3f);

        private static void DrawPreview(TsitskiProBuilderPath path) {
            List<ProfileLine> lines = new();
            List<GraphPoints> editable = new();
            Vector2 Flat(Vector3 p) => new(p.x, p.z);

            foreach (PathBranch branch in path.activeBranches.Where(b => b?.path is { Count: >= 2 })) {
                List<TsitskiProBuilderPath.PathSample> samples = path.SampleBranch(branch, out _);
                List<Vector2> left = new(), right = new();
                for (int i = 0; i < samples.Count; i++) {
                    Vector3 forward = samples[Mathf.Min(i + 1, samples.Count - 1)].center - samples[Mathf.Max(i - 1, 0)].center;
                    Vector2 side = new Vector2(forward.z, -forward.x).normalized * (samples[i].width * 0.5f);
                    left.Add(Flat(samples[i].center) - side);
                    right.Add(Flat(samples[i].center) + side);
                }

                lines.Add(new ProfileLine { points = samples.Select(x => Flat(x.center)).ToList(), color = HandleTheme.Guide });
                lines.Add(new ProfileLine { points = left, color = EdgeLines });
                lines.Add(new ProfileLine { points = right, color = EdgeLines });
                lines.Add(new ProfileLine { points = new List<Vector2> { left[0], right[0] }, color = EdgeLines });
                lines.Add(new ProfileLine { points = new List<Vector2> { left[^1], right[^1] }, color = EdgeLines });

                editable.Add(new GraphPoints {
                    points = branch.path.Select(Flat).ToList(),
                    color = ProfilePreview.Outline,
                    minimumPoints = 2,
                    apply = edited => Change(path, "Edit Path", () => ApplyGraph(path, branch, edited)),
                    deleteKeeping = keep => Change(path, "Delete Path Points", () => {
                        path.SyncWidths(branch);
                        List<Vector3> points = branch.path;
                        List<float> widths = branch.widths;
                        branch.path = keep.Select(i => points[i]).ToList();
                        branch.widths = keep.Select(i => widths[i]).ToList();
                    }),
                    deletePath = () => Change(path, "Delete Path Branch", () => path.activeBranches = path.activeBranches.Where(b => b != branch).ToList())
                });
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                ProfilePreview.Graph(lines, null, 160f, editable);
        }

        private static void ApplyGraph(TsitskiProBuilderPath path, PathBranch branch, List<Vector2> edited) {
            path.SyncWidths(branch);
            List<Vector3> old = branch.path;
            List<float> widths = branch.widths;
            List<Vector3> points = new();
            List<float> newWidths = new();

            for (int i = 0; i < edited.Count; i++) {
                int match = edited.Count == old.Count ? i : old.FindIndex(o => (new Vector2(o.x, o.z) - edited[i]).sqrMagnitude < 1e-8f);
                if (match >= 0) {
                    points.Add(new Vector3(edited[i].x, old[match].y, edited[i].y));
                    newWidths.Add(widths[match]);
                    continue;
                }

                Vector3 previous = points.Count > 0 ? points[^1] : old[0];
                float previousWidth = newWidths.Count > 0 ? newWidths[^1] : widths[0];
                points.Add(new Vector3(edited[i].x, previous.y, edited[i].y));
                newWidths.Add(previousWidth);
            }

            branch.path = points;
            branch.widths = newWidths;
        }

        private static void SaveStats(TsitskiProBuilderPath path) {
            string assetPath = StatsSavePanel.Show("Save Path Stats", "Path Stats", "Save this path's settings as a reusable Path Stats asset.");
            if (string.IsNullOrEmpty(assetPath))
                return;

            TsitskiProBuilderPathStats asset = CreateInstance<TsitskiProBuilderPathStats>();
            asset.storeWidth = path.storeWidth;
            asset.width = path.width;
            asset.height = path.height;
            asset.samplesPerSegment = path.samplesPerSegment;
            asset.smoothingAngle = path.smoothingAngle;
            asset.topMaterial = path.topMaterial;
            asset.sideMaterial = path.sideMaterial;
            asset.bottomMaterial = path.bottomMaterial;
            asset.branches = path.branches.Select(b => b?.Copy()).ToList();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(path, "Assign Path Stats");
            path.stats = asset;
            EditorUtility.SetDirty(path);
        }

        private (int branch, int point)? SelectedPoint(TsitskiProBuilderPath path) {
            int b = SelectedBranch(path);
            if (b < 0 || _branches[b].selected.Count != 1)
                return null;
            return (b, _branches[b].selected.First());
        }

        private static Vector3 PointSide(List<Vector3> points, int i) {
            Vector3 forward = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)];
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-8f ? Vector3.Cross(Vector3.up, forward.normalized) : Vector3.right;
        }

        private void AddBranch(TsitskiProBuilderPath path) {
            List<Vector3> points = path.activeBranches.Where(b => b?.path != null).SelectMany(b => b.path).ToList();
            Vector3 start = points.Count > 0 ? new Vector3(points.Max(p => p.x) + path.activeWidth * 2f, points.Average(p => p.y), points.Min(p => p.z)) : Vector3.zero;
            InsertBranch(path, "Add Path Branch", new PathBranch {
                path = new List<Vector3> { start, start + Vector3.forward * BranchLength },
                widths = new List<float> { path.activeWidth, path.activeWidth }
            });
        }

        private void BranchFromSelected(TsitskiProBuilderPath path) {
            if (SelectedPoint(path) is not { } selected)
                return;

            PathBranch from = path.activeBranches[selected.branch];
            Vector3 start = from.path[selected.point];
            float pointWidth = path.PointWidth(from, selected.point);
            InsertBranch(path, "Branch From Point", new PathBranch {
                path = new List<Vector3> { start, start + PointSide(from.path, selected.point) * BranchLength },
                widths = new List<float> { pointWidth, pointWidth }
            });
            SelectOnly(path.activeBranches.Count - 1, 2, new[] { 1 });
        }

        private static void InsertBranch(TsitskiProBuilderPath path, string undoName, PathBranch branch) =>
            Change(path, undoName, () => path.activeBranches = new List<PathBranch>(path.activeBranches) { branch });

        private static void Change(TsitskiProBuilderPath path, string undoName, System.Action change) {
            Object owner = path.pathsOwner;
            Undo.RecordObject(owner, undoName);
            change();
            EditorUtility.SetDirty(owner);
            if (owner is TsitskiProBuilderPathStats stats)
                RebuildQueue.Run(stats, stats.NotifyChanged);
            RebuildQueue.Run(path, path.Rebuild);
        }

        private void SelectOnly(int branch, int count, IEnumerable<int> points) {
            foreach (PathPointHandles handles in _branches)
                handles.selected.Clear();
            BranchHandles(branch).Select(count, points);
        }

        private void OnSceneGUI() {
            TsitskiProBuilderPath path = (TsitskiProBuilderPath)target;
            bool editing = path && Selection.gameObjects.Length == 1;

            if (!editing) {
                foreach (PathPointHandles handles in _branches)
                    handles.selected.Clear();
                UnityEditor.Tools.hidden = false;
                SetSuspended(path, false);
                return;
            }

            if (anySelected)
                SetSuspended(path, true);

            DoClipboard(path);

            List<PathBranch> branches = path.activeBranches;
            if (_branches.Count > branches.Count)
                _branches.RemoveRange(branches.Count, _branches.Count - branches.Count);

            using (new Handles.DrawingScope(Color.white, path.transform.localToWorldMatrix)) {
                foreach (int b in Enumerable.Range(0, branches.Count).OrderBy(i => BranchHandles(i).selected.Count > 0))
                    if (DoBranch(path, b))
                        return;

                for (int b = 0; b < branches.Count; b++)
                    if (WidthHandles(path, b))
                        return;

                if (DoHeight(path))
                    return;

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    for (int b = 0; b < branches.Count; b++)
                        if (branches[b]?.path != null && BranchHandles(b).SelectInRect(rect, branches[b].path))
                            break;
            }

            UnityEditor.Tools.hidden = anySelected;
            SetSuspended(path, UnityEditor.Tools.hidden);
        }

        private bool DoBranch(TsitskiProBuilderPath path, int index) {
            PathBranch branch = path.activeBranches[index];
            if (branch?.path is not { Count: > 0 })
                return false;

            path.SyncWidths(branch);
            PathEdit edit = BranchHandles(index).Do(branch.path, branch.widths, false, path.transform.rotation);
            if (edit == null)
                return false;

            if (edit.deletePath) {
                Change(path, "Delete Path Branch", () => path.activeBranches = path.activeBranches.Where((_, i) => i != index).ToList());
                _branches.RemoveAt(index);
                return true;
            }

            List<float> widths = edit.radii;
            for (int i = 0; i < widths.Count; i++) {
                if (widths[i] > 0f)
                    continue;
                float previous = i > 0 ? widths[i - 1] : 0f, next = i + 1 < widths.Count ? widths[i + 1] : 0f;
                widths[i] = previous > 0f && next > 0f ? (previous + next) * 0.5f : Mathf.Max(previous, next, path.activeWidth);
            }

            List<Vector3> points = edit.points;
            foreach (int i in BranchHandles(index).selected.Where(i => i < points.Count))
                points[i] = Snap(path, index, i, points);

            SetSuspended(path, anySelected);
            Change(path, $"{edit.undoName} (Path)", () => {
                branch.path = points;
                branch.widths = widths;
            });
            return true;
        }

        private static Vector3 Snap(TsitskiProBuilderPath path, int branchIndex, int pointIndex, List<Vector3> points) {
            Vector3 point = points[pointIndex];
            float distance = UnityEditor.HandleUtility.GetHandleSize(point) * SnapScreenSize;
            Vector3 best = point;
            float bestDistance = distance;

            List<PathBranch> branches = path.activeBranches;
            for (int b = 0; b < branches.Count; b++) {
                List<Vector3> candidates = b == branchIndex ? points : branches[b]?.path;
                if (candidates == null)
                    continue;
                for (int i = 0; i < candidates.Count; i++) {
                    if (b == branchIndex && (Mathf.Abs(i - pointIndex) <= 1 || !IsEnd(i, candidates.Count) || !IsEnd(pointIndex, candidates.Count)))
                        continue;
                    float d = Vector3.Distance(point, candidates[i]);
                    if (d < bestDistance) {
                        bestDistance = d;
                        best = candidates[i];
                    }
                }
            }

            if (best != point || !IsEnd(pointIndex, points.Count))
                return best;

            List<List<TsitskiProBuilderPath.PathSample>> curves = branches.Select(b => b?.path is { Count: >= 2 }
                ? path.SampleBranch(b, out _)
                : new List<TsitskiProBuilderPath.PathSample>()).ToList();
            if (path.ClosestOnCurve(curves, branchIndex, point, out _, out _, out TsitskiProBuilderPath.PathSample hit) &&
                Vector3.Distance(point, hit.center) < distance)
                return hit.center;

            return best;
        }

        private static bool IsEnd(int index, int count) => index == 0 || index == count - 1;

        private bool WidthHandles(TsitskiProBuilderPath path, int index) {
            PathBranch branch = path.activeBranches[index];
            if (branch?.path is not { Count: >= 2 })
                return false;

            PathPointHandles handles = BranchHandles(index);
            for (int i = 0; i < branch.path.Count; i++) {
                float pointWidth = path.PointWidth(branch, i);
                if (!SliderHandles.Width(branch.path[i], PointSide(branch.path, i), ref pointWidth, HandleTheme.Secondary, TsitskiProBuilderPath.MinWidth))
                    continue;

                if (path.uniformWidth) {
                    TsitskiProBuilderPathStats stats = path.widthInStats ? path.stats : null;
                    Undo.RecordObject(stats ? stats : path, "Path Width");
                    if (stats) {
                        stats.width = pointWidth;
                        EditorUtility.SetDirty(stats);
                        RebuildQueue.Run(stats, stats.NotifyChanged);
                    }
                    else
                        path.width = pointWidth;
                    RebuildQueue.Run(path, path.Rebuild);
                    return true;
                }

                IEnumerable<int> indices = handles.selected.Contains(i) ? handles.selected.ToList() : new List<int> { i };
                Change(path, "Path Width", () => {
                    path.SyncWidths(branch);
                    foreach (int k in indices.Where(k => k < branch.widths.Count))
                        branch.widths[k] = pointWidth;
                });
                return true;
            }

            return false;
        }

        private static bool DoHeight(TsitskiProBuilderPath path) {
            PathBranch first = path.activeBranches.FirstOrDefault(b => b?.path is { Count: > 0 });
            if (first == null)
                return false;

            float value = path.activeHeight;
            if (!SliderHandles.SignedLength(first.path[0], Vector3.up, ref value, HandleTheme.Secondary, TsitskiProBuilderPath.MinHeight))
                return false;

            TsitskiProBuilderPathStats stats = path.heightInStats ? path.stats : null;
            Undo.RecordObject(stats ? stats : path, "Path Height");
            if (stats) {
                stats.height = value;
                EditorUtility.SetDirty(stats);
                RebuildQueue.Run(stats, stats.NotifyChanged);
            }
            else
                path.height = value;

            RebuildQueue.Run(path, path.Rebuild);
            return true;
        }

        private void DoClipboard(TsitskiProBuilderPath path) {
            Event e = Event.current;
            bool execute = e.type == EventType.ExecuteCommand;
            if (!execute && e.type != EventType.ValidateCommand)
                return;

            List<PathBranch> branches = path.activeBranches;
            if (PointClipboard.IsCopy(e)) {
                for (int b = 0; b < branches.Count && b < _branches.Count; b++) {
                    List<Vector3> points = branches[b]?.path;
                    int selected = _branches[b].selected.Count;
                    if (points == null || selected == 0 || selected != points.Count)
                        continue;

                    e.Use();
                    if (execute) {
                        path.SyncWidths(branches[b]);
                        PointClipboard.CopyPath(points, branches[b].Copy());
                    }
                    return;
                }
                return;
            }

            if (!PointClipboard.IsPaste(e) || !PointClipboard.HasPath<PathBranch>())
                return;

            e.Use();
            if (!execute)
                return;

            PathBranch pasted = PointClipboard.PastePath<PathBranch>();
            if (pasted?.path is not { Count: >= 2 })
                return;

            InsertBranch(path, "Paste Path Branch", pasted);
            SelectOnly(path.activeBranches.Count - 1, pasted.path.Count, Enumerable.Range(0, pasted.path.Count));
        }
    }

    [InitializeOnLoad]
    internal static class ProBuilderPathWatcher {
        static ProBuilderPathWatcher() => Undo.undoRedoPerformed += () => {
            foreach (TsitskiProBuilderPath path in TsitskiProBuilderPath.All.ToArray())
                if (path)
                    RebuildQueue.Queue(path, path.Rebuild, true);
        };
    }
}
