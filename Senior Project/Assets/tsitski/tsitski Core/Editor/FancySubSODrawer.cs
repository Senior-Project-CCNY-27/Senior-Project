using UnityEngine;

namespace Tsitski.Core {

    // Like FancySODrawer, but "+" nests the created ScriptableObject as a sub-asset of the
    // asset that owns the field instead of saving a standalone asset into a folder. Falls
    // back to folder creation when the owner isn't a saved asset (eg. a scene object).
    public abstract class FancySubSODrawer<T> : FancySODrawer<T> where T : ScriptableObject {
        protected override bool CreateAsSubAsset => true;
    }
}
