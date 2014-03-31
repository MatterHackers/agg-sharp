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
using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

using MatterHackers.RenderOpenGl;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;

using OpenTK.Graphics.OpenGL;

namespace MatterHackers.Agg.UI
{
    public class WidgetForWindowsFormsOpenGL : WidgetForWindowsFormsAbstract
    {
        private int initWidth;
        private int initHeight;
        private CreateFlags initFlags;

        public WidgetForWindowsFormsOpenGL(int initWidth, int initHeight, CreateFlags initFlags, PixelFormat pixelFormat, int stencilDepth = 0)
            : base(GuiHalWidget.ImageFormats.pix_format_bgra32)
        {
            this.initWidth = initWidth;
            this.initHeight = initHeight;
            this.initFlags = initFlags;

            WindowsFormsWindow = new WindowsFormsOpenGL(this, GuiHalWidget.ImageFormats.pix_format_bgra32, stencilDepth);
        }

        public override void OnBoundsChanged(EventArgs e)
        {
			GL.Viewport(0, 0, WindowsFormsWindow.ClientSize.Width, WindowsFormsWindow.ClientSize.Height);					// Reset The Current Viewport


            // The following lines set the screen up for a perspective view. Meaning things in the distance get smaller. 
            // This creates a realistic looking scene. 
            // The perspective is calculated with a 45 degree viewing angle based on the windows width and height. 
            // The 0.1f, 100.0f is the starting point and ending point for how deep we can draw into the screen.

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
			GL.Scissor(0, 0, WindowsFormsWindow.ClientSize.Width, WindowsFormsWindow.ClientSize.Height);

            NewGraphics2D().Clear(new RGBA_Floats(1, 1, 1, 1));

            base.OnBoundsChanged(e);
        }

        public override Graphics2D NewGraphics2D()
        {
            Graphics2D graphics2D;

			graphics2D = new Graphics2DOpenGL(WindowsFormsWindow.ClientSize.Width, WindowsFormsWindow.ClientSize.Height);

            graphics2D.PushTransform();
            return graphics2D;
        }

        public void Init()
        {
            if (WindowsFormsWindow.systemImageFormat == GuiHalWidget.ImageFormats.pix_format_undefined)
            {
                throw new InvalidDataException();
            }

            m_window_flags = initFlags;

            System.Drawing.Size clientSize = new System.Drawing.Size();
            clientSize.Width = initWidth;
            clientSize.Height = initHeight;
            WindowsFormsWindow.ClientSize = clientSize;

            if ((m_window_flags & CreateFlags.Resizable) == 0)
            {
                WindowsFormsWindow.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                WindowsFormsWindow.MaximizeBox = false;
            }

            initialWidth = initWidth;
            initialHeight = initHeight;

            clientSize.Width = initWidth;
            clientSize.Height = initHeight;
            WindowsFormsWindow.ClientSize = clientSize;

            OnInitialize();
        }

        public override void OnInitialize()
        {
            NewGraphics2D().Clear(new RGBA_Floats(1, 1, 1, 1));

            base.OnInitialize();
        }

        public override void Run()
        {
            base.Run();
        }
    }
}