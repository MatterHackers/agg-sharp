using MatterHackers.Agg.Image;
using System;

namespace MatterHackers.Agg.ImageProcessing
{
	public static class SubtractImages
	{
		private static int[] lookupSubtractAndClamp;

		private static void CreateLookup()
		{
			if (lookupSubtractAndClamp == null)
			{
				lookupSubtractAndClamp = new int[512];
				for (int i = 0; i < lookupSubtractAndClamp.Length; i++)
				{
					lookupSubtractAndClamp[i] = Math.Max(0, Math.Min(255, i - 255));
				}
			}
		}

		public static void DoSubtract(ImageBuffer result, ImageBuffer imageToSubtractFrom, ImageBuffer imageToSubtract)
		{
			if (lookupSubtractAndClamp == null)
			{
				CreateLookup();
			}

			if (imageToSubtractFrom.BitDepth != imageToSubtract.BitDepth || imageToSubtract.BitDepth != result.BitDepth)
			{
				throw new NotImplementedException("All the images have to be the same bit depth.");
			}
			if (imageToSubtractFrom.Width != imageToSubtract.Width || imageToSubtractFrom.Height != imageToSubtract.Height
				|| imageToSubtractFrom.Width != result.Width || imageToSubtractFrom.Height != result.Height)
			{
				throw new Exception("All images must be the same size.");
			}

			switch (imageToSubtractFrom.BitDepth)
			{
				case 32:
					{
						int height = imageToSubtractFrom.Height;
						int width = imageToSubtractFrom.Width;
						byte[] resultBuffer = result.GetBuffer();
						byte[] imageABuffer = imageToSubtractFrom.GetBuffer();
						byte[] imageBBuffer = imageToSubtract.GetBuffer();
						for (int y = 0; y < height; y++)
						{
							int offset = imageToSubtractFrom.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								resultBuffer[offset] = (byte)lookupSubtractAndClamp[imageABuffer[offset] - imageBBuffer[offset] + 255]; // add 255 to make sure not < 0
								offset++;
								resultBuffer[offset] = (byte)lookupSubtractAndClamp[imageABuffer[offset] - imageBBuffer[offset] + 255];
								offset++;
								resultBuffer[offset] = (byte)lookupSubtractAndClamp[imageABuffer[offset] - imageBBuffer[offset] + 255];
								offset++;
								resultBuffer[offset] = 255;
								offset++;
							}
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}
}