/*
Copyright (c) 2026, Lars Brubaker
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
using System.Runtime.CompilerServices;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	// NOTE: GL render path is deprecated and will be removed. D3D is the active render path.
	public class Graphics2DGpu : Graphics2D
	{
        public readonly GL gl;

        /// <summary>
        /// The GL objects a Graphics2DGpu caches - tesselators that emit immediate mode vertices into
        /// a captured <see cref="GL"/>, and display list ids minted by a specific context - are only
        /// meaningful on the context that created them. These used to be process wide statics on the
        /// assumption that all gl rendering happened on the ui thread, which is no longer true:
        /// MatterCAD renders thumbnails on background worker threads that each own their own GL
        /// context. A tesselator built for the ui thread's context, driven from a worker, pushes
        /// vertices into the ui thread's in-flight vertex buffer and corrupts the paint that is
        /// flushing it.
        /// The caches are keyed by context rather than being plain instance fields because several
        /// call sites build a short lived Graphics2DGpu on every draw - per instance state would
        /// rebuild the tesselator pool and leak a fresh display list every frame.
        /// </summary>
        private class GlContextCaches
        {
            public readonly Dictionary<ulong, AARenderTesselator> TriangleEdgeInfos = new Dictionary<ulong, AARenderTesselator>();
            public readonly List<AARenderTesselator> AvailableTriangleEdgeInfos = new List<AARenderTesselator>();
            public readonly Dictionary<ulong, int> DisplayListCache = new Dictionary<ulong, int>();
            public RenderTesselator RenderNowTesselator;

            /// <summary>
            /// The value of <see cref="cacheGeneration"/> these caches were last reset at. Only ever
            /// read and written by the thread that owns this context.
            /// </summary>
            public int Generation = Volatile.Read(ref cacheGeneration);
        }

        // Weak on the GL so a closed window's caches go away with its context. Guarded because the ui
        // thread and the thumbnail workers all create Graphics2DGpu instances.
        private static readonly ConditionalWeakTable<GL, GlContextCaches> cachesByContext = new ConditionalWeakTable<GL, GlContextCaches>();
        private static readonly object contextCachesLock = new object();

        // Bumped by InvalidateGlCaches. Each context notices the bump on its own thread at its next
        // render and resets its own caches there - see SyncCacheGeneration.
        private static int cacheGeneration;

        // Mesh tessellation of a path is pure cpu work with no gl affinity, so it can stay shared -
        // but it is now reached from more than one thread and needs guarding.
        private static readonly Dictionary<ulong, Mesh> NativeScenePathMeshes = new Dictionary<ulong, Mesh>();

        // The anti-aliasing alpha ramp textures are cpu side ImageBuffers with no gl affinity, so they
        // are built once and never invalidated - only the gl textures made from them are context bound.
        // Volatile plus publish-when-complete so a racing thread can never see a half filled list.
        private static volatile List<ImageBuffer> aATextureImages;
        private static readonly object aATextureImagesLock = new object();

        private readonly GlContextCaches caches;

        private readonly int width;
        private readonly int height;
        private RectangleDouble cachedClipRect;

        public bool DoEdgeAntiAliasing { get; set; } = true;

        /// <summary>
        /// Marks all GL-context-dependent caches, for every context, as stale. Must be called when a GL
        /// context is destroyed and recreated (e.g., between automation tests) to prevent stale
        /// display list IDs and tessellation data from causing rendering failures.
        /// </summary>
        public static void InvalidateGlCaches()
        {
            // Bump a generation rather than reaching into the other contexts' caches. The readers of
            // those caches (DrawAAShape, RenderTriangleEdgeInfo, ...) run lock free on their own
            // threads, so clearing from here - typically the ui thread, while a thumbnail worker is
            // mid render - would corrupt their dictionaries. Each context notices the bump at its next
            // render entry point and resets itself, which is also the only place its display lists can
            // legally be deleted (a list id may only be freed by the context that minted it).
            Interlocked.Increment(ref cacheGeneration);

            lock (NativeScenePathMeshes)
            {
                NativeScenePathMeshes.Clear();
            }

            ImageTexturePlugin.MarkAllImagesNeedRefresh();
        }

        private static GlContextCaches GetCachesForContext(GL gl)
        {
            lock (contextCachesLock)
            {
                if (!cachesByContext.TryGetValue(gl, out var contextCaches))
                {
                    contextCaches = new GlContextCaches();
                    cachesByContext.Add(gl, contextCaches);
                }

                return contextCaches;
            }
        }

        /// <summary>
        /// Resets this context's caches if <see cref="InvalidateGlCaches"/> has run since they were
        /// populated. Called at every entry point that touches the caches, on the thread that owns
        /// this context - which is what makes it safe to delete the display lists here.
        /// </summary>
        private void SyncCacheGeneration()
        {
            var generation = Volatile.Read(ref cacheGeneration);
            if (caches.Generation == generation)
            {
                return;
            }

            foreach (var displayListId in caches.DisplayListCache.Values)
            {
                gl.DeleteLists(displayListId, 1);
            }

            caches.DisplayListCache.Clear();
            caches.TriangleEdgeInfos.Clear();
            caches.AvailableTriangleEdgeInfos.Clear();
            caches.RenderNowTesselator = null;
            caches.Generation = generation;
        }

        public Graphics2DGpu(GL gl, double deviceScale)
        {
            this.gl = gl;

            // D3D11SystemWindow builds a Graphics2DGpu before the device is initialized and again
            // after teardown. Such an instance can not draw anything, so give it throwaway caches
            // instead of keying the context table on null (which throws) or minting a thousand
            // tesselators bound to nothing.
            this.caches = gl == null ? new GlContextCaches() : GetCachesForContext(gl);

            if (gl != null)
            {
                SyncCacheGeneration();

                if (caches.RenderNowTesselator == null)
                {
                    caches.RenderNowTesselator = new RenderTesselator(gl);
                }

                if (caches.AvailableTriangleEdgeInfos.Count == 0)
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        caches.AvailableTriangleEdgeInfos.Add(new AARenderTesselator(gl));
                    }
                }
            }

            DeviceScale = deviceScale;
        }

        public Graphics2DGpu(GL gl, int width, int height, double deviceScale)
            : this(gl, deviceScale)
        {
            this.width = width;
            this.height = height;
            cachedClipRect = new RectangleDouble(0, 0, width, height);
        }

        public override RectangleDouble GetClippingRect() => cachedClipRect;

        public override void SetClippingRect(RectangleDouble clippingRect)
        {
            cachedClipRect = clippingRect;
            gl.Scissor(
                (int)Math.Floor(Math.Max(clippingRect.Left, 0)),
                (int)Math.Floor(Math.Max(clippingRect.Bottom, 0)),
                (int)Math.Ceiling(Math.Max(clippingRect.Width, 0)),
                (int)Math.Ceiling(Math.Max(clippingRect.Height, 0))
            );
            gl.Enable(EnableCap.ScissorTest);
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
            gl.Disable(EnableCap.CullFace);

            gl.MatrixMode(MatrixMode.Projection);
            gl.PushMatrix();
            gl.LoadIdentity();
            gl.Ortho(0, width, 0, height, 0, 1);

            gl.MatrixMode(MatrixMode.Modelview);
            gl.PushMatrix();
            gl.LoadIdentity();
        }

        public void PopOrthoProjection()
        {
            gl.MatrixMode(MatrixMode.Projection);
            gl.PopMatrix();
            gl.MatrixMode(MatrixMode.Modelview);
            gl.PopMatrix();
        }

        /// <summary>
        /// Builds (once) and returns the anti-aliasing alpha ramp images. Returns the list rather than
        /// leaving callers to read the field, so a caller can never index a field that changed between
        /// the check and the read.
        /// </summary>
        private static List<ImageBuffer> GetLineImageCache()
        {
            var existing = aATextureImages;
            if (existing != null) return existing;

            lock (aATextureImagesLock)
            {
                if (aATextureImages != null) return aATextureImages;

                // Fill a local list and publish it only when it is complete - a thumbnail worker and
                // the ui thread can both land here, and a partially filled list would index-fault.
                var textureImages = new List<ImageBuffer>();
                for (int i = 0; i < 256; i++)
                {
                    var texture = new ImageBuffer(1024, 4);
                    textureImages.Add(texture);
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

                aATextureImages = textureImages;
                return textureImages;
            }
        }

        private void DrawAAShape(IVertexSource vertexSourceIn, IColorType colorIn, bool useCache)
        {
            SyncCacheGeneration();

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
            var colorBytes = colorIn.ToColor();
            var longHash = vertexSource.GetLongHashCode();
            // Include color in cache key so same geometry with different colors gets separate display lists
            longHash = longHash * 31 + (ulong)(colorBytes.red | (colorBytes.green << 8) | (colorBytes.blue << 16) | (colorBytes.Alpha0To255 << 24));

            if (caches.AvailableTriangleEdgeInfos.Count == 0)
            {
                MoveTriangleEdgeInfos();
            }

            if (!caches.TriangleEdgeInfos.TryGetValue(longHash, out var triangleEdgeInfo))
            {
                triangleEdgeInfo = GetAvailableTriangleEdgeInfo();
                caches.TriangleEdgeInfos.Add(longHash, triangleEdgeInfo);

                triangleEdgeInfo.Clear();
                //using (new RecursiveReportTimer("Graphics2DOpenGl.SendShapeToTesselator"))
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

        private void SetColor(IColorType colorIn)
        {
            var colorBytes = colorIn.ToColor();
            gl.Color4(colorBytes.red, colorBytes.green, colorBytes.blue, (byte)255);
        }

        private void MoveTriangleEdgeInfos()
        {
            foreach (var triangleEdgeInfoToMove in caches.TriangleEdgeInfos.Values)
            {
                caches.AvailableTriangleEdgeInfos.Add(triangleEdgeInfoToMove);
            }
            caches.TriangleEdgeInfos.Clear();
        }

        private AARenderTesselator GetAvailableTriangleEdgeInfo()
        {
            var available = caches.AvailableTriangleEdgeInfos;
            if (available.Count == 0)
            {
                // The pool is emptied by a generation reset; refill it bound to this context's gl.
                return new AARenderTesselator(gl);
            }

            var triangleEdgeInfo = available[^1];
            available.RemoveAt(available.Count - 1);
            return triangleEdgeInfo;
        }

        private void RenderTriangleEdgeInfo(AARenderTesselator triangleEdgeInfo, Vector2 translation)
        {
            //using (new RecursiveReportTimer("Graphics2DOpenGl.RenderLastToGL"))
            {
                gl.Translate(translation.X, translation.Y, 0);
                triangleEdgeInfo.RenderLastToGL();
                gl.Translate(-translation.X, -translation.Y, 0);
            }
        }

        private const int MaxCacheSize = 1000;

        public void RenderTriangleEdgeInfo(AARenderTesselator triangleEdgeInfo, Vector2 translation, ulong cacheKey)
        {
            SyncCacheGeneration();

            //using (new RecursiveReportTimer("Graphics2DOpenGl.RenderLastToGL"))
            {
                var useLists = true;
                {
                    if (useLists)
                    {
                        int displayListId;

                        if (!caches.DisplayListCache.TryGetValue(cacheKey, out displayListId))
                        {
                            // Create a new display list
                            displayListId = gl.GenLists(1);
                            gl.NewList(displayListId, GL.GL_COMPILE);

                            // Perform the rendering
                            triangleEdgeInfo.RenderLastToGL();

                            gl.EndList();

                            // Add to cache
                            AddToCache(cacheKey, displayListId);
                        }
                        else
                        {
                            var a = 0;
                        }

                        // Call the cached display list
                        gl.Translate(translation.X, translation.Y, 0);
                        gl.CallList(displayListId);
                        gl.Translate(-translation.X, -translation.Y, 0);
                    }
                    else
                    {
                        gl.Translate(translation.X, translation.Y, 0);
                        triangleEdgeInfo.RenderLastToGL();
                        gl.Translate(-translation.X, -translation.Y, 0);
                    }
                }
            }
        }

        private void AddToCache(ulong cacheKey, int displayListId)
        {
            if (caches.DisplayListCache.Count >= MaxCacheSize)
            {
                // Clear and release all cached display lists if the cache size exceeds the limit
                foreach (var id in caches.DisplayListCache.Values)
                {
                    gl.DeleteLists(id, 1);
                }
                caches.DisplayListCache.Clear();
            }

            caches.DisplayListCache[cacheKey] = displayListId;
        }

        public void PreRender(IColorType colorIn)
        {
            SyncCacheGeneration();

            var lineImages = GetLineImageCache();
            PushOrthoProjection();

            gl.Enable(EnableCap.Texture2D);
            gl.BindTexture(TextureTarget.Texture2D, RenderGl.ImageTexturePlugin.GetImageTexturePlugin(gl, lineImages[colorIn.Alpha0To255], false).GLTextureHandle);
            gl.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            gl.Enable(EnableCap.Blend);
        }

        /// <inheritdoc/>
        protected override void RenderVertexSource(IVertexSource vertexSource, IColorType colorIn)
        {
            PreRender(colorIn);

            if (DoEdgeAntiAliasing)
            {
                //using (new RecursiveReportTimer("Graphics2DOpenGl.DrawAAShape"))
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
                // May have been dropped by a generation reset; it has to be bound to this context's gl.
                var renderNowTesselator = caches.RenderNowTesselator ??= new RenderTesselator(gl);
                renderNowTesselator.Clear();
                VertexSourceToTesselator.SendShapeToTesselator(renderNowTesselator, vertexSource);
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
            var glPlugin = ImageTexturePlugin.GetImageTexturePlugin(gl, sourceAsImageBuffer, false);

            PushOrthoProjection();
            gl.Disable(EnableCap.Lighting);
            gl.Enable(EnableCap.Texture2D);
            gl.Disable(EnableCap.DepthTest);
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            gl.Translate(x, y, 0);
            gl.Rotate(MathHelper.RadiansToDegrees(angleRadians), 0, 0, 1);
            gl.Scale(scaleX, scaleY, 1);

            gl.Color4(Color.White);
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

                gl.Disable(EnableCap.Texture2D);
                gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                gl.EnableOrDisable(EnableCap.Blend, fillColor.Alpha0To255 < 255);

                gl.Color4(fillColor.Red0To255, fillColor.Green0To255, fillColor.Blue0To255, fillColor.Alpha0To255);

                DrawRectangle(fastLeft, fastBottom, fastRight, fastTop);

                PopOrthoProjection();
            }
            else
            {
                var rect = new RoundedRect(left, bottom, right, top, 0);
                Render(rect, fillColor.ToColor());
            }
        }

        private void DrawRectangle(double fastLeft, double fastBottom, double fastRight, double fastTop)
        {
            gl.Begin(BeginMode.Triangles);

            gl.Vertex2(fastLeft, fastBottom);
            gl.Vertex2(fastRight, fastBottom);
            gl.Vertex2(fastRight, fastTop);

            gl.Vertex2(fastLeft, fastBottom);
            gl.Vertex2(fastRight, fastTop);
            gl.Vertex2(fastLeft, fastTop);

            gl.End();
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

        internal static Mesh CreateNativeScenePathMesh(Matrix4X4 transform, IVertexSource path)
        {
            var sourceMesh = GetOrCreateNativeScenePathMesh(path);
            var transformedMesh = new Mesh(sourceMesh.Vertices, sourceMesh.Faces);
            transformedMesh.Transform(transform);
            return transformedMesh;
        }

        private static Mesh GetOrCreateNativeScenePathMesh(IVertexSource path)
        {
            ulong pathHash = path.GetLongHashCode();

            // Reachable from the ui thread and the thumbnail workers at the same time.
            lock (NativeScenePathMeshes)
            {
                if (NativeScenePathMeshes.TryGetValue(pathHash, out var cachedMesh))
                {
                    return cachedMesh;
                }
            }

            // Native scene rendering composites mesh overlays correctly after opaque content.
            // Cache the local-space tessellation so repeated gizmo redraws only vary by transform.
            // Tessellate outside the lock - it is the expensive part, and holding the lock through it
            // would park the ui thread behind a thumbnail worker. Two threads racing on the same path
            // just tessellate it twice and the first one published wins.
            var mesh = new FlattenCurves(path).Vertices().TriangulateFaces();

            lock (NativeScenePathMeshes)
            {
                if (NativeScenePathMeshes.TryGetValue(pathHash, out var publishedMesh))
                {
                    return publishedMesh;
                }

                NativeScenePathMeshes[pathHash] = mesh;
                return mesh;
            }
        }

        public void RenderTransformedPath(Matrix4X4 transform, IVertexSource path, Color color, bool doDepthTest)
        {
            if (gl?.GpuContext is INativeSceneRenderer nativeSceneRenderer
                && nativeSceneRenderer.IsSceneRenderingActive)
            {
                gl.EnableOrDisable(EnableCap.DepthTest, doDepthTest);

                var mesh = GetOrCreateNativeScenePathMesh(path);
                if (mesh.Faces.Count > 0)
                {
                    var command = new MeshRenderCommand
                    {
                        Mesh = mesh,
                        Color = color,
                        Transform = transform,
                        RenderType = RenderTypes.Shaded,
                        BlendTexture = false,
                        ForceCullBackFaces = false,
						CastsBedShadow = false,
                        Unlit = true,
                    };

                    if (nativeSceneRenderer.CanRender(command)
                        && nativeSceneRenderer.TryRender(command))
                    {
                        return;
                    }
                }
            }

            var lineImages = GetLineImageCache();
            gl.Enable(EnableCap.Texture2D);
            gl.BindTexture(TextureTarget.Texture2D, RenderGl.ImageTexturePlugin.GetImageTexturePlugin(gl, lineImages[color.Alpha0To255], false).GLTextureHandle);
            gl.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            gl.Enable(EnableCap.Blend);
            gl.Disable(EnableCap.CullFace);

            gl.MatrixMode(MatrixMode.Modelview);
            gl.PushMatrix();
            gl.MultMatrix(transform.GetAsFloatArray());
            gl.EnableOrDisable(EnableCap.DepthTest, doDepthTest);

            affineTransformStack.Push(Affine.NewIdentity());
            DrawAAShape(path, color, false);
            affineTransformStack.Pop();

            gl.PopMatrix();
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
