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
		private Matrix4X4 currentRotationMatrix = Matrix4X4.Identity;
		private Matrix4X4 currentTranslationMatrix = Matrix4X4.Identity;

		public WorldView(double width, double height)
		{
			this.width = width;
			this.height = height;

			this.CalculateProjectionMatrix(width, height);
		}

		public void OnTransformChanged(EventArgs e)
		{
			this.CalculateModelviewMatrix();
		}

		public void Reset()
		{
			currentRotationMatrix = Matrix4X4.Identity;
			currentTranslationMatrix = Matrix4X4.Identity;
		}

		public void Translate(Vector3 deltaPosition)
		{
			currentTranslationMatrix = Matrix4X4.CreateTranslation(deltaPosition) * currentTranslationMatrix;
			OnTransformChanged(null);
		}

		public void Rotate(Quaternion rotation)
		{
			currentRotationMatrix = currentRotationMatrix * Matrix4X4.CreateRotation(rotation);
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

					currentTranslationMatrix *= Matrix4X4.CreateScale(requiredChange);
					OnTransformChanged(null);
				}
			}
		}

		public Matrix4X4 RotationMatrix
		{
			get => currentRotationMatrix;
			set
			{
				currentRotationMatrix = value;
				OnTransformChanged(null);
			}
		}

		public Matrix4X4 TranslationMatrix
		{
			get => currentTranslationMatrix;
			set
			{
				currentTranslationMatrix = value;
				OnTransformChanged(null);
			}
		}

		public Matrix4X4 GetTransform4X4()
		{
			return currentTranslationMatrix * RotationMatrix;
		}

		public Vector2 ScreenCenter { get; set; }

		public void CalculateProjectionMatrix(double width, double height)
		{
			if (width > 0 && height > 0)
			{
				this.width = width;
				this.height = height;

				var projectionMatrix = Matrix4X4.Identity;
				Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45), width / height, zNear, zFar, out projectionMatrix);

				// thinking about how to do orthographic
				//projectionMatrix = Matrix4X4.CreateOrthographic(width * 10, height * 10, zNear, zFar);

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

			Ray rightOnePixelRay = GetRayForLocalBounds(new Vector2(screenPosition.x + 1, screenPosition.y));
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
			rayClip.x = (2.0 * localPosition.x) / this.width - 1.0;
			rayClip.y = (2.0 * localPosition.y) / this.height - 1.0;
			rayClip.z = -1.0;
			rayClip.w = 1.0;

			Vector4 rayEye = Vector4.Transform(rayClip, InverseProjectionMatrix);
			rayEye.z = -1; rayEye.w = 0;

			Vector4 rayWorld = Vector4.Transform(rayEye, InverseModelviewMatrix);

			Vector3 finalRayWorld = new Vector3(rayWorld).GetNormal();

			Vector3 origin = Vector3.Transform(Vector3.Zero, InverseModelviewMatrix);

			return new Ray(origin, finalRayWorld);
		}

		public Vector3 GetWorldPosition(Vector2 screenPosition)
		{
			Vector4 homoginizedScreenSpace = new Vector4((2.0f * (screenPosition.x / width)) - 1,
				1 - (2 * (screenPosition.y / height)),
				1,
				1);

			Matrix4X4 viewProjection = ModelviewMatrix * ProjectionMatrix;
			Matrix4X4 viewProjectionInverse = Matrix4X4.Invert(viewProjection);
			Vector4 woldSpace = Vector4.Transform(homoginizedScreenSpace, viewProjectionInverse);

			double perspectiveDivide = 1 / woldSpace.w;

			woldSpace.x *= perspectiveDivide;
			woldSpace.y *= perspectiveDivide;
			woldSpace.z *= perspectiveDivide;

			return new Vector3(woldSpace);
		}

		public Vector2 GetScreenPosition(Vector3 worldPosition)
		{
			Vector3 homoginizedViewPosition = Vector3.Transform(worldPosition, this.ModelviewMatrix);

			Vector3 homoginizedScreenPosition = Vector3.TransformPerspective(homoginizedViewPosition, this.ProjectionMatrix);

			// Screen position
			return new Vector2(homoginizedScreenPosition.x * width / 2 + width / 2,
				homoginizedScreenPosition.y * height / 2 + height / 2);
		}

		public Vector3 GetScreenSpace(Vector3 worldPosition)
		{
			Vector3 viewPosition = Vector3.Transform(worldPosition, ModelviewMatrix);
			return Vector3.Transform(viewPosition, ProjectionMatrix);
		}
	}
}