using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;

using AForge.Video;
using AForge.Video.DirectShow;
using MatterHackers.Agg.Image;

namespace AForge
{
	public interface ICamera
    {
        void TakeSnapShot();
        ImageBuffer CurrentImage { get; }
        int Exposure0To511 { get; set; }
        int RedBalance0To255 { get; set; }
        int GreenBalance0To255 { get; set; }
        int BlueBalance0To255 { get; set; }
        bool IsNewImageReady();
        void CloseCurrentVideoSource();
        void OpenSettings();
    }

    public class AForgeCamera : ICamera
    {
        public enum DownSample { None, HalfSize };

        VideoCaptureDevice videoCaptureDevice;
        bool newImageReady = false;
        ImageBuffer asyncCopiedVideoImage = new ImageBuffer();
        ImageBuffer imageForExternalUse = new ImageBuffer();
        DownSample downSampleVideo = DownSample.None;

        bool flipY = true;

        public AForgeCamera(string preferedCameraName = null, int preferedWidth = 640, int preferedHeight = 480, DownSample downSampleVideo = DownSample.None)
        {
            this.downSampleVideo = downSampleVideo;

            if (preferedCameraName != null)
            {
                FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                foreach (FilterInfo info in videoDevices)
                {
                    if (info.Name.Contains(preferedCameraName))
                    {
                        videoCaptureDevice = new VideoCaptureDevice(info.MonikerString);
                        videoCaptureDevice.DesiredFrameSize = new Size(preferedWidth, preferedHeight);
                        break;
                    }
                }
            }

            if (videoCaptureDevice == null)
            {
                VideoCaptureDeviceForm form = new VideoCaptureDeviceForm();
                if (form.ShowDialog(null) == DialogResult.OK)
                {
                    // create video source
                    videoCaptureDevice = form.VideoDevice;
                }
            }

            if (videoCaptureDevice != null)
            {
                //videoCaptureDevice.DesiredFrameRate = 5;
                //videoCaptureDevice.ProvideSnapshots = true;
                //videoCaptureDevice.DesiredSnapshotSize = new Size(preferedWidth, preferedHeight);
                //videoCaptureDevice.SnapshotFrame += new NewFrameEventHandler(videoCaptureDevice_SnapshotFrame);

                asyncCopiedVideoImage = new ImageBuffer(videoCaptureDevice.DesiredFrameSize.Width, videoCaptureDevice.DesiredFrameSize.Height, 32, new BlenderBGRA());
                if (downSampleVideo == DownSample.HalfSize)
                {
                    imageForExternalUse = new ImageBuffer(videoCaptureDevice.DesiredFrameSize.Width / 2, videoCaptureDevice.DesiredFrameSize.Height / 2, 32, new BlenderBGRA());
                }
                else
                {
                    imageForExternalUse = new ImageBuffer(videoCaptureDevice.DesiredFrameSize.Width, videoCaptureDevice.DesiredFrameSize.Height, 32, new BlenderBGRA());
                }
                videoCaptureDevice.Start();
                videoCaptureDevice.NewFrame += new NewFrameEventHandler(source_NewFrame);
            }
        }

        public ImageBuffer CurrentImage 
        {
            get
            {
                return imageForExternalUse;
            }
        }

        public int Exposure0To511 { get; set; }
        public int RedBalance0To255 { get; set; }
        public int GreenBalance0To255 { get; set; }
        public int BlueBalance0To255 { get; set; }

        public void OpenSettings()
        {
            videoCaptureDevice.DisplayPropertyPage(IntPtr.Zero);
        }

        bool currentlyUsingCameraImage = false;
        public bool IsNewImageReady()
        {
            if (newImageReady)
            {
                if (!currentlyUsingCameraImage)
                {
                    currentlyUsingCameraImage = true;
                    lock (asyncCopiedVideoImage)
                    {
                        if (downSampleVideo == DownSample.HalfSize)
                        {
                            imageForExternalUse.NewGraphics2D().Render(asyncCopiedVideoImage, 0, 0, 0, .5, .5);
                        }
                        else
                        {
                            imageForExternalUse.NewGraphics2D().Render(asyncCopiedVideoImage, 0, 0);
                        }
                    }
                    imageForExternalUse.MarkImageChanged();

                    newImageReady = false;
                    currentlyUsingCameraImage = false;
                    return true;
                }
            }

            return false;
        }

        void source_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (!currentlyUsingCameraImage)
            {
                currentlyUsingCameraImage = true;
                Bitmap bitmap = eventArgs.Frame;
                if (bitmap.Width != asyncCopiedVideoImage.Width || bitmap.Height != asyncCopiedVideoImage.Height)
                {
                    asyncCopiedVideoImage = new ImageBuffer(bitmap.Width, bitmap.Height, 32, new BlenderBGRA());
                }
                UpdateImageBuffer(asyncCopiedVideoImage, bitmap);
                newImageReady = true;
                currentlyUsingCameraImage = false;
            }
        }

        public void TakeSnapShot()
        {
            videoCaptureDevice.Stop();
            videoCaptureDevice.ProvideSnapshots = true;
            videoCaptureDevice.SimulateTrigger();
            videoCaptureDevice.Start();

        }

        void videoCaptureDevice_SnapshotFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap bitmap = eventArgs.Frame;
            bitmap.Save("snapshot.png");
        }

        // Close video source if it is running
        public void CloseCurrentVideoSource()
        {
            if (videoCaptureDevice != null)
            {
                videoCaptureDevice.SignalToStop();

                // wait ~ 3 seconds
                for (int i = 0; i < 30; i++)
                {
                    if (!videoCaptureDevice.IsRunning)
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(100);
                }

                if (videoCaptureDevice.IsRunning)
                {
                    videoCaptureDevice.Stop();
                }

                videoCaptureDevice.Stop();

                videoCaptureDevice = null;
            }
        }

        internal void UpdateImageBuffer(ImageBuffer destImageBuffer, Bitmap sourceBitmap)
        {
            BitmapData bitmapData = null;
            bool isLocked = false;
            if (destImageBuffer != null)
            {
                if (!isLocked)
                {
                    bitmapData = sourceBitmap.LockBits(new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, sourceBitmap.PixelFormat);
                }
                int destBufferStrideInBytes = destImageBuffer.StrideInBytes();
                int destBufferHeight = destImageBuffer.Height;
                int destBufferWidth = destImageBuffer.Width;
                int destBufferHeightMinusOne = destBufferHeight - 1;
                int bitmapDataStride = bitmapData.Stride;
                int offset;
                byte[] buffer = destImageBuffer.GetBuffer(out offset);
                if (flipY)
                {
                    unsafe
                    {
                        byte* bitmapDataScan0 = (byte*)bitmapData.Scan0;
                        fixed (byte* pDestFixed = &buffer[offset])
                        {
                            byte* pSource = bitmapDataScan0;
                            for (int y = 0; y < destBufferHeight; y++)
                            {
                                byte* pDest = pDestFixed + destBufferStrideInBytes * (destBufferHeight - 1 - y);

                                for (int x = 0; x < destBufferWidth; x++)
                                {
                                    pDest[x * 4 + 0] = pSource[x * 3 + 0];
                                    pDest[x * 4 + 1] = pSource[x * 3 + 1];
                                    pDest[x * 4 + 2] = pSource[x * 3 + 2];
                                    pDest[x * 4 + 3] = 255;
                                }

                                pSource += bitmapDataStride;
                            }
                        }
                    }
                }
                else
                {
                    unsafe
                    {
                        byte* bitmapDataScan0 = (byte*)bitmapData.Scan0;
                        fixed (byte* pDestFixed = &buffer[offset])
                        {
                            byte* pSource = bitmapDataScan0;
                            for (int y = 0; y < destBufferHeight; y++)
                            {
                                byte* pDest = pDestFixed + destBufferStrideInBytes * (y);

                                for (int x = 0; x < destBufferWidth; x++)
                                {
                                    pDest[x * 4 + 0] = pSource[x * 3 + 0];
                                    pDest[x * 4 + 1] = pSource[x * 3 + 1];
                                    pDest[x * 4 + 2] = pSource[x * 3 + 2];
                                    pDest[x * 4 + 3] = 255;
                                }

                                pSource += bitmapDataStride;
                            }
                        }
                    }
                }
                if (!isLocked)
                {
                    sourceBitmap.UnlockBits(bitmapData);
                }
            }
        }
    }
}
