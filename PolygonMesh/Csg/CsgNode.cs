// Original CSG.JS library by Evan Wallace (http://madebyevan.com), under the MIT license.
// GitHub: https://github.com/evanw/csg.js/
// 
// C++ port by Tomasz Dabrowski (http://28byteslater.com), under the MIT license.
// GitHub: https://github.com/dabroz/csgjs-cpp/
// C# port by Lars Brubaker
// 
// Constructive Solid Geometry (CSG) is a modeling technique that uses Boolean
// operations like union and intersection to combine 3D solids. This library
// implements CSG operations on meshes elegantly and concisely using BSP trees,
// and is meant to serve as an easily understandable implementation of the
// algorithm. All edge cases involving overlapping coplanar polygons in both
// solids are correctly handled.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
    // Holds a node in a BSP tree. A BSP tree is built from a collection of polygons
    // by picking a polygon to split along. That polygon (and all other coplanar
    // polygons) are added directly to that node and the other polygons are added to
    // the front and/or back subtrees. This is not a leafy BSP tree since there is
    // no distinction between internal and leaf nodes.
    public class CsgNode
    {
        List<CsgPolygon> polygons = new List<CsgPolygon>();
        CsgNode front;
        CsgNode back;
        CsgPlane plane = new CsgPlane();

        // Return a new CSG solid representing space in this solid but not in the
        // solid `csg`. Neither this solid nor the solid `csg` are modified.
        // 
        //     A.subtract(B)
        // 
        //     +-------+            +-------+
        //     |       |            |       |
        //     |   A   |            |       |
        //     |    +--+----+   =   |    +--+
        //     +----+--+    |       +----+
        //          |   B   |
        //          |       |
        //          +-------+
        // 
        public static CsgNode Subtract(CsgNode a1, CsgNode b1)
        {
            CsgNode a = a1.Clone();
            CsgNode b = b1.Clone();
            a.Invert();
            a.ClipTo(b);
            b.ClipTo(a);
            b.Invert();
            b.ClipTo(a);
            b.Invert();
            a.BuildFromPolygons(b.GetAllPolygons());
            a.Invert();
            CsgNode ret = new CsgNode(a.GetAllPolygons());
            return ret;
        }

        // Return a new CSG solid representing space both this solid and in the
        // solid `csg`. Neither this solid nor the solid `csg` are modified.
        // 
        //     A.intersect(B)
        // 
        //     +-------+
        //     |       |
        //     |   A   |
        //     |    +--+----+   =   +--+
        //     +----+--+    |       +--+
        //          |   B   |
        //          |       |
        //          +-------+
        // 
        public static CsgNode Intersect(CsgNode a1, CsgNode b1)
        {
            CsgNode a = a1.Clone();
            CsgNode b = b1.Clone();
            a.Invert();
            b.ClipTo(a);
            b.Invert();
            a.ClipTo(b);
            b.ClipTo(a);
            a.BuildFromPolygons(b.GetAllPolygons());
            a.Invert();
            CsgNode ret = new CsgNode(a.GetAllPolygons());
            return ret;
        }

        // Return a new CSG solid representing space in either this solid or in the
        // solid `csg`. Neither this solid nor the solid `csg` are modified.
        // 
        //     +-------+            +-------+
        //     |       |            |       |
        //     |   A   |            |       |
        //     |    +--+----+   =   |       +----+
        //     +----+--+    |       +----+       |
        //          |   B   |            |       |
        //          |       |            |       |
        //          +-------+            +-------+
        //   
        public static CsgNode Union(CsgNode a1, CsgNode b1)
        {
            CsgNode a = a1.Clone();
            CsgNode b = b1.Clone();
            a.ClipTo(b);
            b.ClipTo(a);
            b.Invert();
            b.ClipTo(a);
            b.Invert();
            a.BuildFromPolygons(b.GetAllPolygons());
            CsgNode ret = new CsgNode(a.GetAllPolygons());
            return ret;
        }

        // Convert solid space to empty space and empty space to solid space.
        public void Invert()
        {
            for (int i = 0; i < this.polygons.Count; i++)
            {
                this.polygons[i].flip();
            }
            this.plane.flip();
            if (this.front != null)
            {
                this.front.Invert();
            }
            if (this.back != null)
            {
                this.back.Invert();
            }

            CsgNode hold = this.front;
            this.front = this.back;
            this.back = hold;
        }

        // Recursively remove all polygons in `polygons` that are inside this BSP
        // tree.
        public List<CsgPolygon> ClipPolygons(List<CsgPolygon> list)
        {
            if (!this.plane.ok())
            {
                return list;
            }

            List<CsgPolygon> list_front = new List<CsgPolygon>();
            List<CsgPolygon> list_back = new List<CsgPolygon>();

            for (int i = 0; i < list.Count; i++)
            {
                this.plane.SplitPolygon(list[i], list_front, list_back, list_front, list_back);
            }
            if (this.front != null)
            {
                list_front = this.front.ClipPolygons(list_front);
            }

            if (this.back != null)
            {
                list_back = this.back.ClipPolygons(list_back);
            }
            else
            {
                list_back.Clear();
            }

            list_front.AddRange(list_back);
            return list_front;
        }

        // Remove all polygons in this BSP tree that are inside the other BSP tree
        // `bsp`.
        public void ClipTo(CsgNode other)
        {
            this.polygons = other.ClipPolygons(this.polygons);
            if (this.front != null)
            {
                this.front.ClipTo(other);
            }

            if (this.back != null)
            {
                this.back.ClipTo(other);
            }
        }

        // Return a list of all polygons in this BSP tree.
        public List<CsgPolygon> GetAllPolygons()
        {
            List<CsgPolygon> list = this.polygons;
            List<CsgPolygon> list_front = new List<CsgPolygon>();
            List<CsgPolygon> list_back = new List<CsgPolygon>();

            if (this.front != null)
            {
                list_front = this.front.GetAllPolygons();
            }

            if (this.back != null)
            {
                list_back = this.back.GetAllPolygons();
            }

            list.AddRange(list_front);
            list.AddRange(list_back);
            return list;
        }

        // Return a list of all polygons in this BSP tree.
        public static int GetTotalPolygons(CsgNode root)
        {
            int total = 0;
            if (root != null)
            {
                total += GetTotalPolygons(root.back);
                total += GetTotalPolygons(root.front);
                total += root.polygons.Count;
            }
            
            return total;
        }

        public CsgNode Clone()
        {
            CsgNode ret = new CsgNode();
            ret.polygons = this.polygons;
            ret.plane = this.plane;
            if (this.front != null)
            {
                ret.front = this.front.Clone();
            }

            if (this.back != null)
            {
                ret.back = this.back.Clone();
            }

            return ret;
        }

        // Build a BSP tree out of `polygons`. When called on an existing tree, the
        // new polygons are filtered down to the bottom of the tree and become new
        // nodes there. Each set of polygons is partitioned using the first polygon
        // (no heuristic is used to pick a good split).
        public void BuildFromPolygons(List<CsgPolygon> list)
        {
            if (list.Count <= 0) return;
            if (!this.plane.ok()) this.plane = list[0].plane;
            List<CsgPolygon> list_front = new List<CsgPolygon>();
            List<CsgPolygon> list_back = new List<CsgPolygon>();

            for (int i = 0; i < list.Count; i++)
            {
                this.plane.SplitPolygon(list[i], this.polygons, this.polygons, list_front, list_back);
                int count = CsgNode.GetTotalPolygons(this);
            }

            if (list_front.Count > 0)
            {
                if (this.front == null) this.front = new CsgNode();
                this.front.BuildFromPolygons(list_front);
            }

            if (list_back.Count > 0)
            {
                if (this.back == null) this.back = new CsgNode();
                this.back.BuildFromPolygons(list_back);
            }
        }

        public CsgNode()
        {
            front = null;
            back = null;
        }

        public CsgNode(List<CsgPolygon> list)
        {
            front = null;
            back = null;
            BuildFromPolygons(list);
        }
    }
}
