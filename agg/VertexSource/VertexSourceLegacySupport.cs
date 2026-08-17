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

            // There is no end check here: MoveNext() returning false is not what stops the caller.
            // A Roslyn iterator keeps handing back the last value it yielded once it is exhausted, so
            // ending the caller's while-not-Stop loop is a contract on Vertices() - every implementation
            // must yield an explicit Stop as its final element. A Vertices() that merely yield breaks
            // would leave this repeating whatever came before forever.
            x = currentEnumerator.Current.Position.X;
            y = currentEnumerator.Current.Position.Y;
            FlagsAndCommand command = currentEnumerator.Current.Command;

            currentEnumerator.MoveNext();

            return command;
        }

        /// <summary>
        /// Throws away the cached enumerator so the next Vertex() call re-reads Vertices().
        /// </summary>
        /// <remarks>
        /// Derived types must call this from every mutator that changes vertex-defining state. Vertex()
        /// only builds an enumerator when it does not already have one, so a source that has already been
        /// drained to Stop keeps handing back its exhausted enumerator: the caller reads Stop on the very
        /// first call and the reshaped geometry silently draws nothing. Before this existed the hazard was
        /// worked around one call site at a time by re-Rewinding by hand (see
        /// Gui/PolygonWidget.cs InitControlPointEllipse), which only helped the callers that remembered.
        /// </remarks>
        protected void InvalidateVertices()
        {
            currentEnumerator = null;
        }

        public ulong GetLongHashCode(ulong hash = 14695981039346656037)
        {
            foreach (var vertex in this.Vertices())
            {
                hash = vertex.GetLongHashCode(hash);
            }

            return hash;
        }

        public abstract IEnumerable<VertexData> Vertices();
    }
}