using MatterHackers.Agg.Image;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.PlatformAbstract
{
    public interface IStaticData
    {
        string ReadAllText(string path);
		string[] ReadAllLines(string path);
        Stream OpenSteam(string path);
        void LoadImage(string path, ImageBuffer destImage);
        ImageBuffer LoadImage(string path);

        bool FileExists(string path);

        bool DirectoryExists(string pathToManufacturers);

        IEnumerable<string> GetDirectories(string pathToManufacturers);

        IEnumerable<string> GetFiles(string path);

        /// <summary>
        /// Loads the specified file from the StaticData/Icons path
        /// </summary>
        /// <param name="path">The file path to load</param>
        /// <returns>An ImageBuffer initialized with data from the given file</returns>
        ImageBuffer LoadIcon(string path);

        /// <summary>
        /// Loads the specified file from the StaticData/Icons path
        /// </summary>
        /// <param name="path">The file path to load</param>
        /// <param name="buffer">The ImageBuffer to populate with data from the given file</param>
        void LoadIcon(string path, ImageBuffer buffer);
    }

    public static class StaticData
    {
        public static IStaticData Instance { get; set; }
    }
}
