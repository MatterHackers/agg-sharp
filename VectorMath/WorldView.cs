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

		public WorldView(double width, double height)
		{
			this.width = width;
			this.height = height;

			this.CalculateProjectionMatrix(width, height);
			this.CalculateModelviewMatrix();
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

		public void Translate(Vector3 deltaPosition)
		{
			TranslationMatrix = Matrix4X4.CreateTranslation(deltaPosition) * TranslationMatrix;
			OnTransformChanged(null);
		}

		public void Rotate(Quaternion rotation)
		{
			RotationMatrix *= Matrix4X4.CreateRotation(rotation);
			OnTransformChanged(null);
		}

		public double Scale
		{
			get
			{
				Vector3 scaledUnitVector = Vector3.TransformPosition(Vector3.UnitX, this.GetTransform4X4());
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

		public Matrix4X4 RotationMatrix
		{
			get => _rotationMatrix;
			set
			{
				_rotationMatrix = value;
				OnTransformChanged(null);
			}
		}

		public Matrix4X4 TranslationMatrix
		{
			get => _translationMatrix;
			set
			{
				_translationMatrix = value;
				OnTransformChanged(null);
			}
		}

		public Matrix4X4 GetTransform4X4()
		{
			return TranslationMatrix * RotationMatrix;
		}

		public void CalculateProjectionMatrixOffCenter(double width, double height, double centerOffsetX)
		{
			if (width > 0 && height > 0)
			{
				this.width = width;
				this.height = height;

				var projectionMatrix = Matrix4X4.Identity;

				var yAngleR = MathHelper.DegreesToRadians(45)/2;

				var screenDist = height / 2 / Math.Tan(yAngleR);

				var center = width / 2;
				var xAngleL = Math.Atan2(-center - centerOffsetX / 2, screenDist);
				var xAngleR = Math.Atan2(center - centerOffsetX / 2, screenDist);

				// calculate yMin and yMax at the near clip plane
				double yMax = zNear * System.Math.Tan(yAngleR);
				double yMin = -yMax;
				double xMax = zNear * System.Math.Tan(xAngleR);
				double xMin = zNear * System.Math.Tan(xAngleL);
				Matrix4X4.CreatePerspectiveOffCenter(xMin, xMax, yMin, yMax, zNear, zFar, out projectionMatrix);

				this.ProjectionMatrix = projectionMatrix;
				this.InverseProjectionMatrix = Matrix4X4.Invert(projectionMatrix);
			}
		}

		public void CalculateProjectionMatrix(double width, double height)
		{
			if (width > 0 && height > 0)
			{
				this.width = width;
				this.height = height;

				var fovYRadians = MathHelper.DegreesToRadians(45);
				var aspectWidthOverHeight = width / height;
				var projectionMatrix = Matrix4X4.Identity;
				Matrix4X4.CreatePerspectiveFieldOfView(fovYRadians, aspectWidthOverHeight, zNear, zFar, out projectionMatrix);

				this.ProjectionMatrix = projectionMatrix;
				this.InverseProjectionMatrix = Matrix4X4.Invert(projectionMatrix);
			}
		}

		public Matrix4X4 ProjectionMatrix { get; private set; } = Matrix4X4.Identity;

		public Matrix4X4 InverseProjectionMatrix { get; private set; }

		public Matrix4X4 ModelviewMatrix { get; private set; }

		public Matrix4X4 InverseModelviewMatrix { get; set; }

		private double width;
		private double height;
		double zNear = .1;
		double zFar = 100;

		public void CalculateModelviewMatrix()
		{
			this.ModelviewMatrix = this.GetTransform4X4() * Matrix4X4.CreateTranslation(0, 0, -7);
			this.InverseModelviewMatrix = Matrix4X4.Invert(this.ModelviewMatrix);
		}

		public double GetWorldUnitsPerScreenPixelAtPosition(Vector3 worldPosition, double maxRatio = 5)
		{
			Vector2 screenPosition = GetScreenPosition(worldPosition);

			Ray rayFromScreen = GetRayForLocalBounds(screenPosition);
			double distanceFromOriginToWorldPos = (worldPosition - rayFromScreen.origin).Length;

			Ray rightOnePixelRay = GetRayForLocalBounds(new Vector2(screenPosition.X + 1, screenPosition.Y));
			Vector3 rightOnePixel = rightOnePixelRay.origin + rightOnePixelRay.directionNormal * distanceFromOriginToWorldPos;
			double distBetweenPixelsWorldSpace = (rightOnePixel - worldPosition).Length;
			if (distBetweenPixelsWorldSpace > maxRatio)
			{
				return maxRatio;
			}
			return distBetweenPixelsWorldSpace;
		}

		public Ray GetRayForLocalBounds(Vector2 localPosition)
		{
			Vector4 rayClip = new Vector4();
			rayClip.X = (2.0 * localPosition.X) / this.width - 1.0;
			rayClip.Y = (2.0 * localPosition.Y) / this.height - 1.0;
			rayClip.Z = -1.0;
			rayClip.W = 1.0;

			Vector4 rayEye = Vector4.Transform(rayClip, InverseProjectionMatrix);
			rayEye.Z = -1; rayEye.W = 0;

			Vector4 rayWorld = Vector4.Transform(rayEye, InverseModelviewMatrix);

			Vector3 finalRayWorld = new Vector3(rayWorld).GetNormal();

			Vector3 origin = Vector3.Transform(Vector3.Zero, InverseModelviewMatrix);

			return new Ray(origin, finalRayWorld);
		}

		public Vector3 GetWorldPosition(Vector2 screenPosition)
		{
			Vector4 homoginizedScreenSpace = new Vector4((2.0f * (screenPosition.X / width)) - 1,
				1 - (2 * (screenPosition.Y / height)),
				1,
				1);

			Matrix4X4 viewProjection = ModelviewMatrix * ProjectionMatrix;
			Matrix4X4 viewProjectionInverse = Matrix4X4.Invert(viewProjection);
			Vector4 woldSpace = Vector4.Transform(homoginizedScreenSpace, viewProjectionInverse);

			double perspectiveDivide = 1 / woldSpace.W;

			woldSpace.X *= perspectiveDivide;
			woldSpace.Y *= perspectiveDivide;
			woldSpace.Z *= perspectiveDivide;

			return new Vector3(woldSpace);
		}

		public Vector2 GetScreenPosition(Vector3 worldPosition)
		{
			Vector3 homoginizedViewPosition = Vector3.Transform(worldPosition, this.ModelviewMatrix);

			Vector3 homoginizedScreenPosition = Vector3.TransformPerspective(homoginizedViewPosition, this.ProjectionMatrix);

			// Screen position
			return new Vector2(homoginizedScreenPosition.X * width / 2 + width / 2,
				homoginizedScreenPosition.Y * height / 2 + height / 2);
		}

		public Vector3 GetScreenSpace(Vector3 worldPosition)
		{
			Vector3 viewPosition = Vector3.Transform(worldPosition, ModelviewMatrix);
			return Vector3.Transform(viewPosition, ProjectionMatrix);
		}
	}
}