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

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace MatterHackers.Agg.UI
{
    public class MyGLControl : GLControl
    {
        internal static MyGLControl currentControl;

        static int nextId;
        public int Id;

        // If you have an error here it is likely that you need to bulid your project with Platform Target x86.
        public MyGLControl(int bitDepth, int setencilDepth)
            : base(new GraphicsMode())
        {
            Id = nextId++;
        }

        internal void MakeCurrent()
        {
            currentControl = this;
            base.MakeCurrent();
        }

        protected override bool ProcessDialogKey(System.Windows.Forms.Keys keyData)
        {
            return false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Parent.Invalidate();
            base.OnPaint(e);
        }

        public override string ToString()
        {
            return "{0}".FormatWith(Id);
        }
    }

    public class WindowsFormsOpenGL : WindowsFormsAbstract
    {
        MyGLControl glControl;

        public WindowsFormsOpenGL(AbstractOsMappingWidget app, SystemWindow childSystemWindow)
        {
            switch (childSystemWindow.BitDepth)
            {
                case 32:
                    glControl = new MyGLControl(32, childSystemWindow.StencilBufferDepth);
                    break;

                default:
                    throw new NotImplementedException();
            }

            Controls.Add(glControl);

            SetUpFormsWindow(app, childSystemWindow);

            HookWindowsInputAndSendToWidget communication = new HookWindowsInputAndSendToWidget(glControl, aggAppWidget);
        }

        bool doneLoading = false;
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            doneLoading = true;
            ((WidgetForWindowsFormsOpenGL)aggAppWidget).Init();
        }

        private void SetupViewport()
        {
            // If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
            // Call this function you have called Show().
            glControl.MakeCurrent();
            int w = glControl.Width;
            int h = glControl.Height;
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, w, 0, h, -1, 1); // Bottom-left corner pixel has coordinate (0, 0)
            GL.Viewport(0, 0, w, h); // Use all of the glControl painting area
        }

        protected override void OnPaint(PaintEventArgs paintEventArgs)
        {
            // We have to make current the gl for the window we are.
            // If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
            // Call this function after you have called Show().
            glControl.MakeCurrent();
            base.OnPaint(paintEventArgs);
        }

        protected override void OnResize(EventArgs e)
        {
            if (doneLoading)
            {
                // If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
                // Call this function you have called Show().
                glControl.MakeCurrent();
                Invalidate();
                //glSurface.Location = new Point(0, 0);
                glControl.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
                base.OnResize(e);
                SetupViewport();
            }
        }

#if false
        void MakeCurrentAndInvalidate()
        {
            glControl.MakeCurrent();
            Invalidate();
        }

        internal override void RequestInvalidate(Rectangle windowsRectToInvalidate)
        {
            if (doneLoading)
            {
                if (InvokeRequired)
                {
                    // This currently causes a lock when we close a window (the main window locks).
                    //Invoke(new MethodInvoker(MakeCurrentAndInvalidate));
                }
                else
                {
                    glControl.MakeCurrent();
                    base.RequestInvalidate(windowsRectToInvalidate);
                }
            }
        }
#endif

        public override Size MinimumSize
        {
            get
            {
                return base.MinimumSize;
            }
            set
            {
                if (doneLoading)
                {
                    glControl.MakeCurrent();
                }

                base.MinimumSize = value;
            }
        }

        public override void CopyBackBufferToScreen(Graphics displayGraphics)
        {
            // If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
            // Call this function you have called Show().
            glControl.SwapBuffers();
        }
    }
}