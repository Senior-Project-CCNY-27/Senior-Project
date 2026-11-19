using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProBuilderMesh = UnityEngine.ProBuilder.ProBuilderMesh;
using Part = Tsitski.ProBuilderTools.TsitskiProBuilderPillar.Part;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderPillar))]
    [CanEditMultipleObjects]
    public class TsitskiProBuilderPillarEditor : UnityEditor.Editor {
        private static readonly int RectSelectHash = "ProBuilderPillarRectSelect".GetHashCode();
        private static readonly Color SplitColor = new(1f, 1f, 1f, 0.5f);
        private static readonly Part[] Parts = { Part.Bottom, Part.Middle, Part.Top };
        private const float Epsilon = 1e-5f;
        private const float MinShownSpan = 0.1f;

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PathPointHandles[] _sections;
        private float _dragSquish;
        private bool _rectSelecting;
        private Quaternion _multiRotation = Quaternion.identity;
        private Vector3 _multiScale = Vector3.one;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private List<Vector2> _dragPoints;
        private Part _dragPart;
        private float _dragSpan;

        private bool anySelected => _sections.Any(s => s.selected.Count > 0);

        private void OnEnable() {
            _sections = Parts.Select(part => new PathPointHandles(Repaint) { pathColor = PillarProfilePreview.PartColor(part) }).ToArray();
            foreach (PathPointHandles section in _sections)
                section.Selected += () => {
                    if (_rectSelecting || Event.current is { shift: true })
                        return;
                    foreach (PathPointHandles other in _sections)
                        if (other != section)
                            other.selected.Clear();
                };
        }

        private void OnDisable() {
            Tools.hidden = false;
            SetSuspended(target as TsitskiProBuilderPillar, false);
        }

        private static void SetSuspended(TsitskiProBuilderPillar pillar, bool suspended) {
            if (pillar && pillar.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderPillar.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            SerializedProperty shapeProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderPillar.shape));
            bool mixedShape = shapeProperty.hasMultipleDifferentValues;
            bool corner = !mixedShape && shapeProperty.enumValueIndex == (int)TsitskiProBuilderPillar.Shape.Corner;
            if (usesStats && !serializedObject.isEditingMultipleObjects) {
                mixedShape = false;
                corner = ((TsitskiProBuilderPillarStats)statsProperty.objectReferenceValue).shape == TsitskiProBuilderPillar.Shape.Corner;
            }
            SerializedProperty snapProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderPillar.autoSnapToPath));
            bool snaps = snapProperty.hasMultipleDifferentValues || snapProperty.boolValue;
            if (usesStats && !serializedObject.isEditingMultipleObjects)
                snaps = ((TsitskiProBuilderPillarStats)statsProperty.objectReferenceValue).autoSnapToPath;
            SerializedProperty stretchProperty = serializedObject.FindProperty(nameof(TsitskiProBuilderPillar.stretchCenter));
            bool stretches = !stretchProperty.hasMultipleDifferentValues && stretchProperty.boolValue;

            TsitskiInspector.Headers headers = new();
            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                headers.Next(property);
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name is nameof(TsitskiProBuilderPillar.shape) or nameof(TsitskiProBuilderPillar.autoSnapToPath) or nameof(TsitskiProBuilderPillar.snapToFlats)
                        or nameof(TsitskiProBuilderPillar.generateDownwards)
                        or nameof(TsitskiProBuilderPillar.cornerLength) or nameof(TsitskiProBuilderPillar.roundBevel) && usesStats)
                    continue;
                if (property.name is nameof(TsitskiProBuilderPillar.width) or nameof(TsitskiProBuilderPillar.bottomProfile) or nameof(TsitskiProBuilderPillar.centerProfile)
                        or nameof(TsitskiProBuilderPillar.topProfile) or nameof(TsitskiProBuilderPillar.bottomHeight) or nameof(TsitskiProBuilderPillar.topHeight)
                        or nameof(TsitskiProBuilderPillar.stretchCenter) or nameof(TsitskiProBuilderPillar.centerHeight)
                        or nameof(TsitskiProBuilderPillar.sides) or nameof(TsitskiProBuilderPillar.roundingSegments)
                        or nameof(TsitskiProBuilderPillar.smoothingAngle) or nameof(TsitskiProBuilderPillar.material) && usesStats)
                    continue;
                if (property.name is nameof(TsitskiProBuilderPillar.cornerSide) or nameof(TsitskiProBuilderPillar.cornerLength) or nameof(TsitskiProBuilderPillar.roundBevel)
                        or nameof(TsitskiProBuilderPillar.cornerStart) or nameof(TsitskiProBuilderPillar.cornerEnd) && !corner && !mixedShape)
                    continue;
                if (property.name == nameof(TsitskiProBuilderPillar.snapDistance) && !snaps)
                    continue;
                if (property.name == nameof(TsitskiProBuilderPillar.sides) && corner)
                    continue;
                if (property.name == nameof(TsitskiProBuilderPillar.centerHeight) && stretches)
                    continue;

                if (TsitskiInspector.HiddenByCondition(property))
                    continue;

                headers.BeforePropertyField(property);
                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (serializedObject.isEditingMultipleObjects)
                return;

            TsitskiProBuilderPillar pillar = (TsitskiProBuilderPillar)target;

            if (GUILayout.Button(new GUIContent("Snap To Path",
                    "Move the pillar to the nearest floor plan, trim, array or stairs corner (or straight part, with Snap To Flats on).")))
                Snap(pillar, float.MaxValue);

            PillarProfilePreview.Draw(pillar.stats ? (Object)pillar.stats : pillar, pillar.ProfileLayout(), pillar.activeBottomProfile,
                pillar.activeCenterProfile, pillar.activeTopProfile, pillar.activeRoundingSegments, () => Changed(pillar));

            using (new EditorGUILayout.HorizontalScope()) {
                if (!pillar.stats && GUILayout.Button("Save As Pillar Stats…"))
                    SaveStats(pillar);
                if (pillar.stats && GUILayout.Button("Copy Stats Into Pillar")) {
                    Undo.RecordObject(pillar, "Copy Pillar Stats");
                    TsitskiProBuilderPillarStats stats = pillar.stats;
                    pillar.shape = stats.shape;
                    pillar.autoSnapToPath = stats.autoSnapToPath;
                    pillar.snapToFlats = stats.snapToFlats;
                    pillar.generateDownwards = stats.generateDownwards;
                    pillar.cornerLength = stats.cornerLength;
                    pillar.roundBevel = stats.roundBevel;
                    pillar.bottomProfile = stats.bottomProfile.Copy();
                    pillar.centerProfile = stats.centerProfile.Copy();
                    pillar.topProfile = stats.topProfile.Copy();
                    pillar.bottomHeight = stats.bottomHeight;
                    pillar.topHeight = stats.topHeight;
                    pillar.stretchCenter = stats.stretchCenter;
                    pillar.centerHeight = stats.centerHeight;
                    pillar.sides = stats.sides;
                    pillar.material = stats.material;
                    pillar.roundingSegments = stats.roundingSegments;
                    pillar.smoothingAngle = stats.smoothingAngle;
                    pillar.stats = null;
                    pillar.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    pillar.Rebuild();
            }
        }

        private static void Changed(TsitskiProBuilderPillar pillar) {
            if (pillar.stats) {
                EditorUtility.SetDirty(pillar.stats);
                RebuildQueue.Run(pillar.stats, pillar.stats.NotifyChanged);
                return;
            }

            RebuildQueue.Run(pillar, pillar.Rebuild);
        }

        private static void SaveStats(TsitskiProBuilderPillar pillar) {
            string path = StatsSavePanel.Show("Save Pillar Stats", "Pillar Stats",
                "Save this pillar's profiles and settings as a reusable Pillar Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            TsitskiProBuilderPillarStats asset = CreateInstance<TsitskiProBuilderPillarStats>();
            asset.shape = pillar.shape;
            asset.autoSnapToPath = pillar.autoSnapToPath;
            asset.snapToFlats = pillar.snapToFlats;
            asset.generateDownwards = pillar.generateDownwards;
            asset.cornerLength = pillar.cornerLength;
            asset.roundBevel = pillar.roundBevel;
            asset.bottomProfile = pillar.bottomProfile.Copy();
            asset.centerProfile = pillar.centerProfile.Copy();
            asset.topProfile = pillar.topProfile.Copy();
            asset.bottomHeight = pillar.bottomHeight;
            asset.topHeight = pillar.topHeight;
            asset.stretchCenter = pillar.stretchCenter;
            asset.centerHeight = pillar.centerHeight;
            asset.sides = pillar.sides;
            asset.roundingSegments = pillar.roundingSegments;
            asset.smoothingAngle = pillar.smoothingAngle;
            asset.material = pillar.material;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(pillar, "Assign Pillar Stats");
            pillar.stats = asset;
            EditorUtility.SetDirty(pillar);
        }

        private static PillarProfile Section(TsitskiProBuilderPillar pillar, Part part) => part switch {
            Part.Bottom => pillar.activeBottomProfile,
            Part.Middle => pillar.activeCenterProfile,
            _ => pillar.activeTopProfile
        };

        private static float NominalHeight(TsitskiProBuilderPillar pillar, Part part) => part switch {
            Part.Bottom => pillar.activeBottomHeight,
            Part.Middle => pillar.activeCenterHeight,
            _ => pillar.activeTopHeight
        };

        private static void ShownRange(TsitskiProBuilderPillar pillar, TsitskiProBuilderPillar.Layout layout, Part part, out float start, out float span) {
            start = layout.Start(part);
            span = layout.Span(part);
            if (span > Epsilon)
                return;

            span = Mathf.Max(MinShownSpan, NominalHeight(pillar, part));
            if (part == Part.Top)
                start = layout.height - span;
        }

        private void OnSceneGUI() {
            TsitskiProBuilderPillar pillar = (TsitskiProBuilderPillar)target;

            if (!pillar || Selection.gameObjects.Length != 1) {
                foreach (PathPointHandles section in _sections)
                    section.selected.Clear();
                Tools.hidden = false;
                SetSuspended(pillar, false);
                return;
            }

            if (anySelected)
                SetSuspended(pillar, true);

            Vector3 position = pillar.transform.position;
            if (pillar.activeAutoSnapToPath && _hasLastPosition && position != _lastPosition && GUIUtility.hotControl != 0)
                Snap(pillar, pillar.snapDistance);
            _lastPosition = pillar.transform.position;
            _hasLastPosition = true;

            bool corner = pillar.activeShape == TsitskiProBuilderPillar.Shape.Corner;
            if (corner && CornerHandles(pillar))
                return;

            TsitskiProBuilderPillar.Layout layout = pillar.ProfileLayout();
            Matrix4x4 frame = pillar.ProfileFrame();
            Quaternion rotation = pillar.transform.rotation * frame.rotation;

            using (new Handles.DrawingScope(Color.white, pillar.transform.localToWorldMatrix * frame)) {
                DrawSections(pillar, layout);

                List<Vector3>[] shown = new List<Vector3>[Parts.Length];
                float[] starts = new float[Parts.Length], spans = new float[Parts.Length];
                for (int i = 0; i < Parts.Length; i++) {
                    PillarProfile profile = Section(pillar, Parts[i]);
                    if (profile?.points == null)
                        continue;

                    ShownRange(pillar, layout, Parts[i], out starts[i], out spans[i]);
                    float start = starts[i], span = spans[i];
                    shown[i] = profile.points.Select(p => new Vector3(p.x, start + p.y * span, 0f)).ToList();
                }

                bool multi = _sections.Count(section => section.selected.Count > 0) > 1;
                foreach (PathPointHandles section in _sections)
                    section.transformHandles = !multi;

                if (multi && MultiCommands(pillar, shown, starts, spans))
                    return;

                for (int i = 0; i < Parts.Length; i++) {
                    PillarProfile profile = Section(pillar, Parts[i]);
                    if (shown[i] == null)
                        continue;

                    PathEdit edit = _sections[i].Do(shown[i], profile.radii, false, rotation);
                    if (edit == null)
                        continue;

                    if (GUIUtility.hotControl == 0)
                        ResetDrag(layout);
                    if (JointDrag(pillar, layout, i, profile, edit, shown[i]))
                        return;

                    ApplySection(pillar, Parts[i], profile, edit, ToSection(edit.points, shown[i], profile.points, starts[i], spans[i]));
                    return;
                }

                if (multi && MultiTransform(pillar, layout, shown, starts, spans, rotation))
                    return;

                float right = Parts.Select(part => Section(pillar, part)).Where(p => p?.points != null)
                    .SelectMany(p => p.points).Select(p => p.x).DefaultIfEmpty(0f).Max() + 0.15f;
                if (SectionHandles(pillar, layout, right))
                    return;

                bool downwards = pillar.activeGenerateDownwards;
                using (new Handles.DrawingScope(Color.white, pillar.transform.localToWorldMatrix * pillar.UnshiftedProfileFrame())) {
                    float height = pillar.height;
                    if (SliderHandles.Length(Vector3.zero, downwards ? Vector3.down : Vector3.up, ref height, HandleTheme.Secondary)) {
                        Undo.RecordObject(pillar, "Pillar Height");
                        pillar.height = height;
                        RebuildQueue.Run(pillar, pillar.Rebuild);
                    }
                }

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect)) {
                    if (!Event.current.shift)
                        foreach (PathPointHandles section in _sections)
                            section.selected.Clear();

                    _rectSelecting = true;
                    for (int i = 0; i < Parts.Length; i++)
                        if (shown[i] != null)
                            _sections[i].SelectInRect(rect, shown[i]);
                    _rectSelecting = false;
                }
            }

            SetSuspended(pillar, anySelected);
            Tools.hidden = anySelected;
        }

        private bool MultiCommands(TsitskiProBuilderPillar pillar, List<Vector3>[] shown, float[] starts, float[] spans) {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) {
                foreach (PathPointHandles section in _sections)
                    section.selected.Clear();
                e.Use();
                Repaint();
                return true;
            }

            bool command = e.type is EventType.ValidateCommand or EventType.ExecuteCommand && e.commandName is "Delete" or "SoftDelete";
            bool key = e.type == EventType.KeyDown && e.keyCode is KeyCode.Delete or KeyCode.Backspace;
            if (!command && !key)
                return false;

            e.Use();
            if (e.type == EventType.ValidateCommand)
                return true;

            Undo.RecordObject(pillar.stats ? (Object)pillar.stats : pillar, "Delete Pillar Profile Points");
            for (int i = 0; i < Parts.Length; i++) {
                HashSet<int> selected = _sections[i].selected;
                PillarProfile profile = Section(pillar, Parts[i]);
                if (selected.Count == 0 || shown[i] == null || profile.points.Count - selected.Count < 2)
                    continue;

                List<float> radii = PointRounding.Synced(profile.radii, profile.points.Count);
                profile.points = profile.points.Where((_, j) => !selected.Contains(j)).ToList();
                profile.radii = radii.Where((_, j) => !selected.Contains(j)).ToList();
                selected.Clear();
                KeepJoints(pillar, Parts[i]);
            }

            Changed(pillar);
            return true;
        }

        private bool MultiTransform(TsitskiProBuilderPillar pillar, TsitskiProBuilderPillar.Layout layout, List<Vector3>[] shown, float[] starts, float[] spans,
            Quaternion worldRotation) {
            Vector3 pivot = Vector3.zero;
            int count = 0;
            for (int i = 0; i < Parts.Length; i++) {
                if (shown[i] == null)
                    continue;
                foreach (int j in _sections[i].selected) {
                    pivot += shown[i][j];
                    count++;
                }
            }
            if (count == 0)
                return false;
            pivot /= count;

            Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Global ? Quaternion.Inverse(worldRotation) : Quaternion.identity;
            if (GUIUtility.hotControl == 0) {
                _multiRotation = handleRotation;
                _multiScale = Vector3.one;
            }

            System.Func<Vector3, Vector3> transform = null;
            switch (Tools.current) {
                case Tool.Rotate: {
                    EditorGUI.BeginChangeCheck();
                    Quaternion turned = Handles.RotationHandle(_multiRotation, pivot);
                    if (EditorGUI.EndChangeCheck()) {
                        Quaternion delta = turned * Quaternion.Inverse(_multiRotation);
                        _multiRotation = turned;
                        transform = p => pivot + delta * (p - pivot);
                    }
                    break;
                }
                case Tool.Scale: {
                    EditorGUI.BeginChangeCheck();
                    Vector3 scale = Handles.ScaleHandle(_multiScale, pivot, handleRotation, HandleUtility.GetHandleSize(pivot));
                    if (EditorGUI.EndChangeCheck() && Mathf.Abs(scale.x) > 1e-4f && Mathf.Abs(scale.y) > 1e-4f && Mathf.Abs(scale.z) > 1e-4f) {
                        Vector3 ratio = new(scale.x / _multiScale.x, scale.y / _multiScale.y, scale.z / _multiScale.z);
                        _multiScale = scale;
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
                return false;

            if (GUIUtility.hotControl == 0)
                ResetDrag(layout);
            if (JointBoundary(pillar, layout, shown) is { } boundary) {
                Undo.RecordObject(pillar.stats ? (Object)pillar.stats : pillar, "Move Pillar Section");
                float y = 0f;
                for (int i = 0; i < Parts.Length; i++) {
                    if (shown[i] == null)
                        continue;
                    PillarProfile profile = Section(pillar, Parts[i]);
                    foreach (int j in _sections[i].selected) {
                        Vector3 moved = transform(shown[i][j]);
                        profile.points[j] = new Vector2(moved.x, profile.points[j].y);
                        y = moved.y;
                    }
                    if (_sections[i].selected.Count > 0)
                        KeepJoints(pillar, Parts[i]);
                }

                return ResizeSection(pillar, layout, boundary, y);
            }

            Undo.RecordObject(pillar.stats ? (Object)pillar.stats : pillar, "Transform Pillar Profile Points");
            for (int i = 0; i < Parts.Length; i++) {
                if (shown[i] == null || _sections[i].selected.Count == 0)
                    continue;

                PillarProfile profile = Section(pillar, Parts[i]);
                List<Vector3> edited = new(shown[i]);
                foreach (int j in _sections[i].selected)
                    edited[j] = transform(edited[j]);
                profile.points = ToSection(edited, shown[i], profile.points, starts[i], spans[i]);
                KeepJoints(pillar, Parts[i]);
            }

            SetSuspended(pillar, true);
            Changed(pillar);
            return true;
        }

        private bool CornerHandles(TsitskiProBuilderPillar pillar) {
            Vector3 center = Vector3.zero;
            Vector3 start, end;
            if (pillar.TryGetCorner(out TsitskiProBuilderPillar.Corner corner)) {
                start = corner.start;
                end = corner.end;
            }
            else {
                start = pillar.cornerStart;
                end = pillar.cornerEnd;
            }

            using (new Handles.DrawingScope(HandleTheme.Path, pillar.transform.localToWorldMatrix)) {
                if (Event.current.type == EventType.Repaint)
                    Handles.DrawAAPolyLine(3f, start, center, end);

                Vector3 movedStart = PlaneHandle(start, 0.06f, Handles.CubeHandleCap);
                Vector3 movedEnd = PlaneHandle(end, 0.06f, Handles.CubeHandleCap);
                if (movedStart == start && movedEnd == end)
                    return false;

                bool startMoved = movedStart != start;
                Vector3 leg = startMoved ? movedStart : movedEnd;
                leg.y = 0f;
                if (leg.magnitude < TsitskiProBuilderPillar.MinCornerLength)
                    return true;

                Undo.RecordObject(pillar, "Move Corner Pillar Leg");
                if (pillar.stats) {
                    Undo.RecordObject(pillar.stats, "Move Corner Pillar Leg");
                    pillar.stats.cornerLength = leg.magnitude;
                    EditorUtility.SetDirty(pillar.stats);
                    pillar.stats.NotifyChanged();
                }
                else {
                    pillar.cornerLength = leg.magnitude;
                }
                if (startMoved)
                    pillar.cornerStart = leg;
                else
                    pillar.cornerEnd = leg;

                if (pillar.activeAutoSnapToPath)
                    Snap(pillar, pillar.snapDistance);
                RebuildQueue.Run(pillar, pillar.Rebuild);
                _lastPosition = pillar.transform.position;
                return true;
            }
        }

        private static void Snap(TsitskiProBuilderPillar pillar, float distance) {
            Undo.RecordObjects(new Object[] { pillar, pillar.transform }, "Snap Pillar");
            if (pillar.SnapToPath(distance))
                RebuildQueue.Run(pillar, pillar.Rebuild);
        }

        private static Vector3 PlaneHandle(Vector3 position, float size, Handles.CapFunction cap) {
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(position, Vector3.up, Vector3.right, Vector3.forward,
                HandleUtility.GetHandleSize(position) * size, cap, Vector2.zero);
            if (!EditorGUI.EndChangeCheck())
                return position;

            moved.y = position.y;
            return moved;
        }

        private bool SectionHandles(TsitskiProBuilderPillar pillar, TsitskiProBuilderPillar.Layout layout, float right) {
            if (GUIUtility.hotControl == 0)
                ResetDrag(layout);

            if (pillar.activeBottomProfile is { valid: true } && SectionHandle(layout.bottomSpan, right, out float bottom))
                return ResizeSection(pillar, layout, Part.Bottom, bottom);

            if (pillar.activeTopProfile is { valid: true } && SectionHandle(layout.topStart, right, out float top))
                return ResizeSection(pillar, layout, Part.Top, top);

            if (pillar.activeCenterProfile is { valid: true } && layout.repeats > 0 && !pillar.activeStretchCenter &&
                    SectionHandle(layout.middleStart + pillar.activeCenterHeight, right * 0.5f, out float center))
                return ResizeSection(pillar, layout, Part.Middle, center);

            return false;
        }

        private void ResetDrag(TsitskiProBuilderPillar.Layout layout) {
            _dragPoints = null;
            _dragSquish = layout.squish;
        }

        private bool ResizeSection(TsitskiProBuilderPillar pillar, TsitskiProBuilderPillar.Layout layout, Part part, float boundary) {
            if (_dragSquish <= 0f)
                _dragSquish = layout.squish;
            float scale = Mathf.Max(Epsilon, _dragSquish);
            bool squished = _dragSquish < 1f;
            float bottomHeight = pillar.activeBottomHeight, topHeight = pillar.activeTopHeight, centerHeight = pillar.activeCenterHeight;

            switch (part) {
                case Part.Bottom:
                    bottomHeight = Mathf.Max(0f, boundary / scale);
                    if (!squished)
                        bottomHeight = Mathf.Min(bottomHeight, Mathf.Max(0f, pillar.height - topHeight));
                    break;
                case Part.Top:
                    topHeight = Mathf.Max(0f, (pillar.height - boundary) / scale);
                    if (!squished)
                        topHeight = Mathf.Min(topHeight, Mathf.Max(0f, pillar.height - bottomHeight));
                    break;
                default:
                    centerHeight = Mathf.Max(TsitskiProBuilderPillar.MinCenterHeight, boundary - layout.middleStart);
                    break;
            }

            PillarProfile profile = Section(pillar, part);
            if (_dragPoints == null || _dragPart != part) {
                _dragPart = part;
                _dragPoints = new List<Vector2>(profile.points);
                _dragSpan = layout.Span(part);
            }

            TsitskiProBuilderPillar.Layout resized = TsitskiProBuilderPillar.ComputeLayout(pillar.height, pillar.activeBottomProfile, pillar.activeCenterProfile,
                pillar.activeTopProfile, bottomHeight, topHeight, centerHeight, pillar.activeStretchCenter);
            float span = resized.Span(part);
            if (_dragSpan > Epsilon && span > Epsilon) {
                Undo.RecordObject(pillar.stats ? (Object)pillar.stats : pillar, "Pillar Sections");
                float ratio = _dragSpan / span;
                List<Vector2> current = profile.points;
                bool sameCount = current.Count == _dragPoints.Count;
                profile.points = _dragPoints.Select((p, i) => new Vector2(sameCount ? current[i].x : p.x, KeepHeight(p.y, ratio, part == Part.Top))).ToList();
            }

            ApplySections(pillar, bottomHeight, topHeight, centerHeight);
            return true;
        }

        private static Part? JointSizing(TsitskiProBuilderPillar pillar, TsitskiProBuilderPillar.Layout layout, Part part, int index, int count) {
            int last = count - 1;
            return part switch {
                Part.Bottom when index == last => Part.Bottom,
                Part.Middle when index == 0 => Part.Bottom,
                Part.Middle when index == last => pillar.activeStretchCenter || layout.repeats == 0 ? Part.Top : Part.Middle,
                Part.Top when index == 0 => Part.Top,
                _ => null
            };
        }

        private Part? JointBoundary(TsitskiProBuilderPillar pillar, TsitskiProBuilderPillar.Layout layout, List<Vector3>[] shown) {
            Part? boundary = null;
            for (int i = 0; i < Parts.Length; i++) {
                if (shown[i] == null)
                    continue;
                foreach (int j in _sections[i].selected) {
                    Part? sizing = JointSizing(pillar, layout, Parts[i], j, shown[i].Count);
                    if (sizing == null || boundary != null && boundary != sizing)
                        return null;
                    boundary = sizing;
                }
            }
            return boundary;
        }

        private bool JointDrag(TsitskiProBuilderPillar pillar, TsitskiProBuilderPillar.Layout layout, int section, PillarProfile profile, PathEdit edit,
            List<Vector3> shown) {
            if (edit.points.Count != shown.Count || shown.Count < 2)
                return false;

            Part part = Parts[section];
            int last = shown.Count - 1;
            foreach (int j in _sections[section].selected) {
                if (j != 0 && j != last || Mathf.Abs(edit.points[j].y - shown[j].y) <= Epsilon)
                    continue;

                if (JointSizing(pillar, layout, part, j, shown.Count) is not { } resize)
                    continue;

                List<Vector2> points = new(profile.points);
                points[j] = new Vector2(edit.points[j].x, points[j].y);
                ApplySection(pillar, part, profile, edit, points);
                return ResizeSection(pillar, layout, resize, edit.points[j].y);
            }

            return false;
        }

        private static float KeepHeight(float y, float ratio, bool fromTop) {
            if (fromTop)
                return y <= Epsilon ? 0f : Mathf.Clamp01(1f - (1f - y) * ratio);
            return y >= 1f - Epsilon ? 1f : Mathf.Clamp01(y * ratio);
        }

        private static bool SectionHandle(float y, float right, out float moved) {
            Vector3 position = new(right, y, 0f);
            Handles.color = SplitColor;
            Handles.DrawDottedLine(new Vector3(0f, y, 0f), position, 3f);

            EditorGUI.BeginChangeCheck();
            moved = Handles.Slider(position, Vector3.up, HandleUtility.GetHandleSize(position) * 0.06f, Handles.CubeHandleCap, 0f).y;
            return EditorGUI.EndChangeCheck();
        }

        private static List<Vector2> ToSection(List<Vector3> edited, List<Vector3> shown, List<Vector2> original, float start, float span) {
            List<Vector2> points = new();
            bool sameCount = edited.Count == shown.Count;
            for (int i = 0; i < edited.Count; i++)
                points.Add(sameCount && edited[i] == shown[i]
                    ? original[i]
                    : new Vector2(edited[i].x, Mathf.Clamp01((edited[i].y - start) / Mathf.Max(Epsilon, span))));
            return points;
        }

        private static void DrawSections(TsitskiProBuilderPillar pillar, TsitskiProBuilderPillar.Layout layout) {
            if (Event.current.type != EventType.Repaint)
                return;

            foreach ((List<Vector2> points, Part part, int repeat) in TsitskiProBuilderPillar.LayoutLines(layout, pillar.activeBottomProfile,
                         pillar.activeCenterProfile, pillar.activeTopProfile, pillar.activeRoundingSegments)) {
                if (points.Count < 2)
                    continue;

                Color color = PillarProfilePreview.PartColor(part);
                Handles.color = repeat == 0 ? color : new Color(color.r, color.g, color.b, color.a * 0.5f);
                Handles.DrawAAPolyLine(repeat == 0 ? 6f : 4f, points.Select(p => new Vector3(p.x, p.y, 0f)).ToArray());
            }
        }

        private static void ApplySection(TsitskiProBuilderPillar pillar, Part part, PillarProfile profile, PathEdit edit, List<Vector2> points) {
            Object owner = pillar.stats ? (Object)pillar.stats : pillar;
            Undo.RecordObject(owner, $"{edit.undoName} (Pillar Profile)");
            profile.points = points;
            profile.radii = edit.radii;
            KeepJoints(pillar, part);
            SetSuspended(pillar, true);
            Changed(pillar);
        }

        private static void KeepJoints(TsitskiProBuilderPillar pillar, Part edited) {
            PillarProfile bottom = pillar.activeBottomProfile, center = pillar.activeCenterProfile, top = pillar.activeTopProfile;

            void Pin(PillarProfile profile, bool first, float y) {
                if (profile is not { valid: true })
                    return;
                int index = first ? 0 : profile.points.Count - 1;
                profile.points[index] = new Vector2(profile.points[index].x, y);
            }

            Pin(bottom, false, 1f);
            Pin(center, true, 0f);
            Pin(center, false, 1f);
            Pin(top, true, 0f);

            void Share(PillarProfile from, bool fromFirst, PillarProfile to, bool toFirst) {
                if (from is not { valid: true } || to is not { valid: true })
                    return;
                Vector2 source = from.points[fromFirst ? 0 : from.points.Count - 1];
                int index = toFirst ? 0 : to.points.Count - 1;
                to.points[index] = new Vector2(source.x, to.points[index].y);
            }

            if (center is not { valid: true }) {
                if (edited == Part.Bottom)
                    Share(bottom, false, top, true);
                else if (edited == Part.Top)
                    Share(top, true, bottom, false);
                return;
            }

            switch (edited) {
                case Part.Bottom:
                    Share(bottom, false, center, true);
                    break;
                case Part.Middle:
                    Share(center, true, bottom, false);
                    Share(center, false, top, true);
                    break;
                default:
                    Share(top, true, center, false);
                    break;
            }
        }

        private static void ApplySections(TsitskiProBuilderPillar pillar, float bottomHeight, float topHeight, float centerHeight) {
            TsitskiProBuilderPillarStats stats = pillar.stats;
            if (stats) {
                Undo.RecordObject(stats, "Pillar Sections");
                stats.bottomHeight = bottomHeight;
                stats.topHeight = topHeight;
                stats.centerHeight = centerHeight;
                EditorUtility.SetDirty(stats);
                RebuildQueue.Run(stats, stats.NotifyChanged);
                return;
            }

            Undo.RecordObject(pillar, "Pillar Sections");
            pillar.bottomHeight = bottomHeight;
            pillar.topHeight = topHeight;
            pillar.centerHeight = centerHeight;
            RebuildQueue.Run(pillar, pillar.Rebuild);
        }
    }

    [CustomEditor(typeof(TsitskiProBuilderPillarStats))]
    public class TsitskiProBuilderPillarStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            if (serializedObject.isEditingMultipleObjects)
                return;

            TsitskiProBuilderPillarStats stats = (TsitskiProBuilderPillarStats)target;
            float natural = stats.bottomHeight + stats.topHeight + stats.centerHeight;
            TsitskiProBuilderPillar.Layout layout = TsitskiProBuilderPillar.ComputeLayout(Mathf.Max(0.01f, natural), stats.bottomProfile, stats.centerProfile,
                stats.topProfile, stats.bottomHeight, stats.topHeight, stats.centerHeight, true);
            PillarProfilePreview.Draw(stats, layout, stats.bottomProfile, stats.centerProfile, stats.topProfile, stats.roundingSegments,
                () => {
                    EditorUtility.SetDirty(stats);
                    stats.NotifyChanged();
                });
        }
    }

    internal static class PillarProfilePreview {
        public static Color PartColor(Part part) => part switch {
            Part.Bottom => new Color(0.3f, 0.7f, 1f, 1f),
            Part.Middle => new Color(0.45f, 0.9f, 0.35f, 1f),
            _ => new Color(0.8f, 0.45f, 1f, 1f)
        };

        public static void Draw(Object owner, TsitskiProBuilderPillar.Layout layout, PillarProfile bottom, PillarProfile center, PillarProfile top,
            int segments, System.Action changed) {
            List<ProfileLine> lines = TsitskiProBuilderPillar.LayoutLines(layout, bottom, center, top, segments)
                .Select(line => {
                    Color color = PartColor(line.part);
                    return new ProfileLine {
                        points = line.points,
                        color = line.repeat == 0 ? color : new Color(color.r, color.g, color.b, color.a * 0.5f)
                    };
                })
                .ToList();

            PillarProfile[] sections = { bottom, center, top };
            List<Vector2> all = sections.Where(s => s?.points != null).SelectMany(s => s.points).ToList();

            List<GraphPoints> editable = new();
            Part[] parts = { Part.Bottom, Part.Middle, Part.Top };
            for (int i = 0; i < sections.Length; i++) {
                PillarProfile section = sections[i];
                float start = layout.Start(parts[i]), span = layout.Span(parts[i]);
                if (section is not { valid: true } || span <= 1e-5f)
                    continue;

                editable.Add(new GraphPoints {
                    points = section.points,
                    color = PartColor(parts[i]),
                    toGraph = p => new Vector2(p.x, start + p.y * span),
                    fromGraph = (p, _) => new Vector2(p.x, Mathf.Clamp01((p.y - start) / span)),
                    apply = edited => {
                        Undo.RecordObject(owner, "Edit Pillar Profile");
                        section.points = edited;
                        changed();
                    },
                    radii = section.radii,
                    applyRadii = radii => {
                        Undo.RecordObject(owner, "Pillar Profile Radius");
                        section.radii = radii;
                        changed();
                    },
                    canDelete = index => index != 0 && index != section.points.Count - 1
                });
            }

            ProfilePreview.Draw(lines, new[] { layout.bottomSpan, layout.topStart }, all, edited => {
                Undo.RecordObject(owner, "Snap Pillar Profile");
                int index = 0;
                foreach (PillarProfile section in sections) {
                    if (section?.points == null)
                        continue;
                    section.points = edited.GetRange(index, section.points.Count);
                    index += section.points.Count;
                }
                changed();
            }, 160f, editable);
        }
    }
}
