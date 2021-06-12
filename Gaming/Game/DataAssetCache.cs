using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Gaming.Game
{
	public class DataAssetCache
	{
		private DataAssetTree m_DataAssetTree;

		private Dictionary<Type, Dictionary<string, GameObject>> m_AssetCache = new Dictionary<Type, Dictionary<string, GameObject>>();

		private static DataAssetCache s_GlobalAssetCache = new DataAssetCache();

		public static DataAssetCache Instance
		{
			get
			{
				return s_GlobalAssetCache;
			}
		}

		private DataAssetCache()
		{
		}

		public void SetAssetTree(DataAssetTree dataAssetTree)
		{
			m_DataAssetTree = dataAssetTree;
		}

		public bool AssetExists(Type gameObjectType, string assetName)
		{
			string pathToAsset = m_DataAssetTree.GetPathToAsset(gameObjectType.Name, assetName);
			if (pathToAsset == null)
			{
				return false;
			}

			return true;
		}

		public GameObject GetCopyOfAsset(Type gameObjectType, string assetName)
		{
			// TODO: this code below seems like it should work and would be better (use the copy of the asset in the cache).
			/*
			GameObject asset = GetAsset(GameObjectType, AssetName);
			MemoryStream memoryStream = new MemoryStream();

			XmlTextWriter xmlWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
			asset.SaveXML(xmlWriter);

			memoryStream.Position = 0;

			XmlTextReader xmlReader = new XmlTextReader(memoryStream);
			return GameObject.Load(xmlReader);
			 */

			string pathToAsset = m_DataAssetTree.GetPathToAsset(gameObjectType.Name, assetName);
			return LoadGameObjectFromDisk(gameObjectType, assetName, pathToAsset);
		}

		public GameObject GetAsset(Type gameObjectType, string assetName)
		{
			if (assetName == null)
			{
				assetName = "!Default";
			}

			GameObject asset = GetAssetFromCache(gameObjectType, assetName);

			if (asset == null)
			{
				string pathToAsset = m_DataAssetTree.GetPathToAsset(gameObjectType.Name, assetName);

				if (pathToAsset == null)
				{
					if (assetName == "!Default")
					{
						// we are trying to load the default item and we don't have one yet.
						ConstructorInfo constructInfo = gameObjectType.GetConstructor(Type.EmptyTypes);
						if (constructInfo == null)
						{
							throw new Exception("You must have a default constructor defined for '" + gameObjectType.Name + "' for default game object creation to work.");
						}

						var defaultGameObjectItem = (GameObject)constructInfo.Invoke(null);
						string defaultAssetPath = m_DataAssetTree.Root + Path.DirectorySeparatorChar + "Default";
						if (!Directory.Exists(defaultAssetPath))
						{
							Directory.CreateDirectory(defaultAssetPath);
						}

						string pathName = defaultAssetPath + Path.DirectorySeparatorChar + assetName + "." + gameObjectType.Name;
						defaultGameObjectItem.SaveXML(pathName);

						AddAssetToCache(gameObjectType, assetName, defaultGameObjectItem);
						return defaultGameObjectItem;
					}
					else
					{
						throw new Exception("'" + gameObjectType.Name + "' named '" + assetName + "' does not exist.");
					}
				}

				GameObject gameObjectItem = LoadGameObjectFromDisk(gameObjectType, assetName, pathToAsset);

				AddAssetToCache(gameObjectType, assetName, gameObjectItem);

				return gameObjectItem;
			}

			return asset;
		}

		private static GameObject LoadGameObjectFromDisk(Type gameObjectType, string assetName, string pathToAsset)
		{
			var paramsLoadTakes = new Type[] { typeof(string) };
			// TODO: more checking for right function. Must be static must return a GameObject.
			MethodInfo loadFunction = gameObjectType.GetMethod("Load", paramsLoadTakes);

			if (loadFunction == null)
			{
				throw new Exception("You must implement the load function on '" + gameObjectType.Name + "'.\n"
					+ "It will look like this, \n 'public new static GameObject Load(String PathName)'.");
			}

			object[] paramsToCallLoadWith = new object[] { pathToAsset };
			GameObject gameObjectItem;
			try
			{
				gameObjectItem = (GameObject)loadFunction.Invoke(null, paramsToCallLoadWith);
			}
			catch (Exception e)
			{
				throw e.InnerException;
			}

			if (gameObjectItem == null)
			{
				throw new Exception("The load failed for the '" + gameObjectType.Name + "' named '" + assetName + "'.");
			}

			return gameObjectItem;
		}

		private GameObject GetAssetFromCache(Type gameObjectType, string assetName)
		{
			if (m_AssetCache.TryGetValue(gameObjectType, out Dictionary<string, GameObject> gameObjectClassDictionary))
			{
				if (gameObjectClassDictionary.TryGetValue(assetName, out GameObject gameObjectItem))
				{
					return gameObjectItem;
				}
			}

			return null;
		}

		private void AddAssetToCache(Type gameObjectType, string assetName, GameObject asset)
		{
			if (!m_AssetCache.TryGetValue(gameObjectType, out Dictionary<string, GameObject> gameObjectClassDictionary))
			{
				// create the dictionary
				gameObjectClassDictionary = new Dictionary<string, GameObject>();
				m_AssetCache.Add(gameObjectType, gameObjectClassDictionary);
			}

			if (gameObjectClassDictionary.TryGetValue(assetName, out GameObject itemInCach))
			{
				throw new Exception("The '" + gameObjectType.Name + "' asset named '" + assetName + "' is already in the cache.");
			}

			gameObjectClassDictionary.Add(assetName, asset);
		}

		public void ModifyOrCreateAsset(GameObject assetToSave, string desiredPathHint, string assetName)
		{
			if (AssetExists(assetToSave.GetType(), assetName))
			{
				// re-save it
				string pathToAsset = m_DataAssetTree.GetPathToAsset(assetToSave.GetType().Name, assetName);
				assetToSave.SaveXML(pathToAsset);
			}
			else
			{
				// create the file and save the asset
				string desiredAssetPath = m_DataAssetTree.Root + Path.DirectorySeparatorChar + desiredPathHint;
				if (!Directory.Exists(desiredAssetPath))
				{
					Directory.CreateDirectory(desiredAssetPath);
				}

				string pathName = desiredAssetPath + Path.DirectorySeparatorChar + assetName + "." + assetToSave.GetType().Name;
				assetToSave.SaveXML(pathName);

				AddAssetToCache(assetToSave.GetType(), assetName, assetToSave);
				m_DataAssetTree.AddItemToTree(pathName);
			}
		}
	}
}