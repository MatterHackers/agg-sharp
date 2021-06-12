using MatterHackers.VectorMath;
using System;
using System.Reflection;
using System.Xml;

namespace Gaming.Game
{
	#region GameDataValueAttribute

	[AttributeUsage(AttributeTargets.Field)]
	public class GameDataNumberAttribute : GameDataAttribute
	{
		private double m_MinValue;
		private double m_MaxValue;
		private double m_Increment;

		public GameDataNumberAttribute(string name)
			: base(name)
		{
			m_Increment = 1;
			m_MinValue = double.MinValue;
			m_MaxValue = double.MaxValue;
		}

		public override object ReadField(XmlReader xmlReader)
		{
			return xmlReader.ReadElementContentAsDouble();
		}

		public override void WriteField(XmlWriter xmlWriter, object fieldToWrite)
		{
			if (!fieldToWrite.GetType().IsValueType)
			{
				throw new Exception("You can only put a GameDataNumberAttribute on a ValueType.");
			}

			base.WriteField(xmlWriter, fieldToWrite);
		}

		public double Min
		{
			get
			{
				return m_MinValue;
			}
			set
			{
				m_MinValue = value;
			}
		}

		public double Max
		{
			get
			{
				return m_MaxValue;
			}
			set
			{
				m_MaxValue = value;
			}
		}

		public double Increment
		{
			get
			{
				return m_Increment;
			}
			set
			{
				m_Increment = value;
			}
		}
	}

	#endregion GameDataValueAttribute

	#region GameDataBoolAttribute

	[AttributeUsage(AttributeTargets.Field)]
	public class GameDataBoolAttribute : GameDataAttribute
	{
		public GameDataBoolAttribute(string name)
			: base(name)
		{
		}

		public override object ReadField(XmlReader xmlReader)
		{
			return xmlReader.ReadElementContentAsBoolean();
		}

		public override void WriteField(XmlWriter xmlWriter, object fieldToWrite)
		{
			if (!fieldToWrite.GetType().IsPrimitive)
			{
				throw new Exception("You can only put a GameDataBoolAttribute on a Boolean.");
			}

			base.WriteField(xmlWriter, fieldToWrite);
		}
	}

	#endregion GameDataBoolAttribute

	#region GameDataVector2DAttribute

	[AttributeUsage(AttributeTargets.Field)]
	public class GameDataVector2DAttribute : GameDataAttribute
	{
		public GameDataVector2DAttribute(string name)
			: base(name)
		{
		}

		public override object ReadField(XmlReader xmlReader)
		{
			var newVector2D = (Vector2)GameDataAttribute.ReadTypeAttributes(xmlReader);

			string xString = xmlReader.GetAttribute("x");
			string yString = xmlReader.GetAttribute("y");
			newVector2D = new Vector2(Convert.ToDouble(xString), Convert.ToDouble(yString));

			return newVector2D;
		}

		public override void WriteField(XmlWriter xmlWriter, object fieldToWrite)
		{
			if (!(fieldToWrite is Vector2))
			{
				throw new Exception("You can only put a GameDataVector2DAttribute on a Vector2D.");
			}

			var vector2DToWrite = (Vector2)fieldToWrite;
			xmlWriter.WriteStartAttribute("x");
			xmlWriter.WriteValue(vector2DToWrite.X);
			xmlWriter.WriteEndAttribute();

			xmlWriter.WriteStartAttribute("y");
			xmlWriter.WriteValue(vector2DToWrite.Y);
			xmlWriter.WriteEndAttribute();
		}
	}

	#endregion GameDataVector2DAttribute

	#region GameDataListAttribute

	[AttributeUsage(AttributeTargets.Field)]
	public class GameDataListAttribute : GameDataAttribute
	{
		public GameDataListAttribute(string name)
			: base(name)
		{
		}

		public override object ReadField(XmlReader xmlReader)
		{
			object list = GameDataAttribute.ReadTypeAttributes(xmlReader);

			while (xmlReader.Read())
			{
				if (xmlReader.NodeType == XmlNodeType.Element)
				{
					if (xmlReader.Name == "Item")
					{
						object listItem = GameDataAttribute.ReadTypeAttributes(xmlReader);
						if (listItem is GameObject)
						{
							var listGameObject = (GameObject)listItem;
							listGameObject.LoadGameObjectData(xmlReader);
							MethodInfo addMethod = list.GetType().GetMethod("Add");
							addMethod.Invoke(list, new object[] { listGameObject });
						}
						else
						{
							throw new NotImplementedException("List of non-GameObjects not deserializable");
						}
					}
				}
				else if (xmlReader.NodeType == XmlNodeType.EndElement)
				{
					break;
				}
			}

			return list;
		}

		public override void WriteField(XmlWriter xmlWriter, object fieldToWrite)
		{
			object list = fieldToWrite;

			int listCount = (int)list.GetType().GetProperty("Count").GetValue(list, null);
			for (int index = 0; index < listCount; index++)
			{
				object item = list.GetType().GetMethod("get_Item").Invoke(list, new object[] { index });
				if (item is GameObject)
				{
					xmlWriter.WriteStartElement("Item");

					GameDataAttribute.WriteTypeAttributes(xmlWriter, item);

					((GameObject)item).WriteGameObjectData(xmlWriter);
					xmlWriter.WriteEndElement();
				}
				else
				{
					xmlWriter.WriteValue(item);
					xmlWriter.WriteValue(" ");
				}
			}
		}
	}

	#endregion GameDataListAttribute
}