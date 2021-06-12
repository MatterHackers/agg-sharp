using System;
using System.Collections.Generic;
using System.IO;

namespace Gaming.Game
{
	public class DataAssetTree
	{
		private string m_RootFolderOrZip;
		private Dictionary<string, Dictionary<string, DataAssetTreeItem>> m_AssetTree = new Dictionary<string, Dictionary<string, DataAssetTreeItem>>();
		private List<string> ExtensionExcludeList = new List<string>();

		public class DataAssetTreeItem
		{
			private string m_FullPathName;
			private string m_GameObjectClass;
			private string m_InstanceName;

			public DataAssetTreeItem(string fullPathName)
			{
				m_FullPathName = fullPathName;
				m_GameObjectClass = Path.GetExtension(fullPathName).Substring(1);
				m_InstanceName = Path.GetFileNameWithoutExtension(fullPathName);
			}

			public string FullPathName
			{
				get { return m_FullPathName; }
			}

			public string GameObjectClass
			{
				get { return m_GameObjectClass; }
			}

			public string InstanceName
			{
				get { return m_InstanceName; }
			}
		}

		public DataAssetTree(string rootFolderOrZip)
		{
			ExtensionExcludeList.Add(".svn");

			m_RootFolderOrZip = rootFolderOrZip;
			if (Directory.Exists(m_RootFolderOrZip))
			{
				AddDirectoryToTree(m_RootFolderOrZip);
			}
			else if (File.Exists(m_RootFolderOrZip + ".zip"))
			{
				throw new NotImplementedException("We don't support zip files yet.");
				//AddZipToTree(m_RootFolderOrZip);
			}
			else
			{
				throw new InvalidOperationException("The root folder or zip you specified does not exist.");
			}
		}

		public string Root
		{
			get
			{
				return m_RootFolderOrZip;
			}
		}

		public string GetPathToAsset(string gameObjectClassName, string instanceName)
		{
			if (!m_AssetTree.TryGetValue(gameObjectClassName, out Dictionary<string, DataAssetTreeItem> gameObjectClassDictionary))
			{
				return null;
			}

			if (!gameObjectClassDictionary.TryGetValue(instanceName, out DataAssetTreeItem dataAssetItemItem))
			{
				return null;
			}

			return dataAssetItemItem.FullPathName;
		}

		private void AddZipToTree(string zipToEnumerate)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		private void AddDirectoryToTree(string directoryToEnumerate)
		{
			string[] directories = Directory.GetDirectories(directoryToEnumerate);
			foreach (string directory in directories)
			{
				if (ExtensionExcludeList.Contains(Path.GetExtension(directory)))
				{
				}
				else
				{
					if (Path.HasExtension(directory))
					{
						AddItemToTree(directory);
					}
					else
					{
						AddDirectoryToTree(directory);
					}
				}
			}

			string[] files = Directory.GetFiles(directoryToEnumerate, "*.zip");
			foreach (string file in files)
			{
				AddZipToTree(file);
			}

			files = Directory.GetFiles(directoryToEnumerate, "*.xml");
			foreach (string file in files)
			{
				AddItemToTree(Path.ChangeExtension(file, null));
			}
		}

		public void AddItemToTree(string itemToAddToTree)
		{
			var dataAssetItem = new DataAssetTreeItem(itemToAddToTree);
			if (!m_AssetTree.TryGetValue(dataAssetItem.GameObjectClass, out Dictionary<string, DataAssetTreeItem> gameObjectClassDictionary))
			{
				// create the dictionary
				gameObjectClassDictionary = new Dictionary<string, DataAssetTreeItem>();
				m_AssetTree.Add(dataAssetItem.GameObjectClass, gameObjectClassDictionary);
			}

			if (gameObjectClassDictionary.TryGetValue(dataAssetItem.InstanceName, out DataAssetTreeItem itemOfSameName))
			{
				throw new Exception("The GameDateObjectList '" + dataAssetItem.GameObjectClass + "' already contains an instance named '" + dataAssetItem.InstanceName + "'.\n"
					+ "Please change the name, or delete one of them.\n"
					+ "\n"
					+ "Item 1: " + itemOfSameName.FullPathName + "\n"
					+ "Item 2: " + dataAssetItem.FullPathName);
			}

			gameObjectClassDictionary.Add(dataAssetItem.InstanceName, dataAssetItem);
		}
	}
}