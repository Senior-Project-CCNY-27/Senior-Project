using UnityEngine;

namespace Tsitski.Core {
    // Stats for anything generated along a path. An abstract class, not an interface,
    // because Unity can't serialize a reference to an interface.
    public abstract class TsitskiPathStats : ScriptableObject { }
}
