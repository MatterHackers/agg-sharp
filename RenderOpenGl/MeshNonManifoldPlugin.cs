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

	public class MeshNonManifoldPlugin : IEdgeLinesContainer
	{
		public delegate void DrawToGL(Mesh meshToRender);

		public static string MeshNonManifoldPluginName => nameof(MeshNonManifoldPluginName);

		public VectorPOD<WireVertexData> EdgeLines { get; private set; } = new VectorPOD<WireVertexData>();

		private int meshUpdateCount;

		public static MeshNonManifoldPlugin Get(Mesh mesh, Color minifoldWireColor, Action meshChanged = null)
		{
			mesh.PropertyBag.TryGetValue(MeshNonManifoldPluginName, out object meshData);
			if (meshData is MeshNonManifoldPlugin plugin)
			{
				if (mesh.ChangedCount == plugin.meshUpdateCount)
				{
					return plugin;
				}

				// else we need to rebuild the data
				plugin.meshUpdateCount = mesh.ChangedCount;
				mesh.PropertyBag.Remove(MeshNonManifoldPluginName);
			}

			var newPlugin = new MeshNonManifoldPlugin();
			newPlugin.CreateRenderData(mesh, minifoldWireColor, meshChanged);
			newPlugin.meshUpdateCount = mesh.ChangedCount;
			mesh.PropertyBag.Add(MeshNonManifoldPluginName, newPlugin);

			return newPlugin;
		}

		private MeshNonManifoldPlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
		}

		private void CreateRenderData(Mesh mesh, Color wireColor, Action meshChanged = null)
		{
			var edgeLines = new VectorPOD<WireVertexData>();

			// create a quick edge list of all the polygon edges
			for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
			{
				var face = mesh.Faces[faceIndex];
                MeshWirePlugin.AddEdgeLine(edgeLines, mesh.Vertices[face.v0], mesh.Vertices[face.v1], wireColor);
                MeshWirePlugin.AddEdgeLine(edgeLines, mesh.Vertices[face.v1], mesh.Vertices[face.v2], wireColor);
                MeshWirePlugin.AddEdgeLine(edgeLines, mesh.Vertices[face.v2], mesh.Vertices[face.v0], wireColor);
			}

			this.EdgeLines = edgeLines;

			// do this in a background thread and wait for the results
			Task.Run(() =>
			{
				var filteredEdgeLines = new VectorPOD<WireVertexData>();

                foreach (var meshEdge in mesh.GetMeshEdges())
                {
                    if (meshEdge.Faces.Count() != 2)
                    {
                        MeshWirePlugin.AddEdgeLine(filteredEdgeLines,
                            mesh.Vertices[meshEdge.Vertex0Index],
                            mesh.Vertices[meshEdge.Vertex1Index],
							Color.Red);
                    }
					else
					{
                        MeshWirePlugin.AddEdgeLine(filteredEdgeLines,
                            mesh.Vertices[meshEdge.Vertex0Index],
                            mesh.Vertices[meshEdge.Vertex1Index],
							wireColor);
                    }
                }

				this.EdgeLines = filteredEdgeLines;
				meshChanged?.Invoke();
			});
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