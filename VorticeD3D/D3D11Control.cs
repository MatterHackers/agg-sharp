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

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace MatterHackers.RenderGl
{
	public class D3D11Control : Control
	{
		public ID3D11Device Device { get; private set; }
		public ID3D11DeviceContext DeviceContext { get; private set; }
		public IDXGISwapChain SwapChain { get; private set; }
		public VorticeD3DGl GlBackend { get; private set; }

		private bool isInitialized;
		private bool isRecoveringDevice;

		private const int DxgiErrorDeviceRemoved = unchecked((int)0x887A0005);
		private const int DxgiErrorDeviceHung = unchecked((int)0x887A0006);
		private const int DxgiErrorDeviceReset = unchecked((int)0x887A0007);
		private const int DxgiErrorDriverInternalError = unchecked((int)0x887A0020);

		public D3D11Control()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint, true);
		}

		public void InitializeD3D()
		{
			if (isInitialized || IsDisposed || !IsHandleCreated) return;

			CreateDeviceResources();
			isInitialized = true;
		}

		private void CreateDeviceResources()
		{

			DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;

			var featureLevels = new[]
			{
				FeatureLevel.Level_11_1,
				FeatureLevel.Level_11_0,
				FeatureLevel.Level_10_1,
				FeatureLevel.Level_10_0,
			};

			ID3D11Device device;
			ID3D11DeviceContext deviceContext;
			D3D11.D3D11CreateDevice(
				(IDXGIAdapter)null,
				DriverType.Hardware,
				flags,
				featureLevels,
				out device,
				out deviceContext).CheckError();

			Device = device;
			DeviceContext = deviceContext;

			using var dxgiDevice = Device.QueryInterface<IDXGIDevice>();
			using var adapter = dxgiDevice.GetAdapter();
			using var factory = adapter.GetParent<IDXGIFactory2>();

			var swapChainDesc = new SwapChainDescription1
			{
				Width = (uint)Math.Max(1, ClientSize.Width),
				Height = (uint)Math.Max(1, ClientSize.Height),
				Format = Format.B8G8R8A8_UNorm,
				SampleDescription = new SampleDescription(1, 0),
				BufferUsage = Usage.RenderTargetOutput,
				BufferCount = 2,
				SwapEffect = SwapEffect.FlipDiscard,
				AlphaMode = AlphaMode.Ignore,
				Flags = SwapChainFlags.AllowModeSwitch,
			};

			SwapChain = factory.CreateSwapChainForHwnd(Device, Handle, swapChainDesc);

			GlBackend = new VorticeD3DGl();
			GlBackend.Initialize(Device, DeviceContext, SwapChain);
		}

		protected override bool IsInputKey(Keys keyData)
		{
			switch (keyData & Keys.KeyCode)
			{
				case Keys.Up:
				case Keys.Down:
				case Keys.Left:
				case Keys.Right:
				case Keys.Tab:
					return true;
			}

			return base.IsInputKey(keyData);
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			if (isInitialized && ClientSize.Width > 0 && ClientSize.Height > 0)
			{
               try
				{
					GlBackend.ResizeBuffers(ClientSize.Width, ClientSize.Height);
				}
				catch (Exception ex) when (TryRecoverIfDeviceLost(ex))
				{
				}
			}
		}

		public void Present()
		{
			if (isInitialized)
			{
                try
				{
					GlBackend.Present();
				}
				catch (Exception ex) when (TryRecoverIfDeviceLost(ex))
				{
				}
			}
		}

		public bool TryRecoverDevice()
		{
			if (isRecoveringDevice || IsDisposed || !IsHandleCreated)
			{
				return false;
			}

			try
			{
				isRecoveringDevice = true;
				DisposeDeviceResources();
				CreateDeviceResources();
				isInitialized = true;

                MatterHackers.RenderGl.OpenGl.GL.Instance = GlBackend;
				Graphics2DGpu.InvalidateGlCaches();

				return true;
			}
			catch
			{
				return false;
			}
			finally
			{
				isRecoveringDevice = false;
			}
		}

		private bool TryRecoverIfDeviceLost(Exception ex)
		{
			if (!IsDeviceLostException(ex))
			{
				return false;
			}

			TryRecoverDevice();
			return true;
		}

		private static bool IsDeviceLostException(Exception ex)
		{
			if (ex is SharpGenException sharpGenException)
			{
				return sharpGenException.HResult == DxgiErrorDeviceRemoved
					|| sharpGenException.HResult == DxgiErrorDeviceHung
					|| sharpGenException.HResult == DxgiErrorDeviceReset
					|| sharpGenException.HResult == DxgiErrorDriverInternalError;
			}

			if (ex.InnerException != null)
			{
				return IsDeviceLostException(ex.InnerException);
			}

			return false;
		}

		private void DisposeDeviceResources()
		{
			GlBackend?.Dispose();
			SwapChain?.Dispose();
			DeviceContext?.Dispose();
			Device?.Dispose();

			GlBackend = null;
			SwapChain = null;
			DeviceContext = null;
			Device = null;
			isInitialized = false;
		}

		public void CaptureScreenshot(string path)
		{
			if (!isInitialized) return;

			using var backBuffer = SwapChain.GetBuffer<ID3D11Texture2D>(0);
			var desc = backBuffer.Description;
			desc.Usage = ResourceUsage.Staging;
			desc.BindFlags = BindFlags.None;
			desc.CPUAccessFlags = CpuAccessFlags.Read;

			using var stagingTexture = Device.CreateTexture2D(desc);
			DeviceContext.CopyResource(stagingTexture, backBuffer);

			var mapped = DeviceContext.Map(stagingTexture, 0, MapMode.Read);
			try
			{
				int width = (int)desc.Width;
				int height = (int)desc.Height;
				using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
				var bmpData = bitmap.LockBits(
					new Rectangle(0, 0, width, height),
					ImageLockMode.WriteOnly,
					PixelFormat.Format32bppArgb);

				for (int y = 0; y < height; y++)
				{
					IntPtr srcRow = IntPtr.Add(mapped.DataPointer, y * (int)mapped.RowPitch);
					IntPtr dstRow = IntPtr.Add(bmpData.Scan0, y * bmpData.Stride);

					byte[] rowData = new byte[width * 4];
					Marshal.Copy(srcRow, rowData, 0, width * 4);
					Marshal.Copy(rowData, 0, dstRow, width * 4);
				}

				bitmap.UnlockBits(bmpData);
				bitmap.Save(path, ImageFormat.Png);
			}
			finally
			{
				DeviceContext.Unmap(stagingTexture, 0);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
               DisposeDeviceResources();
			}

			base.Dispose(disposing);
		}
	}
}
