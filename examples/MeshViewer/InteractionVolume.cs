/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.RayTracer;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MeshVisualizer
{
	public class ValueDisplayInfo
	{
		private string measureDisplayedString = "";
		private ImageBuffer measureDisplayImage = null;
		string formatString;
		string unitsString;

		public ValueDisplayInfo(string formatString = "{0:0.00}", string unitsString = "mm")
		{
			this.formatString = formatString;
			this.unitsString = unitsString;
		}

		public void DisplaySizeInfo(Graphics2D graphics2D, Vector2 widthDisplayCenter, double size)
		{
			string displayString = formatString.FormatWith(size);
			if (measureDisplayImage == null || measureDisplayedString != displayString)
			{
				measureDisplayedString = displayString;
				TypeFacePrinter printer = new TypeFacePrinter(measureDisplayedString, 16);
				TypeFacePrinter unitPrinter = new TypeFacePrinter(unitsString, 10);
				Double unitPrinterOffset = 1;

				BorderDouble margin = new BorderDouble(5);
				printer.Origin = new Vector2(margin.Left, margin.Bottom);
				RectangleDouble bounds = printer.LocalBounds;

				unitPrinter.Origin = new Vector2(bounds.Right + unitPrinterOffset, margin.Bottom);
				RectangleDouble unitPrinterBounds = unitPrinter.LocalBounds;

				measureDisplayImage = new ImageBuffer((int)(bounds.Width + margin.Width + unitPrinterBounds.Width + unitPrinterOffset), (int)(bounds.Height + margin.Height), 32, new BlenderBGRA());
				// make sure the texture has mipmaps (so it can reduce well)
				ImageGlPlugin glPlugin = ImageGlPlugin.GetImageGlPlugin(measureDisplayImage, true);
				Graphics2D widthGraphics = measureDisplayImage.NewGraphics2D();
				widthGraphics.Clear(new RGBA_Bytes(RGBA_Bytes.White, 128));
				printer.Render(widthGraphics, RGBA_Bytes.Black);
				unitPrinter.Render(widthGraphics, RGBA_Bytes.Black);
			}

			widthDisplayCenter -= new Vector2(measureDisplayImage.Width / 2, measureDisplayImage.Height / 2);
			graphics2D.Render(measureDisplayImage, widthDisplayCenter);
		}
	}

	public class InteractionVolume
	{
		public bool MouseDownOnControl;
		public Matrix4X4 TotalTransform = Matrix4X4.Identity;
		private MeshViewerWidget meshViewerToDrawWith;

		private bool mouseOver = false;

		public InteractionVolume(IPrimitive collisionVolume, MeshViewerWidget meshViewerToDrawWith)
		{
			this.CollisionVolume = collisionVolume;
			this.meshViewerToDrawWith = meshViewerToDrawWith;
		}

		[Flags]
		public enum LineArrows { None = 0, Start = 1, End = 2, Both = 3 };

		public IPrimitive CollisionVolume { get; set; }

		public bool DrawOnTop { get; protected set; }

		public MeshViewerWidget MeshViewerToDrawWith { get { return meshViewerToDrawWith; } }

		public bool MouseOver
		{
			get
			{
				return mouseOver;
			}

			set
			{
				if (mouseOver != value)
				{
					mouseOver = value;
					Invalidate();
				}
			}
		}

		public IntersectInfo MouseMoveInfo { get; set; }

		public static void DrawMeasureLine(Graphics2D graphics2D, Vector2 lineStart, Vector2 lineEnd, RGBA_Bytes color, LineArrows arrows)
		{
			graphics2D.Line(lineStart, lineEnd, RGBA_Bytes.Black);

			Vector2 direction = lineEnd - lineStart;
			if (direction.LengthSquared > 0
				&& (arrows.HasFlag(LineArrows.Start) || arrows.HasFlag(LineArrows.End)))
			{
				PathStorage arrow = new PathStorage();
				arrow.MoveTo(-3, -5);
				arrow.LineTo(0, 0);
				arrow.LineTo(3, -5);
				if (arrows.HasFlag(LineArrows.End))
				{
					double rotation = Math.Atan2(direction.y, direction.x);
					IVertexSource correctRotation = new VertexSourceApplyTransform(arrow, Affine.NewRotation(rotation - MathHelper.Tau / 4));
					IVertexSource inPosition = new VertexSourceApplyTransform(correctRotation, Affine.NewTranslation(lineEnd));
					graphics2D.Render(inPosition, RGBA_Bytes.Black);
				}
				if (arrows.HasFlag(LineArrows.Start))
				{
					double rotation = Math.Atan2(direction.y, direction.x) + MathHelper.Tau / 2;
					IVertexSource correctRotation = new VertexSourceApplyTransform(arrow, Affine.NewRotation(rotation - MathHelper.Tau / 4));
					IVertexSource inPosition = new VertexSourceApplyTransform(correctRotation, Affine.NewTranslation(lineStart));
					graphics2D.Render(inPosition, RGBA_Bytes.Black);
				}
			}
		}

		public static void RenderTransformedPath(Matrix4X4 transform, IVertexSource path, RGBA_Bytes color, bool doDepthTest)
		{
			GL.Disable(EnableCap.Texture2D);

			GL.MatrixMode(MatrixMode.Modelview);
			GL.PushMatrix();
			GL.MultMatrix(transform.GetAsFloatArray());
			//GL.DepthMask(false);
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Disable(EnableCap.Lighting);
			if (doDepthTest)
			{
				GL.Enable(EnableCap.DepthTest);
			}
			else
			{
				GL.Disable(EnableCap.DepthTest);
			}

			Graphics2DOpenGL openGlRender = new Graphics2DOpenGL();
			openGlRender.DrawAAShape(path, color);

			GL.PopMatrix();
		}

		public virtual void Draw2DContent(Agg.Graphics2D graphics2D)
		{
		}

		public virtual void DrawGlContent(EventArgs e)
		{
		}

		public void Invalidate()
		{
			MeshViewerToDrawWith.Invalidate();
		}

		public virtual void OnMouseDown(MouseEvent3DArgs mouseEvent3D)
		{
			MouseDownOnControl = true;
			MeshViewerToDrawWith.Invalidate();
		}

		public virtual void OnMouseMove(MouseEvent3DArgs mouseEvent3D)
		{
		}

		public virtual void OnMouseUp(MouseEvent3DArgs mouseEvent3D)
		{
			MouseDownOnControl = false;
		}

		public virtual void SetPosition(IObject3D selectedItem)
		{
		}

		public static Vector3 SetBottomControlHeight(AxisAlignedBoundingBox originalSelectedBounds, Vector3 cornerPosition)
		{
			if (originalSelectedBounds.minXYZ.z < 0)
			{
				if (originalSelectedBounds.maxXYZ.z < 0)
				{
					cornerPosition.z = originalSelectedBounds.maxXYZ.z;
				}
				else
				{
					cornerPosition.z = 0;
				}
			}

			return cornerPosition;
		}
	}
}