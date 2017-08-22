using System;
using System.Windows.Forms;
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg.UI
{
	public class WindowsFormsClipboard : ISystemClipboard
	{
		public string GetText()
		{
			return System.Windows.Forms.Clipboard.GetText();
		}

		public void SetText(string text)
		{
			System.Windows.Forms.Clipboard.SetText(text);
		}

		public bool ContainsText()
		{
			return System.Windows.Forms.Clipboard.ContainsText();
		}
	}

	public static class WinformExtensions
	{
		private static GuiWidget widgetToSendTo;
		public static bool EnableInputHook = true;

		public static void HookEvents(this ContainerControl controlToHook, GuiWidget widgetToSendTo2)
		{
			widgetToSendTo = widgetToSendTo2;
			controlToHook.GotFocus += controlToHook_GotFocus;
			controlToHook.LostFocus += controlToHook_LostFocus;

			controlToHook.KeyDown += controlToHook_KeyDown;
			controlToHook.KeyUp += controlToHook_KeyUp;
			controlToHook.KeyPress += controlToHook_KeyPress;

			controlToHook.MouseDown += controlToHook_MouseDown;
			controlToHook.MouseMove += formToHook_MouseMove;
			controlToHook.MouseUp += controlToHook_MouseUp;
			controlToHook.MouseWheel += controlToHook_MouseWheel;

			controlToHook.MouseCaptureChanged += controlToHook_MouseCaptureChanged;

			controlToHook.MouseLeave += controlToHook_MouseLeave;
		}

		private static void controlToHook_MouseLeave(object sender, EventArgs e)
		{
			widgetToSendTo.OnMouseMove(new MatterHackers.Agg.UI.MouseEventArgs(MatterHackers.Agg.UI.MouseButtons.None, 0, -10, -10, 0));
		}

		private static void controlToHook_GotFocus(object sender, EventArgs e)
		{
			widgetToSendTo.OnFocusChanged(e);
		}

		private static void controlToHook_LostFocus(object sender, EventArgs e)
		{
			widgetToSendTo.Unfocus();
			widgetToSendTo.OnFocusChanged(e);
		}

		private static void controlToHook_KeyDown(object sender, System.Windows.Forms.KeyEventArgs windowsKeyEvent)
		{
			if (AggContext.OperatingSystem == OSType.Mac
				&& windowsKeyEvent.KeyCode == System.Windows.Forms.Keys.Cancel)
			{
				windowsKeyEvent = new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Enter | windowsKeyEvent.Modifiers);
			}

			MatterHackers.Agg.UI.KeyEventArgs aggKeyEvent;
			if (AggContext.OperatingSystem == OSType.Mac
				&& (windowsKeyEvent.KeyData & System.Windows.Forms.Keys.Alt) == System.Windows.Forms.Keys.Alt)
			{
				aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)(System.Windows.Forms.Keys.Control | (windowsKeyEvent.KeyData & ~System.Windows.Forms.Keys.Alt)));
			}
			else
			{
				aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)windowsKeyEvent.KeyData);
			}
			widgetToSendTo.OnKeyDown(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, true);

			windowsKeyEvent.Handled = aggKeyEvent.Handled;
			windowsKeyEvent.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;
		}

		private static void controlToHook_KeyUp(object sender, System.Windows.Forms.KeyEventArgs windowsKeyEvent)
		{
			MatterHackers.Agg.UI.KeyEventArgs aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)windowsKeyEvent.KeyData);
			widgetToSendTo.OnKeyUp(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, false);

			windowsKeyEvent.Handled = aggKeyEvent.Handled;
			windowsKeyEvent.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;
		}

		private static void controlToHook_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs windowsKeyPressEvent)
		{
			MatterHackers.Agg.UI.KeyPressEventArgs aggKeyPressEvent = new MatterHackers.Agg.UI.KeyPressEventArgs(windowsKeyPressEvent.KeyChar);
			widgetToSendTo.OnKeyPress(aggKeyPressEvent);
			windowsKeyPressEvent.Handled = aggKeyPressEvent.Handled;
		}

		private static MatterHackers.Agg.UI.MouseEventArgs ConvertWindowsMouseEventToAggMouseEvent(System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			// we invert the y as we are bottom left coordinate system and windows is top left.
			int Y = windowsMouseEvent.Y;
			Y = (int)widgetToSendTo.BoundsRelativeToParent.Height - Y;

			return new MatterHackers.Agg.UI.MouseEventArgs((MatterHackers.Agg.UI.MouseButtons)windowsMouseEvent.Button, windowsMouseEvent.Clicks, windowsMouseEvent.X, Y, windowsMouseEvent.Delta);
		}

		private static void controlToHook_MouseDown(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			widgetToSendTo.OnMouseDown(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
		}

		private static void formToHook_MouseMove(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			// TODO: Remove short term workaround for automation issues where mouse events fire differently if mouse is within window region
			if (!EnableInputHook)
			{
				return;
			}

			widgetToSendTo.OnMouseMove(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
		}

		private static void controlToHook_MouseUp(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			widgetToSendTo.OnMouseUp(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
		}

		private static void controlToHook_MouseCaptureChanged(object sender, EventArgs e)
		{
			if (widgetToSendTo.ChildHasMouseCaptured || widgetToSendTo.MouseCaptured)
			{
				widgetToSendTo.OnMouseUp(new MatterHackers.Agg.UI.MouseEventArgs(MouseButtons.Left, 0, -10, -10, 0));
			}
		}

		private static void controlToHook_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			widgetToSendTo.OnMouseWheel(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
		}
	}
}