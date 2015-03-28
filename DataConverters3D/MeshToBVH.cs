using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using MatterHackers.Agg;

namespace MatterHackers.DataConverters3D
{
    public static class MeshToBVH
    {
        public static IRayTraceable Convert(PolygonMesh.Mesh simpleMesh)
        {
            List<IRayTraceable> renderCollection = new List<IRayTraceable>();

            //SolidMaterial redStuff = new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0);
			RGBA_Bytes partColor = new RGBA_Bytes(0, 130, 153);
			SolidMaterial mhBlueStuff = new SolidMaterial(partColor.GetAsRGBA_Floats(), .01, 0.0, 2.0);
            int index = 0;
            Vector3[] triangle = new Vector3[3];
            //PolygonMesh.Mesh simpleMesh = PolygonMesh.Processors.StlProcessing.Load("complex.stl");
            //PolygonMesh.Mesh simpleMesh = PolygonMesh.Processors.StlProcessing.Load("Spider With Base.stl");
            foreach (PolygonMesh.Face face in simpleMesh.Faces)
            {
                foreach (PolygonMesh.Vertex vertex in face.Vertices())
                {
                    triangle[index++] = vertex.Position;
                    if (index == 3)
                    {
                        index = 0;
                        renderCollection.Add(new TriangleShape(triangle[0], triangle[1], triangle[2], mhBlueStuff));
                    }
                }
            }

            return BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
        }

        public static IRayTraceable ConvertUnoptomized(PolygonMesh.Mesh simpleMesh)
        {
            List<IRayTraceable> renderCollection = new List<IRayTraceable>();

            //SolidMaterial redStuff = new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0);
            SolidMaterial mhBlueStuff = new SolidMaterial(new RGBA_Floats(0, .32, .58), .01, 0.0, 2.0);
            int index = 0;
            Vector3[] triangle = new Vector3[3];
            //PolygonMesh.Mesh simpleMesh = PolygonMesh.Processors.StlProcessing.Load("complex.stl");
            //PolygonMesh.Mesh simpleMesh = PolygonMesh.Processors.StlProcessing.Load("Spider With Base.stl");
            foreach (PolygonMesh.Face face in simpleMesh.Faces)
            {
                foreach (PolygonMesh.Vertex vertex in face.Vertices())
                {
                    triangle[index++] = vertex.Position;
                    if (index == 3)
                    {
                        index = 0;
                        renderCollection.Add(new TriangleShape(triangle[0], triangle[1], triangle[2], mhBlueStuff));
                    }
                }
            }

            return new UnboundCollection(renderCollection);
        }
    }
}
