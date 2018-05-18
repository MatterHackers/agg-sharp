/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg.Image;
using NUnit.Framework;

namespace MatterHackers.Agg.UI.Tests
{
	[TestFixture, Category("Agg.UI")]
	public class ScrollableWidgetTests
	{
		public static bool saveImagesForDebug = false;

		private void OutputImages(GuiWidget control, GuiWidget test)
		{
			if (saveImagesForDebug)
			{
				ImageTgaIO.Save(control.BackBuffer, "image-control.tga");
				ImageTgaIO.Save(test.BackBuffer, "image-test.tga");
			}
		}

		[Test]
		public void LimitScrolToContetsTests()
		{
			GuiWidget containerControl = new GuiWidget(200, 200);
			containerControl.DoubleBuffer = true;
			containerControl.BackBuffer.NewGraphics2D().Clear(Color.White);
			containerControl.OnDraw(containerControl.NewGraphics2D());

			ScrollableWidget containerTest = new ScrollableWidget(200, 200);
			containerTest.DoubleBuffer = true;
			containerTest.BackBuffer.NewGraphics2D().Clear(Color.White);
			containerTest.OnDraw(containerTest.NewGraphics2D());

			OutputImages(containerControl, containerTest);

			Assert.IsTrue(containerControl.BackBuffer != null, "When we set a guiWidget to DoubleBuffer it needs to create one.");
			Assert.IsTrue(containerControl.BackBuffer == containerTest.BackBuffer, "The Anchored widget should be in the correct place.");
		}
	}
}