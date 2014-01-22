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

using System;
using System.Collections.Generic;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.Rectification
{
    public class LensDistortionMapping
    {
        public int distortedWidth;
        public int distortedHeight;
        public double k1_RadialDistortion;
        public double k2_RadialDistortion;
        public double k3_RadialDistortion;
        public Vector2 distortionCenter;
        public double p1_TangentialDistortion;
        public double p2_TangentialDistortion;

        public Vector2 offsetToFitCorrectedImage;
        public Vector2 scallToFitCorrectedImage = new Vector2(1, 1);

        public LensDistortionMapping(int distortedWidth, int distortedHeight,
            double k1_RadialDistortion = 0, double k2_RadialDistortion = 0, double k3_RadialDistortion = 0,
            Vector2 distortionCenter = default(Vector2),
            double p1_TangentialDistortion = 0,
            double p2_TangentialDistortion = 0,
            Vector2 offsetToFitCorrectedImage = default(Vector2),
            Vector2 scallToFitCorrectedImage = default(Vector2))
        {
            this.distortedWidth = distortedWidth;
            this.distortedHeight = distortedHeight;
            this.k1_RadialDistortion = k1_RadialDistortion;
            this.k2_RadialDistortion = k2_RadialDistortion;
            this.k3_RadialDistortion = k3_RadialDistortion;
            this.distortionCenter = distortionCenter;
            this.p1_TangentialDistortion = p1_TangentialDistortion;
            this.p2_TangentialDistortion = p2_TangentialDistortion;
            this.offsetToFitCorrectedImage = offsetToFitCorrectedImage;
            this.scallToFitCorrectedImage = scallToFitCorrectedImage;

            if (this.scallToFitCorrectedImage.x == 0)
            {
                this.scallToFitCorrectedImage = new Vector2(1, 1);
            }

            DistortionUnitTests.Run();
        }

        public Vector2 GetCorrected(Vector2 distortedImageCoords)
        {
            Vector2 distortedAtOrigin = distortedImageCoords - distortionCenter;
            //distortedAtOrigin.x /= distortedWidth;
            //distortedAtOrigin.y /= distortedHeight;

            Vector2 corrected = Vector2.Zero;

            double radius = distortedAtOrigin.Length;
            double radiusSquared = radius * radius;
            double radiusToFourth = radiusSquared * radiusSquared;
            double radiusToSixth = radiusSquared * radiusToFourth;
            // x2 = x1 ( 1 + k1 r^2 + k2 r^4)
            // y2 = y1 ( 1 + k1 r^2 + k2 r^4)
            corrected = distortedAtOrigin * (1 + k1_RadialDistortion * radiusSquared + k2_RadialDistortion * radiusToFourth + k3_RadialDistortion * radiusToSixth);

#if false
            // x2 += 2 p1 x1 y1 + p2 ( r^2 + 2 x1^2 )
            // y2 += 2 p2 x1 y1 + p1 ( r^2 + 2 y1^2 )
            corrected.x += 2 * p1_TangentialDistortion * distortedAtOrigin.x * distortedAtOrigin.y + p2_TangentialDistortion * (radiusSquared + 2 * distortedAtOrigin.x * distortedAtOrigin.x);
            corrected.y += 2 * p2_TangentialDistortion * distortedAtOrigin.x * distortedAtOrigin.y + p1_TangentialDistortion * (radiusSquared + 2 * distortedAtOrigin.y * distortedAtOrigin.y);
#endif
            //corrected.x *= distortedWidth / 2;
            //corrected.y *= distortedHeight / 2;

            corrected.x *= scallToFitCorrectedImage.x;
            corrected.y *= scallToFitCorrectedImage.y;
            corrected += distortionCenter;

            return corrected;
        }

        public Vector2 GetDistorted(Vector2 corrected)
        {
            // the actual values don't matter so we set them to something reasonable.
            Vector2 minDistorted = corrected - new Vector2(256, 256);
            Vector2 maxDistorted = corrected + new Vector2(256, 256);

            Vector2 minCorrected = GetCorrected(minDistorted);
            Vector2 maxCorrected = GetCorrected(maxDistorted);
            int numIterations = 64;
            for (int i = 0; i < numIterations; i++)
            {
                double xDist = maxDistorted.x - minDistorted.x;
                double yDist = maxDistorted.y - minDistorted.y;
                if (xDist < .01 && yDist < .01)
                {
                    return minDistorted;
                }

                if (minCorrected.x > corrected.x)
                {
                    AdjustPositionIteration(corrected.x, minCorrected.x, ref minDistorted.x, xDist);
                }
                else if (maxCorrected.x < corrected.x)
                {
                    AdjustPositionIteration(corrected.x, maxCorrected.x, ref maxDistorted.x, xDist);
                }
                else if (Math.Abs(minCorrected.x - corrected.x) > Math.Abs(maxCorrected.x - corrected.x))
                {
                    AdjustPositionIteration(corrected.x, minCorrected.x, ref minDistorted.x, xDist);
                }
                else
                {
                    AdjustPositionIteration(corrected.x, maxCorrected.x, ref maxDistorted.x, xDist);
                }

                if (minCorrected.y > corrected.y)
                {
                    AdjustPositionIteration(corrected.y, minCorrected.y, ref minDistorted.y, yDist);
                }
                else if (maxCorrected.y < corrected.y)
                {
                    AdjustPositionIteration(corrected.y, maxCorrected.y, ref maxDistorted.y, yDist);
                }
                else if (Math.Abs(minCorrected.y - corrected.y) > Math.Abs(maxCorrected.y - corrected.y))
                {
                    AdjustPositionIteration(corrected.y, minCorrected.y, ref minDistorted.y, yDist);
                }
                else
                {
                    AdjustPositionIteration(corrected.y, maxCorrected.y, ref maxDistorted.y, yDist);
                }

                minCorrected = GetCorrected(minDistorted);
                maxCorrected = GetCorrected(maxDistorted);
            }

            return minDistorted;
        }

        private static void AdjustPositionIteration(double desiredValue, double calculatedValue, ref double valueToAdjust, double distance)
        {
            if (desiredValue < calculatedValue)
            {
                // move the min out more to 
                valueToAdjust -= distance / 2;
            }
            else // corrected.x > calculatedValue
            {
                valueToAdjust += distance / 2;
            }
        }
    }
}
