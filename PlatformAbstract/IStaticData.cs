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
        IEnumerable<string> ReadAllLines(string path);
        Stream OpenSteam(string path);
        void LoadImage(string path, ImageBuffer destImage);
        ImageBuffer LoadImage(string path);

        bool FileExists(string path);

        bool DirectoryExists(string pathToManufacturers);

        IEnumerable<string> GetDirectories(string pathToManufacturers);

        IEnumerable<string> GetFiles(string path);
    }

    public static class StaticData
    {
        public static IStaticData Instance { get; set; }
    }
}
