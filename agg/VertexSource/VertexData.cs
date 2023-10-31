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
        public VertexData(ShapePath.FlagsAndCommand command, Vector2 position)
        {
            this.command = command;
            this.position = position;
        }

        public VertexData(ShapePath.FlagsAndCommand command, double x, double y)
            : this(command, new Vector2(x, y))
        {
        }

        public ShapePath.FlagsAndCommand command { get; set; }

        [JsonIgnore]
        public bool IsClose => ShapePath.is_close(command);

        [JsonIgnore]
        public bool IsLineTo => ShapePath.is_line_to(command);

        [JsonIgnore]
        public bool IsMoveTo => ShapePath.IsMoveTo(command);

        [JsonIgnore]
        public bool IsStop => ShapePath.IsStop(command);

        [JsonIgnore]
        public bool IsVertex => ShapePath.IsVertex(command);

        public Vector2 position { get; set; }

        [JsonIgnore]
        public double X => position.X;

        [JsonIgnore]
        public double Y => position.Y;

        public override string ToString()
        {
            return $"{command}:{position}";
        }

        public ulong GetLongHashCode(ulong hash = 14695981039346656037)
        {
            hash = position.GetLongHashCode(hash);
            hash = hash * 1099511628211 + (ulong)command;

            return hash;
        }
    }
}