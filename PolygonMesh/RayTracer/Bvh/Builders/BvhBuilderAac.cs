/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.IO;
using System.Linq;

namespace MatterHackers.RayTracer
{
    public class BvhBuilderAac
    {
        public double alpha;
        public int minSize;
        public int[] table = new int[100];
        private const double infi = 1e10;
        private AxisAlignedBoundingBox[] AABB;
        private Vector3[] bary;
        private AxisAlignedBoundingBox box = new AxisAlignedBoundingBox();
        private long[] code;
        private double[] cost;
        private int curNode;
        private bool[] isLeaf;
        private int[] lChild;
        private Mesh model;
        private int MortonDigit;
        private int[] nodeNum;
        private int[] rChild;
        private int[] set;
        private int[] setTmp;
        private double[] surA;
        private int[] triNum;
        public double epsi => 1e-8;

        public static void Create(Mesh mesh)
        {
            Console.WriteLine($"Aac Mesh {mesh.Faces.Count}");
            using (new QuickTimer("High Quality ACC BVH"))
            {
                var bvhLong = new BvhBuilderAac();
                bvhLong.Prepare(mesh, 20, 0.1);
                bvhLong.Build("C:\\Temp\\Bvh-HQ.bin");
            }

            using (new QuickTimer("Fast ACC BVH"))
            {
                var bvhLong = new BvhBuilderAac();
                bvhLong.Prepare(mesh, 4, 0.2);
                bvhLong.Build("C:\\Temp\\Bvh-fast.bin");
            }
        }

        // Main process
        public void Build(string fileName)
        {
            var faceCount = model.Faces.Count;
            curNode = faceCount;
            Thread thread = new Thread();
            thread.init(4 * (int)(minSize * Math.Pow(faceCount / 2.0 / minSize, 0.5 - alpha / 2) + 1e-5));
            RadixSort();
            int finalLen;
            ThreadBuild(thread, 0, 0, faceCount, MortonDigit * 3 - 1, out finalLen);
            AAClomerate(thread, 0, finalLen, 1, out finalLen);

            PruneTree();
            PrintTreeBIN(fileName);
            PrintTree(fileName);
        }

        // Data input.
        public void Prepare(Mesh modelInput, int delta, double alpha)
        {
            this.minSize = delta / 2; this.alpha = alpha;
            for (int j = 0; j < 100; j++)
            {
                table[j] = (int)(delta * Math.Pow((double)(j) / delta, 0.5 - alpha) / 2 - epsi) + 1;
            }

            model = modelInput;
            Vector3[] ver = model.Vertices.Select(v => v.AsVector3()).ToArray();
            int faceCount = model.Faces.Count;
            int[] tri = new int[faceCount * 3];
            int triIndex = 0;
            foreach (var face in model.Faces)
            {
                tri[triIndex++] = face.v0;
                tri[triIndex++] = face.v1;
                tri[triIndex++] = face.v2;
            }
            MortonDigit = 9 + (((faceCount >> 18) > 0) ? 1 : 0);
            if (faceCount >= 1 << 20)
            {
                MortonDigit++;
            }
            if (faceCount >= 1 << 22 && delta > 10)
            {
                MortonDigit++;
            }
            Vector3 p1 = new Vector3();
            Vector3 p2 = new Vector3();
            Vector3 p3 = new Vector3();
            lChild = new int[faceCount * 2];
            rChild = new int[faceCount * 2];
            AABB = new AxisAlignedBoundingBox[faceCount * 2];
            for (int i = 0; i < AABB.Length; i++)
            {
                AABB[i] = new AxisAlignedBoundingBox();
            }
            triNum = new int[faceCount * 2];
            bary = new Vector3[faceCount * 2];
            for (int i = 0; i < faceCount; i++)
            {
                SwapPoints(ref p1, ver, tri[i * 3]);
                SwapPoints(ref p2, ver, tri[i * 3 + 1]);
                SwapPoints(ref p3, ver, tri[i * 3 + 2]);
                AABB[i].MinXYZ.X = Min3(p1[0], p2[0], p3[0]);
                AABB[i].MinXYZ.Y = Min3(p1[1], p2[1], p3[1]);
                AABB[i].MinXYZ.Z = Min3(p1[2], p2[2], p3[2]);
                AABB[i].MaxXYZ.X = Max3(p1[0], p2[0], p3[0]);
                AABB[i].MaxXYZ.Y = Max3(p1[1], p2[1], p3[1]);
                AABB[i].MaxXYZ.Z = Max3(p1[2], p2[2], p3[2]);
                bary[i][0] = (AABB[i].MinXYZ.X + AABB[i].MaxXYZ[0]) / 2;
                bary[i][1] = (AABB[i].MaxXYZ.Y + AABB[i].MaxXYZ.Y) / 2;
                bary[i][2] = (AABB[i].MaxXYZ.Z + AABB[i].MaxXYZ.Z) / 2;
                triNum[i] = 1;
                Update(box, bary[i]);
            }

            code = new long[faceCount];
            set = new int[faceCount];
            setTmp = new int[faceCount];
            cost = new double[faceCount * 2];
            surA = new double[faceCount * 2];
            isLeaf = new bool[faceCount * 2];
            nodeNum = new int[faceCount * 2];
        }

        public void PrintTreeBIN(string fileName)
        {
            var f = new BinaryWriter(new FileStream(fileName, FileMode.Create));
            f.Write(nodeNum[curNode - 1]);
            double area = AABB[curNode - 1].GetSurfaceArea();
            PrintTreeBIN(f, curNode - 1, 0, area);
            f.Close();
        }

        public void PrintTree(string fileName)
        {
            var f = new StreamWriter(new FileStream(Path.ChangeExtension(fileName, ".txt"), FileMode.Create));
            f.Write(nodeNum[curNode - 1]);
            double area = AABB[curNode - 1].GetSurfaceArea();
            PrintTree(f, curNode - 1, 0, area, 0);
            f.Close();
        }

        //The function to process agglomeration.
        private void AAClomerate(Thread thread, int start, int startNum, int endNum, out int finalNum)
        {
            int n = startNum;
            finalNum = Math.Min(startNum, endNum);
            int a = 0;
            int b = 0;
            int last = 0;
            while (n > endNum)
            {
                double mn = infi;
                for (int i = start; i < start + n; i++)
                {
                    if (thread.minPos[i] == start + n)
                    {
                        if (last == start + n)
                        {
                            thread.minLabel[i] = -1;
                        }
                        else
                        {
                            thread.minPos[i] = last;
                        }
                    }

                    if (thread.minLabel[i] != thread.label[thread.minPos[i]])
                    {
                        thread.minArea[i] = infi;
                        for (int j = start; j < i; j++)
                        {
                            if (thread.area[i][j] < thread.minArea[i])
                            {
                                thread.minArea[i] = thread.area[i][j];
                                thread.minPos[i] = j; thread.minLabel[i] = thread.label[j];
                            }
                        }

                        for (int j = i + 1; j < start + n; j++)
                        {
                            if (thread.area[j][i] < thread.minArea[i])
                            {
                                thread.minArea[i] = thread.area[j][i];
                                thread.minPos[i] = j; thread.minLabel[i] = thread.label[j];
                            }
                        }
                    }
                    if (thread.minArea[i] < mn)
                    {
                        mn = thread.minArea[i];
                        a = i; b = thread.minPos[i];
                    }
                }

                lChild[curNode] = thread.label[a];
                rChild[curNode] = thread.label[b];
                Update(AABB[curNode], AABB[lChild[curNode]], AABB[rChild[curNode]]);
                triNum[curNode] = triNum[lChild[curNode]] + triNum[rChild[curNode]];
                for (int j = start; j < a; j++)
                {
                    thread.area[a][j] = SA(AABB[curNode], AABB[thread.label[j]]);
                }

                for (int j = a + 1; j < start + n; j++)
                {
                    thread.area[j][a] = SA(AABB[curNode], AABB[thread.label[j]]);
                }

                n--;
                for (int j = start; j < b; j++)
                {
                    thread.area[b][j] = thread.area[start + n][j];
                }

                for (int j = b + 1; j < start + n; j++)
                {
                    thread.area[j][b] = thread.area[start + n][j];
                }

                thread.label[a] = curNode;
                thread.label[b] = thread.label[start + n];
                thread.minArea[b] = thread.minArea[start + n];
                thread.minLabel[b] = thread.minLabel[start + n];
                thread.minPos[b] = thread.minPos[start + n];
                last = b;
                curNode++;
            }
        }

        // function F.
        private int f(int len)
        {
            return (int)(minSize * Math.Pow((double)(len) / minSize / 2, 0.5 - alpha) - epsi) + 1;
        }

        private double Max3(double a, double b, double c)
        {
            return Math.Max(Math.Max(a, b), c);
        }

        //Combine two subtaskes.
        private void Merge(Thread thread, int start, int len1, int len2)
        {
            for (int i = start; i < start + len1; i++)
            {
                for (int j = start + len1; j < start + len1 + len2; j++)
                {
                    thread.area[j][i] = SA(AABB[thread.label[i]], AABB[thread.label[j]]);
                    if (thread.area[j][i] < thread.minArea[i])
                    {
                        thread.minArea[i] = thread.area[j][i];
                        thread.minPos[i] = j; thread.minLabel[i] = set[j];
                    }
                    if (thread.area[j][i] < thread.minArea[j])
                    {
                        thread.minArea[j] = thread.area[j][i];
                        thread.minPos[j] = i; thread.minLabel[j] = set[i];
                    }
                }
            }
        }

        private double Min3(double a, double b, double c)
        {
            return Math.Min(Math.Min(a, b), c);
        }

        private void PrintTreeBIN(BinaryWriter f, int root, int forb, double topArea)
        {
            var faceCount = model.Faces.Count;

            if (forb > 0)
            {
                if (root < faceCount)
                {
                    f.Write(root);
                }
                else
                {
                    PrintTreeBIN(f, lChild[root], 1, 0);
                    PrintTreeBIN(f, rChild[root], 1, 0);
                }
                return;
            }

            double area = AABB[root].GetSurfaceArea();
            bool skip = (area / topArea > 0.75);
            if (!skip)
            {
                topArea = area;
            }

            f.Write(isLeaf[root]);
            int ax = 0;
            if (!isLeaf[root])
            {
                ax = nodeNum[lChild[root]] + 1;
                f.Write(ax);
                ax = 0;
                f.Write(ax);
                f.Write(skip);
                f.Write(AABB[root].MinXYZ.X);
                f.Write(AABB[root].MinXYZ.Y);
                f.Write(AABB[root].MinXYZ.Z);
                f.Write(AABB[root].MaxXYZ.X);
                f.Write(AABB[root].MaxXYZ.Y);
                f.Write(AABB[root].MaxXYZ.Z);
                PrintTreeBIN(f, lChild[root], 0, topArea);
                PrintTreeBIN(f, rChild[root], 0, topArea);
            }
            else
            {
                f.Write(triNum[root]);
                f.Write(ax);
                f.Write(skip);
                f.Write(AABB[root].MinXYZ.X);
                f.Write(AABB[root].MinXYZ.Y);
                f.Write(AABB[root].MinXYZ.Z);
                f.Write(AABB[root].MaxXYZ.X);
                f.Write(AABB[root].MaxXYZ.Y);
                f.Write(AABB[root].MaxXYZ.Z);
                PrintTreeBIN(f, root, 1, 0);
            }
        }

        bool group = false;
        private void PrintTree(TextWriter f, int root, int forb, double topArea, int depth)
        {
            var faceCount = model.Faces.Count;

            void Indent()
            {
                f.Write(new String(' ', depth * 4));
            }

            if (forb > 0)
            {
                if (root < faceCount)
                {
                    if (!group)
                    {
                        f.Write("\n");
                        Indent();
                    }
                    group = true;
                    f.Write($"[{root}]");
                }
                else
                {
                    PrintTree(f, lChild[root], 1, 0, depth + 1);
                    PrintTree(f, rChild[root], 1, 0, depth + 1);
                }
                return;
            }

            group = false;

            double area = AABB[root].GetSurfaceArea();
            bool skip = (area / topArea > 0.75);
            if (!skip)
            {
                topArea = area;
            }

            f.Write("\n");
            Indent();
            int ax = 0;
            if (!isLeaf[root])
            {
                ax = nodeNum[lChild[root]] + 1;
                f.Write($"Node: ax[{ax}]");
                ax = 0;
                f.Write($",ax[{ax}]");
                f.Write($"skip[{skip}]");
                f.Write(AABB[root].ToString());
                PrintTree(f, lChild[root], 0, topArea, depth + 1);
                PrintTree(f, rChild[root], 0, topArea, depth + 1);
            }
            else
            {
                f.Write($"Leaf: Tri[{triNum[root]}]");
                f.Write($",ax[{ax}]");
                f.Write($"skip[{skip}]");
                f.Write(AABB[root].ToString());
                PrintTree(f, root, 1, 0, depth + 1);
            }
        }

        // Prune tree at last. This can be also done in tree building process.
        private void PruneTree()
        {
            var faceCount = model.Faces.Count;

            for (int i = 0; i < faceCount; i++)
            {
                nodeNum[i] = 1;
                cost[i] = 2;
                surA[i] = AABB[i].GetSurfaceArea();
                isLeaf[i] = true;
            }

            for (int i = faceCount; i < curNode; i++)
            {
                surA[i] = AABB[i].GetSurfaceArea();
                if (AABB[i].GetSurfaceArea() - SA(AABB[lChild[i]], AABB[rChild[i]]) < -1e-8)
                {
                    throw new Exception();
                }

                double tmp = surA[lChild[i]] / surA[i] * cost[lChild[i]] + surA[lChild[i]] / surA[i] * cost[lChild[i]];
                if (tmp + 1 > triNum[i] * 2)
                {
                    cost[i] = triNum[i];
                    nodeNum[i] = 1;
                    isLeaf[i] = true;
                }
                else
                {
                    cost[i] = tmp + 1;
                    nodeNum[i] = nodeNum[lChild[i]] + nodeNum[rChild[i]] + 1;
                    isLeaf[i] = false;
                }
            }
        }

        //Generate Morton Code.
        private void RadixSort()
        {
            var faceCount = model.Faces.Count;
            long[] mapping = new long[1 << MortonDigit];
            for (int i = 0; i < (1 << MortonDigit); i++)
            {
                mapping[i] = 0;
                for (int j = 0; j < MortonDigit; j++)
                {
                    int bit = ((long)(i & (1 << j)) > 0) ? 1 : 0;
                    mapping[i] += bit << (j * 3);
                }
            }

            double[] cutLen = new double[]
            {
                (1<<MortonDigit) / (box.MaxXYZ.X-box.MinXYZ.X + 1e-8),
                (1<<MortonDigit) / (box.MaxXYZ.Y-box.MinXYZ.Y + 1e-8),
                (1<<MortonDigit) / (box.MaxXYZ.Z-box.MinXYZ.Z + 1e-8)
            };

            for (int i = 0; i < faceCount; i++)
            {
                code[i] = (mapping[(int)((bary[i][0] - box.MinXYZ.X) * cutLen[0])] << 2) +
                          (mapping[(int)((bary[i][1] - box.MinXYZ.Y) * cutLen[1])] << 1) +
                          (mapping[(int)((bary[i][2] - box.MinXYZ.Z) * cutLen[2])]);
            }

            int totBuc = (1 << ((MortonDigit * 3 + 1) >> 1)), digit = (MortonDigit * 3 + 1) >> 1;
            int maskLow = totBuc - 1, maskHigh = (totBuc - 1) << digit;
            int[] cntHigh = new int[totBuc + 1];
            int[] cntLow = new int[totBuc + 1];

            for (int i = 0; i < totBuc; i++)
            {
                cntHigh[i] = cntLow[i] = 0;
            }

            for (int i = 0; i < faceCount; i++)
            {
                cntLow[(code[i] & maskLow) + 1]++;
                cntHigh[((code[i] & maskHigh) >> digit) + 1]++;
            }

            for (int i = 1; i < totBuc; i++)
            {
                cntLow[i] += cntLow[i - 1];
                cntHigh[i] += cntHigh[i - 1];
            }

            for (int i = 0; i < faceCount; i++)
            {
                setTmp[cntLow[(code[i] & maskLow)]++] = i;
            }

            for (int i = 0; i < faceCount; i++)
            {
                set[cntHigh[((code[setTmp[i]] & maskHigh) >> digit)]++] = setTmp[i];
            }
        }

        private double SA(AxisAlignedBoundingBox a, AxisAlignedBoundingBox b)
        {
            double x = Math.Max(a.MaxXYZ.X, b.MaxXYZ.X) - Math.Min(a.MinXYZ.X, b.MinXYZ.X);
            double y = Math.Max(a.MaxXYZ.Y, b.MaxXYZ.Y) - Math.Min(a.MinXYZ.Y, b.MinXYZ.Y);
            double z = Math.Max(a.MaxXYZ.Z, b.MaxXYZ.Z) - Math.Min(a.MinXYZ.Z, b.MinXYZ.Z);
            return x * y + x * z + y * z;
        }

        //Set leaves.
        private void SetLeaf(Thread thread, int start, int startTri, int faceCount)
        {
            for (int i = 0; i < faceCount; i++)
            {
                thread.minArea[start + i] = infi;
                thread.label[start + i] = set[startTri + i];
            }

            for (int i = start; i < start + faceCount; i++)
            {
                for (int j = start; j < i; j++)
                {
                    thread.area[i][j] = SA(AABB[thread.label[i]], AABB[thread.label[j]]);
                    if (thread.area[i][j] < thread.minArea[i])
                    {
                        thread.minArea[i] = thread.area[i][j];
                        thread.minPos[i] = j; thread.minLabel[i] = set[j];
                    }

                    if (thread.area[i][j] < thread.minArea[j])
                    {
                        thread.minArea[j] = thread.area[i][j];
                        thread.minPos[j] = i; thread.minLabel[j] = set[i];
                    }
                }
            }
        }

        private void SwapPoints(ref Vector3 a, Vector3[] b, int index)
        {
            var swap = a;
            a = b[index];
            b[index] = swap;
        }

        //A single agglomerative clustering thread.
        private void ThreadBuild(Thread thread, int start, int startTri, int endTri, int digit, out int finalLen)
        {
            if (startTri >= endTri)
            {
                finalLen = 0;
                return;
            }

            if (endTri - startTri < minSize * 2)
            {
                if (start + endTri - startTri >= thread.size)
                {
                    throw new Exception();
                }
                SetLeaf(thread, start, startTri, endTri - startTri);
                AAClomerate(thread, start, endTri - startTri, minSize, out finalLen);
                return;
            }

            int s = startTri, t = endTri - 1, mid;
            long tar = (long)1 << digit;
            while (digit >= 0)
            {
                if ((code[set[s]] & tar) == 0
                    && (code[set[t]] & tar) != 0)
                {
                    break;
                }
                digit--;
                tar = (long)1 << digit;
            }

            if (digit < 0)
            {
                s = (s + t) >> 1;
            }
            else
            {
                while (s < t)
                {
                    mid = (s + t) >> 1;
                    if ((code[set[mid]] & tar) != 0)
                    {
                        t = mid;
                    }
                    else
                    {
                        s = mid + 1;
                    }
                }
            }

            int len1, len2;
            ThreadBuild(thread, start, startTri, s, digit - 1, out len1);
            ThreadBuild(thread, start + len1, s, endTri, digit - 1, out len2);
            Merge(thread, start, len1, len2);
            finalLen = (endTri - startTri >= 100) ? f(endTri - startTri) : table[endTri - startTri];
            AAClomerate(thread, start, len1 + len2, finalLen, out finalLen);
        }

        private void Update(AxisAlignedBoundingBox a, Vector3 b)
        {
            if (b[0] < a.MinXYZ.X) a.MinXYZ.X = b[0];
            if (b[1] < a.MinXYZ.Y) a.MinXYZ.Y = b[1];
            if (b[2] < a.MinXYZ.Z) a.MinXYZ.Z = b[2];
            if (b[0] > a.MaxXYZ.X) a.MaxXYZ.X = b[0];
            if (b[1] > a.MaxXYZ.Y) a.MaxXYZ.Y = b[1];
            if (b[2] > a.MaxXYZ.Z) a.MaxXYZ.Z = b[2];
        }

        private void Update(AxisAlignedBoundingBox a, AxisAlignedBoundingBox b, AxisAlignedBoundingBox c)
        {
            a.MinXYZ.X = (b.MinXYZ.X < c.MinXYZ.X) ? b.MinXYZ.X : c.MinXYZ.X;
            a.MaxXYZ.X = (b.MaxXYZ.X > c.MaxXYZ.X) ? b.MaxXYZ.X : c.MaxXYZ.X;
            a.MinXYZ.Y = (b.MinXYZ.Y < c.MinXYZ.Y) ? b.MinXYZ.Y : c.MinXYZ.Y;
            a.MaxXYZ.Y = (b.MaxXYZ.Y > c.MaxXYZ.Y) ? b.MaxXYZ.Y : c.MaxXYZ.Y;
            a.MinXYZ.Z = (b.MinXYZ.Z < c.MinXYZ.Z) ? b.MinXYZ.Z : c.MinXYZ.Z;
            a.MaxXYZ.Z = (b.MaxXYZ.Z > c.MaxXYZ.Z) ? b.MaxXYZ.Z : c.MaxXYZ.Z;
        }

        // Interval operations
        public class Interval
        {
            public double min, max;

            public Interval()
            {
                min = -(max = -1e10);
            }

            public bool check(double x)
            {
                return min <= x && x <= max;
            }

            public double mid()
            {
                return (max + min) / 2;
            }

            public double range()
            {
                return max - min;
            }

            public void twice()
            {
                min *= 2; max *= 2; 
            }

            public double update(Interval a)
            {
                return a.max < min ? max - a.max : a.min > max ? a.min - min : max - min;
            }
        }

        public class Thread  // A single thread. In order to parallel, just start multiple threads.
        {
            public double[][] area;
            public int[] label;
            public double[] minArea;
            public int[] minLabel;
            public int[] minPos;
            public int size;

            public void init(int size)
            {
                this.size = size;
                label = new int[size];
                minArea = new double[size];
                minPos = new int[size];
                minLabel = new int[size];
                area = new double[size][];
                for (int i = 0; i < size; i++)
                {
                    area[i] = new double[size];
                }
            }
        }
    }
}