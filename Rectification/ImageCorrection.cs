/*
Copyright (c) 2012, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/
// TODO:
//  * Make sure right and top edge pixels are handled. Currently they are ignored and not processed.
#define MULTI_THREAD

using System;
using System.Collections.Generic;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.Rectification
{
    public class ImageCorrection
    {
        int width;
        int height;
        int[] bilinearDistortedToRectifideOffsetTable;
        int[] bilinearWeightTable;

        const int blendFractionDenominator = 512; // this should be a power of 2

        public bool DoBilinearFilter { get; set; }

        public ImageCorrection(int width, int height, bool doBilinearFilter = true, LensDistortionMapping correctionForLens = null)
        {
            DoBilinearFilter = true;
            this.width = width;
            this.height = height;

            bilinearDistortedToRectifideOffsetTable = new int[width * height * 4];
            bilinearWeightTable = new int[width * height * 4];

            if (correctionForLens != null)
            {
                CreateInverseImageMappingCache(correctionForLens);
            }
        }

        private void CreateInverseImageMappingCache(LensDistortionMapping correctionForLens)
        {
#if MULTI_THREAD
            System.Threading.Tasks.Parallel.For(0, height, y => //  
#else
            for (int y = 0; y < height; y++)
#endif
            {
                for (int x = 0; x < width; x++)
                {
                    int offsetToFirstPixelForBlend = y * width * 4 + x * 4;

                    Vector2 distortedVector = new Vector2(x, y);
                    Vector2 correctedVector = correctionForLens.GetDistorted(distortedVector);
                    if (correctedVector.x < 0 || correctedVector.x >= width - 1 ||
                        correctedVector.y < 0 || correctedVector.y >= height - 1)
                    {
                        bilinearDistortedToRectifideOffsetTable[offsetToFirstPixelForBlend + 0] = 0;
                        bilinearWeightTable[y * width * 4 + x * 4 + 0] = blendFractionDenominator;

                        bilinearDistortedToRectifideOffsetTable[offsetToFirstPixelForBlend + 1] = 0;
                        bilinearWeightTable[y * width * 4 + x * 4 + 1] = 0;

                        bilinearDistortedToRectifideOffsetTable[offsetToFirstPixelForBlend + 2] = 0;
                        bilinearWeightTable[y * width * 4 + x * 4 + 2] = 0;

                        bilinearDistortedToRectifideOffsetTable[offsetToFirstPixelForBlend + 3] = 0;
                        bilinearWeightTable[y * width * 4 + x * 4 + 3] = 0;
                    }
                    else
                    {
                        Point2D correctedPoint = new Point2D((int)correctedVector.x, (int)correctedVector.y);
                        double xFraction = correctedVector.x - correctedPoint.x;
                        double yFraction = correctedVector.y - correctedPoint.y;
                        bilinearDistortedToRectifideOffsetTable[offsetToFirstPixelForBlend + 0] = (correctedPoint.y + 0) * width + correctedPoint.x + 0;
                        bilinearWeightTable[y * width * 4 + x * 4 + 0] = (int)(blendFractionDenominator * (1-xFraction)*(1-yFraction));

                        bilinearDistortedToRectifideOffsetTable[offsetToFirstPixelForBlend + 1] = (correctedPoint.y + 0) * width + correctedPoint.x + 1;
                        bilinearWeightTable[y * width * 4 + x * 4 + 1] = (int)(blendFractionDenominator * (xFraction) * (1 - yFraction));

                        bilinearDistortedToRectifideOffsetTable[offsetToFirstPixelForBlend + 2] = (correctedPoint.y + 1) * width + correctedPoint.x + 0;
                        bilinearWeightTable[y * width * 4 + x * 4 + 2] = (int)(blendFractionDenominator * (1 - xFraction) * (yFraction));

                        bilinearDistortedToRectifideOffsetTable[offsetToFirstPixelForBlend + 3] = (correctedPoint.y + 1) * width + correctedPoint.x + 1;
                        bilinearWeightTable[y * width * 4 + x * 4 + 3] = (int)(blendFractionDenominator * (xFraction) * (yFraction));
                    }
                }
            }
#if MULTI_THREAD
);
#endif
        }

        public void ProcessImage(ImageBuffer distortedImage, ImageBuffer rectifideImage)
        {
            if (distortedImage.BitDepth != 32 || rectifideImage.BitDepth != 32)
            {
                throw new ArgumentException("Both inputs must be 32 bit.");
            }
            if (distortedImage.Width != width || distortedImage.Height != height || rectifideImage.Width != width || rectifideImage.Height != height)
            {
                throw new ArgumentException("Both inputs must be the right size.");
            }

            byte[] distortedBuffer = distortedImage.GetBuffer();
            byte[] rectifideBuffer = rectifideImage.GetBuffer();

            if (DoBilinearFilter)
            {
                for (int i = 0; i < bilinearDistortedToRectifideOffsetTable.Length / 4; i++)
                {
                    int rectifedBufferIndex = i * 4;
                    int pixelOffset = bilinearDistortedToRectifideOffsetTable[i * 4 + 0] * 4;
                    int weight = bilinearWeightTable[i * 4 + 0];
                    int red = distortedBuffer[pixelOffset + 0] * weight;
                    int green = distortedBuffer[pixelOffset + 1] * weight;
                    int blue = distortedBuffer[pixelOffset + 2] * weight;

                    pixelOffset = bilinearDistortedToRectifideOffsetTable[i * 4 + 1] * 4;
                    weight = bilinearWeightTable[i * 4 + 1];
                    red += distortedBuffer[pixelOffset + 0] * weight;
                    green += distortedBuffer[pixelOffset + 1] * weight;
                    blue += distortedBuffer[pixelOffset + 2] * weight;

                    pixelOffset = bilinearDistortedToRectifideOffsetTable[i * 4 + 2] * 4;
                    weight = bilinearWeightTable[i * 4 + 2];
                    red += distortedBuffer[pixelOffset + 0] * weight;
                    green += distortedBuffer[pixelOffset + 1] * weight;
                    blue += distortedBuffer[pixelOffset + 2] * weight;

                    pixelOffset = bilinearDistortedToRectifideOffsetTable[i * 4 + 3] * 4;
                    weight = bilinearWeightTable[i * 4 + 3];
                    red += distortedBuffer[pixelOffset + 0] * weight;
                    green += distortedBuffer[pixelOffset + 1] * weight;
                    blue += distortedBuffer[pixelOffset + 2] * weight;

                    rectifideBuffer[rectifedBufferIndex + 0] = (byte)(red / blendFractionDenominator);
                    rectifideBuffer[rectifedBufferIndex + 1] = (byte)(green / blendFractionDenominator);
                    rectifideBuffer[rectifedBufferIndex + 2] = (byte)(blue / blendFractionDenominator);
                    rectifideBuffer[rectifedBufferIndex + 3] = 255;
                }
            }
            else
            {
                for (int i = 0; i < bilinearDistortedToRectifideOffsetTable.Length / 4; i++)
                {
                    int rectifedBufferIndex = i * 4;
                    int pixelOffset = bilinearDistortedToRectifideOffsetTable[i * 4] * 4;
                    int red = distortedBuffer[pixelOffset + 0]; ;
                    int green = distortedBuffer[pixelOffset + 1];
                    int blue = distortedBuffer[pixelOffset + 2];
                    rectifideBuffer[rectifedBufferIndex + 0] = (byte)red;
                    rectifideBuffer[rectifedBufferIndex + 1] = (byte)green;
                    rectifideBuffer[rectifedBufferIndex + 2] = (byte)blue;
                    rectifideBuffer[rectifedBufferIndex + 3] = 255;
                }
            }

            rectifideImage.MarkImageChanged();
        }
    }
}
