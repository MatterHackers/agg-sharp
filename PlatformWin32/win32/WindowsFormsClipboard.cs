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
}