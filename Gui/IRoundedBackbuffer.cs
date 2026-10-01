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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A double-buffered widget whose backbuffer composites cut to its bounds with rounded corners - agg-gui's
	/// <c>BackbufferSpec.rounded_clip</c>. <see cref="WidgetBackbuffer"/> asks for it; the window's panel is
	/// the one widget that answers.
	/// </summary>
	/// <remarks>
	/// An interface the backbuffer tests for rather than a property on <see cref="GuiWidget"/>, so the
	/// thousands of widgets that never round their layer carry nothing for it.
	/// </remarks>
	internal interface IRoundedBackbuffer
	{
		/// <summary>The corner radius in the widget's own units; 0 composites the buffer unclipped.</summary>
		double BackbufferCornerRadius { get; }

		/// <summary>
		/// True when the widget's own background paints its whole buffer opaque (up to the rounded clip), so
		/// text painted over it lands on known pixels and may carry LCD subpixel colour even though the buffer
		/// is a compositing layer - agg-gui's <c>set_layer_opaque_backdrop</c>. Without it every label inside a
		/// rounded window was greyscale with LCD text on.
		/// </summary>
		bool HasOpaqueBackdrop { get; }

		/// <summary>
		/// Paints the widget's backdrop into its buffer, before its ordinary background - unclipped, since the
		/// rounded clip the buffer is composited through shapes it. Only called when painting into the buffer.
		/// </summary>
		void PaintBackdrop(Graphics2D layerGraphics);
	}
}
