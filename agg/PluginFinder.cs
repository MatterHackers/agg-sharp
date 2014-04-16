using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace MatterHackers.Agg
{
    public class PluginFinder<BaseClassToFind>
    {
        public List<BaseClassToFind> Plugins;

        public PluginFinder(string searchDirectory = null, IComparer<BaseClassToFind> sorter = null)
        {
            string searchPath;
            if (searchDirectory == null)
            {
                searchPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            else
            {
                searchPath = Path.GetFullPath(searchDirectory);
            }
            
            Plugins = FindAndAddPlugins(searchPath);
            if (sorter != null)
            {
                Plugins.Sort(sorter);
            }
        }

        public List<BaseClassToFind> FindAndAddPlugins(string searchDirectory)
        {
            List<BaseClassToFind> factoryList = new List<BaseClassToFind>();
            if (Directory.Exists(searchDirectory))
            {
                //string[] files = Directory.GetFiles(searchDirectory, "*_HalFactory.dll");
                string[] dllFiles = Directory.GetFiles(searchDirectory, "*.dll");
                string[] exeFiles = Directory.GetFiles(searchDirectory, "*.exe");

                List<string> allFiles = new List<string>();
                allFiles.AddRange(dllFiles);
                allFiles.AddRange(exeFiles);
                string[] files = allFiles.ToArray();

                foreach (string file in files)
                {
                    try
                    {
                        Assembly assembly = Assembly.LoadFile(file);

                        foreach (Type type in assembly.GetTypes())
                        {
                            if (type == null || !type.IsClass || !type.IsPublic)
                            {
                                continue;
                            }

                            if (type.BaseType == typeof(BaseClassToFind))
                            {
                                factoryList.Add((BaseClassToFind)Activator.CreateInstance(type));
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException)
                    {
                    }
                    catch (BadImageFormatException)
                    {
                    }
                    catch (NotSupportedException)
                    {
                    }
                }
            }

            return factoryList;
        }
    }
}
