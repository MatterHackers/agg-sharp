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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.RenderGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// A retained GPU layer released from a thread that is not drawing - a widget closed by a worker, as
	/// MatterCAD's library view does when it reloads its rows - must not touch the context's GPU state
	/// there. The render thread owns the pipeline cache's bind groups; the release waits for it.
	/// </summary>
	[NotInParallel]
	public class GpuLayerThreadingTests
	{
		private const int Width = 64;
		private const int Height = 48;

		[Test]
		public async Task RetainedLayerDisposedOffTheRenderThreadWaitsForIt()
		{
			var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var device = capture.Device;
			try
			{
				// All of the rendering stays on this thread, with no await before the checks, so this thread is
				// the render thread throughout.
				var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
				var layer = (GpuRetainedLayer)graphics.CreateRetainedLayer();
				using (var paint = layer.Begin(16, 16))
				{
					paint.Graphics.Clear(Color.Red);
				}

				graphics.RenderRetainedLayer(layer, 4, 4);
				capture.Context.Submit();

				var failure = ReleaseOnAnotherThreadWhileDrawing(capture, graphics, layer.Dispose);

				await Assert.That(failure).IsNull();

				// Nothing is released until the render thread gets to it...
				await Assert.That(layer.Target.Texture).IsNotNull();

				// ...and its next submit does.
				capture.Context.Submit();
				await Assert.That(layer.Target.Texture).IsNull();
			}
			finally
			{
				capture.Dispose();
			}

			await Assert.That(device.LiveResources.Count).IsEqualTo(0);
		}

		[Test]
		public async Task BackbufferedWidgetClosedOffTheRenderThreadWaitsForIt()
		{
			var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var device = capture.Device;
			try
			{
				var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
				var root = new GuiWidget(Width, Height);
				var child = new GuiWidget(32, 24) { BackgroundColor = Color.Red, DoubleBuffer = true };
				root.AddChild(child);
				root.OnDraw(graphics);
				capture.Context.Submit();

				await Assert.That(child.ResolveBackbufferMode(graphics)).IsEqualTo(BackbufferMode.GpuTexture);

				var failure = ReleaseOnAnotherThreadWhileDrawing(capture, graphics, root.Close);

				await Assert.That(failure).IsNull();
				await Assert.That(child.HasBeenClosed).IsTrue();
			}
			finally
			{
				capture.Dispose();
			}

			// Still queued when the context went away: released with it, not leaked.
			await Assert.That(device.LiveResources.Count).IsEqualTo(0);
		}

		[Test]
		public async Task WidgetClosedByAWorkerMidFrameIsNotRepaintedIntoANewLayer()
		{
			var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var device = capture.Device;
			try
			{
				var root = new GuiWidget(Width, Height);
				var closer = new CloseOnDrawWidget();
				var buffered = new PaintCountingWidget { BackgroundColor = Color.Red, DoubleBuffer = true };
				root.AddChild(closer);
				root.AddChild(buffered);

				var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
				root.OnDraw(graphics);
				capture.Context.Submit();
				await Assert.That(buffered.PaintsAfterClose).IsEqualTo(0);

				// The next frame repaints the buffered widget, but the widget drawn before it has it closed by a
				// worker first - a list reloading its rows off the UI thread.
				buffered.Invalidate();
				closer.CloseOnWorker = buffered;
				graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
				root.OnDraw(graphics);
				capture.Context.Submit();

				await Assert.That(closer.Failure).IsNull();
				await Assert.That(buffered.HasBeenClosed).IsTrue();
				await Assert.That(buffered.PaintsAfterClose).IsEqualTo(0)
					.Because("a closed widget must not be painted into a new retained layer that nothing will release");
			}
			finally
			{
				capture.Dispose();
			}

			await Assert.That(device.LiveResources.Count).IsEqualTo(0);
		}

		private sealed class CloseOnDrawWidget : GuiWidget
		{
			public CloseOnDrawWidget()
				: base(8, 8)
			{
			}

			public GuiWidget CloseOnWorker { get; set; }

			public Exception Failure { get; private set; }

			public override void OnDraw(Graphics2D graphics2D)
			{
				var target = this.CloseOnWorker;
				this.CloseOnWorker = null;
				if (target != null)
				{
					var worker = new Thread(() =>
					{
						try
						{
							target.Close();
						}
						catch (Exception exception)
						{
							this.Failure = exception;
						}
					});
					worker.Start();
					worker.Join();
				}

				base.OnDraw(graphics2D);
			}
		}

		private sealed class PaintCountingWidget : GuiWidget
		{
			public PaintCountingWidget()
				: base(32, 24)
			{
			}

			public int PaintsAfterClose { get; private set; }

			public override void OnDraw(Graphics2D graphics2D)
			{
				if (this.HasBeenClosed)
				{
					this.PaintsAfterClose++;
				}

				base.OnDraw(graphics2D);
			}
		}

		/// <summary>
		/// Runs <paramref name="release"/> on a new thread. Should it scan the pipeline cache's bind groups,
		/// this (render) thread draws a new image into the frame mid-scan - adding a bind group, as a frame
		/// in progress does - and the scan fails as it did in the field. Returns what the thread threw.
		/// </summary>
		private static Exception ReleaseOnAnotherThreadWhileDrawing(WebGpuOffscreenCapture capture, Graphics2DGpu graphics, Action release)
		{
			using var scanning = new ManualResetEventSlim();
			using var drawn = new ManualResetEventSlim();
			using var finished = new ManualResetEventSlim();
			var pipelines = capture.Context.Pipelines;
			pipelines.BindGroupScanStep = () =>
			{
				if (!scanning.IsSet)
				{
					scanning.Set();
					drawn.Wait(TimeSpan.FromSeconds(10));
				}
			};

			Exception failure = null;
			var releaser = new Thread(() =>
			{
				try
				{
					release();
				}
				catch (Exception exception)
				{
					failure = exception;
				}
				finally
				{
					finished.Set();
				}
			});

			try
			{
				releaser.Start();
				if (WaitHandle.WaitAny(new[] { scanning.WaitHandle, finished.WaitHandle }, TimeSpan.FromSeconds(10)) == 0)
				{
					var image = new ImageBuffer(8, 8);
					image.NewGraphics2D().Clear(Color.Blue);
					graphics.Render(image, 40, 20);
					graphics.FlushDeferredDraws();
					capture.Context.FlushPass();
					drawn.Set();
				}

				releaser.Join();
			}
			finally
			{
				pipelines.BindGroupScanStep = null;
			}

			return failure;
		}
	}
}
