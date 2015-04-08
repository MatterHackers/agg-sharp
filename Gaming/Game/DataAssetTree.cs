using System;
using System.Collections.Generic;
using System.IO;

namespace Gaming.Game
{
	public class DataAssetTree
	{
		private String m_RootFolderOrZip;
		private Dictionary<String, Dictionary<String, DataAssetTreeItem>> m_AssetTree = new Dictionary<string, Dictionary<string, DataAssetTreeItem>>();
		private List<String> ExtensionExcludeList = new List<string>();

		public class DataAssetTreeItem
		{
			private String m_FullPathName;
			private String m_GameObjectClass;
			private String m_InstanceName;

			public DataAssetTreeItem(String fullPathName)
			{
				m_FullPathName = fullPathName;
				m_GameObjectClass = Path.GetExtension(fullPathName).Substring(1);
				m_InstanceName = Path.GetFileNameWithoutExtension(fullPathName);
			}

			public String FullPathName
			{
				get { return m_FullPathName; }
			}

			public String GameObjectClass
			{
				get { return m_GameObjectClass; }
			}

			public String InstanceName
			{
				get { return m_InstanceName; }
			}
		};

		public DataAssetTree(String rootFolderOrZip)
		{
			ExtensionExcludeList.Add(".svn");

			m_RootFolderOrZip = rootFolderOrZip;
			if (Directory.Exists(m_RootFolderOrZip))
			{
				AddDirectoryToTree(m_RootFolderOrZip);
			}
			else if (File.Exists(m_RootFolderOrZip + ".zip"))
			{
				throw new System.NotImplementedException("We don't support zip files yet.");
				//AddZipToTree(m_RootFolderOrZip);
			}
			else
			{
				throw new System.InvalidOperationException("The root folder or zip you specified does not exist.");
			}
		}

		public String Root
		{
			get
			{
				return m_RootFolderOrZip;
			}
		}

		public String GetPathToAsset(String GameObjectClassName, String InstanceName)
		{
			Dictionary<string, DataAssetTreeItem> gameObjectClassDictionary;
			if (!m_AssetTree.TryGetValue(GameObjectClassName, out gameObjectClassDictionary))
			{
				return null;
			}

			DataAssetTreeItem dataAssetItemItem;
			if (!gameObjectClassDictionary.TryGetValue(InstanceName, out dataAssetItemItem))
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
			String[] directories = Directory.GetDirectories(directoryToEnumerate);
			foreach (String directory in directories)
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

			String[] files = Directory.GetFiles(directoryToEnumerate, "*.zip");
			foreach (String file in files)
			{
				AddZipToTree(file);
			}

			files = Directory.GetFiles(directoryToEnumerate, "*.xml");
			foreach (String file in files)
			{
				AddItemToTree(Path.ChangeExtension(file, null));
			}
		}

		public void AddItemToTree(string itemToAddToTree)
		{
			DataAssetTreeItem dataAssetItem = new DataAssetTreeItem(itemToAddToTree);
			Dictionary<string, DataAssetTreeItem> gameObjectClassDictionary;
			if (!m_AssetTree.TryGetValue(dataAssetItem.GameObjectClass, out gameObjectClassDictionary))
			{
				// create the dictionary
				gameObjectClassDictionary = new Dictionary<string, DataAssetTreeItem>();
				m_AssetTree.Add(dataAssetItem.GameObjectClass, gameObjectClassDictionary);
			}

			DataAssetTreeItem itemOfSameName;
			if (gameObjectClassDictionary.TryGetValue(dataAssetItem.InstanceName, out itemOfSameName))
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