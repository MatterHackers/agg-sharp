﻿/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.Csg;
using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.RenderOpenGl
{
	public class RenderCsgToGl
	{
		public static void Render(CsgObject objectToProcess)
		{
			RenderCsgToGl visitor = new RenderCsgToGl();
			visitor.RenderToGlRecursive((dynamic)objectToProcess);
		}

		public RenderCsgToGl()
		{
		}

		#region Visitor Pattern Functions

		public void RenderToGlRecursive(CsgObject objectToProcess)
		{
			throw new Exception("You must write the specialized function for this type.");
		}

		#region Mesh

		public void RenderToGlRecursive(Csg.Solids.MeshContainer objectToProcess)
		{
			var partColor = new ColorF(.8, .8, 1).ToColor();
			GLHelper.Render(objectToProcess.GetMesh(), partColor);
		}

		#endregion Mesh

		#region PrimitiveWrapper

		public void RenderToGlRecursive(CsgObjectWrapper objectToProcess)
		{
			RenderToGlRecursive((dynamic)objectToProcess.Root);
		}

		#endregion PrimitiveWrapper

		#region Box

		public static PolygonMesh.Mesh CreateBox(AxisAlignedBoundingBox aabb)
		{
			PolygonMesh.Mesh cube = new PolygonMesh.Mesh();
			IVertex[] verts = new Vertex[8];
			//verts[0] = cube.CreateVertex(new Vector3(-1, -1, 1));
			//verts[1] = cube.CreateVertex(new Vector3(1, -1, 1));
			//verts[2] = cube.CreateVertex(new Vector3(1, 1, 1));
			//verts[3] = cube.CreateVertex(new Vector3(-1, 1, 1));
			//verts[4] = cube.CreateVertex(new Vector3(-1, -1, -1));
			//verts[5] = cube.CreateVertex(new Vector3(1, -1, -1));
			//verts[6] = cube.CreateVertex(new Vector3(1, 1, -1));
			//verts[7] = cube.CreateVertex(new Vector3(-1, 1, -1));

			verts[0] = cube.CreateVertex(new Vector3(aabb.minXYZ.X, aabb.minXYZ.Y, aabb.maxXYZ.Z));
			verts[1] = cube.CreateVertex(new Vector3(aabb.maxXYZ.X, aabb.minXYZ.Y, aabb.maxXYZ.Z));
			verts[2] = cube.CreateVertex(new Vector3(aabb.maxXYZ.X, aabb.maxXYZ.Y, aabb.maxXYZ.Z));
			verts[3] = cube.CreateVertex(new Vector3(aabb.minXYZ.X, aabb.maxXYZ.Y, aabb.maxXYZ.Z));
			verts[4] = cube.CreateVertex(new Vector3(aabb.minXYZ.X, aabb.minXYZ.Y, aabb.minXYZ.Z));
			verts[5] = cube.CreateVertex(new Vector3(aabb.maxXYZ.X, aabb.minXYZ.Y, aabb.minXYZ.Z));
			verts[6] = cube.CreateVertex(new Vector3(aabb.maxXYZ.X, aabb.maxXYZ.Y, aabb.minXYZ.Z));
			verts[7] = cube.CreateVertex(new Vector3(aabb.minXYZ.X, aabb.maxXYZ.Y, aabb.minXYZ.Z));

			// front
			cube.CreateFace(new IVertex[] { verts[0], verts[1], verts[2], verts[3] });
			// left
			cube.CreateFace(new IVertex[] { verts[4], verts[0], verts[3], verts[7] });
			// right
			cube.CreateFace(new IVertex[] { verts[1], verts[5], verts[6], verts[2] });
			// back
			cube.CreateFace(new IVertex[] { verts[4], verts[7], verts[6], verts[5] });
			// top
			cube.CreateFace(new IVertex[] { verts[3], verts[2], verts[6], verts[7] });
			// bottom
			cube.CreateFace(new IVertex[] { verts[4], verts[5], verts[1], verts[0] });

			return cube;
		}

		public void RenderToGlRecursive(BoxPrimitive objectToProcess)
		{
			if (objectToProcess.CreateCentered)
			{
				//objectToProcess.Size;
			}
			else
			{
			}

			var partColor = new ColorF(.8, .8, 1).ToColor();
			GLHelper.Render(CreateBox(objectToProcess.GetAxisAlignedBoundingBox()), partColor);
		}

		#endregion Box

		#region Cylinder

		public static PolygonMesh.Mesh CreateCylinder(Cylinder.CylinderPrimitive cylinderToMeasure)
		{
			PolygonMesh.Mesh cylinder = new PolygonMesh.Mesh();
			List<IVertex> bottomVerts = new List<IVertex>();
			List<IVertex> topVerts = new List<IVertex>();

			int count = 20;
			for (int i = 0; i < count; i++)
			{
				Vector2 bottomRadialPos = Vector2.Rotate(new Vector2(cylinderToMeasure.Radius1, 0), MathHelper.Tau * i / 20);
				IVertex bottomVertex = cylinder.CreateVertex(new Vector3(bottomRadialPos.X, bottomRadialPos.Y, -cylinderToMeasure.Height / 2));
				bottomVerts.Add(bottomVertex);
				Vector2 topRadialPos = Vector2.Rotate(new Vector2(cylinderToMeasure.Radius1, 0), MathHelper.Tau * i / 20);
				IVertex topVertex = cylinder.CreateVertex(new Vector3(topRadialPos.X, topRadialPos.Y, cylinderToMeasure.Height / 2));
				topVerts.Add(topVertex);
			}

			cylinder.ReverseFaceEdges(cylinder.CreateFace(bottomVerts.ToArray()));
			cylinder.CreateFace(topVerts.ToArray());

			for (int i = 0; i < count - 1; i++)
			{
				cylinder.CreateFace(new IVertex[] { topVerts[i], bottomVerts[i], bottomVerts[i + 1], topVerts[i + 1] });
			}
			cylinder.CreateFace(new IVertex[] { topVerts[count - 1], bottomVerts[count - 1], bottomVerts[0], topVerts[0] });

			return cylinder;
		}

		public void RenderToGlRecursive(Cylinder.CylinderPrimitive objectToProcess)
		{
			var partColor = new ColorF(.8, .8, 1).ToColor();
			GLHelper.Render(CreateCylinder(objectToProcess), partColor);
		}

		#endregion Cylinder

		#region NGonExtrusion

		public void RenderToGlRecursive(NGonExtrusion.NGonExtrusionPrimitive objectToProcess)
		{
			throw new NotImplementedException();
#if false
            info += "cylinder(r1=" + objectToProcess.Radius1.ToString() + ", r2=" + objectToProcess.Radius1.ToString() + ", h=" + objectToProcess.Height.ToString() + ", center=true, $fn=" + objectToProcess.NumSides.ToString() + ");";

            return ApplyIndent(info);
#endif
		}

		#endregion NGonExtrusion

		#region Sphere

		public void RenderToGlRecursive(Sphere objectToProcess)
		{
			throw new NotImplementedException();
#if false
            info += "sphere(" + objectToProcess.Radius.ToString() + ", $fn=40);" ;
            return ApplyIndent(info);
#endif
		}

		#endregion Sphere

		#region Transform

		public void RenderToGlRecursive(TransformBase objectToProcess)
		{
			GL.PushMatrix();
			GL.MultMatrix(objectToProcess.transform.GetAsFloatArray());
			RenderToGlRecursive((dynamic)objectToProcess.objectToTransform);
			GL.PopMatrix();
		}

		#endregion Transform

		#region Union

		public void RenderToGlRecursive(Union objectToProcess)
		{
			// do whatever we might need to do for the renderer
			foreach (CsgObject objectToOutput in objectToProcess.AllObjects)
			{
				RenderToGlRecursive((dynamic)objectToOutput);
			}
		}

		#endregion Union

		#region Difference

		public void RenderToGlRecursive(Difference objectToProcess)
		{
			RenderToGlRecursive((dynamic)objectToProcess.Primary);
			foreach (CsgObject objectToOutput in objectToProcess.AllSubtracts)
			{
				RenderToGlRecursive((dynamic)objectToOutput);
			}
		}

		#endregion Difference

		#region Intersection

		public void RenderToGlRecursive(Intersection objectToProcess)
		{
			throw new NotImplementedException();
#if false
            return ApplyIndent("intersection()" + "\n{\n" + RenderToGlRecursive((dynamic)objectToProcess.a, level + 1) + "\n" + RenderToGlRecursive((dynamic)objectToProcess.b, level + 1) + "\n}");
#endif
		}

		#endregion Intersection

		#endregion Visitor Patern Functions
	}
}