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
			string searchPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

			// Build type lookup
			assemblyAndTypes = new Dictionary<Assembly, List<Type>>();

			string[] dllFiles = Directory.GetFiles(searchPath, "*.dll");
			string[] exeFiles = Directory.GetFiles(searchPath, "*.exe");

			List<string> allFiles = new List<string>();
			allFiles.AddRange(dllFiles);
			allFiles.AddRange(exeFiles);
			string[] files = allFiles.ToArray();

			foreach (var file in files)
			{
				try
				{
					var assembly = Assembly.LoadFile(file);
					var assemblyTypeList = new List<Type>();

					assemblyAndTypes.Add(assembly, assemblyTypeList);

					foreach (var type in assembly.GetTypes())
					{
						if (type == null || !type.IsClass || !type.IsPublic)
						{
							continue;
						}

						assemblyTypeList.Add(type);
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

#if __ANDROID__

		// Technique for loading directly form Android Assets (Requires you create and populate the Assets->StaticData->Plugins
		// folder with the actual plugins you want to load
		Plugins = LoadPluginsFromAssets();

		private string[] pluginsInAssetsFolder = null;

		private byte[] LoadBytesFromStream(string assetsPath, Android.Content.Res.AssetManager assets)
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

		public List<BaseClassToFind> LoadPluginsFromAssets()
		{
			List<BaseClassToFind> factoryList = new List<BaseClassToFind>();

			var assets = Android.App.Application.Context.Assets;

			if(pluginsInAssetsFolder == null)
			{
				pluginsInAssetsFolder = assets.List("StaticData/Plugins");
			}

			List<Assembly> pluginAssemblies = new List<Assembly> ();
			string directory = Path.Combine("StaticData", "Plugins");

			// Iterate the Android Assets in the StaticData/Plugins directory
			foreach (string fileName in assets.List(directory))
			{
				if(Path.GetExtension(fileName) == ".dll")
				{
					try
					{
						string assemblyAssetPath = Path.Combine (directory, fileName);
						Byte[] bytes = LoadBytesFromStream(assemblyAssetPath, assets);

						Assembly assembly;
#if DEBUG
						// If symbols exist for the assembly, load both together to support debug breakpoints
						if(pluginsInAssetsFolder.Contains(fileName + ".mdb"))
						{
							byte[] symbolData = LoadBytesFromStream(assemblyAssetPath + ".mdb", assets);
							assembly = Assembly.Load(bytes, symbolData);
						}
						else
						{
							assembly = Assembly.Load(bytes);
						}
#else
						assembly = Assembly.Load(bytes);
#endif
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

			return constructedTypes;
		}
	}
}