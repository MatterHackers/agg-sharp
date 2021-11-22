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
	public class Axis3D
	{
		public Matrix4X4 Transform { get { return AxisToWorld; } set { AxisToWorld = value; } }

		public Matrix4X4 AxisToWorld;
		public Matrix4X4 WorldToAxis;

		public Axis3D()
		{
			AxisToWorld = Matrix4X4.Identity;
			WorldToAxis = Matrix4X4.Identity;
		}

		public Axis3D(Vector3 position)
		{
			Transform = Matrix4X4.CreateTranslation(position);
		}

		// get the position of the Origin in world space
		public Vector3 Origin
		{
			get
			{
				return new Vector3(AxisToWorld[3, 0], AxisToWorld[3, 1], AxisToWorld[3, 2]);
			}

			set
			{
				AxisToWorld[3, 0] = value.X;
				AxisToWorld[3, 1] = value.Y;
				AxisToWorld[3, 2] = value.Z;

				WorldToAxis = Matrix4X4.Invert(AxisToWorld);
			}
		}

		public void MoveToAbsolute(double x, double y, double z)
		{
			AxisToWorld[3, 0] = x;
			AxisToWorld[3, 1] = y;
			AxisToWorld[3, 2] = z;

			WorldToAxis = Matrix4X4.Invert(AxisToWorld);
		}

		public void MoveToAbsolute(Vector3 worldPosition)
		{
			MoveToAbsolute(worldPosition.X, worldPosition.Y, worldPosition.Z);
		}

		public void MoveToRelative(double x, double y, double z)
		{
			if (x != 0 || y != 0 || z != 0)
			{
				Vector3 add;

				add.X = x;
				add.Y = y;
				add.Z = z;

				add = Vector3Ex.Transform(add, AxisToWorld);

				AxisToWorld[3, 0] += add.X;
				AxisToWorld[3, 1] += add.Y;
				AxisToWorld[3, 2] += add.Z;

				WorldToAxis = Matrix4X4.Invert(AxisToWorld);
			}
		}

		public void MoveToRelative(Vector3 pVect)
		{
			MoveToRelative(pVect.X, pVect.Y, pVect.Z);
		}

		// move an object relative to it's current position relative to the rotation of another axis
		// i.e. move an object left relative to the camera (left from the view of the camera)
		public void MoveToRelativeTo(Axis3D pReferenceAxis, double x, double y, double z)
		{
			Vector3 add;

			add.X = x;
			add.Y = y;
			add.Z = z;

			add = Vector3Ex.Transform(add, pReferenceAxis.AxisToWorld);

			AxisToWorld[3, 0] += add.X;
			AxisToWorld[3, 1] += add.Y;
			AxisToWorld[3, 2] += add.Z;

			// and do it for the inverse
			add = Vector3Ex.Transform(add, WorldToAxis);

			WorldToAxis[3, 0] -= add.X;
			WorldToAxis[3, 1] -= add.Y;
			WorldToAxis[3, 2] -= add.Z;
		}

		// place an object relative to another object
		// i.e. place and object to the left of the camera (left from the view of the camera)
		public void PlaceRelativeTo(Axis3D pReferenceAxis, double x, double y, double z)
		{
			Vector3 pos = new Vector3(x, y, z);

			pos = Vector3Ex.Transform(pos, pReferenceAxis.AxisToWorld);
			Vector3 RefPos = pReferenceAxis.Origin;

			pos += RefPos;

			MoveToAbsolute(pos.X, pos.Y, pos.Z);
		}

		public void RotateAbsolute(Axis3D pSourceAxis)
		{
			Vector3 save1;

			// save the old position
			save1.X = AxisToWorld[3, 0];
			save1.Y = AxisToWorld[3, 1];
			save1.Z = AxisToWorld[3, 2];

			AxisToWorld = pSourceAxis.AxisToWorld;

			// stuff the old position back in
			AxisToWorld[3, 0] = save1.X;
			AxisToWorld[3, 1] = save1.Y;
			AxisToWorld[3, 2] = save1.Z;

			AxisToWorld = Matrix4X4.Invert(WorldToAxis);
		}

		public void RotateAbsolute(double x, double y, double z)
		{
			Matrix4X4 M1 = Matrix4X4.Identity;
			Matrix4X4 M2 = Matrix4X4.Identity;
			Matrix4X4 M3 = Matrix4X4.Identity;
			Matrix4X4 M4 = Matrix4X4.Identity;
			Vector3 save1;

			// save the old position
			save1.X = AxisToWorld[3, 0];
			save1.Y = AxisToWorld[3, 1];
			save1.Z = AxisToWorld[3, 2];

			M1 = Matrix4X4.CreateRotationX(x);
			M2 = Matrix4X4.CreateRotationY(y);
			M3 = Matrix4X4.CreateRotationZ(z);
			// 1 * 2 * 3
			M4 = M2 * M1;
			AxisToWorld = M3 * M4;

			// stuff the old position back in
			AxisToWorld[3, 0] = save1.X;
			AxisToWorld[3, 1] = save1.Y;
			AxisToWorld[3, 2] = save1.Z;

			AxisToWorld = Matrix4X4.Invert(WorldToAxis);
		}

		public void RotateAbsolute(Vector3 pVect)
		{
			RotateAbsolute(pVect.X, pVect.Y, pVect.Z);
		}

		public void RotateRelative(double x, double y, double z)
		{
#if true
			if (x != 0)
			{
				Matrix4X4 M1 = Matrix4X4.Identity;
				M1 = Matrix4X4.CreateRotationX(x);
				if (y != 0)
				{
					Matrix4X4 M2 = Matrix4X4.Identity;
					Matrix4X4 M4 = Matrix4X4.Identity;
					M2 = Matrix4X4.CreateRotationY(y);
					M4 = M2 * M1;
					if (z != 0)
					{
						Matrix4X4 M3 = Matrix4X4.Identity;
						M3 = Matrix4X4.CreateRotationZ(z);
						Matrix4X4 Delta = Matrix4X4.Identity;
						Delta = M3 * M4;
						AxisToWorld *= Delta;
					}
					else
					{
						AxisToWorld *= M4;
					}
				}
				else
				{
					if (z != 0)
					{
						Matrix4X4 M3 = Matrix4X4.Identity;
						M3 = Matrix4X4.CreateRotationZ(z);
						Matrix4X4 Delta = Matrix4X4.Identity;
						Delta = M3 * M1;
						AxisToWorld *= Delta;
					}
					else
					{
						AxisToWorld *= M1;
					}
				}
			}
			else
			{
				if (y != 0)
				{
					Matrix4X4 M2 = Matrix4X4.Identity;
					M2 = Matrix4X4.CreateRotationY(y);
					if (z != 0)
					{
						Matrix4X4 M3 = Matrix4X4.Identity;
						M3 = Matrix4X4.CreateRotationZ(z);
						Matrix4X4 Delta = Matrix4X4.Identity;
						Delta = M3 * M2;
						AxisToWorld *= Delta;
					}
					else
					{
						AxisToWorld *= M2;
					}
				}
				else
				{
					if (z != 0)
					{
						Matrix4X4 M3 = Matrix4X4.Identity;
						M3 = Matrix4X4.CreateRotationZ(z);
						AxisToWorld *= M3;
					}
				}
			}

			WorldToAxis = Matrix4X4.Invert(AxisToWorld);
#else
	M1.Rotate(0, x);
	M2.Rotate(1, y);
	M3.Rotate(2, z);
	// 1 * 2 * 3
	M4 = M2 * M1;
	Delta = M3 * M4;

	AxisToWorld = AxisToWorld * Delta;

	WorldToAxis = AxisToWorld.GetInverse();
#endif
		}

		public void RotateRelative(Vector3 pVect)
		{
			RotateRelative(pVect.X, pVect.Y, pVect.Z);
		}

		public void GetRotation(ref Vector3 pOutVector)
		{
			Vector3 UpVector = new Vector3(0, 1, 0);
			Vector3 ForwardVector = new Vector3(0, 0, 1);

			UpVector = Vector3Ex.Transform(UpVector, AxisToWorld);
			ForwardVector = Vector3Ex.Transform(ForwardVector, AxisToWorld);

			pOutVector.Z = Math.Atan2(UpVector.X, UpVector.Y);
			//	DebugStream("It looks like z is now " << RadToDeg(pOutVector->z));

			Matrix4X4 ZMatrix = Matrix4X4.CreateRotationZ(-pOutVector.Z);
			UpVector = Vector3Ex.Transform(UpVector, ZMatrix);
			ForwardVector = Vector3Ex.Transform(ForwardVector, ZMatrix);

			//	DebugStream("It looks like z is now " << RadToDeg(atan2(UpVector.x, UpVector.y)));

			pOutVector.Y = -Math.Atan2(ForwardVector.X, ForwardVector.Z);
			//	DebugStream("It looks like y is now " << RadToDeg(pOutVector->y));

			Matrix4X4 YMatrix = Matrix4X4.CreateRotationY(-pOutVector.Y);
			UpVector = Vector3Ex.Transform(UpVector, YMatrix);
			ForwardVector = Vector3Ex.Transform(ForwardVector, YMatrix);

			//	DebugStream("It looks like y is now " << RadToDeg(-atan2(ForwardVector.x, ForwardVector.z)));

			pOutVector.X = -Math.Atan2(UpVector.Z, UpVector.Y);
			//	DebugStream("It looks like x is now " << RadToDeg(pOutVector->x));

			//CMatrix XMatrix;
			//XMatrix.Rotate(0, -pOutVector->x, ROTATE_FAST);
			//XMatrix.TransformVector(&UpVector);
			//XMatrix.TransformVector(&ForwardVector);

			//	DebugStream("It looks like x is now " << RadToDeg(-atan2(UpVector.z, UpVector.y)));

#if _DEBUG
	CMatrix Test;
	CVector3D TestV, HoldV;
	TestV.Set((double)(rand() % 1024), (double)(rand() % 1024), (double)(rand() % 1024));
	HoldV = TestV;
	Test.PrepareMatrix(0.f, 0.f, 0.f,
		pOutVector->x, pOutVector->y, pOutVector->z);
	WorldToAxis.TransformVector(&TestV);
	Test.TransformVector(&TestV);
#endif
		}

		public void SetPosAndRotation(Axis3D pSourceAxis)
		{
			AxisToWorld = pSourceAxis.AxisToWorld;
			WorldToAxis = pSourceAxis.WorldToAxis;
		}

		public void Scale(Vector3 scale)
		{
			WorldToAxis = Matrix4X4.CreateScale(scale);
			AxisToWorld = Matrix4X4.Invert(WorldToAxis);
		}

		public double GetYAngleToTarget(Vector3 pTarget)
		{
			Vector3 TargetPos = pTarget;

			TargetPos = Vector3Ex.TransformVector(TargetPos, WorldToAxis);

			// Will rotate Y to make z point away from object (make -z point at object)
			return Math.Atan2(TargetPos.X, -TargetPos.Z);
		}

		public double GetXAngleToTarget(ref Vector3 pTarget)
		{
			Vector3 TargetPos = pTarget;

			TargetPos = Vector3Ex.TransformVector(TargetPos, WorldToAxis);

			// (-90 degrees) Will rotate X to make z point away from object (-z point at object)
			return Math.Atan2(-TargetPos.Z, TargetPos.Y) - MathHelper.Tau / 4;
		}

		// fist the Y axis is rotated and then if YandX the X axis is.
		public void PointAt(Vector3 pTarget, bool YandX = true)
		{
			//AxisToWorld = Matrix4X4.LookAt(Origin, pTarget, Vector3.UnitZ);
			WorldToAxis = Matrix4X4.LookAt(Origin, pTarget, Vector3.UnitZ);

			AxisToWorld = Matrix4X4.Invert(WorldToAxis);
			//WorldToAxis = Matrix4X4.Invert(AxisToWorld);
		}

		public void PointAtOrigin(Axis3D pTarget, bool YandX = true)
		{
			PointAt(pTarget.Origin, YandX);
		}
	}
}