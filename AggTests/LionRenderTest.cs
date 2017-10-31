using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using NUnit.Framework;
using System.IO;

namespace MatterHackers.Agg.Tests
{
	[TestFixture]
	public class LionRenderTest
	{
		[Test, Category("FixNeeded")]
		public void CompareToLionTGA()
		{
			LionShape lionShape = new LionShape();
			ImageBuffer renderedImage = new ImageBuffer(512, 400, 24, new BlenderBGR());
			byte alpha = (byte)(.1 * 255);
			for (int i = 0; i < lionShape.NumPaths; i++)
			{
				lionShape.Colors[i].Alpha0To255 = alpha;
			}

			Affine transform = Affine.NewIdentity();
			transform *= Affine.NewTranslation(-lionShape.Center.x, -lionShape.Center.y);
			transform *= Affine.NewTranslation(renderedImage.Width / 2, renderedImage.Height / 2);

			// This code renders the lion:
			VertexSourceApplyTransform transformedPathStorage = new VertexSourceApplyTransform(lionShape.Path, transform);
			Graphics2D renderer = renderedImage.NewGraphics2D();
			renderer.Clear(new ColorF(1.0, 1.0, 1.0, 1.0));
			renderer.Render(transformedPathStorage, lionShape.Colors, lionShape.PathIndex, lionShape.NumPaths);

			ImageTgaIO.Save(renderedImage, "TestOutput.tga");

			Stream imageStream = File.Open("LionRenderMaster.tga", FileMode.Open);
			ImageBuffer masterImage = new ImageBuffer();
			ImageTgaIO.LoadImageData(masterImage, imageStream, 24);

			bool sameWidth = masterImage.Width == renderedImage.Width;
			bool sameHeight = masterImage.Height == renderedImage.Height;
			Assert.IsTrue(sameWidth && sameHeight);
			Assert.IsTrue(masterImage.BitDepth == renderedImage.BitDepth);
			int unused;
			byte[] masterBuffer = masterImage.GetBuffer(out unused);
			byte[] renderedBuffer = renderedImage.GetBuffer(out unused);
			Assert.IsTrue(masterBuffer.Length == renderedBuffer.Length);
			for (int i = 0; i < masterBuffer.Length; i++)
			{
				if (masterBuffer[i] != renderedBuffer[i])
				{
					Assert.IsTrue(false);
				}
			}
		}
	}
}