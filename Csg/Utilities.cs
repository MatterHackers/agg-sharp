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
using System.Text;

using MatterHackers.VectorMath;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Transform;

namespace MatterHackers.Csg
{
    using Aabb = AxisAlignedBoundingBox;

    public static class Utilities
    {
        public const double M3ScrewDiameter = 3.4;
        public const double M3HoldThreadsDiameter = 3;

        public const double M4ScrewDiameter = 4.4;
        public const double M4HoldThreadesDiameter = 4;

        public const double LinearBearing8UUDiameter = 15;
        public const double LinearBearing8UUHeight = 24;

        const double linearBearing8UUEnclosureExtraDiameter = 8;
        public const double zipTieSizeZ = 3;

        public const double m8SmoothRodDiameter = 8;
        public const double m8SmoothRodDiameterToPrint = m8SmoothRodDiameter + .5;

        public const double ClampGap = 2;

        public static CsgObject PutOnPlatformAndCenter(CsgObject part, Vector3 rotation = new Vector3())
        {
            part = new Rotate(part, rotation);
            part = new SetCenter(part, new Vector3());
            part = new Align(part, Face.Bottom, offsetZ: 0);

            return part;
        }

        static public CsgObject LinearNewBearing8UUZipTieRemove(Vector3 bearingCenter)
        {
            double zipTieSizeX = 6;
            CsgObject zipTieRemoveRing = new Cylinder(LinearBearing8UUDiameter / 2 + linearBearing8UUEnclosureExtraDiameter / 2 + zipTieSizeZ, zipTieSizeX);
            CsgObject ringRemove = new Cylinder(LinearBearing8UUDiameter / 2 + linearBearing8UUEnclosureExtraDiameter / 2, zipTieSizeX + .1);
            zipTieRemoveRing = new Difference(zipTieRemoveRing, ringRemove);

            zipTieRemoveRing = new SetCenter(zipTieRemoveRing, bearingCenter);

            return zipTieRemoveRing;
        }

        static public CsgObject HexHole(double radius, double height, string name = "")
        {
            return new Box(radius * 2, radius * 2, height, createCentered: false);
        }

        static public CsgObject LinearBearing8UUHolder(Alignment alignment, double extraAtEachEndZ = 4, double extraHeight = 0)
        {
            double holdDiameterReduction = -2.5;
            double totalXSize = LinearBearing8UUDiameter + linearBearing8UUEnclosureExtraDiameter;
            double totalYSize = LinearBearing8UUDiameter / 2 + linearBearing8UUEnclosureExtraDiameter / 2 + zipTieSizeZ + extraHeight;
            double totalHeight = LinearBearing8UUHeight + extraAtEachEndZ * 2;

            double extraOverBearing = 2;
            CsgObject enclosure = new Box(totalXSize, totalYSize + extraOverBearing, totalHeight, createCentered: false, name: "Enclosure");
            CsgObject root = enclosure;

            Vector3 bearingCenter = enclosure.GetCenter() + new Vector3(0, totalYSize / 2 - extraOverBearing/2, 0);

            CsgObject bearingRemove = new Cylinder(LinearBearing8UUDiameter / 2 + .25, LinearBearing8UUHeight + .5);
            bearingRemove = new SetCenter(bearingRemove, bearingCenter);
            root -= bearingRemove;

            CsgObject rodeRemove = new Cylinder(LinearBearing8UUDiameter / 2 + holdDiameterReduction / 2, totalHeight + .1);
            rodeRemove = new SetCenter(rodeRemove, bearingCenter);
            root = new Difference(root, rodeRemove);

            //root = new Difference(root, LinearNewBearing8UUZipTieRemove(bearingCenter));

#if false
            ObjectCSG groovGrip1 = new Cylinder(LinearBearing8UUDiameter / 2 + .5, .75);
            groovGrip1 = new SetCenter(groovGrip1, bearingCenter + new Vector3(0, 0, 17/2 - groovGrip1.ZSize / 2));
            groovGrip1 = new Intersection(groovGrip1, enclosure);
            groovGrip1 -= new SetCenter(new Cylinder(LinearBearing8UUDiameter / 2 - .5, LinearBearing8UUHeight), bearingCenter);
            root += groovGrip1;

            ObjectCSG groovGrip2 = new Cylinder(LinearBearing8UUDiameter / 2 + .5, .75);
            groovGrip2 = new SetCenter(groovGrip2, bearingCenter - new Vector3(0, 0, 17/2 - groovGrip2.ZSize / 2));
            groovGrip2 = new Intersection(groovGrip2, enclosure);
            groovGrip2 -= new SetCenter(new Cylinder(LinearBearing8UUDiameter / 2 - .5, LinearBearing8UUHeight), bearingCenter);
            root += groovGrip2;
#endif

            switch (alignment)
            {
                case Alignment.x:
                    root = new Rotate(root, x: MathHelper.Tau / 4, z: MathHelper.Tau / 4);
                    break;

                case Alignment.y:
                    root = new Rotate(root, x: MathHelper.Tau / 4);
                    break;
            }

            return root;
        }

        static public CsgObject Slot(double width, double length, double depth)
        {
            CsgObject box = new Box(length, width, depth, createCentered: false);
            box = new Translate(box, 0, 0, -depth / 2);
            CsgObject hole1 = new Cylinder(width / 2, depth);
            CsgObject hole2 = new Cylinder(width / 2, depth);
            return new SetCenter(
                new Union(
                    box,
                    new Union(
                        new Translate(
                            hole1,
                            length, width / 2, 0),
                            new Translate(
                                hole2,
                                0, width / 2, 0)
                            ), "slot"
                        ),
                        0, 0, 0);
        }

    }
}
