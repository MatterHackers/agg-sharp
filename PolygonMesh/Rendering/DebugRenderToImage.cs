using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Font;

namespace MatterHackers.PolygonMesh
{
    public static class DebugRenderToImage
    {
        static public void RenderToTga(string tgaFileName, Mesh meshToRender)
        {
            ImageTgaIO.Save(CreateImage(tgaFileName, meshToRender), tgaFileName);
        }

        static public ImageBuffer CreateImage(string tgaFileName, Mesh meshToRender)
        {
            // assume project on y for now
            int xAxis = 0;
            int yAxis = 2;

            int xResolution = 1024;
            int yResolution = 1024;

            Vector2 min = new Vector2(double.MaxValue, double.MaxValue);
            Vector2 max = new Vector2(double.MinValue, double.MinValue);
            ImageBuffer image = new ImageBuffer(xResolution, yResolution, 32, new BlenderBGRA());
            Graphics2D graphics = image.NewGraphics2D();
            graphics.Clear(RGBA_Bytes.White);
            foreach (Vertex vertex in meshToRender.Vertices)
            {
                min.x = Math.Min(min.x, vertex.Position[xAxis]);
                min.y = Math.Min(min.y, vertex.Position[yAxis]);

                max.x = Math.Max(max.x, vertex.Position[xAxis]);
                max.y = Math.Max(max.y, vertex.Position[yAxis]);
            }

            int padding = 20;
            double scale = Math.Min((image.Width - padding * 2) / (max.x - min.x), (image.Height - padding * 2) / (max.y - min.y));
            Vector2 origin = new Vector2(min.x * scale, min.y * scale) - new Vector2(padding, padding);

            foreach (Face faceToRender in meshToRender.Faces)
            {
                // draw all the mesh edges
                Vector2 lastVertexPosition = new Vector2();
                Vector2 firstVertexPosition = new Vector2();
                bool isFirst = true;
                int lastId = 0;

                foreach (FaceEdge faceEdge in faceToRender.FaceEdgeIterator())
                {
                    Vector2 currentVertexPosition = new Vector2(faceEdge.firstVertex.Position[xAxis] * scale - origin.x, faceEdge.firstVertex.Position[yAxis] * scale - origin.y);
                    if (!isFirst)
                    {
                        graphics.Line(currentVertexPosition, lastVertexPosition, RGBA_Bytes.Black);
                        graphics.FillRectangle((currentVertexPosition + lastVertexPosition) / 2 - new Vector2(20, 7), (currentVertexPosition + lastVertexPosition) / 2 + new Vector2(20, 7), RGBA_Bytes.White);
                        WriteNumber(graphics, lastId, new Vector2((currentVertexPosition.x + lastVertexPosition.x) / 2, (currentVertexPosition.y + lastVertexPosition.y) / 2));
                    }
                    else
                    {
                        firstVertexPosition = currentVertexPosition;
                        isFirst = false;
                    }
                    lastVertexPosition = currentVertexPosition;
                    lastId = faceEdge.meshEdge.Data.ID;
                }

                graphics.Line(firstVertexPosition, lastVertexPosition, RGBA_Bytes.Black);
                graphics.FillRectangle((firstVertexPosition + lastVertexPosition) / 2 - new Vector2(20, 7), (firstVertexPosition + lastVertexPosition) / 2 + new Vector2(20, 7), RGBA_Bytes.White);
                WriteNumber(graphics, lastId, new Vector2((firstVertexPosition.x + lastVertexPosition.x) / 2, (firstVertexPosition.y + lastVertexPosition.y) / 2));

                Vector2 faceAverageCenter = new Vector2();
                int vertexCount = 0;
                // draw all the vertecies
                foreach (Vertex vertex in faceToRender.VertexIterator())
                {
                    Vector2 imagePosition = new Vector2(vertex.Position[xAxis] * scale - origin.x, vertex.Position[yAxis] * scale - origin.y);

                    DrawCircle(graphics, imagePosition);
                    WriteNumber(graphics, vertex.Data.ID, imagePosition);

                    faceAverageCenter += imagePosition;
                    vertexCount++;
                }
                faceAverageCenter /= vertexCount;

                DrawRectangle(graphics, faceAverageCenter);
                WriteNumber(graphics, faceToRender.Data.ID, faceAverageCenter);
            }

            return image;
        }

        private static void DrawCircle(Graphics2D graphics, Vector2 imagePosition)
        {
            Ellipse circle = new Ellipse(imagePosition, 14);
            graphics.Render(circle, RGBA_Bytes.White);
            graphics.Render(new Stroke(circle), RGBA_Bytes.Black);
        }

        private static void DrawRectangle(Graphics2D graphics, Vector2 imagePosition)
        {
            RoundedRect rect = new RoundedRect(imagePosition.x - 20, imagePosition.y - 7, imagePosition.x + 20, imagePosition.y + 7, 3);
            graphics.Render(rect, RGBA_Bytes.White);
            graphics.Render(new Stroke(rect), RGBA_Bytes.Black);
        }

        private static void WriteNumber(Graphics2D graphics, int number, Vector2 imagePosition)
        {
            graphics.DrawString(number.ToString(), imagePosition.x, imagePosition.y, 10, justification: Justification.Center, baseline: Baseline.BoundsCenter, color: RGBA_Bytes.Black);
        }
    }
}
