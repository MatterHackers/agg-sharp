using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Text.RegularExpressions;

namespace Gaming.Game
{
    //[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    [AttributeUsage(AttributeTargets.Field)]
    public class GameDataAttribute : System.Attribute
    {
        String m_Name;
        String m_Description;

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
            string TypeString = xmlReader.GetAttribute("Type");
            Type rootType = Type.GetType(TypeString);
            return Activator.CreateInstance(rootType);
        }

        public static void WriteTypeAttributes(XmlWriter xmlWriter, object objectWithType)
        {
            string FullyQualifiedName = objectWithType.GetType().AssemblyQualifiedName;
            Regex RemoveExtraCrapSearch = new Regex(", (Version|Culture|PublicKeyToken)=[^,\\]]+");
            string LessQualifiedName = RemoveExtraCrapSearch.Replace(FullyQualifiedName, "");
            xmlWriter.WriteStartAttribute("Type");
            xmlWriter.WriteValue(LessQualifiedName);
        }

        public GameDataAttribute(String Name)
        {
            m_Name = Name;
        }

        public String Name
        {
            get
            {
                return m_Name;
            }
        }

        public String Description
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
    };
}