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
using System.Drawing;
using System.Windows.Forms;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The WebGPU flavour of the WinForms host: structurally a copy of <see cref="D3D11SystemWindow"/>
	/// (same viewport setup, same <see cref="Graphics2DGpu"/> over a GL facade, same present per frame),
	/// with a <see cref="WebGpuControl"/> where the D3D11 one has its swapchain control.
	/// <para>
	/// Selected with <c>AGG_WINDOW_PROVIDER=webgpu</c>; nothing picks it by default, so the classic path
	/// stays the daily driver until the Phase 4.5 cutover.
	/// </para>
	/// </summary>
	public class WebGpuSystemWindow : WinformsSystemWindow
	{
		private WebGpuControl webGpuControl;
		private bool doneLoading;
		private bool viewPortHasBeenSet;

		/// <summary>
		/// A screenshot asked for but not taken yet. The read-back has to happen at the end of a frame,
		/// so a request made at any other time waits here for one.
		/// </summary>
		private string pendingScreenshotPath;

		public WebGpuSystemWindow()
		{
			this.webGpuControl = new WebGpuControl
			{
				Dock = DockStyle.Fill,
				Location = new Point(0, 0),
				TabIndex = 0,
			};

			this.Controls.Add(this.webGpuControl);
		}

		/// <summary>The control that owns the wgpu device and swapchain.</summary>
		public WebGpuControl WebGpuControl => this.webGpuControl;

		/// <inheritdoc/>
		public override string RenderErrorReport => this.webGpuControl?.LastError;

		/// <inheritdoc/>
		public override string RenderStatusReport
		{
			get
			{
				var control = this.webGpuControl;
				if (control?.Device == null)
				{
					return "webgpu not initialized";
				}

				return $"{control.BackendType} {control.Device.AdapterName}, presented {control.Surface?.PresentedFrameCount ?? 0}";
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			this.webGpuControl.InitializeWebGpu();

			this.doneLoading = true;

			this.EventSink = new WinformsEventSink(this.webGpuControl, AggSystemWindow);

			if (!AggSystemWindow.Resizable)
			{
				this.FormBorderStyle = FormBorderStyle.FixedDialog;
				this.MaximizeBox = false;
			}

			this.ClientSize = new Size((int)AggSystemWindow.Width, (int)AggSystemWindow.Height);
			this.WindowState = AggSystemWindow.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;

			this.IsInitialized = true;
		}

		protected override void OnClosed(EventArgs e)
		{
			try
			{
				if (!this.IsDisposed && this.webGpuControl != null && !this.webGpuControl.IsDisposed)
				{
					this.webGpuControl.Parent?.Controls.Remove(this.webGpuControl);
					this.webGpuControl.Dispose();
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
				this.webGpuControl = null;
			}

			base.OnClosed(e);
		}

		protected override void OnPaint(PaintEventArgs paintEventArgs)
		{
			try
			{
				if (this.webGpuControl == null || this.webGpuControl.IsDisposed)
				{
					return;
				}

				if (this.Focused)
				{
					try
					{
						this.webGpuControl.Focus();
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
		}

		protected override void OnResize(EventArgs e)
		{
			var bounds = new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height);

			if (this.doneLoading && this.WindowState != FormWindowState.Minimized && this.webGpuControl.Bounds != bounds)
			{
				this.Invalidate();
				this.webGpuControl.Bounds = bounds;
				this.viewPortHasBeenSet = false;
				base.OnResize(e);
			}
		}

		/// <summary>
		/// Reads the frame back through wgpu rather than through the base class's
		/// <c>CopyBackBufferToScreen</c> trick - there is no CPU back buffer to blit, and calling that
		/// path here would present the frame into a bitmap's Graphics and lose it.
		/// </summary>
		/// <remarks>
		/// The capture is deferred to the end of the next frame because that is the only moment a
		/// swapchain texture exists and is still readable.
		/// </remarks>
		public override void CaptureScreenshot(string path)
		{
			this.pendingScreenshotPath = path;
			this.Invalidate();
		}

		/// <summary>
		/// Presents the frame. Any screenshot requested for this frame is read back first: after the
		/// present the texture is the swapchain's again.
		/// </summary>
		public override void CopyBackBufferToScreen(Graphics displayGraphics)
		{
			if (this.webGpuControl == null || this.webGpuControl.IsDisposed)
			{
				return;
			}

			this.viewPortHasBeenSet = false;

			string screenshotPath = this.pendingScreenshotPath;
			if (screenshotPath == null)
			{
				this.webGpuControl.Present();
				return;
			}

			this.pendingScreenshotPath = null;
			this.CaptureThenPresent(screenshotPath);
		}

		public override Graphics2D NewGraphics2D()
		{
			if (this.webGpuControl?.Gl == null)
			{
				// Without this the caller gets a bare NullReferenceException out of Graphics2DGpu and no
				// hint at all that the real problem is a window painting before its wgpu device exists.
				throw new InvalidOperationException(
					"The WebGPU device is not initialized, so this window cannot make a Graphics2D. "
					+ "InitializeWebGpu runs from OnLoad and retries when the control's handle is created; "
					+ "reaching a paint before either happened means the control was never shown or its "
					+ "initialization threw.");
			}

			if (!this.viewPortHasBeenSet)
			{
				this.SetAndClearViewPort();
			}

			Graphics2D graphics2D = new Graphics2DGpu(
				this.webGpuControl.Gl,
				this.ClientSize.Width,
				this.ClientSize.Height,
				GuiWidget.DeviceScale);
			graphics2D.PushTransform();

			return graphics2D;
		}

		/// <summary>
		/// Saves the frame and then presents it. <c>async void</c> on purpose: this is the end of a paint
		/// message and there is nobody to hand a Task to. The native read-back completes before its
		/// ValueTask is returned, so the present still happens inline, while the frame is alive.
		/// </summary>
		/// <param name="path">Where to write the PNG.</param>
		private async void CaptureThenPresent(string path)
		{
			try
			{
				await this.webGpuControl.SaveCurrentFrameAsync(path);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"WebGpuSystemWindow screenshot failed: {ex.Message}");
			}

			this.webGpuControl.Present();
		}

		private void SetAndClearViewPort()
		{
			this.webGpuControl.BeginFrame();

			var gl = this.webGpuControl.Gl?.GpuContext;
			if (gl == null)
			{
				return;
			}

			gl.Viewport(0, 0, this.ClientSize.Width, this.ClientSize.Height);
			this.viewPortHasBeenSet = true;

			gl.MatrixMode(MatrixMode.Projection);
			gl.LoadIdentity();

			gl.MatrixMode(MatrixMode.Modelview);
			gl.LoadIdentity();
			gl.Scissor(0, 0, this.ClientSize.Width, this.ClientSize.Height);

			this.NewGraphics2D().Clear(new ColorF(1, 1, 1, 1));
		}
	}
}
