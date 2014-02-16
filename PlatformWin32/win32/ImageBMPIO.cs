/*
Copyright (c) 2014, Lars Brubaker
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
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace MatterHackers.Agg.Image
{
    public static class ImageBMPIO
    {
        static public bool LoadImageData(String fileName, ImageBuffer destImage)
        {
            if (System.IO.File.Exists(fileName))
            {
                Bitmap m_WidowsBitmap = new Bitmap(fileName);
                if (m_WidowsBitmap != null)
                {
                    switch (m_WidowsBitmap.PixelFormat)
                    {
                        case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                            {
                                destImage.Allocate(m_WidowsBitmap.Width, m_WidowsBitmap.Height, m_WidowsBitmap.Width * 4, 32);
                                if (destImage.GetRecieveBlender() == null)
                                {
                                    destImage.SetRecieveBlender(new BlenderBGRA());
                                }

                                BitmapData bitmapData = m_WidowsBitmap.LockBits(new Rectangle(0, 0, m_WidowsBitmap.Width, m_WidowsBitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, m_WidowsBitmap.PixelFormat);
                                int sourceIndex = 0;
                                int destIndex = 0;
                                unsafe
                                {
                                    int offset;
                                    byte[] destBuffer = destImage.GetBuffer(out offset);
                                    byte* pSourceBuffer = (byte*)bitmapData.Scan0;
                                    for (int y = 0; y < destImage.Height; y++)
                                    {
                                        destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
                                        for (int x = 0; x < destImage.Width; x++)
                                        {
#if true
                                            destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
                                            destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
                                            destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
                                            destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
#else
                                            RGBA_Bytes notPreMultiplied = new RGBA_Bytes(pSourceBuffer[sourceIndex + 0], pSourceBuffer[sourceIndex + 1], pSourceBuffer[sourceIndex + 2], pSourceBuffer[sourceIndex + 3]);
                                            sourceIndex += 4;
                                            RGBA_Bytes preMultiplied = notPreMultiplied.GetAsRGBA_Floats().premultiply().GetAsRGBA_Bytes();
                                            destBuffer[destIndex++] = preMultiplied.blue;
                                            destBuffer[destIndex++] = preMultiplied.green;
                                            destBuffer[destIndex++] = preMultiplied.red;
                                            destBuffer[destIndex++] = preMultiplied.alpha;
#endif
                                        }
                                    }
                                }

                                m_WidowsBitmap.UnlockBits(bitmapData);

                                return true;
                            }

                        case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                            {
                                destImage.Allocate(m_WidowsBitmap.Width, m_WidowsBitmap.Height, m_WidowsBitmap.Width * 4, 32);

                                BitmapData bitmapData = m_WidowsBitmap.LockBits(new Rectangle(0, 0, m_WidowsBitmap.Width, m_WidowsBitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, m_WidowsBitmap.PixelFormat);
                                int sourceIndex = 0;
                                int destIndex = 0;
                                unsafe
                                {
                                    int offset;
                                    byte[] destBuffer = destImage.GetBuffer(out offset);
                                    byte* pSourceBuffer = (byte*)bitmapData.Scan0;
                                    for (int y = 0; y < destImage.Height; y++)
                                    {
                                        destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
                                        for (int x = 0; x < destImage.Width; x++)
                                        {
                                            destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
                                            destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
                                            destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
                                            destBuffer[destIndex++] = 255;
                                        }
                                    }
                                }

                                m_WidowsBitmap.UnlockBits(bitmapData);
                                return true;
                            }

                        case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                            {
                                destImage.Allocate(m_WidowsBitmap.Width, m_WidowsBitmap.Height, m_WidowsBitmap.Width, 8);

                                BitmapData bitmapData = m_WidowsBitmap.LockBits(new Rectangle(0, 0, m_WidowsBitmap.Width, m_WidowsBitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, m_WidowsBitmap.PixelFormat);
                                int sourceIndex = 0;
                                int destIndex = 0;
                                unsafe
                                {
                                    int offset;
                                    byte[] destBuffer = destImage.GetBuffer(out offset);
                                    byte* pSourceBuffer = (byte*)bitmapData.Scan0;
                                    for (int y = 0; y < destImage.Height; y++)
                                    {
                                        destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
                                        for (int x = 0; x < destImage.Width; x++)
                                        {
                                            destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
                                        }
                                    }
                                }

                                m_WidowsBitmap.UnlockBits(bitmapData);
                                return true;
                            }

                        default:
                            throw new System.NotImplementedException();
                    }

                }
            }
            else
            {
                throw new System.Exception(string.Format("Image file not found: {0}", fileName));
            }
            return false;
        }

        static public bool SaveImageData(String filename, IImageByte sourceImage)
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            ImageFormat format = ImageFormat.Jpeg;
            if (filename.ToLower().EndsWith(".png"))
            {
                format = ImageFormat.Png;
            }
            else if (!filename.ToLower().EndsWith(".jpg") && !filename.ToLower().EndsWith(".jpeg"))
            {
                filename += ".jpg";
            }

            if (!System.IO.File.Exists(filename))
            {
                if (sourceImage.BitDepth == 32)
                {
                    Bitmap bitmapToSave = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format24bppRgb);
                    BitmapData bitmapData = bitmapToSave.LockBits(new Rectangle(0, 0, bitmapToSave.Width, bitmapToSave.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmapToSave.PixelFormat);
                    int destIndex = 0;
                    unsafe
                    {
                        byte[] sourceBuffer = sourceImage.GetBuffer();
                        byte* pDestBuffer = (byte*)bitmapData.Scan0;
                        int scanlinePadding = bitmapData.Stride - bitmapData.Width * 3;
                        for (int y = 0; y < sourceImage.Height; y++)
                        {
                            int sourceIndex = sourceImage.GetBufferOffsetXY(0, sourceImage.Height - 1 - y);
                            for (int x = 0; x < sourceImage.Width; x++)
                            {
                                pDestBuffer[destIndex++] = sourceBuffer[sourceIndex++];
                                pDestBuffer[destIndex++] = sourceBuffer[sourceIndex++];
                                pDestBuffer[destIndex++] = sourceBuffer[sourceIndex++];
                                sourceIndex++;
                            }
                            destIndex += scanlinePadding;
                        }
                    }
                    bitmapToSave.Save(filename, format);
                    bitmapToSave.UnlockBits(bitmapData);
                    return true;
                }
                else if (sourceImage.BitDepth == 8 && format == ImageFormat.Png)
                {
                    Bitmap bitmapToSave = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format8bppIndexed);
                    ColorPalette palette = bitmapToSave.Palette;
                    for (int i = 0; i < palette.Entries.Length; i++)
                    {
                        palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                    bitmapToSave.Palette = palette;
                    BitmapData bitmapData = bitmapToSave.LockBits(new Rectangle(0, 0, bitmapToSave.Width, bitmapToSave.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmapToSave.PixelFormat);
                    int destIndex = 0;
                    unsafe
                    {
                        byte[] sourceBuffer = sourceImage.GetBuffer();
                        byte* pDestBuffer = (byte*)bitmapData.Scan0;
                        for (int y = 0; y < sourceImage.Height; y++)
                        {
                            int sourceIndex = sourceImage.GetBufferOffsetXY(0, sourceImage.Height - 1 - y);
                            for (int x = 0; x < sourceImage.Width; x++)
                            {
                                pDestBuffer[destIndex++] = sourceBuffer[sourceIndex++];
                            }
                        }
                    }
                    bitmapToSave.Save(filename, format);
                    bitmapToSave.UnlockBits(bitmapData);
                    return true;
                }
                else
                {
                    throw new System.NotImplementedException();
                }
            }

            return false;
        }

        static public bool LoadImageData(String filename, ImageBufferFloat destImage)
        {
            if (System.IO.File.Exists(filename))
            {
                Bitmap m_WidowsBitmap = new Bitmap(filename);
                if (m_WidowsBitmap != null)
                {
                    switch (m_WidowsBitmap.PixelFormat)
                    {
                        case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                            destImage.Allocate(m_WidowsBitmap.Width, m_WidowsBitmap.Height, m_WidowsBitmap.Width * 4, 128);
                            break;

                        default:
                            throw new System.NotImplementedException();
                    }

                    BitmapData bitmapData = m_WidowsBitmap.LockBits(new Rectangle(0, 0, m_WidowsBitmap.Width, m_WidowsBitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, m_WidowsBitmap.PixelFormat);
                    int sourceIndex = 0;
                    int destIndex = 0;
                    unsafe
                    {
                        int offset;
                        float[] destBuffer = destImage.GetBuffer(out offset);
                        byte* pSourceBuffer = (byte*)bitmapData.Scan0;
                        for (int y = 0; y < destImage.Height; y++)
                        {
                            destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
                            for (int x = 0; x < destImage.Width; x++)
                            {
                                destBuffer[destIndex++] = pSourceBuffer[sourceIndex++] / 255.0f;
                                destBuffer[destIndex++] = pSourceBuffer[sourceIndex++] / 255.0f;
                                destBuffer[destIndex++] = pSourceBuffer[sourceIndex++] / 255.0f;
                                destBuffer[destIndex++] = 1.0f;
                            }
                        }
                    }

                    m_WidowsBitmap.UnlockBits(bitmapData);

                    return true;
                }
            }

            return false;
        }
    }
}
