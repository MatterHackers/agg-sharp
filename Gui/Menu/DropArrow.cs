/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg.UI
{
	public class DropArrow
	{
		private static readonly object buildLocker = new object();

		private static VertexStorage _downArrow;

		public static VertexStorage DownArrow
		{
			get
			{
				lock (buildLocker)
				{
					if (calculatedDeviceScale != GuiWidget.DeviceScale)
					{
						BuildDropArrow();
					}

					return _downArrow;
				}
			}
		}

		private static VertexStorage _upArrow;

		public static VertexStorage UpArrow
		{
			get
			{
				lock (buildLocker)
				{
					if (calculatedDeviceScale != GuiWidget.DeviceScale)
					{
						BuildDropArrow();
					}

					return _upArrow;
				}
			}
		}

		public static double ArrowHeight => 5 * GuiWidget.DeviceScale;

		private static double calculatedDeviceScale;

		static DropArrow()
		{
			BuildDropArrow();
		}

		private static void BuildDropArrow()
		{
			// Build into locals and publish only fully populated storage so a concurrent reader
			// can never observe an empty or partially built arrow. Capture DeviceScale once so
			// both arrows are built at the same scale, and record it last.
			var deviceScale = GuiWidget.DeviceScale;
			var arrowHeight = 5 * deviceScale;

			var downArrow = new VertexStorage();
			downArrow.MoveTo(-arrowHeight, 0);
			downArrow.LineTo(arrowHeight, 0);
			downArrow.LineTo(0, -arrowHeight);
			downArrow.ClosePolygon();

			var upArrow = new VertexStorage();
			upArrow.MoveTo(-arrowHeight, -arrowHeight);
			upArrow.LineTo(arrowHeight, -arrowHeight);
			upArrow.LineTo(0, 0);
			upArrow.ClosePolygon();

			_downArrow = downArrow;
			_upArrow = upArrow;
			calculatedDeviceScale = deviceScale;
		}
	}
}