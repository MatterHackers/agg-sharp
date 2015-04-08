/*
Copyright (c) 2014, Lars Brubaker, Kevin Pope
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

namespace MatterHackers.Agg
{
	public class FloodFill
	{
		protected byte[] destBuffer = null;

		protected int imageStride = 0;

		protected bool[] pixelsChecked;

		private ImageBuffer destImage;

		private FillingRule fillRule;

		private FirstInFirstOutQueue<Range> ranges = new FirstInFirstOutQueue<Range>(9);

		public FloodFill(RGBA_Bytes fillColor)
		{
			fillRule = new ExactMatch(fillColor);
		}

		public FloodFill(RGBA_Bytes fillColor, int tolerance0To255)
		{
			if (tolerance0To255 > 0)
			{
				fillRule = new ToleranceMatch(fillColor, tolerance0To255);
			}
			else
			{
				fillRule = new ExactMatch(fillColor);
			}
		}

		public FloodFill(FillingRule fillRule)
		{
			this.fillRule = fillRule;
		}

		public void Fill(ImageBuffer bufferToFillOn, int x, int y)
		{
			unchecked // this way we can overflow the uint on negative and get a big number
			{
				if ((uint)x > bufferToFillOn.Width || (uint)y > bufferToFillOn.Height)
				{
					return;
				}
			}

			destImage = bufferToFillOn;
			imageStride = destImage.StrideInBytes();
			destBuffer = destImage.GetBuffer();
			int imageWidth = destImage.Width;
			int imageHeight = destImage.Height;

			pixelsChecked = new bool[destImage.Width * destImage.Height];

			int startColorBufferOffset = destImage.GetBufferOffsetXY(x, y);

			fillRule.SetStartColor(new RGBA_Bytes(destImage.GetBuffer()[startColorBufferOffset + 2], destImage.GetBuffer()[startColorBufferOffset + 1], destImage.GetBuffer()[startColorBufferOffset]));

			LinearFill(x, y);

			while (ranges.Count > 0)
			{
				Range range = ranges.Dequeue();

				int downY = range.y - 1;
				int upY = range.y + 1;
				int downPixelOffset = (imageWidth * (range.y - 1)) + range.startX;
				int upPixelOffset = (imageWidth * (range.y + 1)) + range.startX;
				for (int rangeX = range.startX; rangeX <= range.endX; rangeX++)
				{
					if (range.y > 0)
					{
						if (!pixelsChecked[downPixelOffset])
						{
							int bufferOffset = destImage.GetBufferOffsetXY(rangeX, downY);
							if (fillRule.CheckPixel(destBuffer, bufferOffset))
							{
								LinearFill(rangeX, downY);
							}
						}
					}

					if (range.y < (imageHeight - 1))
					{
						if (!pixelsChecked[upPixelOffset])
						{
							int bufferOffset = destImage.GetBufferOffsetXY(rangeX, upY);
							if (fillRule.CheckPixel(destBuffer, bufferOffset))
							{
								LinearFill(rangeX, upY);
							}
						}
					}
					upPixelOffset++;
					downPixelOffset++;
				}
			}
		}

		private void LinearFill(int x, int y)
		{
			int bytesPerPixel = destImage.GetBytesBetweenPixelsInclusive();
			int imageWidth = destImage.Width;

			int leftFillX = x;
			int bufferOffset = destImage.GetBufferOffsetXY(x, y);
			int pixelOffset = (imageWidth * y) + x;
			while (true)
			{
				fillRule.SetPixel(destBuffer, bufferOffset);
				pixelsChecked[pixelOffset] = true;
				leftFillX--;
				pixelOffset--;
				bufferOffset -= bytesPerPixel;
				if (leftFillX <= 0 || (pixelsChecked[pixelOffset]) || !fillRule.CheckPixel(destBuffer, bufferOffset))
				{
					break;
				}
			}
			leftFillX++;

			int rightFillX = x;
			bufferOffset = destImage.GetBufferOffsetXY(x, y);
			pixelOffset = (imageWidth * y) + x;
			while (true)
			{
				fillRule.SetPixel(destBuffer, bufferOffset);
				pixelsChecked[pixelOffset] = true;
				rightFillX++;
				pixelOffset++;
				bufferOffset += bytesPerPixel;
				if (rightFillX >= imageWidth || pixelsChecked[pixelOffset] || !fillRule.CheckPixel(destBuffer, bufferOffset))
				{
					break;
				}
			}
			rightFillX--;

			ranges.Enqueue(new Range(leftFillX, rightFillX, y));
		}

		private struct Range
		{
			public int endX;
			public int startX;
			public int y;

			public Range(int startX, int endX, int y)
			{
				this.startX = startX;
				this.endX = endX;
				this.y = y;
			}
		}

		public class ExactMatch : FillingRule
		{
			public ExactMatch(RGBA_Bytes fillColor)
				: base(fillColor)
			{
			}

			public override bool CheckPixel(byte[] destBuffer, int bufferOffset)
			{
				return (destBuffer[bufferOffset] == startColor.red) &&
					(destBuffer[bufferOffset + 1] == startColor.green) &&
					(destBuffer[bufferOffset + 2] == startColor.blue);
			}
		}

		public abstract class FillingRule
		{
			protected RGBA_Bytes fillColor;
			protected RGBA_Bytes startColor;

			protected FillingRule(RGBA_Bytes fillColor)
			{
				this.fillColor = fillColor;
			}

			public abstract bool CheckPixel(byte[] destBuffer, int bufferOffset);

			public virtual void SetPixel(byte[] destBuffer, int bufferOffset)
			{
				destBuffer[bufferOffset] = fillColor.blue;
				destBuffer[bufferOffset + 1] = fillColor.green;
				destBuffer[bufferOffset + 2] = fillColor.red;
			}

			public void SetStartColor(RGBA_Bytes startColor)
			{
				this.startColor = startColor;
			}
		}

		public class ToleranceMatch : FillingRule
		{
			private int tolerance0To255;

			public ToleranceMatch(RGBA_Bytes fillColor, int tolerance0To255)
				: base(fillColor)
			{
				this.tolerance0To255 = tolerance0To255;
			}

			public override bool CheckPixel(byte[] destBuffer, int bufferOffset)
			{
				return (destBuffer[bufferOffset] >= (startColor.red - tolerance0To255)) && destBuffer[bufferOffset] <= (startColor.red + tolerance0To255) &&
					(destBuffer[bufferOffset + 1] >= (startColor.green - tolerance0To255)) && destBuffer[bufferOffset + 1] <= (startColor.green + tolerance0To255) &&
					(destBuffer[bufferOffset + 2] >= (startColor.blue - tolerance0To255)) && destBuffer[bufferOffset + 2] <= (startColor.blue + tolerance0To255);
			}
		}
	}
}