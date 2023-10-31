//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.Agg.VertexSource
{
    public struct VertexData
    {
        public VertexData(FlagsAndCommand command, Vector2 position, CommandHint hint = CommandHint.None)
        {
            this.Command = command;
            this.Position = position;
            this.Hint = hint;
        }

        public VertexData(FlagsAndCommand command, double x, double y, CommandHint hint = CommandHint.None)
            : this(command, new Vector2(x, y), hint)
        {
        }

        public FlagsAndCommand Command { get; set; }

        [JsonIgnore]
        public bool IsClose => ShapePath.IsClose(Command);

        [JsonIgnore]
        public bool IsLineTo => ShapePath.IsLineTo(Command);

        [JsonIgnore]
        public bool IsMoveTo => ShapePath.IsMoveTo(Command);

        [JsonIgnore]
        public bool IsStop => ShapePath.IsStop(Command);

        [JsonIgnore]
        public bool IsVertex => ShapePath.IsVertex(Command);

        public Vector2 Position { get; set; }
        public CommandHint Hint { get; }

        [JsonIgnore]
        public double X => Position.X;

        [JsonIgnore]
        public double Y => Position.Y;

        public override string ToString()
        {
            return $"{Command}:{Position}";
        }

        public ulong GetLongHashCode(ulong hash = 14695981039346656037)
        {
            hash = Position.GetLongHashCode(hash);
            hash = hash * 1099511628211 + (ulong)Command;

            return hash;
        }
    }
}