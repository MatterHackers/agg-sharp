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

using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg.SvgTools;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public class SvgWidget : GuiWidget
	{
		List<ColoredVertexSource> items = new List<ColoredVertexSource>();

		private ImageBuffer imageBuffer;

		public double Scale { get; set; } = 0.7;

        public SvgWidget()
		{
		}

        public SvgWidget(string filePath, double scale, int width = -1, int height = -1)
		{
			using (var stream = File.OpenRead(filePath))
			{
				LoadSvg(stream, scale, width, height);
			}
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
			graphics2D.Render(imageBuffer, Point2D.Zero);

			base.OnDraw(graphics2D);
		}
	}
}
