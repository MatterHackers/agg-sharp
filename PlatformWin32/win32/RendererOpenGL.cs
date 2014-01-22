//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# Port port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software 
// is granted provided this copyright notice appears in all copies. 
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
#define AA_TIPS

using Tao.OpenGl;

using System;
using System.Collections.Generic;

using AGG.Image;
using AGG.VertexSource;
using AGG.Transform;

using Tesselate;

namespace AGG
{
    public abstract class VertexCachedTesselator : Tesselator
    {
        public abstract void AddVertex(double x, double y);
    }

    public class RenderToGLTesselator : VertexCachedTesselator
    {
        List<AddedVertex> m_Vertices = new List<AddedVertex>();

        internal class AddedVertex
        {
            internal AddedVertex(double x, double y)
            {
                m_X = x;
                m_Y = y;
            }
            internal double m_X;
            internal double m_Y;
        };

        public RenderToGLTesselator()
        {
            callBegin += new Tesselator.CallBeginDelegate(BeginCallBack);
            callEnd += new Tesselator.CallEndDelegate(EndCallBack);
            callVertex += new Tesselator.CallVertexDelegate(VertexCallBack);
            //callEdgeFlag += new Tesselator.CallEdgeFlagDelegate(EdgeFlagCallBack);
            callCombine += new Tesselator.CallCombineDelegate(CombineCallBack);
        }

        public override void BeginPolygon()
        {
            m_Vertices.Clear();

            base.BeginPolygon();
        }

        public void BeginCallBack(Tesselator.TriangleListType type)
        {
            switch (type)
            {
                case Tesselator.TriangleListType.Triangles:
                    Gl.glBegin(Gl.GL_TRIANGLES);
                    break;

                case Tesselator.TriangleListType.TriangleFan:
                    Gl.glBegin(Gl.GL_TRIANGLE_FAN);
                    break;

                case Tesselator.TriangleListType.TriangleStrip:
                    Gl.glBegin(Gl.GL_TRIANGLE_STRIP);
                    break;
            }
        }

        public void EndCallBack()
        {
            Gl.glEnd();
        }

        public void VertexCallBack(int index)
        {
            Gl.glVertex2d(m_Vertices[index].m_X, m_Vertices[index].m_Y);
        }

        public void EdgeFlagCallBack(int IsEdge)
        {
            Gl.glEdgeFlag(IsEdge);
        }

        public void CombineCallBack(double[] coords3, int[] data4,
            double[] weight4, out int outData)
        {
            outData = AddVertex(coords3[0], coords3[1], false);
        }

        public override void AddVertex(double x, double y)
        {
            AddVertex(x, y, true);
        }

        public int AddVertex(double x, double y, bool passOnToTesselator)
        {
            int index = m_Vertices.Count;
            m_Vertices.Add(new AddedVertex(x, y));
            double[] coords = new double[3];
            coords[0] = x;
            coords[1] = y;
            if (passOnToTesselator)
            {
                AddVertex(coords, index);
            }
            return index;
        }
    };

    public class CacheTriangleWithEdgeInfoTesselator : VertexCachedTesselator
    {
        internal class AddedVertex
        {
            internal AddedVertex(double x, double y)
            {
                m_Vertex.x = x;
                m_Vertex.y = y;
            }

            internal Vector2D m_Vertex;
        };

        internal class RenderIndices
        {
            internal RenderIndices(int index, bool isEdge)
            {
                m_Index = index;
                m_IsEdge = isEdge;
            }

            internal int m_Index;
            internal bool m_IsEdge;
        }

        bool m_IsEdge = false;
        List<AddedVertex> m_Vertices = new List<AddedVertex>();
        List<RenderIndices> m_Indices = new List<RenderIndices>();

        public CacheTriangleWithEdgeInfoTesselator()
        {
            callVertex += new Tesselator.CallVertexDelegate(VertexCallBack);
            callEdgeFlag += new Tesselator.CallEdgeFlagDelegate(EdgeFlagCallBack);
            callCombine += new Tesselator.CallCombineDelegate(CombineCallBack);
        }

        protected void DrawNonAATriangle(Vector2D p0, Vector2D p1, Vector2D p2)
        {
            Gl.glBegin(Gl.GL_TRIANGLES);
            {
                // P1
                Gl.glTexCoord2d(.9, 0);
                Gl.glVertex2d(p0.x, p0.y);

                // P2
                Gl.glTexCoord2d(.9, 1);
                Gl.glVertex2d(p1.x, p1.y);

                // P3
                Gl.glTexCoord2d(1, .5);
                Gl.glVertex2d(p2.x, p2.y);
            }
            Gl.glEnd();
        }

        protected void Draw1EdgeTriangle(Vector2D p0, Vector2D p1, Vector2D p2)
        {
            if(p0 == p1 || p1 == p2 || p2 == p0)
            {
                return;
            }
            Vector2D edegP0P1Vector = p1 - p0;
            Vector2D edegeP0P1Normal = edegP0P1Vector;
            edegeP0P1Normal.Normalize();

#if AA_TIPS
            Vector2D edegP2P1Vector = p1 - p2;
            Vector2D edegeP2P1Normal = edegP2P1Vector;
            edegeP2P1Normal.Normalize();

            Vector2D edegP2P0Vector = p0 - p2;
            Vector2D edegeP2P0Normal = edegP2P0Vector;
            edegeP2P0Normal.Normalize();
#endif

            Vector2D Normal = edegeP0P1Normal.GetPerpendicular();
            double edgeDotP3 = Normal.Dot(p2 - p0);
            if (edgeDotP3 < 0)
            {
                edgeDotP3 = -edgeDotP3;
            }
            else
            {
                Normal.Negate();
            }

            Vector2D edgeP0Offset = p0 + Normal;
            Vector2D edgeP1Offset = p1 + Normal;

            Gl.glBegin(Gl.GL_TRIANGLE_FAN);
            {
                Gl.glTexCoord2d(.5, 0);
                Gl.glVertex2d(p0.x, p0.y);

#if AA_TIPS
                // the new point
                Gl.glTexCoord2d(0, 1);
                Gl.glVertex2d(p0.x + edegeP2P0Normal.x, p0.y + edegeP2P0Normal.y);
#endif

                Gl.glTexCoord2d(0, 0);
                Gl.glVertex2d(edgeP0Offset.x, edgeP0Offset.y);

                Gl.glTexCoord2d(0, 1);
                Gl.glVertex2d(edgeP1Offset.x, edgeP1Offset.y);

                Gl.glTexCoord2d(.5, 1);
                Gl.glVertex2d(p1.x, p1.y);

                Gl.glTexCoord2d(.5 + (edgeDotP3 / 2), 0);
                Gl.glVertex2d(p2.x, p2.y);
            }
            Gl.glEnd();

#if AA_TIPS
            Gl.glBegin(Gl.GL_TRIANGLES);
            {
                Gl.glTexCoord2d(.5, 1);
                Gl.glVertex2d(p1.x, p1.y);

                Gl.glTexCoord2d(0, 1);
                Gl.glVertex2d(edgeP1Offset.x, edgeP1Offset.y);

                // the new point
                Gl.glTexCoord2d(0, 1);
                Gl.glVertex2d(p0.x + edegeP2P1Normal.x, p0.y + edegeP2P1Normal.y);
            }
            Gl.glEnd();
#endif
        }

        protected void Draw2EdgeTriangle(Vector2D p0, Vector2D p1, Vector2D p2)
        {
            //Draw3EdgeTriangle(p0, p1, p2);
            Vector2D centerPoint = p0 + p1 + p2;
            centerPoint /= 3;

            Draw1EdgeTriangle(p0, p1, centerPoint);
            Draw1EdgeTriangle(p1, p2, centerPoint);
            DrawNonAATriangle(p2, p0, centerPoint);
        }

        protected void Draw3EdgeTriangle(Vector2D p0, Vector2D p1, Vector2D p2)
        {
            Vector2D centerPoint = p0 + p1 + p2;
            centerPoint /= 3;

            Draw1EdgeTriangle(p0, p1, centerPoint);
            Draw1EdgeTriangle(p1, p2, centerPoint);
            Draw1EdgeTriangle(p2, p0, centerPoint);
        }

        public void RenderLastToGL()
        {
            for (int i = 0; i < m_Indices.Count; i += 3)
            {
                Vector2D v0 = m_Vertices[m_Indices[i + 0].m_Index].m_Vertex;
                Vector2D v1 = m_Vertices[m_Indices[i + 1].m_Index].m_Vertex;
                Vector2D v2 = m_Vertices[m_Indices[i + 2].m_Index].m_Vertex;
                if (v0 == v1 || v1 == v2 || v2 == v0)
                {
                    continue;
                }

                int e0 = m_Indices[i + 0].m_IsEdge ? 1 : 0;
                int e1 = m_Indices[i + 1].m_IsEdge ? 1 : 0;
                int e2 = m_Indices[i + 2].m_IsEdge ? 1 : 0;
                switch (e0 + e1 + e2)
                {
                    case 0:
                        DrawNonAATriangle(v0, v1, v2);
                        break;

                    case 1:
                        if (e0 == 1)
                        {
                            Draw1EdgeTriangle(v0, v1, v2);
                        }
                        else if (e1 == 1)
                        {
                            Draw1EdgeTriangle(v1, v2, v0);
                        }
                        else
                        {
                            Draw1EdgeTriangle(v2, v0, v1);
                        }
                        break;

                    case 2:
                        if (e0 == 1)
                        {
                            if (e1 == 1)
                            {
                                Draw2EdgeTriangle(v0, v1, v2);
                            }
                            else
                            {
                                Draw2EdgeTriangle(v2, v0, v1);
                            }
                        }
                        else
                        {
                            Draw2EdgeTriangle(v1, v2, v0);
                        }
                        break;

                    case 3:
                        Draw3EdgeTriangle(v0, v1, v2);
                        break;
                }
            }
        }

        public override void BeginPolygon()
        {
            m_Vertices.Clear();
            m_Indices.Clear();

            base.BeginPolygon();
        }


        public void VertexCallBack(int index)
        {
            m_Indices.Add(new RenderIndices(index, m_IsEdge));
        }

        public void EdgeFlagCallBack(bool isEdge)
        {
            m_IsEdge = isEdge;
        }

        public void CombineCallBack(double[] coords3, int[] data4,
            double[] weight4, out int outData)
        {
            outData = AddVertex(coords3[0], coords3[1], false);
        }

        public override void AddVertex(double x, double y)
        {
            AddVertex(x, y, true);
        }

        public int AddVertex(double x, double y, bool passOnToTesselator)
        {
            int index = m_Vertices.Count;
            m_Vertices.Add(new AddedVertex(x, y));
            double[] coords = new double[3];
            coords[0] = x;
            coords[1] = y;
            if (passOnToTesselator)
            {
                AddVertex(coords, index);
            }
            return index;
        }
    };

    public class RendererOpenGL : RendererBase
    {
        public bool m_ForceTexturedEdgeAntiAliasing = true;
        RenderToGLTesselator m_RenderNowTesselator = new RenderToGLTesselator();

        public RendererOpenGL()
        {
            TextPath = new gsv_text();
            StrockedText = new conv_stroke(TextPath);
            int[] bounds = new int[4];
            Gl.glGetIntegerv(Gl.GL_VIEWPORT, bounds);
        }

        public override void SetClippingRect(rect_d clippingRect)
        {
            Gl.glScissor((int)Math.Floor(clippingRect.x1), (int)Math.Floor(clippingRect.y1),
                (int)Math.Ceiling(clippingRect.Width), (int)Math.Ceiling(clippingRect.Height));
        }

        public override IScanlineCache ScanlineCache
        {
            get { return null; }
            set { throw new Exception("There is no scanline cache on a GL surface."); }
        }

        public static void PushOrthoProjection()
        {
            Gl.glPushAttrib(Gl.GL_TRANSFORM_BIT | Gl.GL_ENABLE_BIT);

            Gl.glEnable(Gl.GL_BLEND);
            Gl.glEnable(Gl.GL_SCISSOR_TEST);

            Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);

            int[] viewport = new int[4];
            Gl.glGetIntegerv(Gl.GL_VIEWPORT, viewport);
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            Gl.glOrtho(viewport[0], viewport[2], viewport[1], viewport[3], 0, 1);

            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glPushMatrix();
            Gl.glLoadIdentity();

            viewport = null;
        }

        public static void PopOrthoProjection()
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Gl.glPopMatrix();
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glPopMatrix();
            Gl.glPopAttrib();
        }

#if use_timers
        static CNamedTimer OpenGLRenderTimer = new CNamedTimer("OpenGLRenderTimer");
        static CNamedTimer OpenGLEndPolygonTimer = new CNamedTimer("OpenGLEndPolygonTimer");        
#endif
        void SendShapeToTeselator(VertexCachedTesselator teselator, IVertexSource vertexSource)
        {
            teselator.BeginPolygon();

            Path.FlagsAndCommand PathAndFlags = 0;
            double x, y;
            bool haveBegunContour = false;
            while (!Path.is_stop(PathAndFlags = vertexSource.vertex(out x, out y)))
            {
                if (Path.is_close(PathAndFlags)
                    || (haveBegunContour && Path.is_move_to(PathAndFlags)))
                {
                    teselator.EndContour();
                    haveBegunContour = false;
                }

                if (!Path.is_close(PathAndFlags))
                {
                    if (!haveBegunContour)
                    {
                        teselator.BeginContour();
                        haveBegunContour = true;
                    }

                    teselator.AddVertex(x, y);
                }
            }

            if (haveBegunContour)
            {
                teselator.EndContour();
            }

#if use_timers
            OpenGLEndPolygonTimer.Start();
#endif
            teselator.EndPolygon();
#if use_timers
            OpenGLEndPolygonTimer.Stop();
#endif
        }

        int m_AATextureHandle = -1;
        void CheckLineImageCache()
        {
            if (m_AATextureHandle == -1)
            {
                // Create the texture handle and display list handle
                int[] textureHandle = new int[1];
                Gl.glGenTextures(1, textureHandle);
                m_AATextureHandle = textureHandle[0];

                // Set up some texture parameters for openGL
                Gl.glBindTexture(Gl.GL_TEXTURE_2D, m_AATextureHandle);
                Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MAG_FILTER, Gl.GL_LINEAR);
                Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MIN_FILTER, Gl.GL_LINEAR);

                Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_S, Gl.GL_CLAMP_TO_EDGE);
                Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_T, Gl.GL_CLAMP_TO_EDGE);

                byte[] hardwarePixelBuffer = new byte[8];
                hardwarePixelBuffer[0] = hardwarePixelBuffer[1] = hardwarePixelBuffer[2] = 255; hardwarePixelBuffer[3] = 0;
                hardwarePixelBuffer[4] = hardwarePixelBuffer[5] = hardwarePixelBuffer[6] = 255; hardwarePixelBuffer[7] = 255;

                // Create the texture
                Gl.glTexImage2D(Gl.GL_TEXTURE_2D, 0, Gl.GL_RGBA, 2, 1,
                    0, Gl.GL_RGBA, Gl.GL_UNSIGNED_BYTE, hardwarePixelBuffer);
            }
        }

        void DrawAAShape(IVertexSource vertexSource)
        {
            CheckLineImageCache();
            Gl.glEnable(Gl.GL_TEXTURE_2D);
            Gl.glBindTexture(Gl.GL_TEXTURE_2D, m_AATextureHandle);

            CacheTriangleWithEdgeInfoTesselator triangleEddgeInfo = new CacheTriangleWithEdgeInfoTesselator();
            SendShapeToTeselator(triangleEddgeInfo, vertexSource);

            // now render it
            triangleEddgeInfo.RenderLastToGL();
        }

        public override void Render(IVertexSource vertexSource, int pathIndexToRender, RGBA_Bytes colorBytes)
        {
#if use_timers
            OpenGLRenderTimer.Start();
#endif
            PushOrthoProjection();

            vertexSource.rewind(pathIndexToRender);

            RGBA_Doubles color = colorBytes.GetAsRGBA_Doubles();

            Gl.glColor4d(color.m_r, color.m_g, color.m_b, color.m_a);

            Affine transform = GetTransform();
            if (!transform.is_identity())
            {
                vertexSource = new conv_transform(vertexSource, transform);
            }

            if (m_ForceTexturedEdgeAntiAliasing)
            {
                DrawAAShape(vertexSource);
            }
            else
            {
                SendShapeToTeselator(m_RenderNowTesselator, vertexSource);
            }

            PopOrthoProjection();
#if use_timers
            OpenGLRenderTimer.Stop();
#endif
        }

        public override void Render(IImage source,
            double x, double y,
            double angleDegrees,
            double scaleX, double ScaleY,
            RGBA_Bytes color,
            BlendMode renderingMode)
        {
            ImageGL
            x = 0;
        }

        public override void Clear(IColorType color)
        {
            RGBA_Doubles colorDoubles = color.GetAsRGBA_Doubles();
            Gl.glClearColor((float)colorDoubles.m_r, (float)colorDoubles.m_g, (float)colorDoubles.m_b, (float)colorDoubles.m_a);
            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);
        }
    }
}
