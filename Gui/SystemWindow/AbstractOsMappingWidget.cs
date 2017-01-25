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

using System;

namespace MatterHackers.Agg.UI
{
	public abstract class AbstractOsMappingWidget : GuiWidget
	{
		public abstract string Caption { get; set; }

		public abstract void ShowModal();

		public abstract void Show();

		public abstract void Run();

		public abstract Point2D DesktopPosition { get; set; }

		public bool Maximized
		{
			get { return childSystemWindow.Maximized; }
			set { childSystemWindow.Maximized = value; }
		}

		public abstract int TitleBarHeight { get; }

		public virtual void OnInitialize()
		{
		}

		protected SystemWindow childSystemWindow;

		// format - see enum pix_format_e {};
		// flip_y - true if you want to have the Y-axis flipped vertically.
		public AbstractOsMappingWidget(SystemWindow childSystemWindow)
			: base(childSystemWindow.Width, childSystemWindow.Height, SizeLimitsToSet.None)
		{
			this.childSystemWindow = childSystemWindow;
		}

		public double width()
		{
			return BoundsRelativeToParent.Width;
		}

		public double height()
		{
			return BoundsRelativeToParent.Height;
		}

		// Get raw display handler depending on the system.
		// For win32 its an HDC, for other systems it can be a pointer to some
		// structure. See the implementation files for details.
		// It's provided "as is", so, first you should check if it's not null.
		// If it's null the raw_display_handler is not supported. Also, there's
		// no guarantee that this function is implemented, so, in some
		// implementations you may have simply an unresolved symbol when linking.
		//public void* raw_display_handler();
	}
}