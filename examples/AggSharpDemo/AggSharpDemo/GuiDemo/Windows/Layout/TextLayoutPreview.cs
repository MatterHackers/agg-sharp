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
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout
{
	/// <summary>The Text Layout window's settings, as agg-gui's TextLayoutDemoState, with egui's defaults.</summary>
	public class TextLayoutSettings
	{
		public const string LoremIpsum = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

		/// <summary>Excerpt from Dolores Ibarruri's farewell speech to the International Brigades.</summary>
		public const string LaPasionaria = "Mothers! Women!\n\nWhen the years pass by and the wounds of war are stanched; when the memory of the sad and bloody days dissipates in a present of liberty, of peace and of wellbeing; when the rancor have died out and pride in a free country is felt equally by all Spaniards, speak to your children. Tell them of these men of the International Brigades.\n\nRecount for them how, coming over seas and mountains, crossing frontiers bristling with bayonets, sought by raving dogs thirsting to tear their flesh, these men reached our country as crusaders for freedom, to fight and die for Spain's liberty and independence threatened by German and Italian fascism. They gave up everything - their loves, their countries, home and fortune, fathers, mothers, wives, brothers, sisters and children - and they came and said to us: \"We are here. Your cause, Spain's cause, is ours. It is the cause of all advanced and progressive mankind.\"\n\n- Dolores Ibarruri, 1938";

		/// <summary>The overflow choices' markers, by <see cref="Overflow"/> index; null is "None".</summary>
		public static readonly string[] OverflowMarkers = { null, "…", "—", "-" };

		public int MaxRows { get; set; } = 1000;

		/// <summary>False breaks at word boundaries, true anywhere.</summary>
		public bool BreakAnywhere { get; set; }

		/// <summary>Index into <see cref="OverflowMarkers"/>; egui starts on "…".</summary>
		public int Overflow { get; set; } = 1;

		/// <summary>Added after every character, in design units.</summary>
		public double ExtraLetterSpacing { get; set; }

		public bool CustomLineHeight { get; set; }

		/// <summary>The row height in design units when <see cref="CustomLineHeight"/> is on.</summary>
		public double LineHeight { get; set; } = 20;

		public Justification Align { get; set; } = Justification.Left;

		/// <summary>Stretch every row but a paragraph's last to the full width.</summary>
		public bool Justify { get; set; }

		/// <summary>False shows <see cref="LoremIpsum"/>, true <see cref="LaPasionaria"/>.</summary>
		public bool PasionariaText { get; set; }

		public string Text => this.PasionariaText ? LaPasionaria : LoremIpsum;
	}

	/// <summary>One laid out row; <see cref="ParagraphEnd"/> rows are never justified.</summary>
	public readonly record struct TextLayoutLine(string Text, bool ParagraphEnd);

	/// <summary>
	/// agg-gui's TextLayoutPreview (text_demos/text_layout.rs): the settings' text wrapped to this widget's width at
	/// word boundaries or anywhere, cut to the row limit with an overflow marker, and drawn left, centred, right or
	/// justified with extra letter spacing and a chosen row height. Its height follows the rows.
	/// </summary>
	public class TextLayoutPreview : GuiWidget
	{
		/// <summary>agg-gui's preview text size.</summary>
		public const double FontSizeUnits = 13;

		private const double PadUnits = 12;

		private readonly DemoTheme demoTheme;
		private readonly StyledTypeFace typeFace;
		private double laidOutWidth = -1;

		public TextLayoutPreview(TextLayoutSettings settings, DemoTheme demoTheme)
		{
			this.Settings = settings;
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Absolute;
			this.typeFace = new StyledTypeFace(LiberationSansFont.Instance, demoTheme.Theme.DefaultFontSize * FontSizeUnits / 12 * DeviceScale);
			this.Relayout();
		}

		public TextLayoutSettings Settings { get; }

		/// <summary>The rows as last laid out.</summary>
		public IReadOnlyList<TextLayoutLine> Lines { get; private set; } = Array.Empty<TextLayoutLine>();

		/// <summary>Row height in device pixels.</summary>
		public double LineHeight { get; private set; }

		/// <summary>The width rows wrap to, in device pixels: the widget less its padding.</summary>
		public double ContentWidth => Math.Max(1, this.Width - 2 * PadUnits * DeviceScale);

		/// <summary>Width of <paramref name="text"/> drawn with the extra letter spacing, in device pixels.</summary>
		public double LineWidth(string text)
		{
			int gaps = Math.Max(0, text.Length - 1);
			return new TypeFacePrinter(text, this.typeFace).GetSize().X + gaps * this.Settings.ExtraLetterSpacing * DeviceScale;
		}

		/// <summary>Lays the rows out again for the current settings and width; call after changing a setting.</summary>
		public void Relayout()
		{
			double maxWidth = this.ContentWidth;
			var lines = new List<TextLayoutLine>();
			foreach (string paragraph in this.Settings.Text.Split('\n'))
			{
				if (this.Settings.BreakAnywhere)
				{
					this.WrapAnywhere(paragraph, maxWidth, lines);
				}
				else
				{
					this.WrapWords(paragraph, maxWidth, lines);
				}
			}

			if (lines.Count > this.Settings.MaxRows)
			{
				lines.RemoveRange(this.Settings.MaxRows, lines.Count - this.Settings.MaxRows);
				if (lines.Count > 0)
				{
					lines[lines.Count - 1] = new TextLayoutLine(this.WithOverflow(lines[lines.Count - 1].Text, maxWidth), true);
				}
			}

			this.Lines = lines;
			this.laidOutWidth = this.Width;
			this.LineHeight = this.Settings.CustomLineHeight
				? Math.Max(8, this.Settings.LineHeight) * DeviceScale
				: this.typeFace.EmSizeInPoints * 1.35;
			this.Height = Math.Max(1, lines.Count) * this.LineHeight + 2 * PadUnits * DeviceScale;
			this.Invalidate();
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);
			if (this.Width != this.laidOutWidth)
			{
				this.Relayout();
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			DemoPalette palette = this.demoTheme.Palette;
			RectangleDouble r = this.LocalBounds;
			graphics2D.FillRectangle(r, new Color(0, 0, 0, 15));
			graphics2D.Render(new Stroke(new RoundedRect(r.Left + .5, r.Bottom + .5, r.Right - .5, r.Top - .5, 0), 1), palette.WidgetStroke);

			double pad = PadUnits * DeviceScale;
			double contentWidth = this.ContentWidth;
			double extra = this.Settings.ExtraLetterSpacing * DeviceScale;
			double fontSize = this.typeFace.EmSizeInPoints;
			double y = r.Top - pad - this.LineHeight * .5 - (this.LineHeight - fontSize) * .35;
			Color textColor = palette.TextColor;

			for (int i = 0; i < this.Lines.Count; i++)
			{
				TextLayoutLine line = this.Lines[i];
				if (line.Text.Length > 0)
				{
					double lineWidth = this.LineWidth(line.Text);
					bool justify = this.Settings.Justify && !line.ParagraphEnd && i + 1 < this.Lines.Count;
					int spaces = line.Text.Count(char.IsWhiteSpace);
					double justifySpacing = justify && spaces > 0 ? Math.Max(0, (contentWidth - lineWidth) / spaces) : 0;
					double drawWidth = justify ? contentWidth : lineWidth;
					double x = this.Settings.Align switch
					{
						Justification.Center => pad + (contentWidth - drawWidth) * .5,
						Justification.Right => pad + contentWidth - drawWidth,
						_ => pad,
					};

					// agg-gui's y is the text's vertical centre; the printer's is its baseline.
					double baseline = y - fontSize * .35;
					if (Math.Abs(extra) > .01 || justifySpacing > 0)
					{
						foreach (char c in line.Text)
						{
							var glyph = new TypeFacePrinter(c.ToString(), this.typeFace, new Vector2(x, baseline));
							graphics2D.Render(glyph, textColor);
							x += glyph.GetSize().X + extra + (char.IsWhiteSpace(c) ? justifySpacing : 0);
						}
					}
					else
					{
						graphics2D.Render(new TypeFacePrinter(line.Text, this.typeFace, new Vector2(x, baseline)), textColor);
					}
				}

				y -= this.LineHeight;
			}

			base.OnDraw(graphics2D);
		}

		private void WrapWords(string paragraph, double maxWidth, List<TextLayoutLine> lines)
		{
			string current = "";
			foreach (string word in paragraph.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
			{
				string candidate = current.Length == 0 ? word : current + " " + word;
				if (current.Length == 0 || this.LineWidth(candidate) <= maxWidth)
				{
					current = candidate;
				}
				else
				{
					lines.Add(new TextLayoutLine(current, false));
					current = word;
				}
			}

			lines.Add(new TextLayoutLine(current, true));
		}

		private void WrapAnywhere(string paragraph, double maxWidth, List<TextLayoutLine> lines)
		{
			string current = "";
			foreach (char c in paragraph)
			{
				string candidate = current + c;
				if (current.Length > 0 && this.LineWidth(candidate) > maxWidth)
				{
					lines.Add(new TextLayoutLine(current, false));
					current = c.ToString();
				}
				else
				{
					current = candidate;
				}
			}

			lines.Add(new TextLayoutLine(current, true));
		}

		/// <summary><paramref name="line"/> shortened until the overflow marker fits after it, then marked.</summary>
		private string WithOverflow(string line, double maxWidth)
		{
			string marker = TextLayoutSettings.OverflowMarkers[this.Settings.Overflow];
			if (marker == null)
			{
				return line;
			}

			while (line.Length > 0 && this.LineWidth(line + marker) > maxWidth)
			{
				line = line.Substring(0, line.Length - 1);
			}

			return line + marker;
		}
	}
}
