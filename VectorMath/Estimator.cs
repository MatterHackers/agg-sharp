/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

namespace MatterHackers.VectorMath
{
	public static class Estimator
	{
		public static double GetSecondsForMovement(Vector3 delta,
			double ratePerSecond,
			Vector3 maxAccelerationPerS2,
			Vector3 maxVelocityPerS,
			Vector3 velocitySameAsStopPerS,
			Vector3 speedMultiplier)
		{
			return GetSecondsForMovement(delta.Length,
				ratePerSecond,
				maxAccelerationPerS2.X,
				maxVelocityPerS.X,
				velocitySameAsStopPerS.X,
				speedMultiplier.X);
		}


		public static double GetSecondsForMovement(double delta,
			double ratePerSecond,
			double maxAccelerationPerS2,
			double maxVelocityPerS,
			double velocitySameAsStopPerS,
			double speedMultiplier = 1)
		{
			if (delta == 0)
			{
				return 0;
			}

			double maxVelocityMmPerSx = Math.Min(ratePerSecond, maxVelocityPerS);
			double startingVelocityMmPerS = Math.Min(velocitySameAsStopPerS, maxVelocityMmPerSx);
			double endingVelocityMmPerS = startingVelocityMmPerS;

			double distanceToMaxVelocity = GetDistanceToReachEndingVelocity(startingVelocityMmPerS, maxVelocityMmPerSx, maxAccelerationPerS2);
			if (distanceToMaxVelocity <= delta / 2)
			{
				// we will reach max velocity then run at it and then decelerate
				double accelerationTime = GetTimeToAccelerateDistance(startingVelocityMmPerS, distanceToMaxVelocity, maxAccelerationPerS2) * 2;
				double runningTime = (delta - (distanceToMaxVelocity * 2)) / maxVelocityMmPerSx;
				return (accelerationTime + runningTime) * speedMultiplier;
			}
			else
			{
				// we will accelerate to the center then decelerate
				double accelerationTime = GetTimeToAccelerateDistance(startingVelocityMmPerS, delta / 2, maxAccelerationPerS2) * 2;
				return (accelerationTime) * speedMultiplier;
			}
		}

		private static double GetDistanceToReachEndingVelocity(double startingVelocityMmPerS, double endingVelocityMmPerS, double accelerationMmPerS2)
		{
			double endingVelocityMmPerS2 = endingVelocityMmPerS * endingVelocityMmPerS;
			double startingVelocityMmPerS2 = startingVelocityMmPerS * startingVelocityMmPerS;
			return (endingVelocityMmPerS2 - startingVelocityMmPerS2) / (2.0 * accelerationMmPerS2);
		}

		private static double GetTimeToAccelerateDistance(double startingVelocityMmPerS, double distanceMm, double accelerationMmPerS2)
		{
			// d = vi * t + .5 * a * t^2;
			// t = (√(vi^2+2ad)-vi)/a
			double startingVelocityMmPerS2 = startingVelocityMmPerS * startingVelocityMmPerS;
			double distanceAcceleration2 = 2 * accelerationMmPerS2 * distanceMm;
			return (Math.Sqrt(startingVelocityMmPerS2 + distanceAcceleration2) - startingVelocityMmPerS) / accelerationMmPerS2;
		}
	}
}