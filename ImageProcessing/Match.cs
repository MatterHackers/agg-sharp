/*
Copyright (c) 2015, Lars Brubaker
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

using MatterHackers.Agg.Image;
using System;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.ImageProcessing
{
    public static class Match
    {
        public static void DoMatch(ImageBufferFloat imageToSearch, ImageBufferFloat imageToFind, ImageBufferFloat result)
        {
            result = new ImageBufferFloat(imageToSearch.Width, imageToSearch.Height, 32, new BlenderBGRAFloat());
            if (imageToSearch.Width >= imageToFind.Width
                && imageToSearch.Height >= imageToFind.Height
                && imageToSearch.BitDepth == imageToFind.BitDepth)
            {
                int floatsPerPixel = imageToSearch.BitDepth / 32;
                int searchDistanceBetweenPixels = imageToSearch.GetFloatsBetweenPixelsInclusive();
                int findDistanceBetweenPixels = imageToFind.GetFloatsBetweenPixelsInclusive();
                float[] searchBuffer = imageToSearch.GetBuffer();
                float[] findBuffer = imageToFind.GetBuffer();
                float[] resultBuffer = imageToFind.GetBuffer();
                int resutsBufferOffset = 0;
                for (int matchY = 0; matchY <= imageToSearch.Height - imageToFind.Height; matchY++)
                {
                    for (int matchX = 0; matchX <= imageToSearch.Width - imageToFind.Width; matchX++)
                    {
                        double currentLeastSquares = 0;

                        for (int imageToFindY = 0; imageToFindY < imageToFind.Height; imageToFindY++)
                        {
                            int searchBufferOffset = imageToSearch.GetBufferOffsetXY(matchX, matchY + imageToFindY);
                            int findBufferOffset = imageToFind.GetBufferOffsetY(imageToFindY);
                            for (int findX = 0; findX < imageToFind.Width; findX++)
                            {
                                for (int byteIndex = 0; byteIndex < floatsPerPixel; byteIndex++)
                                {
                                    float aByte = searchBuffer[searchBufferOffset + byteIndex];
                                    float bByte = findBuffer[findBufferOffset + byteIndex];
                                    int difference = (int)aByte - (int)bByte;
                                    currentLeastSquares += difference * difference;
                                }
                                searchBufferOffset += searchDistanceBetweenPixels;
                                findBufferOffset += findDistanceBetweenPixels;
                            }
                        }

                        resultBuffer[resutsBufferOffset] = (float)currentLeastSquares;
                        resutsBufferOffset++;
                    }
                }
            }
        }
    }
}