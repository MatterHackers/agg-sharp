using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.PolygonMesh.Csg
{
    // Represents a convex polygon. The vertices used to initialize a polygon must
    // be coplanar and form a convex loop. They do not have to be `CSG.Vertex`
    // instances but they must behave similarly.
    // 
    // Each convex polygon has a `shared` property, which is shared between all
    // polygons that are clones of each other or were split from the same polygon.
    // This can be used to define per-polygon properties (such as surface color).
    public class CsgPolygon
    {
        public List<Vertex> vertices = new List<Vertex>();
        public CsgPlane plane;

        public void flip()
        {
            vertices.Reverse();
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i].Normal = -vertices[i].Normal;
            }
            plane.flip();
        }

        public CsgPolygon()
        {
        }

        public CsgPolygon(List<Vertex> list)
        {
            vertices = new List<Vertex>(list);
            plane = new CsgPlane(vertices[0].Position, vertices[1].Position, vertices[2].Position);
        }
    }
}
