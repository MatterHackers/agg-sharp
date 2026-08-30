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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The WinForms window host: a <see cref="WebGpuControl"/> for the swapchain, a
	/// <see cref="Graphics2DGpu"/> over a GL facade for widget paint, and one present per frame. Since the
	/// Phase 4.5 cutover this is the only window backend; <c>AGG_WINDOW_PROVIDER=webgpu</c> still names it
	/// explicitly, but nothing else is selectable.
	/// </summary>
	public class WebGpuSystemWindow : WinformsSystemWindow
	{
		/// <summary>
		/// The single consumption point of <see cref="SystemWindow.UseGpu"/>, which is seeded from
		/// RootSystemWindow.DefaultUseGpu by the FORCE_SOFTWARE_RENDERING command-line flag. Returns true
		/// when the window asked for no GPU, so wgpu is made to pick its software (fallback) adapter.
		/// Defaults to hardware when there is no window to ask.
		/// </summary>
		public static bool ShouldUseSoftwareAdapter(SystemWindow systemWindow) => systemWindow?.UseGpu == false;

		/// <summary>
		/// How many times <see cref="CaptureScreenshot"/> pumps the message queue waiting for a capture
		/// whose read-back suspended. Bounded so a window that never repaints cannot hang the caller; the
		/// native path does not reach the loop at all.
		/// </summary>
		private const int ScreenshotPumpSpins = 200;

		/// <summary>
		/// How long <see cref="CaptureScreenshotAsync"/> waits for a queued capture. Bounded for the same
		/// reason <see cref="ScreenshotPumpSpins"/> is: a window that never repaints must not hang the caller.
		/// </summary>
		private static readonly TimeSpan ScreenshotAsyncTimeout = TimeSpan.FromSeconds(10);

		private WebGpuControl webGpuControl;
		private bool doneLoading;
		private bool viewPortHasBeenSet;

		/// <summary>
		/// A screenshot asked for but not taken yet. The read-back has to happen at the end of a frame,
		/// so a request made at any other time waits here for one.
		/// </summary>
		private string pendingScreenshotPath;

		/// <summary>Signalled by the paint that performs a queued capture, so the requester can return only
		/// once the file is on disk.</summary>
		private ManualResetEventSlim screenshotComplete;

		/// <summary>The same signal as <see cref="screenshotComplete"/> for an awaiting requester. Only one of
		/// the two is ever non-null: <see cref="ThrowIfCapturePending"/> holds both entry points to one
		/// in-flight request.</summary>
		private TaskCompletionSource screenshotCompletion;

		/// <summary>True while a paint is running, so a capture requested from inside one (the smoke run)
		/// queues instead of forcing a re-entrant paint.</summary>
		private bool isInsidePaint;

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

			this.webGpuControl.UseSoftwareAdapter = ShouldUseSoftwareAdapter(AggSystemWindow);
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

				this.isInsidePaint = true;
				try
				{
					base.OnPaint(paintEventArgs);
				}
				finally
				{
					this.isInsidePaint = false;
				}
			}
			catch (ObjectDisposedException)
			{
				// Kept, though the paint no longer needs it: WinformsSystemWindow.OnPaint now absorbs the
				// close race at the paint boundary itself, so base.OnPaint above cannot reach here. What
				// still can is this.Focused - Control.Focused reads Handle, and a form mid-close answers
				// IsHandleCreated true while already disposed (the finding recorded at
				// WinformsSystemWindow.OnIdle's ObjectDisposedException filter), which makes that read throw.
				// Deleting this would send that one back to WinForms' modal ThreadExceptionDialog.
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
		/// path here would present the frame into a bitmap's Graphics and lose it. No
		/// <c>System.Drawing</c> anywhere on the path: the pixels go through agg's own
		/// <c>ImageBuffer</c>/<c>ImageIO</c>, which is what makes the same code work on mac later.
		/// </summary>
		/// <remarks>
		/// The read-back can only happen at the end of a frame - that is the only moment a swapchain
		/// texture exists and is still readable - so the request is queued and a paint forced, and this
		/// call does not return until that paint has written the file. Callers (failure diagnostics, the
		/// automation harness) treat <c>CaptureScreenshot</c> as "the PNG exists when I get control back",
		/// which is the contract every other <c>IPlatformWindow</c> gives them.
		/// </remarks>
		/// <param name="path">Where to write the PNG.</param>
		public override void CaptureScreenshot(string path)
		{
			if (this.InvokeRequired)
			{
				this.Invoke((Action)(() => this.CaptureScreenshot(path)));
				return;
			}

			if (this.webGpuControl == null || this.webGpuControl.IsDisposed)
			{
				return;
			}

			if (this.isInsidePaint)
			{
				// The smoke-run path asks from inside the paint, just before the present that would consume
				// the request. Forcing another paint from here would re-enter WM_PAINT; queuing is enough,
				// because this frame is about to run CopyBackBufferToScreen anyway.
				this.pendingScreenshotPath = path;
				return;
			}

			// The pump below runs arbitrary queued work, which is free to ask for a screenshot of its own.
			// One pending path and one completion signal is all the frame machinery has, so a second
			// request would steal this one's frame and leave this caller pumping for a capture that will
			// never happen.
			this.ThrowIfCapturePending(path);

			var completed = new ManualResetEventSlim(false);

			this.pendingScreenshotPath = path;
			this.screenshotComplete = completed;

			try
			{
				// Invalidate then Update: Update only sends WM_PAINT when there is an invalid region, and
				// that paint is what runs CopyBackBufferToScreen and therefore the capture.
				this.Invalidate();
				this.Update();

				// The native read-back completes inside the paint (wgpu's buffer map is polled to
				// completion there), so this is normally already set. It is only not set if the await in
				// CaptureThenPresent genuinely suspended, in which case its continuation is posted to this
				// thread's message queue - hence pumping rather than blocking, which would deadlock.
				for (int spin = 0; spin < ScreenshotPumpSpins && !completed.IsSet; spin++)
				{
					Application.DoEvents();
				}
			}
			finally
			{
				this.pendingScreenshotPath = null;
				this.screenshotComplete = null;

				// Only dispose once the capturing frame is done with it. If the pump gave up while the
				// capture was still in flight, CaptureThenPresent still holds this same instance and will
				// Set() it - and that Set would throw ObjectDisposedException inside an async void, which
				// is a process kill. Leaving it to the GC costs nothing: this event is only ever polled
				// through IsSet, so it never allocates a wait handle to leak.
				if (completed.IsSet)
				{
					completed.Dispose();
				}
			}
		}

		/// <summary>
		/// Refuses a capture while another one is still in flight. The frame machinery holds exactly one
		/// pending path and one completion signal, so overlapping requests cannot both be served, and
		/// failing loudly beats one caller silently receiving the other's frame - or no frame at all.
		/// </summary>
		private void ThrowIfCapturePending(string path)
		{
			if (this.pendingScreenshotPath != null
				|| this.screenshotComplete != null
				|| this.screenshotCompletion != null)
			{
				throw new InvalidOperationException(
					$"A screenshot capture is already pending (to '{this.pendingScreenshotPath}'); only one capture can be in flight at a time. Requested '{path}'.");
			}
		}

		/// <summary>
		/// The awaitable form of <see cref="CaptureScreenshot"/>. Queues the same request onto the same
		/// frame machinery, but waits on the completion the capturing paint posts instead of pumping
		/// messages, so the caller's thread is free while the frame runs.
		/// </summary>
		/// <param name="path">Where to write the PNG.</param>
		public override async Task CaptureScreenshotAsync(string path)
		{
			if (this.InvokeRequired)
			{
				// Invoke returns as soon as the UI thread's call reaches its first suspension, handing back
				// the Task to finish waiting on here rather than blocking that thread.
				await (Task)this.Invoke((Func<Task>)(() => this.CaptureScreenshotAsync(path)));
				return;
			}

			if (this.webGpuControl == null || this.webGpuControl.IsDisposed)
			{
				return;
			}

			if (this.isInsidePaint)
			{
				// Same as the synchronous path: this frame is about to consume the request, and forcing
				// another paint from here would re-enter WM_PAINT.
				this.pendingScreenshotPath = path;
				return;
			}

			this.ThrowIfCapturePending(path);

			var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

			this.pendingScreenshotPath = path;
			this.screenshotCompletion = completion;

			try
			{
				// Invalidate then Update: Update only sends WM_PAINT when there is an invalid region, and
				// that paint is what runs CopyBackBufferToScreen and therefore the capture. The read-back
				// usually finishes inside that synchronous paint, leaving nothing to await.
				this.Invalidate();
				this.Update();

				// A capture that ran and failed faults here, on purpose: the contract is that the file
				// exists once this completes, so a swallowed failure would be a lie.
				await completion.Task.WaitAsync(ScreenshotAsyncTimeout);
			}
			catch (TimeoutException)
			{
				// Matches the synchronous path's bounded pump, which also gives up quietly rather than
				// hanging a caller whose window never painted.
			}
			finally
			{
				// Only clear what still belongs to this request. A timed-out capture leaves this method
				// while its frame is still running, and by the time that frame finishes and this cleanup
				// runs, the fields may already have been claimed by the next request.
				if (ReferenceEquals(this.screenshotCompletion, completion))
				{
					this.pendingScreenshotPath = null;
					this.screenshotCompletion = null;
				}
			}
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
			this.CaptureThenPresent(screenshotPath, this.screenshotComplete, this.screenshotCompletion);
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
		/// <param name="completed">Signalled once the file is written (or the attempt has failed), so a
		/// synchronous <see cref="CaptureScreenshot"/> caller knows when to stop pumping. May be null when
		/// the capture was requested by the smoke-run path, which does not wait.</param>
		/// <param name="completion">The same signal for an awaiting <see cref="CaptureScreenshotAsync"/>
		/// caller. Null unless the request came from there. A failed capture faults it rather than
		/// completing it, so the async contract's "the file exists once the task completes" holds.</param>
		private async void CaptureThenPresent(string path, ManualResetEventSlim completed, TaskCompletionSource completion)
		{
			Exception failure = null;

			try
			{
				await this.webGpuControl.SaveCurrentFrameAsync(path);
			}
			catch (Exception ex)
			{
				// The synchronous caller has no channel for this - releasing its pump is all that can be
				// done for it - so it keeps the long-standing behaviour of a stderr note and a quiet give up.
				failure = ex;
				Console.Error.WriteLine($"WebGpuSystemWindow screenshot failed: {ex.Message}");
			}
			finally
			{
				completed?.Set();

				// TrySet, not Set: the awaiting caller may already have timed out and moved on.
				if (failure != null)
				{
					completion?.TrySetException(failure);
				}
				else
				{
					completion?.TrySetResult();
				}
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
