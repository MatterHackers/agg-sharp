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
        public WidgetForWindowsFormsOpenGL(SystemWindow childSystemWindow)
            : base(childSystemWindow)
        {
            WindowsFormsWindow = new WindowsFormsOpenGL(this, childSystemWindow);
        }

        public override void OnBoundsChanged(EventArgs e)
        {
            if (initHasBeenCalled)
            {
                SetAndClearViewPort();
            }

            base.OnBoundsChanged(e);
        }

        bool viewPortHasBeenSet = false;
        private void SetAndClearViewPort()
        {
            GL.Viewport(0, 0, WindowsFormsWindow.ClientSize.Width, WindowsFormsWindow.ClientSize.Height);					// Reset The Current Viewport
            viewPortHasBeenSet = true;

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
        }

        public override Graphics2D NewGraphics2D()
        {
            if (!viewPortHasBeenSet)
            {
                SetAndClearViewPort();
            }

            Graphics2D graphics2D;

			graphics2D = new Graphics2DOpenGL(WindowsFormsWindow.ClientSize.Width, WindowsFormsWindow.ClientSize.Height);

            graphics2D.PushTransform();
            return graphics2D;
        }

        bool initHasBeenCalled = false;
        public void Init()
        {
            System.Drawing.Size clientSize = new System.Drawing.Size();
            clientSize.Width = (int)childSystemWindow.Width;
            clientSize.Height = (int)childSystemWindow.Height;
            WindowsFormsWindow.ClientSize = clientSize;

            if (!childSystemWindow.Resizable)
            {
                WindowsFormsWindow.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                WindowsFormsWindow.MaximizeBox = false;
            }

            clientSize.Width = (int)childSystemWindow.Width;
            clientSize.Height = (int)childSystemWindow.Height;
            WindowsFormsWindow.ClientSize = clientSize;

            OnInitialize();

            initHasBeenCalled = true;
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