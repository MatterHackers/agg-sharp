/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public interface IPlatformWindow
	{
		// This must be set to false when doing parallel UI testing.
		public static bool EnablePlatformWindowInput { get; set; } = true;

		string Caption { get; set; }

		int TitleBarHeight { get; }

		Point2D DesktopPosition { get; set; }

		Vector2 MinimumSize { get; set; }

		Keys ModifierKeys { get; }

		/// <summary>
		/// Bring this window to the front of any windows that are part of this application. Does not bring it to the front of othe apps
		/// </summary>
		void BringToFront();

		/// <summary>
		/// Bring this window to the front of other app windows
		/// </summary>
		void Activate();

		void Invalidate(RectangleDouble rectToInvalidate);

		void Close();

		void SetCursor(Cursors cursorToSet);

		Graphics2D NewGraphics2D();

		void ShowSystemWindow(SystemWindow systemWindow);

		void CloseSystemWindow(SystemWindow systemWindow);
	}
}