using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.Agg
{
    public class FileSystemStaticData : IStaticData
    {
        private string MapPath(string path)
        {
            string staticDataPath = Directory.Exists("StaticData") ? "StaticData" : Path.Combine("..", "..", "StaticData");
            return Path.Combine(staticDataPath, path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(MapPath(path));
        }

        public IEnumerable<string> ReadAllLines(string path)
        {
            return File.ReadLines(MapPath(path));
        }

        public Stream OpenSteam(string path)
        {
            return File.OpenRead(MapPath(path));
        }

        public void LoadImage(string path, ImageBuffer destImage)
        {
            using (var imageStream = OpenSteam(path))
            {
                var bitmap = new Bitmap(imageStream);
                ImageIOWindowsPlugin.ConvertBitmapToImage(destImage, bitmap);
            }
        }

        public ImageBuffer LoadImage(string path)
        {
            ImageBuffer temp = new ImageBuffer();
            LoadImage(path, temp);

            return temp;
        }

        public bool FileExists(string path)
        {
            return File.Exists(MapPath(path));
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(MapPath(path));
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return Directory.GetDirectories(MapPath(path));
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return Directory.GetFiles(MapPath(path)).Select(p => p.Substring(p.IndexOf("StaticData") + 11));
        }

        /// <summary>
        /// Loads the specified file from the StaticData/Icons path
        /// </summary>
        /// <param name="path">The file path to load</param>
        /// <returns>An ImageBuffer initialized with data from the given file</returns>
        public ImageBuffer LoadIcon(string path)
        {
            return LoadImage(Path.Combine("Icons", path));
        }

        /// <summary>
        /// Loads the specified file from the StaticData/Icons path
        /// </summary>
        /// <param name="path">The file path to load</param>
        /// <param name="buffer">The ImageBuffer to populate with data from the given file</param>
        public void LoadIcon(string path, ImageBuffer buffer)
        {
            LoadImage(Path.Combine("Icons", path), buffer);
        }
    }
}
