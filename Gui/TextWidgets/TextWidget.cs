//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// classes rbox_ctrl_impl, rbox_ctrl
//
//----------------------------------------------------------------------------
using System;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	//------------------------------------------------------------------------
	public class TextWidget : GuiWidget
	{
		public static bool DoubleBufferDefault = true;

		private RGBA_Bytes textColor;

		private TypeFacePrinter printer;

		public bool EllipsisIfClipped { get; set; }

		public RGBA_Bytes DisabledColor { get; set; }

		public double PointSize
		{
			get
			{
				return printer.TypeFaceStyle.EmSizeInPoints / GuiWidget.DeviceScale;
			}

			set
			{
				printer.TypeFaceStyle = new StyledTypeFace(printer.TypeFaceStyle.TypeFace, value * GuiWidget.DeviceScale, printer.TypeFaceStyle.DoUnderline, printer.TypeFaceStyle.FlatenCurves);

				if (AutoExpandBoundsToText)
				{
					DoExpandBoundsToText();
				}

				this.Invalidate();
			}
		}

		public TypeFacePrinter Printer
		{
			get
			{
				return printer;
			}
		}

		public TextWidget(string text, double x = 0, double y = 0, double pointSize = 12, Justification justification = Justification.Left, RGBA_Bytes textColor = new RGBA_Bytes(), bool ellipsisIfClipped = true, bool underline = false, RGBA_Bytes backgroundColor = new RGBA_Bytes(), TypeFace typeFace = null)
		{
			DisabledColor = new RGBA_Bytes(textColor, 50);

			Selectable = false;
			DoubleBuffer = DoubleBufferDefault;
			AutoExpandBoundsToText = false;
			EllipsisIfClipped = ellipsisIfClipped;
			OriginRelativeParent = new Vector2(x, y);
			this.textColor = textColor;
			if (this.textColor.Alpha0To255 == 0)
			{
				// we assume it is the default if alpha 0.  Also there is no reason to make a text color of this as it will draw nothing.
				this.textColor = RGBA_Bytes.Black;
			}
			if (backgroundColor.Alpha0To255 != 0)
			{
				BackgroundColor = backgroundColor;
			}

			base.Text = text;
			if (typeFace == null)
			{
				typeFace = LiberationSansFont.Instance;
			}
			StyledTypeFace typeFaceStyle = new StyledTypeFace(typeFace, pointSize * GuiWidget.DeviceScale, underline);
			printer = new TypeFacePrinter(text, typeFaceStyle, justification: justification);

			LocalBounds = printer.LocalBounds;

			MinimumSize = new Vector2(0, LocalBounds.Height);
		}

		public override RectangleDouble LocalBounds
		{
			get
			{
				return base.LocalBounds;
			}
			set
			{
				if (value != LocalBounds)
				{
					if (AutoExpandBoundsToText)
					{
						RectangleDouble textBoundsWithPadding = printer.LocalBounds;
						textBoundsWithPadding.Inflate(Padding);
						MinimumSize = new Vector2(textBoundsWithPadding.Width, textBoundsWithPadding.Height);
						base.LocalBounds = textBoundsWithPadding;
					}
					else
					{
						base.LocalBounds = value;
					}
				}
			}
		}

		public override BorderDouble Padding
		{
			get
			{
				return base.Padding;
			}
			set
			{
				if (Padding != value)
				{
					base.Padding = value;
					if (AutoExpandBoundsToText)
					{
						LocalBounds = LocalBounds;
					}
				}
			}
		}

		public bool AutoExpandBoundsToText { get; set; }

		public void DoExpandBoundsToText()
		{
			Invalidate(); // do it before and after in case it changes size.
			LocalBounds = printer.LocalBounds;
			if (Text == "" || LocalBounds.Width < 1)
			{
				printer.Text = " ";
				LocalBounds = printer.LocalBounds;
				printer.Text = "";
			}

			Invalidate();
		}

		public override string Text
		{
			get
			{
				return base.Text;
			}
			set
			{
				string convertedText = value;
				if (value != null)
				{
					convertedText = value.Replace("\r\n", "\n");
					convertedText = convertedText.Replace('\r', '\n');
					if (Text.Contains("\r"))
					{
						throw new Exception("These should have be converted to \n.");
					}
				}

				if (base.Text != convertedText)
				{
					base.Text = convertedText;
					bool wasUsingHintedCache = printer.DrawFromHintedCache;
					// Text may have been changed by a call back be sure to use what we really have set
					printer = new TypeFacePrinter(base.Text, printer.TypeFaceStyle, justification: printer.Justification);
					printer.DrawFromHintedCache = wasUsingHintedCache;
					if (AutoExpandBoundsToText)
					{
						DoExpandBoundsToText();
					}
					Invalidate();
				}
			}
		}

		private char[] spaceTrim = { ' ' };

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.PushTransform();

			int numLines = Text.Split('\n').Length - 1;
			if (Text.Contains("\r"))
			{
				throw new Exception("These should have be converted to \n.");
			}

			double yOffsetForText = Printer.TypeFaceStyle.EmSizeInPixels * numLines;
			double xOffsetForText = 0;
			switch (printer.Justification)
			{
				case Justification.Left:
					break;

				case Justification.Center:
					xOffsetForText = (Width - Printer.LocalBounds.Width) / 2;
					break;

				case Justification.Right:
					xOffsetForText = Width - Printer.LocalBounds.Width;
					break;

				default:
					throw new NotImplementedException();
			}
			graphics2D.SetTransform(graphics2D.GetTransform() * Affine.NewTranslation(xOffsetForText, yOffsetForText));

			RGBA_Bytes currentColor = this.textColor;

			if (EllipsisIfClipped && Printer.LocalBounds.Width > LocalBounds.Width) // only do this if it's static text
			{
				TypeFacePrinter shortTextPrinter = Printer;
				shortTextPrinter.DrawFromHintedCache = Printer.DrawFromHintedCache;
				while (shortTextPrinter.LocalBounds.Width > LocalBounds.Width && shortTextPrinter.Text.Length > 4)
				{
					shortTextPrinter = new TypeFacePrinter(shortTextPrinter.Text.Substring(0, shortTextPrinter.Text.Length - 4).TrimEnd(spaceTrim) + "...", Printer);
				}
				shortTextPrinter.Render(graphics2D, currentColor);
			}
			else
			{
				// it all fits or it's editable (if editable it will need to be offset/scrolled sometimes).
				Printer.Render(graphics2D, currentColor);
			}

			// Debug onscreen fonts
			if (false && this.Text.Trim().Length > 1)
			{
				graphics2D.FillRectangle(this.Width - 13, 0, this.Width, this.Height - 2, RGBA_Bytes.White);
				graphics2D.DrawString(this.PointSize.ToString(), this.Width - 12, 2, 8, color: RGBA_Bytes.Red);
			}

			graphics2D.PopTransform();

			base.OnDraw(graphics2D);
		}

		public RGBA_Bytes TextColor
		{
			get => (this.Enabled) ? textColor : this.DisabledColor;
			set
			{
				if (textColor != value)
				{
					textColor = value;
					this.Invalidate();
				}
			}
		}
	}
}
