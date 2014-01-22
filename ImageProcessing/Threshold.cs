using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.ImageProcessing
{
    public static class Threshold
    {
        public delegate bool TestThreshold(byte[] buffer, int offset, int threshold);

        public static bool MaxRGB32(byte[] buffer, int offset, int threshold)
        {
            if (buffer[offset + 0] > threshold || buffer[offset + 0] > threshold || buffer[offset + 0] > threshold)
            {
                return true;
            }

            return false;
        }

        public static void DoThreshold(ImageBuffer sourceImageAndDest, int threshold)
        {
            DoThreshold(sourceImageAndDest, sourceImageAndDest, threshold, MaxRGB32);
        }

        public static void DoThreshold(ImageBuffer sourceImageAndDest, int threshold, TestThreshold testFunction)
        {
            DoThreshold(sourceImageAndDest, sourceImageAndDest, threshold, testFunction);
        }

        public static void DoThreshold(ImageBuffer result, ImageBuffer sourceImage, int threshold, TestThreshold testFunction)
        {
            if (sourceImage.BitDepth != result.BitDepth)
            {
                throw new NotImplementedException("All the images have to be the same bit depth.");
            }
            if (sourceImage.Width != result.Width || sourceImage.Height != result.Height)
            {
                throw new Exception("All images must be the same size.");
            }

            switch (sourceImage.BitDepth)
            {
                case 32:
                    {
                        int height = sourceImage.Height;
                        int width = sourceImage.Width;
                        byte[] resultBuffer = result.GetBuffer();
                        byte[] sourceBuffer = sourceImage.GetBuffer();
                        for (int y = 0; y < height; y++)
                        {
                            int offset = sourceImage.GetBufferOffsetY(y);

                            for (int x = 0; x < width; x++)
                            {
                                if(testFunction(sourceBuffer, offset, threshold))
                                {
                                    resultBuffer[offset + 0] = (byte)255;
                                    resultBuffer[offset + 1] = (byte)255;
                                    resultBuffer[offset + 2] = (byte)255;
                                    resultBuffer[offset + 3] = (byte)255;
                                }
                                else
                                {
                                    resultBuffer[offset + 0] = (byte)0;
                                    resultBuffer[offset + 1] = (byte)0;
                                    resultBuffer[offset + 2] = (byte)0;
                                    resultBuffer[offset + 3] = (byte)0;
                                }
                                offset += 4;
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
