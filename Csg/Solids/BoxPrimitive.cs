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

using System;
using System.Collections.Generic;
using System.IO;

using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Transform;
using MatterHackers.VectorMath;

namespace MatterHackers.Csg.Solids
{
    public class BoxPrimitive : Solid
    {
        internal Vector3 size;

        public new Vector3 Size { get { return size; } set { size = value; } }
        public bool CreateCentered { get; set; }

        public BoxPrimitive(double sizeX, double sizeY, double sizeZ, string name = "", bool createCentered = true)
            : this(new Vector3(sizeX, sizeY, sizeZ), name, createCentered)
        {
        }

        public BoxPrimitive(BoxPrimitive objectToCopy)
            : this(objectToCopy.size, objectToCopy.name, objectToCopy.CreateCentered)
        {
        }

        public BoxPrimitive(Vector3 size, string name = "", bool createCentered = true)
            : base(name)
        {
            this.CreateCentered = createCentered;
            this.size = size;
        }

        public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
        {
            if (CreateCentered)
            {
                return new AxisAlignedBoundingBox(-size / 2, size / 2);
            }
            else
            {
                return new AxisAlignedBoundingBox(Vector3.Zero, size);
            }
        }
    }
}
