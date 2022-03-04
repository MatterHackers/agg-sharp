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
		public const double DefaultPerspectiveVFOVDegrees = 45;
		public const double DefaultNearZ = 0.1;
		public const double DefaultFarZ = 100.0;
		// Force this minimum on near Z.
		// As the dynamic near plane will take this value inside an AABB, it'll be the same as the default for now.
		public const double PerspectiveProjectionMinimumNearZ = DefaultNearZ;
		// far / near >= this
		private const double PerspectiveProjectionMinimumFarNearRatio = 1.001;
		// Force this minimum distance between the near and far planes for the orthographic projection.
		private const double OrthographicProjectionMinimumNearFarGap = 0.001;

		// Derive from the perspective minimum near Z.
		public static readonly double OrthographicProjectionMinimumHeight = CalcPerspectiveHeight(PerspectiveProjectionMinimumNearZ, DefaultPerspectiveVFOVDegrees);

		private const double CameraZTranslationFudge = -7;

		public double Height { get; private set; } = 0;
		public double Width { get; private set; } = 0;
		public Vector2 ViewportSize { get { return new Vector2(Width, Height); } }
		/// <summary>
		/// The vertical FOV of the latest perspective projection. Untouched by orthographic projection.
		/// </summary>
		public double VFovDegrees { get; private set; } = 0;
		/// <summary>
		/// The horizontal FOV of the latest perspective projection. Untouched by orthographic projection.
		/// May not be symmetric due to center offset.
		/// </summary>
		public double HFovDegrees { get; private set; } = 0;
		/// <summary>
		/// The height of the near plane in viewspace in either projection mode. For orthographic, this is constant for all Z.
		/// This value is used to maintain orthographic scale during viewport resize.
		/// </summary>
		public double NearPlaneHeightInViewspace { get; private set; } = 0;
		public bool IsOrthographic { get; private set; } = false;
		public Matrix4X4 ModelviewMatrix { get; private set; } = Matrix4X4.Identity;
		public Matrix4X4 ProjectionMatrix { get; private set; } = Matrix4X4.Identity;
		public Matrix4X4 InverseModelviewMatrix { get; private set; } = Matrix4X4.Identity;
		public Matrix4X4 InverseProjectionMatrix { get; private set; } = Matrix4X4.Identity;
		/// <summary>
		/// Signed distance of the near plane from the eye along the forward axis. Positive for perspective projection.
		/// </summary>
		public double NearZ { get; private set; } = 0;
		/// <summary>
		/// Signed distance of the far plane from the eye along the forward axis. Positive for perspective projection.
		/// </summary>
		public double FarZ { get; private set; } = 0;

		private Matrix4X4 _rotationMatrix = Matrix4X4.Identity;
		private Matrix4X4 _translationMatrix = Matrix4X4.Identity;

		public Matrix4X4 RotationMatrix
		{
			get => _rotationMatrix;
			set
			{
				if (_rotationMatrix != value)
				{
#if DEBUG
					if (!value.IsValid())
					{
						int a = 0;
					}
#endif
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
#if DEBUG
					if (!value.IsValid())
					{
						int a = 0;
					}
#endif
					_translationMatrix = value;
					OnTransformChanged(null);
				}
			}
		}

		public void CalculateModelviewMatrix()
		{
			this.ModelviewMatrix = this.GetTransform4X4() * Matrix4X4.CreateTranslation(0, 0, CameraZTranslationFudge);
			this.InverseModelviewMatrix = Matrix4X4.Invert(this.ModelviewMatrix);
		}

		public Vector3 EyePosition
		{
			get
			{
				return Vector3.Zero.Transform(InverseModelviewMatrix);
			}

			set
			{
				this.Translate(EyePosition - value);
			}
		}

		/// <summary>
		/// Initialises with a typical perspective projection and a default camera transform.
		/// </summary>
		/// <param name="width">Width of the viewport.</param>
		/// <param name="height">Height of the viewport.</param>
		public WorldView(double width, double height)
		{
			this.CalculatePerspectiveMatrix(width, height);
			this.CalculateModelviewMatrix();
		}


		/// <summary>
		/// Sets a typical perspective projection with an FOV of 45.
		/// </summary>
		/// <param name="width">Width of the viewport.</param>
		/// <param name="height">Height of the viewport.</param>
		/// <param name="zNear">Positive position of the near plane along the forward axis.</param>
		/// <param name="zFar">Positive position of the far plane along the forward axis.</param>
		public void CalculatePerspectiveMatrix(
			double width, double height,
			double zNear = DefaultNearZ, double zFar = DefaultFarZ)
		{
			CalculatePerspectiveMatrixOffCenter(width, height, 0, zNear, zFar);
		}

		public static void SanitisePerspectiveNearFar(ref double near, ref double far)
		{
			near = Math.Max(near, PerspectiveProjectionMinimumNearZ);
			far = Math.Max(far, near * PerspectiveProjectionMinimumFarNearRatio);
		}

		/// <param name="distance">Signed distance from the camera to the plane.</param>
		/// <param name="vfovDegrees">Symmetric vertical FOV in degrees between the top and bottom.</param>
		/// <returns>The visible height within the FOV. Positive for positive arguments.</returns>
		public static double CalcPerspectiveHeight(double distance, double vfovDegrees)
		{
			return distance * 2 * Math.Tan(MathHelper.DegreesToRadians(vfovDegrees) * 0.5);
		}

		/// <param name="height">Signed visible height within the FOV.</param>
		/// <param name="vfovDegrees">Symmetric vertical FOV in degrees between the top and bottom.</param>
		/// <returns>Distance from the camera to the plane. Positive for positive arguments.</returns>
		public static double CalcPerspectiveDistance(double height, double vfovDegrees)
		{
			return height * 0.5 / Math.Tan(MathHelper.DegreesToRadians(vfovDegrees) * 0.5);
		}

		/// <param name="distance">Signed distance from the camera to the plane.</param>
		/// <param name="height">Signed visible height within the FOV.</param>
		/// <returns>The resulting symmetric vertical FOV in degrees between the top and bottom. Positive for positive arguments.</returns>
		public static double CalcPerspectiveVFOVDegreesFromDistanceAndHeight(double distance, double height)
		{
			return MathHelper.RadiansToDegrees(Math.Atan(height * 0.5 / distance) * 2);
		}


		/// <param name="distance">Signed distance from the camera to the plane.</param>
		/// <param name="vfovDegrees">Symmetric vertical FOV in degrees between the top and bottom.</param>
		/// <returns>The visible height within the FOV. Positive for positive arguments.</returns>
		public static double CalcPerspectiveHeight(double distance, double vfovDegrees)
		{
			return distance * 2 * Math.Tan(MathHelper.DegreesToRadians(vfovDegrees) * 0.5);
		}

		/// <param name="height">Signed visible height within the FOV.</param>
		/// <param name="vfovDegrees">Symmetric vertical FOV in degrees between the top and bottom.</param>
		/// <returns>Distance from the camera to the plane. Positive for positive arguments.</returns>
		public static double CalcPerspectiveDistance(double height, double vfovDegrees)
		{
			return height * 0.5 / Math.Tan(MathHelper.DegreesToRadians(vfovDegrees) * 0.5);
		}

		/// <param name="distance">Signed distance from the camera to the plane.</param>
		/// <param name="height">Signed visible height within the FOV.</param>
		/// <returns>The resulting symmetric vertical FOV in degrees between the top and bottom. Positive for positive arguments.</returns>
		public static double CalcPerspectiveVFOVDegreesFromDistanceAndHeight(double distance, double height)
		{
			return MathHelper.RadiansToDegrees(Math.Atan(height * 0.5 / distance) * 2);
		}

		/// <summary>
		/// Sets a perspective projection with a given center adjustment and FOV.
		/// </summary>
		/// <param name="width">Width of the viewport.</param>
		/// <param name="height">Height of the viewport.</param>
		/// <param name="centerOffsetX">Offset of the right edge, to adjust the position of the viewport's center.</param>
		/// <param name="zNear">Positive position of the near plane along the forward axis.</param>
		/// <param name="zFar">Positive position of the far plane along the forward axis.</param>
		/// <param name="vfovDegrees">Vertical FOV in degrees.</param>
		public void CalculatePerspectiveMatrixOffCenter(
			double width, double height,
			double centerOffsetX,
			double zNear = DefaultNearZ, double zFar = DefaultFarZ,
			double vfovDegrees = DefaultPerspectiveVFOVDegrees)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			SanitisePerspectiveNearFar(ref zNear, ref zFar);

			var screenDist = CalcPerspectiveDistance(height, vfovDegrees);
			var center = width / 2;
			var xAngleL = Math.Atan2(-center - centerOffsetX / 2, screenDist);
			var xAngleR = Math.Atan2(center - centerOffsetX / 2, screenDist);

			// calculate yMin and yMax at the near clip plane
			double yMax = CalcPerspectiveHeight(zNear, vfovDegrees) * 0.5;
			double yMin = -yMax;
			double xMax = zNear * Math.Tan(xAngleR);
			double xMin = zNear * Math.Tan(xAngleL);

			Matrix4X4.CreatePerspectiveOffCenter(xMin, xMax, yMin, yMax, zNear, zFar, out Matrix4X4 projectionMatrix);

			this.ProjectionMatrix = projectionMatrix;
			this.InverseProjectionMatrix = Matrix4X4.Invert(projectionMatrix);
			this.Width = width;
			this.Height = height;
			this.VFovDegrees = vfovDegrees;
			this.HFovDegrees = MathHelper.RadiansToDegrees(Math.Abs(xAngleR - xAngleL));
			this.NearPlaneHeightInViewspace = yMax * 2;
			this.NearZ = zNear;
			this.FarZ = zFar;
			this.IsOrthographic = false;
		}

		public static void SanitiseOrthographicNearFar(ref double near, ref double far)
		{
			far = Math.Max(far, near + WorldView.OrthographicProjectionMinimumNearFarGap);
			if (far <= near)
			{
				// zNear is so large that the addition didn't make a difference.
				// Hope it's not infinity and do something multiplicative instead.
				if (near >= 0)
					far = near * 2;
				else
					far = near / 2;
			}
		}

		/// <summary>
		/// Sets an orthographic projection with an explicit height in viewspace.
		/// </summary>
		/// <param name="width">Width of the viewport.</param>
		/// <param name="height">Height of the viewport.</param>
		/// <param name="centerOffsetX">Offset of the right edge, to adjust the position of the viewport's center.</param>
		/// <param name="heightInViewspace">The height of the projection in viewspace.</param>
		/// <param name="zNear">Signed position of the near plane along the forward axis.</param>
		/// <param name="zFar">Signed position of the far plane along the forward axis.</param>
		public void CalculateOrthogrphicMatrixOffCenterWithViewspaceHeight(
			double width, double height,
			double centerOffsetX,
			double heightInViewspace,
			double zNear = DefaultNearZ, double zFar = DefaultFarZ)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			heightInViewspace = Math.Max(heightInViewspace, OrthographicProjectionMinimumHeight);
			SanitiseOrthographicNearFar(ref zNear, ref zFar);

			double effectiveViewWidth = Math.Max(1, width + centerOffsetX);
			double screenCenterX = effectiveViewWidth / 2;
			double xMax = heightInViewspace / height * (width - screenCenterX);
			double xMin = heightInViewspace / height * -screenCenterX;
			double yMax = heightInViewspace / 2;
			double yMin = -yMax;

			Matrix4X4.CreateOrthographicOffCenter(xMin, xMax, yMin, yMax, zNear, zFar, out Matrix4X4 projectionMatrix);

			this.Width = width;
			this.Height = height;
			this.ProjectionMatrix = projectionMatrix;
			this.InverseProjectionMatrix = Matrix4X4.Invert(projectionMatrix);
			this.NearPlaneHeightInViewspace = heightInViewspace;
			this.NearZ = zNear;
			this.FarZ = zFar;
			this.IsOrthographic = true;
		}
		
		/// <param name="worldPosition">Position in worldspace.</param>
		/// <returns>[0, 0]..[Width, Height] (+Y is up)</returns>
		public Vector2 GetScreenPosition(Vector3 worldPosition)
		{
			var viewspace = WorldspaceToViewspace(worldPosition);
			var ndc = ViewspaceToNDC(viewspace);
			return NDCToBottomScreenspace(ndc.Xy);
		}

		/// <param name="worldPosition">Position in worldspace.</param>
		/// <returns>NDC before the perspective divide (clip-space).</returns>
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

		/// <param name="screenPosition">Screenspace coordinate with bottom-left origin.</param>
		/// <returns>
		/// A ray in worldspace along all points at the given position.
		/// In perspective mode, the origin is EyePosition.
		/// In orthographic mode, the origin is on the near plane with infinite extent in both directions (Ray.minDistanceToConsider is -inf).
		/// </returns>
		public Ray GetRayForLocalBounds(Vector2 screenPosition)
		{
			var nearNDC = BottomScreenspaceToNDC(new Vector3(screenPosition, -1));
			var nearViewspacePosition = NDCToViewspace(nearNDC);
			if (IsOrthographic)
			{
				return new Ray(nearViewspacePosition.TransformPosition(InverseModelviewMatrix), -Vector3.UnitZ.TransformVector(InverseModelviewMatrix).GetNormal(),
					minDistanceToConsider: double.NegativeInfinity);
			}
			else
			{
				return new Ray(EyePosition, nearViewspacePosition.TransformVector(InverseModelviewMatrix).GetNormal());
			}
		}

		public Matrix4X4 GetTransform4X4()
		{
			return TranslationMatrix * RotationMatrix;
		}

		// This code just doesn't look right... (and appears to be unused)
		/*
		public Vector3 GetWorldPosition(Vector2 screenPosition)
		{
			var homoginizedScreenSpace = new Vector4((2.0f * (screenPosition.X / Width)) - 1,
				1 - (2 * (screenPosition.Y / Height)),
				1,
				1);

			var unprojected = Vector4.Transform(homoginizedScreenSpace, InverseProjectionMatrix);
			var worldSpace2 = Vector4.Transform(unprojected, InverseModelviewMatrix);

			return new Vector3(worldSpace2);

			Matrix4X4 viewProjection = ModelviewMatrix * ProjectionMatrix;
			var viewProjectionInverse = Matrix4X4.Invert(viewProjection);
			var woldSpace = Vector4.Transform(homoginizedScreenSpace, viewProjectionInverse);

			double perspectiveDivide = 1 / woldSpace.W;

			woldSpace.X *= perspectiveDivide;
			woldSpace.Y *= perspectiveDivide;
			woldSpace.Z *= perspectiveDivide;

			return new Vector3(woldSpace);
		}
		*/

		public double GetViewspaceHeightAtPosition(Vector3 viewspacePosition)
		{
			if (this.IsOrthographic)
				return NearPlaneHeightInViewspace;
			else
				return NearPlaneHeightInViewspace * viewspacePosition.Z / -NearZ;
		}

		/// <param name="worldPosition">Position in worldspace.</param>
		/// <returns>
		/// Units per screenspace X in worldspace at the given position.
		/// Always positive unless underflow or NaN occurs.
		/// The absolute value is taken and clamped to a minimum derived from the minimum allowed near plane.
		/// </returns>
		// NOTE: Original implementation always returns non-negative and callers depend on non-zero.
		public double GetWorldUnitsPerScreenPixelAtPosition(Vector3 worldPosition, double maxRatio = 5)
		{
			Vector3 viewspace = WorldspaceToViewspace(worldPosition);
			double viewspaceUnitsPerPixel = GetViewspaceHeightAtPosition(viewspace) / Height;
			double minMagnitude = GetViewspaceHeightAtPosition(new Vector3(0, 0, -PerspectiveProjectionMinimumNearZ)) / Height;
			viewspaceUnitsPerPixel = Math.Max(Math.Abs(viewspaceUnitsPerPixel), minMagnitude);
			double worldspaceXUnitsPerPixel = new Vector3(viewspaceUnitsPerPixel, 0, 0).TransformVector(InverseModelviewMatrix).Length;
			return Math.Min(worldspaceXUnitsPerPixel, maxRatio);
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

		/// <param name="worldspacePosition">Position in worldspace.</param>
		/// <returns>[0..0]..[Width, Height], if on-screen (+Y is up)</returns>
		public Vector3 WorldspaceToBottomScreenspace(Vector3 worldspacePosition)
		{
			Vector3 viewspace = WorldspaceToViewspace(worldspacePosition);
			Vector3 ndc = ViewspaceToNDC(viewspace);
			return NDCToBottomScreenspace(ndc);
		}

		/// <param name="worldspacePosition">Worldspace</param>
		/// <returns>[l, b, -near]..[r, t, -far] (if ortho)</returns>
		private Vector3 WorldspaceToViewspace(Vector3 worldspacePosition)
		{
			return worldspacePosition.TransformPosition(ModelviewMatrix);
		}

		/// <param name="viewspacePosition">[l, b, -near]..[r, t, -far] (if ortho)</param>
		/// <returns>[-1, -1, -1]..[1, 1, 1]</returns>
		private Vector3 ViewspaceToNDC(Vector3 viewspacePosition)
		{
			var v = Vector4.Transform(new Vector4(viewspacePosition, 1), ProjectionMatrix);
			return v.Xyz / v.W;
		}

		/// <param name="ndcPosition">[-1, -1, -1]..[1, 1, 1]</param>
		/// <returns>[l, b, -near]..[r, t, -far] (if ortho)</returns>
		public Vector3 NDCToViewspace(Vector3 ndcPosition)
		{
			var v = Vector4.Transform(new Vector4(ndcPosition, 1), InverseProjectionMatrix);
			return v.Xyz / v.W;
		}

		/// <param name="ndcPosition">[-1, -1]..[1, 1] (+Y is up)</param>
		/// <returns>[0, 0]..[Width, Height] (+Y is down)</returns>
		private Vector2 NDCToTopScreenspace(Vector2 ndcPosition)
		{
			return Vector2.Multiply(Vector2.Multiply(ndcPosition - new Vector2(-1, 1), ViewportSize), new Vector2(0.5, -0.5));
		}

		/// <param name="ndcPosition">[-1, -1]..[1, 1] (+Y is up)</param>
		/// <returns>[0, 0]..[Width, Height] (+Y is up)</returns>
		private Vector2 NDCToBottomScreenspace(Vector2 ndcPosition)
		{
			return Vector2.Multiply(Vector2.Multiply(ndcPosition - new Vector2(-1, -1), ViewportSize), new Vector2(0.5, 0.5));
		}

		/// <param name="screenspacePosition">[0, 0]..[Width, Height] (+Y is up)</param>
		/// <returns>[-1, -1]..[1, 1] (+Y is up)</returns>
		private Vector2 BottomScreenspaceToNDC(Vector2 screenspacePosition)
		{
			return Vector2.Divide(Vector2.Multiply(screenspacePosition, new Vector2(2, 2)), ViewportSize) + new Vector2(-1, -1);
		}

		/// <param name="ndcPosition">[-1, -1, -1]..[1, 1, 1] (+Y is up)</param>
		/// <returns>[0, 0, -1]..[Width, Height, 1] (+Y is up)</returns>
		private Vector3 NDCToBottomScreenspace(Vector3 ndcPosition)
		{
			return new Vector3(NDCToBottomScreenspace(ndcPosition.Xy), ndcPosition.Z);
		}

		/// <param name="screenspacePosition">[0, 0, -1]..[Width, Height, 1] (+Y is up)</param>
		/// <returns>[-1, -1, -1]..[1, 1, 1] (+Y is up)</returns>
		private Vector3 BottomScreenspaceToNDC(Vector3 screenspacePosition)
		{
			return new Vector3(BottomScreenspaceToNDC(screenspacePosition.Xy), screenspacePosition.Z);
		}
	}
}