//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# Port port by: Lars Brubaker
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
using System.Collections.Generic;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    //------------------------------------------------------------------------
    public class TextWidget : GuiWidget
    {
        static bool debugIt = false;
        public static bool DoubleBufferDefault = true;

        RGBA_Bytes textColor;

        TypeFacePrinter printer;

        public bool EllipsisIfClipped { get; set; }

        public TypeFacePrinter Printer
        {
            get 
			{ 
				return printer; 
			}
        }

        public TextWidget(string text, double x = 0, double y = 0, double pointSize = 12, Justification justification = Justification.Left, RGBA_Bytes textColor = new RGBA_Bytes(), bool ellipsisIfClipped = true, bool underline = false, RGBA_Bytes backgroundColor = new RGBA_Bytes())
        {
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
            StyledTypeFace typeFaceStyle = new StyledTypeFace(LiberationSansFont.Instance, pointSize, underline);
            printer = new TypeFacePrinter(text, typeFaceStyle, justification: justification);

            LocalBounds = printer.LocalBounds;

            MinimumSize = new Vector2(LocalBounds.Width, LocalBounds.Height);
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
            if (Text == "")
            {
                printer.Text = " ";
                LocalBounds = printer.LocalBounds;
                printer.Text = "";
            }
            if (LocalBounds.Width < 1)
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
                string convertedToNewline = value.Replace('\r', '\n');
                if (base.Text != convertedToNewline)
                {
                    base.Text = convertedToNewline;
                    // Text may have been changed by a call back be sure to use what we really have set
                    printer = new TypeFacePrinter(base.Text, printer.TypeFaceStyle, justification: printer.Justification);
                    if (AutoExpandBoundsToText)
                    {
                        DoExpandBoundsToText();
                    }
                    Invalidate();
                }
                if (Text.Contains("\r"))
                {
                    throw new Exception("These should have be converted to \n.");
                }
            }
        }

        char[] spaceTrim = { ' ' };

        public override void OnDraw(Graphics2D graphics2D)
		{
            graphics2D.PushTransform();

            int numLines = Text.Split('\n').Length - 1;
            if(Text.Contains("\r"))
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

            if (debugIt)
            {
                graphics2D.Line(-5, 0, 5, 0, RGBA_Bytes.Blue);
                graphics2D.Line(0, -5, 0, 5, RGBA_Bytes.Blue);
            }

            graphics2D.PopTransform();

            if (debugIt)
            {
                graphics2D.Line(-5, 0, 5, 0, RGBA_Bytes.Red);
                graphics2D.Line(0, -5, 0, 5, RGBA_Bytes.Red);
            }

			base.OnDraw(graphics2D);
		}

        public RGBA_Bytes TextColor
        {
            get
            {
                return textColor;
            }

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