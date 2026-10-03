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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Compat;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// What a frame whose present fails may cost: that frame, never the frames after it. The draw finishes
	/// cleanly but the recording it left is one wgpu rejects, so the throw comes out of the host's present
	/// (the layer's submit), after the draw and outside anything that guards the draw.
	/// </summary>
	/// <remarks>
	/// A present that threw used to skip the layer's "forget the target", so the next BeginFrame took its
	/// "already in a frame" exit: every later frame continued the failed one, GL.BeginFrame never reset
	/// the stacks, and a draw that left a push outstanding piled them up until the depth guard threw at the
	/// host's own first push. The companion draw-throws cases are in <see cref="PaintExceptionContainmentTests"/>.
	/// </remarks>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PresentFailureContainmentTests
	{
		[Test]
		[Timeout(60_000)]
		public async Task APresentThatFailsEveryFrameStillStartsEachNextFrameWhole(CancellationToken cancellationToken)
		{
			var reported = new List<Exception>();
			void CollectReport(Exception reportedException)
			{
				lock (reported)
				{
					reported.Add(reportedException);
				}
			}

			var window = new SystemWindow(300, 200) { BackgroundColor = Color.White };
			var saboteur = new InvalidRecordingWidget();
			window.AddChild(saboteur);

			UiThread.UnhandledException += CollectReport;
			var shown = Task.Run(() => window.ShowAsSystemWindow(), cancellationToken);

			try
			{
				await WaitFor(() => saboteur.SabotagedCount > 0, TimeSpan.FromSeconds(20), cancellationToken);
				if (saboteur.SabotagedCount == 0 && saboteur.NotAGpuFrame)
				{
					Skip.Test("This host draws on the CPU; there is no GPU recording to reject.");
				}

				int enoughToTripTheGuard = MatterHackers.RenderGl.OpenGl.GlStackBalance.MaxMatrixDepth + 8;
				await WaitFor(() => saboteur.SabotagedCount >= enoughToTripTheGuard, TimeSpan.FromSeconds(30), cancellationToken);

				await Assert.That(saboteur.SabotagedCount).IsGreaterThanOrEqualTo(enoughToTripTheGuard)
					.Because("the widget has to keep drawing whole frames past the depth of the matrix stack");

				List<Exception> seen;
				lock (reported)
				{
					seen = new List<Exception>(reported);
				}

				await Assert.That(seen.Count).IsGreaterThan(0)
					.Because("the rejected submit has to be reported, or the recording was never invalid and nothing has been proved");
				await Assert.That(seen.Where(ex => ex.Message.Contains("PushMatrix")).Count()).IsEqualTo(0)
					.Because("a 'PushMatrix without PopMatrix' means a frame after a failed present continued it instead of starting whole");

				saboteur.StopSabotage();
				await WaitFor(() => saboteur.CleanDrawCount > 0, TimeSpan.FromSeconds(20), cancellationToken);
				await Assert.That(saboteur.CleanDrawCount).IsGreaterThan(0);
			}
			finally
			{
				window.CloseOnIdle();
				var closed = await Task.WhenAny(shown, Task.Delay(TimeSpan.FromSeconds(20), cancellationToken));
				UiThread.UnhandledException -= CollectReport;

				if (!ReferenceEquals(closed, shown))
				{
					ThreadStackDump.WriteToConsole(
						"PresentFailureContainmentTests: the window did not close 20s after CloseOnIdle");
				}

				saboteur.ReleaseTextures();

				await Assert.That(ReferenceEquals(closed, shown)).IsTrue()
					.Because("the window has to close after frames whose present failed");
			}
		}

		/// <summary>Waits for a condition, polling off the UI thread; throws nothing, the assert does that.</summary>
		private static async Task WaitFor(Func<bool> condition, TimeSpan limit, CancellationToken cancellationToken)
		{
			var watch = Stopwatch.StartNew();
			while (!condition() && watch.Elapsed < limit)
			{
				await Task.Delay(10, cancellationToken);
			}
		}

		/// <summary>
		/// A widget that draws without throwing but leaves the frame a recording wgpu rejects at submit - an
		/// 8x8 copy out of a 4x4 texture, as in <c>NativeAbortProbe.SubmittingAnInvalidRecordingThrows</c> - and
		/// one model-view push it never pops, then asks for the next frame.
		/// </summary>
		private class InvalidRecordingWidget : GuiWidget
		{
			private int sabotagedCount;
			private int cleanDrawCount;
			private volatile bool sabotaging = true;
			private IRenderDevice texturesDevice;
			private IGpuTexture source;
			private IGpuTexture destination;

			public InvalidRecordingWidget()
			{
				this.HAnchor = HAnchor.Stretch;
				this.VAnchor = VAnchor.Stretch;
			}

			public int SabotagedCount => Volatile.Read(ref this.sabotagedCount);

			public int CleanDrawCount => Volatile.Read(ref this.cleanDrawCount);

			public bool NotAGpuFrame { get; private set; }

			public void StopSabotage() => this.sabotaging = false;

			public override void OnDraw(Graphics2D graphics2D)
			{
				base.OnDraw(graphics2D);

				if (!this.sabotaging)
				{
					Interlocked.Increment(ref this.cleanDrawCount);
					return;
				}

				UiThread.RunOnIdle(this.Invalidate);

				if (!(graphics2D is Graphics2DGpu gpu) || !(gpu.gl.GpuContext is GlCompatContext compat))
				{
					this.NotAGpuFrame = true;
					return;
				}

				IRenderDevice device = compat.Device;
				if (!ReferenceEquals(device, this.texturesDevice))
				{
					this.ReleaseTextures();
					this.texturesDevice = device;
					this.source = device.CreateTexture(new TextureDescriptor(4, 4, TextureFormat.Rgba8Unorm, TextureUsage.CopySrc | TextureUsage.TextureBinding, 1, 1, "presentFailureSource"));
					this.destination = device.CreateTexture(new TextureDescriptor(8, 8, TextureFormat.Rgba8Unorm, TextureUsage.CopyDst | TextureUsage.TextureBinding, 1, 1, "presentFailureDestination"));
				}

				// A copy cannot be recorded inside a render pass; the next draw opens a new one.
				compat.FlushPass();
				device.CopyTextureToTexture(this.source, this.destination, 0, 0, 8, 8);

				gpu.gl.MatrixMode(MatterHackers.RenderGl.OpenGl.MatrixMode.Modelview);
				gpu.gl.PushMatrix();

				Interlocked.Increment(ref this.sabotagedCount);
			}

			public void ReleaseTextures()
			{
				this.source?.Dispose();
				this.destination?.Dispose();
				this.source = null;
				this.destination = null;
			}
		}
	}
}
