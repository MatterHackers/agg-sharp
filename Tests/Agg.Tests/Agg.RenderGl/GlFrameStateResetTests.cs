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
using System.Threading.Tasks;
using MatterHackers.RenderGl.OpenGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// What <see cref="GL.BeginFrame"/> has to undo. A draw that throws between a push and its pop
	/// leaves the push counted; the host abandons the frame and paints the next one, and a draw that
	/// throws every frame (a scene renderer whose submit fails, say) leaks a push per frame until the
	/// guard in <see cref="GlStackBalance"/> trips at the first push of a frame - after which every frame
	/// dies there, before a single widget draws, and the window is black until the process is killed.
	/// That is how MatterCAD on X11 went from a wgpu validation error to an unclosable black window.
	/// </summary>
	public class GlFrameStateResetTests
	{
		[Test]
		public async Task ALeakedPushPerFrameTripsTheGuardWithoutBeginFrame()
		{
			// The failure this file exists for, pinned so the reset below is known to be load bearing.
			var context = new RecordingGpuContext();
			var gl = new GL(context);

			var trippedAfter = 0;
			try
			{
				for (int frame = 1; frame <= GlStackBalance.MaxMatrixDepth + 1; frame++)
				{
					trippedAfter = frame;
					DrawFrameThatThrowsAfterAPush(gl);
				}

				trippedAfter = 0;
			}
			catch (Exception ex) when (ex.Message.Contains("PushMatrix"))
			{
			}

			await Assert.That(trippedAfter).IsEqualTo(GlStackBalance.MaxMatrixDepth + 1)
				.Because("without a per-frame reset the leaked pushes accumulate until the depth guard throws");
		}

		[Test]
		public async Task BeginFrameForgetsTheLeakedPushesSoEveryFrameStartsBalanced()
		{
			var context = new RecordingGpuContext();
			var gl = new GL(context);

			for (int frame = 1; frame <= GlStackBalance.MaxMatrixDepth * 3; frame++)
			{
				gl.BeginFrame();
				DrawFrameThatThrowsAfterAPush(gl);
			}

			// And a clean frame after all of that pushes and pops from zero.
			gl.BeginFrame();
			gl.MatrixMode(MatrixMode.Projection);
			gl.PushMatrix();
			gl.PopMatrix();
			gl.MatrixMode(MatrixMode.Modelview);
			gl.PushMatrix();
			gl.PopMatrix();

			await Assert.That(context.BeginFrameCount).IsEqualTo(GlStackBalance.MaxMatrixDepth * 3 + 1)
				.Because("the context's own per-frame state (its matrix stacks) has to be reset too, so the call is forwarded");
		}

		[Test]
		public async Task BeginFrameAlsoResetsTheAttribStackAndTheMatrixMode()
		{
			var gl = new GL(new RecordingGpuContext());

			for (int i = 0; i < GlStackBalance.MaxAttribDepth; i++)
			{
				gl.PushAttrib(AttribMask.ViewportBit);
			}

			gl.MatrixMode(MatrixMode.Projection);
			gl.BeginFrame();

			// A frame's first pop, with nothing pushed this frame, is the classic-path bug this guards, so
			// the stack has to be empty rather than 100 deep; and the mode has to be back at model-view,
			// the state a fresh GL starts in and the one every host's viewport setup assumes.
			gl.PushAttrib(AttribMask.ViewportBit);
			gl.PopAttrib();

			var threw = false;
			try
			{
				gl.PopMatrix();
			}
			catch (Exception)
			{
				threw = true;
			}

			await Assert.That(threw).IsTrue().Because("after the reset nothing is pushed on any stack, so the first pop is the unbalanced one");
		}

		/// <summary>
		/// The shape of the real failure: a scene draw pushes the projection and model-view for its
		/// viewport, then throws before either pop, as Object3DControlsLayer did when the scene submit
		/// failed validation.
		/// </summary>
		private static void DrawFrameThatThrowsAfterAPush(GL gl)
		{
			gl.MatrixMode(MatrixMode.Projection);
			gl.PushMatrix();
			gl.MatrixMode(MatrixMode.Modelview);
			gl.PushMatrix();

			// The throw itself is the widget's; here only its consequence is modelled.
		}
	}
}
