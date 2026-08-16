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
*/

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MatterHackers.WebGpu.Example
{
	/// <summary>
	/// The wgpu-native HWND swapchain spike. Run with no arguments for an interactive window (resize it
	/// to exercise surface reconfiguration); run with <c>--smoke</c> to render a fixed number of frames
	/// and then close, exiting non zero if wgpu reported an error or lost the device.
	/// </summary>
	public static class Program
	{
		private const int SmokeFrameCount = 60;

		[STAThread]
		public static int Main(string[] args)
		{
			bool smoke = Array.Exists(args, argument => string.Equals(argument, "--smoke", StringComparison.OrdinalIgnoreCase));

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			var control = new TriangleControl { Dock = DockStyle.Fill };
			var form = new Form
			{
				Text = "wgpu-native triangle",
				ClientSize = new Size(800, 600),
				StartPosition = FormStartPosition.CenterScreen,
			};

			form.Controls.Add(control);

			string failure = null;
			string adapterDescription = "(no adapter)";
			int framesRendered = 0;

			// Idle only fires when the message queue drains, so the render loop lives inside it and keeps
			// going while no messages are pending - the standard WinForms continuous-render pump. Fifo
			// present mode is what paces it.
			void OnIdle(object sender, EventArgs e)
			{
				while (failure == null && NoMessagesPending())
				{
					var renderer = control.Renderer;
					if (renderer == null)
					{
						return;
					}

					renderer.RenderFrame();

					adapterDescription = $"{renderer.AdapterName} (backend {renderer.BackendType})";
					framesRendered = renderer.FramesRendered;

					// Static rather than per renderer: see TriangleRenderer.FirstError.
					if (TriangleRenderer.FirstError != null)
					{
						failure = TriangleRenderer.FirstError;
						form.Close();
						return;
					}

					if (smoke)
					{
						// Resize part way through so the smoke run also covers surface reconfiguration,
						// which is the part of the swapchain contract that has no offscreen equivalent.
						if (renderer.FramesRendered == SmokeFrameCount / 2)
						{
							form.ClientSize = new Size(640, 480);
						}

						if (renderer.FramesRendered >= SmokeFrameCount)
						{
							form.Close();
							return;
						}
					}
				}
			}

			Application.Idle += OnIdle;
			try
			{
				Application.Run(form);
			}
			catch (Exception exception)
			{
				failure = exception.ToString();
			}
			finally
			{
				Application.Idle -= OnIdle;
			}

			Console.WriteLine($"wgpu adapter: {adapterDescription}");
			Console.WriteLine($"frames rendered: {framesRendered}");

			if (failure != null)
			{
				Console.Error.WriteLine(failure);
				return 1;
			}

			if (smoke && framesRendered < SmokeFrameCount)
			{
				Console.Error.WriteLine($"only rendered {framesRendered} of {SmokeFrameCount} frames");
				return 1;
			}

			return 0;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct NativeMessage
		{
			public IntPtr handle;

			public uint message;

			public IntPtr wParam;

			public IntPtr lParam;

			public uint time;

			public Point point;
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);

		private static bool NoMessagesPending() => !PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
	}
}
