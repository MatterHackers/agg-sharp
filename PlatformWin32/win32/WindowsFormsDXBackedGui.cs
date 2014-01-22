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
//
// class platform_support
//
//----------------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

using AGG.Image;
using AGG.RasterizerScanline;

namespace AGG.UI
{
    public class WindowsFormsDXBackendGuiFactory : IGuiFactory
    {
        public GuiHalSurface CreateSurface(int Width, int Height, GuiHalSurface.CreateFlags flags, GuiHalSurface.PixelFormat pixelFormat)
        {
            GuiHalSurface newSurface;

            switch (pixelFormat)
            {
                case GuiHalSurface.PixelFormat.PixelFormatBgra32:
                    newSurface = new GuiHalSurfaceWindowsFormsDXBackend(GuiHalSurface.ImageFormats.pix_format_bgra32);
                    break;
                default:
                    throw new NotImplementedException();
            }

            newSurface.Init(Width, Height, flags, pixelFormat);
            return newSurface;
        }
    }

    public class WindowsFormBridgeDXBackend : AbstractWindowsFormBridge
    {
        Device DX_Device;

        public WindowsFormBridgeDXBackend(GuiHalSurface app, GuiHalSurface.ImageFormats format)
            : base(app, format)
        {
            SetUpFormsWindow(app, format);
        }

        public bool InitializeDirect3D()
        {
            try
            {
                PresentParameters pps = new PresentParameters();
                pps.Windowed = true;
                pps.SwapEffect = SwapEffect.Discard;
                pps.EnableAutoDepthStencil = false;
                pps.PresentFlag |= PresentFlag.LockableBackBuffer;
                pps.MultiSample = MultiSampleType.None;
                DX_Device = new Device(0, DeviceType.Hardware, this, CreateFlags.SoftwareVertexProcessing, pps);
                return true;
            }
            catch (DirectXException e)
            {
                MessageBox.Show(e.ToString(), "Error");
                return false;
            }
        }

        public override void display_pmap(Graphics displayGraphics)
        {
            GuiHalSurfaceWindowsFormsDXBackend app = ((GuiHalSurfaceWindowsFormsDXBackend)m_app);
            if (DX_Device == null)
            {
                return;
            }

            DX_Device.Clear(ClearFlags.Target, Color.White, 1.0f, 0);
            DX_Device.BeginScene();

            if (this.ClientRectangle.Width != app.backBuffer.Width
                || this.ClientRectangle.Height != app.backBuffer.Height)
            {
                throw new System.IO.InvalidDataException("the back buffer must be the same size as the client area");
            }

            using (Surface s = DX_Device.GetBackBuffer(0, 0, BackBufferType.Mono))
            {
                int pitch;
                using (GraphicsStream graphicsStream = s.LockRectangle(this.ClientRectangle, LockFlags.None, out pitch))
                {
                    unsafe
                    {
                        int sourceOffset;
                        Byte[] sourceBuffer = app.backBuffer.GetBuffer(out sourceOffset);

                        byte* pDestBuffer = (byte*)graphicsStream.InternalDataPointer;
                        int* pDestBufferInt = (int*)pDestBuffer;

                        int height = app.backBuffer.Height;
                        int scanWidth = app.backBuffer.Width * 4;
                        int numLongs = scanWidth / 4;
                        fixed (byte* pSourceBuffer = &sourceBuffer[sourceOffset])
                        {
                            int* pSourceBufferInt = (int*)pSourceBuffer;
                            for (int y = 0; y < height; y++)
                            {
                                int destOffsetInt = y * pitch / 4;
                                int sourceOffsetInt = app.backBuffer.GetBufferOffsetXY(0, height - y - 1) / 4;
                                for (int x = 0; x < numLongs; x++)
                                {
                                    pDestBufferInt[destOffsetInt++] = pSourceBufferInt[sourceOffsetInt++];
                                }
                            }
                        }
                    }
                }
                s.UnlockRectangle();
            }


            // Rendering is done here
            DX_Device.EndScene();
            DX_Device.Present();
        }
    };

    public class GuiHalSurfaceWindowsFormsDXBackend : GuiHalSurfaceWindowsFormsBase
    {
        internal ImageBuffer backBuffer;

        public GuiHalSurfaceWindowsFormsDXBackend(ImageFormats format)
            : base(format)
        {
            windowsFormsWindow = new WindowsFormBridgeDXBackend(this, format);

            if (((WindowsFormBridgeDXBackend)windowsFormsWindow).InitializeDirect3D() == false) // Check if D3D could be initialized
            {
                MessageBox.Show("Could not initialize Direct3D.", "Error");
                return;
            }
        }

        public override void OnResize(int sx, int sy)
        {
            Bounds = new rect_d(0, 0, sx, sy); ;
            int bitDepth = GetBitDepthForPixelFormat(m_format);

            backBuffer.Allocate((int)Width, (int)Height, (int)Width * bitDepth / 8, bitDepth);
            NewGraphics2D().Clear(new RGBA_Doubles(1, 1, 1, 1));
        }

        public override Graphics2D NewGraphics2D()
        {
            Graphics2D graphics2D = null;
            if (backBuffer != null)
            {
                graphics2D = backBuffer.NewGraphics2D();
            }
            else
            {
                throw new NotImplementedException("We don't support float in DX yet.");
                //graphics2D = m_BackBufferFloat.NewGraphics2D();
            }
            graphics2D.PushTransform();
            return graphics2D;
        }

        public override void Init(int width, int height, GuiHalSurface.CreateFlags flags, GuiHalSurface.PixelFormat pixelFormat)
        {
            //if (windowsFormsWindow.m_sys_format == PlatformSupportAbstract.ImageFormats.pix_format_undefined)
            {
                //  return false;
            }

            m_window_flags = flags;

            initialWidth = width;
            initialHeight = height;

            int bitDepth = GetBitDepthForPixelFormat(m_format);
            switch (bitDepth)
            {
                case 24:
                    backBuffer = new ImageBuffer(initialWidth, initialHeight, 24, new BlenderBGR());
                    break;

                case 32:
                    backBuffer = new ImageBuffer(initialWidth, initialHeight, 32, new BlenderBGRA());
                    break;

                case 128:
                    throw new NotImplementedException();
                    //backBuffer = null;
                    //m_BackBufferFloat = new ImageBufferFloat(initialWidth, initialHeight, 128, new BlenderBGRAFloat());
                    //break;

                default:
                    throw new NotImplementedException("Don't support this bit depth yet.");
            }

            System.Drawing.Size clientSize = new System.Drawing.Size();
            clientSize.Width = (int)width;
            clientSize.Height = (int)height;
            windowsFormsWindow.ClientSize = clientSize;

            if ((m_window_flags & CreateFlags.Resizable) == 0)
            {
                windowsFormsWindow.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                windowsFormsWindow.MaximizeBox = false;
            }

            clientSize.Width = width;
            clientSize.Height = height;
            windowsFormsWindow.ClientSize = clientSize;

            Bounds = new rect_d(0, 0, width, height);

            OnInitialize();
            windowsFormsWindow.m_WindowContentNeedsRedraw = true;
        }

        public override void OnInitialize()
        {
            NewGraphics2D().Clear(new RGBA_Doubles(1, 1, 1, 1));

            base.OnInitialize();
        }

        public override void Run()
        {
            Show();

            while (windowsFormsWindow.Created)
            {
                Application.DoEvents();
                OnIdle();
                if (MilliSecondsToSleepEachIdle > 0)
                {
                    System.Threading.Thread.Sleep(MilliSecondsToSleepEachIdle);
                }
            }
        }
    };
}
