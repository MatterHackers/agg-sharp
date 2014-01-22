using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Gaming.Game;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace Gaming.Graphics
{
    public class ImageSequence : GameObject
    {
        [GameDataNumber("FramesPerSecond", 
            Description="Stored so that an object using this sequence knows how fast it should play.",
            Min = 0, Max = 120)]
        double m_FramesPerSecond = 30;
        [GameDataBool("AnimationIsLooping",
            Description = "Does the last frame loop back to the first frame.  Used when asking for a frame out of range.")]
        bool m_Looping = false;
        [GameDataBool("CenterOriginDurringPreprocessing",
            Description = "Used durring asset ingestion when processing raw art.  This lets the engine know this asset should have a centerd origin.")]
        bool m_CenterOriginDurringPreprocessing = true;
        [GameDataBool("CropToVisibleDurringPreprocessing",
            Description = "Used durring asset ingestion when processing raw art.  This lets the engine know this asset should have all alpha 0 pixels croped off.")]
        bool m_CropToVisibleDurringPreprocessing = true;

        ImageBuffer[] m_Images;

        public ImageSequence()
        {
        }

        private static ImageSequence LoadSerializationFileForFolder(String gameDataObjectXMLPath)
        {
            ImageSequence sequenceLoaded;

            sequenceLoaded = (ImageSequence)GameObject.Load(gameDataObjectXMLPath);

            if (sequenceLoaded == null)
            {
                sequenceLoaded = new ImageSequence();
                sequenceLoaded.SaveXML(gameDataObjectXMLPath);
            }

            return sequenceLoaded;
        }

        public void SetAlpha(byte value)
        {
            foreach (ImageBuffer image in m_Images)
            {
                image.SetAlpha(value);
            }
        }

        public void CenterOriginOffset()
        {
            foreach(ImageBuffer image in m_Images)
            {
                image.OriginOffset = new Vector2(image.Width / 2, image.Height / 2);
            }
        }

        public void CropToVisible()
        {
            foreach (ImageBuffer image in m_Images)
            {
                image.CropToVisible();
            }
        }

        public new static GameObject Load(String PathName)
        {
            // First we load up the Data In the Serialization file.
            String gameDataObjectXMLPath = System.IO.Path.Combine(PathName, "ImageSequence");
            ImageSequence sequenceLoaded = LoadSerializationFileForFolder(gameDataObjectXMLPath);

            // Now lets look for and load up any images that we find.
            String[] tgaFilesArray = Directory.GetFiles(PathName, "*.tga");
            List<String> sortedTgaFiles = new List<string>(tgaFilesArray);
            // Make sure they are sorted.
            sortedTgaFiles.Sort();
            sequenceLoaded.m_Images = new ImageBuffer[sortedTgaFiles.Count];
            int imageIndex = 0;
            foreach (String tgaFile in sortedTgaFiles)
            {
                sequenceLoaded.m_Images[imageIndex] = new ImageBuffer(new BlenderPreMultBGRA());
                Stream imageStream = File.Open(tgaFile, FileMode.Open);
                ImageTgaIO.LoadImageData(sequenceLoaded.m_Images[imageIndex], imageStream, 32);
                imageIndex++;
            }

            if (sequenceLoaded.m_CenterOriginDurringPreprocessing)
            {
                sequenceLoaded.CenterOriginOffset();
            }

            if (sequenceLoaded.m_CropToVisibleDurringPreprocessing)
            {
                sequenceLoaded.CropToVisible();
            }

            return sequenceLoaded;
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
            if (m_Looping)
            {
                return m_Images[ImageIndex % NumFrames];
            }

            if(ImageIndex < 0)
            {
                return m_Images[0];
            }
            else if (ImageIndex > NumFrames - 1)
            {
                return m_Images[NumFrames - 1];
            }

            return m_Images[ImageIndex];
        }

        public int NumFrames
        {
            get
            {
                return m_Images.Length;
            }
        }

        public double FramePerSecond
        {
            get
            {
                return m_FramesPerSecond;
            }
        }
    }
}
