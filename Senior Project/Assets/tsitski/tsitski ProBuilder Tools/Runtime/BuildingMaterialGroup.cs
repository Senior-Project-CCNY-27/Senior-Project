using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    public enum MaterialSetupType {
        Simple,
        Complex
    }

    public enum RealBlendLayer {
        [InspectorName("Base (0,0,0,0)")] Base,
        [InspectorName("Layer 1 (0,1,0,0 Green)")] Layer1,
        [InspectorName("Layer 2 (0,0,1,0 Blue)")] Layer2
    }

    public static class RealBlendLayers {
        public static Color Color(this RealBlendLayer layer) => layer switch {
            RealBlendLayer.Layer1 => new Color(0f, 1f, 0f, 0f),
            RealBlendLayer.Layer2 => new Color(0f, 0f, 1f, 0f),
            _ => UnityEngine.Color.clear
        };
    }

    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Building Material Group", fileName = "Building Material Group")]
    public class BuildingMaterialGroup : ScriptableObject {
        [Tooltip(Tooltips.MaterialSetupType)] public MaterialSetupType setupType;
        [Tooltip(Tooltips.SimpleMaterial)] public Material material;
        [Tooltip(Tooltips.RealBlendLayer)] public RealBlendLayer materialLayer;
        [Tooltip(Tooltips.InsideWall)] public Material insideWall;
        [Tooltip(Tooltips.RealBlendLayer)] public RealBlendLayer insideWallLayer;
        [Tooltip(Tooltips.OutsideWall)] public Material outsideWall;
        [Tooltip(Tooltips.RealBlendLayer)] public RealBlendLayer outsideWallLayer;
        [Tooltip(Tooltips.InsideFloor)] public Material insideFloor;
        [Tooltip(Tooltips.RealBlendLayer)] public RealBlendLayer insideFloorLayer;
        [Tooltip(Tooltips.OutsideFloor)] public Material outsideFloor;
        [Tooltip(Tooltips.RealBlendLayer)] public RealBlendLayer outsideFloorLayer;
        [Tooltip(Tooltips.InsideRoof)] public Material insideRoof;
        [Tooltip(Tooltips.RealBlendLayer)] public RealBlendLayer insideRoofLayer;
        [Tooltip(Tooltips.OutsideRoof)] public Material outsideRoof;
        [Tooltip(Tooltips.RealBlendLayer)] public RealBlendLayer outsideRoofLayer;
        [Tooltip(Tooltips.InsideTrim)] public Material insideTrim;
        [Tooltip(Tooltips.RealBlendLayer)] public RealBlendLayer insideTrimLayer;
        [Tooltip(Tooltips.OutsideTrim)] public Material outsideTrim;
        [Tooltip(Tooltips.RealBlendLayer)] public RealBlendLayer outsideTrimLayer;
        [Tooltip(Tooltips.InsideDistance)] [Min(0.1f)] public float insideDistance = 30f;
    }
}
