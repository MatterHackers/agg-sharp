
using MatterHackers.VectorMath;

public class OctreeNode
{
    public OctreeNodeType Type;
    public Vector3 min;
    public int size;
    public OctreeNode[] children;
    public OctreeDrawInfo drawInfo;

	public OctreeNode()
	{
        Type = OctreeNodeType.Node_None;
        min = Vector3.Zero;
        size = 0;
        drawInfo = new OctreeDrawInfo();

        children = new OctreeNode[8];
        for (int i = 0; i < 8; i++)
        {
            children[i] = null;
        }
	}

    public OctreeNode(OctreeNodeType _type)
    {
        Type = _type;
        min = Vector3.Zero;
        size = 0;
        drawInfo = new OctreeDrawInfo();

        children = new OctreeNode[8];
        for (int i = 0; i < 8; i++)
        {
            children[i] = null;
        }
    }
}