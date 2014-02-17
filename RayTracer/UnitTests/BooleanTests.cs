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
using System.Linq;
using System.Text;
using System.IO;

using NUnit.Framework;

using MatterHackers.Agg;
using MatterHackers.VectorMath;

using MatterHackers.RayTracer.Traceable;

namespace MatterHackers.RayTracer
{
    [TestFixture]
    public class BooleanTests
    {
        [Test]
        public void DifferenceTestsForBox()
        {
            SolidMaterial redMaterial = new SolidMaterial(RGBA_Floats.Red, 0, 0, 0);
            SolidMaterial blueMaterial = new SolidMaterial(RGBA_Floats.Blue, 0, 0, 0);
            Ray castRay = new Ray(new Vector3(0, -1, 0), Vector3.UnitY);

            BoxShape box1X1 = new BoxShape(new Vector3(-.5, -.5, -.5), new Vector3(.5, .5, .5), blueMaterial);

            // just a box all by itself
            {
                IntersectInfo testInfo = box1X1.GetClosestIntersection(castRay);

                Assert.IsTrue(testInfo.hitType == IntersectionType.FrontFace, "Found Hit : Box No CSG");
                Assert.IsTrue(testInfo.closestHitObject == box1X1, "Found Hit : Box No CSG");
                Assert.IsTrue(testInfo.hitPosition == new Vector3(0, -.5, 0), "Hit position y = -.5 : Box No CSG");
                Assert.IsTrue(testInfo.distanceToHit == .5, "Hit length = .5 : Box No CSG");
                Assert.IsTrue(testInfo.normalAtHit == -Vector3.UnitY, "Normal Correct : Box No CSG");
            }

            // one subtract from the front of a box, the front faces are aligned
            {
                BoxShape subtractBox = new BoxShape(new Vector3(-.5, -.5, -.5), new Vector3(.5, 0, .5), redMaterial);
                Difference merge = new Difference(box1X1, subtractBox);
                IntersectInfo testInfo = merge.GetClosestIntersection(castRay);

                Assert.IsTrue(testInfo.hitType == IntersectionType.FrontFace, "Found Hit : One Subtract");
                Assert.IsTrue(testInfo.closestHitObject == subtractBox, "Found Hit : One Subtract");
                Assert.IsTrue(testInfo.hitPosition == new Vector3(0, 0, 0), "Hit position y = 0 : One Subtract");
                Assert.IsTrue(testInfo.distanceToHit == 1, "Hit length = 1 : One Subtract");
                Assert.IsTrue(testInfo.normalAtHit == -Vector3.UnitY, "Normal Correct : One Subtract");
            }

#if false
            // An internal primary object that needs to be skipped over
            {
                List<IRayTraceable> primaryShapes = new List<IRayTraceable>();
                BoxShape insideBox = new BoxShape(new Vector3(-.1, -.1, -.1), new Vector3(.1, .1, .1), blueMaterial);
                primaryShapes.Add(box1X1);
                primaryShapes.Add(insideBox);
                IRayTraceable primamryGroup = BoundingVolumeHierarchy.CreateNewHierachy(primaryShapes);

                List<IRayTraceable> subtractShapes = new List<IRayTraceable>();
                subtractShapes.Add(new BoxShape(new Vector3(-.5, -.5, -.5), new Vector3(.5, .4, .5), redMaterial));

                IRayTraceable subtractGroup = BoundingVolumeHierarchy.CreateNewHierachy(subtractShapes);
                Difference merge = new Difference(primamryGroup, subtractGroup);

                IntersectInfo testInfo = merge.GetClosestIntersection(castRay);

                Assert.IsTrue(testInfo.isHit == true, "Found Hit : 5 Subtracts");
                //Assert.IsTrue(testInfo.closestHitObject == subtractBox, "Found Hit : 5 Subtracts");
                Assert.IsTrue(testInfo.hitPosition == new Vector3(0, 0, 0), "Hit position y = 0 : 5 Subtracts");
                Assert.IsTrue(testInfo.distanceToHit == 1, "Hit length = 1 : 5 Subtracts");
                Assert.IsTrue(testInfo.normalAtHit == -Vector3.UnitY, "Normal Correct : 5 Subtracts");
            }

            // Go through 5 subtract boxes to get to 1/2 way through the main box.
            {
                List<IRayTraceable> subtractShapes = new List<IRayTraceable>();

                for (int i = 0; i < 5; i++)
                {
                    subtractShapes.Add(new BoxShape(new Vector3(-.5, -.5 + i * .1, -.5), new Vector3(.5, -.4 + i * .1, .5), redMaterial));
                }

                IRayTraceable subtractGroup = BoundingVolumeHierarchy.CreateNewHierachy(subtractShapes);
                Difference merge = new Difference(box1X1, subtractGroup);

                IntersectInfo testInfo = merge.GetClosestIntersection(castRay);

                Assert.IsTrue(testInfo.isHit == true, "Found Hit : 5 Subtracts");
                //Assert.IsTrue(testInfo.closestHitObject == subtractBox, "Found Hit : 5 Subtracts");
                Assert.IsTrue(testInfo.hitPosition == new Vector3(0, 0, 0), "Hit position y = 0 : 5 Subtracts");
                Assert.IsTrue(testInfo.distanceToHit == 1, "Hit length = 1 : 5 Subtracts");
                Assert.IsTrue(testInfo.normalAtHit == -Vector3.UnitY, "Normal Correct : 5 Subtracts");
            }
#endif
        }

        [Test]
        public void DiscoveredBadIntersectInfoListSubtraction()
        {
            string primaryString = @"2
FrontFace, 6.55505298172777
BackFace, 7.05554361306285";
            string subtractString = @"4
FrontFace, 7.05554387355765
BackFace, 7.14478176419901
FrontFace, 7.28926063619785
BackFace, 7.36209329430552";

            List<IntersectInfo> allPrimary = new List<IntersectInfo>();
            IntersectInfo.ReadInList(allPrimary, new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(primaryString))));

            List<IntersectInfo> allSubtract = new List<IntersectInfo>();
            IntersectInfo.ReadInList(allSubtract, new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(subtractString))));

            List<IntersectInfo> result = new List<IntersectInfo>();

            IntersectInfo.Subtract(allPrimary, allSubtract, result);
        }
    }
}
