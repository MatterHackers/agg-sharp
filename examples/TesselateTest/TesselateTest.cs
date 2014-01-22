// C# Port port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007

using System;
using System.Collections.Generic;

using AGG.UI;
using AGG.PixelFormat;
using AGG.VertexSource;
using AGG.RasterizerScanline;
using AGG.Transform;

using Tesselate;
using NUnitTesselate;

namespace AGG
{
    public class TeselateTestApplication : AGG.UI.win32.PlatformSupport
    {
        rbox_ctrl m_WhichShape;
        rbox_ctrl m_WindingRule;
        cbox_ctrl m_BoundryOnly;
        cbox_ctrl m_EdgeFlag;
        Tesselate_Tests m_TesselateTest = new Tesselate_Tests();
        Tesselator.TriangleListType m_TriangleListType;
        List<int> m_VertexIndexList = new List<int>();
        double m_LineWidth = .5;
        List<double> m_LineWidthList = new List<double>();
        RGBA_Doubles m_TriangleColor = new RGBA_Doubles(0, 0, 0, .2);
        RGBA_Doubles m_LineColor = new RGBA_Doubles(0, 0, 0);
        Random m_ColorRand = new Random();
        Tesselate_Tests.Vertex m_LastTriangleCenter = new Tesselate_Tests.Vertex(0,0);
        Tesselate_Tests.Vertex m_LastEdgeCenter = new Tesselate_Tests.Vertex(0,0);

        public TeselateTestApplication(PixelFormats format, PlatformSupportAbstract.ERenderOrigin RenderOrigin)
            : base(format, RenderOrigin)
        {
        }

        public override void OnInitialize()
        {
            base.OnInitialize();

            int whichHeight = 112;
            int whichWidth = 300;
            int windingHeight = 140;
            int windingWidth = 300;
            m_WhichShape = new rbox_ctrl(0.0, Height - whichHeight, whichWidth, Height);
            AddChild(m_WhichShape);
            m_WhichShape.add_item("Nested Rects All CCW");
            m_WhichShape.add_item("Nested Rects CCW, CW, CW");
            m_WhichShape.add_item("Rects And Triangle");
            m_WhichShape.add_item("Complex Path");
            m_WhichShape.cur_item(0);

            m_WhichShape.background_color(new RGBA_Doubles(0.0, 0.0, 0.0, 0.1));
            m_WhichShape.text_size(12.0);
            m_WhichShape.text_thickness(2);

            m_WindingRule = new rbox_ctrl(whichWidth + 100, Height - windingHeight, whichWidth + 100 + windingWidth, Height);
            AddChild(m_WindingRule);
            m_WindingRule.add_item("Winding ODD");
            m_WindingRule.add_item("Winding NON-ZERO");
            m_WindingRule.add_item("Winding POSITIVE");
            m_WindingRule.add_item("Winding NEGATIVE");
            m_WindingRule.add_item("Winding ABS >= 2");
            m_WindingRule.cur_item(0);

            m_WindingRule.background_color(new RGBA_Doubles(0.0, 0.0, 0.0, 0.1));
            m_WindingRule.text_size(12.0);
            m_WindingRule.text_thickness(2);

            m_EdgeFlag = new cbox_ctrl(10, Height - whichHeight - 40, "Turn on Edge Flag");
            m_EdgeFlag.status(true);
            AddChild(m_EdgeFlag);

            m_BoundryOnly = new cbox_ctrl(10, Height - whichHeight - 20, "Render Only Boundary");
            AddChild(m_BoundryOnly);

            Tesselate_Tests test = new Tesselate_Tests();
            test.MatchesGLUTesselator();
        }

        double m_Scale = 4;
        double m_XOffset = 250;
        double m_YOffset = 100;

        void Line(Tesselate_Tests.Vertex Vertex1, Tesselate_Tests.Vertex Vertex2, double lineWidth, RendererBase renderer, bool ArrowTip)
        {
            PathStorage line = new PathStorage();
            line.move_to(Vertex1.m_X * m_Scale + m_XOffset, Vertex1.m_Y * m_Scale + m_YOffset);
            line.line_to(Vertex2.m_X * m_Scale + m_XOffset, Vertex2.m_Y * m_Scale + m_YOffset);

            // Drawing as an outline
            conv_stroke wideLine = new conv_stroke(line);
            wideLine.width(lineWidth);

            renderer.Render(wideLine, m_LineColor.GetAsRGBA_Bytes());

            if(ArrowTip)
            {
                Ellipse Dot = new Ellipse(
                    (Vertex2.m_X * m_Scale * 9 + Vertex1.m_X * m_Scale) / 10 + m_XOffset,
                    (Vertex2.m_Y * m_Scale * 9 + Vertex1.m_Y * m_Scale) / 10 + m_YOffset, 3, 3);
                GetRenderer().Render(Dot, m_LineColor.GetAsRGBA_Bytes());
            }
        }

        void Triangle(Tesselate_Tests.Vertex Vertex1, Tesselate_Tests.Vertex Vertex2, Tesselate_Tests.Vertex Vertex3,
            RendererBase renderer)
        {
            PathStorage triangle = new PathStorage();
            triangle.move_to(Vertex1.m_X * m_Scale + m_XOffset, Vertex1.m_Y * m_Scale + m_YOffset);
            triangle.line_to(Vertex2.m_X * m_Scale + m_XOffset, Vertex2.m_Y * m_Scale + m_YOffset);
            triangle.line_to(Vertex3.m_X * m_Scale + m_XOffset, Vertex3.m_Y * m_Scale + m_YOffset);

            renderer.Render(triangle, m_TriangleColor.GetAsRGBA_Bytes());
        }

        public void BeginCallBack(Tesselator.TriangleListType type)
        {
            m_VertexIndexList.Clear();
            m_LineWidthList.Clear();
            m_TriangleListType = type;
            m_LineWidth = .5;

            switch (m_TriangleListType)
            {
                case Tesselator.TriangleListType.Triangles:
                    m_TriangleColor = new RGBA_Doubles(.1, 0, 0, .2);
                    m_LineColor = new RGBA_Doubles(0, 0, 0);
                    break;

                case Tesselator.TriangleListType.TriangleFan:
                    m_TriangleColor = new RGBA_Doubles(0, 1, 0, .2);
                    m_LineColor = new RGBA_Doubles(0, 0, 0);
                    break;

                case Tesselator.TriangleListType.TriangleStrip:
                    m_TriangleColor = new RGBA_Doubles(0, 0, 1, .2);
                    m_LineColor = new RGBA_Doubles(0, 0, 0);
                    break;

                case Tesselator.TriangleListType.LineLoop:
                    m_LineColor = new RGBA_Doubles(m_ColorRand.NextDouble(), m_ColorRand.NextDouble(), m_ColorRand.NextDouble());
                    break;

            }
        }

        public void EndCallBack()
        {
            if (m_TriangleListType == Tesselator.TriangleListType.LineLoop)
            {
                Line(m_TesselateTest.m_VertexList[m_VertexIndexList[1]],
                m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                .5,
                GetRenderer(), true);
            }
        }

        public void VertexCallBack(int index)
        {
            switch(m_TriangleListType)
            {
                case Tesselator.TriangleListType.Triangles:
                    m_VertexIndexList.Add(index);
                    m_LineWidthList.Add(m_LineWidth);
                    if (m_VertexIndexList.Count == 3)
                    {
                        Triangle(m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                            m_TesselateTest.m_VertexList[m_VertexIndexList[1]],
                            m_TesselateTest.m_VertexList[m_VertexIndexList[2]],
                            GetRenderer());

                        Line(m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                            m_TesselateTest.m_VertexList[m_VertexIndexList[1]],
                            m_LineWidthList[0], GetRenderer(), false);

                        Line(m_TesselateTest.m_VertexList[m_VertexIndexList[1]],
                            m_TesselateTest.m_VertexList[m_VertexIndexList[2]],
                            m_LineWidthList[1], GetRenderer(), false);

                        Line(m_TesselateTest.m_VertexList[m_VertexIndexList[2]],
                            m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                            m_LineWidthList[2], GetRenderer(), false);

                        m_VertexIndexList.Clear();
                        m_LineWidthList.Clear();
                    }
                    break;

                case Tesselator.TriangleListType.TriangleFan:
                    if (m_VertexIndexList.Count > 1)
                    {
                        Triangle(m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                            m_TesselateTest.m_VertexList[m_VertexIndexList[1]],
                            m_TesselateTest.m_VertexList[index],
                            GetRenderer());

                        Line(m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                            m_TesselateTest.m_VertexList[index],
                            .5, GetRenderer(), false);

                        Line(m_TesselateTest.m_VertexList[m_VertexIndexList[1]],
                            m_TesselateTest.m_VertexList[index],
                            .5, GetRenderer(), false);

                        AGG.VertexSource.Ellipse Dot = new Ellipse(
                            m_TesselateTest.m_VertexList[m_VertexIndexList[0]].m_X * m_Scale + m_XOffset,
                            m_TesselateTest.m_VertexList[m_VertexIndexList[0]].m_Y * m_Scale + m_YOffset, 5, 5);
                        GetRenderer().Render(Dot, m_TriangleColor.GetAsRGBA_Bytes());

                        Tesselate_Tests.Vertex polyCenter = new Tesselate_Tests.Vertex(
                            (m_TesselateTest.m_VertexList[m_VertexIndexList[0]].m_X
                            + m_TesselateTest.m_VertexList[m_VertexIndexList[1]].m_X
                            + m_TesselateTest.m_VertexList[index].m_X) / 3,
                            (m_TesselateTest.m_VertexList[m_VertexIndexList[0]].m_Y
                            + m_TesselateTest.m_VertexList[m_VertexIndexList[1]].m_Y
                            + m_TesselateTest.m_VertexList[index].m_Y) / 3);

                        Tesselate_Tests.Vertex newEdgeCenter = new Tesselate_Tests.Vertex(
                            (m_TesselateTest.m_VertexList[index].m_X
                            + m_TesselateTest.m_VertexList[m_VertexIndexList[0]].m_X) / 2,
                            (m_TesselateTest.m_VertexList[index].m_Y
                            + m_TesselateTest.m_VertexList[m_VertexIndexList[0]].m_Y) / 2);

                        if (m_LastTriangleCenter.m_X == -.9823471232)
                        {
                            Line(polyCenter, newEdgeCenter, .5, GetRenderer(), true);
                        }
                        else
                        {
                            Line(m_LastEdgeCenter, newEdgeCenter, .5, GetRenderer(), true);
                        }

                        m_LastEdgeCenter = newEdgeCenter;
                        m_LastTriangleCenter = polyCenter;

                        m_VertexIndexList[1] = index;
                    }
                    else
                    {
                        if (m_VertexIndexList.Count == 1)
                        {
                            Line(m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                                m_TesselateTest.m_VertexList[index],
                                .5, GetRenderer(), false);

                            m_LastTriangleCenter.m_X = -.9823471232;
                        }
                        m_VertexIndexList.Add(index);
                    }
                    break;

                case Tesselator.TriangleListType.TriangleStrip:
                    if (m_VertexIndexList.Count > 1)
                    {
                        Triangle(m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                            m_TesselateTest.m_VertexList[m_VertexIndexList[1]],
                            m_TesselateTest.m_VertexList[index],
                            GetRenderer());

                        Line(m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                            m_TesselateTest.m_VertexList[index],
                            .5, GetRenderer(), false);

                        Line(m_TesselateTest.m_VertexList[m_VertexIndexList[1]],
                            m_TesselateTest.m_VertexList[index],
                            .5, GetRenderer(), false);

                        Tesselate_Tests.Vertex polyCenter = new Tesselate_Tests.Vertex(
                            (m_TesselateTest.m_VertexList[m_VertexIndexList[0]].m_X 
                            + m_TesselateTest.m_VertexList[m_VertexIndexList[1]].m_X
                            + m_TesselateTest.m_VertexList[index].m_X) / 3,
                            (m_TesselateTest.m_VertexList[m_VertexIndexList[0]].m_Y
                            + m_TesselateTest.m_VertexList[m_VertexIndexList[1]].m_Y
                            + m_TesselateTest.m_VertexList[index].m_Y) / 3);

                        Tesselate_Tests.Vertex newEdgeCenter = new Tesselate_Tests.Vertex(
                            (m_TesselateTest.m_VertexList[index].m_X
                            + m_TesselateTest.m_VertexList[m_VertexIndexList[1]].m_X) / 2,
                            (m_TesselateTest.m_VertexList[index].m_Y
                            + m_TesselateTest.m_VertexList[m_VertexIndexList[1]].m_Y) / 2);

                        if(m_LastTriangleCenter.m_X == -.9823471232)
                        {
                            Line(polyCenter, newEdgeCenter, .5, GetRenderer(), true);
                        }
                        else
                        {
                            Line(m_LastEdgeCenter, newEdgeCenter, .5, GetRenderer(), true);
                        }

                        m_LastEdgeCenter = newEdgeCenter;
                        m_LastTriangleCenter = polyCenter;

                        m_VertexIndexList[0] = m_VertexIndexList[1];
                        m_VertexIndexList[1] = index;
                    }
                    else
                    {
                        if (m_VertexIndexList.Count == 1)
                        {
                            Line(m_TesselateTest.m_VertexList[m_VertexIndexList[0]],
                                m_TesselateTest.m_VertexList[index],
                                .5, GetRenderer(), false);
                            m_LastTriangleCenter.m_X = -.9823471232;
                        }
                        m_VertexIndexList.Add(index);
                    }
                    break;

                case Tesselator.TriangleListType.LineLoop:
                    if (m_VertexIndexList.Count > 0)
                    {
                        Line(m_TesselateTest.m_VertexList[m_VertexIndexList[1]],
                            m_TesselateTest.m_VertexList[index],
                            .5, GetRenderer(), true);

                        m_VertexIndexList[1] = index;
                    }
                    else
                    {
                        m_VertexIndexList.Add(index);
                        m_VertexIndexList.Add(index);

                        AGG.VertexSource.Ellipse Dot = new Ellipse(
                            m_TesselateTest.m_VertexList[index].m_X * m_Scale + m_XOffset,
                            m_TesselateTest.m_VertexList[index].m_Y * m_Scale + m_YOffset, 5, 5);
                        GetRenderer().Render(Dot, m_LineColor.GetAsRGBA_Bytes());
                    }
                    break;

            }
        }

        public void EdgeFlagCallBack(bool IsEdge)
        {
            m_LineWidth = .5;

            if (IsEdge)
            {
                m_LineWidth = 2;
            }
        }

        public void CombineCallBack(double[] coords3, int[] data4,
            double[] weight4, out int outData)
        {
            outData = m_TesselateTest.m_VertexList.Count;
            m_TesselateTest.m_VertexList.Add(new Tesselate_Tests.Vertex(coords3[0], coords3[1]));
        }

        public override void OnDraw()
        {
            GetRenderer().Clear(new RGBA_Doubles(1, 1, 1));

            Tesselator tesselator = new Tesselator();
            tesselator.callBegin += new Tesselator.CallBeginDelegate(BeginCallBack);
            tesselator.callEnd += new Tesselator.CallEndDelegate(EndCallBack);
            tesselator.callVertex += new Tesselator.CallVertexDelegate(VertexCallBack);
            tesselator.callCombine += new Tesselator.CallCombineDelegate(CombineCallBack);

            switch (m_WindingRule.cur_item())
            {
                case 0:
                    tesselator.windingRule = Tesselator.WindingRuleType.Odd;
                    break;

                case 1:
                    tesselator.windingRule = Tesselator.WindingRuleType.NonZero;
                    break;

                case 2:
                    tesselator.windingRule = Tesselator.WindingRuleType.Positive;
                    break;

                case 3:
                    tesselator.windingRule = Tesselator.WindingRuleType.Negative;
                    break;

                case 4:
                    tesselator.windingRule = Tesselator.WindingRuleType.ABS_GEQ_Two;
                    break;

            }

            if (m_EdgeFlag.status())
            {
                tesselator.callEdgeFlag += new Tesselator.CallEdgeFlagDelegate(EdgeFlagCallBack);
            }

            if (m_BoundryOnly.status()) // edgesOnly
            {
                tesselator.BoundaryOnly = true;
            }

            m_TesselateTest.ParseStreamForTesselator(tesselator, m_WhichShape.cur_item());

            // now render the outline
            {
                string[] instructionStream = Tesselate_Tests.m_InsructionStream[m_WhichShape.cur_item()];
                bool gotFirst = false;
                Tesselate_Tests.Vertex firstInContour = new Tesselate_Tests.Vertex(0, 0);
                bool havePrev = false;
                Tesselate_Tests.Vertex prevVertex = new Tesselate_Tests.Vertex(0,0);
                PathStorage line = new PathStorage();
                conv_stroke wideLine;
                AGG.VertexSource.Ellipse Dot;

                for (int curInstruction = 0; curInstruction < instructionStream.Length; curInstruction++)
                {
                    switch (instructionStream[curInstruction])
                    {
                        case "BC":
                            break;
                        
                        case "EC":
                            gotFirst = false;
                            havePrev = false;
                            line.remove_all();
                            line.move_to(prevVertex.m_X + 30, prevVertex.m_Y + 100);
                            line.line_to(firstInContour.m_X + 30, firstInContour.m_Y + 100);

                            // Drawing as an outline
                            wideLine = new conv_stroke(line);
                            wideLine.width(1);

                            GetRenderer().Render(wideLine, new RGBA_Bytes(0, 0, 0));

                            Dot = new Ellipse(
                                (firstInContour.m_X * 9 + prevVertex.m_X)/10 + 30,
                                (firstInContour.m_Y * 9 + prevVertex.m_Y)/10 +100, 3, 3);
                            GetRenderer().Render(Dot, new RGBA_Bytes(0, 0, 0));
                            break;
                        
                        case "V":
                            double x = Convert.ToDouble(instructionStream[curInstruction + 1]);
                            double y = Convert.ToDouble(instructionStream[curInstruction + 2]);
                            curInstruction += 2;
                            if (!gotFirst)
                            {
                                gotFirst = true;
                                firstInContour = new Tesselate_Tests.Vertex(x, y);
                            }
                            if (!havePrev)
                            {
                                prevVertex = new Tesselate_Tests.Vertex(x, y);
                                havePrev = true;
                            }
                            else
                            {
                                line.remove_all();
                                line.move_to(prevVertex.m_X + 30, prevVertex.m_Y + 100);
                                line.line_to(x + 30, y + 100);

                                // Drawing as an outline
                                wideLine = new conv_stroke(line);
                                wideLine.width(1);

                                GetRenderer().Render(wideLine, new RGBA_Bytes(0, 0, 0));

                                line.remove_all();
                                Dot = new Ellipse(
                                    (x * 9 + prevVertex.m_X) / 10 + 30,
                                    (y * 9 + prevVertex.m_Y) / 10 + 100, 3, 3);
                                GetRenderer().Render(Dot, new RGBA_Bytes(0, 0, 0));

                                prevVertex = new Tesselate_Tests.Vertex(x, y);
                            }
                            break;
                    }
                }
            }

            base.OnDraw();
        }


        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            if(mouseEvent.Button == MouseButtons.Left)
            {
            }

            base.OnMouseDown(mouseEvent);
        }


        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            if (mouseEvent.Button == MouseButtons.Left)
            {
            }

            base.OnMouseMove(mouseEvent);
        }

        override public void OnMouseUp(MouseEventArgs mouseEvent)
        {
            base.OnMouseUp(mouseEvent);
        }
        
        public static void StartDemo()
        {
            TeselateTestApplication app = new TeselateTestApplication(PixelFormats.pix_format_rgba32, PlatformSupportAbstract.ERenderOrigin.OriginBottomLeft);
            app.Caption = "AGG Example. A simple example to show of the polygon tesselator.";

            //if (app.init(800, 600, (uint)(AGG.UI.PlatformSupportAbstract.WindowFlags.Risizeable | PlatformSupportAbstract.WindowFlags.UseOpenGL)))
            if (app.init(800, 600, (uint)AGG.UI.PlatformSupportAbstract.WindowFlags.Risizeable))
            {
                app.run();
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
        	StartDemo();
        }
    };
}