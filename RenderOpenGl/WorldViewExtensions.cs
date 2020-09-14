/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
	public static class WorldViewExtensions
	{
		private static Mesh scaledLineMesh = PlatonicSolids.CreateCube();

		private static Mesh unscaledLineMesh = PlatonicSolids.CreateCube();

		public static Frustum GetClippingFrustum(this WorldView world)
		{
			var frustum = Frustum.FrustumFromProjectionMatrix(world.ProjectionMatrix);
			var frustum2 = Frustum.Transform(frustum, world.InverseModelviewMatrix);

			return frustum2;
		}

		/// <summary>
		/// Draw a line in the scene in 3D but scale it such that it appears as a 2D line in the view.
		/// If drawing lots of lines call with a pre-calculated clipping frustum.
		/// </summary>
		/// <param name="world"></param>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="color"></param>
		/// <param name="doDepthTest"></param>
		/// <param name="width"></param>
		public static void Render3DLine(this WorldView world, Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1)
		{
			world.Render3DLine(world.GetClippingFrustum(), start, end, color, doDepthTest, width);
		}

		/// <summary>
		/// Draw a line in the scene in 3D but scale it such that it appears as a 2D line in the view.
		/// </summary>
		/// <param name="world"></param>
		/// <param name="clippingFrustum">This is a cache of the frustum from world.
		/// Much faster to pass this way if drawing lots of lines.</param>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="color"></param>
		/// <param name="doDepthTest"></param>
		/// <param name="width"></param>
		public static void Render3DLine(this WorldView world, Frustum clippingFrustum, Vector3 start, Vector3 end, Color color, bool doDepthTest = true, double width = 1)
		{
			GL.PushAttrib(AttribMask.EnableBit);
			GLHelper.PrepareFor3DLineRender(doDepthTest);
			world.Render3DLineNoPrep(clippingFrustum, start, end, color, width);
			GL.PopAttrib();
		}

		public static void Render3DLineNoPrep(this WorldView world, Frustum clippingFrustum, Vector3Float start, Vector3Float end, Color color, double width = 1)
		{
			world.Render3DLineNoPrep(clippingFrustum, new Vector3(start), new Vector3(end), new Color(color), width);
		}

		public static void Render3DLineNoPrep(this WorldView world, Frustum clippingFrustum, Vector3 start, Vector3 end, Color color, double width = 1)
		{
			if (clippingFrustum.ClipLine(ref start, ref end))
			{
				double unitsPerPixelStart = world.GetWorldUnitsPerScreenPixelAtPosition(start);
				double unitsPerPixelEnd = world.GetWorldUnitsPerScreenPixelAtPosition(end);

				Vector3 delta = start - end;
				var deltaLength = delta.Length;
				var rotateTransform = Matrix4X4.CreateRotation(new Quaternion(Vector3.UnitX + new Vector3(.0001, -.00001, .00002), -delta / deltaLength));
				var scaleTransform = Matrix4X4.CreateScale(deltaLength, 1, 1);
				Vector3 lineCenter = (start + end) / 2;
				Matrix4X4 lineTransform = scaleTransform * rotateTransform * Matrix4X4.CreateTranslation(lineCenter);

				var startScale = unitsPerPixelStart * width;
				var endScale = unitsPerPixelEnd * width;
				for (int i = 0; i < unscaledLineMesh.Vertices.Count; i++)
				{
					var vertexPosition = unscaledLineMesh.Vertices[i];
					if (vertexPosition.X < 0)
					{
						scaledLineMesh.Vertices[i] = new Vector3Float(vertexPosition.X, vertexPosition.Y * startScale, vertexPosition.Z * startScale);
					}
					else
					{
						scaledLineMesh.Vertices[i] = new Vector3Float(vertexPosition.X, vertexPosition.Y * endScale, vertexPosition.Z * endScale);
					}
				}

				if (true)
				{
					GL.Color4(color.Red0To255, color.Green0To255, color.Blue0To255, color.Alpha0To255);

					if (color.Alpha0To1 < 1)
					{
						GL.Enable(EnableCap.Blend);
					}
					else
					{
						// GL.Disable(EnableCap.Blend);
					}

					GL.MatrixMode(MatrixMode.Modelview);
					GL.PushMatrix();
					GL.MultMatrix(lineTransform.GetAsFloatArray());

					GL.Begin(BeginMode.Triangles);
					for (int faceIndex = 0; faceIndex < scaledLineMesh.Faces.Count; faceIndex++)
					{
						var face = scaledLineMesh.Faces[faceIndex];
						var vertices = scaledLineMesh.Vertices;
						var position = vertices[face.v0];
						GL.Vertex3(position.X, position.Y, position.Z);
						position = vertices[face.v1];
						GL.Vertex3(position.X, position.Y, position.Z);
						position = vertices[face.v2];
						GL.Vertex3(position.X, position.Y, position.Z);
					}

					GL.End();
					GL.PopMatrix();
				}
				else
				{
					scaledLineMesh.MarkAsChanged();

					GLHelper.Render(scaledLineMesh, color, lineTransform, RenderTypes.Shaded);
				}
			}
		}

		public static void RenderCylinderOutline(this WorldView world, Matrix4X4 worldMatrix, Vector3 center, double diameter, double height, int sides, Color color, double lineWidth = 1, double extendLineLength = 0)
		{
			world.RenderCylinderOutline(worldMatrix, center, diameter, height, sides, color, color, lineWidth, extendLineLength);
		}

		public static void RenderCylinderOutline(this WorldView world, Matrix4X4 worldMatrix, Vector3 center, double diameter, double height, int sides, Color topBottomRingColor, Color sideLinesColor, double lineWidth = 1, double extendLineLength = 0, double phase = 0)
		{
			GLHelper.PrepareFor3DLineRender(true);
			Frustum frustum = world.GetClippingFrustum();

			for (int i = 0; i < sides; i++)
			{
				var startAngle = MathHelper.Tau * i / sides + phase;
				var rotatedPoint = new Vector3(Math.Cos(startAngle), Math.Sin(startAngle), 0) * diameter / 2;
				var sideTop = Vector3Ex.Transform(center + rotatedPoint + new Vector3(0, 0, height / 2), worldMatrix);
				var sideBottom = Vector3Ex.Transform(center + rotatedPoint + new Vector3(0, 0, -height / 2), worldMatrix);
				var endAngle = MathHelper.Tau * (i + 1) / sides + phase;
				var rotated2Point = new Vector3(Math.Cos(endAngle), Math.Sin(endAngle), 0) * diameter / 2;
				var topStart = sideTop;
				var topEnd = Vector3Ex.Transform(center + rotated2Point + new Vector3(0, 0, height / 2), worldMatrix);
				var bottomStart = sideBottom;
				var bottomEnd = Vector3Ex.Transform(center + rotated2Point + new Vector3(0, 0, -height / 2), worldMatrix);

				if (extendLineLength > 0)
				{
					GLHelper.ExtendLineEnds(ref sideTop, ref sideBottom, extendLineLength);
				}

				if (sideLinesColor != Color.Transparent)
				{
					world.Render3DLineNoPrep(frustum, sideTop, sideBottom, sideLinesColor, lineWidth);
				}

				if (topBottomRingColor != Color.Transparent)
				{
					world.Render3DLineNoPrep(frustum, topStart, topEnd, topBottomRingColor, lineWidth);
					world.Render3DLineNoPrep(frustum, bottomStart, bottomEnd, topBottomRingColor, lineWidth);
				}
			}

			// turn the lighting back on
			GL.Enable(EnableCap.Lighting);
		}

		public static void RenderAabb(this WorldView world, AxisAlignedBoundingBox bounds, Matrix4X4 matrix, Color color, double width, double extendLineLength = 0)
		{
			GLHelper.PrepareFor3DLineRender(true);

			Frustum frustum = world.GetClippingFrustum();
			for (int i = 0; i < 4; i++)
			{
				Vector3 sideStartPosition = Vector3Ex.Transform(bounds.GetBottomCorner(i), matrix);
				Vector3 sideEndPosition = Vector3Ex.Transform(bounds.GetTopCorner(i), matrix);

				Vector3 bottomStartPosition = sideStartPosition;
				Vector3 bottomEndPosition = Vector3Ex.Transform(bounds.GetBottomCorner((i + 1) % 4), matrix);

				Vector3 topStartPosition = sideEndPosition;
				Vector3 topEndPosition = Vector3Ex.Transform(bounds.GetTopCorner((i + 1) % 4), matrix);

				if (extendLineLength > 0)
				{
					GLHelper.ExtendLineEnds(ref sideStartPosition, ref sideEndPosition, extendLineLength);
					GLHelper.ExtendLineEnds(ref topStartPosition, ref topEndPosition, extendLineLength);
					GLHelper.ExtendLineEnds(ref bottomStartPosition, ref bottomEndPosition, extendLineLength);
				}

				// draw each of the edge lines (4) and their touching top and bottom lines (2 each)
				world.Render3DLineNoPrep(frustum, sideStartPosition, sideEndPosition, color, width);
				world.Render3DLineNoPrep(frustum, topStartPosition, topEndPosition, color, width);
				world.Render3DLineNoPrep(frustum, bottomStartPosition, bottomEndPosition, color, width);
			}

			GL.Enable(EnableCap.Lighting);
		}

		public static void RenderAxis(this WorldView world, Vector3 position, Matrix4X4 matrix, double size, double lineWidth)
		{
			GLHelper.PrepareFor3DLineRender(true);

			Frustum frustum = world.GetClippingFrustum();
			Vector3 length = Vector3.One * size;
			for (int i = 0; i < 3; i++)
			{
				var min = position;
				min[i] -= length[i];
				Vector3 start = Vector3Ex.Transform(min, matrix);

				var max = position;
				max[i] += length[i];
				Vector3 end = Vector3Ex.Transform(max, matrix);

				var color = Agg.Color.Red;
				switch (i)
				{
					case 1:
						color = Agg.Color.Green;
						break;

					case 2:
						color = Agg.Color.Blue;
						break;
				}

				// draw each of the edge lines (4) and their touching top and bottom lines (2 each)
				world.Render3DLineNoPrep(frustum, start, end, color, lineWidth);
			}

			GL.Enable(EnableCap.Lighting);
		}

		private static readonly ConditionalWeakTable<WorldView, AAGLTesselator> TesselatorsByWorld = new ConditionalWeakTable<WorldView, AAGLTesselator>();

		public static void RenderPath(this WorldView world, IVertexSource vertexSource, Color color, bool doDepthTest)
		{
			AAGLTesselator tesselator;

			if (!TesselatorsByWorld.TryGetValue(world, out tesselator))
			{
				// Update reference and store in dictionary
				tesselator = new AAGLTesselator(world);

				TesselatorsByWorld.Add(world, tesselator);
			}

			// TODO: Necessary?
			// CheckLineImageCache();
			// GL.Enable(EnableCap.Texture2D);
			// GL.BindTexture(TextureTarget.Texture2D, RenderOpenGl.ImageGlPlugin.GetImageGlPlugin(AATextureImages[color.Alpha0To255], false).GLTextureHandle);

			// the source is always all white so has no does not have its color changed by the alpha
			GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Enable(EnableCap.Blend);

			GL.Disable(EnableCap.CullFace);
			GL.Disable(EnableCap.Lighting);

			if (doDepthTest)
			{
				GL.Enable(EnableCap.DepthTest);
			}
			else
			{
				GL.Disable(EnableCap.DepthTest);
			}

			vertexSource.rewind(0);

			// the alpha has to come from the bound texture
			GL.Color4(color.red, color.green, color.blue, (byte)255);

			tesselator.Clear();
			VertexSourceToTesselator.SendShapeToTesselator(tesselator, vertexSource);

			// now render it
			tesselator.RenderLastToGL();
		}
	}
}
