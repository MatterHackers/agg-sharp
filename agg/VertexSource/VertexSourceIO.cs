using System.IO;

namespace MatterHackers.Agg.VertexSource
{
	public class VertexSourceIO
	{
		public static void Load(VertexStorage vertexSource, string pathAndFileName)
		{
			vertexSource.Clear();
			string[] allLines = File.ReadAllLines(pathAndFileName);
			foreach (string line in allLines)
			{
				string[] elements = line.Split(',');
				double x = double.Parse(elements[0]);
				double y = double.Parse(elements[1]);
				FlagsAndCommand flagsAndCommand = (FlagsAndCommand)System.Enum.Parse(typeof(FlagsAndCommand), elements[2].Trim());
				for (int i = 3; i < elements.Length; i++)
				{
					flagsAndCommand |= (FlagsAndCommand)System.Enum.Parse(typeof(FlagsAndCommand), elements[i].Trim());
				}

				vertexSource.Add(x, y, flagsAndCommand);
			}
		}

		public static void Save(IVertexSource vertexSource, string pathAndFileName, bool oldStyle = true)
		{
			if (oldStyle)
			{
				using (StreamWriter outFile = new StreamWriter(pathAndFileName))
				{
					vertexSource.Rewind(0);
					double x;
					double y;
					FlagsAndCommand flagsAndCommand = vertexSource.vertex(out x, out y);
					do
					{
						outFile.WriteLine("{0}, {1}, {2}", x, y, flagsAndCommand.ToString());
						flagsAndCommand = vertexSource.vertex(out x, out y);
					}
					while (flagsAndCommand != FlagsAndCommand.Stop);
				}
			}
			else
			{
				using (StreamWriter outFile = new StreamWriter(pathAndFileName))
				{
					foreach (VertexData vertexData in vertexSource.Vertices())
					{
						outFile.WriteLine("{0}, {1}, {2}", vertexData.Position.X, vertexData.Position.Y, vertexData.Command.ToString());
					}
				}
			}
		}
	}
}