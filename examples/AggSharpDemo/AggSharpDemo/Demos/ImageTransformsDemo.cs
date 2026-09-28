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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's image_transforms.cpp: a star filled with the spheres image through a bilinear image filter
	/// (span_image_filter_rgba_bilinear_clip), under one of seven ways of combining the star's and the image's
	/// own rotation, scale and center. Drag the star, or the image center's small circle; turn either on idle.
	/// </summary>
	public class ImageTransformsDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly ImageBuffer sourceImage = SpheresImage.Load();

		// What the left button holds: 0 nothing, 1 the image center, 2 the star.
		private int dragFlag;

		private double dragDx;

		private double dragDy;

		public ImageTransformsDemo()
		{
			// on_init: the window opens at the image's size, and both centers start in its middle.
			this.ImageCenterX = this.PolygonCenterX = this.Width / 2.0;
			this.ImageCenterY = this.PolygonCenterY = this.Height / 2.0;

			// image_transforms.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.PolygonAngleSlider = new SliderCtrl(5, 5, 145, 11, false) { Label = "Polygon Angle={0:F2}" };
			this.PolygonAngleSlider.SetRange(-180.0, 180.0);
			this.PolygonAngleSlider.Value = 0.0;
			this.PolygonScaleSlider = new SliderCtrl(5, 5 + 14, 145, 12 + 14, false) { Label = "Polygon Scale={0:F2}" };
			this.PolygonScaleSlider.SetRange(0.1, 5.0);
			this.PolygonScaleSlider.Value = 1.0;
			this.ImageAngleSlider = new SliderCtrl(155, 5, 300, 12, false) { Label = "Image Angle={0:F2}" };
			this.ImageAngleSlider.SetRange(-180.0, 180.0);
			this.ImageAngleSlider.Value = 0.0;
			this.ImageScaleSlider = new SliderCtrl(155, 5 + 14, 300, 12 + 14, false) { Label = "Image Scale={0:F2}" };
			this.ImageScaleSlider.SetRange(0.1, 5.0);
			this.ImageScaleSlider.Value = 1.0;
			this.RotatePolygonCbox = new CboxCtrl(5, 5 + 14 + 14, "Rotate Polygon", false);
			this.RotateImageCbox = new CboxCtrl(5, 5 + 14 + 14 + 14, "Rotate Image", false);

			// The example gives its rbox a degenerate box at (-3, 56), so little but the items shows.
			this.ExampleRbox = new RboxCtrl(-3.0, 14 + 14 + 14 + 14, -3.0, 14 + 14 + 14 + 14, false);
			for (int i = 0; i <= 6; i++)
			{
				this.ExampleRbox.AddItem(i.ToString());
			}

			this.ExampleRbox.CurrentItem = 0;

			this.ctrls.Add(this.PolygonAngleSlider);
			this.ctrls.Add(this.PolygonScaleSlider);
			this.ctrls.Add(this.ImageAngleSlider);
			this.ctrls.Add(this.ImageScaleSlider);
			this.ctrls.Add(this.RotatePolygonCbox);
			this.ctrls.Add(this.RotateImageCbox);
			this.ctrls.Add(this.ExampleRbox);
			this.ctrls.Changed += (s, e) =>
			{
				// on_ctrl_change: animate while either rotation is on.
				this.WaitMode = !(this.RotatePolygonCbox.Checked || this.RotateImageCbox.Checked);
				this.Invalidate();
			};
		}

		/// <summary>C++ <c>m_polygon_angle</c>, in degrees.</summary>
		public SliderCtrl PolygonAngleSlider { get; }

		/// <summary>C++ <c>m_polygon_scale</c>.</summary>
		public SliderCtrl PolygonScaleSlider { get; }

		/// <summary>C++ <c>m_image_angle</c>, in degrees.</summary>
		public SliderCtrl ImageAngleSlider { get; }

		/// <summary>C++ <c>m_image_scale</c>.</summary>
		public SliderCtrl ImageScaleSlider { get; }

		/// <summary>C++ <c>m_rotate_polygon</c>: turn the polygon half a degree each idle tick.</summary>
		public CboxCtrl RotatePolygonCbox { get; }

		/// <summary>C++ <c>m_rotate_image</c>: turn the image half a degree each idle tick.</summary>
		public CboxCtrl RotateImageCbox { get; }

		/// <summary>C++ <c>m_example</c>: which of the seven image matrices (0 is the identity).</summary>
		public RboxCtrl ExampleRbox { get; }

		/// <summary>C++ <c>m_image_cx</c>: the image center, the small circle that can be dragged.</summary>
		public double ImageCenterX { get; set; }

		/// <summary>C++ <c>m_image_cy</c>.</summary>
		public double ImageCenterY { get; set; }

		/// <summary>C++ <c>m_polygon_cx</c>: the star's center.</summary>
		public double PolygonCenterX { get; set; }

		/// <summary>C++ <c>m_polygon_cy</c>.</summary>
		public double PolygonCenterY { get; set; }

		public override string Name => "image_transforms";

		public override string Category => "Images";

		public override string Description => "A star filled with an image through a bilinear filter, under seven ways of combining the star's and the image's rotation, scale and center.";

		// The example opens its window at the spheres image's size.
		public override int Width => 320;

		public override int Height => 300;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);

			Affine imageMatrix = this.ImageMatrix();
			Affine transform = graphics.GetTransform();
			var star = new VertexSourceApplyTransform(this.CreateStar(), this.PolygonMatrix());
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			if (rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				// The software reference: the star's spans generated straight from the image, as C++ does. A span
				// generator has no Graphics2D call, so the interpolator maps a frame pixel back through the graphics
				// transform into demo space itself.
				Affine frameToImage = imageMatrix;
				if (!transform.is_identity())
				{
					frameToImage = new Affine(transform);
					frameToImage.invert();
					frameToImage *= imageMatrix;
				}

				var interpolator = new span_interpolator_linear(frameToImage);
				var accessor = new ImageBufferAccessorClip(this.sourceImage, Color.White);
				var spanGenerator = new span_image_filter_rgba_bilinear_clip(accessor, Color.White, interpolator);
				rasterizer.reset();
				rasterizer.add_path(new VertexSourceApplyTransform(star, transform));
				new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), spanGenerator);
				destination.MarkImageChanged();
			}
			else
			{
				// A GPU surface has no span generator: it draws the image through the same matrix, not clipped to
				// the star, and outlines the star. (Image fills clipped to a path are planned GPU work; see
				// docs/demo-site-plan.md.)
				var imageToDemo = new Affine(imageMatrix);
				imageToDemo.invert();
				double angle = Math.Atan2(imageToDemo.shy, imageToDemo.sx);
				double scale = Math.Sqrt((imageToDemo.sx * imageToDemo.sx) + (imageToDemo.shy * imageToDemo.shy));
				graphics.Render(this.sourceImage, imageToDemo.tx, imageToDemo.ty, angle, scale, scale);
				graphics.Render(new Stroke(star), Color.Black);
			}

			var outer = new Ellipse(this.ImageCenterX, this.ImageCenterY, 5, 5, 20);
			var inner = new Ellipse(this.ImageCenterX, this.ImageCenterY, 2, 2, 20);
			graphics.Render(outer, Rgba8.FromRgba(0.7, 0.8, 0.0));
			graphics.Render(new Stroke(outer), Rgba8.FromRgba(0.0, 0.0, 0.0));
			graphics.Render(inner, Rgba8.FromRgba(0.0, 0.0, 0.0));

			this.ctrls.Render(graphics);
		}

		public override void OnIdle()
		{
			bool redraw = false;
			if (this.RotatePolygonCbox.Checked)
			{
				this.PolygonAngleSlider.Value = TurnHalfDegree(this.PolygonAngleSlider.Value);
				redraw = true;
			}

			if (this.RotateImageCbox.Checked)
			{
				this.ImageAngleSlider.Value = TurnHalfDegree(this.ImageAngleSlider.Value);
				redraw = true;
			}

			if (redraw)
			{
				this.Invalidate();
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			double dx = x - this.ImageCenterX;
			double dy = y - this.ImageCenterY;
			if (Math.Sqrt((dx * dx) + (dy * dy)) < 5.0)
			{
				this.dragDx = dx;
				this.dragDy = dy;
				this.dragFlag = 1;
			}
			else if (InsidePolygon(new VertexSourceApplyTransform(this.CreateStar(), this.PolygonMatrix()), x, y))
			{
				this.dragDx = x - this.PolygonCenterX;
				this.dragDy = y - this.PolygonCenterY;
				this.dragFlag = 2;
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (!flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.dragFlag = 0;
				return;
			}

			if (this.dragFlag == 1)
			{
				this.ImageCenterX = x - this.dragDx;
				this.ImageCenterY = y - this.dragDy;
				this.Invalidate();
			}
			else if (this.dragFlag == 2)
			{
				this.PolygonCenterX = x - this.dragDx;
				this.PolygonCenterY = y - this.dragDy;
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			this.dragFlag = 0;
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		private static double TurnHalfDegree(double angle)
		{
			angle += 0.5;
			return angle >= 180.0 ? angle - 360.0 : angle;
		}

		// A crossing test on the outline in place of C++'s rasterizer hit_test (private in agg-sharp): the star
		// is a simple polygon, so both give the same answer.
		private static bool InsidePolygon(IVertexSource path, double x, double y)
		{
			var points = new List<Vector2>();
			foreach (var vertex in path.Vertices())
			{
				if (vertex.IsMoveTo || vertex.IsLineTo)
				{
					points.Add(vertex.Position);
				}
			}

			bool inside = false;
			for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
			{
				Vector2 a = points[i];
				Vector2 b = points[j];
				if ((a.Y > y) != (b.Y > y) && x < ((b.X - a.X) * (y - a.Y) / (b.Y - a.Y)) + a.X)
				{
					inside = !inside;
				}
			}

			return inside;
		}

		// create_star: a 14-point star around the polygon center, sized from the window.
		private VertexStorage CreateStar()
		{
			double r = Math.Min(this.Width, this.Height);
			double r1 = (r / 3) - 8.0;
			double r2 = r1 / 1.45;
			const int points = 14;
			var star = new VertexStorage();
			for (int i = 0; i < points; i++)
			{
				double a = (Math.PI * 2.0 * i / points) - (Math.PI / 2.0);
				double dx = Math.Cos(a);
				double dy = Math.Sin(a);
				double radius = (i & 1) != 0 ? r1 : r2;
				if (i == 0)
				{
					star.MoveTo(this.PolygonCenterX + (dx * radius), this.PolygonCenterY + (dy * radius));
				}
				else
				{
					star.LineTo(this.PolygonCenterX + (dx * radius), this.PolygonCenterY + (dy * radius));
				}
			}

			// C++ leaves the path open and lets the rasterizer close it; closing it here fills the same and lets
			// the GPU outline close too.
			star.ClosePolygon();
			return star;
		}

		private Affine PolygonMatrix()
		{
			Affine m = Affine.NewTranslation(-this.PolygonCenterX, -this.PolygonCenterY);
			m *= Affine.NewRotation(this.PolygonAngleSlider.Value * Math.PI / 180.0);
			m *= Affine.NewScaling(this.PolygonScaleSlider.Value);
			m *= Affine.NewTranslation(this.PolygonCenterX, this.PolygonCenterY);
			return m;
		}

		// The on_draw switch: demo space to image space for the chosen example.
		private Affine ImageMatrix()
		{
			// m_image_center: the window's middle, which is the image's since the window opens at its size.
			double imageCenterX = this.Width / 2.0;
			double imageCenterY = this.Height / 2.0;
			double polygonAngle = this.PolygonAngleSlider.Value * Math.PI / 180.0;
			double imageAngle = this.ImageAngleSlider.Value * Math.PI / 180.0;
			double polygonScale = this.PolygonScaleSlider.Value;
			double imageScale = this.ImageScaleSlider.Value;
			Affine m = Affine.NewIdentity();
			switch (this.ExampleRbox.CurrentItem)
			{
				case 1:
					m *= Affine.NewTranslation(-imageCenterX, -imageCenterY);
					m *= Affine.NewRotation(polygonAngle);
					m *= Affine.NewScaling(polygonScale);
					m *= Affine.NewTranslation(this.PolygonCenterX, this.PolygonCenterY);
					break;

				case 2:
					m *= Affine.NewTranslation(-imageCenterX, -imageCenterY);
					m *= Affine.NewRotation(imageAngle);
					m *= Affine.NewScaling(imageScale);
					m *= Affine.NewTranslation(this.ImageCenterX, this.ImageCenterY);
					break;

				case 3:
					m *= Affine.NewTranslation(-imageCenterX, -imageCenterY);
					m *= Affine.NewRotation(imageAngle);
					m *= Affine.NewScaling(imageScale);
					m *= Affine.NewTranslation(this.PolygonCenterX, this.PolygonCenterY);
					break;

				case 4:
					m *= Affine.NewTranslation(-this.ImageCenterX, -this.ImageCenterY);
					m *= Affine.NewRotation(polygonAngle);
					m *= Affine.NewScaling(polygonScale);
					m *= Affine.NewTranslation(this.PolygonCenterX, this.PolygonCenterY);
					break;

				case 5:
					m *= Affine.NewTranslation(-imageCenterX, -imageCenterY);
					m *= Affine.NewRotation(imageAngle);
					m *= Affine.NewRotation(polygonAngle);
					m *= Affine.NewScaling(imageScale);
					m *= Affine.NewScaling(polygonScale);
					m *= Affine.NewTranslation(this.ImageCenterX, this.ImageCenterY);
					break;

				case 6:
					m *= Affine.NewTranslation(-this.ImageCenterX, -this.ImageCenterY);
					m *= Affine.NewRotation(imageAngle);
					m *= Affine.NewScaling(imageScale);
					m *= Affine.NewTranslation(this.ImageCenterX, this.ImageCenterY);
					break;

				default:
					// Example 0: the identity.
					return m;
			}

			m.invert();
			return m;
		}
	}
}
