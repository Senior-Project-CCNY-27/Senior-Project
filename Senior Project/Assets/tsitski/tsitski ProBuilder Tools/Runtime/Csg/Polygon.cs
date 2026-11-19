using UnityEngine;
using System.Collections.Generic;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools.Csg
{
    /// <summary>
    /// Represents a polygon face with an arbitrary number of vertices.
    /// </summary>
    sealed class Polygon
    {
        public List<Vertex> vertices;
        public Plane plane;
        public Material material;
        public int cutterFace = -1;
        public int smoothingGroup = -1;
        public float smoothingAngle;

        public Polygon(List<Vertex> list, Material mat)
        {
            vertices = list;
            plane = new Plane(list[0].position, list[1].position, list[2].position);
            material = mat;
        }

        public void Flip()
        {
            vertices.Reverse();

            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex vertex = vertices[i];
                vertex.Flip();
                vertices[i] = vertex;
            }

            plane.Flip();
        }

        public override string ToString()
        {
            return $"[{vertices.Count}] {plane.normal}";
        }
    }
}
