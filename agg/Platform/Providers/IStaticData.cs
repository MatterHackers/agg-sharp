using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.Platform
{
	public interface IStaticData
	{
		bool DirectoryExists(string pathToManufacturers);

		bool FileExists(string path);

		IEnumerable<string> GetDirectories(string pathToManufacturers);

		IEnumerable<string> GetFiles(string path);

		/// <summary>
		/// Loads the specified file from the StaticData/Icons path
		/// </summary>
		/// <param name="path">The file path to load</param>
		/// <returns>An ImageBuffer initialized with data from the given file</returns>
		ImageBuffer LoadIcon(string path, bool invertImage = false);

		ImageBuffer LoadIcon(string path, int width, int height, bool invertImage = false);

		ImageBuffer LoadImage(string path);

		Stream OpenStream(string path);

		string[] ReadAllLines(string path);

		string ReadAllText(string path);

		void LoadImageData(Stream stream, ImageBuffer unScaledImage);

		void LoadSequence(string path, ImageSequence sequence);
	}
}
