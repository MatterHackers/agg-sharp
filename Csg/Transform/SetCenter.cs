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

namespace MatterHackers.Csg.Transform
{
	public class SetCenter : Translate
	{
		public SetCenter(CsgObject objectToCenter, double x = 0, double y = 0, double z = 0, bool onX = true, bool onY = true, bool onZ = true, string name = "")
			: this(objectToCenter, new Vector3(x, y, z), onX, onY, onZ, name)
		{
		}

		public SetCenter(CsgObject objectToCenter, Vector3 offset, bool onX = true, bool onY = true, bool onZ = true, string name = "")
			: base(objectToCenter, offset, name)
		{
			AxisAlignedBoundingBox bounds = objectToCenter.GetAxisAlignedBoundingBox();
			Vector3 center = (bounds.maxXYZ + bounds.minXYZ) / 2;

			Vector3 origin = Vector3.Zero; // zero out anything we don't want
			if (onX)
			{
				origin.x = offset.x - center.x;
			}
			if (onY)
			{
				origin.y = offset.y - center.y;
			}
			if (onZ)
			{
				origin.z = offset.z - center.z;
			}

			base.Translation = origin;
		}
	}
}