using System;

namespace MatterHackers.Agg.UI
{
	public static class Clipboard
	{
		private static ISystemClipboard clipboard { get; set; }
		public static void SetSystemClipboard(ISystemClipboard clipBoard)
		{
			clipboard = clipBoard;
		}

		public static ISystemClipboard Instance => clipboard;
	}
}