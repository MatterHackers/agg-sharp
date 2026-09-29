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

using MatterHackers.Agg.Image;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Threading;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// One open video: an IMFSourceReader on the file's first video stream, set to decode to RGB32, plus
	/// what it takes to turn a decoded sample into an upright agg <see cref="ImageBuffer"/>.
	/// </summary>
	/// <remarks>
	/// Advanced video processing is what lets the reader output RGB32 from any decoder's native YUV; it also
	/// handles the colour matrix, and it turns the frames upright by the file's rotation itself. What it does
	/// not do is crop the codec's padding, and after its rotation the output type no longer says where the
	/// picture is - so <see cref="ReadOutputFormat"/> works that out from the native type. Should an output
	/// type still carry a rotation (basic video processing leaves it, for one), <see cref="CopyPixels"/>
	/// applies what is left; both routes give the same upright picture.
	/// </remarks>
	internal sealed unsafe class MediaFoundationVideoSession : IDisposable
	{
		private readonly string fileName;
		private bool started;
		private IntPtr reader;

		// The file's rotation (reported, and already applied by the video processor), the stored frame size,
		// and the part of the stored frame that is picture (its minimum display aperture): a 1080p stream
		// decodes 1088 rows high, and the last eight are padding.
		private int fileRotation;
		private int nativeWidth;
		private int nativeHeight;
		private Rectangle nativeAperture;

		// The decoded output's size and the picture within it, and whatever rotation it still needs.
		private int bufferWidth;
		private int bufferHeight;
		private int defaultStride;
		private int cropX;
		private int cropY;
		private int cropWidth;
		private int cropHeight;
		private int remainingRotation;

		// The frame last decoded, kept so a later time in the same display interval is served from it, and
		// whether the reader sits just after it (false before the first read and after end of stream).
		private IntPtr current;
		private long currentStart;
		private long currentEnd;
		private bool canReadOn;

		/// <summary>
		/// How far past the last decoded frame a time may be and still be reached by reading on rather than
		/// seeking. Decoding is nearly all the cost (the RGB32 conversion measured about a fifth of it), and
		/// every frame between two times has to be decoded anyway unless a key frame lies between them, so
		/// reading on only loses to a seek across a gap long enough to hold a key frame well before the target.
		/// </summary>
		private static readonly TimeSpan ReadOnWindow = TimeSpan.FromSeconds(3);

		private MediaFoundationVideoSession(string path)
		{
			fileName = Path.GetFileName(path);
		}

		public VideoInfo Info { get; private set; }

		/// <summary>
		/// Opens <paramref name="path"/>, or throws <see cref="VideoFrameReaderException"/> with a message the
		/// user can act on.
		/// </summary>
		public static MediaFoundationVideoSession Open(string path)
		{
			ArgumentNullException.ThrowIfNull(path);
			var session = new MediaFoundationVideoSession(path);
			try
			{
				session.OpenReader(Path.GetFullPath(path));
				return session;
			}
			catch (COMException e)
			{
				session.Dispose();
				throw session.Unexpected(e);
			}
			catch (Exception e) when (MissingMediaFoundation(e) is VideoFrameReaderException missing)
			{
				session.Dispose();
				throw missing;
			}
			catch
			{
				session.Dispose();
				throw;
			}
		}

		/// <summary>
		/// The user-facing error for Media Foundation not being there at all, or null when <paramref name="e"/>
		/// is something else. Windows N and KN editions ship without it until the Media Feature Pack is added,
		/// so mfplat.dll (or mfreadwrite.dll) fails to load, or loads without an entry point. Opening a
		/// session calls nothing native but Media Foundation, so any such failure there is this one.
		/// </summary>
		internal static VideoFrameReaderException MissingMediaFoundation(Exception e)
		{
			if (e is DllNotFoundException || e is EntryPointNotFoundException)
			{
				return new VideoFrameReaderException(
					"Windows is missing its media features, so it cannot read videos. "
						+ "Open Settings, go to Apps > Optional features, add 'Media Feature Pack', restart, then try again.",
					e);
			}

			return null;
		}

		private void OpenReader(string fullPath)
		{
			// Checked up front because Media Foundation's answer for a missing file is a bare HRESULT.
			if (!File.Exists(fullPath))
			{
				throw new VideoFrameReaderException($"Could not find the video '{fullPath}'. Check that the file still exists.");
			}

			MediaFoundation.Startup();
			started = true;

			var attributes = MediaFoundation.CreateAttributes();
			int hr;
			try
			{
				MediaFoundation.SetUInt32(attributes, MediaFoundation.EnableAdvancedVideoProcessing, 1);
				hr = MediaFoundation.CreateSourceReader(fullPath, attributes, out reader);
			}
			finally
			{
				MediaFoundation.Release(ref attributes);
			}

			if (hr == MediaFoundation.UnsupportedByteStreamType)
			{
				throw new VideoFrameReaderException($"Windows cannot open '{fileName}' as a video. Use an .mp4 or .mov file.");
			}

			MediaFoundation.Check(hr, "MFCreateSourceReaderFromURL");

			MediaFoundation.Check(MediaFoundation.SetStreamSelection(reader, MediaFoundation.AllStreams, false), "IMFSourceReader.SetStreamSelection");
			if (MediaFoundation.SetStreamSelection(reader, MediaFoundation.FirstVideoStream, true) < 0)
			{
				throw new VideoFrameReaderException($"'{fileName}' has no video in it.");
			}

			MediaFoundation.Check(MediaFoundation.GetNativeMediaType(reader, MediaFoundation.FirstVideoStream, 0, out var nativeType), "IMFSourceReader.GetNativeMediaType");
			Guid codec;
			double framesPerSecond = 0;
			try
			{
				MediaFoundation.GetGuid(nativeType, MediaFoundation.Subtype, out codec);
				MediaFoundation.GetUInt32(nativeType, MediaFoundation.VideoRotation, out uint storedRotation);
				fileRotation = NormalizeRotation(storedRotation);
				MediaFoundation.Check(MediaFoundation.GetUInt64(nativeType, MediaFoundation.FrameSize, out ulong nativeSize), "MF_MT_FRAME_SIZE");
				nativeWidth = (int)(nativeSize >> 32);
				nativeHeight = (int)(uint)nativeSize;
				nativeAperture = ReadAperture(nativeType) ?? new Rectangle(0, 0, nativeWidth, nativeHeight);
				if (MediaFoundation.GetUInt64(nativeType, MediaFoundation.FrameRate, out ulong rate) >= 0 && (uint)rate != 0)
				{
					framesPerSecond = (double)(uint)(rate >> 32) / (uint)rate;
				}
			}
			finally
			{
				MediaFoundation.Release(ref nativeType);
			}

			var outputType = MediaFoundation.CreateMediaType();
			try
			{
				MediaFoundation.SetGuid(outputType, MediaFoundation.MajorType, MediaFoundation.MediaTypeVideo);
				MediaFoundation.SetGuid(outputType, MediaFoundation.Subtype, MediaFoundation.VideoFormatRgb32);

				// This is where a missing codec shows up: the reader cannot find a decoder to reach RGB32.
				int setHr = MediaFoundation.SetCurrentMediaType(reader, MediaFoundation.FirstVideoStream, outputType);
				if (setHr < 0)
				{
					throw new VideoFrameReaderException(SetOutputTypeFailureMessage(setHr, codec, fileName));
				}
			}
			finally
			{
				MediaFoundation.Release(ref outputType);
			}

			ReadOutputFormat();

			MediaFoundation.GetPresentationUInt64(reader, MediaFoundation.PresentationDuration, out ulong duration);
			bool sideways = IsSideways(remainingRotation);
			Info = new VideoInfo(
				TimeSpan.FromTicks((long)duration), // both are 100 ns units
				sideways ? cropHeight : cropWidth,
				sideways ? cropWidth : cropHeight,
				fileRotation,
				framesPerSecond);
		}

		/// <summary>
		/// The user-facing message for IMFSourceReader.SetCurrentMediaType refusing RGB32 output with
		/// <paramref name="hr"/>. Only MF_E_TOPO_CODEC_NOT_FOUND means no decoder is installed, where naming
		/// the extension helps; anything else (MF_E_INVALIDMEDIATYPE says a decoder was found but rejected the
		/// type) would send the user to install something that will not fix it, so it reports the error.
		/// </summary>
		internal static string SetOutputTypeFailureMessage(int hr, Guid codec, string fileName)
		{
			return hr == MediaFoundation.TopoCodecNotFound ? MissingDecoderMessage(codec) : CouldNotReadMessage(fileName, hr);
		}

		private static string CouldNotReadMessage(string fileName, int hr)
		{
			return $"Windows could not read the video '{fileName}' (Media Foundation error 0x{hr:X8}).";
		}

		/// <summary>
		/// The user-facing message for a video whose codec Windows cannot decode. The common ones name the
		/// Store extension that fixes it; phones record HEVC by default, and Windows ships without it.
		/// </summary>
		internal static string MissingDecoderMessage(Guid codec)
		{
			string fourCC = FourCC(codec);
			switch (fourCC.ToUpperInvariant())
			{
				case "HEVC":
				case "H265":
				case "HVC1":
				case "HEV1":
					return "This video uses HEVC (H.265), which Windows cannot decode yet. "
						+ "Install 'HEVC Video Extensions' from the Microsoft Store, then try again.";

				case "VP90":
					return "This video uses VP9, which Windows cannot decode yet. "
						+ "Install 'VP9 Video Extensions' from the Microsoft Store, then try again.";

				case "AV01":
					return "This video uses AV1, which Windows cannot decode yet. "
						+ "Install 'AV1 Video Extension' from the Microsoft Store, then try again.";

				default:
					return $"Windows has no decoder for this video's format ({fourCC}). "
						+ "Convert it to an H.264 .mp4 and try again.";
			}
		}

		// Media Foundation video subtypes are FOURCC-based GUIDs: the code is the first four bytes.
		private static string FourCC(Guid subtype)
		{
			var bytes = subtype.ToByteArray();
			var chars = new char[4];
			for (int i = 0; i < 4; i++)
			{
				chars[i] = bytes[i] >= 0x20 && bytes[i] < 0x7F ? (char)bytes[i] : '?';
			}

			return new string(chars);
		}

		private static int NormalizeRotation(uint degrees)
		{
			int normalized = (int)(degrees % 360);
			return normalized % 90 == 0 ? normalized : 0;
		}

		private void ReadOutputFormat()
		{
			var outputType = MediaFoundation.GetCurrentMediaType(reader, MediaFoundation.FirstVideoStream);
			try
			{
				MediaFoundation.Check(MediaFoundation.GetUInt64(outputType, MediaFoundation.FrameSize, out ulong size), "MF_MT_FRAME_SIZE");
				bufferWidth = (int)(size >> 32);
				bufferHeight = (int)(uint)size;
				int strideHr = MediaFoundation.GetUInt32(outputType, MediaFoundation.DefaultStride, out uint stride);
				defaultStride = ResolveDefaultStride(strideHr, stride, bufferWidth);
				MediaFoundation.GetUInt32(outputType, MediaFoundation.VideoRotation, out uint outputRotation);
				remainingRotation = NormalizeRotation(outputRotation);

				// The output's own aperture when it has one; otherwise the native one. When the video processor
				// has turned the frame on its side it letterboxes the turned picture, centred, into the turned
				// padded size: a Pixel's portrait HEVC is native 1920x1088 with a 1920x1080 aperture at 90
				// degrees, and comes out 1088x1920 with black in columns 0-3 and 1084-1087 (Windows 10 22H2).
				// Its turn is clockwise, as the file asks - checked against the decoder's own unrotated NV12
				// frame, and it matches what CopyPixels makes of basic video processing's unturned output.
				Rectangle crop;
				if (ReadAperture(outputType) is Rectangle outputAperture)
				{
					crop = outputAperture;
				}
				else if (bufferWidth == nativeWidth && bufferHeight == nativeHeight)
				{
					crop = nativeAperture;
				}
				else if (IsSideways(fileRotation) && bufferWidth == nativeHeight && bufferHeight == nativeWidth)
				{
					crop = new Rectangle(
						(bufferWidth - nativeAperture.Height) / 2,
						(bufferHeight - nativeAperture.Width) / 2,
						nativeAperture.Height,
						nativeAperture.Width);
				}
				else
				{
					crop = new Rectangle(0, 0, bufferWidth, bufferHeight);
				}

				(cropX, cropY, cropWidth, cropHeight) = (crop.X, crop.Y, crop.Width, crop.Height);
				if (cropWidth <= 0 || cropHeight <= 0 || cropX < 0 || cropY < 0
					|| cropX + cropWidth > bufferWidth || cropY + cropHeight > bufferHeight)
				{
					cropX = 0;
					cropY = 0;
					cropWidth = bufferWidth;
					cropHeight = bufferHeight;
				}
			}
			finally
			{
				MediaFoundation.Release(ref outputType);
			}
		}

		/// <summary>
		/// The row stride of an RGB32 output <paramref name="width"/> pixels wide, from its MF_MT_DEFAULT_STRIDE
		/// (read with HRESULT <paramref name="hr"/>; a negative stride is stored as its UINT32 bits). When the
		/// type does not carry one, Media Foundation's rule is the one MFGetStrideForBitmapInfoHeader encodes:
		/// uncompressed RGB is bottom-up like a DIB, so the stride is negative - assuming top-down there would
		/// hand back the frame upside down.
		/// </summary>
		internal static int ResolveDefaultStride(int hr, uint attributeValue, int width)
		{
			return hr >= 0 && attributeValue != 0
				? (int)attributeValue
				: MediaFoundation.GetStrideForBitmapInfoHeader(MediaFoundation.Rgb32Format, width);
		}

		private static bool IsSideways(int rotation) => rotation == 90 || rotation == 270;

		/// <summary>MF_MT_MINIMUM_DISPLAY_APERTURE, or null when the type does not set it.</summary>
		private static Rectangle? ReadAperture(IntPtr mediaType)
		{
			// MFVideoArea: two MFOffsets ({ WORD fract; short value }) then a SIZE of two LONGs.
			byte* area = stackalloc byte[16];
			if (MediaFoundation.GetBlob(mediaType, MediaFoundation.MinimumDisplayAperture, area, 16) < 0)
			{
				return null;
			}

			return new Rectangle(*(short*)(area + 2), *(short*)(area + 6), *(int*)(area + 8), *(int*)(area + 12));
		}

		/// <summary>
		/// Decodes the frame on screen at <paramref name="time"/>: the last sample that starts at or before
		/// it, or the video's last frame for a time past the end.
		/// </summary>
		public ImageBuffer ReadFrameAt(TimeSpan time, CancellationToken cancellationToken)
		{
			try
			{
				return ReadFrameAtCore(time, cancellationToken);
			}
			catch (COMException e)
			{
				throw Unexpected(e);
			}
		}

		private ImageBuffer ReadFrameAtCore(TimeSpan time, CancellationToken cancellationToken)
		{
			long target = Math.Max(0, time.Ticks);

			// A seek past the end yields end-of-stream and no sample at all; seeking just inside instead lands
			// on the last key frame and reads forward to the true last frame.
			if (Info.Duration > TimeSpan.Zero && target >= Info.Duration.Ticks)
			{
				target = Info.Duration.Ticks - 1;
			}

			// Two times inside one frame's display interval get that frame twice, without decoding again.
			if (current != IntPtr.Zero && target >= currentStart && target < currentEnd)
			{
				return CopyFrame(current);
			}

			// A seek goes back to the key frame before the target and decodes forward from there - for a
			// phone's 1080p HEVC that measured 0.5 to 10 s a frame, growing through the clip. When the target
			// is a little ahead of the frame last decoded, keep reading forward instead: the loop below stops
			// on the same frame either way, the first whose display interval reaches the target.
			bool readOn = canReadOn && current != IntPtr.Zero && target >= currentEnd && target - currentEnd <= ReadOnWindow.Ticks;
			if (!readOn)
			{
				MediaFoundation.Release(ref current);
				canReadOn = false;
				MediaFoundation.SetCurrentPosition(reader, target);
				canReadOn = true;
			}

			try
			{
				while (true)
				{
					cancellationToken.ThrowIfCancellationRequested();
					MediaFoundation.ReadSample(reader, MediaFoundation.FirstVideoStream, out uint flags, out long timestamp, out IntPtr sample);
					if ((flags & MediaFoundation.ReaderFlagError) != 0)
					{
						MediaFoundation.Release(ref sample);
						throw new VideoFrameReaderException($"Windows hit an error while decoding '{fileName}'. The file may be damaged.");
					}

					if ((flags & MediaFoundation.ReaderFlagCurrentMediaTypeChanged) != 0)
					{
						ReadOutputFormat();
					}

					if (sample != IntPtr.Zero)
					{
						MediaFoundation.Release(ref current);
						current = sample;
						currentStart = timestamp;
						currentEnd = timestamp + Math.Max(1, MediaFoundation.GetSampleDuration(sample));

						// Decode forward until the frame whose display interval covers the target.
						if (currentEnd > target)
						{
							break;
						}
					}

					if ((flags & MediaFoundation.ReaderFlagEndOfStream) != 0)
					{
						// Nothing follows, so the next time has to seek.
						canReadOn = false;
						break;
					}
				}
			}
			catch
			{
				// Cancelled or failed part way: where the reader sits is no longer known.
				canReadOn = false;
				throw;
			}

			if (current == IntPtr.Zero)
			{
				throw new VideoFrameReaderException($"Could not read a frame at {time} from '{fileName}'.");
			}

			return CopyFrame(current);
		}

		private ImageBuffer CopyFrame(IntPtr sample)
		{
			var buffer = MediaFoundation.ConvertToContiguousBuffer(sample);
			try
			{
				// Prefer IMF2DBuffer: it reports the real pitch and where the top row is, whichever way up the
				// memory is. The plain buffer falls back on the media type's default stride, whose sign says
				// the same thing (negative is bottom-up).
				if (MediaFoundation.QueryInterface(buffer, MediaFoundation.IidMF2DBuffer, out var buffer2D) >= 0)
				{
					try
					{
						byte* top = MediaFoundation.Lock2D(buffer2D, out int pitch);
						try
						{
							return CopyPixels(top, pitch);
						}
						finally
						{
							MediaFoundation.Unlock2D(buffer2D);
						}
					}
					finally
					{
						MediaFoundation.Release(ref buffer2D);
					}
				}

				byte* data = MediaFoundation.Lock(buffer);
				try
				{
					byte* topRow = defaultStride < 0 ? data + (long)-defaultStride * (bufferHeight - 1) : data;
					return CopyPixels(topRow, defaultStride);
				}
				finally
				{
					MediaFoundation.Unlock(buffer);
				}
			}
			finally
			{
				MediaFoundation.Release(ref buffer);
			}
		}

		/// <summary>
		/// Copies the display aperture out of an RGB32 frame (top row at <paramref name="top"/>, rows
		/// <paramref name="pitch"/> bytes apart) into a new agg image, turned upright by
		/// <see cref="remainingRotation"/> degrees clockwise. RGB32 is B, G, R, X in memory, which is agg's
		/// BGRA once X - which the video processor leaves as 0 - is made opaque.
		/// </summary>
		private ImageBuffer CopyPixels(byte* top, long pitch)
		{
			bool sideways = IsSideways(remainingRotation);
			int width = sideways ? cropHeight : cropWidth;
			int height = sideways ? cropWidth : cropHeight;
			var image = new ImageBuffer(width, height);
			byte[] destination = image.GetBuffer();
			byte* origin = top + (cropY * pitch) + (cropX * 4L);

			fixed (byte* destinationStart = destination)
			{
				for (int displayY = 0; displayY < height; displayY++)
				{
					// agg's y runs up from the bottom, so the top display row is agg row height - 1.
					byte* output = destinationStart + image.GetBufferOffsetXY(0, height - 1 - displayY);
					for (int displayX = 0; displayX < width; displayX++)
					{
						int storedX, storedY;
						switch (remainingRotation)
						{
							case 90:
								storedX = displayY;
								storedY = cropHeight - 1 - displayX;
								break;

							case 180:
								storedX = cropWidth - 1 - displayX;
								storedY = cropHeight - 1 - displayY;
								break;

							case 270:
								storedX = cropWidth - 1 - displayY;
								storedY = displayX;
								break;

							default:
								storedX = displayX;
								storedY = displayY;
								break;
						}

						byte* input = origin + (storedY * pitch) + (storedX * 4L);
						output[0] = input[0];
						output[1] = input[1];
						output[2] = input[2];
						output[3] = 255;
						output += 4;
					}
				}
			}

			return image;
		}

		private VideoFrameReaderException Unexpected(COMException e)
		{
			return new VideoFrameReaderException(CouldNotReadMessage(fileName, e.HResult), e);
		}

		public void Dispose()
		{
			MediaFoundation.Release(ref current);
			MediaFoundation.Release(ref reader);
			if (started)
			{
				started = false;
				MediaFoundation.Shutdown();
			}
		}
	}
}
