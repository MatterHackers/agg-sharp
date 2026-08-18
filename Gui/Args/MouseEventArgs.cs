using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
    public enum FlingDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public class FlingEventArgs : EventArgs
    {
        private FlingDirection direction;
        private double x;
        private double y;

        public FlingEventArgs(double originX, double originY, FlingDirection flingDirection)
        {
            this.direction = flingDirection;
            this.x = originX;
            this.y = originY;
        }

        public FlingDirection Direction
        { get { return direction; } }

        public Vector2 Position
        { get { return new Vector2(x, y); } }

        public double X
        { get { return x; } set { x = value; } }

        public double Y
        { get { return y; } set { y = value; } }
    }

    public class MouseEventArgs : EventArgs
    {
        private bool acceptDrop = false;
        private List<Vector2> positions = new List<Vector2>();

        public MouseEventArgs(MouseEventArgs original, double newX, double newY)
            : this(original.Button, original.Clicks, newX, newY, original.WheelDelta, original.DragFiles)
        {
            this.WheelDeltaX = original.WheelDeltaX;
            this.WheelDeltaIsPreciseScroll = original.WheelDeltaIsPreciseScroll;
            positions[0] = new Vector2(newX, newY);
            for (int i = 1; i < original.NumPositions; i++)
            {
                positions.Add(original.GetPosition(i));
            }
        }

        public MouseEventArgs(MouseButtons button, int clicks, double x, double y, int wheelDelta, List<string> dragDropFiles = null)
            : this(button, clicks, new Vector2[] { new Vector2(x, y) }, wheelDelta, dragDropFiles)
        {
        }

        public MouseEventArgs(MouseButtons button, int clicks, Vector2[] positions, int wheelDelta, List<string> dragDropFiles)
        {
            Button = button;
            Clicks = clicks;
            DragFiles = dragDropFiles;

            this.positions = new List<Vector2>(positions);
            this.WheelDelta = wheelDelta;
        }

        public bool AcceptDrop
        {
            get
            {
                return acceptDrop;
            }

            set
            {
                if (value != acceptDrop)
                {
                    acceptDrop = value;
                }
            }
        }

        public MouseButtons Button { get; private set; }
        public int Clicks { get; private set; }
        public List<string> DragFiles { get; private set; } = null;
        public bool Handled { get; set; } = false;

        public int NumPositions
        {
            get
            {
                return positions.Count;
            }
        }

        public Vector2 Position
        { get { return positions[0]; } }

        public int WheelDelta { get; set; }

        /// <summary>
        /// The sideways component of a scroll, in the same units as <see cref="WheelDelta"/>. Zero unless the
        /// device has a second scroll axis (a trackpad or a tilt wheel), which is why nothing that only reads
        /// <see cref="WheelDelta"/> had to change.
        /// </summary>
        /// <remarks>
        /// The sign follows AppKit's scrollingDeltaX: positive is a gesture whose content should move to the
        /// right (revealing what is off the left edge), negative moves content left. A widget that acts on it
        /// should zero it, the same way <see cref="WheelDelta"/> is zeroed once consumed, so that an ancestor
        /// does not scroll on the same gesture.
        /// </remarks>
        public int WheelDeltaX { get; set; }

        /// <summary>
        /// True when both wheel axes are a pixel-precise scroll - a trackpad or other touch surface reporting
        /// travel - rather than a wheel reporting detents. False for every wheel event that does not say
        /// otherwise, which is every one on Windows.
        /// </summary>
        /// <remarks>
        /// This exists to settle who owns DPI. A precise delta is packed by the platform from a physical
        /// distance and already carries the display's scale, so a consumer must move the content by exactly
        /// what it says. A detent carries no distance at all - Win32's 120 means "one click" - so a consumer
        /// picks its own, and scales it by <see cref="GuiWidget.DeviceScale"/> so a click stays worth the same
        /// number of the lines it is drawing. The wheel numbers cannot be told apart on their own, hence the
        /// flag; see <see cref="ScrollableWidget.OnMouseWheel"/> for the consumer this was added for.
        /// </remarks>
        public bool WheelDeltaIsPreciseScroll { get; set; }

        // public Point Location { get; }
        public double X
        { get { return positions[0].X; } set { positions[0] = new Vector2(value, positions[0].Y); } }

        public double Y
        { get { return positions[0].Y; } set { positions[0] = new Vector2(positions[0].X, value); } }

        public Vector2 GetPosition(int index)
        {
            if (index < positions.Count)
            {
                return positions[index];
            }

            return positions[0];
        }
    }
}