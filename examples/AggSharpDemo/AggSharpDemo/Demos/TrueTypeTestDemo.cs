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
using System.Collections.Concurrent;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's truetype_test_02_win (truetype_lcd.cpp), by way of agg-rust's truetype_test: four paragraphs of
	/// TrueType text through LCD subpixel coverage, with faux weight, faux italic, width, letter interval, the LCD
	/// filter's primary weight and an inverse gamma over the result.
	/// </summary>
	/// <remarks>
	/// <para>Every knob is applied to this demo's glyphs only. agg-sharp's process-wide equivalents
	/// (<see cref="TextStyleSettings"/> and <see cref="LcdRenderSettings"/>) belong to the System window; a demo
	/// moving them would restyle every widget in the app. So the glyphs come from a <see cref="StyledTypeFace"/>
	/// given its own <see cref="GlyphStyle"/> - the same shaping the process-wide settings get - and the LCD mask goes
	/// through <see cref="LcdMaskBuilder"/> with this demo's primary weight rather than through
	/// <see cref="Graphics2D.RenderLcd"/>. Interval is therefore agg-sharp's (a fraction of the em, as agg-gui's),
	/// not the fraction of a pixel C++ adds.</para>
	/// <para>The text area is rendered in software into an image and that image is drawn, on both the GPU and the
	/// reference path: subpixel coverage has to be composited per channel, which a plain GPU fill cannot do. It is
	/// rendered at the device pixels the frame is drawn at (the transform's scale), so on a HiDPI screen the
	/// fringes land on real device pixels rather than on demo pixels that are then resampled.</para>
	/// <para>The C++ faces (Arial, Tahoma, Verdana, Times, Georgia) are Microsoft fonts that cannot be
	/// redistributed, so the radio offers openly licensed faces instead. Hinting here snaps each baseline to a whole
	/// device pixel (what C++ does with the hinted y); there is no TrueType instruction hinting.</para>
	/// </remarks>
	public class TrueTypeTestDemo : AggDemo
	{
		/// <summary>The controls fill the rows below this; the text, and Invert's black, everything above.</summary>
		public const int TextAreaBottom = 120;

		private static readonly string[] Paragraphs =
		{
			"A single pixel on a color LCD is made of three colored elements \n"
			+ "ordered (on various displays) either as blue, green, and red (BGR), \n"
			+ "or as red, green, and blue (RGB). These pixel components, sometimes \n"
			+ "called sub-pixels, appear as a single color to the human eye because \n"
			+ "of blurring by the optics and spatial integration by nerve cells in the eye.",

			"The components are easily visible, however, when viewed with \n"
			+ "a small magnifying glass, such as a loupe. Over a certain resolution \n"
			+ "range the colors in the sub-pixels are not visible, but the relative \n"
			+ "intensity of the components shifts the apparent position or orientation \n"
			+ "of a line. Methods that take this interaction between the display \n"
			+ "technology and the human visual system into account are called \n"
			+ "subpixel rendering algorithms.",

			"The resolution at which colored sub-pixels go unnoticed differs, \n"
			+ "however, with each user some users are distracted by the colored \n"
			+ "\"fringes\" resulting from sub-pixel rendering. Subpixel rendering \n"
			+ "is better suited to some display technologies than others. The \n"
			+ "technology is well-suited to LCDs, but less so for CRTs. In a CRT \n"
			+ "the light from the pixel components often spread across pixels, \n"
			+ "and the outputs of adjacent pixels are not perfectly independent.",

			"If a designer knew precisely a great deal about the display's \n"
			+ "electron beams and aperture grille, subpixel rendering might \n"
			+ "have some advantage. But the properties of the CRT components, \n"
			+ "coupled with the alignment variations that are part of the \n"
			+ "production process, make subpixel rendering less effective for \n"
			+ "these displays. The technique should have good application to \n"
			+ "organic light emitting diodes and other display technologies.",
		};

		/// <summary>
		/// The radio's faces: a sans, a serif, a monospace and a rounded sans, each with the resource its upright
		/// paragraphs use and the one its italic paragraphs use (Nunito ships no italic, so it uses its regular for
		/// both, as agg-rust does for Tahoma). Liberation is GPL-2 with the font exception
		/// (liberation-fonts-ttf-1.07.0/License.txt), Nunito SIL OFL 1.1 (Fonts/Nunito-LICENSE-OFL.txt).
		/// </summary>
		private static readonly (string Name, string Regular, string Italic)[] Faces =
		{
			("Liberation Sans", "LiberationSans-Regular.ttf", "LiberationSans-Italic.ttf"),
			("Liberation Serif", "LiberationSerif-Regular.ttf", "LiberationSerif-Italic.ttf"),
			("Liberation Mono", "LiberationMono-Regular.ttf", "LiberationMono-Italic.ttf"),
			("Nunito", "Nunito-Regular.ttf", "Nunito-Regular.ttf"),
		};

		private static readonly ConcurrentDictionary<string, TypeFace> LoadedFaces = new ConcurrentDictionary<string, TypeFace>();

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private ImageBuffer textArea;

		/// <summary>The settings <see cref="textArea"/> was rendered under; it is rebuilt when they change.</summary>
		private string textAreaSettings;

		public TrueTypeTestDemo()
		{
			this.TypefaceRbox = new RboxCtrl(5.0, 5.0, 155.0, 110.0);
			foreach (var face in Faces)
			{
				this.TypefaceRbox.AddItem(face.Name);
			}

			// C++ defaults to Georgia; Liberation Serif is the serif here.
			this.TypefaceRbox.CurrentItem = 1;

			this.FontScaleSlider = NewSlider(160.0, 10.0, 0.5, 2.0, 1.43, "Font Scale={0:F2}");
			this.FauxItalicSlider = NewSlider(160.0, 25.0, -1.0, 1.0, 0.0, "Faux Italic={0:F2}");
			this.FauxWeightSlider = NewSlider(160.0, 40.0, -1.0, 1.0, 0.0, "Faux Weight={0:F2}");
			this.IntervalSlider = NewSlider(260.0, 55.0, -0.2, 0.2, 0.0, "Interval={0:F3}");
			this.WidthSlider = NewSlider(260.0, 70.0, 0.75, 1.25, 1.0, "Width={0:F2}");
			this.GammaSlider = NewSlider(260.0, 85.0, 0.5, 2.5, 1.0, "Gamma={0:F2}");
			this.PrimaryWeightSlider = NewSlider(260.0, 100.0, 0.0, 1.0, 1.0 / 3.0, "Primary Weight={0:F2}");

			this.GrayscaleCbox = new CboxCtrl(160.0, 50.0, "Grayscale");
			this.HintingCbox = new CboxCtrl(160.0, 65.0, "Hinting") { Checked = true };
			this.KerningCbox = new CboxCtrl(160.0, 80.0, "Kerning") { Checked = true };
			this.InvertCbox = new CboxCtrl(160.0, 95.0, "Invert");

			this.ctrls.Add(this.TypefaceRbox);
			this.ctrls.Add(this.FontScaleSlider);
			this.ctrls.Add(this.FauxItalicSlider);
			this.ctrls.Add(this.FauxWeightSlider);
			this.ctrls.Add(this.IntervalSlider);
			this.ctrls.Add(this.WidthSlider);
			this.ctrls.Add(this.GammaSlider);
			this.ctrls.Add(this.PrimaryWeightSlider);
			this.ctrls.Add(this.HintingCbox);
			this.ctrls.Add(this.KerningCbox);
			this.ctrls.Add(this.InvertCbox);
			this.ctrls.Add(this.GrayscaleCbox);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>The names the typeface radio lists, in its order.</summary>
		public static string[] TypefaceNames => Array.ConvertAll(Faces, face => face.Name);

		/// <summary>C++ <c>m_typeface</c>: which face draws the text.</summary>
		public RboxCtrl TypefaceRbox { get; }

		/// <summary>C++ <c>m_height</c>: the em is 12 pixels times this.</summary>
		public SliderCtrl FontScaleSlider { get; }

		/// <summary>C++ <c>m_faux_italic</c>: each outline is sheared by a third of this.</summary>
		public SliderCtrl FauxItalicSlider { get; }

		/// <summary>C++ <c>m_faux_weight</c>: each outline's contour is offset, almost only horizontally.</summary>
		public SliderCtrl FauxWeightSlider { get; }

		/// <summary>C++ <c>m_interval</c>: spacing added to every advance, as a fraction of the em.</summary>
		public SliderCtrl IntervalSlider { get; }

		/// <summary>C++ <c>m_width</c>: horizontal scale of every outline; advances are unchanged.</summary>
		public SliderCtrl WidthSlider { get; }

		/// <summary>C++ <c>m_gamma</c>: the inverse gamma applied to the finished text area.</summary>
		public SliderCtrl GammaSlider { get; }

		/// <summary>C++ <c>m_primary</c>: the centre tap of the LCD filter.</summary>
		public SliderCtrl PrimaryWeightSlider { get; }

		/// <summary>C++ <c>m_grayscale</c>: ordinary anti-aliasing instead of LCD subpixel coverage.</summary>
		public CboxCtrl GrayscaleCbox { get; }

		/// <summary>C++ <c>m_hinting</c>: baselines on whole pixels.</summary>
		public CboxCtrl HintingCbox { get; }

		/// <summary>C++ <c>m_kerning</c>: the face's pair kerning between neighbouring glyphs.</summary>
		public CboxCtrl KerningCbox { get; }

		/// <summary>C++ <c>m_invert</c>: white text on black in the text area.</summary>
		public CboxCtrl InvertCbox { get; }

		/// <summary>The text area as last rendered, in device pixels - demo size times the draw's scale.</summary>
		public ImageBuffer TextAreaImage => this.textArea;

		public override string Name => "truetype_test";

		public override string Category => "Text";

		public override string Description => "TrueType text through LCD subpixel coverage, with faux weight and italic, width, spacing, the LCD filter's primary weight and gamma. Compare it with Grayscale.";

		public override int Width => 640;

		public override int Height => 560;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			// Device pixels per demo pixel: 1 on the reference path, the view's scale on a HiDPI GPU view.
			double deviceScale = LcdRenderSettings.EffectiveScaleOf(graphics.GetTransform());
			if (!(deviceScale > 0) || double.IsInfinity(deviceScale))
			{
				deviceScale = 1;
			}

			this.UpdateTextArea(deviceScale);
			if (deviceScale == 1)
			{
				graphics.Render(this.textArea, 0, TextAreaBottom);
			}
			else
			{
				graphics.Render(this.textArea, 0, TextAreaBottom, 0, 1 / deviceScale, 1 / deviceScale);
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseDown(x, y, button);
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			this.ctrls.OnMouseMove(x, y, flags);
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		private static SliderCtrl NewSlider(double x1, double y1, double min, double max, double value, string label)
		{
			var slider = new SliderCtrl(x1, y1, 635.0, y1 + 7.0) { Label = label };
			slider.SetRange(min, max);
			slider.Value = value;
			return slider;
		}

		private static TypeFace LoadFace(string fileName)
		{
			return LoadedFaces.GetOrAdd(fileName, name =>
			{
				string resourceName = "MatterHackers.AggSharpDemo.Fonts." + name;
				using var stream = typeof(TrueTypeTestDemo).Assembly.GetManifestResourceStream(resourceName)
					?? throw new InvalidOperationException($"The font resource '{resourceName}' is missing; AggSharpDemo.csproj embeds it.");
				var typeFace = new TypeFace();
				typeFace.LoadTTF(stream);
				return typeFace;
			});
		}

		/// <summary>Re-renders the text area when any control has moved since it was last rendered.</summary>
		private void UpdateTextArea(double deviceScale)
		{
			int face = Math.Clamp(this.TypefaceRbox.CurrentItem, 0, Faces.Length - 1);
			string settings = string.Join(
				"|",
				face,
				deviceScale,
				this.FontScaleSlider.Value,
				this.FauxItalicSlider.Value,
				this.FauxWeightSlider.Value,
				this.IntervalSlider.Value,
				this.WidthSlider.Value,
				this.GammaSlider.Value,
				this.PrimaryWeightSlider.Value,
				this.GrayscaleCbox.Checked,
				this.HintingCbox.Checked,
				this.KerningCbox.Checked,
				this.InvertCbox.Checked);
			if (this.textArea != null && settings == this.textAreaSettings)
			{
				return;
			}

			this.textAreaSettings = settings;
			int width = (int)Math.Ceiling(this.Width * deviceScale);
			int height = (int)Math.Ceiling((this.Height - TextAreaBottom) * deviceScale);
			if (this.textArea == null || this.textArea.Width != width || this.textArea.Height != height)
			{
				this.textArea = new ImageBuffer(width, height);
			}

			this.RenderTextArea(Faces[face], deviceScale);
			this.textArea.MarkImageChanged();
		}

		/// <summary>
		/// C++ on_draw up to the ctrls, in text-area device pixels: y 0 is the frame's row <see cref="TextAreaBottom"/>,
		/// and every demo length is <paramref name="deviceScale"/> of them.
		/// </summary>
		private void RenderTextArea((string Name, string Regular, string Italic) face, double deviceScale)
		{
			bool invert = this.InvertCbox.Checked;
			bool grayscale = this.GrayscaleCbox.Checked;
			Color background = invert ? Color.Black : Color.White;
			Color ink = invert ? Color.White : Color.Black;

			// Opaque, because LcdComposite blends as if the destination's alpha were 255.
			byte[] pixels = this.textArea.GetBuffer();
			for (int i = 0; i < pixels.Length; i += 4)
			{
				pixels[i] = background.blue;
				pixels[i + 1] = background.green;
				pixels[i + 2] = background.red;
				pixels[i + 3] = 255;
			}

			int width = this.textArea.Width;
			int height = this.textArea.Height;
			Graphics2D grayGraphics = grayscale ? this.textArea.NewGraphics2D() : null;
			LcdMaskBuilder lcdMask = grayscale ? null : new LcdMaskBuilder(width, height);

			// C++ font engine height is the em in pixels; StyledTypeFace takes points.
			double emPixels = this.FontScaleSlider.Value * 12.0 * deviceScale;
			double emPoints = emPixels * StyledTypeFace.PointsPerInch / StyledTypeFace.PixelsPerInch;
			var style = new GlyphStyle(this.WidthSlider.Value, this.IntervalSlider.Value, this.FauxWeightSlider.Value, this.FauxItalicSlider.Value);
			double y = (this.Height - 20.0 - TextAreaBottom) * deviceScale;
			for (int i = 0; i < Paragraphs.Length; i++)
			{
				// C++ alternates: the second and fourth paragraphs are italic.
				var styledFace = new StyledTypeFace(LoadFace(i % 2 == 1 ? face.Italic : face.Regular), emPoints) { Style = style };
				y = this.DrawParagraph(Paragraphs[i], styledFace, 10.0 * deviceScale, y, grayscale, path =>
				{
					if (grayGraphics != null)
					{
						grayGraphics.Render(path, ink);
					}
					else
					{
						lcdMask.AddPath(Affine.NewIdentity(), path);
					}
				});
				y -= (7.0 * deviceScale) + emPixels;
			}

			if (lcdMask != null)
			{
				// The demo's own primary weight, not LcdRenderSettings'; the filter's gamma stays off because C++
				// applies its gamma to the finished pixels, below.
				LcdComposite.Composite(this.textArea, lcdMask.FinalizeMask(this.PrimaryWeightSlider.Value), ink, 0, 0);
			}

			// C++ pixfmt apply_gamma_inv over the whole frame: only the text area has anything but black or white.
			double gamma = this.GammaSlider.Value;
			if (gamma != 1.0)
			{
				var table = new GammaLookUpTable(gamma);
				for (int i = 0; i < pixels.Length; i += 4)
				{
					pixels[i] = table.inv(pixels[i]);
					pixels[i + 1] = table.inv(pixels[i + 1]);
					pixels[i + 2] = table.inv(pixels[i + 2]);
				}
			}
		}

		/// <summary>
		/// C++ draw_text: lays <paramref name="text"/> out from <paramref name="startX"/> at baseline
		/// <paramref name="y"/>, 1.25 em per line, handing each glyph of <paramref name="face"/> (which carries the
		/// demo's <see cref="GlyphStyle"/>) to <paramref name="fill"/>. Returns the last line's baseline.
		/// </summary>
		private double DrawParagraph(string text, StyledTypeFace face, double startX, double y, bool grayscale, Action<IVertexSource> fill)
		{
			double emPixels = face.EmSizeInPixels;
			double kerningScale = this.KerningCbox.Checked ? emPixels / face.TypeFace.UnitsPerEm : 0;
			double x = startX;
			for (int i = 0; i < text.Length; i++)
			{
				char character = text[i];
				if (character == '\n')
				{
					x = startX;
					y -= emPixels * 1.25;
					continue;
				}

				// C++ add_kerning: the pair's kerning moves the pen before the second glyph is placed.
				if (kerningScale != 0 && i > 0 && text[i - 1] != '\n')
				{
					x += face.TypeFace.GetKerningForCodePoints(text[i - 1], character) * kerningScale;
				}

				// LCD coverage is rasterized 3x wide, so flatten finely enough for that.
				IVertexSource glyph = face.GetGlyphForCharacter(character, grayscale ? 1 : 3);
				if (glyph != null)
				{
					double baseline = this.HintingCbox.Checked ? Math.Floor(y + 0.5) : y;
					fill(new VertexSourceApplyTransform(glyph, Affine.NewTranslation(x, baseline)));
				}

				x += face.GetAdvanceForCharacter(character);
			}

			return y;
		}
	}
}
