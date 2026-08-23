/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A drop down opened near the bottom of the window used to stay open downward and grow a scroll
	/// bar as long as there were more than 50 pixels below it, even when the whole list would have fit
	/// above the anchor. These tests pin the symmetric rule: use the preferred direction if it fits,
	/// otherwise the opposite direction if it fits, otherwise the roomier side with a scroll bar.
	/// </summary>
	[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
	public class PopupDirectionFlipTests
	{
		private const string PopupName = "_OpenMenuContents";
		private const string ContentName = "_topToBottom";

		[Test]
		public async Task FitsAboveButNotBelowOpensUpWithoutScroll()
		{
			var (systemWindow, dropDown) = OpenDropDown(itemCount: 2, itemHeight: 50, anchorBottom: 60, direction: Direction.Down);

			var popup = FindPopup(systemWindow);
			var content = FindContent(popup);

			// It did not fit below (60 of space for a ~100 tall list) but fits easily above
			await Assert.That(popup.Height).IsEqualTo(content.Height).Within(0.001);
			await Assert.That(popup.Position.Y).IsEqualTo(dropDown.Position.Y + dropDown.Height).Within(0.001);
		}

		[Test]
		public async Task FitsBelowOpensDown()
		{
			var (systemWindow, dropDown) = OpenDropDown(itemCount: 2, itemHeight: 50, anchorBottom: 200, direction: Direction.Down);

			var popup = FindPopup(systemWindow);
			var content = FindContent(popup);

			await Assert.That(popup.Height).IsEqualTo(content.Height).Within(0.001);

			// Opening down puts the top of the popup at the bottom of the anchor
			await Assert.That(popup.Position.Y + popup.Height).IsEqualTo(dropDown.Position.Y).Within(0.001);
		}

		[Test]
		public async Task FitsNeitherSideOpensTowardTheLargerSideWithScroll()
		{
			// 5 x 50 = ~250 tall in a 300 tall window - 140 below the anchor, ~134 above it
			var (systemWindow, dropDown) = OpenDropDown(itemCount: 5, itemHeight: 50, anchorBottom: 140, direction: Direction.Down);

			var popup = FindPopup(systemWindow);
			var content = FindContent(popup);

			await Assert.That(popup.Height).IsLessThan(content.Height);
			await Assert.That(popup.Height).IsEqualTo(dropDown.Position.Y - 5).Within(0.001);

			// Below is the roomier side, so it stays open downward
			await Assert.That(popup.Position.Y + popup.Height).IsEqualTo(dropDown.Position.Y).Within(0.001);
		}

		[Test]
		public async Task UpPreferredButOnlyFitsBelowFlipsDown()
		{
			var (systemWindow, dropDown) = OpenDropDown(itemCount: 2, itemHeight: 50, anchorBottom: 210, direction: Direction.Up);

			var popup = FindPopup(systemWindow);
			var content = FindContent(popup);

			await Assert.That(popup.Height).IsEqualTo(content.Height).Within(0.001);
			await Assert.That(popup.Position.Y + popup.Height).IsEqualTo(dropDown.Position.Y).Within(0.001);
			await Assert.That(popup.Position.Y).IsGreaterThanOrEqualTo(0);
		}

		private static (SystemWindow systemWindow, DropDownList dropDown) OpenDropDown(int itemCount, double itemHeight, double anchorBottom, Direction direction)
		{
			var systemWindow = new SystemWindow(400, 300);

			var dropDown = new DropDownList("no selection", Color.Black, direction)
			{
				Name = "dropDown",
			};

			for (int i = 0; i < itemCount; i++)
			{
				var item = dropDown.AddItem($"Item {i}");
				item.MinimumSize = new Vector2(150, itemHeight);
			}

			systemWindow.AddChild(dropDown);
			dropDown.Position = new Vector2(10, anchorBottom);

			dropDown.InvokeClick();

			return (systemWindow, dropDown);
		}

		private static GuiWidget FindPopup(SystemWindow systemWindow)
		{
			return systemWindow.Descendants<GuiWidget>().First(w => w.Name == PopupName);
		}

		private static GuiWidget FindContent(GuiWidget popup)
		{
			return popup.Descendants<GuiWidget>().First(w => w.Name == ContentName);
		}
	}
}
