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
                // draw all the mesh edges
                Vector2 lastVertexPosition = new Vector2();
                Vector2 firstVertexPosition = new Vector2();

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

                bool isFirst = true;
                int lastFaceEdgeId = 0;
                MeshEdge lastMeshEdge = null;
                foreach (FaceEdge faceEdge in faceToRender.FaceEdgeIterator())
                {
                    Vector2 currentVertexPosition = GetImagePosition(faceEdge.firstVertex.Position);
                    if (!isFirst)
                    {
                        // draw the mesh edge
                        DrawMeshEdge(lastMeshEdge);
                        // draw the face edge
                        DrawEdgeLine(MoveTowardsCenter(lastVertexPosition, faceAverageCenter), MoveTowardsCenter(currentVertexPosition, faceAverageCenter), lastFaceEdgeId.ToString());
                        graphics.Circle(MoveTowardsCenter(lastVertexPosition, faceAverageCenter), 3, RGBA_Bytes.Black);
                    }
                    else
                    {
                        firstVertexPosition = currentVertexPosition;
                        isFirst = false;
                    }
                    lastVertexPosition = currentVertexPosition;
                    lastFaceEdgeId = faceEdge.Data.ID;
                    lastMeshEdge = faceEdge.meshEdge;
                }

                // draw mesh edge
                // draw the mesh edge
                DrawMeshEdge(lastMeshEdge);
                // draw the face edge
                DrawEdgeLine(MoveTowardsCenter(lastVertexPosition, faceAverageCenter), MoveTowardsCenter(firstVertexPosition, faceAverageCenter), lastFaceEdgeId.ToString());
                graphics.Circle(MoveTowardsCenter(lastVertexPosition, faceAverageCenter), 3, RGBA_Bytes.Black);

                // draw all the vertecies
                foreach (Vertex vertex in faceToRender.VertexIterator())
                {
                    Vector2 imagePosition = GetImagePosition(vertex.Position);

                    DrawCircle(imagePosition);
                    WriteStringAtPos(vertex.Data.ID.ToString(), imagePosition);
                }

                DrawRectangle(faceAverageCenter);
                WriteStringAtPos(faceToRender.Data.ID.ToString(), faceAverageCenter);
                WriteStringAtPos(faceToRender.firstFaceEdge.Data.ID.ToString(), faceAverageCenter + new Vector2(0, -12));
            }

            return image;
        }

        private void DrawMeshEdge(MeshEdge lastMeshEdge)
        {
            Vector2 start = GetImagePosition(lastMeshEdge.VertexOnEnd[0].Position);
            Vector2 end = GetImagePosition(lastMeshEdge.VertexOnEnd[1].Position);
            DrawEdgeLine(start, end, "{0}:{1}".FormatWith(lastMeshEdge.VertexOnEnd[0].Data.ID, lastMeshEdge.firstFaceEdge.Data.ID));

            Vector2 delta = end - start;
            Vector2 normal = delta.GetNormal();
            double length = delta.Length;
            Vector2 left = normal.PerpendicularLeft;

            WriteStringAtPos("{0}".FormatWith(lastMeshEdge.NextMeshEdgeFromEnd[0].Data.ID), start + normal * length * .40);
            WriteStringAtPos("{0}".FormatWith(lastMeshEdge.VertexOnEnd[0].Data.ID), start + normal * length * .10);

            WriteStringAtPos("{0}".FormatWith(lastMeshEdge.NextMeshEdgeFromEnd[1].Data.ID), start + normal * length * .60);
            WriteStringAtPos("{0}".FormatWith(lastMeshEdge.VertexOnEnd[1].Data.ID), start + normal * length * .90);
        }

        private void DrawEdgeLine(Vector2 start, Vector2 end, string stringToWrite)
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
            WriteStringAtPos(stringToWrite, new Vector2((end.x + start.x) / 2, (end.y + start.y) / 2));
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

        private void DrawCircle(Vector2 imagePosition)
        {
            Ellipse circle = new Ellipse(imagePosition, 14);
            graphics.Render(circle, RGBA_Bytes.White);
            graphics.Render(new Stroke(circle), RGBA_Bytes.Black);
        }

        private void DrawRectangle(Vector2 imagePosition)
        {
            RoundedRect rect = new RoundedRect(imagePosition.x - 20, imagePosition.y - 7, imagePosition.x + 20, imagePosition.y + 7, 3);
            graphics.Render(rect, RGBA_Bytes.White);
            graphics.Render(new Stroke(rect), RGBA_Bytes.Black);
        }

        private void WriteStringAtPos(string stringToWrite, Vector2 imagePosition)
        {
            graphics.DrawString(stringToWrite, imagePosition.x, imagePosition.y, 10, justification: Justification.Center, baseline: Baseline.BoundsCenter, color: RGBA_Bytes.Black, backgroundColor: RGBA_Bytes.White);
        }
    }
}
