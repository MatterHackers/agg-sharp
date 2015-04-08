namespace MatterHackers.Agg
{
	public class Array2D<dataType>
	{
		private dataType[][] internalArray;

		public Array2D(int width, int height)
		{
			internalArray = new dataType[height][];
			for (int column = 0; column < height; column++)
			{
				internalArray[column] = new dataType[width];
			}
		}

		public int Width { get { return GetRow(0).Length; } }

		public int Height { get { return internalArray.Length; } }

		public dataType[] GetRow(int y)
		{
			return internalArray[y];
		}

		public void Fill(dataType valueToFillWith)
		{
			for (int y = 0; y < Height; y++)
			{
				dataType[] row = GetRow(y);
				for (int x = 0; x < Width; x++)
				{
					row[x] = valueToFillWith;
				}
			}
		}

		public dataType GetValue(int x, int y)
		{
			return GetRow(y)[x];
		}

		public void SetValue(int x, int y, dataType value)
		{
			GetRow(y)[x] = value;
		}
	}
}