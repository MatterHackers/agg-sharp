/*
Copyright (c) 2018, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MatterHackers.Agg
{
	public static class PluginFinder
	{
		static PluginFinder()
		{
#if __ANDROID__
			LoadAssembliesFromAssets();
#else
			LoadAssembliesFromFileSystem();
#endif
		}

		private static void LoadAssembliesFromFileSystem()
		{
			if (assemblyAndTypes != null)
			{
				return;
			}

			assemblyAndTypes = new Dictionary<Assembly, List<Type>>();

			string searchPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

			// Load plugins from all dlls in the startup directory
			foreach (var file in Directory.GetFiles(searchPath, "*.dll"))
			{
				try
				{
					LoadTypesFromAssembly(Assembly.LoadFile(file));
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("Error loading assembly: " + ex.Message);
				}
			}
		}

		private static void LoadTypesFromAssembly(Assembly assembly)
		{
			var assemblyTypes = new List<Type>();

			foreach (var type in assembly.GetTypes())
			{
				try
				{
					if (type == null || !type.IsClass || !type.IsPublic)
					{
						continue;
					}

					assemblyTypes.Add(type);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("Error adding type: " + ex.Message);
				}
			}

			assemblyAndTypes.Add(assembly, assemblyTypes);
		}

#if __ANDROID__
		private static byte[] LoadBytesFromStream(string assetsPath, Android.Content.Res.AssetManager assets)
		{
			byte[] bytes;
			using (var assetStream = assets.Open(assetsPath)){
				using (var memoryStream = new MemoryStream()){
					assetStream.CopyTo (memoryStream);
					bytes = memoryStream.ToArray();
				}
			}
			return bytes;
		}

		private static void LoadAssembliesFromAssets()
		{
			if (assemblyAndTypes != null)
			{
				return;
			}

			assemblyAndTypes = new Dictionary<Assembly, List<Type>>();

			var assets = Android.App.Application.Context.Assets;

			string pluginsDirectory = "StaticData/Plugins";

			var pluginsInAssetsFolder = assets.List(pluginsDirectory);

			var loadedAssemblies = new List<Assembly>();

			// Iterate Android Assets in the StaticData/Plugins directory, loading each applicable assembly
			foreach (string fileName in pluginsInAssetsFolder)
			{
				if (Path.GetExtension(fileName) == ".dll")
				{
					try
					{
						string assemblyAssetPath = Path.Combine(pluginsDirectory, fileName);
						Byte[] bytes = LoadBytesFromStream(assemblyAssetPath, assets);

						Assembly assembly;
#if DEBUG
						// If symbols exist for the assembly, load both together to support debug breakpoints
						if (pluginsInAssetsFolder.Contains(fileName + ".mdb"))
						{
							byte[] symbolData = LoadBytesFromStream(assemblyAssetPath + ".mdb", assets);
							assembly = Assembly.Load(bytes, symbolData);
						}
						else
#endif
						{
							assembly = Assembly.Load(bytes);
						}

						if (assembly != null)
						{
							loadedAssemblies.Add(assembly);
						}
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine("Error loading assembly: " + ex.Message);
					}
				}
			}

			// After all assemblies are loaded, iterate type data. Iterating before all dependent assemblies are loaded will result in exceptions on assembly.GetTypes()
			foreach (var assembly in loadedAssemblies)
			{
				LoadTypesFromAssembly(assembly);
			}
		}
#endif
		private static Dictionary<Assembly, List<Type>> assemblyAndTypes;

		public static IEnumerable<Type> FindTypes<T>()
		{
			Type targetType = typeof(T);

			return assemblyAndTypes?.SelectMany(kvp => kvp.Value)
						.Where(type => targetType.IsAssignableFrom(type));
		}

		public static List<T> CreateInstancesOf<T>()
		{
			List<T> constructedTypes = new List<T>();
			foreach (var keyValue in assemblyAndTypes)
			{
				try
				{
					Type targetType = typeof(T);

					foreach (var type in keyValue.Value)
					{
						if (targetType.IsInterface && targetType.IsAssignableFrom(type) 
							|| type.BaseType == typeof(T))
						{
							constructedTypes.Add((T)Activator.CreateInstance(type));
						}
					}
				}
				//catch (ReflectionTypeLoadException)	{ }
				//catch (BadImageFormatException) { }
				//catch (NotSupportedException) {	}
				catch(Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("Error loading types: " + ex.Message);
				}
			}

			return constructedTypes;
		}

		public static void Initialize(Assembly assembly)
		{
			LoadTypesFromAssembly(assembly);
		}
	}
}