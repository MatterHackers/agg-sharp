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
using System.Text;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A drop down list of fonts that shows each font's name in that font's own face, in every row of the open list and
	/// in the closed field, as agg-gui's font picker (demo-ui/src/font_picker.rs) does. Selection, keyboard, type-to-find
	/// and scrolling are <see cref="DropDownList"/>'s; listen to <see cref="DropDownList.SelectionChanged"/> and read
	/// <see cref="DropDownList.SelectedValue"/> or <see cref="SelectedTypeFace"/>.
	/// </summary>
	/// <remarks>
	/// Names are drawn by ordinary <see cref="TextWidget"/>s, so LCD, hinting and the other process-wide text settings
	/// apply to them as to any label. A face that has no glyphs for its own name (a symbol or icon font) shows its name
	/// in the UI font instead (<see cref="CanDrawName"/>).
	///
	/// Like a <see cref="TextWidget"/>, the selector takes its UI font (<see cref="AggContext.DefaultFont"/>) and size
	/// when it is built: the closed field's fixed height is measured in that font at construction, and a row that
	/// falls back to the UI font gets the one current when it was added (or, for <see cref="AddFontLazy"/>, first drawn).
	/// </remarks>
	public class FontSelector : DropDownList
	{
		/// <summary>The face behind each font row, loaded on first need.</summary>
		private readonly Dictionary<MenuItem, Lazy<TypeFace>> faces = new Dictionary<MenuItem, Lazy<TypeFace>>();

		public FontSelector(string noSelectionString, Color textColor, Direction direction = Direction.Down, double maxHeight = 0, double pointSize = 12)
			: base(noSelectionString, textColor, direction, maxHeight, pointSize: pointSize)
		{
			this.closedLabelStyle = this.mainControlText.Printer.TypeFaceStyle;
			this.MeasureClosedLabelHeight();
		}

		/// <summary>The closed label's UI face and size as built, before any face is shown or a tall face fitted.</summary>
		private readonly StyledTypeFace closedLabelStyle;

		/// <summary>The device pixel height of the closed label plus its margins in the UI font, kept for every face.</summary>
		private double closedLabelHeight;

		/// <summary>The face the closed label shows (null for the UI font), re-fitted when the padding changes.</summary>
		private TypeFace closedLabelFace;

		/// <summary>
		/// The row padding as last set. <see cref="DropDownList"/> keeps it in the closed label's margin, which this
		/// selector moves to fit each face, so it is held here instead.
		/// </summary>
		private BorderDouble rowPadding;

		/// <summary>
		/// The padding of each row added from now on, and the closed label's base margin (its right side at least 30 to
		/// clear the arrow). Fitting a face to the closed field never changes it; setting it re-measures the field.
		/// </summary>
		public override BorderDouble MenuItemsPadding
		{
			get => this.rowPadding;
			set
			{
				base.MenuItemsPadding = value;

				// The base getter reads the label's margin, which the base setter just normalised.
				this.rowPadding = base.MenuItemsPadding;

				// DropDownList's constructor sets this before this class's constructor has run.
				if (this.closedLabelStyle != null)
				{
					this.MeasureClosedLabelHeight();
					this.ShowClosedLabelInFace(this.closedLabelFace);
				}
			}
		}

		/// <summary>
		/// The face of the selected font - the one it was added with, not the UI font its label may fall back to - or
		/// null when nothing is selected or a loader gave none. Reading it loads a lazily added face.
		/// </summary>
		public TypeFace SelectedTypeFace => this.FaceOf(this.SelectedIndex);

		/// <summary>
		/// Adds a font whose face is already loaded. Its row and, when selected, the closed field show
		/// <paramref name="name"/> drawn in <paramref name="typeFace"/>.
		/// </summary>
		/// <param name="name">The name shown in the list.</param>
		/// <param name="typeFace">The face to draw the name in; null draws it in the UI font.</param>
		/// <param name="value">The <see cref="DropDownList.SelectedValue"/> for this font; defaults to <paramref name="name"/>.</param>
		/// <returns>The new row.</returns>
		public MenuItem AddFont(string name, TypeFace typeFace, string value = null)
		{
			MenuItem item = this.AddRow(name, value);
			this.faces[item] = new Lazy<TypeFace>(typeFace);
			ShowInFace(RowText(item), DisplayFace(typeFace, name));
			return item;
		}

		/// <summary>
		/// Adds a font whose face is read only when it is needed: when its row is first drawn (a row scrolled out of
		/// the open list is never drawn) or when it is selected. Until then the row shows its name in the UI font.
		/// Use this when parsing every face up front would cost too much.
		/// </summary>
		/// <remarks>
		/// The row is sized for the UI font until its face loads, so a row for a tall face grows on its first draw. A
		/// loader that throws is treated as one that returned null.
		/// </remarks>
		/// <param name="name">The name shown in the list.</param>
		/// <param name="loadTypeFace">Reads the face; called at most once. Returning null keeps the UI font.</param>
		/// <param name="value">The <see cref="DropDownList.SelectedValue"/> for this font; defaults to <paramref name="name"/>.</param>
		/// <returns>The new row.</returns>
		public MenuItem AddFontLazy(string name, Func<TypeFace> loadTypeFace, string value = null)
		{
			MenuItem item = this.AddRow(name, value);
			this.faces[item] = new Lazy<TypeFace>(() =>
			{
				// A font file that cannot be read must not take the list down with it - this runs inside a draw or a
				// selection - so its name is shown in the UI font, as for a face the loader could not find.
				try
				{
					return loadTypeFace();
				}
				catch (Exception)
				{
					return null;
				}
			});
			ShowInFace(RowText(item), null);

			// BeforeDraw rather than a load now: a row clipped out of the open list is not drawn, so this is the hook
			// that means the user can see it. Rows live as long as the list and are re-parented on every open, so the
			// handler removes itself and the face is swapped in once.
			void ShowInItsOwnFace(object sender, DrawEventArgs e)
			{
				item.BeforeDraw -= ShowInItsOwnFace;
				ShowInFace(RowText(item), DisplayFace(this.faces[item].Value, name));
			}

			item.BeforeDraw += ShowInItsOwnFace;
			return item;
		}

		/// <summary>
		/// Whether <paramref name="typeFace"/> itself (ignoring its <see cref="TypeFace.Fallback"/>) has a glyph for
		/// every visible character of <paramref name="name"/>. A symbol or icon font usually has none for letters, and
		/// its name would draw as empty boxes or borrowed glyphs, so the selector shows such a name in the UI font.
		/// </summary>
		public static bool CanDrawName(TypeFace typeFace, string name)
		{
			if (typeFace == null || name == null)
			{
				return false;
			}

			foreach (Rune rune in name.EnumerateRunes())
			{
				if (!Rune.IsWhiteSpace(rune) && !typeFace.HasGlyph(rune.Value))
				{
					return false;
				}
			}

			return true;
		}

		public override void OnSelectionChanged(EventArgs e)
		{
			// The closed field shows the choice in its own face, as the rows do.
			this.ShowClosedLabelInFace(this.SelectedIndex < 0 ? null : DisplayFace(this.SelectedTypeFace, this.SelectedLabel));
			base.OnSelectionChanged(e);
		}

		/// <summary>
		/// Shows the closed label in <paramref name="typeFace"/> (null for the UI font) without changing the field's
		/// height, so picking a font never moves the layout around the field - agg-gui's combo is a fixed CLOSED_H
		/// whatever face it shows.
		/// </summary>
		/// <remarks>
		/// The label is padded to the face's whole ascent and descent (<see cref="ShowInFace"/>) because a widget's
		/// children are clipped to their bounds; its vertical margins then give back what the padding took, keeping
		/// label plus margins at the UI font's height. A face too tall to fit even with no margin is drawn smaller
		/// until it does, rather than clipped.
		/// </remarks>
		private void ShowClosedLabelInFace(TypeFace typeFace)
		{
			this.closedLabelFace = typeFace;
			TextWidget label = this.mainControlText;
			double points = this.closedLabelStyle.EmSizeInPoints;
			ShowInFace(label, typeFace, points);
			double height = label.LocalBounds.Height;
			if (height > this.closedLabelHeight)
			{
				// Everything in the label's height scales with its size; the small factor absorbs rounding.
				ShowInFace(label, typeFace, points * this.closedLabelHeight / height * .999);
				height = label.LocalBounds.Height;
			}

			double margin = Math.Max(0, (this.closedLabelHeight - height) / 2) / GuiWidget.DeviceScale;
			label.Margin = new BorderDouble(this.rowPadding.Left, margin, this.rowPadding.Right, margin);
		}

		/// <summary>
		/// The closed field's fixed height: the UI font's line box plus the base margin. Measured on a printer of its
		/// own, as the label's text may be empty and has no line box then.
		/// </summary>
		private void MeasureClosedLabelHeight()
		{
			this.closedLabelHeight = new TypeFacePrinter("X", this.closedLabelStyle).LocalBounds.Height
				+ this.rowPadding.Height * GuiWidget.DeviceScale;
		}

		/// <summary>
		/// <see cref="DropDownList.AddItem(string, string, TypeFace)"/>, which resets the closed label's margin to the row
		/// padding and measures the field's minimum size from it. The label is first put back to an unpadded line box,
		/// so that measure sees the fixed height and not a fitted face's padding, then fitted again.
		/// </summary>
		private MenuItem AddRow(string name, string value)
		{
			TextWidget label = this.mainControlText;
			label.Padding = new BorderDouble(label.Padding.Left, 0, label.Padding.Right, 0);
			label.Margin = this.rowPadding;
			MenuItem item = this.AddItem(name, value);
			this.ShowClosedLabelInFace(this.closedLabelFace);
			return item;
		}

		private TypeFace FaceOf(int index)
		{
			if (index < 0 || index >= this.MenuItems.Count || !this.faces.TryGetValue(this.MenuItems[index], out Lazy<TypeFace> face))
			{
				return null;
			}

			return face.Value;
		}

		/// <summary>The face to draw <paramref name="name"/> in: its own when it can, otherwise null for the UI font.</summary>
		private static TypeFace DisplayFace(TypeFace typeFace, string name) => CanDrawName(typeFace, name) ? typeFace : null;

		private static TextWidget RowText(MenuItem item) => item.Descendants<TextWidget>().First();

		/// <summary>
		/// Redraws <paramref name="text"/> in <paramref name="typeFace"/> (null for the UI font) and pads it to the face's
		/// full ascent and descent. <paramref name="points"/> (StyledTypeFace points) resizes it; 0 keeps its size.
		/// </summary>
		/// <remarks>
		/// TypeFacePrinter's line box is one em centred between the ascent and the descent, so a face whose ascent plus
		/// descent spans more than an em (Nunito's is 1.36 em; script faces such as Pacifico more) reaches past the box
		/// by half the excess at the top and at the bottom. Padding by that much keeps a row from clipping the face's
		/// ascenders and descenders; the closed label keeps its height another way (<see cref="ShowClosedLabelInFace"/>).
		/// </remarks>
		private static void ShowInFace(TextWidget text, TypeFace typeFace, double points = 0)
		{
			StyledTypeFace style = text.Printer.TypeFaceStyle;
			points = points > 0 ? points : style.EmSizeInPoints;
			text.Printer.TypeFaceStyle = new StyledTypeFace(typeFace ?? AggContext.DefaultFont, points, style.DoUnderline, style.FlattenCurves)
			{
				// It is UI text, so it follows the System window's typography settings like any TextWidget.
				ApplyTextStyleSettings = true,
			};

			StyledTypeFace newStyle = text.Printer.TypeFaceStyle;
			double overhang = Math.Max(0, (newStyle.AscentInPixels - newStyle.DescentInPixels - newStyle.EmSizeInPixels) / 2);
			text.Padding = new BorderDouble(text.Padding.Left, overhang / GuiWidget.DeviceScale, text.Padding.Right, overhang / GuiWidget.DeviceScale);
			text.DoExpandBoundsToText();
		}
	}
}
