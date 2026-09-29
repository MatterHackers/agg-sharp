// Copyright (c) 2026, Lars Brubaker
// All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="SelectionControlSize.BoxSize"/> is an opt-in: unset, check boxes and radios keep the classic size
	/// MatterCAD's layouts were built around. It and <see cref="GuiWidget.DeviceScale"/> are process wide, so
	/// these are keyless <c>[NotInParallel]</c>.
	/// </summary>
	[NotInParallel]
	public class SelectionControlSizeTests
	{
		[Test]
		public async Task UnsetKeepsTheClassicCheckBoxSlotAndRadioCircle()
		{
			GuiWidget.DeviceScale = 1;
			SelectionControlSize.BoxSize = null;

			await Assert.That(CheckBoxLabelLeft()).IsEqualTo(20);
			await Assert.That(RadioCircleWidth()).IsEqualTo(11);
		}

		[Test]
		public async Task SetWidensTheCheckBoxSlotAndRadioCircle()
		{
			GuiWidget.DeviceScale = 1;
			SelectionControlSize.BoxSize = 16;
			try
			{
				// 2 focus pad + 16 box + 6 gap before the label.
				await Assert.That(CheckBoxLabelLeft()).IsEqualTo(24);
				await Assert.That(RadioCircleWidth()).IsEqualTo(17);
			}
			finally
			{
				SelectionControlSize.BoxSize = null;
			}
		}

		/// <summary>Where the label starts: the width of the slot the box is drawn in.</summary>
		private static double CheckBoxLabelLeft()
		{
			var view = new CheckBoxViewText("Label");
			var row = (FlowLayoutWidget)view.Children.First();
			return row.Children.First().Width;
		}

		private static double RadioCircleWidth()
		{
			var view = new RadioButtonView();
			return view.RadioCircle.MinimumSize.X;
		}
	}
}
