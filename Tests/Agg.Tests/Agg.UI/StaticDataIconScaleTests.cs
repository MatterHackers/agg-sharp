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

using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="StaticData.LoadIcon(string, int, int, bool, System.Func{ImageBuffer, ValueTuple{ImageBuffer, string}})"/>
	/// takes a design size and must hand back an image at that size times
	/// <see cref="GuiWidget.DeviceScale"/>. Its "scale if required" guard compared the loaded icon against the
	/// design size instead, so an icon whose source pixels happened to land exactly on the design size after
	/// the loader's own scaling was passed straight through at half the size the caller asked for.
	/// </summary>
	/// <remarks>
	/// Keyless <c>[NotInParallel]</c>: both <see cref="GuiWidget.DeviceScale"/> and
	/// <see cref="StaticData.RootPath"/> are process wide. See <c>ConstructorHygieneTests</c> for the same
	/// temporary-root pattern.
	/// </remarks>
	public class StaticDataIconScaleTests
	{
		[Test]
		[NotInParallel]
		public async Task AnIconIsReturnedAtTheRequestedSizeTimesDeviceScale()
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			string savedRootPath = StaticData.RootPath;
			string tempRoot = Path.Combine(Path.GetTempPath(), "AggStaticDataIconScale_" + Path.GetRandomFileName());

			try
			{
				Directory.CreateDirectory(Path.Combine(tempRoot, "Icons"));
				StaticData.RootPath = tempRoot;
				GuiWidget.DeviceScale = 2;

				// Source pixels half the requested design size: the loader scales it by the device scale on
				// its way in, landing it on exactly the design size. That coincidence is what used to satisfy
				// the guard and skip the scale to device size.
				WriteBlankIcon(tempRoot, "half_of_design.png", 8, 8);
				ImageBuffer sourceSmallerThanDesign = StaticData.Instance.LoadIcon("half_of_design.png", 16, 16);

				await Assert.That(sourceSmallerThanDesign.Width).IsEqualTo(32)
					.Because("the caller asked for a 16 design pixel icon on a display that draws 2 device pixels per design pixel");
				await Assert.That(sourceSmallerThanDesign.Height).IsEqualTo(32);

				// The ordinary case - source at the design size - has to keep working.
				WriteBlankIcon(tempRoot, "at_design.png", 16, 16);
				ImageBuffer sourceAtDesign = StaticData.Instance.LoadIcon("at_design.png", 16, 16);

				await Assert.That(sourceAtDesign.Width).IsEqualTo(32);
				await Assert.That(sourceAtDesign.Height).IsEqualTo(32);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
				StaticData.RootPath = savedRootPath;
				Directory.Delete(tempRoot, true);
			}
		}

		private static void WriteBlankIcon(string rootPath, string iconName, int width, int height)
		{
			var image = new ImageBuffer(width, height);
			image.NewGraphics2D().Clear(Color.White);
			ImageIO.SaveImageData(Path.Combine(rootPath, "Icons", iconName), image);
		}
	}
}
