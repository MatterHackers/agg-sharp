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

		private double trackBallRadius;

		private MouseDownType currentTrackingType = MouseDownType.None;

		private Matrix4X4 localToScreenTransform;

		private Vector2 mouseDownPosition;

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
			this.trackBallRadius = trackBallRadius;

			this.world = world;
		}

		public void CopyTransforms(TrackBallController trackBallToCopy)
		{
			trackBallRadius = trackBallToCopy.trackBallRadius;
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

		public static Vector3 MapMoveToSphere(WorldView world, double trackBallRadius, Vector2 startPosition, Vector2 endPosition, bool rotateOnZ)
		{
			if (rotateOnZ)
			{
				var deltaFromScreenCenter = world.ScreenCenter - endPosition;

				var angleToTravel = world.ScreenCenter.GetDeltaAngle(startPosition, endPosition);

				// now rotate that position about z in the direction of the screen vector
				var positionOnRotationSphere = Vector3.Transform(new Vector3(1, 0, 0), Matrix4X4.CreateRotationZ(angleToTravel/2));

				return positionOnRotationSphere;
			}
			else
			{
				var deltaFromStartPixels = endPosition - startPosition;
				var deltaOnSurface = new Vector2(deltaFromStartPixels.X / trackBallRadius, deltaFromStartPixels.Y / trackBallRadius);

				var lengthOnSurfaceRadi = deltaOnSurface.Length;

				// get this rotation on the surface of the sphere about y
				var positionAboutY = Vector3.Transform(new Vector3(0, 0, 1), Matrix4X4.CreateRotationY(lengthOnSurfaceRadi));

				// get the angle that this distance travels around the sphere
				var angleToTravel = Math.Atan2(deltaOnSurface.Y, deltaOnSurface.X);

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
					Quaternion activeRotationQuaternion = GetRotationForMove(world, trackBallRadius, mouseDownPosition, mousePosition, rotateOnZ);

					if (activeRotationQuaternion != Quaternion.Identity)
					{
						mouseDownPosition = mousePosition;
						world.RotationMatrix = world.RotationMatrix * Matrix4X4.CreateRotation(activeRotationQuaternion);
						OnTransformChanged(null);
					}

					break;

				case MouseDownType.Translation:
					{
						Vector2 mouseDelta = mousePosition - lastTranslationMousePosition;
						Vector2 scaledDelta = mouseDelta / world.ScreenCenter.X * 4.75;
						Vector3 offset = new Vector3(scaledDelta.X, scaledDelta.Y, 0);
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
						if (mouseDelta.Y < 0)
						{
							zoomDelta = 1 - (-1 * mouseDelta.Y / 100);
						}
						else if (mouseDelta.Y > 0)
						{
							zoomDelta = 1 + (1 * mouseDelta.Y / 100);
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

		public static Quaternion GetRotationForMove(WorldView world, double trackBallRadius, Vector2 startPosition, Vector2 endPosition, bool rotateOnZ)
		{
			var activeRotationQuaternion = Quaternion.Identity;

			//Map the point to the sphere
			var moveSpherePosition = MapMoveToSphere(world, trackBallRadius, startPosition, endPosition, rotateOnZ);

			//Return the quaternion equivalent to the rotation
			//Compute the vector perpendicular to the begin and end vectors
			var rotationStart3D = new Vector3(0, 0, 1);
			if (rotateOnZ)
			{
				rotationStart3D = new Vector3(1, 0, 0);
			}
			Vector3 perp = Vector3.Cross(rotationStart3D, moveSpherePosition);

			//Compute the length of the perpendicular vector
			double epsilon = 1.0e-5;
			if (perp.Length > epsilon)
			{
				//if its non-zero
				//We're ok, so return the perpendicular vector as the transform after all
				activeRotationQuaternion.X = perp.X;
				activeRotationQuaternion.Y = perp.Y;
				activeRotationQuaternion.Z = perp.Z;
				//In the quaternion values, w is cosine (theta / 2), where theta is the rotation angle
				activeRotationQuaternion.W = -Vector3.Dot(rotationStart3D, moveSpherePosition);
			}

			return activeRotationQuaternion;
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
				return trackBallRadius;
			}

			set
			{
				trackBallRadius = value;
			}
		}

	}
}