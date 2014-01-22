/*
Copyright (c) 2012, Lars Brubaker
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
using System.IO;
using System.Reflection;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.CameraCalibration
{
    public class MatterControlWidget : RectangleWidget
    {
        static PrinterComunication printerComunicationChannel = new PrinterComunication();
        Button connectButton;

        Button disableMotors;

        XYJogControls xyJogControls;

        public static PrinterComunication GetPrinter()
        {
            return printerComunicationChannel;
        }

        public CameraCalibrationWidget()
        {
            SuspendLayout();

            FlowLayoutWidget TopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {
                FlowLayoutWidget TopButtons = new FlowLayoutWidget(FlowDirection.LeftToRight);
                {
                    connectButton = new Button("Connect");
                    connectButton.Click += new ButtonBase.ButtonEventHandler(ConnectButton_Click);

                    TopButtons.AddChild(connectButton);
                }
                TopToBottomLayout.AddChild(TopButtons);

                xyJogControls = new XYJogControls();
                TopToBottomLayout.AddChild(xyJogControls);

                disableMotors = new Button("Disable Motors");
                disableMotors.Click += new ButtonBase.ButtonEventHandler(disableMotors_Click);
                TopToBottomLayout.AddChild(disableMotors);
            }

            AddChild(TopToBottomLayout);

            ResumeLayout();
        }

        void disableMotors_Click(object sender, MouseEventArgs mouseEvent)
        {
            DisableMotors();
        }

        void DisableMotors()
        {
            CameraCalibrationWidget.GetPrinter().WriteLineToPrinter("M84");
        }

        public override void OnClosing(out bool CancelClose)
        {
            DisableMotors();
            CameraCalibrationWidget.GetPrinter().Disable();

            base.OnClosing(out CancelClose);
        }

        void ConnectButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            if (connectButton.Text == "Disconnect")
            {
                CameraCalibrationWidget.GetPrinter().Disable();
                connectButton.Text = "Connect";
            }
            else
            {
                CameraCalibrationWidget.GetPrinter().Enable();
                connectButton.Text = "Disconnect";
            }
        }

        public override void OnLayout()
        {
            SetAnchor(AnchorFlags.All);

            base.OnLayout();
        }

        static NamedExecutionTimer CameraCalibrationWidget_OnDraw = new NamedExecutionTimer("CameraCalibrationWidget_OnDraw");
        public override void OnDraw(Graphics2D graphics2D)
        {
            CameraCalibrationWidget_OnDraw.Start();
            graphics2D.Clear(RGBA_Bytes.White);
            rect_d rect = new rect_d(Width - 40, 10, Width - 10, 40);
            graphics2D.Rectangle(rect, RGBA_Bytes.Black);
            Invalidate(rect);

            base.OnDraw(graphics2D);
            CameraCalibrationWidget_OnDraw.Stop();
        }
    }
    
    public class CameraCalibrationWidgetFactory : IAppWidgetFactory
    {
        public GUIWidget NewWidget()
        {
            return new CameraCalibrationWidget();
        }

        public AppWidgetInfo GetAppParameters()
        {
            AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
            "Other",
            "Matter Control",
            "A RepRap printer controler from MatterHackers",
            800,
            600);

            return appWidgetInfo;
        }
    }
}
