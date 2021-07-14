using MatterHackers.VectorMath;

public struct MeshVertex
{
    public Vector3 Position;
    public Vector3 Normal;

	public MeshVertex(Vector3 position, Vector3 normal)
    {
		Position = position;
		Normal = normal;
	}
}



