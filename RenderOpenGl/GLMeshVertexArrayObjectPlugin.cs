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
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.RenderOpenGl
{
	using gl = GL;
	
	public class GLMeshVertexArrayObjectPlugin
	{
		public delegate void DrawToGL(Mesh meshToRender);

		/// <summary>
		/// The vertex array object (an index into gl)
		/// </summary>
		public int Vao;

		private int meshUpdateCount;

		public static string GLMeshVertexArrayObjectPluginName => nameof(GLMeshVertexArrayObjectPluginName);

		static public GLMeshVertexArrayObjectPlugin Get(Mesh mesh, Func<Vector3Float, Color> getColorFunc = null)
		{
			object meshData;
			mesh.PropertyBag.TryGetValue(GLMeshVertexArrayObjectPluginName, out meshData);
			if (meshData is GLMeshVertexArrayObjectPlugin plugin)
			{
				if (mesh.ChangedCount == plugin.meshUpdateCount)
				{
					return plugin;
				}

				// else we need to rebuild the data
				plugin.meshUpdateCount = mesh.ChangedCount;
				mesh.PropertyBag.Remove(GLMeshVertexArrayObjectPluginName);
			}

			GLMeshVertexArrayObjectPlugin newPlugin = new GLMeshVertexArrayObjectPlugin();
			newPlugin.CreateRenderData(mesh, getColorFunc);
			newPlugin.meshUpdateCount = mesh.ChangedCount;
			mesh.PropertyBag.Add(GLMeshVertexArrayObjectPluginName, newPlugin);

			return newPlugin;
		}

		private GLMeshVertexArrayObjectPlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGLDisplayListPlugin.
		}

		private void CreateRenderData(Mesh mesh, Func<Vector3Float, Color> getColorFunc)
		{
			//var verticesPositionsNormals = mesh.ToPositionNormalArray();
			var verticesPositions = mesh.Vertices.ToFloatArray(); 
			var faceVertexIndices = mesh.Faces.ToIntArray();

			// Generate vertex array
			gl.GenVertexArrays(1, out Vao);
			gl.BindVertexArray(Vao);

			// Generate Vertex Buffer
			gl.GenBuffers(1, out int VBO);
			gl.BindBuffer(GL.ARRAY_BUFFER, VBO);
			gl.BufferData(GL.ARRAY_BUFFER, verticesPositions, GL.STATIC_DRAW); 
			//gl.BufferData(GL.ARRAY_BUFFER, verticesPositionsNormals, GL.STATIC_DRAW);

			// Generate Index Buffer
			gl.GenBuffers(1, out int FBO);
			gl.BindBuffer(GL.ELEMENT_ARRAY_BUFFER, FBO);
			gl.BufferData(GL.ELEMENT_ARRAY_BUFFER, faceVertexIndices, GL.STATIC_DRAW);
			gl.VertexAttribPointer(0, 3, GL.FLOAT, GL.FALSE, 0, IntPtr.Zero);

			gl.EnableVertexAttribArray(0);
			gl.BindBuffer(GL.ARRAY_BUFFER, 0);
			gl.BindVertexArray(0);
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