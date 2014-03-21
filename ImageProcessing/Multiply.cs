using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;

namespace MatterHackers.ImageProcessing
{
    public class Multiply
    {
        public static void DoMultiply(ImageBuffer result, ImageBuffer imageA, ImageBuffer imageB)
        {
            if (imageA.BitDepth != imageB.BitDepth || imageB.BitDepth != result.BitDepth)
            {
                throw new NotImplementedException("All the images have to be the same bit depth.");
            }
            if (imageA.Width != imageB.Width || imageA.Height != imageB.Height
                || imageA.Width != result.Width || imageA.Height != result.Height)
            {
                throw new Exception("All images must be the same size.");
            }

            switch (imageA.BitDepth)
            {
                case 32:
                    {
                        int height = imageA.Height;
                        int width = imageA.Width;
                        byte[] resultBuffer = result.GetBuffer();
                        byte[] imageABuffer = imageA.GetBuffer();
                        byte[] imageBBuffer = imageB.GetBuffer();
                        for (int y = 0; y < height; y++)
                        {
                            int offsetA = imageA.GetBufferOffsetY(y);
                            int offsetB = imageB.GetBufferOffsetY(y);
                            int offsetResult = result.GetBufferOffsetY(y);

                            for (int x = 0; x < width; x++)
                            {
                                resultBuffer[offsetResult++] = (byte)((imageABuffer[offsetA++] * imageBBuffer[offsetB++]) / 255);
                                resultBuffer[offsetResult++] = (byte)((imageABuffer[offsetA++] * imageBBuffer[offsetB++]) / 255);
                                resultBuffer[offsetResult++] = (byte)((imageABuffer[offsetA++] * imageBBuffer[offsetB++]) / 255);
                                resultBuffer[offsetResult++] = 255; offsetA++; offsetB++;
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
