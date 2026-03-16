using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	public class Graphics2DGpuTests
	{
		[Test]
		public async Task NativeScenePathMeshAppliesTransformToVertices()
		{
			var rectangle = new VertexStorage();
			rectangle.MoveTo(0, 0);
			rectangle.LineTo(10, 0);
			rectangle.LineTo(10, 20);
			rectangle.LineTo(0, 20);
			rectangle.ClosePolygon();

			var mesh = Graphics2DGpu.CreateNativeScenePathMesh(
				Matrix4X4.CreateScale(2) * Matrix4X4.CreateTranslation(5, 7, 0),
				rectangle);
			var bounds = mesh.GetAxisAlignedBoundingBox();

			await Assert.That(mesh.Faces.Count).IsEqualTo(2);
			await Assert.That(bounds.MinXYZ.X).IsEqualTo(5);
			await Assert.That(bounds.MinXYZ.Y).IsEqualTo(7);
			await Assert.That(bounds.MaxXYZ.X).IsEqualTo(25);
			await Assert.That(bounds.MaxXYZ.Y).IsEqualTo(47);
		}

		[Test]
		public async Task NativeScenePathMeshTriangulatesCurvedPaths()
		{
			var mesh = Graphics2DGpu.CreateNativeScenePathMesh(
				Matrix4X4.Identity,
				new Ellipse(0, 0, 10, 10));

			await Assert.That(mesh.Faces.Count > 0).IsTrue();
			await Assert.That(mesh.Vertices.Count > 0).IsTrue();
		}
	}
}
