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
using System;
using System.Windows.Forms;
using System.Drawing;

using OpenTK;
using MatterHackers.RenderOpenGl;

#if USE_GLES
using OpenTK.Graphics.ES11;
#else

using OpenTK.Graphics.OpenGL;

#endif

namespace MatterHackers.Agg.UI
{
	public class MyGLControl : GLControl
	{
		internal static MyGLControl currentControl;
        static bool checkedCapabilities = false;

		private static int nextId;
		public int Id;

        internal RemoveGlDataCallBackHolder releaseAllGlData = new RemoveGlDataCallBackHolder();

        // If you have an error here it is likely that you need to bulid your project with Platform Target x86.
        public MyGLControl(int bitDepth, int setencilDepth)
		//: base(new GraphicsMode(new ColorFormat(32), 32, 0, 4))
		{
            if (!checkedCapabilities)
            {
				try
				{
					IntPtr address = (this.Context as OpenTK.Graphics.IGraphicsContextInternal).GetAddress("glGenBuffers");

					string versionString = GL.GetString(StringName.Version);
					int firstSpace = versionString.IndexOf(' ');
					if (firstSpace != -1)
					{
						versionString = versionString.Substring(0, firstSpace);
					}

					Version openGLVersion = new Version(versionString);
					string glExtensionsString = GL.GetString(StringName.Extensions);
					bool extensionSupport = glExtensionsString.Contains("GL_ARB_vertex_attrib_binding");

					if (openGLVersion.CompareTo(new Version(2, 1)) < 0 && !extensionSupport)
					{
						MatterHackers.RenderOpenGl.OpenGl.GL.DisableGlBuffers();
					}
				}
				catch
				{
					MatterHackers.RenderOpenGl.OpenGl.GL.DisableGlBuffers();
				}

				checkedCapabilities = true;
            }
			Id = nextId++;
		}

		
		public new void MakeCurrent()
		{
			currentControl = this;
			base.MakeCurrent();
            ImageGlPlugin.SetCurrentContextData(Id, releaseAllGlData);
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
		private MyGLControl glControl;

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

		private bool doneLoading = false;

        protected override void OnClosed(EventArgs e)
        {
            glControl.MakeCurrent();

            glControl.releaseAllGlData.Release();

            base.OnClosed(e);
        }

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
#if USE_GLES
            GL.MatrixMode(All.Projection);
#else
			GL.MatrixMode(MatrixMode.Projection);
#endif
			GL.LoadIdentity();
			GL.Ortho(0, w, 0, h, -1, 1); // Bottom-left corner pixel has coordinate (0, 0)
			GL.Viewport(0, 0, w, h); // Use all of the glControl painting area
		}

		protected override void OnPaint(PaintEventArgs paintEventArgs)
		{
			if (Focused)
			{
				glControl.Focus();
			}
			// We have to make current the gl for the window we are.
			// If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
			// Call this function after you have called Show().
			glControl.MakeCurrent();
			base.OnPaint(paintEventArgs);
		}

		protected override void OnResize(EventArgs e)
		{
			Rectangle bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);

			// Suppress resize events on the Windows platform when the form is being minimized and/or when a Resize event has fired
			// but the control dimensions remain the same. This prevents a bounds resize from the current dimensions to 0,0 and a
			// subsequent resize on Restore to the origin dimensions. In addition, this guard prevents the loss of control state
			// due to cascading control regeneration and avoids the associated performance hit and visible rendering lag during Restore
			if (doneLoading && this.WindowState != FormWindowState.Minimized && glControl.Bounds != bounds)
			{
				// If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
				// Call this function you have called Show().
				glControl.MakeCurrent();
				Invalidate();
				//glSurface.Location = new Point(0, 0);
				glControl.Bounds = bounds;

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