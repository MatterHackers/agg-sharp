/*
Copyright (c) 2026, Lars Brubaker
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
using MatterHackers.Agg;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// The one place a run of <see cref="PosColorVertex"/> becomes draw calls.
	/// </summary>
	/// <remarks>
	/// Both escape hatches - <see cref="ISceneDrawContext.DrawPrimitives"/> in 3D and
	/// <c>Graphics2DGpu.DrawColoredPrimitives</c> in ortho space - land here, so there is exactly one copy
	/// of the state setup and the vertex loop. This still goes through the compat layer's immediate mode:
	/// these primitives are per-frame throwaway geometry with no mesh behind them, which is precisely
	/// what the retained scene path has no representation for.
	/// </remarks>
	internal static class GlPrimitiveEmitter
	{
		/// <summary>
		/// Emits the vertices, leaving behind the enable state it set.
		/// </summary>
		/// <remarks>
		/// Deliberately does not restore anything: on return Texture2D is off, CullFace is off, Blend is on
		/// with the standard src-alpha function, and in 3D (a non-null <c>depthTest</c>) Lighting is off and
		/// the depth test is whatever was asked for. The helpers this consolidated - the line, plane and path
		/// renderers of the GL era - each set exactly these bits and left them set, and every caller already
		/// sets the state it needs before it draws, so restoring here would be new behavior rather than a fix.
		/// (A <c>PushAttrib</c>/<c>PopAttrib</c> pair used to bracket this and looked like it restored them;
		/// <see cref="Compat.GlStateShadow.PushAttrib"/> only ever saves the viewport, so it restored nothing.)
		/// </remarks>
		/// <param name="gl">The facade to draw through.</param>
		/// <param name="topology">How the vertices assemble.</param>
		/// <param name="vertices">The vertices. An empty run draws nothing at all, not even state changes.</param>
		/// <param name="transform">Applied on top of the current model-view matrix.</param>
		/// <param name="depthTest">Whether to depth test, or null to leave the depth state (and the
		/// lighting state) exactly as the caller left it - what the ortho-space 2D sites want, since a
		/// widget frame has already settled both.</param>
		public static void Emit(
			GL gl,
			DrawTopology topology,
			ReadOnlySpan<PosColorVertex> vertices,
			Matrix4X4 transform,
			bool? depthTest)
		{
			if (gl == null
				|| vertices.Length == 0)
			{
				return;
			}

			gl.Disable(EnableCap.Texture2D);

			// Back faces are as meaningful as front faces for a hand-built strip or line list, and the
			// winding of these is whatever the emitting code happened to write.
			gl.Disable(EnableCap.CullFace);

			gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			gl.Enable(EnableCap.Blend);

			if (depthTest.HasValue)
			{
				// The colours are the vertices' own; lit shading would tint them by a normal these
				// vertices do not carry.
				gl.Disable(EnableCap.Lighting);

				if (depthTest.Value)
				{
					gl.Enable(EnableCap.DepthTest);
				}
				else
				{
					gl.Disable(EnableCap.DepthTest);
				}
			}

			gl.MatrixMode(MatrixMode.Modelview);
			gl.PushMatrix();
			gl.MultMatrix(transform.GetAsFloatArray());

			gl.Begin(ToBeginMode(topology));

			for (int i = 0; i < vertices.Length; i++)
			{
				var vertex = vertices[i];
				var color = vertex.Color;
				gl.Color4(color.Red0To255, color.Green0To255, color.Blue0To255, color.Alpha0To255);
				gl.Vertex3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z);
			}

			gl.End();

			gl.PopMatrix();
		}

		private static BeginMode ToBeginMode(DrawTopology topology)
		{
			switch (topology)
			{
				case DrawTopology.TriangleList:
					return BeginMode.Triangles;

				case DrawTopology.TriangleStrip:
					return BeginMode.TriangleStrip;

				case DrawTopology.LineList:
					return BeginMode.Lines;

				default:
					throw new ArgumentOutOfRangeException(nameof(topology), topology, "Unhandled draw topology.");
			}
		}
	}
}
