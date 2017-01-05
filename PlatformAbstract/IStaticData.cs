using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.PlatformAbstract
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
		ImageBuffer LoadIcon(string path);

		ImageBuffer LoadIcon(string path, int width, int height);

		/// <summary>
		/// Loads the specified file from the StaticData/Icons path
		/// </summary>
		/// <param name="path">The file path to load</param>
		/// <param name="buffer">The ImageBuffer to populate with data from the given file</param>
		void LoadIcon(string path, ImageBuffer buffer);

		void LoadImage(string path, ImageBuffer destImage);

		ImageBuffer LoadImage(string path);

		string MapPath(string path);

		Stream OpenSteam(string path);

		string[] ReadAllLines(string path);

		string ReadAllText(string path);

		void LoadImageData(Stream stream, ImageBuffer unScaledImage);

		void LoadSequence(string path, ImageSequence sequence);
	}

	public static class StaticData
	{
		public static IStaticData Instance { get; set; }
	}
}