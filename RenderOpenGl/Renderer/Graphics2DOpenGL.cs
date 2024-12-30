/*
Copyright (c) 2024, Lars Brubaker
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

// #define AA_TIPS

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderOpenGl
{
	public class Graphics2DOpenGL : Graphics2D
	{
        // We can have a single static instance because all gl rendering is required to happen on the ui thread so there can
        // be no runtime contention for this object (no thread contention).
        private static readonly Dictionary<ulong, AAGLTesselator> TriangleEdgeInfos = new Dictionary<ulong, AAGLTesselator>();
        private static readonly List<AAGLTesselator> AvailableTriangleEdgeInfos = new List<AAGLTesselator>();
        private static List<ImageBuffer> aATextureImages;

        private static readonly GLTesselator RenderNowTesselator = new GLTesselator();

        private readonly int width;
        private readonly int height;
        private RectangleDouble cachedClipRect;

        public bool DoEdgeAntiAliasing { get; set; } = true;

        public Graphics2DOpenGL(double deviceScale)
        {
            if (AvailableTriangleEdgeInfos.Count == 0)
            {
                for (int i = 0; i < 1000; i++)
                {
                    AvailableTriangleEdgeInfos.Add(new AAGLTesselator());
                }
            }

            DeviceScale = deviceScale;
        }

        public Graphics2DOpenGL(int width, int height, double deviceScale)
            : this(deviceScale)
        {
            this.width = width;
            this.height = height;
            cachedClipRect = new RectangleDouble(0, 0, width, height);
        }

        public override RectangleDouble GetClippingRect() => cachedClipRect;

        public override void SetClippingRect(RectangleDouble clippingRect)
        {
            cachedClipRect = clippingRect;
            GL.Scissor(
                (int)Math.Floor(Math.Max(clippingRect.Left, 0)),
                (int)Math.Floor(Math.Max(clippingRect.Bottom, 0)),
                (int)Math.Ceiling(Math.Max(clippingRect.Width, 0)),
                (int)Math.Ceiling(Math.Max(clippingRect.Height, 0))
            );
            GL.Enable(EnableCap.ScissorTest);
        }

        public override IScanlineCache ScanlineCache
        {
            get => null;
            set => throw new NotImplementedException("There is no scanline cache on a GL surface.");
        }

        public override int Width => width;

        public override int Height => height;

        public void PushOrthoProjection()
        {
            GL.Disable(EnableCap.CullFace);

            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadIdentity();
            GL.Ortho(0, width, 0, height, 0, 1);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadIdentity();
        }

        public void PopOrthoProjection()
        {
            GL.MatrixMode(MatrixMode.Projection);
            GL.PopMatrix();
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PopMatrix();
        }

        private void CheckLineImageCache()
        {
            if (aATextureImages != null) return;

            aATextureImages = new List<ImageBuffer>();
            for (int i = 0; i < 256; i++)
            {
                var texture = new ImageBuffer(1024, 4);
                aATextureImages.Add(texture);
                var hardwarePixelBuffer = texture.GetBuffer();
                for (int y = 0; y < 4; y++)
                {
                    byte alpha = 0;
                    for (int x = 0; x < 1024; x++)
                    {
                        var index = (y * 1024 + x) * 4;
                        hardwarePixelBuffer[index + 0] = 255;
                        hardwarePixelBuffer[index + 1] = 255;
                        hardwarePixelBuffer[index + 2] = 255;
                        hardwarePixelBuffer[index + 3] = alpha;
                        alpha = (byte)i;
                    }
                }
            }
        }

        private void DrawAAShape(IVertexSource vertexSourceIn, IColorType colorIn, bool useCache)
        {
            var vertexSource = vertexSourceIn;
            vertexSource.Rewind(0);

            var translation = Vector2.Zero;
            var transform = GetTransform();

            if (useCache
                && IsTransformIdentity(transform)
                && vertexSource is Ellipse ellipse)
            {
                translation = new Vector2(ellipse.originX, ellipse.originY);
                vertexSource = new Ellipse(0, 0, ellipse.radiusX, ellipse.radiusY, ellipse.NumSteps, ellipse.IsCw);
            }
            else if (useCache
                && vertexSource is VertexSourceApplyTransform applyTransform
                && applyTransform.TransformToApply is Affine affine)
            {
                if ((affine.sx == 1 && affine.sy == 1)
                    || (affine.sx == 0 && affine.sy == 0))
                {
                    vertexSource = applyTransform.VertexSource;
                    translation = new Vector2(affine.tx, affine.ty);
                    affine.tx = 0;
                    affine.ty = 0;
                }
                else
                {
                    useCache = false;
                }
            }

            if (useCache
                && IsTransformIdentity(transform))
            {
                translation.X += transform.tx;
                translation.Y += transform.ty;
            }
            else if (useCache
                && transform.shx == 0
                && transform.shy == 0)
            {
                translation.X = (float)(translation.X / transform.sx + transform.tx);
                translation.Y = (float)(translation.Y / transform.sy + transform.ty);
                transform.tx = 0;
                transform.ty = 0;
                vertexSource = new VertexSourceApplyTransform(vertexSource, transform);
            }
            else
            {
                vertexSource = new VertexSourceApplyTransform(vertexSource, transform);
            }

            SetColor(colorIn);
            var longHash = vertexSource.GetLongHashCode();

            if (AvailableTriangleEdgeInfos.Count == 0)
            {
                MoveTriangleEdgeInfos();
            }

            if (!TriangleEdgeInfos.TryGetValue(longHash, out var triangleEdgeInfo))
            {
                triangleEdgeInfo = GetAvailableTriangleEdgeInfo();
                TriangleEdgeInfos.Add(longHash, triangleEdgeInfo);

                triangleEdgeInfo.Clear();
                using (new QuickTimerReport("Graphics2DOpenGl.SendShapeToTesselator"))
                {
                    VertexSourceToTesselator.SendShapeToTesselator(triangleEdgeInfo, vertexSource);
                }
            }

            RenderTriangleEdgeInfo(triangleEdgeInfo, translation, longHash);
        }

        private static bool IsTransformIdentity(Affine transform)
        {
            return transform.sx == 1 && transform.sy == 1 && transform.shx == 0 && transform.shy == 0;
        }

        private static void SetColor(IColorType colorIn)
        {
            var colorBytes = colorIn.ToColor();
            GL.Color4(colorBytes.red, colorBytes.green, colorBytes.blue, (byte)255);
        }

        private static void MoveTriangleEdgeInfos()
        {
            foreach (var triangleEdgeInfoToMove in TriangleEdgeInfos.Values)
            {
                AvailableTriangleEdgeInfos.Add(triangleEdgeInfoToMove);
            }
            TriangleEdgeInfos.Clear();
        }

        private static AAGLTesselator GetAvailableTriangleEdgeInfo()
        {
            var triangleEdgeInfo = AvailableTriangleEdgeInfos[^1];
            AvailableTriangleEdgeInfos.RemoveAt(AvailableTriangleEdgeInfos.Count - 1);
            return triangleEdgeInfo;
        }

        private static void RenderTriangleEdgeInfo(AAGLTesselator triangleEdgeInfo, Vector2 translation)
        {
            using (new QuickTimerReport("Graphics2DOpenGl.RenderLastToGL"))
            {
                GL.Translate(translation.X, translation.Y, 0);
                triangleEdgeInfo.RenderLastToGL();
                GL.Translate(-translation.X, -translation.Y, 0);
            }
        }

        private const int MaxCacheSize = 1000;
        private static readonly Dictionary<ulong, int> _displayListCache = new();

        public void RenderTriangleEdgeInfo(AAGLTesselator triangleEdgeInfo, Vector2 translation, ulong cacheKey)
        {
            using (new QuickTimerReport("Graphics2DOpenGl.RenderLastToGL"))
            {
                var useLists = true;
                {
                    if (useLists)
                    {
                        int displayListId;

                        if (!_displayListCache.TryGetValue(cacheKey, out displayListId))
                        {
                            // Create a new display list
                            displayListId = GL.GenLists(1);
                            GL.NewList(displayListId, GL.GL_COMPILE);

                            // Perform the rendering
                            triangleEdgeInfo.RenderLastToGL();

                            GL.EndList();

                            // Add to cache
                            AddToCache(cacheKey, displayListId);
                        }
                        else
                        {
                            var a = 0;
                        }

                        // Call the cached display list
                        GL.Translate(translation.X, translation.Y, 0);
                        GL.CallList(displayListId);
                        GL.Translate(-translation.X, -translation.Y, 0);
                    }
                    else
                    {
                        GL.Translate(translation.X, translation.Y, 0);
                        triangleEdgeInfo.RenderLastToGL();
                        GL.Translate(-translation.X, -translation.Y, 0);
                    }
                }
            }
        }

        private void AddToCache(ulong cacheKey, int displayListId)
        {
            if (_displayListCache.Count >= MaxCacheSize)
            {
                // Clear and release all cached display lists if the cache size exceeds the limit
                foreach (var id in _displayListCache.Values)
                {
                    GL.DeleteLists(id, 1);
                }
                _displayListCache.Clear();
            }

            _displayListCache[cacheKey] = displayListId;
        }

        public void PreRender(IColorType colorIn)
        {
            CheckLineImageCache();
            PushOrthoProjection();

            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, RenderOpenGl.ImageGlPlugin.GetImageGlPlugin(aATextureImages[colorIn.Alpha0To255], false).GLTextureHandle);
            GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Blend);
        }

        public override void Render(IVertexSource vertexSource, IColorType colorIn)
        {
            PreRender(colorIn);

            if (DoEdgeAntiAliasing)
            {
                using (new QuickTimerReport("Graphics2DOpenGl.DrawAAShape"))
                {
                    DrawAAShape(vertexSource, colorIn, true);
                }
            }
            else
            {
                vertexSource.Rewind(0);
                var transform = GetTransform();
                if (!transform.is_identity())
                {
                    vertexSource = new VertexSourceApplyTransform(vertexSource, transform);
                }

                SetColor(colorIn);
                RenderNowTesselator.Clear();
                VertexSourceToTesselator.SendShapeToTesselator(RenderNowTesselator, vertexSource);
            }

            PopOrthoProjection();
        }

        public override void Render(IImageByte source, double x, double y, double angleRadians, double scaleX, double scaleY)
        {
            var transform = GetTransform();
            if (!transform.is_identity())
            {
                transform.Transform(ref x, ref y);
                scaleX *= transform.sx;
                scaleY *= transform.sy;
            }

            var sourceBounds = source.GetBounds();
            sourceBounds.Offset((int)x, (int)y);
            var destBounds = new RectangleInt((int)cachedClipRect.Left, (int)cachedClipRect.Bottom, (int)cachedClipRect.Right, (int)cachedClipRect.Top);

            if (!RectangleInt.DoIntersect(sourceBounds, destBounds))
            {
                if (scaleX != 1 || scaleY != 1)
                {
                    // TODO: <BUG> make this work when there is rotation
                    // throw new NotImplementedException();
                }

                // return;
            }

            var sourceAsImageBuffer = (ImageBuffer)source;
            var glPlugin = ImageGlPlugin.GetImageGlPlugin(sourceAsImageBuffer, false);

            PushOrthoProjection();
            GL.Disable(EnableCap.Lighting);
            GL.Enable(EnableCap.Texture2D);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            GL.Translate(x, y, 0);
            GL.Rotate(MathHelper.RadiansToDegrees(angleRadians), 0, 0, 1);
            GL.Scale(scaleX, scaleY, 1);

            GL.Color4(Color.White);
            glPlugin.DrawToGL();

            PopOrthoProjection();
        }

        public override void Render(IImageFloat imageSource, double x, double y, double angleDegrees, double scaleX, double scaleY)
        {
            throw new NotImplementedException();
        }

        public override void Rectangle(double left, double bottom, double right, double top, Color color, double strokeWidth)
        {
            var transform = GetTransform();
            var fastLeft = left;
            var fastBottom = bottom;
            var fastRight = right;
            var fastTop = top;

            transform.Transform(ref fastLeft, ref fastBottom);
            transform.Transform(ref fastRight, ref fastTop);

            if (IsPixelAligned(fastLeft, fastBottom, fastRight, fastTop) && strokeWidth == 1)
            {
                DrawOptimizedRectangle(left, bottom, right, top, color);
            }
            else
            {
                var rect = new RoundedRect(left + 0.5, bottom + 0.5, right - 0.5, top - 0.5, 0);
                var rectOutline = new Stroke(rect, strokeWidth);
                Render(rectOutline, color);
            }
        }

        private static bool IsPixelAligned(params double[] values)
        {
            foreach (var value in values)
            {
                if (Math.Abs(value - (int)value) >= 0.01) return false;
            }
            return true;
        }

        private void DrawOptimizedRectangle(double left, double bottom, double right, double top, Color color)
        {
            FillRectangle(left, bottom, right, bottom + 1, color);
            FillRectangle(left, top, right, top - 1, color);
            FillRectangle(left, bottom, left + 1, top, color);
            FillRectangle(right - 1, bottom, right, top, color);
        }

        public override void FillRectangle(double left, double bottom, double right, double top, IColorType fillColor)
        {
            var transform = GetTransform();
            var fastLeft = left;
            var fastBottom = bottom;
            var fastRight = right;
            var fastTop = top;

            transform.Transform(ref fastLeft, ref fastBottom);
            transform.Transform(ref fastRight, ref fastTop);

            if (IsPixelAligned(fastLeft, fastBottom, fastRight, fastTop))
            {
                PushOrthoProjection();

                GL.Disable(EnableCap.Texture2D);
                GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                GL.EnableOrDisable(EnableCap.Blend, fillColor.Alpha0To255 < 255);

                GL.Color4(fillColor.Red0To255, fillColor.Green0To255, fillColor.Blue0To255, fillColor.Alpha0To255);

                DrawRectangle(fastLeft, fastBottom, fastRight, fastTop);

                PopOrthoProjection();
            }
            else
            {
                var rect = new RoundedRect(left, bottom, right, top, 0);
                Render(rect, fillColor.ToColor());
            }
        }

        private static void DrawRectangle(double fastLeft, double fastBottom, double fastRight, double fastTop)
        {
            GL.Begin(BeginMode.Triangles);

            GL.Vertex2(fastLeft, fastBottom);
            GL.Vertex2(fastRight, fastBottom);
            GL.Vertex2(fastRight, fastTop);

            GL.Vertex2(fastLeft, fastBottom);
            GL.Vertex2(fastRight, fastTop);
            GL.Vertex2(fastLeft, fastTop);

            GL.End();
        }

        public override void Line(double x1, double y1, double x2, double y2, Color color, double strokeWidth = 1)
        {
            strokeWidth = strokeWidth == -1 ? 1 * DeviceScale : strokeWidth;

            var strokeBounds = x1 == x2 // vertical line
                ? new RectangleDouble(x1 - strokeWidth / 2, y1, x1 + strokeWidth / 2, y2)
                : new RectangleDouble(x1, y1 - strokeWidth / 2, x2, y1 + strokeWidth / 2); // horizontal line

            if (IsAlignedLine(x1, y1, x2, y2, strokeBounds))
            {
                FillRectangle(strokeBounds, color);
            }
            else
            {
                base.Line(x1, y1, x2, y2, color, strokeWidth);
            }
        }

        private static bool IsAlignedLine(double x1, double y1, double x2, double y2, RectangleDouble strokeBounds)
        {
            return (x1 == x2 || y1 == y2) && IsPixelAligned(strokeBounds.Left, strokeBounds.Right, strokeBounds.Bottom, strokeBounds.Top);
        }

        public override void Clear(RectangleDouble rect, IColorType color)
        {
            var transform = GetTransform();
            var transformedRect = TransformRectangle(rect, transform);
            var transformedClipRect = TransformRectangle(cachedClipRect, transform);
            transformedClipRect.IntersectWithRectangle(transformedRect);

            var clearRect = new RoundedRect(transformedClipRect, 0);
            Render(clearRect, color.ToColor());
        }

        public override void Clear(IColorType color) => Clear(cachedClipRect, color);

        public void RenderTransformedPath(Matrix4X4 transform, IVertexSource path, Color color, bool doDepthTest)
        {
            CheckLineImageCache();
            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, RenderOpenGl.ImageGlPlugin.GetImageGlPlugin(aATextureImages[color.Alpha0To255], false).GLTextureHandle);
            GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.MultMatrix(transform.GetAsFloatArray());
            GL.EnableOrDisable(EnableCap.DepthTest, doDepthTest);

            affineTransformStack.Push(Affine.NewIdentity());
            DrawAAShape(path, color, false);
            affineTransformStack.Pop();

            GL.PopMatrix();
        }

        private static RectangleDouble TransformRectangle(RectangleDouble rect, Affine transform)
        {
            return new RectangleDouble(
                rect.Left - transform.tx,
                rect.Bottom - transform.ty,
                rect.Right - transform.tx,
                rect.Top - transform.ty
            );
        }
    }
}
