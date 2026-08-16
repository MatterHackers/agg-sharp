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

using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	/// <summary>
	/// How a run of <see cref="PosColorVertex"/> is assembled into primitives.
	/// </summary>
	/// <remarks>
	/// Deliberately tiny: these are the three shapes the hand-written immediate-mode draw sites in the
	/// application actually emit (a hue ring as a strip, a gradient triangle, path outlines as a line
	/// list). It is declared here rather than in the render backend because <see cref="Graphics2D"/> -
	/// which agg owns - takes it, and agg must not depend on a renderer.
	/// </remarks>
	public enum DrawTopology
	{
		/// <summary>Every three vertices are one triangle.</summary>
		TriangleList,

		/// <summary>Each vertex after the first two closes another triangle with the two before it.</summary>
		TriangleStrip,

		/// <summary>Every two vertices are one line segment.</summary>
		LineList,
	}

	/// <summary>
	/// One vertex of an ad-hoc primitive: a position and its own colour.
	/// </summary>
	/// <remarks>
	/// Per-vertex colour is the whole reason this type exists - a gradient across a primitive cannot be
	/// expressed as a vertex source plus one colour, which is why the sites that need it reached past
	/// <see cref="Graphics2D"/> into raw immediate mode.
	/// </remarks>
	public readonly struct PosColorVertex
	{
		public PosColorVertex(Vector3 position, Color color)
		{
			this.Position = position;
			this.Color = color;
		}

		/// <summary>A 2D vertex, at z = 0. The form the ortho-space widget draws use.</summary>
		public PosColorVertex(Vector2 position, Color color)
			: this(new Vector3(position.X, position.Y, 0), color)
		{
		}

		public Color Color { get; }

		public Vector3 Position { get; }
	}
}
