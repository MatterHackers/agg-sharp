using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MatterHackers.Agg.Tests
{
	[TestFixture]
	public class IVertexSourceTests
	{
		[Test]
		public void CharacterBoundsTest()
		{
			// Validates character bounds computation from IVertexSource
			char[] sampleCharacters = "@ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz{}[]| !\"#$%&?'()*+,-./0123456789".ToCharArray();

			int fontSize = 12;

			var typeface = new StyledTypeFace(LiberationSansFont.Instance, fontSize);

			string filename = $"{nameof(LiberationSansFont)}-{fontSize}.json";

			string testDataPath = TestContext.CurrentContext.ResolveProjectPath(new string[] { "..", "..", "TestData", filename });

			// Project sample string characters to dictionary with character bounds
			var characterBounds = sampleCharacters.ToDictionary(c => c, c => GetCharacterBounds(c, typeface));

			var jsonSettings = new JsonSerializerSettings()
			{
				Converters = new List<JsonConverter>() { new FlatRectangleDoubleConverter() }
			};

			// Update the control data
			if (false)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(testDataPath));

				File.WriteAllText(
					testDataPath,
					JsonConvert.SerializeObject(characterBounds,
					jsonSettings));
			}

			// Load sample data
			string json = File.ReadAllText(testDataPath);
			var controlData = JsonConvert.DeserializeObject<Dictionary<char, RectangleDouble>>(json, jsonSettings);

			// Validate each character against previously computed control data
			foreach (var kvp in characterBounds)
			{
				Assert.IsTrue(controlData.ContainsKey(kvp.Key), "Expected key not found: " + kvp.Key);

				RectangleDouble actual = kvp.Value;
				RectangleDouble expected = controlData[kvp.Key];

				Assert.AreEqual(expected.Left, actual.Left, 0.001, "Bounds Left differ");
				Assert.AreEqual(expected.Bottom, actual.Bottom, 0.001, "Bounds Bottom differ");
				Assert.AreEqual(expected.Right, actual.Right, 0.001, "Bounds Right differ");
				Assert.AreEqual(expected.Top, actual.Top, 0.001, "Bounds Top differ");

				Assert.AreEqual(expected, actual);
			}
		}

		[Test]
		public void CubePolygonCountTest()
		{
			var square = new VertexStorage();
			square.MoveTo(0, 0);
			square.LineTo(100, 0);
			square.LineTo(100, 100);
			square.LineTo(0, 100);
			square.ClosePolygon();

			var polygons = square.CreatePolygons();

			Assert.AreEqual(1, polygons.Count, "One polygon should be created for a simple 4 point cube path");
		}

		[Test]
		public void MoveToCreatesAdditionalPolygonTest()
		{
			// Any MoveTo should always create a new Polygon
			var storage = new VertexStorage();
			storage.MoveTo(0, 0);
			storage.LineTo(100, 0);
			storage.LineTo(100, 100);
			storage.MoveTo(30, 30);
			storage.LineTo(0, 100);
			storage.ClosePolygon();

			var polygons = storage.CreatePolygons();

			Assert.AreEqual(2, polygons.Count, "Two polygons should be created for a path with a floating MoveTo command");
		}

		[Test]
		public void TwoItemPolygonCountTest()
		{
			var square = new VertexStorage();
			square.MoveTo(0, 0);
			square.LineTo(100, 0);
			square.LineTo(100, 100);
			square.LineTo(0, 100);
			square.ClosePolygon();

			var result = square.CombineWith(new Ellipse(Vector2.Zero, 10));

			var polygons = result.CreatePolygons();

			Assert.AreEqual(2, polygons.Count, "Two polygons should be create for a combined square and ellipse");
		}

		[Test]
		public void ThreeItemPolygonCountTest()
		{
			var storage = new VertexStorage();

			// Square
			storage.MoveTo(0, 0);
			storage.LineTo(100, 0);
			storage.LineTo(100, 100);
			storage.LineTo(0, 100);
			storage.ClosePolygon();

			// Triangle
			storage.MoveTo(30, 30);
			storage.LineTo(40, 30);
			storage.LineTo(35, 40);
			storage.ClosePolygon();

			// Small Square
			storage.MoveTo(20, 20);
			storage.LineTo(25, 20);
			storage.LineTo(25, 25);
			storage.LineTo(20, 25);
			storage.ClosePolygon();

			var polygons = storage.CreatePolygons();

			//var image = new ImageBuffer(200, 200);
			//var graphics = image.NewGraphics2D();
			//graphics.Render(new Stroke(storage), Color.Blue);
			//ImageTgaIO.Save(image, @"c:\temp\some.tga");

			Assert.AreEqual(3, polygons.Count, "Three polygons should be create for a two squares and a triangle");
		}

		// Behavior which relies on classic IVertexSource.vertex iteration
		private static RectangleDouble GetCharacterBounds(char character, StyledTypeFace typeface)
		{
			IVertexSource glyphForCharacter = typeface.GetGlyphForCharacter(character, 1);

			glyphForCharacter.rewind(0);

			ShapePath.FlagsAndCommand curCommand;

			var bounds = RectangleDouble.ZeroIntersection;

			do
			{
				curCommand = glyphForCharacter.vertex(out double x, out double y);

				if (curCommand != ShapePath.FlagsAndCommand.Stop
					&& !ShapePath.is_close(curCommand))
				{
					bounds.ExpandToInclude(x, y);
				}
			} while (curCommand != ShapePath.FlagsAndCommand.Stop);

			return bounds;
		}

		private class FlatRectangleDoubleConverter : JsonConverter
		{
			public FlatRectangleDoubleConverter()
			{
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				if (value is RectangleDouble rect)
				{
					serializer.Serialize(writer, new[] { rect.Left, rect.Bottom, rect.Right, rect.Top });
				}
				else
				{
					JArray.FromObject(value).WriteTo(writer);
				}
			}

			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
			{
				if (reader.TokenType == JsonToken.StartArray)
				{
					JToken token = JToken.Load(reader);
					var rect = token.ToObject<double[]>();

					return new RectangleDouble(rect[0], rect[1], rect[2], rect[3]);
				}

				return existingValue;
			}

			public override bool CanConvert(Type objectType)
			{
				return objectType == typeof(RectangleDouble)
					|| objectType == typeof(double[]);
			}
		}
	}
}