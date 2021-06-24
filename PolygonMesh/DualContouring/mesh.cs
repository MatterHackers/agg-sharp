using MatterHackers.VectorMath;
using System.Collections.Generic;

public struct MeshVertex
{
	public Vector3 xyz, normal;

	public MeshVertex(Vector3 _xyz, Vector3 _normal)
    {
		xyz = _xyz;
		normal = _normal;
	}
}

public class DCMesh
{
    public List<int> vertexArrayObj_, vertexBuffer_, indexBuffer_;
    public int numIndices_;

    public DCMesh()
    {
        vertexArrayObj_ = new List<int>();
        vertexBuffer_ = new List<int>();
        indexBuffer_ = new List<int>();
        numIndices_ = 0;

    }

    public void initialise()
    {

    }
    public void uploadData(List<MeshVertex> vertices, List<int> indices)
    {

    }


    public void destroy()
    {

    }

}



