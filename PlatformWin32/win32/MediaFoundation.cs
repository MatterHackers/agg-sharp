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
using System.Runtime.InteropServices;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// The slice of Media Foundation (mfplat.dll, mfreadwrite.dll - both part of Windows) that
	/// <see cref="MediaFoundationVideoFrameReader"/> uses, called through raw vtable slots.
	/// </summary>
	/// <remarks>
	/// Raw slots rather than <c>[ComImport]</c> interfaces: the reader only touches a dozen methods, and
	/// declaring an interface means declaring every method ahead of the one you want (IMFSample alone
	/// inherits thirty from IMFAttributes). Raw pointers also sidestep runtime-callable wrappers and their
	/// apartment rules, so a reader can move between thread-pool threads across awaits. Slot numbers
	/// count the three IUnknown methods first and follow the declaration order in mfobjects.h and
	/// mfreadwrite.h.
	/// </remarks>
	internal static unsafe class MediaFoundation
	{
		// MF_VERSION for the Windows 7+ SDK: MF_SDK_VERSION 0x0002 in the high word, MF_API_VERSION 0x0070 low.
		private const uint MfVersion = 0x00020070;

		public const uint FirstVideoStream = 0xFFFFFFFC; // MF_SOURCE_READER_FIRST_VIDEO_STREAM
		public const uint AllStreams = 0xFFFFFFFE; // MF_SOURCE_READER_ALL_STREAMS
		public const uint MediaSource = 0xFFFFFFFF; // MF_SOURCE_READER_MEDIASOURCE

		// MF_SOURCE_READER_FLAG
		public const uint ReaderFlagError = 0x1;
		public const uint ReaderFlagEndOfStream = 0x2;
		public const uint ReaderFlagCurrentMediaTypeChanged = 0x20;

		public const int FileNotFound = unchecked((int)0x80070002); // HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)
		public const int PathNotFound = unchecked((int)0x80070003);
		public const int UnsupportedByteStreamType = unchecked((int)0xC00D36C4);
		public const int InvalidStreamNumber = unchecked((int)0xC00D36B3);

		/// <summary>MF_E_TOPO_CODEC_NOT_FOUND: IMFSourceReader.SetCurrentMediaType's answer when no decoder is
		/// installed for the stream's native type. (MF_E_INVALIDMEDIATYPE is a different case: a decoder was
		/// found but rejected the requested output.)</summary>
		public const int TopoCodecNotFound = unchecked((int)0xC00D5212);

		// D3DFMT_X8R8G8B8, the FOURCC-position code (Data1) of MFVideoFormat_RGB32.
		public const uint Rgb32Format = 22;

		public static readonly Guid IidMF2DBuffer = new Guid("7DC9D5F9-9ED9-44ec-9BBF-0600BB589FBB");

		public static readonly Guid EnableAdvancedVideoProcessing = new Guid("0f81da2c-b537-4672-a8b2-a681b17307a3");
		public static readonly Guid MajorType = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
		public static readonly Guid Subtype = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
		public static readonly Guid FrameSize = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
		public static readonly Guid FrameRate = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
		public static readonly Guid DefaultStride = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
		public static readonly Guid MinimumDisplayAperture = new Guid("d7388766-18fe-48c6-a177-ee894867c8c4");
		public static readonly Guid VideoRotation = new Guid("c380465d-2271-428c-9b83-ecea3b4a85c1");
		public static readonly Guid PresentationDuration = new Guid("6c990d33-bb8e-477a-8598-0d5d96fcd88a");

		public static readonly Guid MediaTypeVideo = new Guid("73646976-0000-0010-8000-00AA00389B71");
		public static readonly Guid VideoFormatRgb32 = new Guid("00000016-0000-0010-8000-00AA00389B71");

		[DllImport("mfplat.dll")]
		private static extern int MFStartup(uint version, uint flags);

		[DllImport("mfplat.dll")]
		private static extern int MFShutdown();

		[DllImport("mfplat.dll")]
		private static extern int MFCreateAttributes(out IntPtr attributes, uint initialSize);

		[DllImport("mfplat.dll")]
		private static extern int MFCreateMediaType(out IntPtr mediaType);

		[DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)]
		private static extern int MFCreateSourceReaderFromURL(string url, IntPtr attributes, out IntPtr reader);

		[DllImport("mfplat.dll")]
		private static extern int MFGetStrideForBitmapInfoHeader(uint format, uint width, out int stride);

		/// <summary>Media Foundation's default stride for an uncompressed <paramref name="format"/> (a D3DFORMAT
		/// or FOURCC code) that is <paramref name="width"/> pixels wide - negative, meaning bottom-up, for RGB
		/// (RGB32 at 64 wide gives -256), positive for YUV.</summary>
		public static int GetStrideForBitmapInfoHeader(uint format, int width)
		{
			Check(MFGetStrideForBitmapInfoHeader(format, (uint)width, out int stride), "MFGetStrideForBitmapInfoHeader");
			return stride;
		}

		/// <summary>MFStartup is reference counted per process, so each successful call is paired with one
		/// <see cref="Shutdown"/>.</summary>
		public static void Startup() => Check(MFStartup(MfVersion, 0), "MFStartup");

		public static void Shutdown() => MFShutdown();

		public static IntPtr CreateAttributes()
		{
			Check(MFCreateAttributes(out var attributes, 1), "MFCreateAttributes");
			return attributes;
		}

		public static IntPtr CreateMediaType()
		{
			Check(MFCreateMediaType(out var mediaType), "MFCreateMediaType");
			return mediaType;
		}

		/// <summary>Returns the HRESULT rather than throwing: the caller turns the likely failures into
		/// messages a user can act on.</summary>
		public static int CreateSourceReader(string path, IntPtr attributes, out IntPtr reader)
			=> MFCreateSourceReaderFromURL(path, attributes, out reader);

		public static void Check(int hr, string what)
		{
			if (hr < 0)
			{
				throw new COMException(what + " failed", hr);
			}
		}

		private static void* Slot(IntPtr comObject, int slot) => (*(void***)comObject)[slot];

		public static void Release(ref IntPtr comObject)
		{
			if (comObject != IntPtr.Zero)
			{
				Marshal.Release(comObject);
				comObject = IntPtr.Zero;
			}
		}

		// IUnknown
		public static int QueryInterface(IntPtr unknown, Guid iid, out IntPtr result)
		{
			IntPtr found;
			int hr = ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)Slot(unknown, 0))(unknown, &iid, &found);
			result = hr < 0 ? IntPtr.Zero : found;
			return hr;
		}

		// IMFAttributes - also the first thirty methods of IMFMediaType and IMFSample.
		public static int GetUInt32(IntPtr attributes, Guid key, out uint value)
		{
			uint v;
			int hr = ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, uint*, int>)Slot(attributes, 7))(attributes, &key, &v);
			value = hr < 0 ? 0 : v;
			return hr;
		}

		public static int GetUInt64(IntPtr attributes, Guid key, out ulong value)
		{
			ulong v;
			int hr = ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, ulong*, int>)Slot(attributes, 8))(attributes, &key, &v);
			value = hr < 0 ? 0 : v;
			return hr;
		}

		public static int GetGuid(IntPtr attributes, Guid key, out Guid value)
		{
			Guid v;
			int hr = ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, Guid*, int>)Slot(attributes, 10))(attributes, &key, &v);
			value = hr < 0 ? Guid.Empty : v;
			return hr;
		}

		public static int GetBlob(IntPtr attributes, Guid key, byte* buffer, uint bufferSize)
		{
			uint written;
			return ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, byte*, uint, uint*, int>)Slot(attributes, 15))(attributes, &key, buffer, bufferSize, &written);
		}

		public static void SetUInt32(IntPtr attributes, Guid key, uint value)
			=> Check(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, uint, int>)Slot(attributes, 21))(attributes, &key, value), "IMFAttributes.SetUINT32");

		public static void SetGuid(IntPtr attributes, Guid key, Guid value)
			=> Check(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, Guid*, int>)Slot(attributes, 24))(attributes, &key, &value), "IMFAttributes.SetGUID");

		// IMFSourceReader
		public static int SetStreamSelection(IntPtr reader, uint stream, bool selected)
			=> ((delegate* unmanaged[Stdcall]<IntPtr, uint, int, int>)Slot(reader, 4))(reader, stream, selected ? 1 : 0);

		public static int GetNativeMediaType(IntPtr reader, uint stream, uint typeIndex, out IntPtr mediaType)
		{
			IntPtr v;
			int hr = ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, int>)Slot(reader, 5))(reader, stream, typeIndex, &v);
			mediaType = hr < 0 ? IntPtr.Zero : v;
			return hr;
		}

		public static IntPtr GetCurrentMediaType(IntPtr reader, uint stream)
		{
			IntPtr v;
			Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)Slot(reader, 6))(reader, stream, &v), "IMFSourceReader.GetCurrentMediaType");
			return v;
		}

		public static int SetCurrentMediaType(IntPtr reader, uint stream, IntPtr mediaType)
			=> ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint*, IntPtr, int>)Slot(reader, 7))(reader, stream, null, mediaType);

		/// <summary>Seeks to <paramref name="position100ns"/>. The reader lands on the key frame at or before
		/// it, so the samples that follow can start earlier than asked.</summary>
		public static void SetCurrentPosition(IntPtr reader, long position100ns)
		{
			var timeFormat = Guid.Empty; // GUID_NULL: 100-nanosecond units
			var position = new PropVariant { VarType = VarTypeI8, Value = position100ns };
			Check(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, PropVariant*, int>)Slot(reader, 8))(reader, &timeFormat, &position), "IMFSourceReader.SetCurrentPosition");
		}

		/// <summary>Reads the next sample synchronously. <paramref name="sample"/> can be zero even on success
		/// (end of stream, a stream tick), and the caller owns a reference when it is not.</summary>
		public static void ReadSample(IntPtr reader, uint stream, out uint flags, out long timestamp, out IntPtr sample)
		{
			uint actualStream, f;
			long t;
			IntPtr s;
			Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint*, uint*, long*, IntPtr*, int>)Slot(reader, 9))(reader, stream, 0, &actualStream, &f, &t, &s), "IMFSourceReader.ReadSample");
			flags = f;
			timestamp = t;
			sample = s;
		}

		/// <summary>The media source's value for <paramref name="key"/> as a 64-bit integer. VT_UI8 and VT_I8
		/// carry no allocation, so there is nothing for PropVariantClear to free.</summary>
		public static int GetPresentationUInt64(IntPtr reader, Guid key, out ulong value)
		{
			var result = default(PropVariant);
			int hr = ((delegate* unmanaged[Stdcall]<IntPtr, uint, Guid*, PropVariant*, int>)Slot(reader, 12))(reader, MediaSource, &key, &result);
			value = hr >= 0 && (result.VarType == VarTypeUI8 || result.VarType == VarTypeI8) ? (ulong)result.Value : 0;
			return hr;
		}

		// IMFSample
		public static long GetSampleDuration(IntPtr sample)
		{
			long v;
			return ((delegate* unmanaged[Stdcall]<IntPtr, long*, int>)Slot(sample, 37))(sample, &v) < 0 ? 0 : v;
		}

		public static IntPtr ConvertToContiguousBuffer(IntPtr sample)
		{
			IntPtr v;
			Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(sample, 41))(sample, &v), "IMFSample.ConvertToContiguousBuffer");
			return v;
		}

		// IMFMediaBuffer
		public static byte* Lock(IntPtr buffer)
		{
			byte* data;
			uint maxLength, currentLength;
			Check(((delegate* unmanaged[Stdcall]<IntPtr, byte**, uint*, uint*, int>)Slot(buffer, 3))(buffer, &data, &maxLength, &currentLength), "IMFMediaBuffer.Lock");
			return data;
		}

		public static void Unlock(IntPtr buffer) => ((delegate* unmanaged[Stdcall]<IntPtr, int>)Slot(buffer, 4))(buffer);

		// IMF2DBuffer
		/// <summary>Locks a 2D buffer and returns its top row and signed pitch - negative for bottom-up
		/// memory - which is exactly what copying rows in display order needs.</summary>
		public static byte* Lock2D(IntPtr buffer2D, out int pitch)
		{
			byte* scanline0;
			int p;
			Check(((delegate* unmanaged[Stdcall]<IntPtr, byte**, int*, int>)Slot(buffer2D, 3))(buffer2D, &scanline0, &p), "IMF2DBuffer.Lock2D");
			pitch = p;
			return scanline0;
		}

		public static void Unlock2D(IntPtr buffer2D) => ((delegate* unmanaged[Stdcall]<IntPtr, int>)Slot(buffer2D, 4))(buffer2D);

		private const ushort VarTypeI8 = 20;
		private const ushort VarTypeUI8 = 21;

		/// <summary>PROPVARIANT, only as far as its integer members: a 2-byte type tag, 6 reserved bytes and a
		/// union whose largest members (BLOB, the CA* counted arrays) are 16 bytes on x64.</summary>
		[StructLayout(LayoutKind.Explicit, Size = 24)]
		private struct PropVariant
		{
			[FieldOffset(0)]
			public ushort VarType;

			[FieldOffset(8)]
			public long Value;
		}
	}
}
