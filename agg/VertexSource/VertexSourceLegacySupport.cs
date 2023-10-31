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
using System.Collections.Generic;

namespace MatterHackers.Agg.VertexSource
{
    public abstract class VertexSourceLegacySupport : IVertexSource
    {
        private IEnumerator<VertexData> currentEnumerator;

        public void Rewind(int layerIndex)
        {
            currentEnumerator = Vertices().GetEnumerator();
            currentEnumerator.MoveNext();
        }

        public FlagsAndCommand Vertex(out double x, out double y)
        {
            if (currentEnumerator == null)
            {
                Rewind(0);
            }

            x = currentEnumerator.Current.Position.X;
            y = currentEnumerator.Current.Position.Y;
            FlagsAndCommand command = currentEnumerator.Current.Command;

            currentEnumerator.MoveNext();

            return command;
        }

        public abstract IEnumerable<VertexData> Vertices();
    }
}