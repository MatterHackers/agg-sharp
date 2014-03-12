using System;

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

using Gaming.Math;
using Gaming.Game;
using Gaming.Graphics;

namespace RockBlaster
{
    public class SequenceEntity : Entity
    {
        [GameData("Sequence")]
        public AssetReference<GameImageSequence> ImageSequenceReference = new AssetReference<GameImageSequence>("LargFire");

        [GameDataNumberAttribute("Rotation")] // This is for save game
        protected double m_Rotation;

        [GameDataNumberAttribute("Scale")] // This is for save game
        protected double m_Scale;

        double m_TotalSeconds;

        public SequenceEntity(Vector2 position)
            : base(3)
        {
            Position = position;
        }

        protected override void DoDraw(Graphics2D destRenderer)
        {
            ImageBuffer imageToDraw = ImageSequenceReference.Instance.GetImageByTime(m_TotalSeconds);
            //Image imageToDraw = m_PlayerShipSequence.GetImageByIndex(m_ImageIndex);
            //IBlender blender = new BlenderBGRA();
            IRecieveBlenderByte recieveBlender = new BlenderPreMultBGR();

            /*
            unsafe
            {
                IImage destBuffer = destRenderer.DestImage;
                byte* pPixels = destBuffer.GetPixelPointerY(200);
                byte[] sourceBuffer = imageToDraw.ByteBuffer;
                for (int y = 0; y < imageToDraw.Height(); y++)
                {
                    int SourceYOffset = y * imageToDraw.StrideInBytes();
                    int destYOffset = (int)destBuffer.StrideInBytesAbs() * y;
                    for (int x = 0; x < imageToDraw.Width(); x++)
                    {
                        int sourceOffset = SourceYOffset + x * 4;
                        RGBA_Bytes sourceColor = new RGBA_Bytes(sourceBuffer[sourceOffset + 2], sourceBuffer[sourceOffset + 1], sourceBuffer[sourceOffset + 0], sourceBuffer[sourceOffset + 3]);
                        blender.BlendPixel(&pPixels[destYOffset + x * 4], sourceColor);
                    }
                }
            }
             */
        }

        public override void Update(double numSecondsPassed)
        {
            m_TotalSeconds += numSecondsPassed;
            base.Update(numSecondsPassed);
        }
    }
}
