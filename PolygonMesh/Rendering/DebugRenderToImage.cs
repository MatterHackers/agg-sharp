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
    public class DebugRenderToImage
    {
        int xAxis = 0;
        int yAxis = 2;

        int xResolution = 1024;
        int yResolution = 1024;

        Vector2 min = new Vector2(double.MaxValue, double.MaxValue);
        Vector2 max = new Vector2(double.MinValue, double.MinValue);
        ImageBuffer image;
        Graphics2D graphics;

        int padding = 20;
        double scale;
        Vector2 origin;
        Mesh meshToRender;

        RGBA_Bytes vertexColor = RGBA_Bytes.Pink;
        RGBA_Bytes meshEdgeColor = RGBA_Bytes.Green;
        RGBA_Bytes faceEdgeColor = RGBA_Bytes.Orange;
        RGBA_Bytes polygonColor = RGBA_Bytes.Yellow;

        public DebugRenderToImage(Mesh meshToRender)
        {
            this.meshToRender = meshToRender;
            image = new ImageBuffer(xResolution, yResolution, 32, new BlenderBGRA());
            graphics = image.NewGraphics2D();

            // assume project on y for now
            foreach (Vertex vertex in meshToRender.Vertices)
            {
                min.x = Math.Min(min.x, vertex.Position[xAxis]);
                min.y = Math.Min(min.y, vertex.Position[yAxis]);

                max.x = Math.Max(max.x, vertex.Position[xAxis]);
                max.y = Math.Max(max.y, vertex.Position[yAxis]);
            }

            scale = Math.Min((image.Width - padding * 2) / (max.x - min.x), (image.Height - padding * 2) / (max.y - min.y));
            origin = new Vector2(min.x * scale, min.y * scale) - new Vector2(padding, padding);
        }

        public void RenderToTga(string tgaFileName)
        {
            ImageTgaIO.Save(CreateImage(tgaFileName), tgaFileName);
        }

        public ImageBuffer CreateImage(string tgaFileName)
        {
            graphics.Clear(RGBA_Bytes.White);

            foreach (Face faceToRender in meshToRender.Faces)
            {
                Vector2 faceAverageCenter = new Vector2();
                int vertexCount = 0;
                // draw all the vertecies
                foreach (Vertex vertex in faceToRender.VertexIterator())
                {
                    Vector2 imagePosition = GetImagePosition(vertex.Position);
                    faceAverageCenter += imagePosition;
                    vertexCount++;
                }
                faceAverageCenter /= vertexCount;

                foreach (FaceEdge faceEdge in faceToRender.FaceEdgeIterator())
                {
                    // draw the mesh edge
                    DrawMeshEdge(faceEdge.meshEdge);
                    // draw the face edge
                    DrawFaceEdge(faceEdge, faceAverageCenter);
                }

                // draw all the vertecies
                foreach (Vertex vertex in faceToRender.VertexIterator())
                {
                    Vector2 imagePosition = GetImagePosition(vertex.Position);

                    DrawCircle(imagePosition, vertexColor);
                    WriteStringAtPos(vertex.Data.ID.ToString(), imagePosition, new RGBA_Bytes());
                }

                WriteStringAtPos(faceToRender.Data.ID.ToString(), faceAverageCenter, polygonColor);
                DrawRectangle(faceAverageCenter);
                WriteStringAtPos(faceToRender.firstFaceEdge.Data.ID.ToString(), faceAverageCenter + new Vector2(0, -12), faceEdgeColor);
            }

            return image;
        }

        void DrawFaceEdge(FaceEdge faceEdge, Vector2 faceAverageCenter)
        {
            Vector2 start = GetImagePosition(faceEdge.firstVertex.Position);
            Vector2 end = GetImagePosition(faceEdge.nextFaceEdge.firstVertex.Position);

            DrawEdgeLine(MoveTowardsCenter(start, faceAverageCenter), MoveTowardsCenter(end, faceAverageCenter), faceEdge.Data.ID.ToString(), faceEdgeColor);
            graphics.Circle(MoveTowardsCenter(start, faceAverageCenter), 3, RGBA_Bytes.Black);
        }

        private void DrawMeshEdge(MeshEdge meshEdge)
        {
            Vector2 start = GetImagePosition(meshEdge.VertexOnEnd[0].Position);
            Vector2 end = GetImagePosition(meshEdge.VertexOnEnd[1].Position);
            DrawEdgeLine(start, end, "{0}".FormatWith(meshEdge.Data.ID), meshEdgeColor);
            WriteStringAtPos("{0}".FormatWith(meshEdge.firstFaceEdge.Data.ID), (start + end) / 2 + new Vector2(0, -12), faceEdgeColor);

            Vector2 delta = end - start;
            Vector2 normal = delta.GetNormal();
            double length = delta.Length;
            Vector2 left = normal.PerpendicularLeft;

            WriteStringAtPos("{0}".FormatWith(meshEdge.NextMeshEdgeFromEnd[0].Data.ID), start + normal * length * .40, meshEdgeColor);
            WriteStringAtPos("{0}".FormatWith(meshEdge.VertexOnEnd[0].Data.ID), start + normal * length * .10, vertexColor);

            WriteStringAtPos("{0}".FormatWith(meshEdge.NextMeshEdgeFromEnd[1].Data.ID), start + normal * length * .60, meshEdgeColor);
            WriteStringAtPos("{0}".FormatWith(meshEdge.VertexOnEnd[1].Data.ID), start + normal * length * .90, vertexColor);
        }

        private void DrawEdgeLine(Vector2 start, Vector2 end, string stringToWrite, RGBA_Bytes backgroundColor)
        {
            graphics.Line(start, end, RGBA_Bytes.Black);
            
            Vector2 delta = end - start;
            Vector2 normal = delta.GetNormal();
            double length = delta.Length;
            Vector2 left = normal.PerpendicularLeft;

            Vector2 firstArrow = start + normal * length * .80;
            graphics.Line(firstArrow, firstArrow + left * 5 - normal * 5, RGBA_Bytes.Black);
            graphics.Line(firstArrow, firstArrow - left * 5 - normal * 5, RGBA_Bytes.Black);

            graphics.FillRectangle((end + start) / 2 - new Vector2(20, 7), (end + start) / 2 + new Vector2(20, 7), RGBA_Bytes.White);
            Vector2 stringCenter = new Vector2((end.x + start.x) / 2, (end.y + start.y) / 2);
            DrawCircle(stringCenter, backgroundColor);
            WriteStringAtPos(stringToWrite, stringCenter, backgroundColor);
        }

        Vector2 MoveTowardsCenter(Vector2 position, Vector2 center)
        {
            Vector2 delta = position - center;
            delta *= .8;
            delta += center;
            return delta;
        }

        Vector2 GetImagePosition(Vector3 originalPosition)
        {
            return new Vector2(originalPosition[xAxis] * scale - origin.x, originalPosition[yAxis] * scale - origin.y);
        }

        private void DrawCircle(Vector2 imagePosition, RGBA_Bytes color)
        {
            Ellipse circle = new Ellipse(imagePosition, 14);
            graphics.Render(circle, color);
            graphics.Render(new Stroke(circle), RGBA_Bytes.Black);
        }

        private void DrawRectangle(Vector2 imagePosition)
        {
            RoundedRect rect = new RoundedRect(imagePosition.x - 20, imagePosition.y - 7, imagePosition.x + 20, imagePosition.y + 7, 3);
            graphics.Render(new Stroke(rect), RGBA_Bytes.Black);
        }

        private void WriteStringAtPos(string stringToWrite, Vector2 imagePosition, RGBA_Bytes backgroundColor)
        {
            graphics.DrawString(stringToWrite, imagePosition.x, imagePosition.y, 10, justification: Justification.Center, baseline: Baseline.BoundsCenter, color: RGBA_Bytes.Black, backgroundColor: backgroundColor);
        }
    }
}
