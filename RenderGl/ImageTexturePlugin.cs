/*
Copyright (c) 2014-2026, Lars Brubaker
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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MatterHackers.Agg.Image;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	public class RemoveGlDataCallBackHolder
	{
		public event EventHandler releaseAllGlData;

		public void Release()
		{
			releaseAllGlData?.Invoke(this, null);
		}
	}

	public class ImageTexturePlugin
	{
		// A plugin owns a texture handle minted by, and a captured GL bound to, one specific context.
		// MatterCAD renders thumbnails on background worker threads that each have their own GL
		// context, so caching by pixel buffer alone would hand a worker the ui thread's plugin - and
		// DrawToGL would then pump immediate mode vertices into the ui thread's context while it is
		// mid-flush. Key by image buffer and then by context.
		// The inner map is a weak table too: a Dictionary<GL, ...> would keep every context that ever
		// drew this image alive forever, so a closed window would leak its whole gl cache. A weak
		// table's value may reference its own key (the plugin captures the gl) without pinning it.
		private static readonly ConditionalWeakTable<byte[], ConditionalWeakTable<GL, ImageTexturePlugin>> imagesWithCacheData = new ConditionalWeakTable<byte[], ConditionalWeakTable<GL, ImageTexturePlugin>>();

		// Guards the compound check-invalidate-recreate sequence over the inner per-image tables.
		// GetImageTexturePlugin is called concurrently by the ui thread and the thumbnail workers.
		private static readonly object cacheLock = new object();

		internal class glAllocatedData
		{
			// Weak so an entry parked in glDataNeedingToBeDeleted (waiting for its context to come back
			// through and free the texture name) can not keep a dead context's GL alive.
			internal WeakReference<GL> gl;
			internal int glTextureHandle;
			internal int refreshCountCreatedOn;

			// Informational only - SetCurrentContextData is not wired up on desktop, so the deferred
			// delete pass identifies the owning context by comparing gl instances instead.
			internal int glContextId;
			internal int imageWidth;
			internal int imageHeight;
			internal int hardwareWidth;
			internal int hardwareHeight;
			internal float offsetX;
			internal float offsetY;
			public float[] textureUVs;
			public float[] positions;

			internal void DeleteTextureData(object sender, EventArgs e)
			{
				// If the context is already collected the texture died with it and there is nothing
				// left to release.
				if (gl != null && gl.TryGetTarget(out var owningGl))
				{
					owningGl.DeleteTexture(glTextureHandle);
				}

				glTextureHandle = -1;
			}
		}

		private static List<glAllocatedData> glDataNeedingToBeDeleted = new List<glAllocatedData>();

		private GL gl;
		private glAllocatedData glData = new glAllocatedData();

		private int imageUpdateCount;
		private bool createdWithMipMaps;
		private bool clamp;
		private bool magFilterLinear = true;

		private static int currentGlobalRefreshCount = 0;

		public static void MarkAllImagesNeedRefresh()
		{
			currentGlobalRefreshCount++;
		}

		private static int contextId;

		private static RemoveGlDataCallBackHolder removeGlDataCallBackHolder;

		public static void SetCurrentContextData(int inContextId, RemoveGlDataCallBackHolder inCallBackHolder)
		{
			contextId = inContextId;
			removeGlDataCallBackHolder = inCallBackHolder;
		}

		public static ImageTexturePlugin GetImageTexturePlugin(GL gl, ImageBuffer imageToGetDisplayListFor, bool createAndUseMipMaps, bool textureMagFilterLinear = true, bool clamp = true)
		{
			var pluginsForImage = imagesWithCacheData.GetValue(
				imageToGetDisplayListFor.GetBuffer(),
				_ => new ConditionalWeakTable<GL, ImageTexturePlugin>());

			ImageTexturePlugin plugin;
			lock (cacheLock)
			{
				pluginsForImage.TryGetValue(gl, out plugin);
			}

			lock (glDataNeedingToBeDeleted)
			{
				// We run this in here to ensure that we are on the correct thread and have the correct
				// glcontext realized.
				for (int i = glDataNeedingToBeDeleted.Count - 1; i >= 0; i--)
				{
					var pendingDelete = glDataNeedingToBeDeleted[i];
					if (pendingDelete.gl == null
						|| !pendingDelete.gl.TryGetTarget(out var owningGl))
					{
						// The owning context is gone and took its textures with it. There is nothing to
						// free, but the entry must not sit here forever.
						glDataNeedingToBeDeleted.RemoveAt(i);
						continue;
					}

					// Only the owning context may delete its own texture names. Entries belonging to
					// another live context stay in the list until that context next comes through here.
					if (owningGl != gl)
					{
						continue;
					}

					int textureToDelete = pendingDelete.glTextureHandle;
					if (textureToDelete != -1
						&& pendingDelete.refreshCountCreatedOn == currentGlobalRefreshCount) // this is to leak on purpose on android for some gl that kills textures
					{
						gl.DeleteTexture(textureToDelete);
						if (removeGlDataCallBackHolder != null)
						{
							removeGlDataCallBackHolder.releaseAllGlData -= pendingDelete.DeleteTextureData;
						}
					}

					glDataNeedingToBeDeleted.RemoveAt(i);
				}
			}

			if (plugin != null
				&& (imageToGetDisplayListFor.ChangedCount != plugin.imageUpdateCount
				|| plugin.glData.refreshCountCreatedOn != currentGlobalRefreshCount
				|| plugin.glData.glTextureHandle == -1))
			{
				int textureToDelete = plugin.GLTextureHandle;
				if (plugin.glData.refreshCountCreatedOn == currentGlobalRefreshCount)
				{
					gl.DeleteTexture(textureToDelete);
				}

				plugin.glData.glTextureHandle = -1;
				lock (cacheLock)
				{
					// Only drop this context's entry - the other contexts' textures are still valid.
					pluginsForImage.Remove(gl);
				}

				// use the original settings
				createAndUseMipMaps = plugin.createdWithMipMaps;
				clamp = plugin.clamp;
				textureMagFilterLinear = plugin.magFilterLinear;
				plugin = null;
			}

			if (plugin == null)
			{
				var newPlugin = new ImageTexturePlugin(gl);
				lock (cacheLock)
				{
					pluginsForImage.AddOrUpdate(gl, newPlugin);
				}

				newPlugin.createdWithMipMaps = createAndUseMipMaps;
				newPlugin.clamp = clamp;
				newPlugin.glData.glContextId = contextId;
				newPlugin.glData.gl = new WeakReference<GL>(gl);
				newPlugin.CreateGlDataForImage(imageToGetDisplayListFor, textureMagFilterLinear);
				newPlugin.imageUpdateCount = imageToGetDisplayListFor.ChangedCount;
				newPlugin.glData.refreshCountCreatedOn = currentGlobalRefreshCount;

				if (removeGlDataCallBackHolder != null)
				{
					removeGlDataCallBackHolder.releaseAllGlData += newPlugin.glData.DeleteTextureData;
				}

				return newPlugin;
			}

			return plugin;
		}

		public int GLTextureHandle => glData.glTextureHandle;

		private ImageTexturePlugin(GL gl)
		{
			// This is private as you can't build one of these. You have to call GetImageTexturePlugin.
			this.gl = gl;
		}

		~ImageTexturePlugin()
		{
			lock (glDataNeedingToBeDeleted)
			{
				glDataNeedingToBeDeleted.Add(glData);
			}
		}

		private bool hwSupportsOnlyPowerOfTwoTextures = true;
		private bool checkedForHwSupportsOnlyPowerOfTwoTextures = false;

		public static (float[] TextureUVs, float[] Positions) CreateQuadData(
			int imageWidth,
			int imageHeight,
			int hardwareWidth,
			int hardwareHeight,
			double offsetX,
			double offsetY)
		{
			float texCoordX = imageWidth / (float)hardwareWidth;
			float texCoordY = imageHeight / (float)hardwareHeight;
			float offsetXF = (float)offsetX;
			float offsetYF = (float)offsetY;

			var textureUVs = new float[8];
			var positions = new float[8];

			textureUVs[0] = 0; textureUVs[1] = 0; positions[0] = 0 - offsetXF; positions[1] = 0 - offsetYF;
			textureUVs[2] = 0; textureUVs[3] = texCoordY; positions[2] = 0 - offsetXF; positions[3] = imageHeight - offsetYF;
			textureUVs[4] = texCoordX; textureUVs[5] = texCoordY; positions[4] = imageWidth - offsetXF; positions[5] = imageHeight - offsetYF;
			textureUVs[6] = texCoordX; textureUVs[7] = 0; positions[6] = imageWidth - offsetXF; positions[7] = 0 - offsetYF;

			return (textureUVs, positions);
		}

		private int SmallestHardwareCompatibleTextureSize(int size)
		{
			if (!checkedForHwSupportsOnlyPowerOfTwoTextures)
			{
				{
					// Compatible context (GL 1.0-2.1)
					string extensions = gl.GetString(StringName.Extensions);
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

		private void CreateGlDataForImage(ImageBuffer bufferedImage, bool textureMagFilterLinear)
		{
			int imageWidth = bufferedImage.Width;
			int imageHeight = bufferedImage.Height;
			int hardwareWidth = SmallestHardwareCompatibleTextureSize(imageWidth);
			int hardwareHeight = SmallestHardwareCompatibleTextureSize(imageHeight);
			float offsetX = (float)bufferedImage.OriginOffset.X;
			float offsetY = (float)bufferedImage.OriginOffset.Y;

			byte[] pixels = TextureUploadPixels.FromImage(bufferedImage, hardwareWidth, hardwareHeight);

			gl.Enable(EnableCap.Texture2D);
			// Create the texture handle
			glData.glTextureHandle = gl.GenTexture();

			gl.BindTexture(TextureTarget.Texture2D, glData.glTextureHandle);
			this.magFilterLinear = textureMagFilterLinear;
			RestoreSampling();

			gl.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, hardwareWidth, hardwareHeight,
				0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

			if (createdWithMipMaps)
			{
				int levelWidth = hardwareWidth;
				int levelHeight = hardwareHeight;
				int mipLevel = 1;
				while (levelWidth > 1 || levelHeight > 1)
				{
					pixels = TextureUploadPixels.DownsampleStraightAlpha(pixels, levelWidth, levelHeight, out levelWidth, out levelHeight);
					gl.TexImage2D(TextureTarget.Texture2D, mipLevel++, PixelInternalFormat.Rgba, levelWidth, levelHeight,
						0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
				}
			}

			glData.imageWidth = imageWidth;
			glData.imageHeight = imageHeight;
			glData.hardwareWidth = hardwareWidth;
			glData.hardwareHeight = hardwareHeight;
			glData.offsetX = offsetX;
			glData.offsetY = offsetY;

			var quadData = CreateQuadData(imageWidth, imageHeight, hardwareWidth, hardwareHeight, offsetX, offsetY);
			glData.textureUVs = quadData.TextureUVs;
			glData.positions = quadData.Positions;
		}

		/// <summary>The texture's width, which is the image's unless the hardware needed it padded to a power of two.</summary>
		internal int HardwareWidth => glData.hardwareWidth;

		/// <summary>The texture's height; see <see cref="HardwareWidth"/>.</summary>
		internal int HardwareHeight => glData.hardwareHeight;

		/// <summary>
		/// Sets the bound texture's filtering and wrapping to what this plugin was created with - after a caller (the
		/// GPU pattern fill) has sampled it another way.
		/// </summary>
		internal void RestoreSampling()
		{
			gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)(magFilterLinear ? TextureMagFilter.Linear : TextureMagFilter.Nearest));
			gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)(createdWithMipMaps ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
			var wrap = clamp ? TextureWrapMode.ClampToEdge : TextureWrapMode.Repeat;
			gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrap);
			gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrap);
		}

		public void DrawToGL()
		{
			if (glData.textureUVs == null || glData.positions == null)
			{
				var quadData = CreateQuadData(
					glData.imageWidth,
					glData.imageHeight,
					glData.hardwareWidth,
					glData.hardwareHeight,
					glData.offsetX,
					glData.offsetY);

				glData.textureUVs = quadData.TextureUVs;
				glData.positions = quadData.Positions;
			}

			gl.BindTexture(TextureTarget.Texture2D, GLTextureHandle);
			gl.Begin(BeginMode.TriangleFan);

			gl.TexCoord2(glData.textureUVs[0], glData.textureUVs[1]);
			gl.Vertex2(glData.positions[0], glData.positions[1]);

			gl.TexCoord2(glData.textureUVs[2], glData.textureUVs[3]);
			gl.Vertex2(glData.positions[2], glData.positions[3]);

			gl.TexCoord2(glData.textureUVs[4], glData.textureUVs[5]);
			gl.Vertex2(glData.positions[4], glData.positions[5]);

			gl.TexCoord2(glData.textureUVs[6], glData.textureUVs[7]);
			gl.Vertex2(glData.positions[6], glData.positions[7]);

			gl.End();
		}
	}
}