/*
Copyright (c) 2013, Lars Brubaker
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

using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System.Collections.Generic;
using System;

namespace MatterHackers.Agg
{
	// in the original agg this was conv_transform
	public class WarpOnY : IVertexSourceProxy
	{
		internal class WarpYPoint
		{
			internal double original;
			internal double transformed;
			internal WarpYPoint(double original, double transformed)
			{
				this.original = original;
				this.transformed = transformed;
			}
		}

		private List<WarpYPoint> warpYPoints = new List<WarpYPoint>();

		public IVertexSource VertexSource
		{
			get;
			set;
		}

		public WarpOnY(Transform.ITransform newTransformeToApply)
			: this(null, newTransformeToApply)
		{
		}

		public WarpOnY(IVertexSource vertexSource, Transform.ITransform newTransformeToApply)
		{
			VertexSource = vertexSource;
		}

		public void attach(IVertexSource vertexSource)
		{
			VertexSource = vertexSource;
		}

		public IEnumerable<VertexData> Vertices()
		{
			foreach (VertexData vertexData in VertexSource.Vertices())
			{
				VertexData transformedVertex = vertexData;
				if (ShapePath.is_vertex(transformedVertex.command))
				{
					ApplayYWarp(ref transformedVertex.position.X, ref transformedVertex.position.Y);
				}
				yield return transformedVertex;
			}
		}

		private void ApplayYWarp(ref double x, ref double y)
		{
			// do the actual warp
		}

		public void rewind(int path_id)
		{
			VertexSource.rewind(path_id);
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			ShapePath.FlagsAndCommand cmd = VertexSource.vertex(out x, out y);
			if (ShapePath.is_vertex(cmd))
			{
				ApplayYWarp(ref x, ref y);
			}
			return cmd;
		}
	}

	public class FontHintWidget : GuiWidget
	{
		string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		double yOffsetUpper = -.1;
		double ySizeUpper = .2;

		double yOffsetLower = 0;
		double ySizeLower = -.5;

		public FontHintWidget()
		{
			AnchorAll();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			double textHeight = 20;
			double textY = 200;

			base.OnDraw(graphics2D);

			graphics2D.DrawString("YOffset = {0:0.00}".FormatWith(yOffsetUpper), 20, Height - 20);
			graphics2D.DrawString("YScale = {0:0.00}".FormatWith(ySizeUpper), 140, Height - 20);

			graphics2D.DrawString("YOffset = {0:0.00}".FormatWith(yOffsetLower), 20, Height - 40);
			graphics2D.DrawString("YScale = {0:0.00}".FormatWith(ySizeLower), 140, Height - 40);

			graphics2D.DrawString(alphabet, 20, textY);
			graphics2D.DrawString(alphabet.ToLower(), 310, textY);

			TypeFacePrinter upperPrinter = new TypeFacePrinter(alphabet);
			TypeFacePrinter lowerPrinter = new TypeFacePrinter(alphabet.ToLower());

			graphics2D.Render(new VertexSourceApplyTransform(upperPrinter, Affine.NewScaling(1, (12 + ySizeUpper) / 12)), 20, textY - textHeight + yOffsetUpper, Color.Black);
			graphics2D.Render(new VertexSourceApplyTransform(lowerPrinter, Affine.NewScaling(1, (12 + ySizeLower) / 12)), 310, textY - textHeight + yOffsetLower, Color.Black);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Up)
			{
				yOffsetUpper += .1;
				if (yOffsetUpper > .5) yOffsetUpper = .5;
			}
			else if (keyEvent.KeyCode == Keys.Down)
			{
				yOffsetUpper -= .1;
				if (yOffsetUpper < -.5) yOffsetUpper = -.5;
			}
			if (keyEvent.KeyCode == Keys.Right)
			{
				ySizeUpper += .1;
				if (ySizeUpper > .5) ySizeUpper = .5;
			}
			else if (keyEvent.KeyCode == Keys.Left)
			{
				ySizeUpper -= .1;
				if (ySizeUpper < -.5) ySizeUpper = -.5;
			}


			if (keyEvent.KeyCode == Keys.Home)
			{
				yOffsetLower += .1;
				if (yOffsetLower > .5) yOffsetLower = .5;
			}
			else if (keyEvent.KeyCode == Keys.End)
			{
				yOffsetLower -= .1;
				if (yOffsetLower < -.5) yOffsetLower = -.5;
			}
			if (keyEvent.KeyCode == Keys.PageDown)
			{
				ySizeLower += .1;
				if (ySizeLower > .5) ySizeLower = .5;
			}
			else if (keyEvent.KeyCode == Keys.Delete)
			{
				ySizeLower -= .1;
				if (ySizeLower < -.5) ySizeLower = -.5;
			}
			Invalidate();

			base.OnKeyDown(keyEvent);
		}
	}
}