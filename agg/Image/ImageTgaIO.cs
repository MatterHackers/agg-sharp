using System;
using System.IO;
using System.Text;

namespace MatterHackers.Agg.Image
{
	public static class ImageTgaIO
	{
		// Header of a TGA file
		public struct STargaHeader
		{
			public byte PostHeaderSkip;
			public byte ColorMapType;		// 0 = RGB, 1 = Palette
			public byte ImageType;			// 1 = Palette, 2 = RGB, 3 = mono, 9 = RLE Palette, 10 = RLE RGB, 11 RLE mono
			public ushort ColorMapStart;
			public ushort ColorMapLength;
			public byte ColorMapBits;
			public ushort XStart;				// offsets the image would like to have (ignored)
			public ushort YStart;				// offsets the image would like to have (ignored)
			public ushort Width;
			public ushort Height;
			public byte BPP;				// bit depth of the image
			public byte Descriptor;

			public void BinaryWrite(BinaryWriter writerToWriteTo)
			{
				writerToWriteTo.Write(PostHeaderSkip);
				writerToWriteTo.Write(ColorMapType);
				writerToWriteTo.Write(ImageType);
				writerToWriteTo.Write(ColorMapStart);
				writerToWriteTo.Write(ColorMapLength);
				writerToWriteTo.Write(ColorMapBits);
				writerToWriteTo.Write(XStart);
				writerToWriteTo.Write(YStart);
				writerToWriteTo.Write(Width);
				writerToWriteTo.Write(Height);
				writerToWriteTo.Write(BPP);
				writerToWriteTo.Write(Descriptor);
			}
		};

		private const int TargaHeaderSize = 18;
		private const int RGB_BLUE = 2;
		private const int RGB_GREEN = 1;
		private const int RGB_RED = 0;
		private const int RGBA_ALPHA = 3;

		// these are used during loading (only valid during load)
		private static int TGABytesPerLine;

		private static void Do24To8Bit(byte[] Dest, byte[] Source, int SourceOffset, int Width, int Height)
		{
			throw new System.NotImplementedException();
#if false

	        int i;
	        if (Width)
	        {
		        i = 0;
		        Dest = &Dest[Height*Width];
		        do
		        {
			        if(p[RGB_RED] == 0 && p[RGB_GREEN] == 0 && p[RGB_BLUE] == 0)
			        {
				        Dest[i] = 0;
			        }
			        else
			        {
				        // no other color can map to color 0
				        Dest[i] =(byte) pStaticRemap->GetColorIndex(p[RGB_RED], p[RGB_GREEN], p[RGB_BLUE], 1);
			        }
			        p += 3;
		        } while (++i<Width);
	        }
#endif
		}

		private static void Do32To8Bit(byte[] Dest, byte[] Source, int SourceOffset, int Width, int Height)
		{
			throw new System.NotImplementedException();

#if false
	        int i;
	        if (Width)
	        {
		        i = 0;
		        Dest = &Dest[Height*Width];
		        do
		        {
			        if(p[RGB_RED] == 0 && p[RGB_GREEN] == 0 && p[RGB_BLUE] == 0)
			        {
				        Dest[i] = 0;
			        }
			        else
			        {
				        // no other color can map to color 0
				        Dest[i] = (byte)pStaticRemap->GetColorIndex(p[RGB_RED], p[RGB_GREEN], p[RGB_BLUE], 1);
			        }
			        p += 4;
		        } while (++i < Width);
	        }
#endif
		}

		private static unsafe void Do24To24Bit(byte[] Dest, byte[] Source, int SourceOffset, int Width, int Height)
		{
			if (Width > 0)
			{
				int destOffset = Height * Width * 3;
				for (int i = 0; i < Width * 3; i++)
				{
					Dest[destOffset + i] = Source[SourceOffset + i];
				}
			}
		}

		private static unsafe void Do32To24Bit(byte[] Dest, byte[] Source, int SourceOffset, int Width, int Height)
		{
			if (Width > 0)
			{
				int i = 0;
				int destOffest = Height * Width * 3;
				do
				{
					Dest[destOffest + i * 3 + RGB_BLUE] = Source[SourceOffset + RGB_BLUE];
					Dest[destOffest + i * 3 + RGB_GREEN] = Source[SourceOffset + RGB_GREEN];
					Dest[destOffest + i * 3 + RGB_RED] = Source[SourceOffset + RGB_RED];
					SourceOffset += 4;
				} while (++i < Width);
			}
		}

		private static unsafe void Do24To32Bit(byte[] Dest, byte[] Source, int SourceOffset, int Width, int Height)
		{
			if (Width > 0)
			{
				int i = 0;
				int destOffest = Height * Width * 4;
				do
				{
					Dest[destOffest + i * 4 + RGB_BLUE] = Source[SourceOffset + RGB_BLUE];
					Dest[destOffest + i * 4 + RGB_GREEN] = Source[SourceOffset + RGB_GREEN];
					Dest[destOffest + i * 4 + RGB_RED] = Source[SourceOffset + RGB_RED];
					Dest[destOffest + i * 4 + 3] = 255;
					SourceOffset += 3;
				} while (++i < Width);
			}
		}

		private static unsafe void Do32To32Bit(byte[] Dest, byte[] Source, int SourceOffset, int Width, int Height)
		{
			if (Width > 0)
			{
				int i = 0;
				int destOffest = Height * Width * 4;
				do
				{
					Dest[destOffest + RGB_BLUE] = Source[SourceOffset + RGB_BLUE];
					Dest[destOffest + RGB_GREEN] = Source[SourceOffset + RGB_GREEN];
					Dest[destOffest + RGB_RED] = Source[SourceOffset + RGB_RED];
					Dest[destOffest + RGBA_ALPHA] = Source[SourceOffset + RGBA_ALPHA];
					SourceOffset += 4;
					destOffest += 4;
				} while (++i < Width);
			}
		}

		private static bool ReadTGAInfo(byte[] WorkPtr, out STargaHeader TargaHeader)
		{
			TargaHeader.PostHeaderSkip = WorkPtr[0];
			TargaHeader.ColorMapType = WorkPtr[1];
			TargaHeader.ImageType = WorkPtr[2];
			TargaHeader.ColorMapStart = BitConverter.ToUInt16(WorkPtr, 3);
			TargaHeader.ColorMapLength = BitConverter.ToUInt16(WorkPtr, 5);
			TargaHeader.ColorMapBits = WorkPtr[7];
			TargaHeader.XStart = BitConverter.ToUInt16(WorkPtr, 8);
			TargaHeader.YStart = BitConverter.ToUInt16(WorkPtr, 10);
			TargaHeader.Width = BitConverter.ToUInt16(WorkPtr, 12);
			TargaHeader.Height = BitConverter.ToUInt16(WorkPtr, 14);
			TargaHeader.BPP = WorkPtr[16];
			TargaHeader.Descriptor = WorkPtr[17];

			// check the header
			if (TargaHeader.ColorMapType != 0 ||	// 0 = RGB, 1 = Palette
				// 1 = Palette, 2 = RGB, 3 = mono, 9 = RLE Palette, 10 = RLE RGB, 11 RLE mono
				(TargaHeader.ImageType != 2 && TargaHeader.ImageType != 10 && TargaHeader.ImageType != 9) ||
				(TargaHeader.BPP != 24 && TargaHeader.BPP != 32))
			{
#if DEBUG
				throw new NotImplementedException("Unsupported TGA mode");
#endif
#if ASSERTS_ENABLED
		        if ( ((byte*)pTargaHeader)[0] == 'B' && ((byte*)pTargaHeader)[1] == 'M' )
		        {
			        assert(!"This TGA's header looks like a BMP!"); //  look at the first two bytes and see if they are 'BM'
			        // if so it's a BMP not a TGA
		        }
		        else
		        {
			        byte * pColorMapType = NULL;
			        switch (TargaHeader.ColorMapType)
			        {
				        case 0:
					        pColorMapType = "RGB Color Map";
					        break;

				        case 1:
					        pColorMapType = "Palette Color Map";
					        break;

				        default:
					        pColorMapType = "<Illegal Color Map>";
					        break;
			        }
			        byte * pImageType = NULL;
			        switch (TargaHeader.ImageType)
			        {
				        case 1:
					        pImageType = "Palette Image Type";
					        break;

				        case 2:
					        pImageType = "RGB Image Type";
					        break;

				        case 3:
					        pImageType = "mono Image Type";
					        break;

				        case 9:
					        pImageType = "RLE Palette Image Type";
					        break;

				        case 10:
					        pImageType = "RLE RGB Image Type";
					        break;

				        case 11:
					        pImageType = "RLE mono Image Type";
					        break;

				        default:
					        pImageType = "<Illegal Image Type>";
					        break;
			        }
			        int ColorDepth = TargaHeader.BPP;
			        CJString ErrorString;
			        ErrorString.Format( "Image type %s %s (%u bpp) not supported!", pColorMapType, pImageType, ColorDepth);
			        ShowSystemMessage("TGA File IO Error", ErrorString.GetBytePtr(), "TGA Error");
		        }
#endif // ASSERTS_ENABLED
				return false;
			}

			return true;
		}

		private const int IS_PIXLE_RUN = 0x80;
		private const int RUN_LENGTH_MASK = 0x7f;

		private static unsafe int Decompress(byte[] pDecompressBits, byte[] pBitsToPars, int ParsOffset, int Width, int Depth, int LineBeingRead)
		{
			int decompressOffset = 0;
			int total = 0;
			do
			{
				int i;
				int numPixels = (pBitsToPars[ParsOffset] & RUN_LENGTH_MASK) + 1;
				total += numPixels;
				if ((pBitsToPars[ParsOffset++] & IS_PIXLE_RUN) != 0)
				{
					// decompress the run for NumPixels
					byte r, g, b, a;
					b = pBitsToPars[ParsOffset++];
					g = pBitsToPars[ParsOffset++];
					r = pBitsToPars[ParsOffset++];
					switch (Depth)
					{
						case 24:
							for (i = 0; i < numPixels; i++)
							{
								pDecompressBits[decompressOffset++] = b;
								pDecompressBits[decompressOffset++] = g;
								pDecompressBits[decompressOffset++] = r;
							}
							break;

						case 32:
							a = pBitsToPars[ParsOffset++];
							for (i = 0; i < numPixels; i++)
							{
								pDecompressBits[decompressOffset++] = b;
								pDecompressBits[decompressOffset++] = g;
								pDecompressBits[decompressOffset++] = r;
								pDecompressBits[decompressOffset++] = a;
							}
							break;

						default:
							throw new System.Exception("Bad bit depth.");
					}
				}
				else // store NumPixels normally
				{
					switch (Depth)
					{
						case 24:
							for (i = 0; i < numPixels * 3; i++)
							{
								pDecompressBits[decompressOffset++] = pBitsToPars[ParsOffset++];
							}
							break;

						case 32:
							for (i = 0; i < numPixels * 4; i++)
							{
								pDecompressBits[decompressOffset++] = pBitsToPars[ParsOffset++];
							}
							break;

						default:
							throw new System.Exception("Bad bit depth.");
					}
				}
			} while (total < Width);

			if (total > Width)
			{
				throw new System.Exception("The TGA you loaded is corrupt (line " + LineBeingRead.ToString() + ").");
			}

			return ParsOffset;
		}

		private static unsafe int LowLevelReadTGABitsFromBuffer(ImageBuffer imageToReadTo, byte[] wholeFileBuffer, int DestBitDepth)
		{
			STargaHeader targaHeader = new STargaHeader();
			int fileReadOffset;

			if (!ReadTGAInfo(wholeFileBuffer, out targaHeader))
			{
				return 0;
			}

			// if the frame we are loading is different then the one we have allocated
			// or we don't have any bits allocated

			if ((imageToReadTo.Width * imageToReadTo.Height) != (targaHeader.Width * targaHeader.Height))
			{
				imageToReadTo.Allocate(targaHeader.Width, targaHeader.Height, targaHeader.Width * DestBitDepth / 8, DestBitDepth);
			}

			// work out the line width
			switch (imageToReadTo.BitDepth)
			{
				case 24:
					TGABytesPerLine = imageToReadTo.Width * 3;
					if (imageToReadTo.GetRecieveBlender() == null)
					{
						imageToReadTo.SetRecieveBlender(new BlenderBGR());
					}
					break;

				case 32:
					TGABytesPerLine = imageToReadTo.Width * 4;
					if (imageToReadTo.GetRecieveBlender() == null)
					{
						imageToReadTo.SetRecieveBlender(new BlenderBGRA());
					}
					break;

				default:
					throw new System.Exception("Bad bit depth.");
			}

			if (TGABytesPerLine > 0)
			{
				byte[] bufferToDecompressTo = null;
				fileReadOffset = TargaHeaderSize + targaHeader.PostHeaderSkip;

				if (targaHeader.ImageType == 10) // 10 is RLE compressed
				{
					bufferToDecompressTo = new byte[TGABytesPerLine * 2];
				}

				// read all the lines *
				for (int i = 0; i < imageToReadTo.Height; i++)
				{
					byte[] bufferToCopyFrom;
					int copyOffset = 0;

					int curReadLine;

					// bit 5 tells us if the image is stored top to bottom or bottom to top
					if ((targaHeader.Descriptor & 0x20) != 0)
					{
						// bottom to top
						curReadLine = imageToReadTo.Height - i - 1;
					}
					else
					{
						// top to bottom
						curReadLine = i;
					}

					if (targaHeader.ImageType == 10) // 10 is RLE compressed
					{
						fileReadOffset = Decompress(bufferToDecompressTo, wholeFileBuffer, fileReadOffset, imageToReadTo.Width, targaHeader.BPP, curReadLine);
						bufferToCopyFrom = bufferToDecompressTo;
					}
					else
					{
						bufferToCopyFrom = wholeFileBuffer;
						copyOffset = fileReadOffset;
					}

					int bufferOffset;
					byte[] imageBuffer = imageToReadTo.GetBuffer(out bufferOffset);

					switch (imageToReadTo.BitDepth)
					{
						case 8:
							switch (targaHeader.BPP)
							{
								case 24:
									Do24To8Bit(imageBuffer, bufferToCopyFrom, copyOffset, imageToReadTo.Width, curReadLine);
									break;

								case 32:
									Do32To8Bit(imageBuffer, bufferToCopyFrom, copyOffset, imageToReadTo.Width, curReadLine);
									break;
							}
							break;

						case 24:
							switch (targaHeader.BPP)
							{
								case 24:
									Do24To24Bit(imageBuffer, bufferToCopyFrom, copyOffset, imageToReadTo.Width, curReadLine);
									break;

								case 32:
									Do32To24Bit(imageBuffer, bufferToCopyFrom, copyOffset, imageToReadTo.Width, curReadLine);
									break;
							}
							break;

						case 32:
							switch (targaHeader.BPP)
							{
								case 24:
									Do24To32Bit(imageBuffer, bufferToCopyFrom, copyOffset, imageToReadTo.Width, curReadLine);
									break;

								case 32:
									Do32To32Bit(imageBuffer, bufferToCopyFrom, copyOffset, imageToReadTo.Width, curReadLine);
									break;
							}
							break;

						default:
							throw new System.Exception("Bad bit depth");
					}

					if (targaHeader.ImageType != 10) // 10 is RLE compressed
					{
						fileReadOffset += TGABytesPerLine;
					}
				}
			}

			return targaHeader.Width;
		}

		private const int MAX_RUN_LENGTH = 127;

		private static int memcmp(byte[] pCheck, int CheckOffset, byte[] pSource, int SourceOffset, int Width)
		{
			for (int i = 0; i < Width; i++)
			{
				if (pCheck[CheckOffset + i] < pSource[SourceOffset + i])
				{
					return -1;
				}
				if (pCheck[CheckOffset + i] > pSource[SourceOffset + i])
				{
					return 1;
				}
			}

			return 0;
		}

		private static int GetSameLength(byte[] checkBufer, int checkOffset, byte[] sourceBuffer, int sourceOffsetToNextPixel, int numBytesInPixel, int maxSameLengthWidth)
		{
			int count = 0;
			while (memcmp(checkBufer, checkOffset, sourceBuffer, sourceOffsetToNextPixel, numBytesInPixel) == 0 && count < maxSameLengthWidth)
			{
				count++;
				sourceOffsetToNextPixel += numBytesInPixel;
			}

			return count;
		}

		private static int GetDifLength(byte[] pCheck, byte[] pSource, int SourceOffset, int numBytesInPixel, int Max)
		{
			int count = 0;
			while (memcmp(pCheck, 0, pSource, SourceOffset, numBytesInPixel) != 0 && count < Max)
			{
				count++;
				for (int i = 0; i < numBytesInPixel; i++)
				{
					pCheck[i] = pSource[SourceOffset + i];
				}
				SourceOffset += numBytesInPixel;
			}

			return count;
		}

		private const int MIN_RUN_LENGTH = 2;

		private static int CompressLine8(byte[] destBuffer, byte[] sourceBuffer, int sourceOffset, int Width)
		{
			int writePos = 0;
			int pixelsProcessed = 0;

			while (pixelsProcessed < Width)
			{
				// always get as many as you can that are the same first
				int max = System.Math.Min(MAX_RUN_LENGTH, (Width - 1) - pixelsProcessed);
				int sameLength = GetSameLength(sourceBuffer, sourceOffset, sourceBuffer, sourceOffset + 1, 1, max);
				if (sameLength >= MIN_RUN_LENGTH)
				//if(SameLength)
				{
					// write in the count
					if (sameLength > MAX_RUN_LENGTH)
					{
						throw new System.Exception("Bad Length");
					}
					destBuffer[writePos++] = (byte)((sameLength) | IS_PIXLE_RUN);

					// write in the same length pixel value
					destBuffer[writePos++] = sourceBuffer[sourceOffset];

					pixelsProcessed += sameLength + 1;
				}
				else
				{
					byte checkPixel = sourceBuffer[sourceOffset];
					int difLength = max;

					if (difLength == 0)
					{
						difLength = 1;
					}
					// write in the count (if there is only one the count is 0)
					if (difLength > MAX_RUN_LENGTH)
					{
						throw new System.Exception("Bad Length");
					}

					destBuffer[writePos++] = (byte)(difLength - 1);

					while (difLength-- != 0)
					{
						// write in the same length pixel value
						destBuffer[writePos++] = sourceBuffer[sourceOffset++];
						pixelsProcessed++;
					}
				}
			}

			return writePos;
		}

		private static byte[] differenceHold = new byte[4];

		private static int CompressLine24(byte[] destBuffer, byte[] sourceBuffer, int sourceOffset, int Width)
		{
			int writePos = 0;
			int pixelsProcessed = 0;

			while (pixelsProcessed < Width)
			{
				// always get as many as you can that are the same first
				int max = System.Math.Min(MAX_RUN_LENGTH, (Width - 1) - pixelsProcessed);
				int sameLength = GetSameLength(sourceBuffer, sourceOffset, sourceBuffer, sourceOffset + 3, 3, max);
				if (sameLength > 0)
				{
					// write in the count
					if (sameLength > MAX_RUN_LENGTH)
					{
						throw new Exception();
					}

					destBuffer[writePos++] = (byte)((sameLength) | IS_PIXLE_RUN);

					// write in the same length pixel value
					destBuffer[writePos++] = sourceBuffer[sourceOffset + 0];
					destBuffer[writePos++] = sourceBuffer[sourceOffset + 1];
					destBuffer[writePos++] = sourceBuffer[sourceOffset + 2];

					sourceOffset += (sameLength) * 3;
					pixelsProcessed += sameLength + 1;
				}
				else
				{
					differenceHold[0] = sourceBuffer[sourceOffset + 0];
					differenceHold[1] = sourceBuffer[sourceOffset + 1];
					differenceHold[2] = sourceBuffer[sourceOffset + 2];
					int difLength = GetDifLength(differenceHold, sourceBuffer, sourceOffset + 3, 3, max);
					if (difLength == 0)
					{
						difLength = 1;
					}

					// write in the count (if there is only one the count is 0)
					if (sameLength > MAX_RUN_LENGTH)
					{
						throw new Exception();
					}
					destBuffer[writePos++] = (byte)(difLength - 1);

					while (difLength-- > 0)
					{
						// write in the same length pixel value
						destBuffer[writePos++] = sourceBuffer[sourceOffset + 0];
						destBuffer[writePos++] = sourceBuffer[sourceOffset + 1];
						destBuffer[writePos++] = sourceBuffer[sourceOffset + 2];

						sourceOffset += 3;
						pixelsProcessed++;
					}
				}
			}

			return writePos;
		}

		private static int CompressLine32(byte[] destBuffer, byte[] sourceBuffer, int sourceOffset, int Width)
		{
			int writePos = 0;
			int pixelsProcessed = 0;

			while (pixelsProcessed < Width)
			{
				// always get as many as you can that are the same first
				int max = System.Math.Min(MAX_RUN_LENGTH, (Width - 1) - pixelsProcessed);
				int sameLength = GetSameLength(sourceBuffer, sourceOffset, sourceBuffer, sourceOffset + 4, 4, max);
				if (sameLength > 0)
				{
					// write in the count
					if (sameLength > MAX_RUN_LENGTH)
					{
						throw new Exception();
					}

					destBuffer[writePos++] = (byte)((sameLength) | IS_PIXLE_RUN);

					// write in the same length pixel value
					destBuffer[writePos++] = sourceBuffer[sourceOffset + 0];
					destBuffer[writePos++] = sourceBuffer[sourceOffset + 1];
					destBuffer[writePos++] = sourceBuffer[sourceOffset + 2];
					destBuffer[writePos++] = sourceBuffer[sourceOffset + 3];

					sourceOffset += (sameLength) * 4;
					pixelsProcessed += sameLength + 1;
				}
				else
				{
					differenceHold[0] = sourceBuffer[sourceOffset + 0];
					differenceHold[1] = sourceBuffer[sourceOffset + 1];
					differenceHold[2] = sourceBuffer[sourceOffset + 2];
					differenceHold[3] = sourceBuffer[sourceOffset + 3];
					int difLength = GetDifLength(differenceHold, sourceBuffer, sourceOffset + 4, 4, max);
					if (difLength == 0)
					{
						difLength = 1;
					}

					// write in the count (if there is only one the count is 0)
					if (sameLength > MAX_RUN_LENGTH)
					{
						throw new Exception();
					}
					destBuffer[writePos++] = (byte)(difLength - 1);

					while (difLength-- > 0)
					{
						// write in the dif length pixel value
						destBuffer[writePos++] = sourceBuffer[sourceOffset + 0];
						destBuffer[writePos++] = sourceBuffer[sourceOffset + 1];
						destBuffer[writePos++] = sourceBuffer[sourceOffset + 2];
						destBuffer[writePos++] = sourceBuffer[sourceOffset + 3];

						sourceOffset += 4;
						pixelsProcessed++;
					}
				}
			}

			return writePos;
			/*
			while(SourcePos < Width)
			{
				// always get as many as you can that are the same first
				int Max = System.Math.Min(MAX_RUN_LENGTH, (Width - 1) - SourcePos);
				int SameLength = GetSameLength((byte*)&pSource[SourcePos], (byte*)&pSource[SourcePos + 1], 4, Max);
				if(SameLength)
				{
					// write in the count
					assert(SameLength<= MAX_RUN_LENGTH);
					pDest[WritePos++] = (byte)((SameLength) | IS_PIXLE_RUN);

					// write in the same length pixel value
					pDest[WritePos++] = pSource[SourcePos].Blue;
					pDest[WritePos++] = pSource[SourcePos].Green;
					pDest[WritePos++] = pSource[SourcePos].Red;
					pDest[WritePos++] = pSource[SourcePos].Alpha;

					SourcePos += SameLength + 1;
				}
				else
				{
					Pixel32 CheckPixel = pSource[SourcePos];
					int DifLength = GetDifLength((byte*)&CheckPixel, (byte*)&pSource[SourcePos+1], 4, Max);
					if(!DifLength)
					{
						DifLength = 1;
					}

					// write in the count (if there is only one the count is 0)
					assert(DifLength <= MAX_RUN_LENGTH);
					pDest[WritePos++] = (byte)(DifLength-1);

					while(DifLength--)
					{
						// write in the same length pixel value
						pDest[WritePos++] = pSource[SourcePos].Blue;
						pDest[WritePos++] = pSource[SourcePos].Green;
						pDest[WritePos++] = pSource[SourcePos].Red;
						pDest[WritePos++] = pSource[SourcePos].Alpha;
						SourcePos++;
					}
				}
			}

			return WritePos;
			 */
		}

		static public bool SaveImageData(String fileNameToSaveTo, ImageBuffer image)
		{
			return Save(image, fileNameToSaveTo);
		}

		static public bool Save(ImageBuffer image, String fileNameToSaveTo)
		{
			using (Stream file = File.Open(fileNameToSaveTo, FileMode.Create))
			{
				return Save(image, file);
			}
		}

		static public bool Save(ImageBuffer image, Stream streamToSaveImageDataTo)
		{
			STargaHeader targaHeader;

			using (BinaryWriter writerToSaveTo = new BinaryWriter(streamToSaveImageDataTo, new ASCIIEncoding(), true))
			{
				int sourceDepth = image.BitDepth;

				// make sure there is something to save before opening the file
				if (image.Width <= 0 || image.Height <= 0)
				{
					return false;
				}

				// set up the header
				targaHeader.PostHeaderSkip = 0; // no skip after the header
				if (sourceDepth == 8)
				{
					targaHeader.ColorMapType = 1;       // Color type is Palette
					targaHeader.ImageType = 9;      // 1 = Palette, 9 = RLE Palette
					targaHeader.ColorMapStart = 0;
					targaHeader.ColorMapLength = 256;
					targaHeader.ColorMapBits = 24;
				}
				else
				{
					targaHeader.ColorMapType = 0;       // Color type is RGB
#if WRITE_RLE_COMPRESSED
		        TargaHeader.ImageType = 10;		// RLE RGB
#else
					targaHeader.ImageType = 2;      // RGB
#endif
					targaHeader.ColorMapStart = 0;
					targaHeader.ColorMapLength = 0;
					targaHeader.ColorMapBits = 0;
				}

				targaHeader.XStart = 0;
				targaHeader.YStart = 0;
				targaHeader.Width = (ushort)image.Width;
				targaHeader.Height = (ushort)image.Height;
				targaHeader.BPP = (byte)sourceDepth;
				targaHeader.Descriptor = 0; // all 8 bits are used for alpha

				targaHeader.BinaryWrite(writerToSaveTo);

				byte[] pLineBuffer = new byte[image.StrideInBytesAbs() * 2];

				// int BytesToSave;
				switch (sourceDepth)
				{
					case 8:
						/*
					if (image.HasPalette())
					{
						for(int i=0; i<256; i++)
						{
							TGAFile.Write(image.GetPaletteIfAllocated()->pPalette[i * RGB_SIZE + RGB_BLUE]);
							TGAFile.Write(image.GetPaletteIfAllocated()->pPalette[i * RGB_SIZE + RGB_GREEN]);
							TGAFile.Write(image.GetPaletteIfAllocated()->pPalette[i * RGB_SIZE + RGB_RED]);
						}
					}
					else
						 */
						{
							// there is no palette for this DIB but we should write something
							for (int i = 0; i < 256; i++)
							{
								writerToSaveTo.Write((byte)i);
								writerToSaveTo.Write((byte)i);
								writerToSaveTo.Write((byte)i);
							}
						}

						for (int i = 0; i < image.Height; i++)
						{
							int bufferOffset;
							byte[] buffer = image.GetPixelPointerY(i, out bufferOffset);
#if WRITE_RLE_COMPRESSED
                    BytesToSave = CompressLine8(pLineBuffer, buffer, bufferOffset, image.Width());
			        writerToSaveTo.Write(pLineBuffer, 0, BytesToSave);
#else
							writerToSaveTo.Write(buffer, bufferOffset, image.Width);
#endif
						}

						break;

					case 24:
						for (int i = 0; i < image.Height; i++)
						{
							int bufferOffset;
							byte[] buffer = image.GetPixelPointerY(i, out bufferOffset);
#if WRITE_RLE_COMPRESSED
                    BytesToSave = CompressLine24(pLineBuffer, buffer, bufferOffset, image.Width());
                    writerToSaveTo.Write(pLineBuffer, 0, BytesToSave);
#else
							writerToSaveTo.Write(buffer, bufferOffset, image.Width * 3);
#endif
						}

						break;

					case 32:
						for (int i = 0; i < image.Height; i++)
						{
							int bufferOffset;
							byte[] buffer = image.GetPixelPointerY(i, out bufferOffset);
#if WRITE_RLE_COMPRESSED
                    BytesToSave = CompressLine32(pLineBuffer, buffer, bufferOffset, image.Width);
                    writerToSaveTo.Write(pLineBuffer, 0, BytesToSave);
#else
							writerToSaveTo.Write(buffer, bufferOffset, image.Width * 4);
#endif
						}

						break;

					default:
						throw new NotSupportedException();
				}

				writerToSaveTo.Flush();
			}

			return true;
		}

		/*
		bool SourceNeedsToBeResaved(String pFileName)
		{
			CFile TGAFile;
			if(TGAFile.Open(pFileName, CFile::modeRead))
			{
				STargaHeader TargaHeader;
				byte[] pWorkPtr = new byte[sizeof(STargaHeader)];

				TGAFile.Read(pWorkPtr, sizeof(STargaHeader));
				TGAFile.Close();

				if(ReadTGAInfo(pWorkPtr, &TargaHeader))
				{
					ArrayDeleteAndSetNull(pWorkPtr);
					return TargaHeader.ImageType != 10;
				}

				ArrayDeleteAndSetNull(pWorkPtr);
			}

			return true;
		}
		 */

		static public int ReadBitsFromBuffer(ImageBuffer image, byte[] WorkPtr, int destBitDepth)
		{
			return LowLevelReadTGABitsFromBuffer(image, WorkPtr, destBitDepth);
		}

		static public bool LoadImageData(ImageBuffer image, string fileName)
		{
			if (System.IO.File.Exists(fileName))
			{
				using (var stream = File.OpenRead(fileName))
				{
					return LoadImageData(image, stream, 32);
				}
			}

			return false;
		}

		static public bool LoadImageData(ImageBuffer image, Stream streamToLoadImageDataFrom, int destBitDepth)
		{
			byte[] imageData = new byte[streamToLoadImageDataFrom.Length];
			streamToLoadImageDataFrom.Read(imageData, 0, (int)streamToLoadImageDataFrom.Length);
			return ReadBitsFromBuffer(image, imageData, destBitDepth) > 0;
		}

		static public int GetBitDepth(Stream streamToReadFrom)
		{
			STargaHeader targaHeader;
			byte[] imageData = new byte[streamToReadFrom.Length];
			streamToReadFrom.Read(imageData, 0, (int)streamToReadFrom.Length);
			if (ReadTGAInfo(imageData, out targaHeader))
			{
				return targaHeader.BPP;
			}

			return 0;
		}
	}
}