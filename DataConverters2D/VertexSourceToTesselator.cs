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

                bool haveBegunContour = false;
				foreach(var vertexData in vertexSource.Vertices())
				{
					if(vertexData.IsStop)
					{
						break;
					}
					if (vertexData.IsClose
						|| (haveBegunContour && vertexData.IsMoveTo))
					{
						tesselator.EndContour();
						haveBegunContour = false;
					}

					if (!vertexData.IsClose)
					{
						if (!haveBegunContour)
						{
							tesselator.BeginContour();
							haveBegunContour = true;
						}

						tesselator.AddVertex(vertexData.position.X, vertexData.position.Y);
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
