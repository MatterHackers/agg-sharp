
using MatterHackers.VectorMath;

public class OctreeNode
{
    public OctreeNodeType Type;

    /// <summary>
    /// The lowest edge of the octree for this node
    /// </summary>
    public Vector3 Min;

    /// <summary>
    /// The level in the octree (1 = an inner leaf)
    /// </summary>
    public int Level;

    public Vector3 Size;

    public OctreeNode[] Children;
    public OctreeDrawInfo drawInfo;

	public OctreeNode()
	{
        Type = OctreeNodeType.Node_None;
        drawInfo = new OctreeDrawInfo();

        Children = new OctreeNode[8];
        for (int i = 0; i < 8; i++)
        {
            Children[i] = null;
        }
	}

    public OctreeNode(OctreeNodeType _type)
    {
        Type = _type;
        drawInfo = new OctreeDrawInfo();

        Children = new OctreeNode[8];
        for (int i = 0; i < 8; i++)
        {
            Children[i] = null;
        }
    }
}