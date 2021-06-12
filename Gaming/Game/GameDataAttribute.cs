using System;
using System.Text.RegularExpressions;
using System.Xml;

namespace Gaming.Game
{
	//[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	[AttributeUsage(AttributeTargets.Field)]
	public class GameDataAttribute : Attribute
	{
		private string m_Name;
		private string m_Description;

		public virtual object ReadField(XmlReader xmlReader)
		{
			return xmlReader.ReadElementContentAsObject();
		}

		public virtual void WriteField(XmlWriter xmlWriter, object fieldToWrite)
		{
			xmlWriter.WriteValue(fieldToWrite);
		}

		public static object ReadTypeAttributes(XmlReader xmlReader)
		{
			string typeString = xmlReader.GetAttribute("Type");
			var rootType = Type.GetType(typeString);
			return Activator.CreateInstance(rootType);
		}

		public static void WriteTypeAttributes(XmlWriter xmlWriter, object objectWithType)
		{
			string fullyQualifiedName = objectWithType.GetType().AssemblyQualifiedName;
			var removeExtraCrapSearch = new Regex(", (Version|Culture|PublicKeyToken)=[^,\\]]+");
			string lessQualifiedName = removeExtraCrapSearch.Replace(fullyQualifiedName, "");
			xmlWriter.WriteStartAttribute("Type");
			xmlWriter.WriteValue(lessQualifiedName);
		}

		public GameDataAttribute(string name)
		{
			m_Name = name;
		}

		public string Name
		{
			get
			{
				return m_Name;
			}
		}

		public string Description
		{
			get
			{
				return m_Description;
			}
			set
			{
				m_Description = value;
			}
		}
	}

}