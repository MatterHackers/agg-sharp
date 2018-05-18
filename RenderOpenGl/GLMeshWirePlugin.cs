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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

		private static ConditionalWeakTable<Mesh, GLMeshWirePlugin> meshesWithCacheData = new ConditionalWeakTable<Mesh, GLMeshWirePlugin>();

		public VectorPOD<WireVertexData> edgeLinesData = new VectorPOD<WireVertexData>();

		private int meshUpdateCount;
		private double nonPlanarAngleRequired;

		static public GLMeshWirePlugin Get(Mesh meshToGetDisplayListFor, double nonPlanarAngleRequired = 0)
		{
			GLMeshWirePlugin plugin;
			meshesWithCacheData.TryGetValue(meshToGetDisplayListFor, out plugin);

			if (plugin != null
				&& (meshToGetDisplayListFor.ChangedCount != plugin.meshUpdateCount
				|| nonPlanarAngleRequired != plugin.nonPlanarAngleRequired))
			{
				plugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;
				plugin.AddRemoveData();
				plugin.CreateRenderData(meshToGetDisplayListFor, nonPlanarAngleRequired);
				plugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;
				plugin.nonPlanarAngleRequired = nonPlanarAngleRequired;
			}

			if (plugin == null)
			{
				GLMeshWirePlugin newPlugin = new GLMeshWirePlugin();
				meshesWithCacheData.Add(meshToGetDisplayListFor, newPlugin);
				newPlugin.CreateRenderData(meshToGetDisplayListFor, nonPlanarAngleRequired);
				newPlugin.meshUpdateCount = meshToGetDisplayListFor.ChangedCount;
				newPlugin.nonPlanarAngleRequired = nonPlanarAngleRequired;

				return newPlugin;
			}

			return plugin;
		}

		private GLMeshWirePlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
		}

		private void AddRemoveData()
		{
		}

		~GLMeshWirePlugin()
		{
			AddRemoveData();
		}

		private void CreateRenderData(Mesh meshToBuildListFor, double nonPlanarAngleRequired = 0)
		{
			edgeLinesData = new VectorPOD<WireVertexData>();
			// first make sure all the textures are created
			foreach (MeshEdge meshEdge in meshToBuildListFor.MeshEdges)
			{
				if (nonPlanarAngleRequired > 0)
				{
					if (meshEdge.GetNumFacesSharingEdge() == 2)
					{
						FaceEdge firstFaceEdge = meshEdge.firstFaceEdge;
						FaceEdge nextFaceEdge = meshEdge.firstFaceEdge.radialNextFaceEdge;
						double angle = Vector3.CalculateAngle(firstFaceEdge.ContainingFace.Normal, nextFaceEdge.ContainingFace.Normal);
						if (angle > MathHelper.Tau * .1)
						{
							edgeLinesData.Add(AddVertex(meshEdge.VertexOnEnd[0].Position, meshEdge.VertexOnEnd[1].Position));
						}
					}
					else
					{
						edgeLinesData.Add(AddVertex(meshEdge.VertexOnEnd[0].Position, meshEdge.VertexOnEnd[1].Position));
					}
				}
				else
				{
					edgeLinesData.Add(AddVertex(meshEdge.VertexOnEnd[0].Position, meshEdge.VertexOnEnd[1].Position));
				}
			}
		}

		private WireVertexData AddVertex(Vector3 vertex0, Vector3 vertex1)
		{
			WireVertexData tempVertex;
			tempVertex.positionsX = (float)vertex0.X;
			tempVertex.positionsY = (float)vertex0.Y;
			tempVertex.positionsZ = (float)vertex0.Z;
			edgeLinesData.Add(tempVertex);

			tempVertex.positionsX = (float)vertex1.X;
			tempVertex.positionsY = (float)vertex1.Y;
			tempVertex.positionsZ = (float)vertex1.Z;
			return tempVertex;
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