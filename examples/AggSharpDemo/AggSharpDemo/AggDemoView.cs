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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo
{
	/// <summary>How <see cref="AggDemoView"/> gets a demo's frame onto the screen.</summary>
	public enum AggDemoRenderMode
	{
		/// <summary>The demo draws straight onto the window's graphics - the GPU renderer on a real window.</summary>
		Gpu,

		/// <summary>
		/// The demo draws into a demo-sized image through agg's software rasterizer, and that image is shown.
		/// This is the AGG reference: the same render the byte-exact tests hold to the C++ goldens.
		/// </summary>
		Software,
	}

	/// <summary>
	/// Shows one <see cref="AggDemo"/> at the largest whole-number scale that fits (see
	/// <see cref="AggDemoLayout"/>) and hands it mouse and keyboard input in its own demo pixels.
	/// </summary>
	public class AggDemoView : GuiWidget
	{
		private AggDemoRenderMode renderMode = AggDemoRenderMode.Gpu;

		/// <summary>The software reference frame, at the demo's native size. Null until first drawn.</summary>
		private ImageBuffer softwareFrame;

		/// <summary>
		/// <see cref="softwareFrame"/> blown up to the current scale by pixel replication - what is actually
		/// handed to the window. The replication is done here rather than by asking the renderer to scale,
		/// because the GPU renderer samples images with linear filtering and offers no nearest option, which
		/// would smear every demo pixel into its neighbours. Shown at scale 1 on a whole screen pixel, each
		/// texel lands on exactly one screen pixel, so the reference is shown unaltered. Kept and refilled in
		/// place, so a demo redrawing on every mouse move does not allocate a scale-squared image each time.
		/// </summary>
		private ImageBuffer presentedFrame;

		private bool softwareFrameStale = true;

		private bool presentedFrameStale = true;

		/// <summary>Where this view's origin was on the screen at the last draw; see <see cref="AggDemoLayout"/>.</summary>
		private Vector2 viewOriginOnScreen;

		/// <summary>Buttons pressed while inside the demo and not yet released. A press in the margin around
		/// the demo is not the demo's, and neither is its release.</summary>
		private AggInputFlags heldButtons;

		private bool idleQueued;

		public AggDemoView(AggDemo demo)
		{
			this.Demo = demo ?? throw new ArgumentNullException(nameof(demo));
			this.Demo.Invalidated += this.Demo_Invalidated;
		}

		public AggDemo Demo { get; }

		public AggDemoRenderMode RenderMode
		{
			get => this.renderMode;
			set
			{
				if (this.renderMode != value)
				{
					this.renderMode = value;
					this.softwareFrameStale = true;
					this.Invalidate();
				}
			}
		}

		/// <summary>Where the demo sits in this view, as of the last draw.</summary>
		public AggDemoLayout Layout => new AggDemoLayout(this.Width, this.Height, this.Demo.Width, this.Demo.Height, this.viewOriginOnScreen);

		public override void OnClosed(EventArgs e)
		{
			this.Demo.Invalidated -= this.Demo_Invalidated;
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			Affine viewToScreen = graphics2D.GetTransform();
			this.viewOriginOnScreen = new Vector2(viewToScreen.tx, viewToScreen.ty);
			AggDemoLayout layout = this.Layout;

			if (this.renderMode == AggDemoRenderMode.Software)
			{
				this.DrawSoftwareFrame(graphics2D, layout);
			}
			else
			{
				this.DrawDirect(graphics2D, layout, viewToScreen);
			}

			base.OnDraw(graphics2D);

			this.QueueIdleIfAnimating();
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			// Keys go to whichever widget has focus; clicking the demo is how a visitor gives it the keyboard.
			this.Focus();

			// A C++ example only ever sees a press inside its own window.
			AggDemoLayout layout = this.Layout;
			Point2D pixel = this.DemoPixel(layout, mouseEvent.Position);
			if (layout.ContainsDemoPixel(pixel))
			{
				AggInputFlags button = ButtonFlag(mouseEvent.Button);
				this.heldButtons |= button;
				this.Demo.OnMouseDown(pixel.x, pixel.y, button, this.heldButtons | ModifierFlags());
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			AggDemoLayout layout = this.Layout;
			Point2D pixel = this.DemoPixel(layout, mouseEvent.Position);

			// A drag that began inside keeps reporting after it leaves, as a captured C++ drag does; a hover
			// reports only over the demo.
			if (this.heldButtons != AggInputFlags.None || layout.ContainsDemoPixel(pixel))
			{
				this.Demo.OnMouseMove(pixel.x, pixel.y, this.heldButtons | ModifierFlags());
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			base.OnMouseUp(mouseEvent);

			AggInputFlags button = ButtonFlag(mouseEvent.Button);
			if ((this.heldButtons & button) != AggInputFlags.None)
			{
				this.heldButtons &= ~button;
				Point2D pixel = this.DemoPixel(this.Layout, mouseEvent.Position);

				this.Demo.OnMouseUp(pixel.x, pixel.y, button, this.heldButtons | ModifierFlags());
			}
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			base.OnKeyDown(keyEvent);

			AggInputFlags flags = this.heldButtons;
			flags |= keyEvent.Shift ? AggInputFlags.Shift : AggInputFlags.None;
			flags |= keyEvent.Control ? AggInputFlags.Ctrl : AggInputFlags.None;
			this.Demo.OnKeyDown(keyEvent.KeyCode, flags);
		}

		/// <summary>
		/// The whole demo pixel under a view point, in the demo's own coordinates: <see cref="AggDemoLayout.ViewToDemoPixel"/>,
		/// then for a demo that <see cref="AggDemo.DrawsYDown"/> the row counted from the top, as C++
		/// <c>flip_y = false</c> reports it. Row r from the bottom is row Height - 1 - r from the top, so the
		/// demo's pixels map onto themselves and <see cref="AggDemoLayout.ContainsDemoPixel"/> still holds.
		/// </summary>
		private Point2D DemoPixel(AggDemoLayout layout, Vector2 viewPosition)
		{
			Point2D pixel = layout.ViewToDemoPixel(viewPosition);
			if (this.Demo.DrawsYDown)
			{
				pixel.y = this.Demo.Height - 1 - pixel.y;
			}

			return pixel;
		}

		private static AggInputFlags ButtonFlag(MouseButtons button)
		{
			return button switch
			{
				MouseButtons.Left => AggInputFlags.MouseLeft,
				MouseButtons.Right => AggInputFlags.MouseRight,
				_ => AggInputFlags.None,
			};
		}

		/// <summary>Mouse events carry no modifiers, so they are read from the keyboard's down state.</summary>
		private static AggInputFlags ModifierFlags()
		{
			AggInputFlags flags = AggInputFlags.None;
			flags |= Keyboard.IsKeyDown(Keys.Shift) ? AggInputFlags.Shift : AggInputFlags.None;
			flags |= Keyboard.IsKeyDown(Keys.Control) ? AggInputFlags.Ctrl : AggInputFlags.None;
			return flags;
		}

		/// <summary>
		/// The GPU mode: the demo draws on this view's own graphics under <see cref="AggDemoLayout.DemoToView"/>,
		/// clipped to its frame so a demo that draws past its edges does not paint over the page.
		/// </summary>
		private void DrawDirect(Graphics2D graphics2D, AggDemoLayout layout, Affine viewToScreen)
		{
			RectangleDouble oldClip = graphics2D.GetClippingRect();

			// The clip rect is in screen space, the transform's destination, not in view coordinates. Rounded
			// outward as GuiWidget rounds its own clips, so a scissor never drops a column the demo covers.
			RectangleDouble demoClip = layout.DemoBoundsInView;
			viewToScreen.Transform(ref demoClip.Left, ref demoClip.Bottom);
			viewToScreen.Transform(ref demoClip.Right, ref demoClip.Top);
			demoClip.Left = Math.Floor(demoClip.Left);
			demoClip.Bottom = Math.Floor(demoClip.Bottom);
			demoClip.Right = Math.Ceiling(demoClip.Right);
			demoClip.Top = Math.Ceiling(demoClip.Top);

			// Nothing visible means nothing to draw - and must mean it: the emptied rect IntersectWithRectangle
			// leaves behind is inverted, and ImageGraphics2D would normalize that into the gap between the two
			// rects and let the demo paint over its neighbours.
			if (!demoClip.IntersectWithRectangle(oldClip))
			{
				return;
			}

			graphics2D.PushTransform();
			try
			{
				graphics2D.SetClippingRect(demoClip);
				graphics2D.SetTransform(YDownToYUp(this.Demo) * layout.DemoToView * viewToScreen);
				this.Demo.Draw(graphics2D);
			}
			finally
			{
				graphics2D.PopTransform();
				graphics2D.SetClippingRect(oldClip);
			}
		}

		/// <summary>For a demo that <see cref="AggDemo.DrawsYDown"/>, y -> Height - y; otherwise identity.</summary>
		private static Affine YDownToYUp(AggDemo demo)
		{
			return demo.DrawsYDown ? Affine.NewScaling(1, -1) * Affine.NewTranslation(0, demo.Height) : Affine.NewIdentity();
		}

		/// <summary>A demo-sized image for the software reference render (32-bit BGRA, starting transparent).</summary>
		public static ImageBuffer NewReferenceFrame(AggDemo demo)
		{
			return new ImageBuffer(demo.Width, demo.Height);
		}

		/// <summary>
		/// The AGG reference render: <paramref name="demo"/> drawn through the software rasterizer into
		/// <paramref name="frame"/>. What the software mode shows, and what the byte-exact tests compare to C++.
		/// A demo that <see cref="AggDemo.DrawsYDown"/> draws untransformed, so its frame is y down (row y is
		/// C++'s row y) and the software mode shows it flipped.
		/// </summary>
		/// <remarks>
		/// The rasterizer gets no vector clip box, as C++ platform_support's does not: an edge clipped at the
		/// canvas is a different line to the rasterizer, a level off along its whole length. Pixels outside the
		/// frame are still dropped by the renderer, and a demo may still set its own clip. The surface stays
		/// unclipped only until something saves and restores the clip (GuiWidget.DrawChild and DrawDirect restore
		/// a canvas-sized box), so a demo must not draw GuiWidget children through DrawChild in the reference frame.
		/// </remarks>
		public static void DrawReferenceFrame(AggDemo demo, ImageBuffer frame)
		{
			Graphics2D graphics = frame.NewGraphics2D();
			graphics.Rasterizer.reset_clipping();
			demo.Draw(graphics);
			graphics.FlushDeferredDraws();
			frame.MarkImageChanged();
		}

		/// <summary>
		/// The software mode: re-render the reference frame only when the demo said it changed, re-scale it
		/// only when it or the scale changed, and show it pixel for pixel.
		/// </summary>
		private void DrawSoftwareFrame(Graphics2D graphics2D, AggDemoLayout layout)
		{
			if (this.softwareFrame == null
				|| this.softwareFrame.Width != this.Demo.Width
				|| this.softwareFrame.Height != this.Demo.Height)
			{
				this.softwareFrame = NewReferenceFrame(this.Demo);
				this.softwareFrameStale = true;
			}

			if (this.softwareFrameStale)
			{
				DrawReferenceFrame(this.Demo, this.softwareFrame);
				this.softwareFrameStale = false;
				this.presentedFrameStale = true;
			}

			// A y-down frame is shown flipped, so it always goes through the presented copy.
			if (layout.Scale == 1 && !this.Demo.DrawsYDown)
			{
				graphics2D.Render(this.softwareFrame, layout.Offset.X, layout.Offset.Y);
				return;
			}

			int presentedWidth = this.Demo.Width * layout.Scale;
			int presentedHeight = this.Demo.Height * layout.Scale;
			if (this.presentedFrame == null || this.presentedFrame.Width != presentedWidth || this.presentedFrame.Height != presentedHeight)
			{
				this.presentedFrame = new ImageBuffer(presentedWidth, presentedHeight, this.softwareFrame.BitDepth);
				this.presentedFrameStale = true;
			}

			if (this.presentedFrameStale)
			{
				ReplicatePixels(this.softwareFrame, this.presentedFrame, layout.Scale, this.Demo.DrawsYDown);
				this.presentedFrame.MarkImageChanged();
				this.presentedFrameStale = false;
			}

			graphics2D.Render(this.presentedFrame, layout.Offset.X, layout.Offset.Y);
		}

		/// <summary>
		/// Nearest-neighbour upscale by a whole number into an existing image of exactly scale times the size:
		/// each source row is widened once, then copied into its scale destination rows - in reverse row order
		/// when <paramref name="flipRows"/>, which turns a y-down frame the right way up without resampling.
		/// </summary>
		private static void ReplicatePixels(ImageBuffer source, ImageBuffer destination, int scale, bool flipRows)
		{
			int bytesPerPixel = source.BitDepth / 8;
			int rowBytes = destination.Width * bytesPerPixel;
			byte[] sourceBytes = source.GetBuffer();
			byte[] destinationBytes = destination.GetBuffer();
			var widenedRow = new byte[rowBytes];

			for (int sourceY = 0; sourceY < source.Height; sourceY++)
			{
				int sourceRow = source.GetBufferOffsetY(sourceY);
				for (int x = 0; x < destination.Width; x++)
				{
					Buffer.BlockCopy(sourceBytes, sourceRow + (x / scale) * bytesPerPixel, widenedRow, x * bytesPerPixel, bytesPerPixel);
				}

				for (int copy = 0; copy < scale; copy++)
				{
					int destinationY = ((flipRows ? source.Height - 1 - sourceY : sourceY) * scale) + copy;
					Buffer.BlockCopy(widenedRow, 0, destinationBytes, destination.GetBufferOffsetY(destinationY), rowBytes);
				}
			}
		}

		/// <summary>
		/// Keeps C++ <c>wait_mode(false)</c> ticking: after each frame, one <see cref="AggDemo.OnIdle"/> on the
		/// next idle and a redraw, which queues the next. Stops by itself when the demo goes back to wait mode
		/// or the view closes.
		/// </summary>
		private void QueueIdleIfAnimating()
		{
			if (this.Demo.WaitMode || this.idleQueued)
			{
				return;
			}

			this.idleQueued = true;
			UiThread.RunOnIdle(() =>
			{
				this.idleQueued = false;
				if (!this.HasBeenClosed && !this.Demo.WaitMode)
				{
					this.Demo.OnIdle();
					this.Invalidate();
				}
			});
		}

		private void Demo_Invalidated(object sender, EventArgs e)
		{
			this.softwareFrameStale = true;
			this.Invalidate();
		}
	}
}
