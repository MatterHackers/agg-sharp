/*
Copyright (c) 2015, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Pathing
{
#if true
    public class CPathLineSegment
    {

    }

    public class CCollideProcessingState
    {

    }

    public class CLayerPart
    {
    }

    public class CWayPointMap
    {
        internal CPathWayPoint GetpWayPoint(uint startWayPointIndex)
        {
            throw new NotImplementedException();
        }
    }

    public class CPointList
    {

    }

    public class CPathWayPoint
    {
        internal Vector2 GetPosition()
        {
            throw new NotImplementedException();
        }
    }

    public class CAStarNode
    {
        double m_AccumulatedCostFromStartG;
        double m_EstimatedCostToDestH;
        double m_TotalCostForThisNodeF;
        CPathWayPoint m_pCurWayPoint;
        uint m_CurWayPointIndex;
        CAStarNode m_pParent;

        CAStarNode m_pNext;

        static readonly int SHIFT = 6;   // change this to reflect the the size. Ex. 64x64 tile equals 2^6. or a shift of 6
        static readonly int TILESIZE = 64;  // change this also to reflect tile size. 64x64.

        public delegate double ESTIMATE_ADDITIONAL_COST_FUNC_PTR(CAStarNode parentNode, CPathLineSegment connectingSegment, double collisionCostRatio);
        public delegate double ESTIMATE_COST_TO_DEST_FUNC_PTR(CPathWayPoint pCurWayPoint, CPathWayPoint pFinalDestWayPoint);

        static readonly double UNSET_NODE_VALUE = double.MaxValue;

        public CAStarNode()
        {
            Reset();
        }

        public void Reset()
        {
            m_EstimatedCostToDestH = 0;
            m_TotalCostForThisNodeF = 0;
            m_pCurWayPoint = null;
            m_CurWayPointIndex = 0;
            m_pParent = null;
            m_pNext = null;

            m_AccumulatedCostFromStartG = UNSET_NODE_VALUE;
        }

        public void Initialize(CAStarNode pParentNode, CPathLineSegment pConnectingSegment,
                            CPathWayPoint pCurWayPoint, uint CurWayPointIndex,
                            CPathWayPoint pFinalDestWayPoint, double AccumulatedCostFromStartG,
                            ESTIMATE_ADDITIONAL_COST_FUNC_PTR AdditionalCostFunc, double CollisionCostRatio, ESTIMATE_COST_TO_DEST_FUNC_PTR EstimateCostToDestFunc)
        {
            // you shouldn't change this node unless the change is better
            //assert(m_AccumulatedCostFromStartG >= AccumulatedCostFromStartG);

            m_AccumulatedCostFromStartG = AccumulatedCostFromStartG;
            if (pParentNode == null && pConnectingSegment == null)
            {
                m_AccumulatedCostFromStartG += AdditionalCostFunc(pParentNode, pConnectingSegment, CollisionCostRatio);
            }
            m_EstimatedCostToDestH = EstimateCostToDestFunc(pCurWayPoint, pFinalDestWayPoint);
            m_TotalCostForThisNodeF = m_AccumulatedCostFromStartG + m_EstimatedCostToDestH;
            //assert(pCurWayPoint.GetPosition().x < 1000000 && pCurWayPoint.GetPosition().x > -1000000);
            //assert(pCurWayPoint.GetPosition().y < 1000000 && pCurWayPoint.GetPosition().y > -1000000);
            m_pCurWayPoint = pCurWayPoint;
            m_CurWayPointIndex = CurWayPointIndex;
            m_pParent = pParentNode;
        }

        void EditorDraw(Graphics2D pDestFrame)
        {
            Vector2 WayPointPos = m_pCurWayPoint.GetPosition();
            if (m_pParent != null)
            {
                RGBA_Bytes LineColor = new RGBA_Bytes(128, 128, 128);
                Vector2 ParentPos = m_pParent.m_pCurWayPoint.GetPosition();
                // draw a line back to your parent
                pDestFrame.Line(WayPointPos.x, WayPointPos.y, ParentPos.x, ParentPos.y, LineColor);
            }

            // print out the stats for this point
            int LineSpacing = 12;
            int LineOffset = -LineSpacing;
            string Text = "";

            Text.FormatWith("G: {0:0.0}", m_AccumulatedCostFromStartG);
            pDestFrame.DrawString(Text, WayPointPos.x, WayPointPos.y + LineOffset, justification: Justification.Center);
            LineOffset += LineSpacing;

            Text.FormatWith("H: {0:0.0}", m_EstimatedCostToDestH);
            pDestFrame.DrawString(Text, WayPointPos.x, WayPointPos.y + LineOffset, justification: Justification.Center);
            LineOffset += LineSpacing;

            Text.FormatWith("F: {0:0.0}", m_TotalCostForThisNodeF);
            pDestFrame.DrawString(Text, WayPointPos.x, WayPointPos.y + LineOffset, justification: Justification.Center);
            LineOffset += LineSpacing;
        }


        CPathWayPoint GetpCurWayPoint() { return m_pCurWayPoint; }
        CAStarNode GetpParentNode() { return m_pParent; }
        uint GetCurWayPointIndex() { return m_CurWayPointIndex; }
        double GetTotalCostF() { return m_TotalCostForThisNodeF; }
        double GetAccumulatedCostFromStartG() { return m_AccumulatedCostFromStartG; }
        double GetEstimatedCostToDestH() { return m_EstimatedCostToDestH; }
        public static bool operator ==(CAStarNode a, CAStarNode b)
        {
            return a.m_CurWayPointIndex == b.m_CurWayPointIndex;
        }
        public static bool operator !=(CAStarNode a, CAStarNode b)
        {
            return a.m_CurWayPointIndex != b.m_CurWayPointIndex;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() == typeof(RGBA_Floats))
            {
                return this == (CAStarNode)obj;
            }
            return false;
        }
        public CAStarNode GetpNext() { return m_pNext; }
        public void SetpNext(CAStarNode pNewNext) { m_pNext = pNewNext; }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

public class CPathAStar
{
    static readonly int MAX_NODES = 300;
        public		CPathAStar()
        {
            // Make all the nodes that we will use, and put them on the unused list
            for (uint i = 0; i < MAX_NODES; i++)
            {
                CAStarNode pNewNode = new CAStarNode();
                pNewNode.SetpNext(m_UnusedNodes.GetpNext());
                m_UnusedNodes.SetpNext(pNewNode);
            }
        }

        static List<CAStarNode> NodeList = new List<CAStarNode>();

        bool FindPath(CLayerPart pActor, CWayPointMap pWayPointMap, uint StartWayPointIndex, uint EndWayPointIndex,
                 CPointList pFinalList, CCollideProcessingState pCollideProcessingState)
        {
            // here's the way it works
            // first we set the open and closed lists to nothing
            ReturnAllNodesToUnusedList(m_OpenNodes);
            ReturnAllNodesToUnusedList(m_ClosedNodes);

            //AssertNoLostNodes;

            // we add the StartWayPointIndex to the Open list
            CPathWayPoint pStartWayPoint = pWayPointMap.GetpWayPoint(StartWayPointIndex);
            CPathWayPoint pFinalDestWayPoint = pWayPointMap.GetpWayPoint(EndWayPointIndex);
            CAStarNode pNewNode = GetUnusedNode();
            pNewNode.Initialize(null, null, pStartWayPoint, StartWayPointIndex, pFinalDestWayPoint, 0, SegmentLengthFunc, CCollideOutputs::MIN_COST_RATIO_TO_TREAT_AS_IMPASSABLE, UclidianDistFunc);
            // put our first point in the open list
            m_OpenNodes.SetpNext(pNewNode);
            //AssertNoLostNodes;

            // we processe the open list until we fill up the open list, the closed list, or we find the end
            // if we found the end we return true, else we return false
            double RadiusOfDestPosSqr = pCollideProcessingState.GetRadiusOfDestPos();
            RadiusOfDestPosSqr = RadiusOfDestPosSqr * RadiusOfDestPosSqr;
            double OneOverYToXAspectRatioOfDestPosRadius = 1.f / pCollideProcessingState.GetYToXAspectRatioOfDestPosRadius();

            CAStarNode pBestNode = null;
            bool Done = false;
            while (!Done)
            {
                // we look in the open list and find the node that has the lowest m_TotalCostForThisNodeF
                pBestNode = FindBestNode();
                if (pBestNode != null)
                {
                    //AssertNoLostNodes;
                    CPathWayPoint pCurBestWayPoint = pBestNode.GetpCurWayPoint();
                    bool CloseEnough = false;
                    if (RadiusOfDestPosSqr > 0.f)
                    {
                        CFPoint FFinalDestPos, FCurBestPos;
                        FFinalDestPos.Set(pFinalDestWayPoint.GetPosition());
                        FCurBestPos.Set(pCurBestWayPoint.GetPosition());
                        double DistSqrd = GetDistSqrd(FFinalDestPos, FCurBestPos, OneOverYToXAspectRatioOfDestPosRadius);

                        if (DistSqrd < RadiusOfDestPosSqr)
                        {
                            CloseEnough = true;
                        }
                    }

                    // now that we have the best node lets see if it's the end
                    if (pCurBestWayPoint == pFinalDestWayPoint || CloseEnough)
                    {
                        Done = true;
                    }
                    else
                    {
                        // Process all the nodes that we can get to from this node (the best one).
                        // We will look at all the sub-waypoints of this one and check each
                        // against the open list and the closed list.
                        // If we find the node in the open list we check its m_TotalCostForThisNodeF if this one
                        // is lower than this path is better and we hook that node back to this one if not we move on.
                        // If we find it in the closed list we will not do anything with it.
                        // If we don't find it in either then we will add it to the open list and move on to the next one.
                        //AssertNoLostNodes;
                        if (!AddSubWayPointsToOpenList(pActor, pWayPointMap, pBestNode, pFinalDestWayPoint, pCollideProcessingState))
                        {
                            // put the node we just tried on the closed list
                            AddNodeToClosedList(pBestNode);
                            // We ran out of space on the open list.  We are done, there is no more space in the open list

                            //Show something on screen to indicate we failed 
                            // to find a path due to running out of points (search went on to long)					
                            // Commented out by JCS 6-30-01 because it shows up all the time even in non-bad cases
                            //pTheGame.ShowWarningMessage("CPathAStar::FindPath() failed to find a path (Ran out of points)... This is probably what is slowing things down...");

                            return false;
                        }

                        // We have arrived at this way point through a minimum path and
                        // added all its possible paths to the open list, it is done, add it to the closed list.
                        //AssertNoLostNodes;
                        AddNodeToClosedList(pBestNode);
                        //AssertNoLostNodes;
                    }
                }
                else
                {
                    // we could not find a best node because there are no more open points
                    // the path has no solution
                    return false;
                }
            }

            // So we are done and found a path to the dest.
            // Add all the positions to the pFinalList and return true
            // the list goes from the end to the start, so we need to add the list in the reverse order.
            NodeList.Clear();

            // store each pointer
            do
            {
                NodeList.Add(pBestNode);
                pBestNode = pBestNode.GetpParentNode();
            } while (pBestNode != null);

            // and add them from start to end (they were backwards)
            uint NumNodes = NodeList.GetNumItems();
            for (int i = NumNodes - 1; i >= 0; i--)
            {
                pFinalList.AddByVal(NodeList.GetItem(i).GetpCurWayPoint().GetPosition());
            }

            return true;
        }


        void EditorDraw(CFrameInterface pDest)
        {
            // draw all the items in the closed list
            CAStarNode* pCurNode = m_ClosedNodes.GetpNext();

            while (pCurNode)
            {
                pCurNode.EditorDraw(pDest);
                pCurNode = pCurNode.GetpNext();
            }

            pCurNode = m_OpenNodes.GetpNext();

            while (pCurNode)
            {
                pCurNode.EditorDraw(pDest);
                pCurNode = pCurNode.GetpNext();
            }
        }


        CAStarNode FindBestNode()
        {
            // Pick the node with lowest m_TotalCostForThisNodeF, in this case it's the first node in list
            // because we sort the m_OpenNodes list.
            return m_OpenNodes.GetpNext();
        }

        CAStarNode FindInOpenList(uint WayPointIndex)
        {
            CAStarNode pNodeToCheck = m_OpenNodes.GetpNext();
            while (pNodeToCheck != nul)l
            {
                if (pNodeToCheck.GetCurWayPointIndex() == WayPointIndex)
                {
                    return pNodeToCheck;
                }

                pNodeToCheck = pNodeToCheck.GetpNext();
            }

            return null;
        }

        CAStarNode FindInClosedList(uint WayPointIndex)
        {
            CAStarNode pNodeToCheck = m_ClosedNodes.GetpNext();
            while (pNodeToCheck != null)
            {
                if (pNodeToCheck.GetCurWayPointIndex() == WayPointIndex)
                {
                    return pNodeToCheck;
                }

                pNodeToCheck = pNodeToCheck.GetpNext();
            }

            return null;
        }

        void AddToOpenList(CAStarNode pNewNode)
        {
            CAStarNode pParentNode = m_OpenNodes;
            CAStarNode pNodeToCheck = m_OpenNodes.GetpNext();
            while (pNodeToCheck != null)
            {
                if (pNewNode.GetTotalCostF() < pNodeToCheck.GetTotalCostF())
                {
                    pParentNode.SetpNext(pNewNode);
                    pNewNode.SetpNext(pNodeToCheck);
                    return;
                }

                pParentNode = pNodeToCheck;
                pNodeToCheck = pNodeToCheck.GetpNext();
            }

            // it is the worst one in the list put it at the end
            //assert(pParentNode.GetpNext() == null);
            pParentNode.SetpNext(pNewNode);
            //assert(pNewNode.GetpNext() == null);
        }

        void RemoveFromOpenList(CAStarNode pNodeToRemove)
        {
            CAStarNode pParentNode = m_OpenNodes;
            CAStarNode pNodeToCheck = m_OpenNodes.GetpNext();
            while (pNodeToCheck != null)
            {
                if (pNodeToRemove == pNodeToCheck)
                {
                    pParentNode.SetpNext(pNodeToCheck.GetpNext());
                    pNodeToCheck.SetpNext(null);
                    return;
                }

                pParentNode = pNodeToCheck;
                pNodeToCheck = pNodeToCheck.GetpNext();
            }

            //assert(0);  // why are trying to remove an item that is not in the list
        }


        static double SegmentLengthFunc(CAStarNode  /*pParentNode*/ , CPathLineSegment pConnectingSegment, double CollisionCostRatio)
        {
            return pConnectingSegment.GetLength() * CollisionCostRatio;
        }

        static double UclidianDistFunc(CPathWayPoint pCurWayPoint, CPathWayPoint pFinalDestWayPoint)
        {
            return MMSqrt((double)(pCurWayPoint.GetPosition().GetDistSqrd(pFinalDestWayPoint.GetPosition())));
        }

        static double GetDistSqrd(CFPoint pPosition1, CFPoint pPosition2, double OneOverYToXRatio)
        {
            CFPoint Position1 = pPosition1;

            Position1 -= *pPosition2;

            Position1.y *= OneOverYToXRatio;
            // uses the aspect ratio so that it is right visually
            return Position1.x * Position1.x + Position1.y * Position1.y;
        }

        static bool IsCurrentlyPassable(CLayerPart pActor, CFPoint pPolyStartPoint, CFPoint pPolyEndPoint)
        {
            // Check if it is passable right now. LBB [2/26/2003]
            CCollideInputs CollideInputs(pPolyStartPoint, pPolyEndPoint);
            CCollideProcessingState CollideProcessingState;
            return !pActor.GetLayer().SaveData.CheckCollideLine(pPolyStartPoint, &CollideInputs, &CollideProcessingState, null, null, 0, 0, const_cast<CLayerPart*>(pActor));
        }

        double CalculatePathingCostMultiplyer(CLayerPart pActor, CPoint StartPoint, CPoint EndPoint, CCollideProcessingState pCollideProcessingState)
        {
            CFPoint PolyDelta, NormalPos, CenterPos;
            pActor.GetCollisionCenterPosition(&CenterPos);
            pActor.GetPosition(&NormalPos);
            PolyDelta.Minus(CenterPos, NormalPos);
            CFPoint FPolyStartPoint, FPolyEndPoint;
            FPolyStartPoint.Set(StartPoint);
            FPolyEndPoint.Set(EndPoint);
            FPolyStartPoint -= PolyDelta;
            FPolyEndPoint -= PolyDelta;

            CCollideInputs CollideInputs(&FPolyStartPoint, &FPolyEndPoint);

            uint IsTempExcluded = pActor.GetFlag(CLayerPart::IsTemporarilyExcluded);
            const_cast<CLayerPart*>(pActor).OrInFlags(CLayerPart::IsTemporarilyExcluded);

            // <WIP> pEntityMovable should not require a const cast here (many changes would have to be made)
            pCollideProcessingState.ResetPathingCostMultiplyer();
            bool Collision = pActor.GetLayer().SaveData.CheckCollide(pActor.GetpPolygon(0), &FPolyStartPoint, &CollideInputs, pCollideProcessingState, null, null, const_cast<CLayerPart*>(pActor), 0, 0);

            const_cast<CLayerPart*>(pActor).ClearFlags(CLayerPart::IsTemporarilyExcluded);
            const_cast<CLayerPart*>(pActor).OrInFlags(IsTempExcluded);

            if (Collision)
            {
                return CCollideOutputs::MIN_COST_RATIO_TO_TREAT_AS_IMPASSABLE;
            }
            else
            {
                return pCollideProcessingState.GetAndResetPathingCostMultiplyer();
            }
        }


        bool AddSubWayPointsToOpenList(CLayerPart pActor, CWayPointMap pWayPointMap, CAStarNode pParentNode,
                                     CPathWayPoint pFinalDestWayPoint, CCollideProcessingState pCollideProcessingState)
        {
            throw new NotImplementedException();
        }

        void AddNodeToClosedList(CAStarNode pNodeToClose)
        {
            RemoveFromOpenList(pNodeToClose);

            //assert(pNodeToClose.GetpNext() == null); // it should be in no list when you add it to the clossed
            pNodeToClose.SetpNext(m_ClosedNodes.GetpNext());
            m_ClosedNodes.SetpNext(pNodeToClose);
            //AssertNoLostNodes;
        }


        // <WIP> we may want to make these binary trees eventually
        // the open list would be sorted on the CostToThisNodeF
        // the closed list on WayPointIndex (or maybe we would keep two trees for open, one sorted on each) 
        // this is for debuging
        uint CountUnusedNodes()
        {
            uint Count = 0;
            CAStarNode pCurNode = m_UnusedNodes.GetpNext();

            while (pCurNode != null)
            {
                Count++;
                pCurNode = pCurNode.GetpNext();
            }

            return Count;
        }
        // this is for debuging
        uint CountOpenNodes()
        {
            uint Count = 0;
            CAStarNode pCurNode = m_OpenNodes.GetpNext();

            while (pCurNode)
            {
                Count++;
                pCurNode = pCurNode.GetpNext();
            }

            return Count;
        }

        // this is for debuging
        uint CountClosedNodes()
        {
            uint Count = 0;
            CAStarNode pCurNode = m_ClosedNodes.GetpNext();

            while (pCurNode != null)
            {
                Count++;
                pCurNode = pCurNode.GetpNext();
            }

            return Count;
        }

        // this is for debuging
        uint CountAllNodes()
        {
            return CountUnusedNodes() + CountOpenNodes() + CountClosedNodes();
        }

        CAStarNode GetUnusedNode()
        {
            //AssertNoLostNodes;
            CAStarNode pNodeToReturn = m_UnusedNodes.GetpNext();

            if (pNodeToReturn != null)
            {
                m_UnusedNodes.SetpNext(pNodeToReturn.GetpNext());
                pNodeToReturn.SetpNext(null);
                pNodeToReturn.Reset();
            }

            return pNodeToReturn;
        }

        void ReturnAllNodesToUnusedList(CAStarNode pParentOfUnusedNode)
        {
            //AssertNoLostNodes();
            CAStarNode pFirstNodeToReturn = pParentOfUnusedNode.GetpNext();
            if (pFirstNodeToReturn != null)
            {
                CAStarNode pLastNodeToReturn = pFirstNodeToReturn;

                // if the unused list has stuff in it already
                if (m_UnusedNodes.GetpNext())
                {
                    // find the last node we want to return
                    while (pLastNodeToReturn.GetpNext())
                    {
                        pLastNodeToReturn = pLastNodeToReturn.GetpNext();
                    }

                    // set the last node to return next to the first node of the unused list
                    pLastNodeToReturn.SetpNext(m_UnusedNodes.GetpNext());
                    // set the first node of the unused list to the first node to return
                    m_UnusedNodes.SetpNext(pFirstNodeToReturn);
                    // make sure the list we took them from doesn't still have them
                    pParentOfUnusedNode.SetpNext(null);
                    //AssertNoLostNodes;
                }
                else
                {
                    // just put it on the unused list
                    m_UnusedNodes.SetpNext(pFirstNodeToReturn);
                    pParentOfUnusedNode.SetpNext(null);
                    //AssertNoLostNodes;
                }
            }

            //AssertNoLostNodes;
        }


        CAStarNode m_UnusedNodes;
    CAStarNode m_OpenNodes;
    CAStarNode m_ClosedNodes;
}

/*
bool CPathAStar::AddSubWayPointsToOpenList(const CLayerPart* pActor, CWayPointMap* pWayPointMap, CAStarNode* pParentNode,
                                           CPathWayPoint* pFinalDestWayPoint, const CCollideProcessingState* pCollideProcessingState)
{
    CPathWayPoint* pParentWayPoint = pParentNode.GetpCurWayPoint();
    uint NumPathSegments = pParentWayPoint.GetNumPathSegments();
    for (uint i = 0; i < NumPathSegments; i++)
    {
        AssertNoLostNodes;
        CPathLineSegment pCurLineSegment = pWayPointMap.GetpPathSegment(pParentWayPoint.GetPathSegmentIndex(i));
        uint CurWayPointIndex = pCurLineSegment.GetEndWayPointIndex(pParentNode.GetCurWayPointIndex());

        // check if the EndWayPointIndex is in the closed list
        CAStarNode pClosedListCurNode = FindInClosedList(CurWayPointIndex);

        // we only do processing to way points that are not closed
        if (!pClosedListCurNode)
        {
            CPathWayPoint pCurWayPoint = pWayPointMap.GetpWayPoint(CurWayPointIndex);

            if (pCurLineSegment.GetPassableType() == CPathLineSegment::REQUIRES_CHECK)
            {
                // figure out if it is possible to ever move over this way point
                CFPoint FPolyStartPoint, FPolyEndPoint;
                FPolyStartPoint.Set(pParentWayPoint.GetPosition());
                FPolyEndPoint.Set(pCurWayPoint.GetPosition());

                CCollideInputs CollideInputs(&FPolyStartPoint, &FPolyEndPoint);
    CCollideProcessingState CollideProcessingState;
    CollideProcessingState.SetCheckingIsEverPotentialyPassable();

    uint IsTempExcluded = pActor.GetFlag(CLayerPart::IsTemporarilyExcluded);
    const_cast<CLayerPart*>(pActor).OrInFlags(CLayerPart::IsTemporarilyExcluded);

    bool PathTotallyBlocked = pActor.GetLayer().SaveData.CheckCollideLine(&FPolyStartPoint, &CollideInputs, &CollideProcessingState, null, null, 0, 0, const_cast<CLayerPart*>(pActor));
    if (PathTotallyBlocked)
    {
        pCurLineSegment.SetPassableType(CPathLineSegment::ALWAYS_BLOCKED);
    }
    else
    {
        // Check if it is passable right now. LBB [2/26/2003]
        CCollideProcessingState CollideProcessingState;
        bool PathBlockedNow = pActor.GetLayer().SaveData.CheckCollideLine(&FPolyStartPoint, &CollideInputs, &CollideProcessingState, null, null, 0, 0, const_cast<CLayerPart*>(pActor));
        if (PathBlockedNow)
        {
            pCurLineSegment.SetPassableType(CPathLineSegment::SOMETIMES_BLOCKED);
        }
        else
        {
            pCurLineSegment.SetPassableType(CPathLineSegment::ALWAYS_PASSABLE);
        }
    }

    const_cast<CLayerPart*>(pActor).ClearFlags(CLayerPart::IsTemporarilyExcluded);
    const_cast<CLayerPart*>(pActor).OrInFlags(IsTempExcluded);
}

CPathLineSegment::CPassableType CurLinePassableType = pCurLineSegment.GetPassableType();
			if(CurLinePassableType != CPathLineSegment::ALWAYS_BLOCKED)
			{
				CFPoint FPolyStartPoint, FPolyEndPoint;
FPolyStartPoint.Set(pParentWayPoint.GetPosition());
				FPolyEndPoint.Set(pCurWayPoint.GetPosition());

				if(CurLinePassableType == CPathLineSegment::ALWAYS_PASSABLE
					|| ( CurLinePassableType == CPathLineSegment::SOMETIMES_BLOCKED
							&& IsCurrentlyPassable(pActor, &FPolyStartPoint, & FPolyEndPoint) ) )
				{
					// only try to move on lines that are at least somewhat Passable
					// see if it is possible to get to the next way point (is there a collison)
					double CollisonCostRatio = CalculatePathingCostMultiplyer(pActor,
                        pParentWayPoint.GetPosition(), pCurWayPoint.GetPosition(),
                        pCollideProcessingState);

CAStarNode* pNewNode = GetUnusedNode();

double ERROR_RANGE = .5f;
					if(CollisonCostRatio<CCollideOutputs::MIN_COST_RATIO_TO_TREAT_AS_IMPASSABLE - ERROR_RANGE)
					{
						// weather it's in the open list or not we need to figure out the node info
						pNewNode.Initialize(pParentNode, pCurLineSegment, pCurWayPoint, CurWayPointIndex,
                            pFinalDestWayPoint, pParentNode.GetAccumulatedCostFromStartG(), SegmentLengthFunc, CollisonCostRatio, UclidianDistFunc);
						
						// check if the CurWayPointIndex is in the open list
						CAStarNode* pOpenListCurNode = FindInOpenList(CurWayPointIndex);
						if(pOpenListCurNode)
						{
							// check to see if this move is better than the one that is already to it
							if(pNewNode.GetTotalCostF() < pOpenListCurNode.GetTotalCostF())
							{
								// if it is set the parent pointer to the pCurParentNode and 
								// fix the other data.
								pOpenListCurNode.Initialize(pParentNode, pCurLineSegment, pCurWayPoint, CurWayPointIndex,
                                    pFinalDestWayPoint, pParentNode.GetAccumulatedCostFromStartG(), SegmentLengthFunc, CollisonCostRatio, UclidianDistFunc);

                                // and make sure it is sorted in the list correctly
                                RemoveFromOpenList(pOpenListCurNode);
                                AddToOpenList(pOpenListCurNode);
// put the new node back on the unused list
pNewNode.SetpNext(m_UnusedNodes.GetpNext());
								m_UnusedNodes.SetpNext(pNewNode);
								AssertNoLostNodes;
								pNewNode = null;
							}
							else
							{
								// put it back into the unused list
								pNewNode.SetpNext(m_UnusedNodes.GetpNext());
								m_UnusedNodes.SetpNext(pNewNode);
								AssertNoLostNodes;
							}
							AssertNoLostNodes;
						}
						else
						{
                            // put it in the open list at the right position
                            AddToOpenList(pNewNode);
AssertNoLostNodes;
							
							// If we are out of unused nodes
							if(m_UnusedNodes.GetpNext() == null)
							{
								return false;
							}
							AssertNoLostNodes;
						}

						AssertNoLostNodes;
					}
					else
					{
						// put it back into the unused list
						pNewNode.SetpNext(m_UnusedNodes.GetpNext());
						m_UnusedNodes.SetpNext(pNewNode);
						AssertNoLostNodes;
					}
				}
			}

			AssertNoLostNodes;
		}
		else
		{
			// This node is in the closed list.  We have already found the cheepest way
			// to get to it.
			AssertNoLostNodes;
		}

		AssertNoLostNodes;
	}

	AssertNoLostNodes;
	return true;
}
*/
}