using System;
using System.IO;
using System.Reflection;
using System.Xml;

namespace Gaming.Game
{
	public class GameObject
	{
		public GameObject()
		{
		}

		public virtual void WriteGameObjectData(XmlWriter xmlWriter)
		{
			Type gameObjectType = GetType();

			BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			FieldInfo[] fieldsOfGameObject = gameObjectType.GetFields(bindingFlags);
			foreach (FieldInfo fieldOfGameObject in fieldsOfGameObject)
			{
				object[] gameDataAttributes = fieldOfGameObject.GetCustomAttributes(typeof(GameDataAttribute), false);
				if (gameDataAttributes.Length > 0)
				{
					if (gameDataAttributes.Length > 1)
					{
						throw new Exception("You can only have one GameDataAttribute on any given Field.");
					}

					object objectWithAttribute = fieldOfGameObject.GetValue(this);
					if (objectWithAttribute != null)
					{
						var singleGameDataAttribute = (GameDataAttribute)gameDataAttributes[0];
						string name = singleGameDataAttribute.Name;

						if (name.Contains(" "))
						{
							throw new Exception(ToString() + " : '" + name + "' has a space. Attribute names con not contain spaces.");
						}

						xmlWriter.WriteStartElement(name);
						GameDataAttribute.WriteTypeAttributes(xmlWriter, objectWithAttribute);
						xmlWriter.WriteEndAttribute();

						if (objectWithAttribute == null)
						{
							throw new Exception(ToString() + " : " + fieldOfGameObject.ToString() + " must have a default value.\n"
								+ "\n"
								+ "All data marked as [GameData] must be a primitive or a struct or if a class have a DEFALT value or filed initializer.");
						}

						if (objectWithAttribute is GameObject)
						{
							((GameObject)objectWithAttribute).WriteGameObjectData(xmlWriter);
						}
						else
						{
							singleGameDataAttribute.WriteField(xmlWriter, objectWithAttribute);
						}

						xmlWriter.WriteEndElement();
					}
				}
			}
		}

		public virtual void LoadGameObjectData(XmlReader xmlReader)
		{
			while (xmlReader.Read())
			{
				if (xmlReader.NodeType == XmlNodeType.Element)
				{
					string attributeNameForElement = xmlReader.Name;

					BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
					FieldInfo[] fieldsOfGameObject = GetType().GetFields(bindingFlags);
					foreach (FieldInfo fieldOfGameObject in fieldsOfGameObject)
					{
						object[] gameDataAttributes = fieldOfGameObject.GetCustomAttributes(typeof(GameDataAttribute), false);
						if (gameDataAttributes.Length > 0)
						{
							var singleGameDataAttribute = (GameDataAttribute)gameDataAttributes[0];
							string AttributeNameForField = singleGameDataAttribute.Name;
							if (AttributeNameForField == attributeNameForElement)
							{
								if (fieldOfGameObject.FieldType.IsSubclassOf(typeof(GameObject)))
								{
									var newGameObject = (GameObject)GameDataAttribute.ReadTypeAttributes(xmlReader);
									newGameObject.LoadGameObjectData(xmlReader);

									fieldOfGameObject.SetValue(this, newGameObject);
								}
								else
								{
									object objectReadByAttribute = singleGameDataAttribute.ReadField(xmlReader);
									fieldOfGameObject.SetValue(this, objectReadByAttribute);
								}

								break;
							}
						}
					}
				}
				else if (xmlReader.NodeType == XmlNodeType.EndElement)
				{
					break;
				}
			}
		}

		public static GameObject Load(XmlReader xmlReader)
		{
			while (xmlReader.Read())
			{
				if (xmlReader.Name == "RootObject")
				{
					string assemblyString = xmlReader.GetAttribute("Assembly");
					string TypeString = xmlReader.GetAttribute("Type");
					var rootType = Type.GetType(TypeString + ", " + assemblyString);
					var newGameObject = (GameObject)Activator.CreateInstance(rootType);
					newGameObject.LoadGameObjectData(xmlReader);
					return newGameObject;
				}
			}

			return null;
		}

		public static GameObject Load(string pathName)
		{
			FileStream stream;
			try
			{
				stream = File.Open(pathName + ".xml", FileMode.Open);
			}
			catch (FileNotFoundException)
			{
				return null;
			}

			return Load(new XmlTextReader(stream));
		}

		public void SaveXML(XmlWriter xmlWriter)
		{
			xmlWriter.WriteStartElement("RootObject");

			xmlWriter.WriteStartAttribute("Assembly");
			int firstComma = GetType().Assembly.ToString().IndexOf(",");
			xmlWriter.WriteValue(GetType().Assembly.ToString().Substring(0, firstComma));
			xmlWriter.WriteEndAttribute();

			xmlWriter.WriteStartAttribute("Type");
			xmlWriter.WriteValue(GetType().ToString());
			xmlWriter.WriteEndAttribute();

			WriteGameObjectData(xmlWriter);

			xmlWriter.WriteEndElement();
		}

		public void SaveXML(string pathName)
		{
			using (var xmlWriter = new XmlTextWriter(pathName + ".xml", System.Text.Encoding.UTF8))
			{
				xmlWriter.Formatting = Formatting.Indented;
				SaveXML(xmlWriter);
			}
		}

		public virtual void CreateEditor()
		{
			/* TODO: get a method for creating a top level window
					EditorWindow editor = new EditorWindow();
					editor.init(800, 600, PlatformSupportAbstract.WindowFlags.Resizable);
					editor.Caption = "Editing Default Something";
					Type gameObjectType = this.GetType();
					double y_location = 0;

					BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
					System.Reflection.FieldInfo[] gameObjectFields = gameObjectType.GetFields(bindingFlags);
					foreach (FieldInfo gameObjectField in gameObjectFields)
					{
						object[] TestAttributes = gameObjectField.GetCustomAttributes(typeof(GameDataAttribute), false);
						if (TestAttributes.Length > 0)
						{
							GameDataAttribute gameDataAttribute = (GameDataAttribute)TestAttributes[0];
							TextWidget name = new TextWidget(gameDataAttribute.Name, 15);
							Affine transform = new Affine();
							transform.translate(0, y_location);
							name.Transform = transform;
							editor.AddChild(name);

							object test = gameObjectField.GetValue(this);
							TextWidget value = new TextWidget(test.ToString(), 15);
							transform = new Affine();
							transform.translate(name.Width, y_location);
							value.Transform = transform;
							editor.AddChild(value);

							y_location += name.Height;
						}
					}
			 */
		}
	}

}