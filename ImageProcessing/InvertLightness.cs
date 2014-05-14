using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.ImageProcessing
{
    public static class InvertLightness
    {
        public static bool MaxRGB32(byte[] buffer, int offset, int threshold)
        {
            if (buffer[offset + 0] > threshold || buffer[offset + 0] > threshold || buffer[offset + 0] > threshold)
            {
                return true;
            }

            return false;
        }

        public static void DoInvertLightness(ImageBuffer sourceImageAndDest)
        {
            DoInvertLightness(sourceImageAndDest, sourceImageAndDest);
        }

        public static void DoInvertLightness(ImageBuffer result, ImageBuffer sourceImage)
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
                                RGBA_Bytes color = new RGBA_Bytes(resultBuffer[offset + 2], resultBuffer[offset + 1], resultBuffer[offset + 0], resultBuffer[offset + 3]);
                                RGBA_Bytes invertedColor = InvertColor(color);

                                resultBuffer[offset + 0] = invertedColor.blue;
                                resultBuffer[offset + 1] = invertedColor.green;
                                resultBuffer[offset + 2] = invertedColor.blue;
                                resultBuffer[offset + 3] = invertedColor.alpha;

                                offset += 4;
                            }
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public static RGBA_Bytes InvertColor(RGBA_Bytes color)
        {
            RGBA_Floats colorFloat = new RGBA_Floats(color);
            double hue0To1;
            double saturation0To1;
            double lightness0To1;
            colorFloat.GetHSL(out hue0To1, out saturation0To1, out lightness0To1);
            RGBA_Floats colorInvertedFloat = RGBA_Floats.FromHSL(hue0To1, saturation0To1, 1 - lightness0To1);
            RGBA_Bytes invertedColor = new RGBA_Bytes(
                colorInvertedFloat.Red0To255, 
                colorInvertedFloat.Green0To255, 
                colorInvertedFloat.Blue0To255,
                // and don't change the alpha value
                color.alpha);
            return invertedColor;
        }

        public static void AssertDebugNotDefined()
        {
#if DEBUG
            throw new Exception("DEBUG is defined and should not be!");
#endif
        }
    }
}
