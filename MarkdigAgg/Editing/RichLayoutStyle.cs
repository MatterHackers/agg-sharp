/*
Copyright(c) 2026, Lars Brubaker
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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
using Markdig.Renderers.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The fonts, colours and spacing the rich editor lays text out with. The numbers copy the markdown viewer's
	/// renderers (TextWordX, HeadingRowX, ListX, QuoteBlockX, CodeBlockX) so a document looks the same in the Edit
	/// tab as in Help. Font sizes and spacing are scaled when the style is built, the way a TextWidget scales its
	/// point size: rebuild the style when <see cref="GuiWidget.DeviceScale"/> or the text size setting changes.
	/// </summary>
	public class RichLayoutStyle
	{
		private readonly Dictionary<(TypeFace Face, double Points, bool Italic, bool Underline), StyledTypeFace> faces = new();
		private readonly double fontScale;

		public RichLayoutStyle(ThemeConfig theme = null, double? deviceScale = null)
		{
			Scale = deviceScale ?? GuiWidget.DeviceScale;
			fontScale = Scale * TextStyleSettings.SizeScale;
			TextColor = theme?.TextColor ?? Color.Black;

			// The viewer shows a link as underlined text in the text colour; the editor matches it.
			LinkColor = TextColor;
		}

		/// <summary>
		/// The device scale every spacing value below is already multiplied by.
		/// </summary>
		public double Scale { get; }

		public Color TextColor { get; set; }

		public Color LinkColor { get; set; }

		public double BodyPointSize { get; set; } = 10;

		public double CodePointSize { get; set; } = 10;

		/// <summary>
		/// What an unordered list item shows in its gutter; the viewer draws "-".
		/// </summary>
		public string BulletMarker { get; set; } = "-";

		/// <summary>
		/// How far each list depth moves the item right (the viewer's ListX margin, (depth + 1) * 14).
		/// </summary>
		public double ListIndent => 14 * Scale;

		/// <summary>
		/// The fixed column a list marker sits in, so text lines up across items whatever their marker's width.
		/// </summary>
		public double MarkerGutter => 18 * Scale;

		/// <summary>
		/// The space between a list marker's right edge and the item text.
		/// </summary>
		public double MarkerGap => 4 * Scale;

		public double QuoteBarWidth => 2 * Scale;

		/// <summary>
		/// Where quote text starts, measured from the bar's left edge (the bar plus the viewer's 10 padding).
		/// </summary>
		public double QuoteIndent => 12 * Scale;

		/// <summary>
		/// Space inside a code block's shaded background, on every side (CodeBlockX's padding).
		/// </summary>
		public double CodePadding => 6 * Scale;

		/// <summary>
		/// Space added to every line's ascent + descent, half above and half below (the viewer's 3 row padding).
		/// </summary>
		public double LineGap => 6 * Scale;

		public double CaretWidth => 1 * Scale;

		/// <summary>
		/// The size an image atom takes. The editor widget supplies loaded image sizes; while this is null an image
		/// is a placeholder square one body line tall.
		/// </summary>
		public Func<InlineAtom, Vector2> ImageSize { get; set; }

		/// <summary>
		/// The box a Raw block (shown rendered by a child widget) takes, given the block and the available width.
		/// While this is null a Raw block is one body line across the width.
		/// </summary>
		public Func<RichBlock, double, Vector2> RawBlockSize { get; set; }

		/// <summary>
		/// The heading sizes HeadingRowX uses.
		/// </summary>
		public double HeadingPointSize(int level)
		{
			return level switch
			{
				1 => 20,
				2 => 17,
				3 => 15,
				4 => 13,
				5 => 12,
				_ => 11
			};
		}

		/// <summary>
		/// Space above and below a block, matching the viewer's margins. A block layout's height includes it, so
		/// blocks stack by adding heights.
		/// </summary>
		public (double Before, double After) BlockSpacing(RichBlock block)
		{
			(double before, double after) = block.Kind switch
			{
				RichBlockKind.Heading => (12, 4),
				RichBlockKind.ListItem => (0, 3),
				_ => (0, 12),
			};
			return (before * Scale, after * Scale);
		}

		/// <summary>
		/// The face a block's unstyled text is drawn in: the bold heading face, the code face, or the body face.
		/// Every line is at least as tall as this face, so an empty line still holds a caret.
		/// </summary>
		public StyledTypeFace BlockFace(RichBlock block)
		{
			return block.Kind switch
			{
				RichBlockKind.Heading => Face(AggContext.DefaultFontBold, HeadingPointSize(block.HeadingLevel), false, false),
				RichBlockKind.CodeBlock => Face(CodeBlockX.GetMonoTypeFace(), CodePointSize, false, false),
				_ => Face(AggContext.DefaultFont, BodyPointSize, false, false),
			};
		}

		/// <summary>
		/// The face a run is drawn in within its block. The shipped fonts have no italic face, so italic shears
		/// (<see cref="StyledTypeFace.FauxItalic"/>) as the viewer's TextWidget.Italic does. Inline code uses the code
		/// block's monospace face; links are underlined.
		/// </summary>
		public StyledTypeFace RunFace(RichBlock block, RichRun run)
		{
			bool heading = block.Kind == RichBlockKind.Heading;
			double points = heading ? HeadingPointSize(block.HeadingLevel) : BodyPointSize;
			TypeFace typeFace = run.Code
				? CodeBlockX.GetMonoTypeFace()
				: run.Bold || heading ? AggContext.DefaultFontBold : AggContext.DefaultFont;
			return Face(typeFace, points, run.Italic, run.LinkUrl != null);
		}

		private StyledTypeFace Face(TypeFace typeFace, double points, bool italic, bool underline)
		{
			var key = (typeFace, points, italic, underline);
			if (!faces.TryGetValue(key, out var face))
			{
				// ApplyTextStyleSettings as TextWidget's UiFace does, so widths match the viewer's.
				face = new StyledTypeFace(typeFace, points * fontScale, underline)
				{
					ApplyTextStyleSettings = true,
					FauxItalic = italic,
				};
				faces[key] = face;
			}

			return face;
		}
	}
}
