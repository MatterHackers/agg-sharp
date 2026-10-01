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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A <see cref="WindowWidget"/>'s visible window inside the grab border, which carries the rounded clip its
	/// backbuffer is composited through (see <see cref="WindowWidget.CornerRadius"/>).
	/// </summary>
	internal class WindowRoundedPanel : FlowLayoutWidget, IRoundedBackbuffer
	{
		private readonly WindowWidget window;

		/// <summary>Set by <see cref="PaintBackdrop"/> for the raster it starts, so the background pass skips the body.</summary>
		private bool backdropPainted;

		public WindowRoundedPanel(WindowWidget window)
			: base(FlowDirection.TopToBottom)
		{
			this.window = window;
		}

		public double BackbufferCornerRadius { get; set; }

		public bool HasOpaqueBackdrop => this.window.PanelPaintsBody && this.window.BackgroundColor.Alpha0To255 == 255;

		/// <summary>
		/// A rounded window's body and title bar colour are painted into the panel's own buffer, rather than by
		/// the window underneath it: then the buffer is opaque where the client area draws, and its text can be
		/// subpixel (see <see cref="HasOpaqueBackdrop"/>). Into the buffer they go as plain rectangles - the
		/// rounded clip the buffer is composited through shapes the corners, so they are anti-aliased once, and
		/// every pixel of the buffer is as opaque as the body, which LCD text composited into it needs (its
		/// passes write colour, never alpha).
		/// </summary>
		public void PaintBackdrop(Graphics2D layerGraphics)
		{
			if (this.window.PanelPaintsBody)
			{
				this.window.DrawBody(layerGraphics, this.LocalBounds, square: true);
				this.backdropPainted = true;
			}
		}

		/// <summary>
		/// Unbuffered (under a zoom) nothing clips the panel, so the body is painted here, rounded, landing on the
		/// parent exactly where the window would have painted it.
		/// </summary>
		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			// Consumed before anything here can throw, so a raster that fails cannot leave it set and skip the body
			// on the next unbuffered draw.
			bool painted = this.backdropPainted;
			this.backdropPainted = false;
			if (this.window.PanelPaintsBody && !painted)
			{
				this.window.DrawBody(graphics2D, this.LocalBounds, square: false);
			}

			base.OnDrawBackground(graphics2D);
		}
	}
}
