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
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MatterHackers.Agg.Platform;
using MatterHackers.RenderOpenGl;

#if USE_GLES
using OpenTK.Graphics.ES11;
#else

using OpenTK.Graphics.OpenGL;

#endif

namespace MatterHackers.Agg.UI
{
	public class OpenGLSystemWindow : WinformsSystemWindow
	{
		private AggGLControl glControl;

		public OpenGLSystemWindow()
		{
			// Set GraphicsMode via user overridable settings
			var config = AggContext.Config.GraphicsMode;

			// NOTE: OpenTK3 will run in Windows Sandbox (no proper GL driver), but OpenTK4 won't, it seems.

			var graphicsMode = new OpenTK.WinForms.GLControlSettings
			{
				Profile = OpenTK.Windowing.Common.ContextProfile.Compatability,
#if DEBUG
				Flags = OpenTK.Windowing.Common.ContextFlags.Debug,
#endif
				NumberOfSamples = config.FSAASamples,
				IsEventDriven = true,
			};

			glControl = new AggGLControl(graphicsMode)
			{
				Dock = DockStyle.Fill,
				Location = new Point(0, 0),
				TabIndex = 0,
			};

			// TODO: Disable VSync when using OpenTK 4? Current versions on NuGet do not expose VSync.

			RenderOpenGl.OpenGl.GL.Instance = new OpenTkGl();

			this.Controls.Add(glControl);
		}

		private bool doneLoading = false;

		protected override void OnClosed(EventArgs e)
		{
			try
			{
				if (!this.IsDisposed && glControl != null && !glControl.IsDisposed)
				{
					// Release GL data without making context current to avoid GLFW errors
					try
					{
						glControl.releaseAllGlData.Release();
					}
					catch
					{
						// Ignore GL data release errors during shutdown
					}

					// Ensure control is removed from parent before disposal
					if (glControl.Parent != null)
					{
						glControl.Parent.Controls.Remove(glControl);
					}

					// Force disposal of the GL control
					glControl.Dispose();
				}

				// Dispose of all child controls to free handles
				while (this.Controls.Count > 0)
				{
					var control = this.Controls[0];
					this.Controls.Remove(control);
					control.Dispose();
				}

				// Force handle destruction
				if (this.IsHandleCreated)
				{
					this.DestroyHandle();
				}
			}
			catch (Exception)
			{
				// Ignore errors during cleanup - the context may already be invalid
				// This can happen during test scenarios or rapid window close/open cycles
			}
			finally
			{
				glControl = null;
			}

			base.OnClosed(e);
		}

		protected override void OnLoad(EventArgs e)
		{
			id = count++;

			base.OnLoad(e);

			doneLoading = true;

			this.EventSink = new WinformsEventSink(glControl, AggSystemWindow);

			// Init();
			if (!AggSystemWindow.Resizable)
			{
				this.FormBorderStyle = FormBorderStyle.FixedDialog;
				this.MaximizeBox = false;
			}

			// Change the WindowsForms window to match the target SystemWindow bounds
			this.ClientSize = new Size((int)AggSystemWindow.Width, (int)AggSystemWindow.Height);

			// Restore to the last maximized or normal window state
			this.WindowState = AggSystemWindow.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;

			this.IsInitialized = true;
			initHasBeenCalled = true;
		}

#if USE_OPENTK4
		// HACK: Huh!? Makes keyboard input work. https://github.com/opentk/GLControl/issues/18
		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			glControl.Focus();
		}
#endif

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
			// ENHANCED: Safe painting with disposal and context error handling
			try
			{
				// Check if the GL control is still valid before attempting to paint
				if (glControl == null || glControl.IsDisposed)
				{
					// The control has been disposed - skip painting
					return;
				}

				if (Focused)
				{
					try
					{
						glControl.Focus();
					}
					catch (ObjectDisposedException)
					{
						// Control was disposed while we were trying to focus it
						return;
					}
				}

				// We have to make current the gl for the window we are.
				// If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
				// Call this function after you have called Show().
				try
				{
					glControl.MakeCurrent();
				}
				catch (ObjectDisposedException)
				{
					// This is the specific exception we're fixing - the GLControl was disposed
					// between when the paint event was queued and when it's being processed
					return;
				}
				catch (System.InvalidOperationException)
				{
					// OpenGL context is invalid - skip painting
					return;
				}
				catch
				{
					// Any other OpenGL context error - skip painting
					return;
				}

				if (CheckGlControl())
				{
					base.OnPaint(paintEventArgs);
				}

				CheckGlControl();
			}
			catch (ObjectDisposedException)
			{
				// Final catch for any ObjectDisposedException that might occur
				// during the painting process
				return;
			}
			catch (System.InvalidOperationException)
			{
				// Handle invalid operation during painting (e.g., invalid GL context)
				return;
			}
			catch
			{
				// For any other unexpected errors during painting, fail gracefully
				// This prevents the test from crashing when OpenGL state is inconsistent
				return;
			}
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
				// glSurface.Location = new Point(0, 0);
				glControl.Bounds = bounds;

				if (initHasBeenCalled)
				{
					CheckGlControl();
					SetAndClearViewPort();
					base.OnResize(e);
					CheckGlControl();
				}
				else
				{
					base.OnResize(e);
				}

				SetupViewport();
			}
		}

		public override void CopyBackBufferToScreen(Graphics displayGraphics)
		{
			// If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
			// Call this function you have called Show().
			glControl.SwapBuffers();
		}

		private static int count;
		private int id;

		public override string ToString()
		{
			return id.ToString();
		}

		private bool viewPortHasBeenSet = false;

		private void SetAndClearViewPort()
		{
			GL.Viewport(0, 0, this.ClientSize.Width, this.ClientSize.Height);                   // Reset The Current Viewport
			viewPortHasBeenSet = true;

			// The following lines set the screen up for a perspective view. Meaning things in the distance get smaller.
			// This creates a realistic looking scene.
			// The perspective is calculated with a 45 degree viewing angle based on the windows width and height.
			// The 0.1f, 100.0f is the starting point and ending point for how deep we can draw into the screen.

			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();
			GL.Scissor(0, 0, this.ClientSize.Width, this.ClientSize.Height);

			NewGraphics2D().Clear(new ColorF(1, 1, 1, 1));
		}

		private bool CheckGlControl()
		{
			if (firstGlControlSeen == null)
			{
				firstGlControlSeen = AggGLControl.currentControl;
			}

			// if (firstGlControlSeen != MyGLControl.currentControl)
			if (AggGLControl.currentControl.Id != this.id)
			{
				Debug.WriteLine("Is {0} Should be {1}".FormatWith(firstGlControlSeen.Id, AggGLControl.currentControl.Id));
				// throw new Exception("We have the wrong gl control realized.");
				return false;
			}

			return true;
		}

		private AggGLControl firstGlControlSeen = null;

		public override Graphics2D NewGraphics2D()
		{
			if (!viewPortHasBeenSet)
			{
				SetAndClearViewPort();
			}

			Graphics2D graphics2D = new Graphics2DOpenGL(this.ClientSize.Width, this.ClientSize.Height, GuiWidget.DeviceScale);
			graphics2D.PushTransform();

			return graphics2D;
		}

		private bool initHasBeenCalled = false;

		private Keys modifierKeys = Keys.None;
		private bool modifiersOverridden = false;

		internal void SetModifierKeys(Keys modifiers)
		{
			modifierKeys = modifiers;
			modifiersOverridden = true;
		}

		public override Keys ModifierKeys
		{
			get
			{
				if (modifiersOverridden)
				{
					return modifierKeys;
				}
				return base.ModifierKeys;
			}
		}
	}
}
