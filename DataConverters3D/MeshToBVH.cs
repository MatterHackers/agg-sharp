using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System.Collections.Generic;

namespace MatterHackers.DataConverters3D
{
	public static class MeshToBVH
	{
		public static IPrimitive Convert(PolygonMesh.Mesh simpleMesh, MaterialAbstract partMaterial = null)
		{
			List<IPrimitive> renderCollection = new List<IPrimitive>();

			if (partMaterial == null)
			{
				partMaterial = new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0);
			}
			int index = 0;
			Vector3[] triangle = new Vector3[3];
			foreach (PolygonMesh.Face face in simpleMesh.Faces)
			{
				foreach (PolygonMesh.Vertex vertex in face.Vertices())
				{
					triangle[index++] = vertex.Position;
					if (index == 3)
					{
						index = 0;
						renderCollection.Add(new TriangleShape(triangle[0], triangle[1], triangle[2], partMaterial));
					}
				}
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
		}

		public static IPrimitive Convert(List<PolygonMesh.MeshGroup> meshGroups, MaterialAbstract partMaterial = null)
		{
			List<IPrimitive> renderCollection = new List<IPrimitive>();
			foreach (MeshGroup meshGroup in meshGroups)
			{
				renderCollection.Add(Convert(meshGroup, partMaterial));
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
		}

		public static IPrimitive Convert(PolygonMesh.MeshGroup meshGroup, MaterialAbstract partMaterial = null)
		{
			List<IPrimitive> renderCollection = new List<IPrimitive>();

			SolidMaterial otherMaterial = new SolidMaterial(new RGBA_Floats(.1, .2, .9), .01, 0.0, 2.0);
			if (partMaterial == null)
			{
				partMaterial = new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0);
			}
			int index = 0;
			Vector3[] triangle = new Vector3[3];
			foreach (PolygonMesh.Mesh mesh in meshGroup.Meshes)
			{
				int extruderIntdex = MeshExtruderData.Get(mesh).ExtruderIndex;
				foreach (PolygonMesh.Face face in mesh.Faces)
				{
					foreach (PolygonMesh.Vertex vertex in face.Vertices())
					{
						if (false)
						{
							if (extruderIntdex == 1)
							{
								renderCollection.Add(new MeshFaceTraceable(face, partMaterial));
							}
							else
							{
								renderCollection.Add(new MeshFaceTraceable(face, otherMaterial));
							}
						}
						else
						{
							triangle[index++] = vertex.Position;
							if (index == 3)
							{
								index = 0;
								if (extruderIntdex == 1)
								{
									renderCollection.Add(new TriangleShape(triangle[0], triangle[1], triangle[2], partMaterial));
								}
								else
								{
									renderCollection.Add(new TriangleShape(triangle[0], triangle[1], triangle[2], otherMaterial));
								}
							}
						}
					}
				}
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
		}

		public static IPrimitive ConvertUnoptomized(PolygonMesh.Mesh simpleMesh)
		{
			List<IPrimitive> renderCollection = new List<IPrimitive>();

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