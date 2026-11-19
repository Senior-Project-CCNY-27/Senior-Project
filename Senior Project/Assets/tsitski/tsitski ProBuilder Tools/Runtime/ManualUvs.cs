using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    internal static class ManualUvs {
        public static void Rebuild(ProBuilderMesh mesh, IList<Vector3> positions, IList<Face> faces, IList<Vector2> uvs) {
            bool[] manual = new bool[faces.Count];
            for (int i = 0; i < faces.Count; i++)
                manual[i] = faces[i].manualUV;

            mesh.RebuildWithPositionsAndFaces(positions, faces);
            if (uvs == null || uvs.Count != mesh.vertexCount)
                return;

            mesh.textures = uvs;
            IList<Face> built = mesh.faces;
            for (int i = 0; i < built.Count && i < manual.Length; i++)
                built[i].manualUV = manual[i];
        }
    }
}
