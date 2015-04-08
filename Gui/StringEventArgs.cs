using System;

namespace MatterHackers.Agg.UI
{
	public class StringEventArgs : EventArgs
	{
		private string data;

		public string Data { get { return data; } }

		public StringEventArgs(string data)
		{
			this.data = data;
		}
	}
}