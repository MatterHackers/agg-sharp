namespace MatterHackers.Agg.UI
{
	public interface ISystemClipboard
	{
		string GetText();

		void SetText(string text);

		bool ContainsText();
	}
}