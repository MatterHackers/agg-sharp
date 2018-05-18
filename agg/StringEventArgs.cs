using System;

namespace MatterHackers.Agg
{
	public class StringEventArgs : EventArgs
	{
		public string Data { get; }

		public StringEventArgs(string data)
		{
			this.Data = data;
		}
	}
}