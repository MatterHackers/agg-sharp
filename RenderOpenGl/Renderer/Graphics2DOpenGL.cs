/*
Copyright (c) 2018, Lars Brubaker
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

//#define AA_TIPS

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using Tesselate;
using MatterHackers.Agg.Platform;
using System.Collections.Generic;

namespace MatterHackers.RenderOpenGl
{
	public class Graphics2DOpenGL : Graphics2D
	{
		// We can have a single static instance because all gl rendering is required to happen on the ui thread so there can
		// be no runtime contention for this object (no thread contention).
		private static AARenderToGLTesselator triangleEddgeInfo = new AARenderToGLTesselator();

		/// <summary>
		/// A texture per alpha value
		/// </summary>
		private static List<ImageBuffer> AATextureImages = null;

		public bool DoEdgeAntiAliasing = true;
		private static RenderToGLTesselator renderNowTesselator = new RenderToGLTesselator();

		private int width;
		private int height;
		private RectangleDouble cachedClipRect;

		public Graphics2DOpenGL()
		{
		}

		public Graphics2DOpenGL(int width, int height)
		{
			this.width = width;
			this.height = height;
			cachedClipRect = new RectangleDouble(0, 0, width, height);
		}

		public override RectangleDouble GetClippingRect()
		{
			return cachedClipRect;
		}

		public override void SetClippingRect(RectangleDouble clippingRect)
		{
			cachedClipRect = clippingRect;
			GL.Scissor((int)Math.Floor(Math.Max(clippingRect.Left, 0)), (int)Math.Floor(Math.Max(clippingRect.Bottom, 0)),
				(int)Math.Ceiling(Math.Max(clippingRect.Width, 0)), (int)Math.Ceiling(Math.Max(clippingRect.Height, 0)));
			GL.Enable(EnableCap.ScissorTest);
		}

		public override IScanlineCache ScanlineCache
		{
			get { return null; }
			set { throw new Exception("There is no scanline cache on a GL surface."); }
		}

		public override int Width => width;

		public override int Height => height;

		public void PushOrthoProjection()
		{
			GL.Disable(EnableCap.CullFace);

			GL.MatrixMode(MatrixMode.Projection);
			GL.PushMatrix();
			GL.LoadIdentity();
			GL.Ortho(0, width, 0, height, 0, 1);

			GL.MatrixMode(MatrixMode.Modelview);
			GL.PushMatrix();
			GL.LoadIdentity();
		}

		public void PopOrthoProjection()
		{
			GL.MatrixMode(MatrixMode.Projection);
			GL.PopMatrix();
			GL.MatrixMode(MatrixMode.Modelview);
			GL.PopMatrix();
		}

		private void CheckLineImageCache()
		{
			if (AATextureImages == null)
			{
				AATextureImages = new List<ImageBuffer>();
				for (int i = 0; i < 256; i++)
				{
					var texture = new ImageBuffer(1024, 4);
					AATextureImages.Add(texture);
					byte[] hardwarePixelBuffer = texture.GetBuffer();
					for (int y = 0; y < 4; y++)
					{
						byte alpha = 0;
						for (int x = 0; x < 1024; x++)
						{
							hardwarePixelBuffer[(y * 1024 + x) * 4 + 0] = 255;
							hardwarePixelBuffer[(y * 1024 + x) * 4 + 1] = 255;
							hardwarePixelBuffer[(y * 1024 + x) * 4 + 2] = 255;
							hardwarePixelBuffer[(y * 1024 + x) * 4 + 3] = alpha;
							alpha = (byte)i;
						}
					}
				}
			}
		}

		public void DrawAAShape(IVertexSource vertexSource, IColorType colorIn)
		{
			vertexSource.rewind(0);

			Affine transform = GetTransform();
			if (!transform.is_identity())
			{
				vertexSource = new VertexSourceApplyTransform(vertexSource, transform);
			}

			Color colorBytes = colorIn.ToColor();
			// the alpha has come from the bound texture
			GL.Color4(colorBytes.red, colorBytes.green, colorBytes.blue, (byte)255);

			triangleEddgeInfo.Clear();
			VertexSourceToTesselator.SendShapeToTesselator(triangleEddgeInfo, vertexSource);

			// now render it
			triangleEddgeInfo.RenderLastToGL();
		}

		public void DrawAALine(Vector2 start, Vector2 end, double halfWidth, IColorType colorIn)
		{
			Color colorBytes = colorIn.ToColor();
			GL.Color4(colorBytes.red, colorBytes.green, colorBytes.blue, colorBytes.alpha);

			Affine transform = GetTransform();
			if (!transform.is_identity())
			{
				transform.transform(ref start);
				transform.transform(ref end);
			}

			GL.Begin(BeginMode.Triangles);
			Vector2 widthRightOffset = (end - start).GetPerpendicularRight().GetNormal() * halfWidth/2;
			triangleEddgeInfo.Draw2EdgeTriangle(start - widthRightOffset, end - widthRightOffset, end + widthRightOffset);
			triangleEddgeInfo.Draw2EdgeTriangle(end + widthRightOffset, start + widthRightOffset, start - widthRightOffset);
			GL.End();
		}

		public void DrawAALineRounded(Vector2 start, Vector2 end, double halfWidth, IColorType colorIn)
		{
			Color colorBytes = colorIn.ToColor();
			GL.Color4(colorBytes.red, colorBytes.green, colorBytes.blue, colorBytes.alpha);

			Affine transform = GetTransform();
			if (!transform.is_identity())
			{
				transform.transform(ref start);
				transform.transform(ref end);
			}

			//GL.Begin(BeginMode.Triangles);
			Vector2 widthRightOffset = (end - start).GetPerpendicularRight().GetNormal() * halfWidth / 2;
			// draw the main line part
			triangleEddgeInfo.Draw1EdgeTriangle(start - widthRightOffset, end - widthRightOffset, end + widthRightOffset);
			triangleEddgeInfo.Draw1EdgeTriangle(end + widthRightOffset, start + widthRightOffset, start - widthRightOffset);
			// now draw the end rounds
			int numSegments = 5;
			Vector2 endCurveStart = end + widthRightOffset;
			Vector2 startCurveStart = start + widthRightOffset;
			for (int i = 0; i < numSegments+1; i++)
			{
				Vector2 endCurveEnd = end + Vector2.Rotate(widthRightOffset, i * Math.PI / numSegments);
				triangleEddgeInfo.Draw1EdgeTriangle(endCurveStart, endCurveEnd, end);
				endCurveStart = endCurveEnd;

				Vector2 startCurveEnd = start + Vector2.Rotate(widthRightOffset, -i * Math.PI / numSegments);
				triangleEddgeInfo.Draw1EdgeTriangle(startCurveStart, startCurveEnd, start);
				startCurveStart = startCurveEnd;
			}
			//GL.End();
		}

		public void DrawAACircle(Vector2 start, double radius, IColorType colorIn)
		{
			Color colorBytes = colorIn.ToColor();
			GL.Color4(colorBytes.red, colorBytes.green, colorBytes.blue, colorBytes.alpha);

			Affine transform = GetTransform();
			if (!transform.is_identity())
			{
				transform.transform(ref start);
			}

			// now draw the end rounds
			int numSegments = 12;
			double anglePerSegment = MathHelper.Tau / numSegments;
			Vector2 currentOffset = new Vector2(0, radius);
			Vector2 curveStart = start + currentOffset;
			for (int i = 0; i < numSegments; i++)
			{
				currentOffset.Rotate(anglePerSegment);
				Vector2 curveEnd = start + currentOffset;

				triangleEddgeInfo.Draw1EdgeTriangle(curveStart, curveEnd, start);
				curveStart = curveEnd;
			}
		}

		public void PreRender(IColorType colorIn)
		{
			CheckLineImageCache();
			PushOrthoProjection();

			GL.Enable(EnableCap.Texture2D);
			GL.BindTexture(TextureTarget.Texture2D, RenderOpenGl.ImageGlPlugin.GetImageGlPlugin(AATextureImages[colorIn.Alpha0To255], false).GLTextureHandle);

			// the source is always all white so has no does not have its color changed by the alpha
			GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Enable(EnableCap.Blend);
		}

		public override void Render(IVertexSource vertexSource, IColorType colorIn)
		{
			PreRender(colorIn);

			if (DoEdgeAntiAliasing)
			{
				DrawAAShape(vertexSource, colorIn);
			}
			else
			{
				vertexSource.rewind(0);

				Affine transform = GetTransform();
				if (!transform.is_identity())
				{
					vertexSource = new VertexSourceApplyTransform(vertexSource, transform);
				}

				Color colorBytes = colorIn.ToColor();
				GL.Color4(colorBytes.red, colorBytes.green, colorBytes.blue, colorBytes.alpha);

				renderNowTesselator.Clear();
                VertexSourceToTesselator.SendShapeToTesselator(renderNowTesselator, vertexSource);
			}

			PopOrthoProjection();
		}

		public override void Render(IImageByte source,
			double x, double y,
			double angleRadians,
			double scaleX, double scaleY)
		{
			Affine transform = GetTransform();
			if (!transform.is_identity())
			{
				// TODO: <BUG> make this do rotation and scaling
				transform.transform(ref x, ref y);
				scaleX *= transform.sx;
				scaleY *= transform.sy;
			}

			// TODO: <BUG> make this do rotation and scaling
			RectangleInt sourceBounds = source.GetBounds();
			sourceBounds.Offset((int)x, (int)y);
			RectangleInt destBounds = new RectangleInt((int)cachedClipRect.Left, (int)cachedClipRect.Bottom, (int)cachedClipRect.Right, (int)cachedClipRect.Top);

			if (!RectangleInt.DoIntersect(sourceBounds, destBounds))
			{
				if (scaleX != 1 || scaleY != 1)// || angleDegrees != 0)
				{
					//throw new NotImplementedException();
				}
				//return;
			}

			ImageBuffer sourceAsImageBuffer = (ImageBuffer)source;
			//ImageIO.SaveImageData($"c:\\temp\\gah-{DateTime.Now.Ticks}.png", sourceAsImageBuffer);

			ImageGlPlugin glPlugin = ImageGlPlugin.GetImageGlPlugin(sourceAsImageBuffer, false);

			// Prepare openGL for rendering
			PushOrthoProjection();
			GL.Disable(EnableCap.Lighting);
			GL.Enable(EnableCap.Texture2D);
			GL.Disable(EnableCap.DepthTest);

			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			GL.Translate(x, y, 0);
			GL.Rotate(MathHelper.RadiansToDegrees(angleRadians), 0, 0, 1);
			GL.Scale(scaleX, scaleY, 1);

			Color color = Color.White;
			GL.Color4((byte)color.Red0To255, (byte)color.Green0To255, (byte)color.Blue0To255, (byte)color.Alpha0To255);

			glPlugin.DrawToGL();

			//Restore openGL state
			PopOrthoProjection();
		}

		public override void Render(IImageFloat imageSource,
			double x, double y,
			double angleDegrees,
			double scaleX, double ScaleY)
		{
			throw new NotImplementedException();
		}

		public override void Rectangle(double left, double bottom, double right, double top, Color color, double strokeWidth)
		{
#if true
			// This only works for translation. If we have a rotation or scale in the transform this will have some problems.
			Affine transform = GetTransform();
			double fastLeft = left;
			double fastBottom = bottom;
			double fastRight = right;
			double fastTop = top;

			transform.transform(ref fastLeft, ref fastBottom);
			transform.transform(ref fastRight, ref fastTop);

			if (fastLeft == (int)fastLeft
				&& fastBottom == (int)fastBottom
				&& fastRight == (int)fastRight
				&& fastTop == (int)fastTop)
			{
				// FillRectangle will do the translation so use the original variables
				FillRectangle(left, bottom, right, bottom + 1, color);
				FillRectangle(left, top, right, top - 1, color);

				FillRectangle(left, bottom, left + 1, top, color);
				FillRectangle(right - 1, bottom, right, top, color);
			}
			else
#endif
			{
				RoundedRect rect = new RoundedRect(left + .5, bottom + .5, right - .5, top - .5, 0);
				Stroke rectOutline = new Stroke(rect, strokeWidth);

				Render(rectOutline, color);
			}
		}

		public override void FillRectangle(double left, double bottom, double right, double top, IColorType fillColor)
		{
			// This only works for translation. If we have a rotation or scale in the transform this will have some problems.
			Affine transform = GetTransform();
			double fastLeft = left;
			double fastBottom = bottom;
			double fastRight = right;
			double fastTop = top;

			transform.transform(ref fastLeft, ref fastBottom);
			transform.transform(ref fastRight, ref fastTop);

			if (Math.Abs(fastLeft - (int)fastLeft) < .01
				&& Math.Abs(fastBottom - (int)fastBottom) < .01
				&& Math.Abs(fastRight - (int)fastRight) < .01
				&& Math.Abs(fastTop - (int)fastTop) < .01)
			{
				PushOrthoProjection();

				GL.Disable(EnableCap.Texture2D);
				GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
				GL.Enable(EnableCap.Blend);

				GL.Color4(fillColor.Red0To255, fillColor.Green0To255, fillColor.Blue0To255, fillColor.Alpha0To255);

				GL.Begin(BeginMode.Triangles);
				// triangle 1
				{
					GL.Vertex2(fastLeft, fastBottom);
					GL.Vertex2(fastRight, fastBottom);
					GL.Vertex2(fastRight, fastTop);
				}

				// triangle 2
				{
					GL.Vertex2(fastLeft, fastBottom);
					GL.Vertex2(fastRight, fastTop);
					GL.Vertex2(fastLeft, fastTop);
				}
				GL.End();

				PopOrthoProjection();
			}
			else
			{
				RoundedRect rect = new RoundedRect(left, bottom, right, top, 0);
				Render(rect, fillColor.ToColor());
			}
		}

		public override void Line(double x1, double y1, double x2, double y2, Color color, double strokeWidth = 1)
		{
			RectangleDouble strokeBounds;
			if (x1 == x2) // vertical line
			{
				strokeBounds = new RectangleDouble(x1 - strokeWidth / 2, y1, x1 + strokeWidth / 2, y2);
			}
			else // horizontal line
			{
				strokeBounds = new RectangleDouble(x1, y1 - strokeWidth / 2, x2, y1 + strokeWidth / 2);
			}

			// lets check for horizontal and vertical lines that are pixel aligned and render them as fills
			bool canUseFill = (x1 == x2 || y1 == y2) // we are vertical or horizontal
				&& Math.Abs(strokeBounds.Left - (int)strokeBounds.Left) < .01
				&& Math.Abs(strokeBounds.Right - (int)strokeBounds.Right) < .01
				&& Math.Abs(strokeBounds.Bottom - (int)strokeBounds.Bottom) < .01
				&& Math.Abs(strokeBounds.Top - (int)strokeBounds.Top) < .01;

			if (canUseFill)
			{
				// Draw as optimized vertical or horizontal line
				FillRectangle(strokeBounds, color);
			}
			else
			{
				// Draw as a VertexSource - may yield incorrect lines in some cases
				base.Line(x1, y1, x2, y2, color, strokeWidth);
			}
		}

		public override void Clear(IColorType color)
		{
			Affine transform = GetTransform();

			RoundedRect clearRect = new RoundedRect(new RectangleDouble(
				cachedClipRect.Left - transform.tx, cachedClipRect.Bottom - transform.ty,
				cachedClipRect.Right - transform.tx, cachedClipRect.Top - transform.ty), 0);
			Render(clearRect, color.ToColor());
		}

		public void RenderTransformedPath(Matrix4X4 transform, IVertexSource path, Color color, bool doDepthTest)
		{
			CheckLineImageCache();
			GL.Enable(EnableCap.Texture2D);
			GL.BindTexture(TextureTarget.Texture2D, RenderOpenGl.ImageGlPlugin.GetImageGlPlugin(AATextureImages[color.Alpha0To255], false).GLTextureHandle);

			// the source is always all white so has no does not have its color changed by the alpha
			GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Enable(EnableCap.Blend);

			GL.Disable(EnableCap.CullFace);

			GL.MatrixMode(MatrixMode.Modelview);
			GL.PushMatrix();
			GL.MultMatrix(transform.GetAsFloatArray());
			GL.Disable(EnableCap.Lighting);
			if (doDepthTest)
			{
				GL.Enable(EnableCap.DepthTest);
			}
			else
			{
				GL.Disable(EnableCap.DepthTest);
			}

			this.DrawAAShape(path, color);

			GL.PopMatrix();
		}
	}
}