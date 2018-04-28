using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.Agg.Image
{
	public class ImageSequence
	{
		public List<ImageBuffer> Frames = new List<ImageBuffer>();
		public List<int> FrameTimesMs = new List<int>();

		public event EventHandler Invalidated;

		public ImageSequence()
		{
		}

		public double Time
		{
			get
			{
				if(FrameTimesMs.Any())
				{
					int totalTime = 0;
					foreach (var time in FrameTimesMs)
					{
						totalTime += time;
					}

					return totalTime / 1000.0;
				}
				else
				{
					return Frames.Count * SecondsPerFrame;
				}
			}
		}

		public double FramePerSecond
		{
			get { return 1 / SecondsPerFrame; }
			set { SecondsPerFrame = 1 / value; }
		}

		public int Height
		{
			get
			{
				if (Frames.Count > 0)
				{
					RectangleInt bounds = new RectangleInt(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
					foreach (ImageBuffer frame in Frames)
					{
						bounds.ExpandToInclude(frame.GetBoundingRect());
					}
					return Math.Max(0, bounds.Height);
				}

				return 0;
			}
		}

		public bool Looping { get; set; }

		public int NumFrames
		{
			get { return Frames.Count; }
		}

		public double SecondsPerFrame { get; set; } = 1.0 / 30.0;

		public int Width
		{
			get
			{
				if (Frames.Count > 0)
				{
					RectangleInt bounds = new RectangleInt(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
					foreach (ImageBuffer frame in Frames)
					{
						bounds.ExpandToInclude(frame.GetBoundingRect());
					}

					return Math.Max(0, bounds.Width);
				}

				return 0;
			}
		}

		public static ImageSequence LoadFromTgas(string pathName)
		{
			var sequenceLoaded = new ImageSequence();

			// Now lets look for and load up any images that we find.
			var sortedTgaFiles = Directory.GetFiles(pathName, "*.tga").OrderBy(s => s);

			foreach (string tgaFile in sortedTgaFiles)
			{
				using (var imageStream = File.OpenRead(tgaFile))
				{
					var imageBuffer = new ImageBuffer(new BlenderPreMultBGRA());
					ImageTgaIO.LoadImageData(imageBuffer, imageStream, 32);
					sequenceLoaded.AddImage(imageBuffer);
				}
			}

			return sequenceLoaded;
		}

		public void AddImage(ImageBuffer imageBuffer, int frameTimeMs = 0)
		{
			Frames.Add(imageBuffer);
			if(frameTimeMs > 0)
			{
				FrameTimesMs.Add(frameTimeMs);
			}
		}

		public void CenterOriginOffset()
		{
			foreach (ImageBuffer image in Frames)
			{
				image.OriginOffset = new Vector2(image.Width / 2, image.Height / 2);
			}
		}

		public void CropToVisible()
		{
			foreach (ImageBuffer image in Frames)
			{
				image.CropToVisible();
			}
		}

		public int GetFrameIndexByRatio(double fractionOfTotalLength)
		{
			return (int)((fractionOfTotalLength * (NumFrames - 1)) + .5);
		}

		public ImageBuffer GetImageByIndex(double ImageIndex)
		{
			return GetImageByIndex((int)(ImageIndex + .5));
		}

		public ImageBuffer GetImageByIndex(int ImageIndex)
		{
			if (Looping)
			{
				return Frames[ImageIndex % NumFrames];
			}

			if (ImageIndex < 0)
			{
				return Frames[0];
			}
			else if (ImageIndex > NumFrames - 1)
			{
				return Frames[NumFrames - 1];
			}

			return Frames[ImageIndex];
		}

		public ImageBuffer GetImageByRatio(double fractionOfTotalLength)
		{
			return GetImageByIndex(fractionOfTotalLength * (NumFrames - 1));
		}

		public ImageBuffer GetImageByTime(double numSeconds)
		{
			if (FrameTimesMs.Count > 0)
			{
				int timeMs = (int)(numSeconds * 1000);
				double totalTime = 0;
				int index = 0;
				foreach (var time in FrameTimesMs)
				{
					totalTime += time;
					if(totalTime > timeMs)
					{
						return Frames[Math.Min(index, Frames.Count - 1)];
					}
					index++;
				}
			}

			double TotalSeconds = NumFrames / FramePerSecond;
			return GetImageByRatio(numSeconds / TotalSeconds);
		}

		public void Invalidate()
		{
			OnInvalidated(null);
		}

		public virtual void OnInvalidated(EventArgs args)
		{ 
			Invalidated?.Invoke(this, args);
		}

		public void SetAlpha(byte value)
		{
			foreach (ImageBuffer image in Frames)
			{
				image.SetAlpha(value);
			}
		}

		public class Properties
		{
			public double FramePerFrame = 30;
			public bool Looping = false;
		}
	}
}