/*
Copyright (c) 2018, John Lewin
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
using System.Collections.Specialized;
using HtmlAgilityPack;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI.Tests
{
	public class SimulatedClipboard : ISystemClipboard
	{
        private ImageBuffer Image { get; set; }
		private string Text { get; set; }

		public bool ContainsFileDropList => throw new NotImplementedException();

		public bool ContainsImage => Image != null;

		public bool ContainsText => !string.IsNullOrEmpty(Text);

		public StringCollection GetFileDropList()
		{
			throw new NotImplementedException();
		}

		public ImageBuffer GetImage()
		{
			throw new NotImplementedException();
		}

		public string GetText()
		{
			return Text;
		}

		public void SetImage(ImageBuffer imageBuffer)
		{
            Image = imageBuffer;
        }

		public void SetText(string text)
		{
			Text = text;
		}
	}
}
