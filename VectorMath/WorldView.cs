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
	public class WorldView
	{
		private Matrix4X4 _rotationMatrix = Matrix4X4.Identity;
		private Matrix4X4 _translationMatrix = Matrix4X4.Identity;

		public double Height { get; private set; }

		public double Width { get; private set; }

		public WorldView(double width, double height)
		{
			this.Width = width;
			this.Height = height;

			this.CalculatePerspectiveMatrix(width, height);
			this.CalculateModelviewMatrix();
		}

		public Matrix4X4 InverseModelviewMatrix { get; private set; }

		public Matrix4X4 InverseProjectionMatrix { get; private set; }

		public Matrix4X4 ModelviewMatrix { get; private set; }

		public Matrix4X4 ProjectionMatrix { get; private set; } = Matrix4X4.Identity;

		public Matrix4X4 RotationMatrix
		{
			get => _rotationMatrix;
			set
			{
				if (_rotationMatrix != value)
				{
					_rotationMatrix = value;
					OnTransformChanged(null);
				}
			}
		}

		public double Scale
		{
			get
			{
				var scaledUnitVector = Vector3.UnitX.TransformPosition(this.GetTransform4X4());
				return scaledUnitVector.Length;
			}

			set
			{
				if (Scale > 0)
				{
					double requiredChange = value / Scale;

					TranslationMatrix *= Matrix4X4.CreateScale(requiredChange);
					OnTransformChanged(null);
				}
			}
		}

		public Matrix4X4 TranslationMatrix
		{
			get => _translationMatrix;
			set
			{
				if (_translationMatrix != value)
				{
					_translationMatrix = value;
					OnTransformChanged(null);
				}
			}
		}

		public void CalculateModelviewMatrix()
		{
			this.ModelviewMatrix = this.GetTransform4X4() * Matrix4X4.CreateTranslation(0, 0, -7);
			this.InverseModelviewMatrix = Matrix4X4.Invert(this.ModelviewMatrix);
		}

		public void CalculatePerspectiveMatrix(double width, double height, double zNear = .1, double zFar = 100)
		{
			if (width > 0 && height > 0)
			{
				this.Width = width;
				this.Height = height;

				var fovYRadians = MathHelper.DegreesToRadians(45);
				var aspectWidthOverHeight = width / height;
				Matrix4X4.CreatePerspectiveFieldOfView(fovYRadians, aspectWidthOverHeight, zNear, zFar, out Matrix4X4 projectionMatrix);

				this.ProjectionMatrix = projectionMatrix;
				this.InverseProjectionMatrix = Matrix4X4.Invert(projectionMatrix);
			}
		}

		public void CalculatePerspectiveMatrixOffCenter(double width, double height, double centerOffsetX, double zNear = .1, double zFar = 100)
		{
			if (width > 0 && height > 0)
			{
				this.Width = width;
				this.Height = height;
				var yAngleR = MathHelper.DegreesToRadians(45) / 2;

				var screenDist = height / 2 / Math.Tan(yAngleR);

				var center = width / 2;
				var xAngleL = Math.Atan2(-center - centerOffsetX / 2, screenDist);
				var xAngleR = Math.Atan2(center - centerOffsetX / 2, screenDist);

				// calculate yMin and yMax at the near clip plane
				double yMax = zNear * Math.Tan(yAngleR);
				double yMin = -yMax;
				double xMax = zNear * Math.Tan(xAngleR);
				double xMin = zNear * Math.Tan(xAngleL);

				Matrix4X4.CreatePerspectiveOffCenter(xMin, xMax, yMin, yMax, zNear, zFar, out Matrix4X4 projectionMatrix);

				this.ProjectionMatrix = projectionMatrix;
				this.InverseProjectionMatrix = Matrix4X4.Invert(projectionMatrix);
			}
		}

		public void CalculateOrthogrphicMatrixOffCenter(double width, double height, double centerOffsetX, double zNear = .1, double zFar = 100)
		{
			if (width > 0 && height > 0)
			{
				this.Width = width;
				this.Height = height;
				var yAngleR = MathHelper.DegreesToRadians(45) / 2;

				var screenDist = height / 2 / Math.Tan(yAngleR);

				var center = width / 2;
				var xAngleL = Math.Atan2(-center - centerOffsetX / 2, screenDist);
				var xAngleR = Math.Atan2(center - centerOffsetX / 2, screenDist);

				// calculate yMin and yMax at the near clip plane
				double yMax = zNear * Math.Tan(yAngleR);
				double yMin = -yMax;
				double xMax = zNear * Math.Tan(xAngleR);
				double xMin = zNear * Math.Tan(xAngleL);

				var screenCenter = this.GetWorldPosition(new Vector2(width / 2, height / 2));
				var distToScreen = (EyePosition - screenCenter).Length;

				Matrix4X4.CreateOrthographicOffCenter(xMin, xMax, yMin, yMax, zNear, zFar, out Matrix4X4 projectionMatrix);

				this.ProjectionMatrix = projectionMatrix;
				this.InverseProjectionMatrix = Matrix4X4.Invert(projectionMatrix);
			}
		}
		public Ray GetRayForLocalBounds(Vector2 localPosition)
		{
			var rayClip = new Vector4();
			rayClip.X = (2.0 * localPosition.X) / this.Width - 1.0;
			rayClip.Y = (2.0 * localPosition.Y) / this.Height - 1.0;
			rayClip.Z = -1.0;
			rayClip.W = 1.0;

			var rayEye = Vector4.Transform(rayClip, InverseProjectionMatrix);
			rayEye.Z = -1; rayEye.W = 0;

			var rayWorld = Vector4.Transform(rayEye, InverseModelviewMatrix);

			var finalRayWorld = new Vector3(rayWorld).GetNormal();

			var origin = Vector3.Zero.Transform(InverseModelviewMatrix);

			return new Ray(origin, finalRayWorld);
		}

		public Vector3 EyePosition
		{
			get
			{
				return Vector3.Zero.Transform(InverseModelviewMatrix);
			}
		}

		public Vector2 GetScreenPosition(Vector3 worldPosition)
		{
			var homoginizedViewPosition = worldPosition.Transform(this.ModelviewMatrix);

			var homoginizedScreenPosition = homoginizedViewPosition.TransformPerspective(this.ProjectionMatrix);

			// Screen position
			return new Vector2(homoginizedScreenPosition.X * Width / 2 + Width / 2,
				homoginizedScreenPosition.Y * Height / 2 + Height / 2);
		}

		public Vector3 WorldToScreenSpace(Vector3 worldPosition)
		{
			var viewPosition = worldPosition.Transform(ModelviewMatrix);
			return viewPosition.Transform(ProjectionMatrix);
		}

		public Vector3 ScreenToWorldSpace(Vector3 screenPosition)
		{
			throw new NotImplementedException();
			// this function is not working at this time (it is not the inverse of the WorldToScreenSpace, and needs to be)
			var xxx = screenPosition.Transform(InverseProjectionMatrix);
			return xxx.Transform(InverseModelviewMatrix);
		}

		public Matrix4X4 GetTransform4X4()
		{
			return TranslationMatrix * RotationMatrix;
		}

		public Vector3 GetWorldPosition(Vector2 screenPosition)
		{
			var homoginizedScreenSpace = new Vector4((2.0f * (screenPosition.X / Width)) - 1,
				1 - (2 * (screenPosition.Y / Height)),
				1,
				1);

			Matrix4X4 viewProjection = ModelviewMatrix * ProjectionMatrix;
			var viewProjectionInverse = Matrix4X4.Invert(viewProjection);
			var woldSpace = Vector4.Transform(homoginizedScreenSpace, viewProjectionInverse);

			double perspectiveDivide = 1 / woldSpace.W;

			woldSpace.X *= perspectiveDivide;
			woldSpace.Y *= perspectiveDivide;
			woldSpace.Z *= perspectiveDivide;

			return new Vector3(woldSpace);
		}

		public double GetWorldUnitsPerScreenPixelAtPosition(Vector3 worldPosition, double maxRatio = 5)
		{
			Vector2 screenPosition = GetScreenPosition(worldPosition);

			Ray rayFromScreen = GetRayForLocalBounds(screenPosition);
			double distanceFromOriginToWorldPos = (worldPosition - rayFromScreen.origin).Length;

			Ray rightOnePixelRay = GetRayForLocalBounds(new Vector2(screenPosition.X + 1, screenPosition.Y));
			var rightOnePixel = rightOnePixelRay.origin + rightOnePixelRay.directionNormal * distanceFromOriginToWorldPos;
			double distBetweenPixelsWorldSpace = (rightOnePixel - worldPosition).Length;
			if (distBetweenPixelsWorldSpace > maxRatio)
			{
				return maxRatio;
			}
			return distBetweenPixelsWorldSpace;
		}

		public void OnTransformChanged(EventArgs e)
		{
			this.CalculateModelviewMatrix();
		}

		public void Reset()
		{
			RotationMatrix = Matrix4X4.Identity;
			TranslationMatrix = Matrix4X4.Identity;
		}

		public void Rotate(Quaternion rotation)
		{
			RotationMatrix *= Matrix4X4.CreateRotation(rotation);
		}

		public void RotateAroundPosition(Vector3 worldPosition, Quaternion rotation)
		{
			var newRotation = RotationMatrix * Matrix4X4.CreateRotation(rotation);
			SetRotationHoldPosition(worldPosition, newRotation);
		}

		public void Translate(Vector3 deltaPosition)
		{
			TranslationMatrix = Matrix4X4.CreateTranslation(deltaPosition) * TranslationMatrix;
		}

		public void SetRotationHoldPosition(Vector3 worldPosition, Quaternion newRotation)
		{
			SetRotationHoldPosition(worldPosition, Matrix4X4.CreateRotation(newRotation));
		}

		public void SetRotationHoldPosition(Vector3 worldPosition, Matrix4X4 newRotation)
		{
			// remember where we started on the screen
			var cameraSpaceStart = worldPosition.Transform(this.ModelviewMatrix);

			// do the rotation
			this.RotationMatrix = newRotation;

			// move back to where we started
			var worldStartPostRotation = cameraSpaceStart.Transform(this.InverseModelviewMatrix);
			var delta = worldStartPostRotation - worldPosition;
			this.Translate(delta);
		}
	}
}