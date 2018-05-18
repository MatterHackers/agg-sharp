using MatterHackers.Agg.Image;
using System.Collections.Specialized;

namespace MatterHackers.Agg.UI
{
	public interface ISystemClipboard
	{
		bool ContainsFileDropList { get; }

		bool ContainsImage { get; }

		bool ContainsText { get; }

		StringCollection GetFileDropList();

		ImageBuffer GetImage();

		string GetText();

		void SetText(string text);
	}
}