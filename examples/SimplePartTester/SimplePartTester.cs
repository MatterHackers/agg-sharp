using System;
using System.Collections.Generic;
using System.Diagnostics;
using MatterHackers.Csg;
using MatterHackers.VectorMath;

namespace SimplePartScripting
{
    using Aabb = AxisAlignedBoundingBox;

    class SimplePartTester
    {
        public static CsgObject SimplePartFunction()
        {
            CsgObject total;
            Box bar = new Box(10, 5, 15, "big");
            CsgObject bar2 = new Box(10, 5, 15, "big");

            bar2 = new Align(bar2, Face.Left | Face.Front, bar, Face.Right | Face.Back);

            total = bar;
            total += bar2;

            BoxBevels addativeBevel = new BoxBevels(10, 2, 15, "#test");
            addativeBevel.BevelEdge(Edge.LeftBack, 2);
            addativeBevel.BevelEdge(Edge.BackBottom, 2);
            //addativeBevel.BevelEdge(Edge.LeftBottom, 2);
            CsgObject addative = addativeBevel;
            addative = new Align(addative, Face.Left | Face.Back, bar, Face.Right | Face.Back);
            total += addative;

            BoxBevels bevel = new BoxBevels(bar.Size, "BoxBevels");

            //bevel.BevelFace(Face.Top, 2);
            //bevel.BevelPoint(Face.Front | Face.Left | Face.Bottom, 2);
            //bevel.BevelPoint(Face.Front | Face.Left | Face.Top, 2);
            //bevel.BevelPoint(Face.Front | Face.Right | Face.Top, 2);
            //bevel.BevelPoint(Face.Front | Face.Right | Face.Bottom, 2);
            //bevel.BevelPoint(Face.Left | Face.Back | Face.Top, 2);
            //bevel.BevelPoint(Face.Right | Face.Back | Face.Top, 2);
            //bevel.BevelPoint(Face.Right | Face.Back | Face.Bottom, 2);
            //bevel.BevelPoint(Face.Left | Face.Back | Face.Bottom, 2);

            bevel.BevelEdge(Edge.FrontBottom, 2);
            bevel.BevelEdge(Edge.LeftBottom, 2);
            bevel.BevelEdge(Edge.LeftFront, 2);

            //bevel.BevelAll(2);

            total -= bevel;

            return total;
        }

        static void Main()
        {
            CsgObject part = SimplePartFunction();
            OpenSCadOutput.Save(part, "temp.scad");

            System.Console.WriteLine("Output the file to 'temp.scad'.");
        }
    }
}
