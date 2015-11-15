using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesselate;

namespace MatterHackers.DataConverters2D
{
    public abstract class VertexTesselatorAbstract : Tesselator
    {
        public abstract void AddVertex(double x, double y);
    }

    public class VertexSourceToTesselator
    {
        public static void SendShapeToTesselator(VertexTesselatorAbstract tesselator, IVertexSource vertexSource)
        {
#if !DEBUG
            try
#endif
            {
                tesselator.BeginPolygon();

                ShapePath.FlagsAndCommand PathAndFlags = 0;
                double x, y;
                bool haveBegunContour = false;
                while (!ShapePath.is_stop(PathAndFlags = vertexSource.vertex(out x, out y)))
                {
                    if (ShapePath.is_close(PathAndFlags)
                        || (haveBegunContour && ShapePath.is_move_to(PathAndFlags)))
                    {
                        tesselator.EndContour();
                        haveBegunContour = false;
                    }

                    if (!ShapePath.is_close(PathAndFlags))
                    {
                        if (!haveBegunContour)
                        {
                            tesselator.BeginContour();
                            haveBegunContour = true;
                        }

                        tesselator.AddVertex(x, y);
                    }
                }

                if (haveBegunContour)
                {
                    tesselator.EndContour();
                }

                tesselator.EndPolygon();
            }
#if !DEBUG
            catch
            {
            }
#endif
        }
    }
}
