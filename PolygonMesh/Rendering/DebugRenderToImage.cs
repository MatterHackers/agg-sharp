/*
Copyright (c) 2017, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public class DebugRenderToImage
	{
		private int xAxis = 0;
		private int yAxis = 2;

		private int xResolution = 1024;
		private int yResolution = 1024;

		private Vector2 min = new Vector2(double.MaxValue, double.MaxValue);
		private Vector2 max = new Vector2(double.MinValue, double.MinValue);
		private ImageBuffer image;
		private Graphics2D graphics;

		private int padding = 20;
		private double scale;
		private Vector2 origin;
		private Mesh meshToRender;

		private Color vertexColor = Color.Red;
		private Color meshEdgeColor = Color.Orange;
		private Color faceEdgeColor = Color.Yellow;
		private Color faceColor = Color.Green;

		public DebugRenderToImage(Mesh meshToRender)
		{
			this.meshToRender = meshToRender;
			image = new ImageBuffer(xResolution, yResolution);
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

		public void RenderToPng(string pngFileName)
		{
			AggContext.ImageIO.SaveImageData(pngFileName, CreateImage(pngFileName));
		}

		public ImageBuffer CreateImage(string pngFileName)
		{
			graphics.Clear(Color.White);

			// draw all the mesh edges
			foreach (MeshEdge meshEdge in meshToRender.MeshEdges)
			{
				// draw the mesh edge
				DrawMeshEdge(meshEdge);
			}

			// draw all the vertices
			foreach (Vertex vertex in meshToRender.Vertices)
			{
				DrawVertex(vertex);
			}

			foreach (Face faceToRender in meshToRender.Faces)
			{
				Vector2 faceAverageCenter = new Vector2();
				int vertexCount = 0;
				// draw all the vertices
				foreach (Vertex vertex in faceToRender.Vertices())
				{
					Vector2 imagePosition = GetImagePosition(vertex.Position);
					faceAverageCenter += imagePosition;
					vertexCount++;
				}
				faceAverageCenter /= vertexCount;

				foreach (FaceEdge faceEdge in faceToRender.FaceEdges())
				{
					// draw the face edge
					DrawFaceEdge(faceEdge, faceAverageCenter);
				}

				WriteStringAtPos(faceToRender.ID.ToString(), faceAverageCenter, faceColor);
				DrawRectangle(faceAverageCenter);
				WriteStringAtPos(faceToRender.firstFaceEdge.ID.ToString(), faceAverageCenter + new Vector2(0, -12), faceEdgeColor);
			}

			return image;
		}

		private void DrawVertex(Vertex vertex)
		{
			Vector2 imagePosition = GetImagePosition(vertex.Position);

			DrawCircle(imagePosition, vertexColor);
			WriteStringAtPos(vertex.ID.ToString(), imagePosition, new Color());
			WriteStringAtPos("{0}".FormatWith(vertex.FirstMeshEdge.ID), imagePosition + new Vector2(0, -12), meshEdgeColor);
		}

		private void DrawFaceEdge(FaceEdge faceEdge, Vector2 faceAverageCenter)
		{
			Vector2 start = MoveTowardsCenter(GetImagePosition(faceEdge.FirstVertex.Position), faceAverageCenter);
			Vector2 end = MoveTowardsCenter(GetImagePosition(faceEdge.nextFaceEdge.FirstVertex.Position), faceAverageCenter);

			DrawEdgeLine(start, end, faceEdge.ID.ToString(), faceEdgeColor);
			graphics.Circle(start, 3, Color.Black);
			WriteStringAtPos("{0}".FormatWith(faceEdge.meshEdge.ID), (start + end) / 2 + new Vector2(0, -12), meshEdgeColor);
			WriteStringAtPos("{0}".FormatWith(faceEdge.ContainingFace.ID), (start + end) / 2 + new Vector2(0, 12), faceColor);

			Vector2 delta = end - start;
			Vector2 normal = delta.GetNormal();
			double length = delta.Length;
			Vector2 left = normal.GetPerpendicularLeft();

			// draw the starting vertex info
			WriteStringAtPos("{0}".FormatWith(faceEdge.FirstVertex.ID), start + normal * length * .10, vertexColor);

			// draw the next and prev faceEdge info
			WriteStringAtPos("{0}".FormatWith(faceEdge.nextFaceEdge.ID), start + normal * length * .60, faceEdgeColor);
			WriteStringAtPos("{0}".FormatWith(faceEdge.prevFaceEdge.ID), start + normal * length * .40, faceEdgeColor);

			// draw the radialFaceEdge info
			WriteStringAtPos("{0}".FormatWith(faceEdge.radialNextFaceEdge.ID), start + new Vector2(0, 7) + normal * length * .90, faceEdgeColor);
			WriteStringAtPos("{0}".FormatWith(faceEdge.radialPrevFaceEdge.ID), start + new Vector2(0, -7) + normal * length * .90, faceEdgeColor);
		}

		private void DrawMeshEdge(MeshEdge meshEdge)
		{
			Vector2 start = GetImagePosition(meshEdge.VertexOnEnd[0].Position);
			Vector2 end = GetImagePosition(meshEdge.VertexOnEnd[1].Position);
			DrawEdgeLine(start, end, "{0}".FormatWith(meshEdge.ID), meshEdgeColor);
			if (meshEdge.firstFaceEdge != null)
			{
				WriteStringAtPos("{0}".FormatWith(meshEdge.firstFaceEdge.ID), (start + end) / 2 + new Vector2(0, 12), faceEdgeColor);
			}
			else
			{
				WriteStringAtPos("null", (start + end) / 2 + new Vector2(0, 12), faceEdgeColor);
			}

			Vector2 delta = end - start;
			Vector2 normal = delta.GetNormal();
			double length = delta.Length;
			Vector2 left = normal.GetPerpendicularLeft();

			WriteStringAtPos("{0}".FormatWith(meshEdge.NextMeshEdgeFromEnd[0].ID), start + normal * length * .40, meshEdgeColor);
			WriteStringAtPos("{0}".FormatWith(meshEdge.VertexOnEnd[0].ID), start + normal * length * .10, vertexColor);

			WriteStringAtPos("{0}".FormatWith(meshEdge.NextMeshEdgeFromEnd[1].ID), start + normal * length * .60, meshEdgeColor);
			WriteStringAtPos("{0}".FormatWith(meshEdge.VertexOnEnd[1].ID), start + normal * length * .90, vertexColor);
		}

		private void DrawEdgeLine(Vector2 start, Vector2 end, string stringToWrite, Color backgroundColor)
		{
			graphics.Line(start, end, Color.Black);

			Vector2 delta = end - start;
			Vector2 normal = delta.GetNormal();
			double length = delta.Length;
			Vector2 left = normal.GetPerpendicularLeft();

			Vector2 firstArrow = start + normal * length * .80;
			graphics.Line(firstArrow, firstArrow + left * 5 - normal * 5, Color.Black);
			graphics.Line(firstArrow, firstArrow - left * 5 - normal * 5, Color.Black);

			graphics.FillRectangle((end + start) / 2 - new Vector2(20, 7), (end + start) / 2 + new Vector2(20, 7), Color.White);
			Vector2 stringCenter = new Vector2((end.x + start.x) / 2, (end.y + start.y) / 2);
			DrawCircle(stringCenter, backgroundColor);
			WriteStringAtPos(stringToWrite, stringCenter, backgroundColor);
		}

		private Vector2 MoveTowardsCenter(Vector2 position, Vector2 center)
		{
			Vector2 delta = position - center;
			delta *= .75;
			delta += center;
			return delta;
		}

		private Vector2 GetImagePosition(Vector3 originalPosition)
		{
			return new Vector2(originalPosition[xAxis] * scale - origin.x, originalPosition[yAxis] * scale - origin.y);
		}

		private void DrawCircle(Vector2 imagePosition, Color color)
		{
			Ellipse circle = new Ellipse(imagePosition, 14);
			graphics.Render(circle, color);
			graphics.Render(new Stroke(circle), Color.Black);
		}

		private void DrawRectangle(Vector2 imagePosition)
		{
			RoundedRect rect = new RoundedRect(imagePosition.x - 20, imagePosition.y - 7, imagePosition.x + 20, imagePosition.y + 7, 3);
			graphics.Render(new Stroke(rect), Color.Black);
		}

		private void WriteStringAtPos(string stringToWrite, Vector2 imagePosition, Color backgroundColor)
		{
			graphics.DrawString(stringToWrite, imagePosition.x, imagePosition.y, 10, justification: Justification.Center, baseline: Baseline.BoundsCenter, color: Color.Black, backgroundColor: backgroundColor);
		}
	}
}
