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
*/

using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// GL's matrix stacks, ported verbatim from the classic D3D11 context so the two paths compose
	/// transforms identically. Three stacks exist because GL has three; only the model-view and
	/// projection ones are reachable today, because <see cref="OpenGl.MatrixMode"/> declares no texture
	/// mode. The texture matrix is still written into the uniform block (as identity) so shaders can be
	/// authored against a stable layout.
	/// <para>
	/// The composition order is the surprising part and is deliberately copied: every concatenating
	/// operation computes <c>newMatrix * current</c>, i.e. the new transform is applied to the vertex
	/// <em>first</em>. That is correct for GL's column-vector convention re-expressed in this codebase's
	/// row-vector matrices, and reversing it silently mirrors and misplaces everything.
	/// </para>
	/// </summary>
	public class GlMatrixStacks
	{
		private readonly Stack<Matrix4X4> modelViewStack = new Stack<Matrix4X4>(new[] { Matrix4X4.Identity });
		private readonly Stack<Matrix4X4> projectionStack = new Stack<Matrix4X4>(new[] { Matrix4X4.Identity });
		private readonly Stack<Matrix4X4> textureStack = new Stack<Matrix4X4>(new[] { Matrix4X4.Identity });

		/// <summary>Which stack the mutating operations act on.</summary>
		public OpenGl.MatrixMode Mode { get; set; } = OpenGl.MatrixMode.Modelview;

		/// <summary>The current model-view matrix.</summary>
		public Matrix4X4 ModelView => this.modelViewStack.Peek();

		/// <summary>The current projection matrix, in GL clip space.</summary>
		public Matrix4X4 Projection => this.projectionStack.Peek();

		/// <summary>The current texture matrix.</summary>
		public Matrix4X4 Texture => this.textureStack.Peek();

		/// <summary>Depth of the model-view stack, counting the always-present bottom entry.</summary>
		public int ModelViewDepth => this.modelViewStack.Count;

		/// <summary>Depth of the projection stack, counting the always-present bottom entry.</summary>
		public int ProjectionDepth => this.projectionStack.Count;

		/// <summary>Replaces the current matrix with the identity.</summary>
		public void LoadIdentity() => this.Replace(Matrix4X4.Identity);

		/// <summary>Replaces the current matrix.</summary>
		/// <param name="matrix">The new matrix.</param>
		public void Load(Matrix4X4 matrix) => this.Replace(matrix);

		/// <summary>Concatenates a transform, applied to the vertex before the current matrix.</summary>
		/// <param name="matrix">The transform to concatenate.</param>
		public void Multiply(Matrix4X4 matrix)
		{
			var stack = this.Current;
			stack.Push(matrix * stack.Pop());
		}

		/// <summary>Duplicates the top of the current stack.</summary>
		public void Push()
		{
			var stack = this.Current;
			stack.Push(stack.Peek());
		}

		/// <summary>
		/// Drops the top of the current stack. Popping the last entry is ignored rather than throwing -
		/// GL leaves an unbalanced pop undefined, and the classic path chose to survive it.
		/// </summary>
		public void Pop()
		{
			var stack = this.Current;
			if (stack.Count > 1)
			{
				stack.Pop();
			}
		}

		/// <summary>Concatenates an orthographic projection, matching <c>glOrtho</c>.</summary>
		/// <param name="left">Left clip plane.</param>
		/// <param name="right">Right clip plane.</param>
		/// <param name="bottom">Bottom clip plane.</param>
		/// <param name="top">Top clip plane.</param>
		/// <param name="zNear">Near clip plane.</param>
		/// <param name="zFar">Far clip plane.</param>
		public void Ortho(double left, double right, double bottom, double top, double zNear, double zFar)
		{
			double width = right - left;
			double height = top - bottom;
			double depth = zFar - zNear;

			this.Multiply(new Matrix4X4(
				2.0 / width, 0, 0, 0,
				0, 2.0 / height, 0, 0,
				0, 0, -2.0 / depth, 0,
				-(right + left) / width, -(top + bottom) / height, -(zFar + zNear) / depth, 1));
		}

		/// <summary>Concatenates a translation.</summary>
		/// <param name="x">X offset.</param>
		/// <param name="y">Y offset.</param>
		/// <param name="z">Z offset.</param>
		public void Translate(double x, double y, double z) => this.Multiply(Matrix4X4.CreateTranslation(x, y, z));

		/// <summary>Concatenates a rotation about an axis.</summary>
		/// <param name="degrees">Rotation angle in degrees, as GL takes it.</param>
		/// <param name="x">Axis X.</param>
		/// <param name="y">Axis Y.</param>
		/// <param name="z">Axis Z.</param>
		public void Rotate(double degrees, double x, double y, double z)
			=> this.Multiply(Matrix4X4.CreateRotation(new Vector3(x, y, z), MathHelper.DegreesToRadians(degrees)));

		/// <summary>Concatenates a scale.</summary>
		/// <param name="x">X scale.</param>
		/// <param name="y">Y scale.</param>
		/// <param name="z">Z scale.</param>
		public void Scale(double x, double y, double z) => this.Multiply(Matrix4X4.CreateScale(x, y, z));

		private Stack<Matrix4X4> Current
			=> this.Mode == OpenGl.MatrixMode.Projection ? this.projectionStack : this.modelViewStack;

		private void Replace(Matrix4X4 matrix)
		{
			var stack = this.Current;
			stack.Pop();
			stack.Push(matrix);
		}
	}
}
