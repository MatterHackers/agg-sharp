/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
	public class TrackBallController
	{
		public enum MouseDownType { None, Translation, Rotation, Scale };

		private const double Epsilon = 1.0e-5;
		private double rotationTrackingRadiusPixels;

		private MouseDownType currentTrackingType = MouseDownType.None;

		private Matrix4X4 localToScreenTransform;

		private Vector2 mouseDownPosition;
		private Vector3 moveSpherePosition;

		private Vector2 lastTranslationMousePosition = Vector2.Zero;
		private Vector2 lastScaleMousePosition = Vector2.Zero;

		public event EventHandler TransformChanged;

		private WorldView world;

		public TrackBallController(WorldView world)
			: this(1, world)
		{
		}

		public TrackBallController(double trackBallRadius, WorldView world)
		{
			mouseDownPosition = new Vector2();
			moveSpherePosition = new Vector3();
			this.rotationTrackingRadiusPixels = trackBallRadius;

			this.world = world;
		}

		public void CopyTransforms(TrackBallController trackBallToCopy)
		{
			rotationTrackingRadiusPixels = trackBallToCopy.rotationTrackingRadiusPixels;
			this.world.RotationMatrix = trackBallToCopy.world.RotationMatrix;
			this.world.TranslationMatrix = trackBallToCopy.world.TranslationMatrix;

			OnTransformChanged(null);
		}

		public MouseDownType CurrentTrackingType
		{
			get
			{
				return currentTrackingType;
			}
		}

		private Vector3 MapMoveToSphere(Vector2 screenPoint, bool rotateOnZ)
		{
			if (rotateOnZ)
			{
				var deltaFromScreenCenter = world.ScreenCenter - screenPoint;

				var angleToTravel = world.ScreenCenter.GetDeltaAngle(mouseDownPosition, screenPoint);

				// now rotate that position about z in the direction of the screen vector
				var positionOnRotationSphere = Vector3.Transform(new Vector3(1, 0, 0), Matrix4X4.CreateRotationZ(angleToTravel/2));

				return positionOnRotationSphere;
			}
			else
			{
				var deltaFromStartPixels = screenPoint - mouseDownPosition;
				var deltaOnSurface = new Vector2(deltaFromStartPixels.x / rotationTrackingRadiusPixels, deltaFromStartPixels.y / rotationTrackingRadiusPixels);

				var lengthOnSurfaceRadi = deltaOnSurface.Length;

				// get this rotation on the surface of the sphere about y
				var positionAboutY = Vector3.Transform(new Vector3(0, 0, 1), Matrix4X4.CreateRotationY(lengthOnSurfaceRadi));

				// get the angle that this distance travels around the sphere
				var angleToTravel = Math.Atan2(deltaOnSurface.y, deltaOnSurface.x);

				// now rotate that position about z in the direction of the screen vector
				var positionOnRotationSphere = Vector3.Transform(positionAboutY, Matrix4X4.CreateRotationZ(angleToTravel));

				return positionOnRotationSphere;
			}
		}

		//Mouse down
		public void OnMouseDown(Vector2 mousePosition, Matrix4X4 screenToLocal, MouseDownType trackType = MouseDownType.Rotation)
		{
			//if (currentTrackingType == MouseDownType.None)
			{
				currentTrackingType = trackType;
				switch (currentTrackingType)
				{
					case MouseDownType.Rotation:
						mouseDownPosition = mousePosition;
						break;

					case MouseDownType.Translation:
						localToScreenTransform = Matrix4X4.Invert(screenToLocal);
						lastTranslationMousePosition = mousePosition;
						break;

					case MouseDownType.Scale:
						lastScaleMousePosition = mousePosition;
						break;

					default:
						throw new NotImplementedException();
				}
			}
		}

		//Mouse drag, calculate rotation
		public void OnMouseMove(Vector2 mousePosition, bool rotateOnZ)
		{
			switch (currentTrackingType)
			{
				case MouseDownType.Rotation:
					var activeRotationQuaternion = Quaternion.Identity;

					//Map the point to the sphere
					moveSpherePosition = MapMoveToSphere(mousePosition, rotateOnZ);

					//Return the quaternion equivalent to the rotation
					//Compute the vector perpendicular to the begin and end vectors
					var rotationStart3D = new Vector3(0, 0, 1);
					if(rotateOnZ)
					{
						rotationStart3D = new Vector3(1, 0, 0);
					}
					Vector3 Perp = Vector3.Cross(rotationStart3D, moveSpherePosition);

					//Compute the length of the perpendicular vector
					if (Perp.Length > Epsilon)
					{
						//if its non-zero
						//We're ok, so return the perpendicular vector as the transform after all
						activeRotationQuaternion.X = Perp.x;
						activeRotationQuaternion.Y = Perp.y;
						activeRotationQuaternion.Z = Perp.z;
						//In the quaternion values, w is cosine (theta / 2), where theta is the rotation angle
						activeRotationQuaternion.W = Vector3.Dot(rotationStart3D, moveSpherePosition);

						mouseDownPosition = mousePosition;
						world.RotationMatrix = world.RotationMatrix * Matrix4X4.CreateRotation(activeRotationQuaternion);
						OnTransformChanged(null);
					}
					break;

				case MouseDownType.Translation:
					{
						Vector2 mouseDelta = mousePosition - lastTranslationMousePosition;
						Vector2 scaledDelta = mouseDelta / world.ScreenCenter.x * 4.75;
						Vector3 offset = new Vector3(scaledDelta.x, scaledDelta.y, 0);
						offset = Vector3.TransformPosition(offset, Matrix4X4.Invert(world.RotationMatrix));
						offset = Vector3.TransformPosition(offset, localToScreenTransform);
						world.TranslationMatrix = world.TranslationMatrix * Matrix4X4.CreateTranslation(offset);
						lastTranslationMousePosition = mousePosition;
						OnTransformChanged(null);
					}
					break;

				case MouseDownType.Scale:
					{
						Vector2 mouseDelta = mousePosition - lastScaleMousePosition;
						double zoomDelta = 1;
						if (mouseDelta.y < 0)
						{
							zoomDelta = 1 - (-1 * mouseDelta.y / 100);
						}
						else if (mouseDelta.y > 0)
						{
							zoomDelta = 1 + (1 * mouseDelta.y / 100);
						}
						world.Scale = world.Scale * zoomDelta;
						lastScaleMousePosition = mousePosition;
						OnTransformChanged(null);
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}

		public void OnMouseUp()
		{
			switch (currentTrackingType)
			{
				case MouseDownType.Rotation:
					break;

				case MouseDownType.Translation:
					//currentTranslationMatrix = Matrix4X4.Identity;
					break;

				case MouseDownType.Scale:
					break;

				default:
					throw new NotImplementedException();
			}
			currentTrackingType = MouseDownType.None;
		}

		public void OnMouseWheel(int wheelDelta)
		{
			double zoomDelta = 1;
			if (wheelDelta > 0)
			{
				zoomDelta = 1.2;
			}
			else if (wheelDelta < 0)
			{
				zoomDelta = .8;
			}

			world.Scale = world.Scale * zoomDelta;
			OnTransformChanged(null);
		}

		private void OnTransformChanged(EventArgs x)
		{
			world.OnTransformChanged(x);
		}

		public double TrackBallRadius
		{
			get
			{
				return rotationTrackingRadiusPixels;
			}

			set
			{
				rotationTrackingRadiusPixels = value;
			}
		}

	}
}