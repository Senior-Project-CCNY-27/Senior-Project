using Tsitski.Core;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Tsitski.ProBuilderTools {
    public static class TsitskiProBuilderSettings {
        public static Material DefaultMaterial {
            get {
                Material material = TsitskiSettings.DefaultMaterial;
                return material ? material : BuiltinMaterials.defaultMaterial;
            }
        }
    }
}
