namespace MatterHackers.Agg.UI
{
	public class KeyPressEventArgs
	{
		public KeyPressEventArgs(char keyChar)
		{
			this.Handled = false;
			this.KeyChar = keyChar;
		}

		public bool Handled { get; set; }

		public char KeyChar { get; set; }
	}
}