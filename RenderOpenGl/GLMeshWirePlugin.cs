/*
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
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MatterHackers.RenderOpenGl
{
	public struct WireVertexData
	{
		public float positionsX;
		public float positionsY;
		public float positionsZ;

		public static readonly int Stride = Marshal.SizeOf(default(WireVertexData));
	}

	public class GLMeshWirePlugin
	{
		public delegate void DrawToGL(Mesh meshToRender);

		public static string GLMeshWirePluginName => nameof(GLMeshWirePluginName);

		public VectorPOD<WireVertexData> EdgeLines = new VectorPOD<WireVertexData>();

		private int meshUpdateCount;
		private double nonPlanarAngleRequired;

		static public GLMeshWirePlugin Get(Mesh mesh, double nonPlanarAngleRequired = 0)
		{
			object meshData;
			mesh.PropertyBag.TryGetValue(GLMeshWirePluginName, out meshData);
			if (meshData is GLMeshWirePlugin plugin)
			{
				if (mesh.ChangedCount == plugin.meshUpdateCount
					&& nonPlanarAngleRequired == plugin.nonPlanarAngleRequired)
				{
					return plugin;
				}

				// else we need to rebuild the data
				plugin.meshUpdateCount = mesh.ChangedCount;
				mesh.PropertyBag.Remove(GLMeshWirePluginName);
			}

			GLMeshWirePlugin newPlugin = new GLMeshWirePlugin();
			newPlugin.CreateRenderData(mesh, nonPlanarAngleRequired);
			newPlugin.meshUpdateCount = mesh.ChangedCount;
			mesh.PropertyBag.Add(GLMeshWirePluginName, newPlugin);

			return newPlugin;
		}

		private GLMeshWirePlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
		}

		private void CreateRenderData(Mesh meshToBuildListFor, double nonPlanarAngleRequired = 0)
		{
			this.nonPlanarAngleRequired = nonPlanarAngleRequired;
			var edgeLines = new VectorPOD<WireVertexData>();

			// create a quick edge list of all the polygon edges
			foreach (MeshEdge meshEdge in meshToBuildListFor.MeshEdges)
			{
				AddVertex(edgeLines, meshEdge.VertexOnEnd[0].Position, meshEdge.VertexOnEnd[1].Position);
			}
			EdgeLines = edgeLines;

			// if we are trying to have a filtered list do this in a backgroud thread and wait for the results
			if (nonPlanarAngleRequired > 0)
			{
				Task.Run(() =>
				{
					var filteredEdgeLines = new VectorPOD<WireVertexData>();

					List<(Vector3 start, Vector3 end, Vector3 normal, double length)> faceEdges = new List<(Vector3 start, Vector3 end, Vector3 normal, double length)>();
					foreach (var face in meshToBuildListFor.Faces)
					{
						foreach (var faceEdge in face.FaceEdges())
						{
							var meshEdge = faceEdge.MeshEdge;
							Vector3 start = meshEdge.VertexOnEnd[0].Position;
							Vector3 end = meshEdge.VertexOnEnd[1].Position;
							if (start.X > end.X || start.Y > end.Y || start.Z > end.Z)
							{
								var temp = start;
								start = end;
								end = temp;
							}

							// quantize the normals so we group them together
							Vector3 normal = (end - start).GetNormal();
							normal.X = ((int)(normal.X * 255)) / 255.0;
							normal.Y = ((int)(normal.Y * 255)) / 255.0;
							normal.Z = ((int)(normal.Z * 255)) / 255.0;
							faceEdges.Add((start, end, normal, (end - start).Length));
						}
					}

					// sort by direction
					faceEdges.Sort((a, b) =>
					{
						if (a.normal.X == b.normal.X)
						{
							if (a.normal.Y == b.normal.Y)
							{
								return b.normal.Z.CompareTo(a.normal.Z);
							}
							return b.normal.Y.CompareTo(a.normal.Y);
						}
						return b.normal.X.CompareTo(a.normal.X);
					});

					// group face edges into co-linear segments

					if (meshEdge.GetNumFacesSharingEdge() == 2)
					{
						FaceEdge firstFaceEdge = meshEdge.firstFaceEdge;
						FaceEdge nextFaceEdge = meshEdge.firstFaceEdge.RadialNextFaceEdge;
						double angle = Vector3.CalculateAngle(firstFaceEdge.ContainingFace.Normal, nextFaceEdge.ContainingFace.Normal);
						if (angle > MathHelper.Tau * .1)
						{
							AddVertex(filteredEdgeLines, meshEdge.VertexOnEnd[0].Position, meshEdge.VertexOnEnd[1].Position);
						}
					}

					EdgeLines = filteredEdgeLines;
				});
			}
		}

		private void AddVertex(VectorPOD<WireVertexData> edgeLines, Vector3 vertex0, Vector3 vertex1)
		{
			WireVertexData tempVertex;
			tempVertex.positionsX = (float)vertex0.X;
			tempVertex.positionsY = (float)vertex0.Y;
			tempVertex.positionsZ = (float)vertex0.Z;
			edgeLines.Add(tempVertex);

			tempVertex.positionsX = (float)vertex1.X;
			tempVertex.positionsY = (float)vertex1.Y;
			tempVertex.positionsZ = (float)vertex1.Z;
			edgeLines.Add(tempVertex);
		}

		public void Render()
		{
		}

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}
	}
}