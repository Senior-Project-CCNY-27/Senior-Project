using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.Core {
    public enum WallSide {
        Outside,
        Inside,
        Both
    }

    // Tells the layer drawer which stats a list's owner can build and which options apply.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TsitskiPathLayersAttribute : Attribute {
        public readonly bool wallSides;
        public readonly Type[] accepts;

        public TsitskiPathLayersAttribute(bool wallSides, params Type[] accepts) {
            this.wallSides = wallSides;
            this.accepts = accepts;
        }
    }

    [Serializable]
    public class TsitskiPathLayer {
        [Tooltip(CoreTooltips.PathLayerName)] public string name;
        [Tooltip(CoreTooltips.PathLayerStats)] public TsitskiPathStats stats;
        [Tooltip(CoreTooltips.PathLayerWallSide)] public WallSide wallSide = WallSide.Outside;
        [Tooltip(CoreTooltips.PathLayerPolesFrom)] [FormerlySerializedAs("fencePoles")] public TsitskiArrayStats polesFrom;
        [Tooltip(CoreTooltips.PathLayerHeight)] public float height;
        [Tooltip(CoreTooltips.PathLayerHeightFromTop)] [FormerlySerializedAs("onTop")] public bool heightFromTop;
        [Tooltip(CoreTooltips.PathLayerMatchHeight)] public bool matchHeight;
        [Tooltip(CoreTooltips.PathLayerSideOffset)] public float sideOffset;

        [SerializeField] [HideInInspector] private string syncedName;
        [SerializeField] [HideInInspector] [FormerlySerializedAs("fence")] private TsitskiPathStats legacyFence;
        [SerializeField] [HideInInspector] [FormerlySerializedAs("arrayStats")] [FormerlySerializedAs("array")] private TsitskiPathStats legacyArray;
        [SerializeField] [HideInInspector] [FormerlySerializedAs("trimStats")] private TsitskiPathStats legacyTrim;

        public TsitskiPathLayer Copy() => (TsitskiPathLayer)MemberwiseClone();

        // Layers used to hold a stats per kind, so one old layer can become several.
        // Call from the owner's OnEnable and OnValidate; also keeps names following stats.
        public static void Upgrade(Object owner, List<TsitskiPathLayer> layers) {
            if (layers == null)
                return;

            bool changed = false;
            for (int i = layers.Count - 1; i >= 0; i--) {
                TsitskiPathLayer layer = layers[i];
                if (layer == null)
                    continue;

                List<TsitskiPathStats> parts = new();
                foreach (TsitskiPathStats legacy in new[] { layer.legacyFence, layer.legacyArray, layer.legacyTrim })
                    if (legacy)
                        parts.Add(legacy);

                layer.legacyFence = layer.legacyArray = layer.legacyTrim = null;
                for (int part = parts.Count - 1; part >= 0; part--) {
                    TsitskiPathLayer target = part == 0 ? layer : layer.Copy();
                    target.stats = parts[part];
                    if (part > 0)
                        layers.Insert(i + 1, target);
                    changed = true;
                }
            }

            foreach (TsitskiPathLayer layer in layers)
                changed |= layer != null && layer.SyncName();

#if UNITY_EDITOR
            if (changed && owner)
                EditorUtility.SetDirty(owner);
#endif
        }

        // Follows the stats' name until someone types their own.
        private bool SyncName() {
            string statsName = stats ? stats.name : "";
            if (statsName == syncedName && !string.IsNullOrEmpty(name))
                return false;

            bool following = string.IsNullOrEmpty(name) || name == syncedName;
            bool changed = syncedName != statsName || following && name != statsName;
            if (following)
                name = statsName;
            syncedName = statsName;
            return changed;
        }
    }
}
