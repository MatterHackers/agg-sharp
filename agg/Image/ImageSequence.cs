using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Image
{
    public class ImageSequence
    {
        double secondsPerFrame = 1.0/30.0;
        public double FramePerSecond
        {
            get { return 1 / secondsPerFrame; }
            set { secondsPerFrame = 1 / value; }
        }

        public double SecondsPerFrame
        {
            get { return secondsPerFrame; }
            set { secondsPerFrame = value; }
        }

        public int NumFrames
        {
            get { return imageList.Count; }
        }

        public int Width
        {
            get
            {
                RectangleInt bounds = new RectangleInt(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
                foreach (ImageBuffer frame in imageList)
                {
                    bounds.ExpandToInclude(frame.GetBoundingRect());
                }

                return Math.Max(0, bounds.Width);
            }
        }

        public int Height
        {
            get
            {
                RectangleInt bounds = new RectangleInt(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
                foreach (ImageBuffer frame in imageList)
                {
                    bounds.ExpandToInclude(frame.GetBoundingRect());
                }

                return Math.Max(0, bounds.Height);
            }
        }

        bool looping = false;

        List<ImageBuffer> imageList = new List<ImageBuffer>();

        public ImageSequence()
        {
        }

        public void SetAlpha(byte value)
        {
            foreach (ImageBuffer image in imageList)
            {
                image.SetAlpha(value);
            }
        }

        public void CenterOriginOffset()
        {
            foreach (ImageBuffer image in imageList)
            {
                image.OriginOffset = new Vector2(image.Width / 2, image.Height / 2);
            }
        }

        public void CropToVisible()
        {
            foreach (ImageBuffer image in imageList)
            {
                image.CropToVisible();
            }
        }

        public static ImageSequence LoadFromTgas(String pathName)
        {
            // First we load up the Data In the Serialization file.
            String gameDataObjectXMLPath = Path.Combine(pathName, "ImageSequence");
            ImageSequence sequenceLoaded = new ImageSequence();

            // Now lets look for and load up any images that we find.
            String[] tgaFilesArray = Directory.GetFiles(pathName, "*.tga");
            List<String> sortedTgaFiles = new List<string>(tgaFilesArray);
            // Make sure they are sorted.
            sortedTgaFiles.Sort();
            sequenceLoaded.imageList = new List<ImageBuffer>();
            int imageIndex = 0;
            foreach (String tgaFile in sortedTgaFiles)
            {
                sequenceLoaded.AddImage(new ImageBuffer(new BlenderPreMultBGRA()));
                Stream imageStream = File.Open(tgaFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                ImageTgaIO.LoadImageData(sequenceLoaded.imageList[imageIndex], imageStream, 32);
                imageIndex++;
            }

            return sequenceLoaded;
        }

        public void AddImage(ImageBuffer imageBuffer)
        {
            imageList.Add(imageBuffer);
        }

        public int GetFrameIndexByRatio(double fractionOfTotalLength)
        {
            return (int)((fractionOfTotalLength * (NumFrames - 1)) + .5);
        }

        public ImageBuffer GetImageByTime(double NumSeconds)
        {
            double TotalSeconds = NumFrames / FramePerSecond;
            return GetImageByRatio(NumSeconds / TotalSeconds);
        }

        public ImageBuffer GetImageByRatio(double fractionOfTotalLength)
        {
            return GetImageByIndex(fractionOfTotalLength * (NumFrames - 1));
        }

        public ImageBuffer GetImageByIndex(double ImageIndex)
        {
            return GetImageByIndex((int)(ImageIndex + .5));
        }

        public ImageBuffer GetImageByIndex(int ImageIndex)
        {
            if (looping)
            {
                return imageList[ImageIndex % NumFrames];
            }

            if (ImageIndex < 0)
            {
                return imageList[0];
            }
            else if (ImageIndex > NumFrames - 1)
            {
                return imageList[NumFrames - 1];
            }

            return imageList[ImageIndex];
        }
    }
}
