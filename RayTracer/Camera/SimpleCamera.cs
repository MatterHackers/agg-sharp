/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.VectorMath;
using System;

namespace MatterHackers.RayTracer
{
	public class SimpleCamera : ICamera
	{
		private double cameraFOV = MathHelper.DegreesToRadians(56);
		private double distanceToCameraPlane;

		public Matrix4X4 axisToWorld;

		public int widthInPixels;
		public int heightInPixels;

		public SimpleCamera(int widthInPixels, int heightInPixels, double fieldOfViewRad)
		{
			if (fieldOfViewRad > 3.14)
			{
				throw new Exception("You need to give the Field of View in radians.");
			}
			cameraFOV = fieldOfViewRad;
			double sin = Math.Sin(cameraFOV / 2);
			distanceToCameraPlane = Math.Cos(cameraFOV / 2) / sin;

			this.widthInPixels = widthInPixels;
			this.heightInPixels = heightInPixels;
		}

		public Vector3 Origin
		{
			get
			{
				return new Vector3(axisToWorld[3, 0], axisToWorld[3, 1], axisToWorld[3, 2]);
			}

			set
			{
				axisToWorld[3, 0] = value.x;
				axisToWorld[3, 1] = value.y;
				axisToWorld[3, 2] = value.z;
			}
		}

		private Vector3 GetDirectionMinus1To1(double screenX, double screenY)
		{
			Vector3 direction = new Vector3();
			double oneOverScale = 1.0 / (widthInPixels / 2.0);
			double x = screenX - widthInPixels / 2.0;
			double y = screenY - heightInPixels / 2.0;
			x *= oneOverScale;
			y *= oneOverScale;
			direction.x = x;
			direction.y = y;
			direction.z = -distanceToCameraPlane;

			direction.Normalize();

			return direction;
		}

		public Ray GetRay(double screenX, double screenY)
		{
			Vector3 origin = Origin;
			Vector3 direction = GetDirectionMinus1To1(screenX, screenY);

			direction = Vector3.TransformVector(direction, axisToWorld);

			Ray ray = new Ray(origin, direction);
			return ray;
		}
	}
}