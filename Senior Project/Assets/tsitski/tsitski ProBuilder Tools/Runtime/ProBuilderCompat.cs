using System.Reflection;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    public static class ProBuilderCompat {
        public static void MakeUniqueMesh(ProBuilderMesh mesh) {
#if TSITSKI_PROBUILDER_5_1_OR_NEWER
            mesh.MakeUnique();
#else
            typeof(ProBuilderMesh).GetMethod("MakeUnique", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(mesh, null);
#endif
        }
    }
}
