using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tsitski.Core {
    public interface ITsitskiPath {
        List<Vector3> GetWorldPath(out bool closed);
    }

    public static class TsitskiPathEvents {
        public static event Action<Object> Changed;

        public static void NotifyChanged(Object source) => Changed?.Invoke(source);
    }

    public class PathSourceAttribute : PropertyAttribute { }
}
