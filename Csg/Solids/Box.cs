/*
Copyright (c) 2013, Lars Brubaker
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
    public class Box : CsgObjectWrapper
    {
        Vector3 size;

        public class BoxPrimitive : Solid
        {
            internal Vector3 size;

            public new Vector3 Size { get { return size; } }
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

        public Box(double sizeX, double sizeY, double sizeZ, string name = "", bool createCentered = true)
            : this(new Vector3(sizeX, sizeY, sizeZ), name, createCentered)
        {
        }

        public Box(Vector3 size, string name = "", bool createCentered = true)
            : base(name)
        {
            this.size = size;
            root = new BoxPrimitive(size, name, createCentered);
        }

        /// <summary>
        /// Cun along the diagonal of a give edge to the opposite edge.  The edge is chosen by saying 
        /// which 2 faces are to be kept solid, and or-ing them together.
        /// </summary>
        /// <param name="facesThatShareEdge">The two faces to maintain after the cut, or-ed together with '|'.</param>
        public void CutAlongDiagonal(Face facesToKeepWhole)
        {
            switch (facesToKeepWhole)
            {
                case (Face.Left | Face.Back):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(Math.Sqrt(2), 1, 1.1, createCentered: false);
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(0, 0, MathHelper.Tau / 8));
                        boxToCutOut = new Translate(boxToCutOut, x: Math.Sqrt(2) / 2, y: -Math.Sqrt(2) / 2, z: -.05);
                        boxToCutOut = new Scale(boxToCutOut, size, name: "boxToCutOut");
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                case (Face.Left | Face.Front):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(Math.Sqrt(2), 1, 1.1, createCentered: false);
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(0, 0, -MathHelper.Tau / 8));
                        boxToCutOut = new Translate(boxToCutOut, y: 1, z: -.05);
                        boxToCutOut = new Scale(boxToCutOut, size, name: "boxToCutOutLeftFront");
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                case (Face.Left | Face.Bottom):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(Math.Sqrt(2), 1.1, 1, createCentered: false);
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(0, MathHelper.Tau / 8, 0));
                        boxToCutOut = new Translate(boxToCutOut, y: -.05, z: 1);
                        boxToCutOut = new Scale(boxToCutOut, size, name: "boxToCutOut");
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                case (Face.Right | Face.Front):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(Math.Sqrt(2), 1, 1.1, createCentered: false, name: "boxtToCutOut");
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(0, 0, MathHelper.Tau / 8));
                        boxToCutOut = new Translate(boxToCutOut, z: -.05);
                        boxToCutOut = new Scale(boxToCutOut, size, name: "boxToCutOutRightFront");
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                case (Face.Right | Face.Bottom):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(Math.Sqrt(2), 1.1, 1, createCentered: false, name: "boxToCutOut");
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(0, -MathHelper.Tau / 8, 0));
                        boxToCutOut = new Translate(boxToCutOut, 0, -.05, 0);
                        //boxToCutOut = new Translate(boxToCutOut, Math.Sqrt(2) / 2, 0, -Math.Sqrt(2) / 2);
                        boxToCutOut = new Scale(boxToCutOut, size, name: "boxToCutOut");
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                case (Face.Right | Face.Top):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(Math.Sqrt(2), 1.1, 1, createCentered: false, name: "boxToCutOut");
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(0, MathHelper.Tau / 8, 0));
                        boxToCutOut = new Translate(boxToCutOut, -Math.Sqrt(2) / 2, 0, 1-Math.Sqrt(2) / 2);
                        boxToCutOut = new Scale(boxToCutOut, size, name: "botToCutOut");
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                case (Face.Front | Face.Top):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(1.1, Math.Sqrt(2), 1, createCentered: false, name: "boxToCutOut");
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(MathHelper.Tau / 8, 0, 0));
                        boxToCutOut = new Translate(boxToCutOut, -.05, Math.Sqrt(2) / 2, -Math.Sqrt(2) / 2);
                        boxToCutOut = new Scale(boxToCutOut, size);
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                case (Face.Front | Face.Bottom):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(1.1, Math.Sqrt(2), 1, createCentered: false, name: "boxToCutOut");
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(-MathHelper.Tau / 8, 0, 0));
                        boxToCutOut = new Translate(boxToCutOut, -.05, 0, 1);
                        boxToCutOut = new Scale(boxToCutOut, size, name: "boxtToCutOutFrontBottom");
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                case (Face.Bottom | Face.Back):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(1.1, Math.Sqrt(2), 1, createCentered: false);
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(MathHelper.Tau / 8, 0, 0));
                        boxToCutOut = new Translate(boxToCutOut, x: -.05);
                        boxToCutOut = new Scale(boxToCutOut, size, name: "boxToCutOut");
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                case (Face.Back | Face.Top):
                    {
                        Vector3 size = root.Size;
                        CsgObject boxToCutOut = new Box(1.1, Math.Sqrt(2), 1, createCentered: false, name: "boxToCutOut");
                        boxToCutOut = new Rotate(boxToCutOut, new Vector3(-MathHelper.Tau / 8, 0, 0));
                        boxToCutOut = new Translate(boxToCutOut, x: -.05, y: -Math.Sqrt(2) / 2, z: .28); // TODO: do the right math. .28 is hacky
                        boxToCutOut = new Scale(boxToCutOut, size, name: "boxToCutOut");
                        root = new Difference(root, boxToCutOut);
                    }
                    break;

                default:
                    throw new NotImplementedException("Just write it for this case.");
            }
        }

        /// <summary>
        /// Bevel a give edge.  The edge is chosen by saying which 2 faces are to be used,
        /// and or-ing them together
        /// </summary>
        /// <param name="facesThatShareEdge">The two faces or-ed together with |</param>
        /// <param name="radius">The radius of the bevel</param>
        public void BevelEdge(Face facesThatShareEdge, double radius)
        {
            Round roundToApply = new Round(Size);
            roundToApply.RoundEdge((Edge)facesThatShareEdge, radius);
            CsgObject offsetRoundToApply = new Align(roundToApply, Face.Left | Face.Front | Face.Bottom, root, Face.Left | Face.Front | Face.Bottom);
            root -= offsetRoundToApply;
        }

        public void BevelEdge(Edge edgeToBevel, double radius)
        {
            Round roundToApply = new Round(Size);
            roundToApply.RoundEdge(edgeToBevel, radius);
            CsgObject offsetRoundToApply = new Align(roundToApply, Face.Left | Face.Front | Face.Bottom, root, Face.Left | Face.Front | Face.Bottom);
            root -= offsetRoundToApply;
        }

        public void BevelFace(Face face, double radius)
        {
            Round roundToApply = new Round(Size);
            roundToApply.RoundFace(face, radius);
            CsgObject offsetRoundToApply = new Align(roundToApply, Face.Left | Face.Front | Face.Bottom, root, Face.Left | Face.Front | Face.Bottom);
            root -= offsetRoundToApply;
        }

        public void BevelAll(double radius)
        {
            Round roundToApply = new Round(Size);
            roundToApply.RoundAll(radius);
            CsgObject offsetRoundToApply = new Align(roundToApply, Face.Left | Face.Front | Face.Bottom, root, Face.Left | Face.Front | Face.Bottom);
            root -= offsetRoundToApply;
        }

        /// <summary>
        /// Will cause a chamfered flat to be applied to the given edge.
        /// </summary>
        /// <param name="facesThatShareEdge"></param>
        /// <param name="ratioOfEdgeToCut">Cut depth is the depth at the center (much like the radius of a fillet.</param>
        public void ChamferEdge(Face facesThatShareEdge, double cutDepth)
        {
            throw new NotImplementedException();
        }
    }
}
