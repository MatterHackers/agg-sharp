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

using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// ToolTipManager's widget tooltips (<see cref="ToolTipManager.SetToolTipContent"/>) and pointer-following
	/// tooltips (<see cref="ToolTipManager.SetToolTipAtPointer"/>), as agg-gui's Tooltip demo uses them. The
	/// mouse goes through the real <see cref="SystemWindow.OnMouseMove"/> hover walk; only the show delay is
	/// skipped, by calling DoShowToolTip directly, and the poll is run by hand.
	/// </summary>
	/// <remarks>Keyless NotInParallel: the placement reads the process wide <see cref="GuiWidget.DeviceScale"/>.</remarks>
	public class ToolTipContentTests
	{
		[Test]
		[NotInParallel]
		public async Task AContentToolTipStaysOpenWhileHoveredAndShowsANestedToolTip()
		{
			var window = new SystemWindow(800, 600);
			try
			{
				var owner = new GuiWidget(200, 20) { Name = "Owner" };
				window.AddChild(owner);
				owner.Position = new Vector2(100, 400);

				GuiWidget link = null;
				ToolTipManager.SetToolTipContent(owner, () =>
				{
					var panel = new FlowLayoutWidget(FlowDirection.TopToBottom) { Padding = new BorderDouble(5) };
					panel.AddChild(new GuiWidget(150, 20) { Name = "Label", Selectable = false });
					link = new GuiWidget(100, 20) { Name = "Link", ToolTipText = "The tooltip has a tooltip in it!" };
					panel.AddChild(link);
					return panel;
				});

				Move(window, 150, 410);
				await Assert.That(ShowArmed(window)).IsTrue();

				GuiWidget toolTip = window.ToolTipManager.ContentToolTip;
				await Assert.That(toolTip).IsNotNull();
				await Assert.That(toolTip.Parent).IsEqualTo(window);

				// Hung under the owner, left edges aligned, the gap between them 4 design units.
				RectangleDouble bounds = toolTip.BoundsRelativeToParent;
				await Assert.That(bounds.Left).IsEqualTo(100);
				await Assert.That(bounds.Top).IsEqualTo(396);

				// Into the tooltip, over its link: the hover walk finds the link, and the tooltip stays up.
				Vector2 overLink = link.TransformToScreenSpace(link.LocalBounds).Center;
				Move(window, overLink.X, overLink.Y);
				Poll(window);
				await Assert.That(window.ToolTipManager.ContentToolTip).IsEqualTo(toolTip);

				// The link's own tooltip opens on top of it.
				await Assert.That(ShowArmed(window)).IsTrue();
				await Assert.That(window.ToolTipManager.CurrentText).IsEqualTo("The tooltip has a tooltip in it!");
				await Assert.That(window.ToolTipManager.ContentToolTip).IsEqualTo(toolTip);

				// Off both: the tooltip lingers through the grace period, then closes with its nested tooltip.
				Move(window, 600, 100);
				Poll(window);
				await Assert.That(window.ToolTipManager.ContentToolTip).IsEqualTo(toolTip);
				await Task.Delay((int)(ToolTipManager.ContentCloseGrace * 1000) + 50);
				Poll(window);
				await Assert.That(window.ToolTipManager.ContentToolTip).IsNull();
				await Assert.That(toolTip.Parent).IsNull();
				await Assert.That(window.ToolTipManager.CurrentText).IsEqualTo("");
			}
			finally
			{
				window.ToolTipManager.Dispose();
			}
		}

		[Test]
		[NotInParallel]
		public async Task AnAtPointerToolTipFollowsTheMouse()
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			var savedCreateToolTip = ToolTipManager.CreateToolTip;
			var window = new SystemWindow(800, 600);
			try
			{
				GuiWidget.DeviceScale = 1;
				ToolTipManager.CreateToolTip = text => (new GuiWidget(80, 40), (widget, newText) => { });

				var owner = new GuiWidget(300, 20) { ToolTipText = "Move me around!!" };
				window.AddChild(owner);
				owner.Position = new Vector2(100, 400);
				ToolTipManager.SetToolTipAtPointer(owner, true);

				Move(window, 150, 410);
				await Assert.That(ShowArmed(window)).IsTrue();
				GuiWidget toolTip = ShownToolTip(window);

				// Below and right of the mouse, clear of the cursor.
				await Assert.That(toolTip.BoundsRelativeToParent.Left).IsEqualTo(150);
				await Assert.That(toolTip.BoundsRelativeToParent.Top).IsEqualTo(410 - ToolTipManager.CursorClearance);

				Move(window, 320, 405);
				await Assert.That(ShownToolTip(window)).IsEqualTo(toolTip);
				await Assert.That(toolTip.BoundsRelativeToParent.Left).IsEqualTo(320);
				await Assert.That(toolTip.BoundsRelativeToParent.Top).IsEqualTo(405 - ToolTipManager.CursorClearance);
			}
			finally
			{
				window.ToolTipManager.Dispose();
				ToolTipManager.CreateToolTip = savedCreateToolTip;
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		private static void Move(SystemWindow window, double x, double y)
		{
			window.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, x, y, 0));
		}

		/// <summary>Shows the armed tooltip now instead of after its delay.</summary>
		private static bool ShowArmed(SystemWindow window)
		{
			var doShow = typeof(ToolTipManager).GetMethod("DoShowToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
			return (bool)doShow.Invoke(window.ToolTipManager, null);
		}

		/// <summary>One tick of the manager's show/close poll.</summary>
		private static void Poll(SystemWindow window)
		{
			var check = typeof(ToolTipManager).GetMethod("CheckIfNeedToDisplayToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
			check.Invoke(window.ToolTipManager, new object[] { false });
		}

		private static GuiWidget ShownToolTip(SystemWindow window)
		{
			return (GuiWidget)typeof(ToolTipManager).GetField("toolTipWidget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window.ToolTipManager);
		}
	}
}
