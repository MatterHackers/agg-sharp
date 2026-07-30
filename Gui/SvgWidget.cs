/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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
using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg.SvgTools;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public class SvgWidget : GuiWidget
	{
		List<ColoredVertexSource> items = new List<ColoredVertexSource>();

		private ImageBuffer imageBuffer;

		// Deferred-load state for the file-path constructor. The file read, SVG parse and
		// software rasterization are heavy, so they run in OnLoad rather than the constructor.
		private string deferredFilePath;
		private double deferredScale;
		private int deferredWidth;
		private int deferredHeight;

		public double Scale { get; set; } = 0.7;

        public SvgWidget()
		{
		}

        public SvgWidget(string filePath, double scale, int width = -1, int height = -1)
		{
			deferredFilePath = filePath;
			deferredScale = scale;
			deferredWidth = width;
			deferredHeight = height;

			// Sizing does not depend on the parse - it is computed from the passed-in
			// dimensions exactly as LoadSvg does - so set it now and defer the heavy work.
			this.Scale = scale;
			this.MinimumSize = new Vector2((int)(width * scale), (int)(height * scale));
		}

		public override void OnLoad(EventArgs args)
		{
			if (deferredFilePath != null)
			{
				var filePath = deferredFilePath;
				deferredFilePath = null;

				using (var stream = File.OpenRead(filePath))
				{
					LoadSvg(stream, deferredScale, deferredWidth, deferredHeight);
				}
			}

			base.OnLoad(args);
		}

		public void LoadSvg(Stream stream, double scale, int width = -1, int height = -1)
		{
			items = SvgParser.Parse(stream, false);

			this.Scale = scale;

			width = (int)(width * this.Scale);
			height = (int)(height * this.Scale);

			imageBuffer = new ImageBuffer(width, height);

			this.MinimumSize = new Vector2(width, height);

			var graphics2D = imageBuffer.NewGraphics2D();

			graphics2D.SetTransform(Affine.NewScaling(this.Scale));
			foreach (var item in items)
			{
				graphics2D.Render(item.VertexSource, item.Color);
			}

			imageBuffer.FlipY();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (!onloadInvoked)
			{
				// Set onloadInvoked before invoking OnLoad to ensure we only fire once.
				// This draw happens before base.OnDraw would fire OnLoad, and the deferred
				// parse/rasterize in OnLoad must run before imageBuffer is rendered.
				onloadInvoked = true;

				this.OnLoad(null);
			}

			graphics2D.Render(imageBuffer, Point2D.Zero);

			base.OnDraw(graphics2D);
		}
	}
}
