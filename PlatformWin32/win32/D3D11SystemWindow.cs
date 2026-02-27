/*
Copyright (c) 2025, Lars Brubaker
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
using System.Drawing;
using System.Windows.Forms;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;

namespace MatterHackers.Agg.UI
{
	public class D3D11SystemWindow : WinformsSystemWindow
	{
		private D3D11Control d3dControl;
		private bool doneLoading = false;
		private bool viewPortHasBeenSet = false;
		private bool initHasBeenCalled = false;

		public D3D11SystemWindow()
		{
			d3dControl = new D3D11Control
			{
				Dock = DockStyle.Fill,
				Location = new Point(0, 0),
				TabIndex = 0,
			};

			this.Controls.Add(d3dControl);
		}

		private Timer screenshotTimer;

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			DebugLog("OnLoad: Initializing D3D11...");
			d3dControl.InitializeD3D();
			GL.Instance = d3dControl.GlBackend;
			DebugLog($"OnLoad: D3D11 initialized. Device={d3dControl.Device != null}, GL.Instance={GL.Instance?.GetType().Name}");

			screenshotTimer = new Timer { Interval = 8000 };
			screenshotTimer.Tick += (s, ev) =>
			{
				screenshotTimer.Stop();
				screenshotTimer.Dispose();
				screenshotTimer = null;
				Invalidate();
				Application.DoEvents();
				try
				{
					var path = System.IO.Path.Combine(
						Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
						"mattercad_d3d11_timed.png");
					d3dControl.CaptureScreenshot(path);
					DebugLog($"Timed screenshot saved: {path}");
				}
				catch (Exception ex) { DebugLog($"Timed screenshot error: {ex.Message}"); }
			};
			screenshotTimer.Start();

			var autoExitTimer = new Timer { Interval = 15000 };
			autoExitTimer.Tick += (s, ev) =>
			{
				autoExitTimer.Stop();
				autoExitTimer.Dispose();
				DebugLog("Auto-exit timer fired, closing application.");
				Close();
			};
			autoExitTimer.Start();

			doneLoading = true;

			this.EventSink = new WinformsEventSink(d3dControl, AggSystemWindow);

			if (!AggSystemWindow.Resizable)
			{
				this.FormBorderStyle = FormBorderStyle.FixedDialog;
				this.MaximizeBox = false;
			}

			this.ClientSize = new Size((int)AggSystemWindow.Width, (int)AggSystemWindow.Height);
			this.WindowState = AggSystemWindow.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;

			this.IsInitialized = true;
			initHasBeenCalled = true;
		}

		protected override void OnClosed(EventArgs e)
		{
			try
			{
				if (!this.IsDisposed && d3dControl != null && !d3dControl.IsDisposed)
				{
					if (d3dControl.Parent != null)
					{
						d3dControl.Parent.Controls.Remove(d3dControl);
					}

					d3dControl.Dispose();
				}

				while (this.Controls.Count > 0)
				{
					var control = this.Controls[0];
					this.Controls.Remove(control);
					control.Dispose();
				}

				if (this.IsHandleCreated)
				{
					this.DestroyHandle();
				}
			}
			catch
			{
			}
			finally
			{
				d3dControl = null;
			}

			base.OnClosed(e);
		}

		protected override void OnPaint(PaintEventArgs paintEventArgs)
		{
			try
			{
				if (d3dControl == null || d3dControl.IsDisposed)
				{
					return;
				}

				if (Focused)
				{
					try
					{
						d3dControl.Focus();
					}
					catch (ObjectDisposedException)
					{
						return;
					}
				}

				base.OnPaint(paintEventArgs);
			}
			catch (ObjectDisposedException)
			{
			}
			catch (InvalidOperationException)
			{
			}
		}

		protected override void OnResize(EventArgs e)
		{
			Rectangle bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);

			if (doneLoading && this.WindowState != FormWindowState.Minimized && d3dControl.Bounds != bounds)
			{
				Invalidate();
				d3dControl.Bounds = bounds;

				if (initHasBeenCalled)
				{
					SetAndClearViewPort();
					base.OnResize(e);
				}
				else
				{
					base.OnResize(e);
				}
			}
		}

		private int frameCount;
		private static readonly string debugLogPath = System.IO.Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "d3d11_debug.log");

		private void DebugLog(string msg)
		{
			try { System.IO.File.AppendAllText(debugLogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n"); } catch { }
		}

		public override void CopyBackBufferToScreen(Graphics displayGraphics)
		{
			if (d3dControl != null && !d3dControl.IsDisposed)
			{
				frameCount++;

				if (frameCount <= 3 || frameCount % 100 == 0)
				{
					DebugLog($"Frame {frameCount}, ClientSize={ClientSize}");
				}

				if (frameCount == 2 || frameCount == 10 || frameCount == 50)
				{
					try
					{
						var path = System.IO.Path.Combine(
							Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
							$"mattercad_d3d11_frame{frameCount}.png");
						d3dControl.CaptureScreenshot(path);
						DebugLog($"Screenshot saved: {path}");
					}
					catch (Exception ex) { DebugLog($"Screenshot error: {ex.Message}"); }
				}

				d3dControl.Present();
				viewPortHasBeenSet = false;
			}
		}

		private void SetAndClearViewPort()
		{
			var gl = GL.Instance;
			if (gl == null) return;

			gl.Viewport(0, 0, this.ClientSize.Width, this.ClientSize.Height);
			viewPortHasBeenSet = true;

			gl.MatrixMode(MatrixMode.Projection);
			gl.LoadIdentity();

			gl.MatrixMode(MatrixMode.Modelview);
			gl.LoadIdentity();
			gl.Scissor(0, 0, this.ClientSize.Width, this.ClientSize.Height);

			NewGraphics2D().Clear(new ColorF(1, 1, 1, 1));
		}

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
	}
}
