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

using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// The Screenshot window's capture and PNG export. A capture draws the app's widget tree into an
	/// <see cref="ImageBuffer"/> in software rather than reading the GPU back: every agg host (mac, browser)
	/// can do that, and the browser host has no GPU readback to call.
	/// </summary>
	public static class ScreenshotCapture
	{
		/// <summary>The widget a capture draws: <paramref name="widget"/>'s top-most ancestor under the system
		/// window, which is the whole app page (for the GUI demo, its shell).</summary>
		public static GuiWidget CaptureRoot(GuiWidget widget)
		{
			GuiWidget root = widget;
			while (root.Parent != null && !(root.Parent is SystemWindow))
			{
				root = root.Parent;
			}

			return root;
		}

		/// <summary>
		/// Draws <paramref name="root"/> into <paramref name="reuse"/> when it is already the root's size (so
		/// continuous capture does not hand the GPU a new texture every frame), or into a new image otherwise.
		/// </summary>
		/// <returns>The image drawn into.</returns>
		public static ImageBuffer Capture(GuiWidget root, ImageBuffer reuse = null)
		{
			int width = System.Math.Max(1, (int)System.Math.Ceiling(root.Width));
			int height = System.Math.Max(1, (int)System.Math.Ceiling(root.Height));
			ImageBuffer image = reuse != null && reuse.Width == width && reuse.Height == height
				? reuse
				: new ImageBuffer(width, height);

			Graphics2D graphics = image.NewGraphics2D();
			// Opaque, as a screen is: a root that leaves its background clear shows white, not a hole.
			graphics.Clear(root.BackgroundColor.alpha == 255 ? root.BackgroundColor : Color.White);
			root.OnDraw(graphics);
			image.MarkImageChanged();
			return image;
		}

		/// <summary>The file a save writes: the chosen path, given a .png extension when it has none.</summary>
		public static string PngPath(string chosen)
		{
			return string.IsNullOrEmpty(Path.GetExtension(chosen)) ? chosen + ".png" : chosen;
		}

		/// <summary>Writes <paramref name="image"/> to <paramref name="path"/> as a PNG, replacing any file there
		/// (the save dialog has already asked the user about that).</summary>
		/// <returns>False if the image could not be encoded.</returns>
		public static bool WritePng(string path, ImageBuffer image)
		{
			using var stream = File.Create(path);
			return ImageIO.SaveImageData(stream, ".png", image);
		}
	}
}
