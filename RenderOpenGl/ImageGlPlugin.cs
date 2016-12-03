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

#define ON_IMAGE_CHANGED_ALWAYS_CREATE_IMAGE

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MatterHackers.RenderOpenGl
{
    public class RemoveGlDataCallBackHolder
    {
        public event EventHandler releaseAllGlData;

        public void Release()
        {
            releaseAllGlData?.Invoke(this, null);
        }
    }

    public class ImageGlPlugin
    {
        private static ConditionalWeakTable<Byte[], ImageGlPlugin> imagesWithCacheData = new ConditionalWeakTable<Byte[], ImageGlPlugin>();

        internal class glAllocatedData
        {
            internal int glTextureHandle;
            internal int refreshCountCreatedOn;
            internal int glContextId;
            public float[] textureUVs;
            public float[] positions;

            internal void DeleteTextureData(object sender, EventArgs e)
            {
                GL.DeleteTextures(1, ref glTextureHandle);
                glTextureHandle = -1;
            }
        }

        private static List<glAllocatedData> glDataNeedingToBeDeleted = new List<glAllocatedData>();

        private glAllocatedData glData = new glAllocatedData();
        private int imageUpdateCount;
        private bool createdWithMipMaps;

        private static int currentGlobalRefreshCount = 0;

        static public void MarkAllImagesNeedRefresh()
        {
            currentGlobalRefreshCount++;
        }

        static int contextId;
        static RemoveGlDataCallBackHolder removeGlDataCallBackHolder;
        public static void SetCurrentContextData(int inContextId, RemoveGlDataCallBackHolder inCallBackHolder)
        {
            contextId = inContextId;
            removeGlDataCallBackHolder = inCallBackHolder;
        }

        static public ImageGlPlugin GetImageGlPlugin(ImageBuffer imageToGetDisplayListFor, bool createAndUseMipMaps, bool TextureMagFilterLinear = true)
		{
			ImageGlPlugin plugin;
			imagesWithCacheData.TryGetValue(imageToGetDisplayListFor.GetBuffer(), out plugin);

			lock(glDataNeedingToBeDeleted)
			{
				// We run this in here to ensure that we are on the correct thread and have the correct
				// glcontext realized.
				for (int i = glDataNeedingToBeDeleted.Count - 1; i >= 0; i--)
				{
					int textureToDelete = glDataNeedingToBeDeleted[i].glTextureHandle;
					if (textureToDelete != -1
                        && glDataNeedingToBeDeleted[i].glContextId == contextId
                        && glDataNeedingToBeDeleted[i].refreshCountCreatedOn == currentGlobalRefreshCount) // this is to leak on purpose on android for some gl that kills textures
					{
						GL.DeleteTextures(1, ref textureToDelete);
						if (removeGlDataCallBackHolder != null)
						{
							removeGlDataCallBackHolder.releaseAllGlData -= glDataNeedingToBeDeleted[i].DeleteTextureData;
						}
                    }
					glDataNeedingToBeDeleted.RemoveAt(i);
				}
			}

#if ON_IMAGE_CHANGED_ALWAYS_CREATE_IMAGE
			if (plugin != null
				&& (imageToGetDisplayListFor.ChangedCount != plugin.imageUpdateCount
				|| plugin.glData.refreshCountCreatedOn != currentGlobalRefreshCount
                || plugin.glData.glTextureHandle == -1))
			{
				int textureToDelete = plugin.GLTextureHandle;
				if (plugin.glData.refreshCountCreatedOn == currentGlobalRefreshCount)
				{
					GL.DeleteTextures(1, ref textureToDelete);
				}
				plugin.glData.glTextureHandle = -1;
				imagesWithCacheData.Remove(imageToGetDisplayListFor.GetBuffer());
				plugin = null;
			}

			if (plugin == null)
			{
				ImageGlPlugin newPlugin = new ImageGlPlugin();
				imagesWithCacheData.Add(imageToGetDisplayListFor.GetBuffer(), newPlugin);
				newPlugin.createdWithMipMaps = createAndUseMipMaps;
                newPlugin.glData.glContextId = contextId;
                newPlugin.CreateGlDataForImage(imageToGetDisplayListFor, TextureMagFilterLinear);
				newPlugin.imageUpdateCount = imageToGetDisplayListFor.ChangedCount;
				newPlugin.glData.refreshCountCreatedOn = currentGlobalRefreshCount;
				if(removeGlDataCallBackHolder != null)
				{
					removeGlDataCallBackHolder.releaseAllGlData += newPlugin.glData.DeleteTextureData;
				}

                return newPlugin;
			}
#else
            if (plugin == null)
            {
                ImageGlPlugin newPlugin = new ImageGlPlugin();
                imagesWithCacheData.Add(imageToGetDisplayListFor.GetBuffer(), newPlugin);
                newPlugin.createdWithMipMaps = createAndUseMipMaps;
                newPlugin.CreateGlDataForImage(imageToGetDisplayListFor, TextureMagFilterLinear);
                newPlugin.imageUpdateCount = imageToGetDisplayListFor.ChangedCount;
				newPlugin.refreshCountCreatedOn = currentGlobalRefreshCount;
                return newPlugin;
            }

            if(imageToGetDisplayListFor.ChangedCount != plugin.imageUpdateCount
				|| plugin.refreshCountCreatedOn != currentGlobalRefreshCount)
            {
                plugin.imageUpdateCount = imageToGetDisplayListFor.ChangedCount;
				plugin.refreshCountCreatedOn = currentGlobalRefreshCount;
                GL.BindTexture(TextureTarget.Texture2D, plugin.GLTextureHandle);
                // Create the texture
                switch (imageToGetDisplayListFor.BitDepth)
                {
                    case 8:
                        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, imageToGetDisplayListFor.Width, imageToGetDisplayListFor.Height,
                            PixelFormat.Luminance, PixelType.UnsignedByte, imageToGetDisplayListFor.GetBuffer());
                        break;

                    case 24:
                        // our bitmaps are not padded and GL is having a problem with them so don't use 24 bit unless you fix this.
                        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, imageToGetDisplayListFor.Width, imageToGetDisplayListFor.Height,
                            PixelFormat.Bgr, PixelType.UnsignedByte, imageToGetDisplayListFor.GetBuffer());
                        break;

                    case 32:
                        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, imageToGetDisplayListFor.Width, imageToGetDisplayListFor.Height,
                            PixelFormat.Bgra, PixelType.UnsignedByte, imageToGetDisplayListFor.GetBuffer());
                        break;

                    default:
                        throw new NotImplementedException();
                }

                if (plugin.createdWithMipMaps)
                {
                    if (GLMajorVersion < 3)
                    {
                        switch (imageToGetDisplayListFor.BitDepth)
                        {
                            case 32:
                                {
                                    ImageBuffer sourceImage = new ImageBuffer(imageToGetDisplayListFor);
                                    ImageBuffer tempImage = new ImageBuffer(sourceImage.Width / 2, sourceImage.Height / 2);
                                    tempImage.NewGraphics2D().Render(sourceImage, 0, 0, 0, .5, .5);
                                    int mipLevel = 1;
                                    while (sourceImage.Width > 1 && sourceImage.Height > 1)
                                    {
                                        GL.TexSubImage2D(TextureTarget.Texture2D, mipLevel++, 0, 0, tempImage.Width, tempImage.Height,
                                            PixelFormat.Bgra, PixelType.UnsignedByte, tempImage.GetBuffer());

                                        sourceImage = new ImageBuffer(tempImage);
                                        tempImage = new ImageBuffer(Math.Max(1, sourceImage.Width / 2), Math.Max(1, sourceImage.Height / 2));
                                        tempImage.NewGraphics2D().Render(sourceImage, 0, 0,
                                            0,
                                            (double)tempImage.Width / (double)sourceImage.Width,
                                            (double)tempImage.Height / (double)sourceImage.Height);
                                    }
                                }
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                    }
                }
            }
#endif
			return plugin;
		}

        public int GLTextureHandle
		{
			get
			{
				return glData.glTextureHandle;
			}
		}

		private ImageGlPlugin()
		{
			// This is private as you can't build one of these. You have to call GetImageGlPlugin.
		}

		~ImageGlPlugin()
		{
			lock(glDataNeedingToBeDeleted)
			{
				glDataNeedingToBeDeleted.Add(glData);
			}
		}

		private bool hwSupportsOnlyPowerOfTwoTextures = true;
		private bool checkedForHwSupportsOnlyPowerOfTwoTextures = false;

		private int SmallestHardwareCompatibleTextureSize(int size)
		{
			if (!checkedForHwSupportsOnlyPowerOfTwoTextures)
			{
				{
					// Compatible context (GL 1.0-2.1)
					string extensions = GL.GetString(StringName.Extensions);
					if (extensions.Contains("ARB_texture_non_power_of_two"))
					{
						hwSupportsOnlyPowerOfTwoTextures = false;
					}
				}

				checkedForHwSupportsOnlyPowerOfTwoTextures = true;
			}

			if (hwSupportsOnlyPowerOfTwoTextures)
			{
				return MathHelper.FirstPowerTowGreaterThanOrEqualTo(size);
			}
			else
			{
				return size;
			}
		}

		private void CreateGlDataForImage(ImageBuffer bufferedImage, bool TextureMagFilterLinear)
		{
			//Next we expand the image into an openGL texture
			int imageWidth = bufferedImage.Width;
			int imageHeight = bufferedImage.Height;
			int bufferOffset;
			byte[] imageBuffer = bufferedImage.GetBuffer(out bufferOffset);
			int hardwareWidth = SmallestHardwareCompatibleTextureSize(imageWidth);
			int hardwareHeight = SmallestHardwareCompatibleTextureSize(imageHeight);
			byte[] hardwareExpandedPixelBuffer = imageBuffer;
			if (hardwareWidth != imageWidth || hardwareHeight != imageHeight)
			{
				// we have to put the data on a buffer that GL can handle.
				hardwareExpandedPixelBuffer = new byte[4 * hardwareWidth * hardwareHeight];
				switch (bufferedImage.BitDepth)
				{
					case 32:
						for (int y = 0; y < hardwareHeight; y++)
						{
							for (int x = 0; x < hardwareWidth; x++)
							{
								int pixelIndex = 4 * (x + y * hardwareWidth);
								if (x >= imageWidth || y >= imageHeight)
								{
									hardwareExpandedPixelBuffer[pixelIndex + 0] = 0;
									hardwareExpandedPixelBuffer[pixelIndex + 1] = 0;
									hardwareExpandedPixelBuffer[pixelIndex + 2] = 0;
									hardwareExpandedPixelBuffer[pixelIndex + 3] = 0;
								}
								else
								{
									hardwareExpandedPixelBuffer[pixelIndex + 0] = imageBuffer[4 * (x + y * imageWidth) + 2];
									hardwareExpandedPixelBuffer[pixelIndex + 1] = imageBuffer[4 * (x + y * imageWidth) + 1];
									hardwareExpandedPixelBuffer[pixelIndex + 2] = imageBuffer[4 * (x + y * imageWidth) + 0];
									hardwareExpandedPixelBuffer[pixelIndex + 3] = imageBuffer[4 * (x + y * imageWidth) + 3];
								}
							}
						}
						break;

					default:
						throw new NotImplementedException();
				}
			}

			GL.Enable(EnableCap.Texture2D);
			// Create the texture handle
			GL.GenTextures(1, out glData.glTextureHandle);

			// Set up some texture parameters for openGL
			GL.BindTexture(TextureTarget.Texture2D, glData.glTextureHandle);
			if (TextureMagFilterLinear)
			{
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
			}
			else
			{
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
			}

			if (createdWithMipMaps)
			{
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
			}
			else
			{
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
			}

			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

			// Create the texture
			switch (bufferedImage.BitDepth)
			{
#if false // not implemented in our gl wrapper and never used in our current code
                case 8:
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Luminance, hardwareWidth, hardwareHeight,
                        0, PixelFormat.Luminance, PixelType.UnsignedByte, hardwareExpandedPixelBuffer);
                    break;

                case 24:
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, hardwareWidth, hardwareHeight,
                        0, PixelFormat.Rgb, PixelType.UnsignedByte, hardwareExpandedPixelBuffer);
                    break;
#endif

				case 32:
#if __ANDROID__
					GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, hardwareWidth, hardwareHeight,
						0, PixelFormat.Rgba, PixelType.UnsignedByte, hardwareExpandedPixelBuffer);
#else
					GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, hardwareWidth, hardwareHeight,
						0, PixelFormat.Bgra, PixelType.UnsignedByte, hardwareExpandedPixelBuffer);
#endif
					break;

				default:
					throw new NotImplementedException();
			}
			hardwareExpandedPixelBuffer = null;

			if (createdWithMipMaps)
			{
				switch (bufferedImage.BitDepth)
				{
					case 32:
						{
							ImageBuffer sourceImage = new ImageBuffer(bufferedImage);
							ImageBuffer tempImage = new ImageBuffer(sourceImage.Width / 2, sourceImage.Height / 2);
							tempImage.NewGraphics2D().Render(sourceImage, 0, 0, 0, .5, .5);
							int mipLevel = 1;
							while (sourceImage.Width > 1 && sourceImage.Height > 1)
							{
#if __ANDROID__
                                GL.TexImage2D(TextureTarget.Texture2D, mipLevel++, PixelInternalFormat.Rgba, tempImage.Width, tempImage.Height,
									0, PixelFormat.Rgba, PixelType.UnsignedByte, tempImage.GetBuffer());
#else
								GL.TexImage2D(TextureTarget.Texture2D, mipLevel++, PixelInternalFormat.Rgba, tempImage.Width, tempImage.Height,
									0, PixelFormat.Bgra, PixelType.UnsignedByte, tempImage.GetBuffer());
#endif
								sourceImage = new ImageBuffer(tempImage);
								tempImage = new ImageBuffer(Math.Max(1, sourceImage.Width / 2), Math.Max(1, sourceImage.Height / 2));
								tempImage.NewGraphics2D().Render(sourceImage, 0, 0,
									0,
									(double)tempImage.Width / (double)sourceImage.Width,
									(double)tempImage.Height / (double)sourceImage.Height);
							}
						}
						break;

					default:
						throw new NotImplementedException();
				}
			}

			float texCoordX = imageWidth / (float)hardwareWidth;
			float texCoordY = imageHeight / (float)hardwareHeight;

			float OffsetX = (float)bufferedImage.OriginOffset.x;
			float OffsetY = (float)bufferedImage.OriginOffset.y;

			glData.textureUVs = new float[8];
			glData.positions = new float[8];

			glData.textureUVs[0] = 0; glData.textureUVs[1] = 0; glData.positions[0] = 0 - OffsetX; glData.positions[1] = 0 - OffsetY;
			glData.textureUVs[2] = 0; glData.textureUVs[3] = texCoordY; glData.positions[2] = 0 - OffsetX; glData.positions[3] = imageHeight - OffsetY;
			glData.textureUVs[4] = texCoordX; glData.textureUVs[5] = texCoordY; glData.positions[4] = imageWidth - OffsetX; glData.positions[5] = imageHeight - OffsetY;
			glData.textureUVs[6] = texCoordX; glData.textureUVs[7] = 0; glData.positions[6] = imageWidth - OffsetX; glData.positions[7] = 0 - OffsetY;
		}

		public void DrawToGL()
		{
			GL.BindTexture(TextureTarget.Texture2D, GLTextureHandle);
#if true
			GL.Begin(BeginMode.TriangleFan);
			GL.TexCoord2(glData.textureUVs[0], glData.textureUVs[1]); GL.Vertex2(glData.positions[0], glData.positions[1]);
			GL.TexCoord2(glData.textureUVs[2], glData.textureUVs[3]); GL.Vertex2(glData.positions[2], glData.positions[3]);
			GL.TexCoord2(glData.textureUVs[4], glData.textureUVs[5]); GL.Vertex2(glData.positions[4], glData.positions[5]);
			GL.TexCoord2(glData.textureUVs[6], glData.textureUVs[7]); GL.Vertex2(glData.positions[6], glData.positions[7]);

			GL.End();
#else
            GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, glData.textureUVs);
            GL.EnableClientState(ArrayCap.TextureCoordArray);

            GL.VertexPointer(2, VertexPointerType.Float, 0, glData.positions);
            GL.EnableClientState(ArrayCap.VertexArray);

            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);

            GL.Disable(EnableCap.Texture2D);
            GL.DisableClientState(ArrayCap.TextureCoordArray);
            GL.DisableClientState(ArrayCap.VertexArray);
#endif
		}
	}
}