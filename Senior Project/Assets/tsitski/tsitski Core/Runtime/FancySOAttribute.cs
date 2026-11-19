using UnityEngine;

namespace Tsitski.Core {
    // Field-level alternative to writing a FancySODrawer subclass per type: draws the
    // ScriptableObject reference with the "+" create button. createAsSubAsset nests the
    // created SO under the asset that owns the field instead of saving it into a folder.
    public class FancySOAttribute : PropertyAttribute {
        public readonly bool createAsSubAsset;

        public FancySOAttribute(bool createAsSubAsset = false) {
            this.createAsSubAsset = createAsSubAsset;
        }
    }
}
