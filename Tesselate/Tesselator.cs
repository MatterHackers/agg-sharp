/*
** License Applicability. Except to the extent portions of this file are
** made subject to an alternative license as permitted in the SGI Free
** Software License B, Version 1.1 (the "License"), the contents of this
** file are subject only to the provisions of the License. You may not use
** this file except in compliance with the License. You may obtain a copy
** of the License at Silicon Graphics, Inc., attn: Legal Services, 1600
** Amphitheatre Parkway, Mountain View, CA 94043-1351, or at:
**
** http://oss.sgi.com/projects/FreeB
**
** Note that, as provided in the License, the Software is distributed on an
** "AS IS" basis, with ALL EXPRESS AND IMPLIED WARRANTIES AND CONDITIONS
** DISCLAIMED, INCLUDING, WITHOUT LIMITATION, ANY IMPLIED WARRANTIES AND
** CONDITIONS OF MERCHANTABILITY, SATISFACTORY QUALITY, FITNESS FOR A
** PARTICULAR PURPOSE, AND NON-INFRINGEMENT.
**
** Original Code. The Original Code is: OpenGL Sample Implementation,
** Version 1.2.1, released January 26, 2000, developed by Silicon Graphics,
** Inc. The Original Code is Copyright (c) 1991-2000 Silicon Graphics, Inc.
** Copyright in any portions created by third parties is as indicated
** elsewhere herein. All Rights Reserved.
**
** Additional Notice Provisions: The application programming interfaces
** established by SGI in conjunction with the Original Code are The
** OpenGL(R) Graphics System: A Specification (Version 1.2.1), released
** April 1, 1999; The OpenGL(R) Graphics System Utility Library (Version
** 1.3), released November 4, 1998; and OpenGL(R) Graphics with the X
** Window System(R) (Version 1.3), released October 19, 1998. This software
** was created using the OpenGL(R) version 1.2.1 Sample Implementation
** published by SGI, but has not been independently verified as being
** compliant with the OpenGL(R) version 1.2.1 Specification.
**
*/
/*
** Author: Eric Veach, July 1994.
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
**
*/

using System;
using C5;

namespace Tesselate
{
	public class Tesselator
	{
		public const double MAX_COORD = 1.0e150;

		public int cacheCount;

		public Dictionary edgeDictionary;

		public Face lonelyTriList;

		public Mesh mesh;

		public VertexAndIndex[] simpleVertexCache = new VertexAndIndex[MAX_CACHE_SIZE];

		public WindingRuleType windingRule;

		// We cache vertex data for single-contour polygons so that we can
		// try a quick-and-dirty decomposition first.
		private const int MAX_CACHE_SIZE = 100;

		private bool boundaryOnly;

		private bool emptyCache;

		private HalfEdge lastHalfEdge;

		private ProcessingState processingState;

		public Tesselator()
		{
			/* Only initialize fields which can be changed by the api.  Other fields
			* are initialized where they are used.
			*/

			this.processingState = ProcessingState.Dormant;

			this.windingRule = WindingRuleType.NonZero;
			this.boundaryOnly = false;
		}

		~Tesselator()
		{
			RequireState(ProcessingState.Dormant);
		}

		public Action<TriangleListType> callBegin;

		public Func<double[], int[], double[], int> callCombine;

		public Action<bool> callEdgeFlag;

		public Action callEnd;

		public Action<Mesh> callMesh;

		public Action<int> callVertex;

		public enum TriangleListType
		{
			LineLoop,
			Triangles,
			TriangleStrip,
			TriangleFan
		}

		public enum WindingRuleType
		{
			Odd,
			NonZero,
			Positive,
			Negative,
			ABS_GEQ_Two,
		}

		// The begin/end calls must be properly nested.  We keep track of
		// the current state to enforce the ordering.
		private enum ProcessingState
		{
			Dormant, InPolygon, InContour
		};

		public bool BoundaryOnly
		{
			get { return this.boundaryOnly; }
			set { this.boundaryOnly = value; }
		}

		public bool EdgeCallBackSet
		{
			get
			{
				return callEdgeFlag != null;
			}
		}

		public WindingRuleType WindingRule
		{
			get { return this.windingRule; }
			set { this.windingRule = value; }
		}

		public IntervalHeap<ContourVertex> VertexPriorityQue { get; set; }

		public ContourVertex CurrentSweepVertex { get; set; }

		public void AddVertex(double[] coords2, int clientIndex)
		{
			RequireState(ProcessingState.InContour);

			if (emptyCache)
			{
				EmptyCache();
				lastHalfEdge = null;
			}

			if (mesh == null)
			{
				if (cacheCount < MAX_CACHE_SIZE)
				{
					CacheVertex(coords2, clientIndex);
					return;
				}
				EmptyCache();
			}

			AddVertex(coords2[0], coords2[1], clientIndex);
		}

		public void BeginContour()
		{
			RequireState(ProcessingState.InPolygon);

			processingState = ProcessingState.InContour;
			lastHalfEdge = null;
			if (cacheCount > 0)
			{
				// Just set a flag so we don't get confused by empty contours
				emptyCache = true;
			}
		}

		public virtual void BeginPolygon()
		{
			RequireState(ProcessingState.Dormant);

			processingState = ProcessingState.InPolygon;
			cacheCount = 0;
			emptyCache = false;
			mesh = null;
		}

		public void CallBegin(TriangleListType triangleType)
		{
			callBegin?.Invoke(triangleType);
		}

		public void CallCombine(double[] coords3, int[] data4,
			double[] weight4, out int outData)
		{
			outData = 0;
			if (callCombine != null)
			{
				outData = callCombine(coords3, data4, weight4);
			}
		}

		public void CallEdgeFlag(bool edgeState)
		{
			callEdgeFlag?.Invoke(edgeState);
		}

		public void CallEnd()
		{
			callEnd?.Invoke();
		}

		public void CallVertex(int vertexData)
		{
			callVertex?.Invoke(vertexData);
		}

		public void EndContour()
		{
			RequireState(ProcessingState.InContour);
			processingState = ProcessingState.InPolygon;
		}

		public void EndPolygon()
		{
			RequireState(ProcessingState.InPolygon);
			processingState = ProcessingState.Dormant;

			if (this.mesh == null)
			{
				if (!this.EdgeCallBackSet && this.callMesh == null)
				{
					/* Try some special code to make the easy cases go quickly
					* (eg. convex polygons).  This code does NOT handle multiple contours,
					* intersections, edge flags, and of course it does not generate
					* an explicit mesh either.
					*/
					if (RenderCache())
					{
						return;
					}
				}

				EmptyCache(); /* could've used a label*/
			}

			/* Determine the polygon normal and project vertices onto the plane
			* of the polygon.
			*/
			ProjectPolygon();

			/* __gl_computeInterior( this ) computes the planar arrangement specified
			* by the given contours, and further subdivides this arrangement
			* into regions.  Each region is marked "inside" if it belongs
			* to the polygon, according to the rule given by this.windingRule.
			* Each interior region is guaranteed to be monotone.
			*/
			ActiveRegion.ComputeInterior(this);

			bool rc = true;

			/* If the user wants only the boundary contours, we throw away all edges
			* except those which separate the interior from the exterior.
			* Otherwise we tessellate all the regions marked "inside".
			*/
			if (this.boundaryOnly)
			{
				rc = this.mesh.SetWindingNumber(1, true);
			}
			else
			{
				rc = this.mesh.TessellateInterior();
			}

			//this.mesh.CheckMesh();

			if (this.callBegin != null || this.callEnd != null
				|| this.callVertex != null || this.callEdgeFlag != null)
			{
				if (this.boundaryOnly)
				{
					RenderBoundary(mesh);  /* output boundary contours */
				}
				else
				{
					RenderMesh(mesh);      /* output strips and fans */
				}
			}
			if (this.callMesh != null)
			{
				/* Throw away the exterior faces, so that all faces are interior.
				* This way the user doesn't have to check the "inside" flag,
				* and we don't need to even reveal its existence.  It also leaves
				* the freedom for an implementation to not generate the exterior
				* faces in the first place.
				*/
				mesh.DiscardExterior();
				callMesh(mesh); /* user wants the mesh itself */
				this.mesh = null;
				return;
			}
			this.mesh = null;
		}

		public bool IsWindingInside(int numCrossings)
		{
			switch (this.windingRule)
			{
				case WindingRuleType.Odd:
					return (numCrossings & 1) != 0;

				case WindingRuleType.NonZero:
					return (numCrossings != 0);

				case WindingRuleType.Positive:
					return (numCrossings > 0);

				case WindingRuleType.Negative:
					return (numCrossings < 0);

				case WindingRuleType.ABS_GEQ_Two:
					return (numCrossings >= 2) || (numCrossings <= -2);
			}

			throw new Exception();
		}

		private bool AddVertex(double x, double y, int clientIndex)
		{
			HalfEdge e;

			e = this.lastHalfEdge;
			if (e == null)
			{
				/* Make a self-loop (one vertex, one edge). */
				e = this.mesh.MakeEdge();
				Mesh.meshSplice(e, e.otherHalfOfThisEdge);
			}
			else
			{
				/* Create a new vertex and edge which immediately follow e
				* in the ordering around the left face.
				*/
				if (Mesh.meshSplitEdge(e) == null)
				{
					return false;
				}
				e = e.nextEdgeCCWAroundLeftFace;
			}

			/* The new vertex is now e.Org. */
			e.originVertex.ClientIndex = clientIndex;
			e.originVertex.coords[0] = x;
			e.originVertex.coords[1] = y;

			/* The winding of an edge says how the winding number changes as we
			* cross from the edge's right face to its left face.  We add the
			* vertices in such an order that a CCW contour will add +1 to
			* the winding number of the region inside the contour.
			*/
			e.winding = 1;
			e.otherHalfOfThisEdge.winding = -1;

			this.lastHalfEdge = e;

			return true;
		}

		private void CacheVertex(double[] coords2, int clientIndex)
		{
			this.simpleVertexCache[this.cacheCount].ClientIndex = clientIndex;
			this.simpleVertexCache[this.cacheCount].x = coords2[0];
			this.simpleVertexCache[this.cacheCount].y = coords2[1];
			++this.cacheCount;
		}

		private void CheckOrientation()
		{
			double area;
			Face curFace, faceHead = this.mesh.faceHead;
			ContourVertex vHead = this.mesh.vertexHead;
			HalfEdge curHalfEdge;

			/* When we compute the normal automatically, we choose the orientation
			 * so that the sum of the signed areas of all contours is non-negative.
			 */
			area = 0;
			for (curFace = faceHead.nextFace; curFace != faceHead; curFace = curFace.nextFace)
			{
				curHalfEdge = curFace.halfEdgeThisIsLeftFaceOf;
				if (curHalfEdge.winding <= 0)
				{
					continue;
				}

				do
				{
					area += (curHalfEdge.originVertex.x - curHalfEdge.directionVertex.x)
						* (curHalfEdge.originVertex.y + curHalfEdge.directionVertex.y);
					curHalfEdge = curHalfEdge.nextEdgeCCWAroundLeftFace;
				} while (curHalfEdge != curFace.halfEdgeThisIsLeftFaceOf);
			}

			if (area < 0)
			{
				/* Reverse the orientation by flipping all the t-coordinates */
				for (ContourVertex curVertex = vHead.nextVertex; curVertex != vHead; curVertex = curVertex.nextVertex)
				{
					curVertex.y = -curVertex.y;
				}
			}
		}

		private void EmptyCache()
		{
			VertexAndIndex[] v = this.simpleVertexCache;

			this.mesh = new Mesh();

			for (int i = 0; i < this.cacheCount; i++)
			{
				this.AddVertex(v[i].x, v[i].y, v[i].ClientIndex);
			}
			this.cacheCount = 0;
			this.emptyCache = false;
		}

		private void GotoState(ProcessingState newProcessingState)
		{
			while (this.processingState != newProcessingState)
			{
				/* We change the current state one level at a time, to get to
				* the desired state.
				*/
				if (this.processingState < newProcessingState)
				{
					switch (this.processingState)
					{
						case ProcessingState.Dormant:
							throw new Exception("MISSING_BEGIN_POLYGON");

						case ProcessingState.InPolygon:
							throw new Exception("MISSING_BEGIN_CONTOUR");

						default:
							break;
					}
				}
				else
				{
					switch (this.processingState)
					{
						case ProcessingState.InContour:
							throw new Exception("MISSING_END_CONTOUR");

						case ProcessingState.InPolygon:
							throw new Exception("MISSING_END_POLYGON");

						default:
							break;
					}
				}
			}
		}

		private void ProjectPolygon()
		{
			ContourVertex v, vHead = this.mesh.vertexHead;

			// Project the vertices onto the sweep plane
			for (v = vHead.nextVertex; v != vHead; v = v.nextVertex)
			{
				v.x = v.coords[0];
				v.y = -v.coords[1];
			}

			CheckOrientation();
		}

		private void RequireState(ProcessingState state)
		{
			if (this.processingState != state)
			{
				GotoState(state);
			}
		}

		public struct Vertex
		{
			public double x;
			public double y;
		}

		public struct VertexAndIndex
		{
			public int ClientIndex;
			public double x;
			public double y;
		}

		/* what begin/end calls have we seen? */
		/* lastEdge.Org is the most recent vertex */
		/* stores the input contours, and eventually the tessellation itself */

		// rule for determining polygon interior

		/* edge dictionary for sweep line */
		/* current sweep event being processed */
		/*** state needed for rendering callbacks (see render.c) ***/

		/* Extract contours, not triangles */
		/* list of triangles which could not be rendered as strips or fans */
		/*** state needed to cache single-contour polygons for renderCache() */

		/* empty cache on next vertex() call */
		/* number of cached vertices */
		/* the vertex data */

		#region CodeFromRender

		private const int SIGN_INCONSISTENT = 2;

		public void RenderBoundary(Mesh mesh)
		{
			for (Face curFace = mesh.faceHead.nextFace; curFace != mesh.faceHead; curFace = curFace.nextFace)
			{
				if (curFace.isInterior)
				{
					this.CallBegin(TriangleListType.LineLoop);
					HalfEdge curHalfEdge = curFace.halfEdgeThisIsLeftFaceOf;
					do
					{
						this.CallVertex(curHalfEdge.originVertex.ClientIndex);
						curHalfEdge = curHalfEdge.nextEdgeCCWAroundLeftFace;
					} while (curHalfEdge != curFace.halfEdgeThisIsLeftFaceOf);
					this.CallEnd();
				}
			}
		}

		public bool RenderCache()
		{
			VertexAndIndex[] vCache = this.simpleVertexCache;
			VertexAndIndex v0 = vCache[0];
			double[] norm3 = new double[3];
			int sign;

			if (this.cacheCount < 3)
			{
				/* Degenerate contour -- no output */
				return true;
			}

			norm3[0] = 0;
			norm3[1] = 0;
			norm3[2] = 1;

			sign = this.ComputeNormal(norm3);
			if (sign == SIGN_INCONSISTENT)
			{
				// Fan triangles did not have a consistent orientation
				return false;
			}
			if (sign == 0)
			{
				// All triangles were degenerate
				return true;
			}

			/* Make sure we do the right thing for each winding rule */
			switch (this.windingRule)
			{
				case WindingRuleType.Odd:
				case WindingRuleType.NonZero:
					break;

				case WindingRuleType.Positive:
					if (sign < 0) return true;
					break;

				case WindingRuleType.Negative:
					if (sign > 0) return true;
					break;

				case WindingRuleType.ABS_GEQ_Two:
					return true;
			}

			this.CallBegin(this.BoundaryOnly ? TriangleListType.LineLoop
				: (this.cacheCount > 3) ? TriangleListType.TriangleFan
				: TriangleListType.Triangles);

			this.CallVertex(v0.ClientIndex);
			if (sign > 0)
			{
				for (int vcIndex = 1; vcIndex < this.cacheCount; ++vcIndex)
				{
					this.CallVertex(vCache[vcIndex].ClientIndex);
				}
			}
			else
			{
				for (int vcIndex = this.cacheCount - 1; vcIndex > 0; --vcIndex)
				{
					this.CallVertex(vCache[vcIndex].ClientIndex);
				}
			}
			this.CallEnd();
			return true;
		}

		public void RenderMesh(Mesh mesh)
		{
			Face f;

			/* Make a list of separate triangles so we can render them all at once */
			this.lonelyTriList = null;

			for (f = mesh.faceHead.nextFace; f != mesh.faceHead; f = f.nextFace)
			{
				f.marked = false;
			}
			for (f = mesh.faceHead.nextFace; f != mesh.faceHead; f = f.nextFace)
			{
				/* We examine all faces in an arbitrary order.  Whenever we find
				* an unprocessed face F, we output a group of faces including F
				* whose size is maximum.
				*/
				if (f.isInterior && !f.marked)
				{
					RenderMaximumFaceGroup(f);
					if (!f.marked)
					{
						throw new Exception();
					}
				}
			}
			if (this.lonelyTriList != null)
			{
				RenderLonelyTriangles(this.lonelyTriList);
				this.lonelyTriList = null;
			}
		}

		private static void RenderFan(Tesselator tess, HalfEdge e, int size)
		{
			/* Render as many CCW triangles as possible in a fan starting from
			* edge "e".  The fan *should* contain exactly "size" triangles
			* (otherwise we've goofed up somewhere).
			*/
			tess.CallBegin(TriangleListType.TriangleFan);
			tess.CallVertex(e.originVertex.ClientIndex);
			tess.CallVertex(e.directionVertex.ClientIndex);

			while (!e.leftFace.Marked())
			{
				e.leftFace.marked = true;
				--size;
				e = e.nextEdgeCCWAroundOrigin;
				tess.CallVertex(e.directionVertex.ClientIndex);
			}

			if (size != 0)
			{
				throw new Exception();
			}
			tess.CallEnd();
		}

		private static void RenderStrip(Tesselator tess, HalfEdge halfEdge, int size)
		{
			/* Render as many CCW triangles as possible in a strip starting from
			* edge "e".  The strip *should* contain exactly "size" triangles
			* (otherwise we've goofed up somewhere).
			*/
			tess.CallBegin(TriangleListType.TriangleStrip);
			tess.CallVertex(halfEdge.originVertex.ClientIndex);
			tess.CallVertex(halfEdge.directionVertex.ClientIndex);

			while (!halfEdge.leftFace.Marked())
			{
				halfEdge.leftFace.marked = true;
				--size;
				halfEdge = halfEdge.Dprev;
				tess.CallVertex(halfEdge.originVertex.ClientIndex);
				if (halfEdge.leftFace.Marked()) break;

				halfEdge.leftFace.marked = true;
				--size;
				halfEdge = halfEdge.nextEdgeCCWAroundOrigin;
				tess.CallVertex(halfEdge.directionVertex.ClientIndex);
			}

			if (size != 0)
			{
				throw new Exception();
			}
			tess.CallEnd();
		}

		private int ComputeNormal(double[] norm3)
		/*
		* Check that each triangle in the fan from v0 has a
		* consistent orientation with respect to norm3[].  If triangles are
		* consistently oriented CCW, return 1; if CW, return -1; if all triangles
		* are degenerate return 0; otherwise (no consistent orientation) return
		* SIGN_INCONSISTENT.
		*/
		{
			VertexAndIndex[] vCache = this.simpleVertexCache;
			VertexAndIndex v0 = vCache[0];
			int vcIndex;
			double dot, xc, yc, xp, yp;
			double[] n = new double[3];
			int sign = 0;

			/* Find the polygon normal.  It is important to get a reasonable
			* normal even when the polygon is self-intersecting (eg. a bowtie).
			* Otherwise, the computed normal could be very tiny, but perpendicular
			* to the true plane of the polygon due to numerical noise.  Then all
			* the triangles would appear to be degenerate and we would incorrectly
			* decompose the polygon as a fan (or simply not render it at all).
			*
			* We use a sum-of-triangles normal algorithm rather than the more
			* efficient sum-of-trapezoids method (used in CheckOrientation()
			* in normal.c).  This lets us explicitly reverse the signed area
			* of some triangles to get a reasonable normal in the self-intersecting
			* case.
			*/
			vcIndex = 1;
			xc = vCache[vcIndex].x - v0.x;
			yc = vCache[vcIndex].y - v0.y;
			while (++vcIndex < this.cacheCount)
			{
				xp = xc; yp = yc;
				xc = vCache[vcIndex].x - v0.x;
				yc = vCache[vcIndex].y - v0.y;

				/* Compute (vp - v0) cross (vc - v0) */
				n[0] = 0;
				n[1] = 0;
				n[2] = xp * yc - yp * xc;

				dot = n[0] * norm3[0] + n[1] * norm3[1] + n[2] * norm3[2];
				if (dot != 0)
				{
					/* Check the new orientation for consistency with previous triangles */
					if (dot > 0)
					{
						if (sign < 0)
						{
							return SIGN_INCONSISTENT;
						}
						sign = 1;
					}
					else
					{
						if (sign > 0)
						{
							return SIGN_INCONSISTENT;
						}
						sign = -1;
					}
				}
			}

			return sign;
		}

		private bool IsEven(int n)
		{
			return (((n) & 1) == 0);
		}

		private FaceCount MaximumFan(HalfEdge eOrig)
		{
			/* eOrig.Lface is the face we want to render.  We want to find the size
			* of a maximal fan around eOrig.Org.  To do this we just walk around
			* the origin vertex as far as possible in both directions.
			*/
			FaceCount newFace = new FaceCount(0, null, RenderFan);
			Face trail = null;
			HalfEdge e;

			for (e = eOrig; !e.leftFace.Marked(); e = e.nextEdgeCCWAroundOrigin)
			{
				Face.AddToTrail(ref e.leftFace, ref trail);
				++newFace.size;
			}
			for (e = eOrig; !e.rightFace.Marked(); e = e.Oprev)
			{
				Face f = e.rightFace;
				Face.AddToTrail(ref f, ref trail);
				e.rightFace = f;
				++newFace.size;
			}
			newFace.eStart = e;

			Face.FreeTrail(ref trail);
			return newFace;
		}

		private FaceCount MaximumStrip(HalfEdge eOrig)
		{
			/* Here we are looking for a maximal strip that contains the vertices
			* eOrig.Org, eOrig.Dst, eOrig.Lnext.Dst (in that order or the
			* reverse, such that all triangles are oriented CCW).
			*
			* Again we walk forward and backward as far as possible.  However for
			* strips there is a twist: to get CCW orientations, there must be
			* an *even* number of triangles in the strip on one side of eOrig.
			* We walk the strip starting on a side with an even number of triangles;
			* if both side have an odd number, we are forced to shorten one side.
			*/
			FaceCount newFace = new FaceCount(0, null, RenderStrip);
			int headSize = 0, tailSize = 0;
			Face trail = null;
			HalfEdge e, eTail, eHead;

			for (e = eOrig; !e.leftFace.Marked(); ++tailSize, e = e.nextEdgeCCWAroundOrigin)
			{
				Face.AddToTrail(ref e.leftFace, ref trail);
				++tailSize;
				e = e.Dprev;
				if (e.leftFace.Marked()) break;
				Face.AddToTrail(ref e.leftFace, ref trail);
			}
			eTail = e;

			for (e = eOrig; !e.rightFace.Marked(); ++headSize, e = e.Dnext)
			{
				Face f = e.rightFace;
				Face.AddToTrail(ref f, ref trail);
				e.rightFace = f;
				++headSize;
				e = e.Oprev;
				if (e.rightFace.Marked()) break;
				f = e.rightFace;
				Face.AddToTrail(ref f, ref trail);
				e.rightFace = f;
			}
			eHead = e;

			newFace.size = tailSize + headSize;
			if (IsEven(tailSize))
			{
				newFace.eStart = eTail.otherHalfOfThisEdge;
			}
			else if (IsEven(headSize))
			{
				newFace.eStart = eHead;
			}
			else
			{
				/* Both sides have odd length, we must shorten one of them.  In fact,
				* we must start from eHead to guarantee inclusion of eOrig.Lface.
				*/
				--newFace.size;
				newFace.eStart = eHead.nextEdgeCCWAroundOrigin;
			}

			Face.FreeTrail(ref trail);
			return newFace;
		}

		private void RenderLonelyTriangles(Face f)
		{
			/* Now we render all the separate triangles which could not be
			* grouped into a triangle fan or strip.
			*/
			HalfEdge e;
			bool newState = false;
			bool edgeState = false; /* force edge state output for first vertex */
			bool sentFirstEdge = false;

			this.CallBegin(TriangleListType.Triangles);

			for (; f != null; f = f.trail)
			{
				/* Loop once for each edge (there will always be 3 edges) */

				e = f.halfEdgeThisIsLeftFaceOf;
				do
				{
					if (this.EdgeCallBackSet)
					{
						/* Set the "edge state" to TRUE just before we output the
						* first vertex of each edge on the polygon boundary.
						*/
						newState = !e.rightFace.isInterior;
						if (edgeState != newState || !sentFirstEdge)
						{
							sentFirstEdge = true;
							edgeState = newState;
							this.CallEdgeFlag(edgeState);
						}
					}

					this.CallVertex(e.originVertex.ClientIndex);

					e = e.nextEdgeCCWAroundLeftFace;
				} while (e != f.halfEdgeThisIsLeftFaceOf);
			}

			this.CallEnd();
		}

		private void RenderMaximumFaceGroup(Face fOrig)
		{
			/* We want to find the largest triangle fan or strip of unmarked faces
			* which includes the given face fOrig.  There are 3 possible fans
			* passing through fOrig (one centered at each vertex), and 3 possible
			* strips (one for each CCW permutation of the vertices).  Our strategy
			* is to try all of these, and take the primitive which uses the most
			* triangles (a greedy approach).
			*/
			HalfEdge e = fOrig.halfEdgeThisIsLeftFaceOf;
			FaceCount max = new FaceCount(1, e, RenderTriangle);
			FaceCount newFace;

			max.size = 1;
			max.eStart = e;

			if (!this.EdgeCallBackSet)
			{
				newFace = MaximumFan(e); if (newFace.size > max.size) { max = newFace; }
				newFace = MaximumFan(e.nextEdgeCCWAroundLeftFace); if (newFace.size > max.size) { max = newFace; }
				newFace = MaximumFan(e.Lprev); if (newFace.size > max.size) { max = newFace; }

				newFace = MaximumStrip(e); if (newFace.size > max.size) { max = newFace; }
				newFace = MaximumStrip(e.nextEdgeCCWAroundLeftFace); if (newFace.size > max.size) { max = newFace; }
				newFace = MaximumStrip(e.Lprev); if (newFace.size > max.size) { max = newFace; }
			}

			max.CallRender(this, max.eStart, max.size);
		}

		private void RenderTriangle(Tesselator tess, HalfEdge e, int size)
		{
			/* Just add the triangle to a triangle list, so we can render all
			* the separate triangles at once.
			*/
			if (size != 1)
			{
				throw new Exception();
			}
			Face.AddToTrail(ref e.leftFace, ref this.lonelyTriList);
		}

		private class FaceCount
		{
			public HalfEdge eStart;

			public int size;

			public FaceCount(int _size, HalfEdge _eStart, Action<Tesselator, HalfEdge, int> _render)
			{
				size = _size;
				eStart = _eStart;
				render = _render;
			}

			/* number of triangles used */
			/* edge where this primitive starts */

			private Action<Tesselator, HalfEdge, int> render;

			// routine to render this primitive

			public void CallRender(Tesselator tess, HalfEdge edge, int data)
			{
				render(tess, edge, data);
			}
		}

		/************************ Strips and Fans decomposition ******************/

		/* __gl_renderMesh( tess, mesh ) takes a mesh and breaks it into triangle
		* fans, strips, and separate triangles.  A substantial effort is made
		* to use as few rendering primitives as possible (ie. to make the fans
		* and strips as large as possible).
		*
		* The rendering output is provided as callbacks (see the api).
		*/
		/************************ Boundary contour decomposition ******************/

		/* Takes a mesh, and outputs one
		* contour for each face marked "inside".  The rendering output is
		* provided as callbacks.
		*/
		/************************ Quick-and-dirty decomposition ******************/
		/* Takes a single contour and tries to render it
		* as a triangle fan.  This handles convex polygons, as well as some
		* non-convex polygons if we get lucky.
		*
		* Returns TRUE if the polygon was successfully rendered.  The rendering
		* output is provided as callbacks (see the api).
		*/

		#endregion CodeFromRender
	}
}