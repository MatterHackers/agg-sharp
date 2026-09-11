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
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Turns a platform drag-and-drop session into the mouse events agg carries drops on.
	/// </summary>
	/// <remarks>
	/// There is no drop API in agg: a widget learns about files from <see cref="MouseEventArgs.DragFiles"/>
	/// on an ordinary move or up, and says it will take them by setting
	/// <see cref="MouseEventArgs.AcceptDrop"/> on the move. Every host therefore has to synthesize the same
	/// sequence, so the sequence lives here rather than in each host, where it could not be tested without a
	/// real drag.
	/// </remarks>
	public static class FileDropDispatcher
	{
		/// <summary>
		/// Reports a drag hovering at <paramref name="x"/>, <paramref name="y"/> (agg device pixels, origin
		/// bottom left) and answers whether anything under it will take the files - which is what a host maps
		/// onto its own "copy" or "no drop" drag operation.
		/// </summary>
		public static bool Hover(GuiWidget window, List<string> droppedPaths, double x, double y)
		{
			var mouseEvent = new MouseEventArgs(MouseButtons.None, 0, x, y, 0, droppedPaths);
			window.OnMouseMove(mouseEvent);

			return mouseEvent.AcceptDrop;
		}

		/// <summary>
		/// Delivers the drop itself, as a mouse up carrying the files. Sent whether or not the last
		/// <see cref="Hover"/> was accepted - the Windows sink does the same, and a widget that wants the
		/// files reads them off the up - so the answer only says files were there to deliver.
		/// </summary>
		public static bool Drop(GuiWidget window, List<string> droppedPaths, double x, double y)
		{
			window.OnMouseUp(new MouseEventArgs(MouseButtons.None, 0, x, y, 0, droppedPaths));

			return droppedPaths?.Count > 0;
		}

		/// <summary>
		/// Reports that the drag left, so any hover highlight it lit goes out.
		/// </summary>
		/// <remarks>
		/// Windows needs no such call: it simply forgets the files, and the next real mouse move - which
		/// keeps arriving there - carries none. A mac delivers no mouse events at all while a drag is in
		/// flight, so without this the highlight would stay lit until the pointer moved again, hence the same
		/// off-screen sentinel a pointer exit uses.
		/// </remarks>
		public static void Exit(GuiWidget window)
		{
			window.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0));
		}

		/// <summary>
		/// Converts pasteboard file URLs ("file:///Users/me/a%20part.mcx") into POSIX paths, passing through
		/// anything that is already a path and skipping entries that are empty.
		/// </summary>
		public static List<string> PathsFromFileUrls(IEnumerable<string> fileUrls)
		{
			var paths = new List<string>();

			if (fileUrls == null)
			{
				return paths;
			}

			foreach (string fileUrl in fileUrls)
			{
				if (string.IsNullOrEmpty(fileUrl))
				{
					continue;
				}

				if (Uri.TryCreate(fileUrl, UriKind.Absolute, out Uri uri)
					&& uri.IsFile)
				{
					paths.Add(uri.LocalPath);
				}
				else
				{
					paths.Add(fileUrl);
				}
			}

			return paths;
		}
	}
}
