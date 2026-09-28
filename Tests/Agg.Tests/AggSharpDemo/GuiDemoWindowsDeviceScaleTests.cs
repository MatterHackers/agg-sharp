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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>
	/// At <see cref="GuiWidget.DeviceScale"/> 2 (Retina, or a browser at devicePixelRatio 2) a GUI demo window's
	/// content must be its 1x layout doubled: text doubles on its own, so a width, row height or canvas size left
	/// in 1x pixels clips or overlaps it.
	/// </summary>
	/// <remarks>
	/// DeviceScale is process wide, so this is keyless <c>[NotInParallel]</c> and restores the previous value.
	/// Each window is built twice, at 1x in its design-size page and at 2x in a page twice that size, and the
	/// two widget trees are walked side by side.
	/// </remarks>
	public class GuiDemoWindowsDeviceScaleTests
	{
		private static readonly string[] SampledWindows =
		{
			"Widget Gallery", "Table", "Frame", "Code Editor", "Scrolling", "Sliders", "Lion",
		};

		[Test]
		[NotInParallel]
		public async Task SampledWindowsDoubleTheirLayoutAtDeviceScaleTwo()
		{
			var problems = new List<string>();
			foreach (string title in SampledWindows)
			{
				problems.AddRange(Compare(title));
			}

			await Assert.That(problems).IsEmpty().Because(string.Join(Environment.NewLine, problems));
		}

		/// <summary>Every place <paramref name="title"/>'s 2x layout is not its 1x layout doubled, and every
		/// visible text at 2x that an ancestor cuts off.</summary>
		private static List<string> Compare(string title)
		{
			var problems = new List<string>();
			double previous = GuiWidget.DeviceScale;
			try
			{
				List<(string Path, GuiWidget Widget)> atOne = Build(title, 1);
				List<(string Path, GuiWidget Widget)> atTwo = Build(title, 2);
				if (atOne.Count != atTwo.Count)
				{
					problems.Add($"{title}: {atOne.Count} widgets at 1x but {atTwo.Count} at 2x");
				}

				for (int i = 0; i < Math.Min(atOne.Count, atTwo.Count); i++)
				{
					GuiWidget one = atOne[i].Widget;
					GuiWidget two = atTwo[i].Widget;
					if (atOne[i].Path != atTwo[i].Path)
					{
						problems.Add($"{title}: tree differs at {atOne[i].Path} / {atTwo[i].Path}");
						break;
					}

					if (!one.Visible || !two.Visible)
					{
						continue;
					}

					if (!Doubled(one.Width, two.Width) || !Doubled(one.Height, two.Height))
					{
						problems.Add($"{title}: {atOne[i].Path} is {one.Width:0.#}x{one.Height:0.#} at 1x but {two.Width:0.#}x{two.Height:0.#} at 2x");
					}
				}

				foreach ((string path, GuiWidget widget) in atTwo)
				{
					if (widget is TextWidget text && IsClipped(text))
					{
						problems.Add($"{title}: text '{text.Text}' at {path} is clipped at 2x");
					}
				}
			}
			finally
			{
				GuiWidget.DeviceScale = previous;
			}

			return problems;
		}

		// Stretch rounding, pixel snapping and hinted text widths wobble by a few pixels; a size left in 1x units
		// is off by half. Hairlines and carets (an empty edit field's inner widget is one caret wide) stay a few
		// device pixels at any scale, so sizes under MinimumCompared are not compared.
		private const double MinimumCompared = 6;

		private static bool Doubled(double atOne, double atTwo) => atOne < MinimumCompared
			|| Math.Abs(atTwo - atOne * 2) <= Math.Max(3, atOne * 2 * .06);

		/// <summary>True when a visible text widget does not fit inside its ancestors, up to the first scroller
		/// (whose content is meant to run past its view).</summary>
		private static bool IsClipped(TextWidget text)
		{
			if (!text.ActuallyVisibleOnScreen() || string.IsNullOrEmpty(text.Text) || text.Width <= 0 || text.Height <= 0)
			{
				return false;
			}

			RectangleDouble bounds = text.TransformToScreenSpace(text.LocalBounds);
			for (GuiWidget parent = text.Parent; parent != null; parent = parent.Parent)
			{
				if (parent is ScrollableWidget)
				{
					return false;
				}

				RectangleDouble parentBounds = parent.TransformToScreenSpace(parent.LocalBounds);
				if (bounds.Left < parentBounds.Left - 1 || bounds.Right > parentBounds.Right + 1
					|| bounds.Bottom < parentBounds.Bottom - 1 || bounds.Top > parentBounds.Top + 1)
				{
					return true;
				}
			}

			return false;
		}

		private static List<(string Path, GuiWidget Widget)> Build(string title, double scale)
		{
			GuiWidget.DeviceScale = scale;
			DemoSpec spec = GuiDemoSpecs.All.First(s => s.Title == title);
			var page = new GuiWidget(spec.DefaultWidth * scale, spec.DefaultHeight * scale);
			GuiWidget content = GuiDemoSpecs.CreateContent(spec, new DemoTheme());
			page.AddChild(content);
			page.PerformLayout();

			var widgets = new List<(string, GuiWidget)>();
			Walk(content, content.Name, widgets);
			return widgets;
		}

		private static void Walk(GuiWidget widget, string path, List<(string, GuiWidget)> widgets)
		{
			widgets.Add((path, widget));
			for (int i = 0; i < widget.Children.Count; i++)
			{
				GuiWidget child = widget.Children[i];
				string name = string.IsNullOrEmpty(child.Name) ? child.GetType().Name : child.Name;
				Walk(child, $"{path}/{i}:{name}", widgets);
			}
		}
	}
}
