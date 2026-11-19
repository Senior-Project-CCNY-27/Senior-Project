using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.Core {
    public class TsitskiSettings : ScriptableObject {
        private const string AssetName = "tsitski Settings.asset";
        private const string DemoMaterialGuid = "46c460769623588498b4802546e968ed";

        [Tooltip(CoreTooltips.DefaultMeshMaterial)] public Material defaultMeshMaterial;
        [Tooltip(CoreTooltips.RebuildAfterDragging)] public bool rebuildAfterDragging = true;
        [Tooltip(CoreTooltips.AutoNameGenerators)] public bool autoNameGenerators;
        [Tooltip(CoreTooltips.GizmosOnlyOnSelected)] public bool gizmosOnlyOnSelected;
        [Tooltip(CoreTooltips.ContributeGIByDefault)] public bool contributeGIByDefault = true;
        [Tooltip(CoreTooltips.ShowReviewButton)] public bool showReviewButton = true;
        [Tooltip(CoreTooltips.PlacementLayers)] public LayerMask placementLayers = ~0;

        private static TsitskiSettings _instance;

        public static Material DefaultMaterial {
            get {
                TsitskiSettings settings = Instance;
                return settings ? settings.defaultMeshMaterial : null;
            }
        }

        public static bool RebuildAfterDragging {
            get {
                TsitskiSettings settings = Instance;
                return !settings || settings.rebuildAfterDragging;
            }
        }

        public static bool AutoNameGenerators {
            get {
                TsitskiSettings settings = Instance;
                return settings && settings.autoNameGenerators;
            }
        }

        public static bool GizmosOnlyOnSelected {
            get {
                TsitskiSettings settings = Instance;
                return settings && settings.gizmosOnlyOnSelected;
            }
        }

        public static int PlacementLayers {
            get {
                TsitskiSettings settings = Instance;
                return settings ? settings.placementLayers : ~0;
            }
        }

        public static bool ContributeGIByDefault {
            get {
                TsitskiSettings settings = Instance;
                return !settings || settings.contributeGIByDefault;
            }
        }

        public static void ApplyDefaultStaticFlags(GameObject gameObject) {
#if UNITY_EDITOR
            if (!gameObject || !ContributeGIByDefault)
                return;

            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
            if ((flags & StaticEditorFlags.ContributeGI) != 0)
                return;

            Undo.RecordObject(gameObject, "Contribute GI");
            GameObjectUtility.SetStaticEditorFlags(gameObject, flags | StaticEditorFlags.ContributeGI);
#endif
        }

        public static void AutoName(Component generator, Object stats, string defaultName) {
            if (!generator || !AutoNameGenerators)
                return;

            string generatorName = stats ? stats.name : defaultName;
            if (generator.gameObject.name == generatorName)
                return;

            generator.gameObject.name = generatorName;
#if UNITY_EDITOR
            EditorUtility.SetDirty(generator.gameObject);
#endif
        }

        public static TsitskiSettings Instance {
            get {
#if UNITY_EDITOR
                if (!_instance)
                    _instance = LoadOrCreate();
#endif
                return _instance;
            }
        }

#if UNITY_EDITOR
        private static TsitskiSettings LoadOrCreate() {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(TsitskiSettings)}")) {
                TsitskiSettings found = AssetDatabase.LoadAssetAtPath<TsitskiSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (found)
                    return found;
            }

            TsitskiSettings created = CreateInstance<TsitskiSettings>();
            created.defaultMeshMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(DemoMaterialGuid));
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(created));
            string folder = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath))?.Replace('\\', '/');
            AssetDatabase.CreateAsset(created, $"{(string.IsNullOrEmpty(folder) ? "Assets" : folder)}/{AssetName}");
            AssetDatabase.SaveAssets();
            return created;
        }
#endif
    }
}
