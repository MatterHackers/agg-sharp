/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// One of a <see cref="CodeEditor"/>'s two floating scroll bars - agg-gui's TextArea overlay bar, which sits over
	/// the text rather than taking width from it. Shown only while there is something to scroll to; drag the thumb
	/// to scroll, or press the track to bring the thumb's middle there and drag on from it.
	/// </summary>
	internal class CodeEditorScrollbar
	{
		/// <summary>The bar's thickness, in design units.</summary>
		public const double Thickness = 6;

		/// <summary>The gap between the bar and the editor's edge, in design units.</summary>
		public const double Margin = 2;

		/// <summary>The shortest the thumb gets, in design units, so it stays grabbable over a long file.</summary>
		public const double MinThumbLength = 20;

		private readonly CodeEditor editor;
		private readonly bool vertical;

		// Where along the thumb the pointer took hold of it, from the thumb's low end.
		private double grabOffset;

		public CodeEditorScrollbar(CodeEditor editor, bool vertical)
		{
			this.editor = editor;
			this.vertical = vertical;
		}

		public bool Dragging { get; private set; }

		public bool Visible => MaxScroll > 0;

		/// <summary>The strip the thumb runs along, in the editor's local pixels.</summary>
		public RectangleDouble Track
		{
			get
			{
				double scale = GuiWidget.DeviceScale;
				double margin = Margin * scale, thickness = Thickness * scale, padding = CodeEditor.TextPadding * scale;
				if (vertical)
				{
					double bottom = editor.HorizontalScrollbarVisible ? 2 * margin + thickness : padding;
					return new RectangleDouble(editor.Width - margin - thickness, bottom, editor.Width - margin, editor.Height - padding);
				}

				double right = editor.Width - (editor.VerticalScrollbarVisible ? 2 * margin + thickness : padding);
				return new RectangleDouble(editor.GutterWidth + padding, margin, right, margin + thickness);
			}
		}

		/// <summary>The draggable part, sized to the share of the content in view and placed at the scroll.</summary>
		public RectangleDouble Thumb
		{
			get
			{
				RectangleDouble track = Track;
				double length = ThumbLength(track);
				double start = ThumbStart(track, length);
				return vertical
					? new RectangleDouble(track.Left, start, track.Right, start + length)
					: new RectangleDouble(start, track.Bottom, start + length, track.Top);
			}
		}

		private double Scroll
		{
			get => vertical ? editor.ScrollY : editor.ScrollX;
			set
			{
				if (vertical)
				{
					editor.ScrollY = value;
				}
				else
				{
					editor.ScrollX = value;
				}
			}
		}

		private double MaxScroll => vertical ? editor.MaxScrollY : editor.MaxScrollX;

		private double View => vertical ? editor.Height : editor.TextViewWidth;

		/// <summary>Takes hold of the bar when <paramref name="position"/> is on its track (a few pixels of slack
		/// around it); false, leaving the press to the text, otherwise.</summary>
		public bool BeginDrag(Vector2 position)
		{
			if (!Visible)
			{
				return false;
			}

			RectangleDouble track = Track;
			double slack = Margin * GuiWidget.DeviceScale;
			track.Inflate(slack);
			if (!track.Contains(position))
			{
				return false;
			}

			RectangleDouble thumb = Thumb;
			double along = Along(position);
			double thumbLow = vertical ? thumb.Bottom : thumb.Left;
			double thumbHigh = vertical ? thumb.Top : thumb.Right;
			grabOffset = along >= thumbLow && along <= thumbHigh ? along - thumbLow : (thumbHigh - thumbLow) / 2;
			Dragging = true;
			DragTo(position);
			return true;
		}

		/// <summary>Moves the thumb with the pointer while dragging; false when not dragging.</summary>
		public bool DragTo(Vector2 position)
		{
			if (!Dragging)
			{
				return false;
			}

			RectangleDouble track = Track;
			double length = ThumbLength(track);
			double travel = TrackLength(track) - length;
			if (travel > 0)
			{
				double start = Along(position) - grabOffset;
				double fraction = vertical ? (track.Top - length - start) / travel : (start - track.Left) / travel;
				Scroll = Math.Clamp(fraction, 0, 1) * MaxScroll;
			}

			return true;
		}

		public void EndDrag() => Dragging = false;

		public void Draw(Graphics2D graphics2D)
		{
			if (Visible)
			{
				RectangleDouble thumb = Thumb;
				graphics2D.FillRectangle(thumb, Dragging ? editor.ScrollbarDragColor : editor.ScrollbarColor);
			}
		}

		private double Along(Vector2 position) => vertical ? position.Y : position.X;

		private double TrackLength(RectangleDouble track) => Math.Max(0, vertical ? track.Height : track.Width);

		private double ThumbLength(RectangleDouble track)
		{
			double trackLength = TrackLength(track);
			double share = View / Math.Max(1, View + MaxScroll);
			return Math.Min(trackLength, Math.Max(MinThumbLength * GuiWidget.DeviceScale, trackLength * share));
		}

		/// <summary>The thumb's low end: at the track's top (vertical) or left (horizontal) at no scroll.</summary>
		private double ThumbStart(RectangleDouble track, double length)
		{
			double fraction = MaxScroll > 0 ? Scroll / MaxScroll : 0;
			double travel = TrackLength(track) - length;
			return vertical ? track.Top - length - fraction * travel : track.Left + fraction * travel;
		}
	}
}
