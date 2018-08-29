/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.VectorMath;
using System;
using MatterHackers.Agg.UI;
using Veldrid;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.RenderOpenGl;
using MatterHackers.VeldridProvider.VertexFormats;
using System.Collections.Generic;

namespace MatterHackers.VeldridProvider
{
	public class Graphics2DVeldrid : Graphics2D
	{
		public VeldridWindowProvider windowProvider;
		public VeldridWindowProvider WindowProvider
		{
			get => windowProvider;
			set => windowProvider = value;
		}

		public Graphics2DVeldrid()
		{
		}

		public Graphics2DVeldrid(int width, int height)
		{
			this.Width = width;
			this.Height = height;
		}

		public override IScanlineCache ScanlineCache { get; set; }

		public override int Width { get; }

		public override int Height { get; }

		public override void Clear(IColorType color)
		{
		}

		public override void Render(IVertexSource vertexSource, IColorType colorType)
		{
		}

		public override void FillRectangle(double left, double bottom, double right, double top, IColorType fillColor)
		{
			Affine transform = GetTransform();
			double fastLeft = left;
			double fastBottom = bottom;
			double fastRight = right;
			double fastTop = top;

			// This only works for translation. If we have a rotation or scale in the transform this will have some problems.
			transform.transform(ref fastLeft, ref fastBottom);
			transform.transform(ref fastRight, ref fastTop);

			ShaderData veldridGL = ShaderData.Instance;

			var quadVertices = veldridGL.vertexPositionColor;

			var sl = (float)(-1 + fastLeft / this.Width * 2);
			var sr = (float)(-1 + fastRight / this.Width * 2);

			var st = (float)(-1 + fastTop / this.Height * 2);
			var sb = (float)(-1 + fastBottom / this.Height * 2);

			var color = new RgbaFloat(fillColor.Red0To1, fillColor.Green0To1, fillColor.Blue0To1, fillColor.Alpha0To1);
			quadVertices[0] = new VertexPositionColor(new System.Numerics.Vector2(sl, st), color);
			quadVertices[1] = new VertexPositionColor(new System.Numerics.Vector2(sr, st), color);
			quadVertices[2] = new VertexPositionColor(new System.Numerics.Vector2(sl, sb), color);
			quadVertices[3] = new VertexPositionColor(new System.Numerics.Vector2(sr, sb), color);

			veldridGL.GraphicsDevice.UpdateBuffer(veldridGL.VertexBufferPositionColor, 0, quadVertices);

			// Begin() must be called before commands can be issued.
			veldridGL.CommandList.Begin();

			// We want to render directly to the output window.
			veldridGL.CommandList.SetFramebuffer(veldridGL.GraphicsDevice.SwapchainFramebuffer);

			// Set all relevant state to draw our quad.
			veldridGL.CommandList.SetVertexBuffer(0, veldridGL.VertexBufferPositionColor);
			veldridGL.CommandList.SetIndexBuffer(veldridGL.IndexBufferPositionColor, IndexFormat.UInt16);
			veldridGL.CommandList.SetPipeline(veldridGL.StandardPipeline);

			// Issue a Draw command for a single instance with 4 indices.
			veldridGL.CommandList.DrawIndexed(4, 1, 0, 0, 0);

			// End() must be called before commands can be submitted for execution.
			veldridGL.CommandList.End();
			veldridGL.GraphicsDevice.SubmitCommands(veldridGL.CommandList);
		}

		public override RectangleDouble GetClippingRect() => new RectangleDouble(0, 0, 200, 200);

		public override void Rectangle(double left, double bottom, double right, double top, Color color, double strokeWidth = 1)
		{
		}

		private static Dictionary<IImageByte, ResourceSet> imageTextures = new Dictionary<IImageByte, ResourceSet>();

		public override void Render(IImageByte imageSource, double x, double y, double angleRadians, double scaleX, double ScaleY)
		{
			if (imageSource == null)
			{
				return;
			}

			ShaderData veldridGL = ShaderData.Instance;

			if (!imageTextures.TryGetValue(imageSource, out ResourceSet imageTexture))
			{
				var resourceFactory = veldridGL.GraphicsDevice.ResourceFactory;

				var resourceLayout = resourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
					new ResourceLayoutElementDescription("Tex", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
					new ResourceLayoutElementDescription("SS", ResourceKind.Sampler, ShaderStages.Fragment)));

				var targetTexture = veldridGL.CreateTexture(veldridGL.GraphicsDevice, resourceFactory, TextureUsage.Sampled | TextureUsage.Storage, imageSource as ImageBuffer);

				var _targetTextureView = resourceFactory.CreateTextureView(targetTexture);

				imageTexture = resourceFactory.CreateResourceSet(
					new ResourceSetDescription(
					resourceLayout,
					_targetTextureView,
					veldridGL.GraphicsDevice.PointSampler));

				imageTextures[imageSource] = imageTexture;
			}

			// Use imageTexture

			Affine transform = GetTransform();
			transform *= Affine.NewTranslation(x, y);

			var bounds = imageSource.GetBounds();
			double fastLeft = 0;
			double fastBottom = 0;
			double fastRight = imageSource.Width;
			double fastTop = imageSource.Height;

			// This only works for translation. If we have a rotation or scale in the transform this will have some problems.
			transform.transform(ref fastLeft, ref fastBottom);
			transform.transform(ref fastRight, ref fastTop);

			var quadVerts = veldridGL.quadVerts;

			var sl = (float)(-1 + fastLeft / this.Width * 2);
			var sr = (float)(-1 + fastRight / this.Width * 2);

			var st = (float)(-1 + fastTop / this.Height * 2);
			var sb = (float)(-1 + fastBottom / this.Height * 2);

			quadVerts[0] = new System.Numerics.Vector4(sl, st, 0, 1);
			quadVerts[1] = new System.Numerics.Vector4(sr, st, 1, 1);
			quadVerts[2] = new System.Numerics.Vector4(sr, sb, 1, 0);
			quadVerts[3] = new System.Numerics.Vector4(sl, sb, 0, 0);

			// Begin() must be called before commands can be issued.
			veldridGL.CommandList.Begin();

			veldridGL.GraphicsDevice.UpdateBuffer(veldridGL.TextureVertexBuffer, 0, quadVerts);

			// We want to render directly to the output window.
			veldridGL.CommandList.SetFramebuffer(veldridGL.GraphicsDevice.SwapchainFramebuffer);

			// Set all relevant state to draw our quad.
			veldridGL.CommandList.SetVertexBuffer(0, veldridGL.TextureVertexBuffer);
			veldridGL.CommandList.SetIndexBuffer(veldridGL.TextureIndexBuffer, IndexFormat.UInt16);
			veldridGL.CommandList.SetPipeline(veldridGL.TextureGraphicsPipeline);

			veldridGL.CommandList.SetGraphicsResourceSet(0, imageTexture);

			// Issue a Draw command for a single instance with 4 indices.
			veldridGL.CommandList.DrawIndexed(6, 1, 0, 0, 0);

			// End() must be called before commands can be submitted for execution.
			veldridGL.CommandList.End();
			veldridGL.GraphicsDevice.SubmitCommands(veldridGL.CommandList);
		}

		public override void Render(IImageFloat imageSource, double x, double y, double angleRadians, double scaleX, double ScaleY)
		{
		}

		public override void SetClippingRect(RectangleDouble rect_d)
		{
		}
	}
}