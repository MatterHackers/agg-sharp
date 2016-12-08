using System;
using System.Threading;
using System.Runtime.InteropServices;
using MatterHackers.Agg.Image;

namespace AForge
{
	public class CLEyeCamera : ICamera
    {
        #region [ Camera Parameters ]
        // camera color mode
        public enum CLEyeCameraColorMode
        {
            CLEYE_MONO_PROCESSED,
            CLEYE_COLOR_PROCESSED,
            CLEYE_MONO_RAW,
            CLEYE_COLOR_RAW,
            CLEYE_BAYER_RAW
        };

        // camera resolution
        public enum CLEyeCameraResolution
        {
            CLEYE_QVGA, // 320 x 240
            CLEYE_VGA // 640 x 480
        };
        // camera parameters
        public enum CLEyeCameraParameter
        {
            // camera sensor parameters
            CLEYE_AUTO_GAIN,			// [false, true]
            CLEYE_GAIN,					// [0, 79]
            CLEYE_AUTO_EXPOSURE,		// [false, true]
            CLEYE_EXPOSURE,				// [0, 511]
            CLEYE_AUTO_WHITEBALANCE,	// [false, true]
            CLEYE_WHITEBALANCE_RED,		// [0, 255]
            CLEYE_WHITEBALANCE_GREEN,	// [0, 255]
            CLEYE_WHITEBALANCE_BLUE,	// [0, 255]
            // camera linear transform parameters
            CLEYE_HFLIP,				// [false, true]
            CLEYE_VFLIP,				// [false, true]
            CLEYE_HKEYSTONE,			// [-500, 500]
            CLEYE_VKEYSTONE,			// [-500, 500]
            CLEYE_XOFFSET,				// [-500, 500]
            CLEYE_YOFFSET,				// [-500, 500]
            CLEYE_ROTATION,				// [-500, 500]
            CLEYE_ZOOM,					// [-500, 500]
            // camera non-linear transform parameters
            CLEYE_LENSCORRECTION1,		// [-500, 500]
            CLEYE_LENSCORRECTION2,		// [-500, 500]
            CLEYE_LENSCORRECTION3,		// [-500, 500]
            CLEYE_LENSBRIGHTNESS		// [-500, 500]
        };
        #endregion

        #region [ CLEyeMulticam Imports ]
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CLEyeGetCameraCount();
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Guid CLEyeGetCameraUUID(int camId);
        [DllImport("CLEyeMulticam.dll", CallingConvention=CallingConvention.Cdecl)]
        public static extern IntPtr CLEyeCreateCamera(Guid camUUID, CLEyeCameraColorMode mode, CLEyeCameraResolution res, float frameRate);
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CLEyeDestroyCamera(IntPtr camera);
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CLEyeCameraStart(IntPtr camera);
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CLEyeCameraStop(IntPtr camera);
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CLEyeCameraLED(IntPtr camera, bool on);
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CLEyeSetCameraParameter(IntPtr camera, CLEyeCameraParameter param, int value);
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CLEyeGetCameraParameter(IntPtr camera, CLEyeCameraParameter param);
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CLEyeCameraGetFrameDimensions(IntPtr camera, ref int width, ref int height);
        [DllImport("CLEyeMulticam.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool CLEyeCameraGetFrame(IntPtr camera, IntPtr pData, int waitTimeout);
        #endregion

        #region [ Variables ]
        ImageBuffer currentImage;
        IntPtr cameraCopyBufferIntPtr;
        bool newImageRead;
        bool _threadRunning;
        ManualResetEvent _exitEvent;
        IntPtr hwCameraIntPtr;
        #endregion

        public static int CameraCount { get { return CLEyeGetCameraCount(); } }
        public static Guid CameraUUID(int idx) { return CLEyeGetCameraUUID(idx); }

        public int Exposure0To511
        {
            get
            {
                if (hwCameraIntPtr != IntPtr.Zero)
                {
                    return CLEyeGetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_EXPOSURE);
                }

                return 0;
            }
            set
            {
                if (hwCameraIntPtr != IntPtr.Zero)
                {
                    CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_EXPOSURE, value);
                }
            }
        }

        public int RedBalance0To255
        {
            get
            {
                return CLEyeGetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_WHITEBALANCE_RED);
            }
            set
            {
                if (hwCameraIntPtr != IntPtr.Zero)
                {
                    CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_WHITEBALANCE_RED, value);
                }
            }
        }

        public int GreenBalance0To255
        {
            get
            {
                return CLEyeGetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_WHITEBALANCE_GREEN);
            }
            set
            {
                if (hwCameraIntPtr != IntPtr.Zero)
                {
                    CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_WHITEBALANCE_GREEN, value);
                }
            }
        }

        public int BlueBalance0To255
        {
            get
            {
                return CLEyeGetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_WHITEBALANCE_BLUE);
            }
            set
            {
                if (hwCameraIntPtr != IntPtr.Zero)
                {
                    CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_WHITEBALANCE_BLUE, value);
                }
            }
        }

        public void TakeSnapShot()
        {
            throw new NotImplementedException();
        }

        public void CloseCurrentVideoSource()
        {
        }

        public void OpenSettings()
        {
        }

        public int Gain0To79
        {
            get
            {
                return CLEyeGetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_GAIN);
            }
            set
            {
                CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_GAIN, value);
            }
        }

        public bool IsNewImageReady()
        {
            if(newImageRead && Monitor.TryEnter(cameraCopyBufferIntPtr, 1))
            {
                unsafe
                {
                    int width = currentImage.Width;
                    int height = currentImage.Height;

                    byte[] currentImageBuffer = currentImage.GetBuffer();
                    int offset = 0;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            currentImageBuffer[offset] = ((byte*)cameraCopyBufferIntPtr)[offset];
                            offset++;
                            currentImageBuffer[offset] = ((byte*)cameraCopyBufferIntPtr)[offset];
                            offset++;
                            currentImageBuffer[offset] = ((byte*)cameraCopyBufferIntPtr)[offset];
                            offset++;
                            currentImageBuffer[offset++] = 255;
                        }
                    }
                }
                newImageRead = false;
                currentImage.MarkImageChanged();
                return true;
            }

            return false;
        }


        public ImageBuffer CurrentImage
        {
            get
            {
                return currentImage;
            }
        }

        public CLEyeCamera(CLEyeCameraColorMode colorMode, CLEyeCameraResolution resolution)
        {
            int cameraImageWidth = 0;
            int cameraImageHeight = 0;
            
            hwCameraIntPtr = CLEyeCreateCamera(CameraUUID(0), colorMode, resolution, 30);
            if (hwCameraIntPtr == IntPtr.Zero)
            {
                return;
            }

            CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_VFLIP, 1);
            CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_AUTO_EXPOSURE, 0);
            CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_AUTO_GAIN, 0);

            CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_AUTO_WHITEBALANCE, 0);
            CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_WHITEBALANCE_GREEN, 128);
            CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_WHITEBALANCE_BLUE, 128);
            CLEyeSetCameraParameter(hwCameraIntPtr, CLEyeCameraParameter.CLEYE_WHITEBALANCE_RED, 128);

            CLEyeCameraGetFrameDimensions(hwCameraIntPtr, ref cameraImageWidth, ref cameraImageHeight);
            currentImage = new ImageBuffer(cameraImageWidth, cameraImageHeight, 32, new BlenderBGRA());
            cameraCopyBufferIntPtr = Marshal.AllocHGlobal(cameraImageWidth * cameraImageHeight * 4);
            // create thread exit event
            _exitEvent = new ManualResetEvent(false);
            // start capture here
            ThreadPool.QueueUserWorkItem(Capture);
        }

        // capture thread
        void Capture(object obj)
        {
            _threadRunning = true;
            CLEyeCameraStart(hwCameraIntPtr);
            while (_threadRunning)
            {
                unsafe
                {
                    if (Monitor.TryEnter(cameraCopyBufferIntPtr, 1))
                    {
                        if (CLEyeCameraGetFrame(hwCameraIntPtr, cameraCopyBufferIntPtr, 500))
                        {
                            newImageRead = true;
                        }
                    }
                }
            }

            CLEyeCameraStop(hwCameraIntPtr);
            CLEyeDestroyCamera(hwCameraIntPtr);
            _exitEvent.Set();
        }
    }
}
