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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.IO;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The check mark a <see cref="PopupMenu.CheckboxMenuItem"/> shows. MatterCAD ships Icons/fa-check_16.png
	/// and keeps that look; an app without it (the agg-sharp demos) gets the same size mark drawn as a
	/// stroke, where the icon load would otherwise throw in debug builds.
	/// </summary>
	internal static class MenuCheckMark
	{
		private const string IconName = "fa-check_16.png";

		/// <summary>A 16 x 16 (logical) check in <paramref name="color"/> at the current device scale.</summary>
		public static ImageBuffer Create(Color color)
		{
			if (StaticData.Instance.FileExists(Path.Combine("Icons", IconName)))
			{
				return StaticData.Instance.LoadIcon(IconName, 16, 16).GrayToColor(color);
			}

			double scale = GuiWidget.DeviceScale;
			var size = (int)Math.Round(16 * scale);
			var image = new ImageBuffer(size, size);
			image.SetRecieveBlender(new BlenderPreMultBGRA());

			// agg-gui's check: a short down stroke into a long up stroke (y is up in agg)
			var check = new VertexStorage();
			check.MoveTo(3 * scale, 8 * scale);
			check.LineTo(6.5 * scale, 4.5 * scale);
			check.LineTo(13 * scale, 12 * scale);
			image.NewGraphics2D().Render(new Stroke(check, 2 * scale), color);

			return image;
		}
	}
}
