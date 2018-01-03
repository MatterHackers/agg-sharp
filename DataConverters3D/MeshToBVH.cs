using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D
{
	public static class MeshToBVH
	{
		public static IPrimitive Convert(Mesh mesh, MaterialAbstract partMaterial = null)
		{
			return Convert(new Object3D()
			{
				Mesh = mesh
			});
		}

		public static IPrimitive Convert(IObject3D renderData)
		{
			List<IPrimitive> renderCollection = new List<IPrimitive>();

			SolidMaterial partMaterial;
			var color = renderData.WorldColor(null);
			if (color.alpha != 0)
			{
				partMaterial = new SolidMaterial(new ColorF(color.Red0To1, color.Green0To1, color.Blue0To1), .01, 0.0, 2.0);
			}
			else
			{
				partMaterial = new SolidMaterial(new ColorF(.9, .2, .1), .01, 0.0, 2.0);
			}

			var worldMatrix = renderData.WorldMatrix(null);
			foreach (Face face in renderData.Mesh.Faces)
			{
				if (false)
				{
					renderCollection.Add(new MeshFaceTraceable(face, partMaterial, worldMatrix));
				}
				else
				{
					foreach (var triangle in face.AsTriangles())
					{
						renderCollection.Add(new TriangleShape(
							Vector3.Transform(triangle.p0, worldMatrix),
							Vector3.Transform(triangle.p1, worldMatrix),
							Vector3.Transform(triangle.p2, worldMatrix),
							partMaterial));
					}
				}
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
		}

		public static IPrimitive Convert(List<IObject3D> renderDatas)
		{
			List<IPrimitive> renderCollection = new List<IPrimitive>();
			foreach (var renderData in renderDatas)
			{
				renderCollection.Add(Convert(renderData));
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
		}

		public static IPrimitive Convert(MeshGroup meshGroup, MaterialAbstract partMaterial = null)
		{
			List<IPrimitive> renderCollection = new List<IPrimitive>();

			if (partMaterial == null)
			{
				partMaterial = new SolidMaterial(new ColorF(.9, .2, .1), .01, 0.0, 2.0);
			}
			int index = 0;
			Vector3[] triangle = new Vector3[3];
			foreach (Mesh mesh in meshGroup.Meshes)
			{
				foreach (Face face in mesh.Faces)
				{
					foreach (Vertex vertex in face.Vertices())
					{
						triangle[index++] = vertex.Position;
						if (index == 3)
						{
							index = 0;
							renderCollection.Add(new TriangleShape(triangle[0], triangle[1], triangle[2], partMaterial));
						}
					}
				}
			}

			return BoundingVolumeHierarchy.CreateNewHierachy(renderCollection);
		}

		public static IPrimitive ConvertUnoptomized(Mesh simpleMesh)
		{
			List<IPrimitive> renderCollection = new List<IPrimitive>();

			//SolidMaterial redStuff = new SolidMaterial(new RGBA_Floats(.9, .2, .1), .01, 0.0, 2.0);
			SolidMaterial mhBlueStuff = new SolidMaterial(new ColorF(0, .32, .58), .01, 0.0, 2.0);
			int index = 0;
			Vector3[] triangle = new Vector3[3];
			//Mesh simpleMesh = Processors.StlProcessing.Load("complex.stl");
			//Mesh simpleMesh = Processors.StlProcessing.Load("Spider With Base.stl");
			foreach (Face face in simpleMesh.Faces)
			{
				foreach (Vertex vertex in face.Vertices())
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