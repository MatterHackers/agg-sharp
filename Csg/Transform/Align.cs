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

namespace MatterHackers.Csg.Transform
{
	public class Align : Translate
	{
		public Align(CsgObject objectToAlign, Face boundingFacesToAlign, CsgObject objectToAlignTo, Face boundingFacesToAlignTo, double offsetX = 0, double offsetY = 0, double offsetZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, GetPositionToAlignTo(objectToAlignTo, boundingFacesToAlignTo, new Vector3(offsetX, offsetY, offsetZ)), name)
		{
			if (objectToAlign == objectToAlignTo)
			{
				throw new Exception("You cannot align an object ot itself.");
			}
		}

		public Align(CsgObject objectToAlign, Face boundingFacesToAlign, double offsetX = 0, double offsetY = 0, double offsetZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, new Vector3(offsetX, offsetY, offsetZ), name)
		{
		}

		public Align(CsgObject objectToAlign, Face boundingFacesToAlign, Vector3 positionToAlignTo, double offsetX, double offsetY, double offsetZ, string name = "")
			: this(objectToAlign, boundingFacesToAlign, positionToAlignTo + new Vector3(offsetX, offsetY, offsetZ), name)
		{
		}

		public Align(CsgObject objectToAlign, Face boundingFacesToAlign, Vector3 positionToAlignTo, string name = "")
			: base(objectToAlign, positionToAlignTo, name)
		{
			AxisAlignedBoundingBox bounds = objectToAlign.GetAxisAlignedBoundingBox();

			if (IsSet(boundingFacesToAlign, Face.Left, Face.Right))
			{
				positionToAlignTo.x = positionToAlignTo.x - bounds.minXYZ.x;
			}
			if (IsSet(boundingFacesToAlign, Face.Right, Face.Left))
			{
				positionToAlignTo.x = positionToAlignTo.x - bounds.minXYZ.x - (bounds.maxXYZ.x - bounds.minXYZ.x);
			}
			if (IsSet(boundingFacesToAlign, Face.Front, Face.Back))
			{
				positionToAlignTo.y = positionToAlignTo.y - bounds.minXYZ.y;
			}
			if (IsSet(boundingFacesToAlign, Face.Back, Face.Front))
			{
				positionToAlignTo.y = positionToAlignTo.y - bounds.minXYZ.y - (bounds.maxXYZ.y - bounds.minXYZ.y);
			}
			if (IsSet(boundingFacesToAlign, Face.Bottom, Face.Top))
			{
				positionToAlignTo.z = positionToAlignTo.z - bounds.minXYZ.z;
			}
			if (IsSet(boundingFacesToAlign, Face.Top, Face.Bottom))
			{
				positionToAlignTo.z = positionToAlignTo.z - bounds.minXYZ.z - (bounds.maxXYZ.z - bounds.minXYZ.z);
			}

			base.Translation = positionToAlignTo;
		}

		public static Vector3 GetPositionToAlignTo(CsgObject objectToAlignTo, Face boundingFacesToAlignTo, Vector3 extraOffset)
		{
			Vector3 positionToAlignTo = new Vector3();
			if (IsSet(boundingFacesToAlignTo, Face.Left, Face.Right))
			{
				positionToAlignTo.x = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.x;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Right, Face.Left))
			{
				positionToAlignTo.x = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.x;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Front, Face.Back))
			{
				positionToAlignTo.y = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.y;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Back, Face.Front))
			{
				positionToAlignTo.y = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.y;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Bottom, Face.Top))
			{
				positionToAlignTo.z = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.z;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Top, Face.Bottom))
			{
				positionToAlignTo.z = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.z;
			}
			return positionToAlignTo + extraOffset;
		}

		private static bool IsSet(Face variableToCheck, Face faceToCheckFor, Face faceToAssertNot)
		{
			if ((variableToCheck & faceToCheckFor) != 0)
			{
				if ((variableToCheck & faceToAssertNot) != 0)
				{
					throw new Exception("You cannot have both " + faceToCheckFor.ToString() + " and " + faceToAssertNot.ToString() + " set when calling Align.  The are mutually exclusive.");
				}
				return true;
			}

			return false;
		}
	}
}