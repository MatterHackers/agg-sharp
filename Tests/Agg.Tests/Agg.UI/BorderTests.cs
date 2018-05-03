/*
Copyright (c) 2018, John Lewin
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

using System.Diagnostics;
using MatterHackers.Agg.Image;
using NUnit.Framework;

namespace MatterHackers.Agg.UI.Tests
{
	[TestFixture, Category("Agg.UI")]
	public class BorderTests
	{
		private int borderSize = 1;
		private static bool debugResult = false;

		private enum Regions
		{
			Left = 0,
			Bottom = 1,
			Right = 2,
			Top = 3,
			All = 4
		}

		[Test]
		public void BorderTestLeft()
		{
			var border = new BorderDouble(left: borderSize);
			var surface = DrawBorderOnSurface(border, "left");

			AssertBorderWhereExpected(Regions.Left, border, surface);
		}

		[Test]
		public void BorderTestBottom()
		{
			var border = new BorderDouble(bottom: borderSize);
			var surface = DrawBorderOnSurface(border, "bottom");

			AssertBorderWhereExpected(Regions.Bottom, border, surface);
		}

		[Test]
		public void BorderTestRight()
		{
			var border = new BorderDouble(right: borderSize);
			var surface = DrawBorderOnSurface(border, "right");

			AssertBorderWhereExpected(Regions.Right, border, surface);
		}

		[Test]
		public void BorderTestTop()
		{
			var border = new BorderDouble(top: borderSize);
			var surface = DrawBorderOnSurface(border, "top");

			AssertBorderWhereExpected(Regions.Top, border, surface);
		}

		// Enable to visually debug
		//[Test, Apartment(System.Threading.ApartmentState.STA)]
		public void BorderTestsVisualizer()
		{
			var systemWindow = new SystemWindow(700, 660)
			{
				BackgroundColor = Color.LightGray,
				Padding = 25
			};

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			systemWindow.AddChild(column);

			int marginSize = 8;

			GuiWidget heading;

			for (var m = 0; m < 2; m++)
			{
				column.AddChild(heading = new GuiWidget()
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit,
					Margin = 15,
					Border = new BorderDouble(bottom: 2),
					BorderColor = Color.Gray,
				});

				heading.AddChild(new TextWidget($"border: {borderSize}, margin: {marginSize}, container: white, widget: blue, border: red", pointSize: 11));

				for (var i = 0; i < 5; i++)
				{
					BorderDouble margin = 0;
					switch (i)
					{
						case 0:
							margin = new BorderDouble(left: marginSize);
							break;
						case 1:
							margin = new BorderDouble(right: marginSize);
							break;
						case 2:
							margin = new BorderDouble(top: marginSize);
							break;
						case 3:
							margin = new BorderDouble(bottom: marginSize);
							break;
						case 4:
							margin = marginSize;
							break;
					}

					var row = new FlowLayoutWidget()
					{
						HAnchor = HAnchor.Fit | HAnchor.Center,
					};
					column.AddChild(row);

					row.AddChild(
						GetBorderedWidget(new BorderDouble(bottom: borderSize), "bottom", margin));
					row.AddChild(new GuiWidget() { Margin = 10 });

					row.AddChild(
						GetBorderedWidget(new BorderDouble(top: borderSize), "top", margin));
					row.AddChild(new GuiWidget() { Margin = 10 });

					row.AddChild(
						GetBorderedWidget(new BorderDouble(right: borderSize), "right", margin));
					row.AddChild(new GuiWidget() { Margin = 10 });

					row.AddChild(
						GetBorderedWidget(new BorderDouble(left: borderSize), "left", margin));
					row.AddChild(new GuiWidget() { Margin = 10 });

					row.AddChild(
						GetBorderedWidget(new BorderDouble(borderSize), "*", margin));
				}

				borderSize = 5;
			}

			systemWindow.Load += (s, e) =>
			{
				foreach(var child in systemWindow.Children)
				{
					child.Invalidate();
				}
			};
			systemWindow.ShowAsSystemWindow();
		}

		private static IImageByte DrawBorderOnSurface(BorderDouble border, string name)
		{
			var widget = GetBorderedWidget(border, name, doubleBuffer: true);

			var graphics2D = widget.NewGraphics2D();
			graphics2D.Clear(Color.White);

			widget.OnDraw(graphics2D);

			if (debugResult)
			{
				string filePath = $@"c:\temp\{name}.tga";

				ImageTgaIO.Save(widget.BackBuffer, filePath);

				Process.Start(filePath);
			}

			return graphics2D.DestImage;
		}

		private static void AssertBorderWhereExpected(Regions region, BorderDouble border, IImageByte imageBuffer)
		{
			RectangleDouble borderBounds = RectangleDouble.ZeroIntersection;

			switch (region)
			{
				case  Regions.Left:
					borderBounds = new RectangleDouble(0, 0, border.Left, imageBuffer.Height);
					break;

				case Regions.Bottom:
					borderBounds = new RectangleDouble(0, 0, imageBuffer.Width, border.Bottom);
					break;

				case Regions.Right:
					borderBounds = new RectangleDouble(imageBuffer.Width - border.Right, 0, imageBuffer.Width, imageBuffer.Height);
					break;

				case Regions.Top:
					borderBounds = new RectangleDouble(0, imageBuffer.Height - border.Top, imageBuffer.Width, imageBuffer.Height);
					break;
			}

			for (int x = 0; x < imageBuffer.Width; x++)
			{
				for (int y = 0; y < imageBuffer.Height; y++)
				{
					var pixel = imageBuffer.GetPixel(x, y);

					bool shouldBeRed = borderBounds.Contains(new Point2D(x + .5, y + .5));
					if (shouldBeRed)
					{
						Assert.AreEqual(Color.Red, pixel);

					}
					else
					{
						Assert.AreNotEqual(Color.Red, pixel);
					}
				}
			}
		}

		private static GuiWidget GetBorderedWidget(BorderDouble border, string name, BorderDouble margin = default(BorderDouble), bool doubleBuffer = false)
		{
			var container = new GuiWidget()
			{
				DoubleBuffer = doubleBuffer,
				Width = 80,
				Height = 40,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				BackgroundColor = Color.White
			};

			var widget = new GuiWidget()
			{
				Border = border,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				BackgroundColor = new Color(Color.Blue, 45),
				Margin = margin,
				BorderColor = Color.Red,
			};
			container.AddChild(widget);

			container.AddChild(new TextWidget(name, justification: Font.Justification.Center)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true
			});
			return container;
		}
	}
}
