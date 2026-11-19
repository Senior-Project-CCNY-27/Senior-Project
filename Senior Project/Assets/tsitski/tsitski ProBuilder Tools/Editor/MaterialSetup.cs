using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Math = UnityEngine.ProBuilder.Math;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    internal static class MaterialSetup {
        private enum Slot {
            InsideWall,
            OutsideWall,
            InsideFloor,
            OutsideFloor,
            InsideRoof,
            OutsideRoof,
            InsideTrim,
            OutsideTrim
        }

        private const float FloorDot = 0.7f;
        private const float RoofUndersideDot = -0.1f;
        private const float RayOffset = 0.02f;
        private const float RaySpread = 25f;
        private const float ContactDistance = 0.3f;
        private const float FloorMinCoverDistance = 1f;
        private const int EnclosureRays = 12;
        private const float EnclosedFraction = 0.75f;

        [MenuItem("Tools/tsitski/ProBuilder/Setup Materials")]
        private static void Open() => MaterialSetupWindow.Open();

        [MenuItem("Tools/tsitski/ProBuilder/Setup Materials", true)]
        private static bool CanOpen() => Selection.gameObjects.Length > 0;

        public static List<ProBuilderMesh> SelectedMeshes() =>
            Selection.gameObjects.SelectMany(root => root.GetComponentsInChildren<ProBuilderMesh>(true)).Distinct().ToList();

        public static void Apply(BuildingMaterialGroup group, List<ProBuilderMesh> meshes) {
            Undo.SetCurrentGroupName("Setup Materials");
            int undoGroup = Undo.GetCurrentGroup();

            if (group.setupType == MaterialSetupType.Complex)
                Physics.SyncTransforms();

            List<ProBuilderMesh> results = meshes.Where(TsitskiProBuilderCutTarget.IsResult).ToList();
            List<ProBuilderMesh> sources = meshes.Except(results).ToList();

            (int changed, int painted) = ApplyTo(group, sources);
            ProBuilderCutSystem.RebuildNow(sources.Where(mesh => mesh.GetComponent<TsitskiProBuilderCutTarget>()));
            (int resultsChanged, int resultsPainted) = ApplyTo(group, results.Where(mesh => mesh).ToList());
            changed += resultsChanged;
            painted += resultsPainted;

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Materials set on {changed} of {meshes.Count} ProBuilder mesh{(meshes.Count == 1 ? "" : "es")}" +
                      (painted > 0 ? $", Real Blend set up on {painted}." : "."));
        }

        private static (int changed, int painted) ApplyTo(BuildingMaterialGroup group, List<ProBuilderMesh> meshes) {
            int changed = 0;
            Dictionary<ProBuilderMesh, Dictionary<Face, Color>> layers = new();

            foreach (ProBuilderMesh mesh in meshes) {
                if (ForcedMaterials.Locked(mesh).Count > 0)
                    continue;

                Dictionary<Face, Color> colors = new();
                Dictionary<Material, List<Face>> faces = group.setupType == MaterialSetupType.Simple
                    ? SimpleFaces(group, mesh, colors)
                    : ComplexFaces(group, mesh, colors);

                if (faces.Count == 0)
                    continue;

                layers[mesh] = colors;

                Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { mesh, mesh.GetComponent<MeshRenderer>() }, "Setup Materials");

                foreach (KeyValuePair<Material, List<Face>> pair in faces)
                    mesh.SetMaterial(pair.Value, pair.Key);

                LightmapUvs.ToMeshKeeping(mesh);
                mesh.Refresh();
                PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
                changed++;
            }

            int painted = 0;
#if TSITSKI_REALBLEND
            painted = RealBlendSetup.SetupVertexPaint(meshes, layers);
#endif

            return (changed, painted);
        }

        private static Dictionary<Material, List<Face>> SimpleFaces(BuildingMaterialGroup group, ProBuilderMesh mesh, Dictionary<Face, Color> colors) {
            Dictionary<Material, List<Face>> faces = new();
            if (!group.material)
                return faces;

            faces[group.material] = mesh.faces.ToList();
            foreach (Face face in mesh.faces)
                colors[face] = group.materialLayer.Color();

            return faces;
        }

        private static Dictionary<Material, List<Face>> ComplexFaces(BuildingMaterialGroup group, ProBuilderMesh mesh, Dictionary<Face, Color> colors) {
            Dictionary<Material, List<Face>> faces = new();
            bool roof = mesh.GetComponent<TsitskiProBuilderRoof>();
            IList<Vector3> positions = mesh.positions;

            if (IsWallDetail(mesh)) {
                bool inside = GeneratedInterior(mesh) ?? MostlyInside(mesh, positions, group.insideDistance);
                Slot wall = inside ? Slot.InsideTrim : Slot.OutsideTrim;
                Material material = MaterialFor(group, wall);
                if (!material)
                    return faces;

                faces[material] = mesh.faces.ToList();
                foreach (Face face in mesh.faces)
                    colors[face] = LayerFor(group, wall).Color();

                return faces;
            }

            foreach (Face face in mesh.faces) {
                Slot slot = Classify(mesh, positions, face, roof, group.insideDistance);
                Material material = MaterialFor(group, slot);
                if (!material)
                    continue;

                colors[face] = LayerFor(group, slot).Color();

                if (!faces.TryGetValue(material, out List<Face> list))
                    faces[material] = list = new List<Face>();

                list.Add(face);
            }

            return faces;
        }

        private static bool IsWallDetail(ProBuilderMesh mesh) =>
            mesh.GetComponent<TsitskiProBuilderTrim>() || mesh.GetComponent<TsitskiArray>() || mesh.GetComponent<TsitskiProBuilderPillar>();

        private static bool? GeneratedInterior(ProBuilderMesh mesh) {
            if (mesh.TryGetComponent(out TsitskiProBuilderTrim trim))
                return trim.generatedInterior;
            if (mesh.TryGetComponent(out TsitskiArray array))
                return array.generatedInterior;
            return mesh.TryGetComponent(out TsitskiProBuilderPillar pillar) ? pillar.generatedInterior : null;
        }

        private static bool MostlyInside(ProBuilderMesh mesh, IList<Vector3> positions, float distance) {
            HashSet<Collider> own = new(mesh.GetComponentsInChildren<Collider>(true));
            Transform transform = mesh.transform;
            Func<Collider, bool> counts = BuildingFilter(mesh, own);
            float inside = 0, total = 0;

            foreach (Face face in mesh.faces) {
                Vector3 normal = transform.TransformDirection(Math.Normal(mesh, face)).normalized;
                float area = FaceArea(transform, positions, face);
                if (area <= 0)
                    continue;

                Vector3 point = FaceCenter(transform, positions, face) + normal * RayOffset;
                if (Hits(point, normal, ContactDistance, counts))
                    continue;

                total += area;
                inside += Enclosed(point, distance, counts) ? area : 0;
            }

            return inside * 2 > total;
        }

        private static bool Enclosed(Vector3 point, float distance, Func<Collider, bool> counts) {
            if (!Hits(point, Vector3.up, distance, counts))
                return false;

            int hits = 0;
            for (int i = 0; i < EnclosureRays; i++) {
                Vector3 direction = Quaternion.AngleAxis(360f * i / EnclosureRays, Vector3.up) * Vector3.forward;
                if (Hits(point, direction, distance, counts))
                    hits++;
            }

            return hits >= EnclosureRays * EnclosedFraction;
        }

        private static float FaceArea(Transform transform, IList<Vector3> positions, Face face) {
            float area = 0;
            IReadOnlyList<int> indexes = face.indexes;

            for (int i = 0; i + 2 < indexes.Count; i += 3) {
                Vector3 a = transform.TransformPoint(positions[indexes[i]]);
                Vector3 b = transform.TransformPoint(positions[indexes[i + 1]]);
                Vector3 c = transform.TransformPoint(positions[indexes[i + 2]]);
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }

            return area;
        }

        private static Vector3 FaceCenter(Transform transform, IList<Vector3> positions, Face face) {
            Vector3 center = Vector3.zero;
            foreach (int index in face.distinctIndexes)
                center += positions[index];

            return transform.TransformPoint(center / face.distinctIndexes.Count);
        }

        private static Material MaterialFor(BuildingMaterialGroup group, Slot slot) => slot switch {
            Slot.InsideWall => group.insideWall,
            Slot.OutsideWall => group.outsideWall,
            Slot.InsideFloor => group.insideFloor,
            Slot.OutsideFloor => group.outsideFloor,
            Slot.InsideRoof => group.insideRoof,
            Slot.InsideTrim => group.insideTrim ? group.insideTrim : group.insideWall,
            Slot.OutsideTrim => group.outsideTrim ? group.outsideTrim : group.outsideWall,
            _ => group.outsideRoof
        };

        private static RealBlendLayer LayerFor(BuildingMaterialGroup group, Slot slot) => slot switch {
            Slot.InsideWall => group.insideWallLayer,
            Slot.OutsideWall => group.outsideWallLayer,
            Slot.InsideFloor => group.insideFloorLayer,
            Slot.OutsideFloor => group.outsideFloorLayer,
            Slot.InsideRoof => group.insideRoofLayer,
            Slot.InsideTrim => group.insideTrim ? group.insideTrimLayer : group.insideWallLayer,
            Slot.OutsideTrim => group.outsideTrim ? group.outsideTrimLayer : group.outsideWallLayer,
            _ => group.outsideRoofLayer
        };

        private static Slot Classify(ProBuilderMesh mesh, IList<Vector3> positions, Face face, bool roof, float distance) {
            Transform transform = mesh.transform;
            Vector3 normal = transform.TransformDirection(Math.Normal(mesh, face)).normalized;

            if (roof)
                return normal.y < RoofUndersideDot ? Slot.InsideRoof : Slot.OutsideRoof;

            float minDistance = normal.y > FloorDot ? FloorMinCoverDistance : 0f;
            bool covered = MostlyCovered(transform, positions, face, normal, minDistance, distance, BuildingFilter(mesh, null));

            if (normal.y > FloorDot)
                return covered ? Slot.InsideFloor : Slot.OutsideFloor;

            if (normal.y < -FloorDot)
                return covered ? Slot.InsideRoof : Slot.OutsideFloor;

            return covered ? Slot.InsideWall : Slot.OutsideWall;
        }

        private static Func<Collider, bool> BuildingFilter(ProBuilderMesh mesh, HashSet<Collider> ignore) {
            Transform root = mesh.transform.root;
            bool sameBuilding = root != mesh.transform;
            return collider => (ignore == null || !ignore.Contains(collider)) && (!sameBuilding || collider.transform.IsChildOf(root));
        }

        private static bool MostlyCovered(Transform transform, IList<Vector3> positions, Face face, Vector3 normal, float minDistance,
            float distance, Func<Collider, bool> counts) {
            IReadOnlyList<int> indexes = face.indexes;
            float covered = 0, total = 0;

            for (int i = 0; i + 2 < indexes.Count; i += 3) {
                Vector3 a = transform.TransformPoint(positions[indexes[i]]);
                Vector3 b = transform.TransformPoint(positions[indexes[i + 1]]);
                Vector3 c = transform.TransformPoint(positions[indexes[i + 2]]);
                float area = Vector3.Cross(b - a, c - a).magnitude;
                total += area;
                if (area > 0 && Covered((a + b + c) / 3f, normal, minDistance, distance, counts))
                    covered += area;
            }

            return total > 0
                ? covered * 2 > total
                : Covered(FaceCenter(transform, positions, face), normal, minDistance, distance, counts);
        }

        private static bool Covered(Vector3 center, Vector3 normal, float minDistance, float distance, Func<Collider, bool> counts) {
            Vector3 origin = center + normal * RayOffset;
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            Vector3[] directions = {
                normal,
                Quaternion.AngleAxis(RaySpread, tangent) * normal,
                Quaternion.AngleAxis(-RaySpread, tangent) * normal,
                Quaternion.AngleAxis(RaySpread, bitangent) * normal,
                Quaternion.AngleAxis(-RaySpread, bitangent) * normal
            };

            int hits = directions.Count(direction => Hits(origin, direction, distance, counts, minDistance));
            return hits * 2 > directions.Length;
        }

        private static bool Hits(Vector3 origin, Vector3 direction, float distance, Func<Collider, bool> counts, float minDistance = 0f) {
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.distance >= minDistance && counts(hit.collider))
                    return true;

            return false;
        }
    }

    internal class MaterialSetupWindow : EditorWindow {
        private const string PrefsKey = "Tsitski.ProBuilderTools.MaterialSetup";

        private BuildingMaterialGroup _local;
        private BuildingMaterialGroup _group;
        private SerializedObject _serialized;

        private static string GroupKey => PrefsKey + ".Group." + Application.dataPath;
        private static string LocalKey => PrefsKey + ".Local." + Application.dataPath;

        public static void Open() {
            MaterialSetupWindow window = GetWindow<MaterialSetupWindow>(true, "Setup Materials", true);
            window.minSize = new Vector2(460, 190);
        }

        private void OnEnable() {
            _local = CreateInstance<BuildingMaterialGroup>();
            _local.hideFlags = HideFlags.DontSave;

            string json = EditorPrefs.GetString(LocalKey, "");
            if (!string.IsNullOrEmpty(json))
                EditorJsonUtility.FromJsonOverwrite(json, _local);

            _group = AssetDatabase.LoadAssetAtPath<BuildingMaterialGroup>(AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(GroupKey, "")));
        }

        private void OnDisable() {
            if (_local)
                DestroyImmediate(_local);
        }

        private void OnSelectionChange() => Repaint();

        private BuildingMaterialGroup active => _group ? _group : _local;

        private void OnGUI() {
            List<ProBuilderMesh> meshes = MaterialSetup.SelectedMeshes();
            EditorGUILayout.LabelField($"{meshes.Count} ProBuilder mesh{(meshes.Count == 1 ? "" : "es")} selected", EditorStyles.miniLabel);

            DrawGroupField();

            if (_serialized == null || _serialized.targetObject != active)
                _serialized = new SerializedObject(active);

            _serialized.Update();
            EditorGUI.BeginChangeCheck();

            SerializedProperty setupType = _serialized.FindProperty(nameof(BuildingMaterialGroup.setupType));
            EditorGUILayout.PropertyField(setupType);

            if ((MaterialSetupType)setupType.enumValueIndex == MaterialSetupType.Simple) {
                MaterialField(nameof(BuildingMaterialGroup.material));
            }
            else {
                foreach (string field in new[] {
                             nameof(BuildingMaterialGroup.insideWall), nameof(BuildingMaterialGroup.outsideWall),
                             nameof(BuildingMaterialGroup.insideFloor), nameof(BuildingMaterialGroup.outsideFloor),
                             nameof(BuildingMaterialGroup.insideRoof), nameof(BuildingMaterialGroup.outsideRoof),
                             nameof(BuildingMaterialGroup.insideTrim), nameof(BuildingMaterialGroup.outsideTrim)
                         })
                    MaterialField(field);

                EditorGUILayout.PropertyField(_serialized.FindProperty(nameof(BuildingMaterialGroup.insideDistance)));
            }

            bool changed = EditorGUI.EndChangeCheck();
            _serialized.ApplyModifiedProperties();

            if (changed && !_group)
                EditorPrefs.SetString(LocalKey, EditorJsonUtility.ToJson(_local));

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(meshes.Count == 0)) {
                if (GUILayout.Button("Setup Materials", GUILayout.Height(24)))
                    MaterialSetup.Apply(active, meshes);
            }
        }

        private void MaterialField(string field) {
            SerializedProperty material = _serialized.FindProperty(field);
#if TSITSKI_REALBLEND
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.PropertyField(material);
                EditorGUILayout.PropertyField(_serialized.FindProperty(field + "Layer"), GUIContent.none, GUILayout.Width(160));
            }
#else
            EditorGUILayout.PropertyField(material);
#endif
        }

        private void DrawGroupField() {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUI.BeginChangeCheck();
                BuildingMaterialGroup group = (BuildingMaterialGroup)EditorGUILayout.ObjectField(
                    new GUIContent("Group", "Material group asset to edit and use. Empty uses the window's own saved materials."),
                    _group, typeof(BuildingMaterialGroup), false);

                if (EditorGUI.EndChangeCheck())
                    SetGroup(group);

                if (GUILayout.Button(new GUIContent("Save As", "Save the current materials as a new Building Material Group asset."), GUILayout.Width(64)))
                    SaveAs();
            }
        }

        private void SetGroup(BuildingMaterialGroup group) {
            _group = group;
            EditorPrefs.SetString(GroupKey, group ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(group)) : "");
        }

        private void SaveAs() {
            string path = StatsSavePanel.Show("Save Building Material Group", "Building Material Group",
                "Save these materials as a Building Material Group");

            if (string.IsNullOrEmpty(path))
                return;

            BuildingMaterialGroup asset = Instantiate(active);
            asset.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            SetGroup(asset);
        }
    }
}
