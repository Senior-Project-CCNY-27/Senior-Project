using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;
using Object = UnityEngine.Object;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderRoof))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderRoofEditor : UnityEditor.Editor, ITsitskiPathSplitter {
        private static readonly int RectSelectHash = "ProBuilderRoofRectSelect".GetHashCode();

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private readonly List<PathPointHandles> _paths = new();
        private readonly List<PathPointHandles> _profiles = new();

        private IEnumerable<PathPointHandles> allHandles => _paths.Concat(_profiles);
        private bool anySelected => allHandles.Any(h => h.selected.Count > 0);

        private void OnDisable() {
            UnityEditor.Tools.hidden = false;
            SetSuspended(target as TsitskiProBuilderRoof, false);
        }

        private int SelectedPath(TsitskiProBuilderRoof roof) {
            if (_profiles.Any(h => h.selected.Count > 0))
                return -1;
            for (int i = 0; i < _paths.Count && i < roof.paths.Count; i++)
                if (_paths[i].selected.Count > 0 && roof.paths[i]?.path != null)
                    return i;
            return -1;
        }

        bool ITsitskiPathSplitter.CanSplitPath() {
            if (target is not TsitskiProBuilderRoof roof || roof.pathsInStats || roof.isGenerated)
                return false;
            int index = SelectedPath(roof);
            if (index < 0)
                return false;
            RoofPath roofPath = roof.paths[index];
            return TsitskiPathSplit.CanSplitPieces(roofPath.path.Count, roofPath.closed && roofPath.path.Count >= 3, _paths[index].selected, roof.paths.Count, 2);
        }

        void ITsitskiPathSplitter.SplitPath() {
            TsitskiProBuilderRoof roof = (TsitskiProBuilderRoof)target;
            int index = SelectedPath(roof);
            if (index < 0)
                return;

            RoofPath original = roof.paths[index].Copy();
            List<float> radii = PointRounding.Synced(original.pathRadii, original.path.Count);
            RoofPath Piece(List<int> indices) {
                RoofPath piece = original.Copy();
                piece.path = TsitskiPathSplit.Pick(original.path, indices);
                if (indices.Count == original.path.Count)
                    return piece;
                piece.pathRadii = TsitskiPathSplit.PieceRadii(indices, radii);
                piece.closed = false;
                return piece;
            }

            TsitskiPathSplit.SplitPieces(roof, original.path.Count, original.closed && original.path.Count >= 3, _paths[index].selected.ToList(), roof.paths.Count, 2,
                TsitskiPathSplit.Duplicate,
                (created, indices) => {
                    created.paths = new List<RoofPath> { Piece(indices) };
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
            PathPointHandles handles = new(Repaint) { pathColor = color };
            handles.Selected += () => {
                foreach (PathPointHandles other in allHandles)
                    if (other != handles)
                        other.selected.Clear();
            };
            return handles;
        }

        private PathPointHandles PathHandles(int index) {
            while (_paths.Count <= index) {
                PathPointHandles handles = CreateHandles(HandleTheme.Path);
                handles.deletesPath = true;
                handles.openMinimum = 2;
                _paths.Add(handles);
            }
            return _paths[index];
        }

        private PathPointHandles ProfileHandles(int index) {
            while (_profiles.Count <= index)
                _profiles.Add(CreateHandles(HandleTheme.Secondary));
            return _profiles[index];
        }

        private static void SetSuspended(TsitskiProBuilderRoof roof, bool suspended) {
            if (roof && roof.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderRoof.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            bool pathsInStats = usesStats && ((TsitskiProBuilderRoof)target).pathsInStats;

            TsitskiInspector.DrawProperties(serializedObject, name =>
                usesStats && name is nameof(TsitskiProBuilderRoof.profile) or nameof(TsitskiProBuilderRoof.profileRadii)
                    or nameof(TsitskiProBuilderRoof.roundingSegments) or nameof(TsitskiProBuilderRoof.thickness)
                    or nameof(TsitskiProBuilderRoof.smoothingAngle) or nameof(TsitskiProBuilderRoof.fillRoof)
                    or nameof(TsitskiProBuilderRoof.generateGableWalls) or nameof(TsitskiProBuilderRoof.gableWallIndent)
                    or nameof(TsitskiProBuilderRoof.gableWallThickness) or nameof(TsitskiProBuilderRoof.material) ||
                pathsInStats && name == nameof(TsitskiProBuilderRoof.paths));

            if (serializedObject.isEditingMultipleObjects) {
                if (GUILayout.Button("Rebuild"))
                    foreach (TsitskiProBuilderRoof each in targets.Cast<TsitskiProBuilderRoof>())
                        each.Rebuild();
                return;
            }

            TsitskiProBuilderRoof roof = (TsitskiProBuilderRoof)target;
            if (roof.isGenerated)
                EditorGUILayout.HelpBox("Generated by its floor plan, which sets the footprint. Only the first path's Width and Height are used, for the slope.",
                    MessageType.Info);
            if (roof.failedRegions > 0)
                EditorGUILayout.HelpBox($"{roof.failedRegions} part(s) of the roof couldn't be built and were left out. Nudge the paths or widths slightly.",
                    MessageType.Warning);
            if (roof.separatedPaths.Count > 0)
                EditorGUILayout.HelpBox($"Path {string.Join(", ", roof.separatedPaths.Select(i => i + 1))} couldn't be joined to the rest of the roof cleanly, " +
                                        "so it's built as its own roof. Nudging its points, width or height may let it join.", MessageType.Info);
            DrawProfilePreview(roof.ProfileCurve(), RoofProfilePoints(roof.activeProfile,
                edited => Edit(roof, roof.profileOwner, "Edit Roof Profile", () => roof.activeProfile = edited), roof.activeProfileRadii,
                radii => Edit(roof, roof.profileOwner, "Roof Profile Radius", () => roof.activeProfileRadii = radii)));

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Add Path"))
                    AddPath(roof);
                if (!roof.stats && GUILayout.Button("Save As Roof Stats…"))
                    SaveStats(roof);
                if (roof.stats && GUILayout.Button("Copy Stats Into Roof")) {
                    Undo.RecordObject(roof, "Copy Roof Stats");
                    CopyFromStats(roof);
                    roof.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    roof.Rebuild();
            }
        }

        internal static GraphPoints RoofProfilePoints(List<Vector2> profile, Action<List<Vector2>> apply, List<float> radii,
            Action<List<float>> applyRadii) {
            int last = profile.Count - 1;
            return new GraphPoints {
                points = profile,
                fromGraph = (p, i) => i == 0 ? Vector2.zero : i == last ? Vector2.one : p,
                apply = apply,
                radii = radii,
                applyRadii = applyRadii,
                canDelete = i => i != 0 && i != last
            };
        }

        internal static void DrawProfilePreview(List<Vector2> curve, GraphPoints editable = null) {
            List<Vector2> mirrored = curve.Select(p => new Vector2(2f - p.x, p.y)).Reverse().ToList();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                ProfilePreview.Graph(new List<ProfileLine> {
                    new() { points = curve, color = ProfilePreview.Outline },
                    new() { points = mirrored, color = HandleTheme.Secondary }
                }, null, 120f, editable == null ? null : new[] { editable });
                EditorGUILayout.LabelField("Eave (0, 0) to ridge (1, 1), stretched to each path's half width and height.", EditorStyles.miniLabel);
            }
        }

        private static void CopyFromStats(TsitskiProBuilderRoof roof) {
            TsitskiProBuilderRoofStats stats = roof.stats;
            roof.profile = new List<Vector2>(stats.profile);
            roof.profileRadii = new List<float>(stats.profileRadii);
            roof.roundingSegments = stats.roundingSegments;
            roof.thickness = stats.thickness;
            roof.smoothingAngle = stats.smoothingAngle;
            roof.fillRoof = stats.fillRoof;
            roof.generateGableWalls = stats.generateGableWalls;
            roof.gableWallIndent = stats.gableWallIndent;
            roof.gableWallThickness = stats.gableWallThickness;
            roof.material = stats.material;
            if (stats.savePaths)
                roof.paths = stats.paths.Select(p => p?.Copy()).ToList();
            roof.stats = null;
        }

        private static void AddPath(TsitskiProBuilderRoof roof) {
            List<Vector2> all = roof.footprint.SelectMany(l => l).ToList();
            Vector2 start = all.Count > 0 ? new Vector2(all.Max(p => p.x) + 3f, all.Min(p => p.y) + 2f) : Vector2.zero;
            float y = roof.baseHeight;
            RoofPath last = roof.activePaths.LastOrDefault();

            Edit(roof, roof.pathsOwner, "Add Roof Path", () => roof.activePaths = new List<RoofPath>(roof.activePaths) {
                new() {
                    width = last?.width ?? 4f, height = last?.height ?? 1.5f,
                    path = new List<Vector3> { new(start.x, y, start.y), new(start.x + 6f, y, start.y) }
                }
            });
        }

        private static void SaveStats(TsitskiProBuilderRoof roof) {
            string path = StatsSavePanel.Show("Save Roof Stats", "Roof Stats", "Save this roof's profile and thickness as a reusable Roof Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            TsitskiProBuilderRoofStats asset = CreateInstance<TsitskiProBuilderRoofStats>();
            asset.profile = new List<Vector2>(roof.profile);
            asset.profileRadii = new List<float>(roof.profileRadii);
            asset.roundingSegments = roof.roundingSegments;
            asset.thickness = roof.thickness;
            asset.smoothingAngle = roof.smoothingAngle;
            asset.fillRoof = roof.fillRoof;
            asset.generateGableWalls = roof.generateGableWalls;
            asset.gableWallIndent = roof.gableWallIndent;
            asset.gableWallThickness = roof.gableWallThickness;
            asset.material = roof.material;
            asset.paths = roof.paths.Select(p => p?.Copy()).ToList();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(roof, "Assign Roof Stats");
            roof.stats = asset;
            EditorUtility.SetDirty(roof);
        }

        private static void Edit(TsitskiProBuilderRoof roof, Object owner, string undoName, Action change) {
            Undo.RecordObject(owner, undoName);
            change();
            EditorUtility.SetDirty(owner);
            if (owner is TsitskiProBuilderRoofStats stats)
                RebuildQueue.Run(stats, stats.NotifyChanged);
            RebuildQueue.Run(roof, roof.Rebuild);
        }

        private void OnSceneGUI() {
            TsitskiProBuilderRoof roof = (TsitskiProBuilderRoof)target;
            bool editing = roof && !roof.isGenerated && Selection.gameObjects.Length == 1;

            if (!editing) {
                foreach (PathPointHandles handles in allHandles)
                    handles.selected.Clear();
                UnityEditor.Tools.hidden = false;
                SetSuspended(roof, false);
                return;
            }

            if (anySelected)
                SetSuspended(roof, true);

            DoPathClipboard(roof);

            List<RoofPath> paths = roof.activePaths;
            if (_paths.Count > paths.Count)
                _paths.RemoveRange(paths.Count, _paths.Count - paths.Count);
            if (_profiles.Count > paths.Count)
                _profiles.RemoveRange(paths.Count, _profiles.Count - paths.Count);

            using (new Handles.DrawingScope(Color.white, roof.transform.localToWorldMatrix)) {
                DrawFootprint(roof);

                for (int index = 0; index < paths.Count; index++)
                    if (DoPathGizmos(roof, index))
                        return;

                for (int index = 0; index < paths.Count; index++)
                    if (DoProfile(roof, index))
                        return;

                foreach (int index in Enumerable.Range(0, paths.Count).OrderBy(i => PathHandles(i).selected.Count > 0))
                    if (DoPath(roof, index))
                        return;

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    for (int index = 0; index < paths.Count; index++)
                        if (paths[index]?.path != null && PathHandles(index).SelectInRect(rect, paths[index].path))
                            break;
            }

            UnityEditor.Tools.hidden = anySelected;
            SetSuspended(roof, UnityEditor.Tools.hidden);
        }

        private void DoPathClipboard(TsitskiProBuilderRoof roof) {
            Event e = Event.current;
            bool execute = e.type == EventType.ExecuteCommand;
            if (!execute && e.type != EventType.ValidateCommand)
                return;

            if (PointClipboard.IsCopy(e)) {
                List<RoofPath> paths = roof.activePaths;
                for (int index = 0; index < paths.Count; index++) {
                    int selected = PathHandles(index).selected.Count;
                    if (paths[index]?.path == null || selected == 0 || selected != paths[index].path.Count)
                        continue;

                    e.Use();
                    if (execute)
                        PointClipboard.CopyPath(paths[index].path, paths[index]);
                    return;
                }
                return;
            }

            if (!PointClipboard.IsPaste(e) || !PointClipboard.HasPath<RoofPath>())
                return;

            e.Use();
            if (!execute)
                return;

            RoofPath pasted = PointClipboard.PastePath<RoofPath>();
            if (pasted?.path is not { Count: > 0 })
                return;

            float y = roof.baseHeight;
            pasted.path = pasted.path.Select(p => new Vector3(p.x, y, p.z)).ToList();
            Edit(roof, roof.pathsOwner, "Paste Roof Path", () => roof.activePaths = new List<RoofPath>(roof.activePaths) { pasted });
            PathHandles(roof.activePaths.Count - 1).SelectAll(pasted.path.Count);
        }

        private static void DrawFootprint(TsitskiProBuilderRoof roof) {
            float y = roof.baseHeight;
            Handles.color = HandleTheme.Secondary;
            foreach (List<Vector2> loop in roof.footprint.Where(l => l.Count >= 3)) {
                Vector3[] points = loop.Select(p => new Vector3(p.x, y, p.y)).Append(new Vector3(loop[0].x, y, loop[0].y)).ToArray();
                Handles.DrawAAPolyLine(2f, points);
            }
        }

        private static bool Frame(TsitskiProBuilderRoof roof, RoofPath roofPath, out Vector3 center, out Vector3 along, out Vector3 across) {
            center = along = across = Vector3.zero;
            if (roofPath?.path is not { Count: >= 2 })
                return false;

            int count = roofPath.closed ? roofPath.path.Count : roofPath.path.Count - 1;
            int longest = Enumerable.Range(0, count).OrderByDescending(i => (roofPath.path[(i + 1) % roofPath.path.Count] - roofPath.path[i]).sqrMagnitude).First();
            Vector3 a = roofPath.path[longest], b = roofPath.path[(longest + 1) % roofPath.path.Count];
            along = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
            if (along == Vector3.zero)
                return false;

            across = new Vector3(-along.z, 0f, along.x);
            center = (a + b) * 0.5f;
            center.y = roof.baseHeight;
            return true;
        }

        private bool DoPathGizmos(TsitskiProBuilderRoof roof, int index) {
            RoofPath roofPath = roof.activePaths[index];
            if (!Frame(roof, roofPath, out Vector3 center, out _, out Vector3 across))
                return false;

            float half = roofPath.width * 0.5f;
            if (SliderHandles.Length(center, -across, ref half, HandleTheme.Secondary, 0.005f, Handles.DotHandleCap)) {
                Edit(roof, roof.pathsOwner, "Roof Width", () => roofPath.width = half * 2f);
                return true;
            }

            if (roofPath.closed)
                return false;

            return DoGable(roof, roofPath, true) || DoGable(roof, roofPath, false);
        }

        private static bool DoGable(TsitskiProBuilderRoof roof, RoofPath roofPath, bool start) {
            List<Vector3> points = roofPath.path;
            Vector3 end = start ? points[0] : points[^1];
            Vector3 outward = Vector3.ProjectOnPlane(start ? points[0] - points[1] : points[^1] - points[^2], Vector3.up).normalized;
            if (outward == Vector3.zero)
                return false;

            end.y = roof.baseHeight;
            Vector3 across = new(-outward.z, 0f, outward.x);
            bool gable = start ? roofPath.gableStart : roofPath.gableEnd;
            float half = roofPath.width * 0.5f;

            Handles.color = gable ? HandleTheme.Path : HandleTheme.Secondary;
            if (gable)
                Handles.DrawAAPolyLine(4f, end - across * half, end + across * half);
            else
                Handles.DrawDottedLine(end - across * half, end + across * half, 3f);

            Vector3 marker = end + outward * (UnityEditor.HandleUtility.GetHandleSize(end) * 0.35f);
            bool toggled = SliderHandles.Toggle(marker, outward, gable, HandleTheme.Path, HandleTheme.Secondary);
            if (toggled == gable)
                return false;

            Edit(roof, roof.pathsOwner, toggled ? "Make Gable End" : "Make Hip End", () => {
                if (start)
                    roofPath.gableStart = toggled;
                else
                    roofPath.gableEnd = toggled;
            });
            return true;
        }

        private bool DoProfile(TsitskiProBuilderRoof roof, int index) {
            RoofPath roofPath = roof.activePaths[index];
            if (!Frame(roof, roofPath, out Vector3 center, out _, out Vector3 across))
                return false;

            float half = roofPath.width * 0.5f, height = roofPath.height;
            Vector3 eave = center + across * half;
            Vector3 ToScene(Vector2 p) => eave - across * (p.x * half) + Vector3.up * (p.y * height);

            List<Vector2> curve = roof.ProfileCurve();
            Handles.color = HandleTheme.Secondary;
            Handles.DrawAAPolyLine(2f, curve.Select(p => center - across * half + across * (p.x * half) + Vector3.up * (p.y * height)).ToArray());

            List<Vector2> source = Normalized(roof.activeProfile);
            List<Vector3> scenePoints = source.Select(ToScene).ToList();
            PathEdit edit = ProfileHandles(index).Do(scenePoints, roof.activeProfileRadii, false, roof.transform.rotation);
            if (edit == null || edit.points.Count < 2)
                return false;

            if (edit.points.Count == scenePoints.Count && (edit.points[0] - scenePoints[0]).sqrMagnitude > 1e-10f) {
                float width = Mathf.Max(0.01f, Vector3.Dot(edit.points[0] - center, across) * 2f);
                Edit(roof, roof.pathsOwner, "Roof Width", () => roofPath.width = width);
                return true;
            }

            if (edit.points.Count == scenePoints.Count && (edit.points[^1] - scenePoints[^1]).sqrMagnitude > 1e-10f) {
                float ridge = Mathf.Max(0.01f, Vector3.Dot(edit.points[^1] - center, Vector3.up));
                Edit(roof, roof.pathsOwner, "Roof Height", () => roofPath.height = ridge);
                return true;
            }

            List<Vector2> edited = edit.points.Select(p => new Vector2(Vector3.Dot(p - eave, -across) / half, Vector3.Dot(p - eave, Vector3.up) / height)).ToList();
            edited[0] = Vector2.zero;
            edited[^1] = Vector2.one;

            Edit(roof, roof.profileOwner, $"{edit.undoName} (Roof Profile)", () => {
                roof.activeProfile = edited;
                roof.activeProfileRadii = edit.radii;
            });
            return true;
        }

        private static List<Vector2> Normalized(List<Vector2> profile) {
            if (profile is not { Count: >= 2 })
                return TsitskiProBuilderRoof.DefaultProfile();

            Vector2 first = profile[0], size = profile[^1] - first;
            if (size.x <= 1e-5f || size.y <= 1e-5f)
                return new List<Vector2>(profile);
            return profile.Select(p => new Vector2((p.x - first.x) / size.x, (p.y - first.y) / size.y)).ToList();
        }

        private bool DoPath(TsitskiProBuilderRoof roof, int index) {
            RoofPath roofPath = roof.activePaths[index];
            if (roofPath?.path is not { Count: > 0 })
                return false;

            PathEdit edit = PathHandles(index).Do(roofPath.path, roofPath.pathRadii, roofPath.closed, roof.transform.rotation);
            if (edit == null)
                return false;

            if (edit.deletePath) {
                Edit(roof, roof.pathsOwner, "Delete Roof Path", () => roof.activePaths = roof.activePaths.Where((_, i) => i != index).ToList());
                _paths.RemoveAt(index);
                if (index < _profiles.Count)
                    _profiles.RemoveAt(index);
                return true;
            }

            float y = roof.baseHeight;
            SetSuspended(roof, true);
            Edit(roof, roof.pathsOwner, $"{edit.undoName} (Roof)", () => {
                roofPath.path = edit.points.Select(p => new Vector3(p.x, y, p.z)).ToList();
                roofPath.pathRadii = edit.radii;
            });
            return true;
        }
    }

    [CustomEditor(typeof(TsitskiProBuilderRoofStats))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderRoofStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            TsitskiInspector.DrawProperties(serializedObject);

            if (serializedObject.isEditingMultipleObjects)
                return;

            TsitskiProBuilderRoofStats stats = (TsitskiProBuilderRoofStats)target;
            TsitskiProBuilderRoofEditor.DrawProfilePreview(TsitskiProBuilderRoof.ProfileCurve(stats.profile, stats.profileRadii, stats.roundingSegments),
                TsitskiProBuilderRoofEditor.RoofProfilePoints(stats.profile, edited => {
                    Undo.RecordObject(stats, "Edit Roof Profile");
                    stats.profile = edited;
                    EditorUtility.SetDirty(stats);
                    stats.NotifyChanged();
                }, stats.profileRadii, radii => {
                    Undo.RecordObject(stats, "Roof Profile Radius");
                    stats.profileRadii = radii;
                    EditorUtility.SetDirty(stats);
                    stats.NotifyChanged();
                }));
        }
    }

    [InitializeOnLoad]
    internal static class ProBuilderRoofWatcher {
        static ProBuilderRoofWatcher() => Undo.undoRedoPerformed += () => {
            foreach (TsitskiProBuilderRoof roof in TsitskiProBuilderRoof.All.ToArray())
                if (roof)
                    RebuildQueue.Queue(roof, roof.Rebuild, true);
        };
    }
}
