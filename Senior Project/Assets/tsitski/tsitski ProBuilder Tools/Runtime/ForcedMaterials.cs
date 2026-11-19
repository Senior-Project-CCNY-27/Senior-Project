using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    public interface IForcedMaterials {
        Material[] forcedMaterials { get; }
    }

    public static class ForcedMaterials {
        public static void Apply(ProBuilderMesh mesh, IList<int> regions, params Material[] materials) {
            if (!materials.Any(m => m) || !mesh.TryGetComponent(out MeshRenderer renderer))
                return;

            List<Material> current = renderer.sharedMaterials.Select(m => m ? m : TsitskiProBuilderSettings.DefaultMaterial).ToList();
            if (current.Count == 0)
                current.Add(TsitskiProBuilderSettings.DefaultMaterial);

            int[] forced = new int[materials.Length];
            for (int m = 0; m < materials.Length; m++) {
                forced[m] = -1;
                if (!materials[m])
                    continue;

                forced[m] = current.IndexOf(materials[m]);
                if (forced[m] < 0) {
                    forced[m] = current.Count;
                    current.Add(materials[m]);
                }
            }

            IList<Face> faces = mesh.faces;
            for (int f = 0; f < faces.Count; f++) {
                int region = Region(regions, f);
                if (region >= 0 && region < forced.Length && forced[region] >= 0)
                    faces[f].submeshIndex = forced[region];
            }

            int[] remap = Enumerable.Repeat(-1, current.Count).ToArray();
            List<Material> used = new();
            foreach (Face face in faces) {
                int submesh = Mathf.Clamp(face.submeshIndex, 0, current.Count - 1);
                if (remap[submesh] < 0) {
                    remap[submesh] = used.Count;
                    used.Add(current[submesh]);
                }
                face.submeshIndex = remap[submesh];
            }

            renderer.sharedMaterials = used.ToArray();
        }

        private static int Region(IList<int> regions, int face) => regions == null ? 0 : face < regions.Count ? regions[face] : -1;

        public static HashSet<Material> Locked(ProBuilderMesh mesh) {
            Component holder = TsitskiProBuilderCutTarget.IsResult(mesh) ? mesh.transform.parent : mesh;
            Material[] materials = holder.TryGetComponent(out IForcedMaterials owner) ? owner.forcedMaterials
                : holder.TryGetComponent(out TsitskiArray array) ? new[] { array.activeMaterial }
                : null;
            return materials == null ? new HashSet<Material>() : materials.Where(m => m).ToHashSet();
        }
    }
}
