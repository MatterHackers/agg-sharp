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

using System;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	/// <summary>
	/// A <see cref="Graphics2D"/> that can draw through an SVG compositing operator (<see cref="CompOp"/>) - what a
	/// software surface does with <see cref="BlenderCompOpBGRA"/>. The GPU does every operator where it can read the
	/// destination back (a window's swapchain may not allow it), so ask <see cref="SupportsCompOp"/> first.
	/// </summary>
	public interface ICompOpGraphics
	{
		/// <summary>True when <see cref="DrawWithCompOp"/> can apply <paramref name="op"/>.</summary>
		bool SupportsCompOp(CompOp op);

		/// <summary>
		/// Runs <paramref name="draw"/> and composites what it draws through <paramref name="op"/>, then goes back to
		/// source-over - also when <paramref name="draw"/> throws. The draws are composited as one layer (source-over
		/// among themselves), like one software span pass; shapes that should each meet what the previous one left
		/// need a call each. <paramref name="draw"/> may run twice (a coverage pass for the operators that need it). Throws <see cref="NotSupportedException"/> for an
		/// operator <see cref="SupportsCompOp"/> turns down.
		/// </summary>
		void DrawWithCompOp(CompOp op, Action draw);
	}
}
