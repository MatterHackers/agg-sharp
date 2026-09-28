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
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Mac;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The mac host sets the cursor from <see cref="MacCursorMap"/> on every OnMouseEnter, and a selector
	/// NSCursor does not have would quietly become the arrow - so the table is checked against the real class.
	/// </summary>
	public class MacCursorMapTests
	{
		/// <summary>
		/// Every public selector the table names must exist on this macOS. The private ones (underscore, and
		/// busyButClickableCursor) are probed at run time and may legitimately be missing, so they are skipped.
		/// </summary>
		[Test]
		public async Task EveryPublicSelectorExistsOnNSCursor()
		{
			IntPtr cursorClass = ObjC.Class("NSCursor");
			foreach (Cursors cursor in Enum.GetValues<Cursors>())
			{
				string selector = MacCursorMap.ToSelectorName(cursor);
				await Assert.That(selector).IsNotNullOrEmpty();

				if (!selector.StartsWith("_") && selector != "busyButClickableCursor")
				{
					await Assert.That(ObjC.RespondsToSelector(cursorClass, ObjC.Sel(selector))).IsTrue()
						.Because($"{cursor} maps to +[NSCursor {selector}]");
				}
			}
		}

		[Test]
		[Arguments(Cursors.Grab, "openHandCursor")]
		[Arguments(Cursors.Grabbing, "closedHandCursor")]
		[Arguments(Cursors.Copy, "dragCopyCursor")]
		[Arguments(Cursors.Alias, "dragLinkCursor")]
		[Arguments(Cursors.ContextMenu, "contextualMenuCursor")]
		[Arguments(Cursors.VerticalText, "IBeamCursorForVerticalLayout")]
		[Arguments(Cursors.ResizeNorth, "resizeUpCursor")]
		[Arguments(Cursors.ResizeWest, "resizeLeftCursor")]
		public async Task TheNewCursorsUseTheirAppKitEquivalents(Cursors cursor, string expected)
		{
			await Assert.That(MacCursorMap.ToSelectorName(cursor)).IsEqualTo(expected);
		}
	}
}
