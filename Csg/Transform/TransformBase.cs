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
	public class TransformBase : CsgObject
	{
		public Matrix4X4 transform;
		public CsgObject objectToTransform;

		public TransformBase(CsgObject objectToTransform, string name = "")
			: this(objectToTransform, Matrix4X4.Identity, name)
		{
		}

		public TransformBase(CsgObject objectToTransform, Matrix4X4 transform, string name = "")
			: base(name)
		{
			this.transform = transform;
			this.objectToTransform = objectToTransform;
		}

		public Matrix4X4 ActiveTransform { get { return transform; } }

		public CsgObject ObjectToTransform { get { return objectToTransform; } }

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			AxisAlignedBoundingBox childBounds = objectToTransform.GetAxisAlignedBoundingBox();
			return childBounds.NewTransformed(transform);
		}
	}
}