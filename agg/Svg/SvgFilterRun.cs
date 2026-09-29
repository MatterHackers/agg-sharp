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
using System.Linq;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// One primitive's run: what it reads and where it draws. <see cref="RunMore"/> parses and runs the primitives
	/// <see cref="SvgFilter"/>'s own switch leaves to it, with attribute defaults as usvg gives them.
	/// </summary>
	internal sealed class SvgFilterRun
	{
		public SvgFilterRun(SvgRenderer.Context context, SvgElement primitive, SvgPixelRect pixels, bool linear, int width, int height, (double X, double Y, double PixelsX, double PixelsY) units, Affine transform, Func<SvgElement, string, byte[]> input, Func<SvgElement, string, SvgPixelRect> inputRegion, RectangleDouble subregion, RectangleDouble? primitiveBox)
		{
			this.Context = context;
			this.Primitive = primitive;
			this.Pixels = pixels;
			this.Linear = linear;
			this.Width = width;
			this.Height = height;
			this.Units = units;
			this.Transform = transform;
			this.Input = input;
			this.InputRegion = inputRegion;
			this.Subregion = subregion;
			this.PrimitiveBox = primitiveBox;
		}

		public SvgRenderer.Context Context { get; }

		public SvgElement Primitive { get; }

		/// <summary>The primitive's subregion in canvas pixels.</summary>
		public SvgPixelRect Pixels { get; }

		public bool Linear { get; }

		public int Width { get; }

		public int Height { get; }

		/// <summary>User units per primitive unit (X, Y) and pixels per primitive unit (PixelsX, PixelsY).</summary>
		public (double X, double Y, double PixelsX, double PixelsY) Units { get; }

		/// <summary>The element's user space to canvas pixels.</summary>
		public Affine Transform { get; }

		/// <summary>An input (in or in2 of a node) in this primitive's colour space.</summary>
		public Func<SvgElement, string, byte[]> Input { get; }

		public Func<SvgElement, string, SvgPixelRect> InputRegion { get; }

		/// <summary>The primitive's subregion in user space.</summary>
		public RectangleDouble Subregion { get; }

		/// <summary>The element's bounding box (user space) when primitiveUnits is objectBoundingBox, else null.</summary>
		public RectangleDouble? PrimitiveBox { get; }

		/// <summary>The primitives past SvgFilter's own switch; null for one it does not know.</summary>
		public static SvgFilterImage RunMore(SvgFilterRun run)
		{
			SvgElement primitive = run.Primitive;
			byte[] pixels;
			switch (primitive.Name)
			{
				case "feComponentTransfer":
					var functions = new Func<double, double>[4];
					string[] names = { "feFuncR", "feFuncG", "feFuncB", "feFuncA" };
					foreach (SvgElement child in primitive.Children)
					{
						int index = Array.IndexOf(names, child.Name);
						if (index >= 0)
						{
							double Number(string name, double fallback) => SvgLength.ParseNumber(child[name], fallback);
							functions[index] = SvgFilterEffects.TransferFunction(child["type"], SvgLength.ParseList(child["tableValues"]),
								Number("slope", 1), Number("intercept", 0), Number("amplitude", 1), Number("exponent", 1), Number("offset", 0));
						}
					}

					pixels = SvgFilterEffects.ComponentTransfer(run.Input(primitive, "in"), run.Width, run.Pixels, functions);
					break;
				case "feMorphology":
					(double rx, double ry) = MorphologyRadius(primitive);
					pixels = SvgFilterEffects.Morphology(run.Input(primitive, "in"), run.Width, run.Pixels, primitive["operator"] == "dilate",
						(int)Math.Round(rx * run.Units.PixelsX), (int)Math.Round(ry * run.Units.PixelsY));
					break;
				case "feTile":
					pixels = SvgFilterEffects.Tile(run.Input(primitive, "in"), run.Width, run.Pixels, run.InputRegion(primitive, "in"));
					break;
				case "feConvolveMatrix":
					pixels = ConvolveMatrix(run);
					break;
				case "feDisplacementMap":
					double scale = SvgLength.ParseNumber(primitive["scale"], 0);
					Affine t = run.Transform;
					(double X, double Y) unitX = (t.sx * scale * run.Units.X, t.shy * scale * run.Units.X);
					(double X, double Y) unitY = (t.shx * scale * run.Units.Y, t.sy * scale * run.Units.Y);
					char Selector(string name) => primitive[name] is string value && value.Length == 1 && "RGB".Contains(value) ? value[0] : 'A';
					pixels = SvgFilterEffects.DisplacementMap(run.Input(primitive, "in"), run.Input(primitive, "in2"), run.Width, run.Pixels, unitX, unitY,
						Selector("xChannelSelector"), Selector("yChannelSelector"));
					break;
				case "feTurbulence":
					// usvg: a missing, malformed or negative baseFrequency is 0 0; numOctaves defaults to 1, a negative
					// is 0; seed is truncated. The noise is in the primitive's colour space, so the result is tagged with it.
					// baseFrequency is not a length, so objectBoundingBox primitiveUnits leave it per user unit (resvg's
					// feTurbulence/primitiveUnits=objectBoundingBox reference).
					List<double> frequency = SvgLength.ParseList(primitive["baseFrequency"]);
					(double fx, double fy) = frequency.Count == 1 ? (frequency[0], frequency[0]) : frequency.Count == 2 ? (frequency[0], frequency[1]) : (0, 0);
					if (fx < 0 || fy < 0)
					{
						(fx, fy) = (0, 0);
					}

					double octaves = Math.Max(0, SvgLength.ParseNumber(primitive["numOctaves"], 1));
					RectangleDouble tile = run.Subregion;
					pixels = SvgTurbulence.Render(run.Width, run.Height, run.Pixels, run.Transform, fx, fy, (int)Math.Round(octaves),
						(long)Math.Truncate(SvgLength.ParseNumber(primitive["seed"], 0)), primitive["type"] == "fractalNoise", primitive["stitchTiles"] == "stitch", tile);
					break;
				case "feImage":
					// Images and drawn elements are sRGB, whatever the primitive's colour space.
					return new SvgFilterImage(Image(run), false);
				case "feDiffuseLighting":
				case "feSpecularLighting":
					pixels = Lighting(run);
					break;
				default:
					return null;
			}

			return pixels == null ? null : new SvgFilterImage(pixels, run.Linear);
		}

		/// <summary>
		/// feImage, as usvg and resvg draw it: href to an element draws that element as a &lt;use&gt; would; anything
		/// else is an image fitted (preserveAspectRatio) into a subregion-sized box. Either way it is drawn from the
		/// subregion's top-left pixel - which is the drawing's user-space origin, even where it lies outside the filter
		/// region - at the filtered element's scale only (its rotation and skew are dropped), then cut to the subregion.
		/// </summary>
		private static byte[] Image(SvgFilterRun run)
		{
			SvgRenderer.Context context = run.Context;
			var layer = new ImageBuffer(run.Width, run.Height, 32, new BlenderPreMultBGRA());
			string href = run.Primitive["href"];
			SvgElement referenced = href != null && href.TrimStart().StartsWith("#", StringComparison.Ordinal) ? context.Document.GetElementById(href) : null;
			Affine toPixels = SubregionCorner(run);
			if (referenced != null)
			{
				if (context.ActiveFeImages.Add(referenced))
				{
					SvgRenderer.DrawReferenced(context, referenced, toPixels, layer, 0);
					context.ActiveFeImages.Remove(referenced);
				}
			}
			else if (href != null)
			{
				RectangleDouble region = run.Subregion;
				var image = new SvgElement("image", null);
				image.Attributes["href"] = href;
				image.Attributes["width"] = Number(region.Width);
				image.Attributes["height"] = Number(region.Height);
				if (run.Primitive["preserveAspectRatio"] is string aspect)
				{
					image.Attributes["preserveAspectRatio"] = aspect;
				}

				SvgImage.Draw(context.Document, image, toPixels, layer, context.ViewportWidth, context.ViewportHeight, SvgRenderer.DrawImageDocument);
			}

			return SvgFilterPrimitives.Crop(layer.GetBuffer(), run.Width, run.Pixels);
		}

		/// <summary>
		/// resvg's feImage transform: the filtered element's x and y scale, moved to the whole-pixel top-left of the
		/// subregion's (unclipped) pixel bounds; agg's y-up, so y runs down from that corner. Under a rotation or skew
		/// the resvg references show the element's whole transform instead, from the subregion's corner in user space.
		/// </summary>
		private static Affine SubregionCorner(SvgFilterRun run)
		{
			Affine t = run.Transform;
			RectangleDouble r = run.Subregion;
			if (t.shx != 0 || t.shy != 0)
			{
				return Affine.NewTranslation(r.Left, r.Bottom) * t;
			}

			double left = double.MaxValue, top = double.MinValue;
			foreach ((double x, double y) in new[] { (r.Left, r.Bottom), (r.Right, r.Bottom), (r.Left, r.Top), (r.Right, r.Top) })
			{
				double px = x, py = y;
				t.Transform(ref px, ref py);
				left = Math.Min(left, px);
				top = Math.Max(top, py);
			}

			double scaleX = Math.Sqrt(t.sx * t.sx + t.shy * t.shy);
			double scaleY = Math.Sqrt(t.shx * t.shx + t.sy * t.sy);
			return new Affine(scaleX, 0, 0, -scaleY, Math.Floor(left + 1e-6), Math.Ceiling(top - 1e-6));
		}

		private static string Number(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

		/// <summary>
		/// A lighting primitive: its first light child (box fractions under objectBoundingBox primitiveUnits) moved
		/// into the region's y-down pixel space (z scaled by the transform's mean scale), lighting-color in the primitive's colour space. Transparent black when there is no
		/// light or, as usvg rejects it, a specularExponent outside 1..128.
		/// </summary>
		private static byte[] Lighting(SvgFilterRun run)
		{
			SvgElement primitive = run.Primitive;
			bool specular = primitive.Name == "feSpecularLighting";
			double Number(SvgElement e, string name, double fallback) => SvgLength.ParseNumber(e[name], fallback);
			SvgElement source = primitive.Children.FirstOrDefault(c => c.Name is "feDistantLight" or "fePointLight" or "feSpotLight");
			double exponent = Number(primitive, "specularExponent", 1);
			if (source == null || (specular && (exponent < 1 || exponent > 128)))
			{
				return new byte[run.Width * run.Height * 4];
			}

			Affine t = run.Transform;
			double zScale = Math.Sqrt(Math.Abs(t.sx * t.sy - t.shx * t.shy));
			(double X, double Y, double Z) ToRegion(double x, double y, double z)
			{
				// With objectBoundingBox primitiveUnits a light's x and y are box fractions and z a fraction of the box's
				// normalized diagonal, as SVG measures a length that is neither across nor down.
				if (run.PrimitiveBox is RectangleDouble box)
				{
					x = box.Left + x * box.Width;
					y = box.Bottom + y * box.Height;
					z *= Math.Sqrt((box.Width * box.Width + box.Height * box.Height) / 2);
				}

				t.Transform(ref x, ref y);
				return (x - run.Pixels.Left, run.Pixels.Top - y, z * zScale);
			}

			var light = new SvgLight { Kind = source.Name.Substring(2, source.Name.Length - 7).ToLowerInvariant() };
			light.Azimuth = Number(source, "azimuth", 0);
			light.Elevation = Number(source, "elevation", 0);
			(light.X, light.Y, light.Z) = ToRegion(Number(source, "x", 0), Number(source, "y", 0), Number(source, "z", 0));
			(light.PointsAtX, light.PointsAtY, light.PointsAtZ) = ToRegion(Number(source, "pointsAtX", 0), Number(source, "pointsAtY", 0), Number(source, "pointsAtZ", 0));
			double spotExponent = Number(source, "specularExponent", 1);
			light.SpecularExponent = spotExponent > 0 ? spotExponent : 1;
			light.LimitingConeAngle = source["limitingConeAngle"] != null ? Number(source, "limitingConeAngle", 0) : (double?)null;

			// lighting-color is sRGB (white by default; its alpha is ignored).
			Color color = SvgColor.TryParse(primitive["lighting-color"], out Color parsed) ? parsed : new Color(255, 255, 255, 255);
			double Channel(int value)
			{
				double c = value / 255.0;
				return !run.Linear ? c : c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4);
			}

			return SvgLighting.Light(run.Input(primitive, "in"), run.Width, run.Height, run.Pixels, light, specular,
				Number(primitive, "surfaceScale", 1), Number(primitive, specular ? "specularConstant" : "diffuseConstant", 1), exponent,
				(Channel(color.red), Channel(color.green), Channel(color.blue)));
		}

		/// <summary>radius as usvg reads it: missing or negative is 1 1, and a zero half of a pair is 1.</summary>
		private static (double X, double Y) MorphologyRadius(SvgElement primitive)
		{
			List<double> values = SvgLength.ParseList(primitive["radius"]);
			if (primitive["radius"] == null)
			{
				return (1, 1);
			}

			(double x, double y) = values.Count == 1 ? (values[0], values[0]) : values.Count == 2 ? (values[0], values[1]) : (0, 0);
			x = x == 0 ? 1 : x;
			y = y == 0 ? 1 : y;
			return x > 0 && y > 0 ? (x, y) : (1, 1);
		}

		/// <summary>feConvolveMatrix, or transparent black when its attributes do not make a kernel (as usvg rejects them).</summary>
		private static byte[] ConvolveMatrix(SvgFilterRun run)
		{
			SvgElement primitive = run.Primitive;
			int orderX = 3, orderY = 3;
			if (primitive["order"] != null)
			{
				List<double> order = SvgLength.ParseList(primitive["order"]);
				int x = order.Count > 0 ? (int)order[0] : 3;
				int y = order.Count > 1 ? (int)order[1] : x;
				if (x > 0 && y > 0)
				{
					(orderX, orderY) = (x, y);
				}
			}

			List<double> kernel = SvgLength.ParseList(primitive["kernelMatrix"]);
			double sum = Math.Round(kernel.Sum() * 1e6) / 1e6;
			double divisor = SvgLength.ParseNumber(primitive["divisor"], sum == 0 ? 1 : sum);
			int Target(string name, int order)
			{
				int target = (int)SvgLength.ParseNumber(primitive[name], order / 2);
				return target >= 0 && target < order ? target : -1;
			}

			int targetX = Target("targetX", orderX), targetY = Target("targetY", orderY);
			if (kernel.Count != orderX * orderY || divisor == 0 || targetX < 0 || targetY < 0)
			{
				return new byte[run.Width * run.Height * 4];
			}

			string edgeMode = primitive["edgeMode"] is "none" or "wrap" ? primitive["edgeMode"] : "duplicate";
			return SvgFilterEffects.ConvolveMatrix(run.Input(primitive, "in"), run.Width, run.Pixels, orderX, orderY, kernel.ToArray(), divisor,
				SvgLength.ParseNumber(primitive["bias"], 0), targetX, targetY, edgeMode, primitive["preserveAlpha"] == "true");
		}
	}
}
