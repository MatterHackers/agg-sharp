using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;

namespace MatterHackers.GCodeVisualizer
{
    public class PrinterMachineInstruction
    {
        public string Line;

        public Vector3 xyzPosition = new Vector3();
        public double ePosition = 0;
        public double feedRate = 0;

        public enum MovementTypes { Absolute, Relative };
        public MovementTypes movementType = MovementTypes.Relative;

        public double secondsThisLine;
        public double secondsToEndFromHere;

        public PrinterMachineInstruction(string Line)
        {
            this.Line = Line;
        }

        public PrinterMachineInstruction(string Line, PrinterMachineInstruction copy)
            : this(Line)
        {
            xyzPosition = copy.xyzPosition;
            feedRate = copy.feedRate;
            ePosition = copy.ePosition;
            movementType = copy.movementType;
            secondsToEndFromHere = copy.secondsToEndFromHere;
        }

        public Vector3 Position
        {
            get { return xyzPosition; }
        }

        public double X
        {
            get { return xyzPosition.x; }
            set
            {
                if (movementType == MovementTypes.Absolute)
                {
                    xyzPosition.x = value;
                }
                else
                {
                    xyzPosition.x += value;
                }
            }
        }

        public double Y
        {
            get { return xyzPosition.y; }
            set
            {
                if (movementType == MovementTypes.Absolute)
                {
                    xyzPosition.y = value;
                }
                else
                {
                    xyzPosition.y += value;
                }
            }
        }

        public double Z
        {
            get { return xyzPosition.z; }
            set
            {
                if (movementType == MovementTypes.Absolute)
                {
                    xyzPosition.z = value;
                }
                else
                {
                    xyzPosition.z += value;
                }
            }
        }

        public double EPosition
        {
            get { return ePosition; }
            set
            {
                if (movementType == MovementTypes.Absolute)
                {
                    ePosition = value;
                }
                else
                {
                    ePosition += value;
                }
            }
        }

        public double FeedRate
        {
            get { return feedRate; }
            set { feedRate = value; }
        }
    }
}
