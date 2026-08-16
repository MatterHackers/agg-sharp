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
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;

namespace MatterHackers.RenderGl
{
	// This is the live GPU 2D render path - WebGpuSystemWindow.NewGraphics2D hands out one of these.
	// All drawing goes through the IGpuContext abstraction, which in production is the compat layer over
	// WebGpuRenderDevice (wgpu-native). The OpenGL-flavored vocabulary in GL/IGpuContext is a historical
	// API shape kept from the since-removed OpenGL backend, not a live OpenGL dependency.
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
            /// The CPU raster layer for this context, kept here rather than on the Graphics2DGpu because
            /// the window host builds a fresh one of those every paint and this buffer is megabytes.
            /// </summary>
            public ImageBuffer CpuLayer;

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

            // A Graphics2DGpu can be built with no GL behind it at all - a destination for a window
            // whose device does not exist yet or is already torn down. Such an instance can not draw
            // anything, so give it throwaway caches instead of keying the context table on null
            // (which throws) or minting a thousand tesselators bound to nothing.
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

        /// <summary>
        /// A CPU-rasterized layer the size of this surface, cleared to transparent, that is drawn over the
        /// GPU frame at the end of the frame.
        /// </summary>
        /// <remarks>
        /// <see cref="Graphics2D.DestImage"/> is the agg CPU rasterizer's back buffer, and a GPU surface has
        /// no such thing - the base class field stays null and every widget that reaches for it (the agg
        /// demos that rasterize by hand: aa_demo, FontHinting, gouraud, blur, image_resample and friends)
        /// used to die on an NRE the moment they were hosted on a GPU window.
        /// <para>
        /// So they get a real one. The buffer is plain system memory that agg rasterizes into exactly as it
        /// would on a bitmap window; the difference is only that it reaches the screen as a texture upload
        /// in <see cref="CompositeCpuLayer"/> rather than as a GDI blit. It is allocated on first ask and
        /// then reused, because these demos ask every frame.
        /// </para>
        /// <para>
        /// Deliberately <b>not</b> a read-back of the GPU frame. A demo that draws into it is compositing
        /// its own picture over whatever the GPU drew, and making it start as a copy of the frame would
        /// cost a full stall-and-map every frame to give a picture nothing here reads.
        /// </para>
        /// </remarks>
        public override IImageByte DestImage => this.EnsureCpuLayer();

        /// <summary>True when something has actually asked for <see cref="DestImage"/> on this context, so
        /// the composite is worth doing.</summary>
        public bool HasCpuLayer => this.caches.CpuLayer != null;

        private ImageBuffer EnsureCpuLayer()
        {
            // Asking for DestImage on a GPU surface costs a full-screen upload and composite every frame
            // after, so the first asker is worth naming out loud when the profiler is on.
            MatterHackers.RenderCore.FrameProfiler.FirstTouch("Graphics2DGpu.DestImage");
            MatterHackers.RenderCore.FrameProfiler.Count("DestImageAsks");

            if (this.width <= 0 || this.height <= 0)
            {
                // A Graphics2DGpu built by the deviceless constructor has no size to allocate against.
                // Returning null puts the caller back where it was, which is the honest answer.
                return null;
            }

            var layer = this.caches.CpuLayer;
            if (layer == null || layer.Width != this.width || layer.Height != this.height)
            {
                layer = new ImageBuffer(this.width, this.height, 32, new BlenderBGRA());
                this.caches.CpuLayer = layer;
            }

            return layer;
        }

        /// <summary>
        /// Uploads the CPU raster layer, if there is one, and draws it over the GPU frame; then clears it
        /// so the next frame starts empty. A no-op when nothing asked for <see cref="DestImage"/>.
        /// </summary>
        /// <remarks>
        /// Called by the window host after widget paint and before present. It has to be the host rather
        /// than this class, because a <see cref="Graphics2D"/> has no idea when a frame ends - and drawing
        /// the layer lazily on the next GPU call would put it under, not over, whatever came after it.
        /// </remarks>
        public void CompositeCpuLayer()
        {
            var layer = this.caches.CpuLayer;
            if (layer == null || this.gl == null)
            {
                return;
            }

            // The texture cache is keyed on the ImageBuffer's change count, so this is what makes the
            // upload happen at all - the buffer object is reused across frames on purpose.
            layer.MarkImageChanged();

            var savedTransform = this.GetTransform();
            this.PushTransform();
            try
            {
                this.SetTransform(Affine.NewIdentity());
                this.Render(layer, 0, 0);
            }
            finally
            {
                this.PopTransform();
                this.SetTransform(savedTransform);
            }

            // Cleared rather than freed: these demos redraw the whole layer every frame anyway, and a
            // stale frame showing through would be a far more confusing bug than an extra memset.
            Array.Clear(layer.GetBuffer(), 0, layer.GetBuffer().Length);
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

        /// <summary>
        /// Draws per-vertex-coloured primitives in widget coordinates.
        /// </summary>
        /// <remarks>
        /// The GPU half of the 2D escape hatch. The vertices are already in widget space, so the ortho
        /// projection is pushed around the draw exactly as the colour-picker widgets used to push it
        /// themselves. Depth is left alone: a widget frame has already settled it, and these primitives
        /// are painted in draw order like every other 2D thing on this surface.
        /// </remarks>
        /// <param name="topology">How the vertices assemble into primitives.</param>
        /// <param name="vertices">The vertices, in widget coordinates. Z is ignored.</param>
        public override void DrawColoredPrimitives(DrawTopology topology, ReadOnlySpan<PosColorVertex> vertices)
        {
            if (vertices.Length == 0)
            {
                return;
            }

            PushOrthoProjection();

            try
            {
                GlPrimitiveEmitter.Emit(gl, topology, vertices, Matrix4X4.Identity, depthTest: null);
            }
            finally
            {
                PopOrthoProjection();
            }
        }

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
                MatterHackers.RenderCore.FrameProfiler.Count("TesselateMiss");
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
                            MatterHackers.RenderCore.FrameProfiler.Count("DisplayListMiss");

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
                            MatterHackers.RenderCore.FrameProfiler.Count("DisplayListHit");
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

            // Known asymmetry with the LCD arm, and pre-existing: this is the path a widget's ordinary RGBA
            // backbuffer blits through, and it uses non-premultiplied source over on a source that is in fact
            // premultiplied, which double-darkens partially covered pixels. CompositeLcdBuffer uses the
            // correct One / OneMinusSrcAlpha. Boundary pixels that differ between the two backbuffer modes are
            // therefore this, not an LCD regression - fixing it means auditing every Render(IImageByte) caller.
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

        // ---- The LCD coverage arm of this GL destination ----
        // Compositing a two plane LcdBuffer onto the framebuffer through three color masked passes, and
        // the per-channel pass images those passes sample. It lives on this class rather than in a
        // collaborator because every one of these members is an override that draws through this
        // instance's own gl, ortho projection and scissor.

        // The three per-channel pass images an LcdBuffer composites through. Like aATextureImages these are
        // cpu side ImageBuffers with no gl affinity, so one set is shared by every context and the per
        // context part is left to ImageTexturePlugin, which already keys its textures by (pixel buffer,
        // context) and re-uploads on InvalidateGlCaches through MarkAllImagesNeedRefresh.
        // Weak on the buffer so a widget's planes take their pass images with them when the widget goes.
        private static readonly ConditionalWeakTable<LcdBuffer, LcdBufferChannelImages> lcdChannelImages = new ConditionalWeakTable<LcdBuffer, LcdBufferChannelImages>();
        private static readonly object lcdChannelImagesLock = new object();

        // The same arrangement for a single mask's three pass images. Weak on the mask because a mask lives in
        // LcdMaskCache, which is LRU bounded - an evicted mask has to be able to take its textures with it, and
        // a strong table here would pin every glyph run the process ever drew.
        // No change stamp, where the buffer table needs one: a mask is finished when it is built and is handed
        // out read only (see Graphics2D.CompositeLcdMask), so one pack per mask is all there ever is.
        private static readonly ConditionalWeakTable<LcdMask, ImageBuffer[]> lcdMaskChannelImages = new ConditionalWeakTable<LcdMask, ImageBuffer[]>();
        private static readonly object lcdMaskChannelImagesLock = new object();

        /// <summary>
        /// True: this destination composites a two plane <see cref="LcdBuffer"/> per channel, through three
        /// color masked passes (see <see cref="CompositeLcdBuffer"/>). This is the gate
        /// <c>GuiWidget.ResolveBackbufferMode</c> consults, so turning it on is what lets a GPU rendered
        /// widget choose an LCD coverage backbuffer at all.
        /// </summary>
        /// <remarks>
        /// False without a context - a Graphics2DGpu can be built with no <see cref="GL"/> behind it, and
        /// such an instance cannot draw anything - and false on a
        /// transparent compositing layer, where the base class's rule applies: subpixel geometry computed
        /// against pixels that get blended again later is geometry against unknown content.
        /// </remarks>
        public override bool CanCompositeLcdBuffer => this.gl != null && !this.IsTransparentCompositingLayer;

        /// <summary>
        /// True: this destination composites a single <see cref="LcdMask"/> per channel, through the same
        /// three color masked passes (see <see cref="CompositeLcdMask"/>). This is the gate every ordinary
        /// vector fill consults (<c>Graphics2D.TryRenderThroughLcd</c>), so turning it on is what makes the
        /// user's LCD setting visible in a GPU rendered window at all - text included, since text is only ever
        /// a caller of the vector path.
        /// </summary>
        /// <remarks>
        /// The same two refusals as <see cref="CanCompositeLcdBuffer"/> directly above, for the same reasons:
        /// no context means nothing can be drawn, and a transparent compositing layer's pixels get blended
        /// again later against content the subpixel phase knew nothing about.
        /// <para>
        /// And a third: a surface of no size. The two argument constructor leaves
        /// <see cref="Graphics2DGpu.Width"/> and <see cref="Graphics2DGpu.Height"/> at zero, and it is used in
        /// production - such an instance would push a degenerate <c>glOrtho(0, 0, 0, 0)</c> and enforce a clip
        /// nothing has set. Nothing routes a fill to one of those today, so this is a latent case made
        /// explicit rather than a bug being fixed.
        /// </para>
        /// </remarks>
        public override bool CanCompositeLcd => this.gl != null
            && this.Width > 0
            && this.Height > 0
            && !this.IsTransparentCompositingLayer;

        /// <summary>
        /// Non-zero, always - the rule this class's own fills use.
        /// </summary>
        /// <remarks>
        /// The base class reads the fill rule off its <see cref="ScanlineRasterizer"/>, and this class has
        /// none: it fills by tessellation (<c>VertexSourceToTesselator</c>), whose
        /// <see cref="Tesselate.Tesselator.WindingRule"/> is left at its <c>NonZero</c> default everywhere in
        /// the render path - nothing in RenderGl ever sets it. So the mask is rasterized under exactly the rule
        /// the tesselated fill it replaces would have used, which is the property that matters: the LCD path
        /// must cover the pixels the ordinary path covered, only with per-channel coverage.
        /// </remarks>
        protected override filling_rule_e? LcdFillingRule => filling_rule_e.fill_non_zero;

        /// <summary>
        /// Composites a finished LCD coverage backbuffer onto the framebuffer at whole pixel
        /// (<paramref name="destX"/>, <paramref name="destY"/>), preserving per-channel coverage.
        /// </summary>
        /// <remarks>
        /// <b>The mechanism.</b> Per channel source alpha needs three different alphas for one fragment,
        /// which is dual-source blending's job - not portable, and not expressible in fixed function GL at
        /// all. The reference's answer is to draw the same quad three times, each pass writing only one color
        /// channel and taking that channel's coverage as the source alpha
        /// (<c>demo-wgpu\src\pipelines.rs</c> <c>lcb_r</c> / <c>lcb_g</c> / <c>lcb_b</c>, blend
        /// One / OneMinusSrcAlpha because the color plane is premultiplied). Ported here as
        /// <see cref="OpenGl.GL.ColorMask"/> per pass over the standard premultiplied blend this class
        /// already uses.
        /// <para>
        /// Where the reference selects the channel in a fragment shader from a uniform, this selects it in
        /// the pixels: <see cref="LcdBufferChannelImages"/> pre-reduces the planes to one ordinary
        /// premultiplied BGRA image per pass, holding that channel's color in its own slot and that channel's
        /// coverage in alpha. The texture then <i>is</i> the shader's output, so the whole composite runs on
        /// fixed function texturing with no shader support required - which matters, because this is the
        /// legacy immediate mode GL path and has none.
        /// </para>
        /// <para>
        /// <b>Destination alpha is never written</b>, by all three passes leaving the alpha write mask off.
        /// That is the reference's behaviour exactly (its <c>ColorWrites::RED</c> and friends exclude alpha),
        /// and a deliberate divergence from the software
        /// <see cref="LcdBuffer.CompositeOnto(ImageBuffer, int, int, double, RectangleInt?)"/>, which sets
        /// destination alpha to <c>max</c> over the three channel alphas. There is no third pixel format to
        /// write it in here: this runs against the window's framebuffer, whose alpha is not read by anything
        /// downstream, and a fourth pass to maintain it would cost a full quad for a channel nobody samples.
        /// </para>
        /// <para>
        /// No transform is applied, matching the base class - the planes are finished pixels and resampling
        /// them would smear each channel's phase into its neighbours. Clipping still applies: the caller's
        /// clip rect is already live as the GL scissor (see <see cref="SetClippingRect"/>).
        /// </para>
        /// </remarks>
        public override void CompositeLcdBuffer(LcdBuffer buffer, int destX, int destY)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!this.CanCompositeLcdBuffer)
            {
                // Nothing to composite through; take the base class's collapse so a caller that reached here
                // anyway still gets its pixels.
                base.CompositeLcdBuffer(buffer, destX, destY);
                return;
            }

            if (buffer.Width <= 0 || buffer.Height <= 0)
            {
                return;
            }

            var channelImages = GetLcdChannelImages(buffer);

            PushOrthoProjection();
            gl.Disable(EnableCap.Lighting);
            gl.Enable(EnableCap.Texture2D);
            gl.Disable(EnableCap.DepthTest);
            gl.Enable(EnableCap.Blend);

            // Premultiplied source over, because the color plane is premultiplied per channel:
            // dst_c = src_c + dst_c * (1 - src_alpha_c), with src_alpha_c coming from the pass image's alpha.
            gl.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

            gl.Translate(destX, destY, 0);

            // White under the default modulate texture environment, so each pass emits its texel unchanged.
            gl.Color4(Color.White);

            try
            {
                for (int channel = 0; channel < LcdBufferChannelImages.ChannelCount; channel++)
                {
                    gl.ColorMask(channel == 0, channel == 1, channel == 2, false);
                    ImageTexturePlugin.GetImageTexturePlugin(gl, channelImages[channel], false).DrawToGL();
                }
            }
            finally
            {
                // Restored rather than left set, and restored on the way out of a throw as well: every other
                // draw on this context expects to be able to write all four channels, and a mask stuck at one
                // channel would silently turn the rest of the frame - every frame - monochrome. A pass can
                // throw (the texture uploader allocates, and the wgpu backing of gl raises on a lost device),
                // and WebGpuControl recovers from a lost device by skipping the frame, so without the finally
                // one bad frame would leave the context permanently miscolored.
                gl.ColorMask(true, true, true, true);
            }

            PopOrthoProjection();
        }

        /// <summary>
        /// Composites a finished coverage mask onto the framebuffer at whole pixel
        /// (<paramref name="originX"/>, <paramref name="originY"/>), applying <paramref name="color"/> per
        /// channel.
        /// </summary>
        /// <remarks>
        /// <b>The mechanism is <see cref="CompositeLcdBuffer"/>'s</b> - three passes of the same quad, each
        /// writing one color channel with <see cref="OpenGl.GL.ColorMask"/> and taking that channel's coverage
        /// as its source alpha - and the differences are all about where the color comes from. A buffer carries
        /// its own color per pixel; a mask carries coverage only and is handed a color per draw, which is what
        /// lets one mask serve every color and position it is ever drawn at (see <c>LcdMaskCache</c>). So the
        /// color arrives as the draw color and the default modulate texture environment multiplies it into the
        /// pass texture, leaving the textures a pure function of the mask.
        /// <para>
        /// <b>The premultiplication choice.</b> Pass <c>c</c>'s texture is white premultiplied by that
        /// channel's coverage - all four bytes are the mask byte - and the blend is
        /// One / OneMinusSrcAlpha, matching the buffer composite above. The reference's mask pipeline states
        /// the same thing as straight white over SrcAlpha / OneMinusSrcAlpha, which is <i>not</i> available
        /// here: <see cref="ImageTexturePlugin"/> blits every image it uploads through the image's own blender
        /// onto a transparent destination, and that blit turns a straight
        /// (<see cref="BlenderBGRA"/>) white into a premultiplied one - so a straight pass image would arrive
        /// at the driver already multiplied and SrcAlpha would multiply the coverage in a second time. A
        /// premultiplied image survives that blit byte for byte (see <see cref="LcdBufferChannelImages"/>).
        /// </para>
        /// <para>
        /// The draw color is therefore <b>premultiplied</b> too, which is what makes the arithmetic come out
        /// at <see cref="LcdCoverage.LcdComposite"/>'s: modulate gives
        /// <c>src_c = mask_c * color_c * color_a</c> and <c>src_a = mask_c * color_a</c>, and One /
        /// OneMinusSrcAlpha then lands <c>color_c * cov + dst_c * (1 - cov)</c> with
        /// <c>cov = mask_c * color_a</c>. It is byte identical to the software composite for an opaque color,
        /// which is what text draws with; a translucent one pays up to one byte level of rounding, because
        /// <see cref="OpenGl.GL.Color4(Color)"/> takes bytes and the premultiplied color has to be quantized
        /// into them. Making that exact would need the color inside the texture, which would key the texture
        /// cache by color and defeat the mask cache behind it.
        /// </para>
        /// <para>
        /// <b>Destination alpha is never written</b> and no transform is applied, both exactly as
        /// <see cref="CompositeLcdBuffer"/> - see its remarks.
        /// </para>
        /// </remarks>
        /// <param name="clip">
        /// Not applied here, and not ignored either: on this destination it is
        /// <see cref="GetClippingRect"/>'s rect, which <see cref="SetClippingRect"/> has already installed as
        /// the GL scissor, live for every pass - so re-clipping would be the same rectangle enforced twice.
        /// The buffer composite above relies on the identical arrangement.
        /// <para>
        /// The two are the same rectangle because a widget clip is whole pixels by the time it gets here
        /// (<c>GuiWidget.DrawChild</c> floors and ceils all four edges first), <b>not</b> because the two roundings
        /// agree in general: the scissor takes <c>floor(left)</c> and <c>ceil(width)</c>, where a mask clip
        /// takes <c>floor(left)</c> and <c>ceil(right)</c>, and those part company on a fractional left edge -
        /// <c>floor(0.5) + ceil(1.0)</c> reaches x = 1, <c>ceil(1.5)</c> reaches x = 2. A caller that sets a
        /// fractional clip would get the scissor's answer, which is the same answer every other GL draw on
        /// this destination already gives it.
        /// </para>
        /// </param>
        protected override void CompositeLcdMask(LcdMask mask, Color color, int originX, int originY, RectangleDouble? clip = null)
        {
            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            if (!this.CanCompositeLcd)
            {
                // Reports the capability disagreeing with itself, as the base class does. Nothing here can
                // paint without a context, and the caller only got here by asking whether it could.
                base.CompositeLcdMask(mask, color, originX, originY, clip);
                return;
            }

            if (mask.Width <= 0 || mask.Height <= 0)
            {
                return;
            }

            ImageBuffer[] channelImages = GetLcdMaskChannelImages(mask);

            PushOrthoProjection();
            gl.Disable(EnableCap.Lighting);
            gl.Enable(EnableCap.Texture2D);
            gl.Disable(EnableCap.DepthTest);
            gl.Enable(EnableCap.Blend);

            // Premultiplied source over, because both the pass images and the draw color below are
            // premultiplied: dst_c = src_c + dst_c * (1 - src_alpha_c).
            gl.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

            gl.Translate(originX, originY, 0);

            gl.Color4(Premultiply(color));

            try
            {
                for (int channel = 0; channel < LcdBufferChannelImages.ChannelCount; channel++)
                {
                    gl.ColorMask(channel == 0, channel == 1, channel == 2, false);
                    ImageTexturePlugin.GetImageTexturePlugin(gl, channelImages[channel], false).DrawToGL();
                }
            }
            finally
            {
                // Restored on the way out of a throw as well - see CompositeLcdBuffer for what a mask left
                // stuck on one channel does to every frame after it.
                gl.ColorMask(true, true, true, true);
            }

            PopOrthoProjection();
        }

        /// <summary>
        /// This mask's three per-channel pass images: channel <c>c</c>'s coverage as white premultiplied by
        /// itself, built once and then shared by every draw of that mask.
        /// </summary>
        /// <remarks>
        /// The pack runs outside the lock, so two threads that both miss can both build - the loser's images
        /// are simply dropped, and only the published set is ever drawn with. That is the same trade the buffer
        /// table above makes: holding a process wide lock across an O(width * height) pass would park every
        /// other context behind a glyph run's repack.
        /// </remarks>
        private static ImageBuffer[] GetLcdMaskChannelImages(LcdMask mask)
        {
            lock (lcdMaskChannelImagesLock)
            {
                if (lcdMaskChannelImages.TryGetValue(mask, out ImageBuffer[] cached))
                {
                    return cached;
                }
            }

            ImageBuffer[] built = PackLcdMaskChannelImages(mask);

            lock (lcdMaskChannelImagesLock)
            {
                if (lcdMaskChannelImages.TryGetValue(mask, out ImageBuffer[] published))
                {
                    return published;
                }

                lcdMaskChannelImages.Add(mask, built);
                return built;
            }
        }

        /// <summary>
        /// Reduces <paramref name="mask"/> to one ordinary premultiplied BGRA image per pass, each holding
        /// channel <c>c</c>'s coverage in all four bytes.
        /// </summary>
        /// <remarks>
        /// White premultiplied by the coverage, rather than the coverage in alpha alone: see
        /// <see cref="CompositeLcdMask"/> for why the image has to be premultiplied to survive the texture
        /// uploader, and <see cref="LcdBufferChannelImages"/> for why a valid premultiplied image
        /// (<c>color &lt;= alpha</c>, trivially true here) makes that blit lossless. The two color channels the
        /// pass's write mask discards are white too, which costs nothing and keeps the image a plain
        /// interpretation of itself - a coverage image - rather than a channel-selecting one, because unlike
        /// the buffer form there is no per-channel color to select.
        /// <para>
        /// Row <c>y</c> in, row <c>y</c> out. Both the mask and the image are Y-up and agg-sharp's GL texture
        /// path is Y-up end to end, so there is no flip anywhere in this composite.
        /// </para>
        /// </remarks>
        private static ImageBuffer[] PackLcdMaskChannelImages(LcdMask mask)
        {
            var images = new ImageBuffer[LcdBufferChannelImages.ChannelCount];

            for (int channel = 0; channel < images.Length; channel++)
            {
                var image = new ImageBuffer(mask.Width, mask.Height, 32, new BlenderPreMultBGRA());
                byte[] pixels = image.GetBuffer();
                int bytesPerPixel = image.GetBytesBetweenPixelsInclusive();

                for (int y = 0; y < mask.Height; y++)
                {
                    int rowOffset = image.GetBufferOffsetXY(0, y);
                    int source = mask.PixelOffset(0, y) + channel;

                    for (int x = 0; x < mask.Width; x++, source += 3)
                    {
                        byte coverage = mask.Data[source];
                        int offset = rowOffset + (x * bytesPerPixel);
                        pixels[offset + ImageBuffer.OrderR] = coverage;
                        pixels[offset + ImageBuffer.OrderG] = coverage;
                        pixels[offset + ImageBuffer.OrderB] = coverage;
                        pixels[offset + ImageBuffer.OrderA] = coverage;
                    }
                }

                images[channel] = image;
            }

            return images;
        }

        /// <summary>
        /// The draw color with its color channels multiplied by its own alpha, rounding half up.
        /// </summary>
        /// <remarks>
        /// Half up rather than truncating because the whole point is to land the software composite's byte:
        /// truncation would darken every translucent fill by up to a full level instead of half of one.
        /// </remarks>
        private static Color Premultiply(Color color)
        {
            if (color.alpha == 255)
            {
                return color;
            }

            return new Color(
                (byte)(((color.red * color.alpha) + 127) / 255),
                (byte)(((color.green * color.alpha) + 127) / 255),
                (byte)(((color.blue * color.alpha) + 127) / 255),
                color.alpha);
        }

        /// <summary>
        /// This buffer's three per-channel pass images, repacked if the buffer has been painted since they
        /// were last built.
        /// </summary>
        /// <remarks>
        /// The lock covers only the table, not the repack: the repack writes into images owned by this
        /// buffer, and a buffer is painted and composited by the one thread that owns it, so two threads
        /// racing here would already be racing over the planes themselves. Holding a process wide lock across
        /// an O(width * height) pass over a full window backbuffer, on the other hand, would park every other
        /// context behind it.
        /// </remarks>
        private static LcdBufferChannelImages GetLcdChannelImages(LcdBuffer buffer)
        {
            LcdBufferChannelImages images;
            lock (lcdChannelImagesLock)
            {
                if (!lcdChannelImages.TryGetValue(buffer, out images)
                    || images.Width != buffer.Width
                    || images.Height != buffer.Height)
                {
                    images = new LcdBufferChannelImages(buffer.Width, buffer.Height);
                    lcdChannelImages.AddOrUpdate(buffer, images);
                }
            }

            images.UpdateFrom(buffer);
            return images;
        }
    }
}
