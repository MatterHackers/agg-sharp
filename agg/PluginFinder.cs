﻿using System;
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
#if __ANDROID__
			Plugins = LoadPluginsFromAssets();
#else
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
#endif

            if (sorter != null)
            {
                Plugins.Sort(sorter);
            }
        }

		private static List<Assembly> pluginAssemblies = null;

		public List<BaseClassToFind> LoadPluginsFromAssets()
		{
			List<BaseClassToFind> factoryList = new List<BaseClassToFind>();

			var assets = Android.App.Application.Context.Assets;

			// Only load application plugin assemblies one time
			if (pluginAssemblies == null) {

				pluginAssemblies = new List<Assembly> ();

				string directory = Path.Combine("StaticData", "Plugins");

				// Iterate the Android Assets in the StaticData/Plugins directory
				foreach (string assemblyPath in assets.List(directory)) 
				{
					if(Path.GetExtension(assemblyPath) == ".dll")
					{
						try
						{
							Byte[] bytes;

							using (var assetStream =  assets.Open (Path.Combine(directory, assemblyPath))) {

								// TODO: This is not the most optimized approach as it results in a duplicate, shortterm copy, however
								// the longer form described by Jon Skeet in the ReadFully implementation seemed too verbose for a late
								// night session
								using (var memoryStream = new MemoryStream())
								{
									assetStream.CopyTo(memoryStream);
									bytes = memoryStream.ToArray();
								}
							}

							Assembly assembly = Assembly.Load(bytes);
							pluginAssemblies.Add(assembly);
						}
						// TODO: All of these exceptions need to be logged!
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
			}

			// Iterate plugin assemblies
			foreach (Assembly assembly in pluginAssemblies)
			{
				// Iterate each type
				foreach (Type type in assembly.GetTypes()) {
					if (type == null || !type.IsClass || !type.IsPublic) {
						continue;
					}

					// Add known/requested types to list
					if (type.BaseType == typeof(BaseClassToFind)) {
						factoryList.Add ((BaseClassToFind)Activator.CreateInstance (type));
					}
				}
			}

			return factoryList;
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
