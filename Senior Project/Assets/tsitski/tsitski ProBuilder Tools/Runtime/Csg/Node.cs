using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools.Csg
{
    sealed class Node
    {
        public List<Polygon> polygons;

        public Node front;
        public Node back;

        public Plane plane;

        public Node()
        {
            front = null;
            back = null;
        }

        public Node(List<Polygon> list)
        {
            Build(list);
        }

        public Node(List<Polygon> list, Plane plane, Node front, Node back)
        {
            this.polygons = list;
            this.plane = plane;
            this.front = front;
            this.back = back;
        }

        public Node Clone()
        {
            Node clone = new Node(this.polygons, this.plane, this.front, this.back);

            return clone;
        }

        // Remove all polygons in this BSP tree that are inside the other BSP tree
        // `bsp`.
        public void ClipTo(Node other)
        {
            var stack = new Stack<Node>();
            stack.Push(this);

            while (stack.Count > 0)
            {
                Node node = stack.Pop();

                if (node.polygons != null)
                    node.polygons = other.ClipPolygons(node.polygons);

                if (node.front != null)
                    stack.Push(node.front);

                if (node.back != null)
                    stack.Push(node.back);
            }
        }

        // Convert solid space to empty space and empty space to solid space.
        public void Invert()
        {
            var stack = new Stack<Node>();
            stack.Push(this);

            while (stack.Count > 0)
            {
                Node node = stack.Pop();

                if (node.polygons != null)
                    for (int i = 0; i < node.polygons.Count; i++)
                        node.polygons[i].Flip();

                node.plane?.Flip();

                Node tmp = node.front;
                node.front = node.back;
                node.back = tmp;

                if (node.front != null)
                    stack.Push(node.front);

                if (node.back != null)
                    stack.Push(node.back);
            }
        }

        // Build a BSP tree out of `polygons`. When called on an existing tree, the
        // new polygons are filtered down to the bottom of the tree and become new
        // nodes there. Each set of polygons is partitioned using the first polygon
        // (no heuristic is used to pick a good split).
        public void Build(List<Polygon> list)
        {
            var stack = new Stack<(Node node, List<Polygon> list)>();
            stack.Push((this, list));

            while (stack.Count > 0)
            {
                var (node, polys) = stack.Pop();

                if (polys.Count < 1)
                    continue;

                bool newNode = node.plane == null || !node.plane.Valid();

                if (newNode)
                {
                    node.plane = new Plane();
                    node.plane.normal = polys[0].plane.normal;
                    node.plane.w = polys[0].plane.w;
                }

                if (node.polygons == null)
                    node.polygons = new List<Polygon>();

                var listFront = new List<Polygon>();
                var listBack = new List<Polygon>();

                for (int i = 0; i < polys.Count; i++)
                    node.plane.SplitPolygon(polys[i], node.polygons, node.polygons, listFront, listBack);

                // SplitPolygon can fail to correctly identify coplanar planes when the epsilon value is too low. When
                // this happens, the front or back list will be filled and built into a new node recursively. This
                // check catches that case and sorts the front/back lists into the coplanar polygons collection.
                if (listFront.Count > 0)
                {
                    if (newNode && polys.SequenceEqual(listFront))
                        node.polygons.AddRange(listFront);
                    else
                        stack.Push((node.front ?? (node.front = new Node()), listFront));
                }

                if (listBack.Count > 0)
                {
                    if (newNode && polys.SequenceEqual(listBack))
                        node.polygons.AddRange(listBack);
                    else
                        stack.Push((node.back ?? (node.back = new Node()), listBack));
                }
            }
        }

        // Remove all polygons in `polygons` that are inside this BSP tree.
        public List<Polygon> ClipPolygons(List<Polygon> list)
        {
            var result = new List<Polygon>();
            var stack = new Stack<(Node node, List<Polygon> list)>();
            stack.Push((this, list));

            while (stack.Count > 0)
            {
                var (node, polys) = stack.Pop();

                if (node.plane == null || !node.plane.Valid())
                {
                    result.AddRange(polys);
                    continue;
                }

                var listFront = new List<Polygon>();
                var listBack = new List<Polygon>();

                for (int i = 0; i < polys.Count; i++)
                    node.plane.SplitPolygon(polys[i], listFront, listBack, listFront, listBack);

                if (node.front != null)
                    stack.Push((node.front, listFront));
                else
                    result.AddRange(listFront);

                if (node.back != null)
                    stack.Push((node.back, listBack));
            }

            return result;
        }

        // Return a list of all polygons in this BSP tree.
        public List<Polygon> AllPolygons()
        {
            var list = new List<Polygon>();
            var stack = new Stack<Node>();
            stack.Push(this);

            while (stack.Count > 0)
            {
                Node node = stack.Pop();

                if (node.polygons != null)
                    list.AddRange(node.polygons);

                if (node.front != null)
                    stack.Push(node.front);

                if (node.back != null)
                    stack.Push(node.back);
            }

            return list;
        }

        #region STATIC OPERATIONS

        // Return a new CSG solid representing space in either this solid or in the
        // solid `csg`. Neither this solid nor the solid `csg` are modified.
        public static Node Union(Node a1, Node b1)
        {
            Node a = a1.Clone();
            Node b = b1.Clone();
        
            a.ClipTo(b);
            b.ClipTo(a);
            b.Invert();
            b.ClipTo(a);
            b.Invert();
        
            a.Build(b.AllPolygons());
        
            Node ret = new Node(a.AllPolygons());
        
            return ret;
        }
        
        // Return a new CSG solid representing space in this solid but not in the
        // solid `csg`. Neither this solid nor the solid `csg` are modified.
        public static Node Subtract(Node a1, Node b1)
        {
            Node a = a1.Clone();
            Node b = b1.Clone();
        
            a.Invert();
            a.ClipTo(b);
            b.ClipTo(a);
            b.Invert();
            b.ClipTo(a);
            b.Invert();
            a.Build(b.AllPolygons());
            a.Invert();
        
            Node ret = new Node(a.AllPolygons());
        
            return ret;
        }

        // Return a new CSG solid representing space both this solid and in the
        // solid `csg`. Neither this solid nor the solid `csg` are modified.
        public static Node Intersect(Node a1, Node b1)
        {
            Node a = a1.Clone();
            Node b = b1.Clone();

            a.Invert();
            b.ClipTo(a);
            b.Invert();
            a.ClipTo(b);
            b.ClipTo(a);

            a.Build(b.AllPolygons());
            a.Invert();

            Node ret = new Node(a.AllPolygons());

            return ret;
        }

        #endregion
    }
}
