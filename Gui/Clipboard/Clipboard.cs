using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
	public static class Clipboard
	{
		private static ISystemClipboard clipboard { get; set; }

		public static bool IsInitialized {
			get { return clipboard != null; }
		}

		public static String GetText()
		{
			return clipboard.GetText();
		}

		public static void SetText(string text)
		{
			if (text != null && text != "")
			{
				clipboard.SetText(text);
			}
		}

		public static bool ContainsText()
		{
			return clipboard.ContainsText();
		}

		public static void SetSystemClipboard(ISystemClipboard clipBoard)
		{
			clipboard = clipBoard;
		}
	}
}
