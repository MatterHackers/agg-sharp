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

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
	interface IEdgeLinesContainer
	{
		VectorPOD<WireVertexData> EdgeLines { get; }
	}

	public struct WireVertexData
	{
		public float PositionsX;
		public float PositionsY;
		public float PositionsZ;

		public static readonly int Stride = Marshal.SizeOf(default(WireVertexData));
	}

	public class GLMeshWirePlugin : IEdgeLinesContainer
	{
		public delegate void DrawToGL(Mesh meshToRender);

		public static string GLMeshWirePluginName => nameof(GLMeshWirePluginName);

		public VectorPOD<WireVertexData> EdgeLines { get; private set; } = new VectorPOD<WireVertexData>();

		private int meshUpdateCount;
		private double nonPlanarAngleRequired;

		public static GLMeshWirePlugin Get(Mesh mesh, double nonPlanarAngleRequired = 0, Action meshChanged = null)
		{
			mesh.PropertyBag.TryGetValue(GLMeshWirePluginName, out object meshData);
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

			var newPlugin = new GLMeshWirePlugin();
			newPlugin.CreateRenderData(mesh, nonPlanarAngleRequired, meshChanged);
			newPlugin.meshUpdateCount = mesh.ChangedCount;
			mesh.PropertyBag.Add(GLMeshWirePluginName, newPlugin);

			return newPlugin;
		}

		private GLMeshWirePlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
		}

		private void CreateRenderData(Mesh mesh, double nonPlanarAngleRequired = 0, Action meshChanged = null)
		{
			this.nonPlanarAngleRequired = nonPlanarAngleRequired;
			var edgeLines = new VectorPOD<WireVertexData>();

			this.EdgeLines = edgeLines;

			// if we are trying to have a filtered list do this in a background thread and wait for the results
			if (nonPlanarAngleRequired > 0)
			{
				Task.Run(() =>
				{
					var meshEdgeList = mesh.NewMeshEdges();

					var filteredEdgeLines = new VectorPOD<WireVertexData>();

					foreach (var meshEdge in meshEdgeList)
					{
						if (meshEdge.Faces.Count() == 2)
						{
							var faceNormal0 = mesh.Faces[meshEdge.Faces[0]].normal;
							var faceNormal1 = mesh.Faces[meshEdge.Faces[1]].normal;
							double angle = faceNormal0.CalculateAngle(faceNormal1);
							if (angle > nonPlanarAngleRequired)
							{
								AddVertex(filteredEdgeLines,
									mesh.Vertices[meshEdge.Vertex0Index],
									mesh.Vertices[meshEdge.Vertex1Index]);
							}
						}
					}

					this.EdgeLines = filteredEdgeLines;
					meshChanged?.Invoke();
				});
			}
            else
            {
				// create a quick edge list of all the polygon edges
				for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
				{
					var face = mesh.Faces[faceIndex];
					AddVertex(edgeLines, mesh.Vertices[face.v0], mesh.Vertices[face.v1]);
					AddVertex(edgeLines, mesh.Vertices[face.v1], mesh.Vertices[face.v2]);
					AddVertex(edgeLines, mesh.Vertices[face.v2], mesh.Vertices[face.v0]);
				}
			}
		}

		private void AddVertex(VectorPOD<WireVertexData> edgeLines, Vector3Float vertex0, Vector3Float vertex1)
		{
			WireVertexData tempVertex;
			tempVertex.PositionsX = (float)vertex0.X;
			tempVertex.PositionsY = (float)vertex0.Y;
			tempVertex.PositionsZ = (float)vertex0.Z;
			edgeLines.Add(tempVertex);

			tempVertex.PositionsX = (float)vertex1.X;
			tempVertex.PositionsY = (float)vertex1.Y;
			tempVertex.PositionsZ = (float)vertex1.Z;
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