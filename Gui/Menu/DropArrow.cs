/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class DropArrow : GuiWidget
	{
		public static VertexStorage DownArrow = null;
		public static VertexStorage UpArrow = null;

		public static int ArrowHeight { get; set; } = 5;

		static DropArrow()
		{
			DownArrow = new VertexStorage();
			DownArrow.MoveTo(-ArrowHeight, 0);
			DownArrow.LineTo(ArrowHeight, 0);
			DownArrow.LineTo(0, -ArrowHeight);

			UpArrow = new VertexStorage();
			UpArrow.MoveTo(-ArrowHeight, -ArrowHeight);
			UpArrow.LineTo(ArrowHeight, -ArrowHeight);
			UpArrow.LineTo(0, 0);
		}

		public DropArrow()
		{
			this.Width = ArrowHeight;
			this.Height = ArrowHeight;
			this.VAnchor = VAnchor.Center;
			this.HAnchor = HAnchor.Right;

			this.Arrow = DownArrow;
		}

		public VertexStorage Arrow { get; set; }

		public int StrokeWidth { get; set; } = 1;

		public Color StrokeColor { get; set; }

		public Vector2 DrawBounds { get; set; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			if (this.Arrow != null)
			{
				graphics2D.Render(this.Arrow, this.DrawBounds.X - ArrowHeight * 2 - 2, this.DrawBounds.Y / 2 + ArrowHeight / 2, ActiveTheme.Instance.SecondaryTextColor);
			}
		}
	}
}