using System;
using System.Xml;

namespace Gaming.Game
{
	public class AssetReference<GameObjectType> : GameObject where GameObjectType : GameObject
	{
		[GameData("AssetName")]
		private string m_AssetName = null;

		private GameObjectType m_AssetReference = null;

		public AssetReference(String defaultAssetName)
		{
			m_AssetName = defaultAssetName;
		}

		#region GameObjectStuff

		public AssetReference()
		{
		}

		public new static GameObject Load(String PathName)
		{
			return GameObject.Load(PathName);
		}

		#endregion GameObjectStuff

		public override void WriteGameObjectData(XmlWriter writer)
		{
			writer.WriteStartAttribute("AssetName");
			if (m_AssetName == null)
			{
				writer.WriteValue("!Default");
			}
			else
			{
				writer.WriteValue(m_AssetName);
			}
			writer.WriteEndAttribute();
		}

		public override void LoadGameObjectData(XmlReader xmlReader)
		{
			m_AssetName = xmlReader.GetAttribute("AssetName");
		}

		public GameObjectType Instance
		{
			get
			{
				if (m_AssetReference == null)
				{
					m_AssetReference = (GameObjectType)DataAssetCache.Instance.GetAsset(typeof(GameObjectType), m_AssetName);
				}
				return m_AssetReference;
			}
		}
	};
}