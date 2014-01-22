/*
Copyright (c) 2013, Lars Brubaker
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg
{
    public class DirtyRects
    {
        int xSize;				// size of the area having the dirty rects
        int ySize;

        public int numCellsX;
        public int numCellsY;

        int[] yTable;
        DirtyCell[] dirtyCells;

        int xCellSize;			// size of a single cell
        int yCellSize;

        int xCellFactor;		// the binary shift factor for x
        int yCellFactor;		// the binary shift factor for y

        const int DEFAULT_SHIFT_FACTOR = 5;

        public class DirtyCell
        {
            public bool isDirty;
            public RectangleInt coverage;

            public DirtyCell()
            {
                coverage.SetRect(0, 0, 0, 0);
                isDirty = false;
            }
        }

        public DirtyRects()
        {
            yTable = null;
            dirtyCells = null;

            xSize = 0;				// size of the area having the dirty rects
            ySize = 0;

            xCellFactor = DEFAULT_SHIFT_FACTOR;		// the binary shift factor for x (default 16 pixels)
            yCellFactor = DEFAULT_SHIFT_FACTOR;		// the binary shift factor for y

            Initialize();
        }

        public DirtyRects(int wide, int height)					// of the screen or window
        {
            yTable = null;
            dirtyCells = null;

            // size of the area having the dirty rects
            xSize = wide;
            ySize = height;

            // the binary shift factor for x (default 16 pixels)
            xCellFactor = DEFAULT_SHIFT_FACTOR;
            // the binary shift factor for y
            yCellFactor = DEFAULT_SHIFT_FACTOR;

            Initialize();
        }

        public bool IsDirty(int PosX, int PosY)
        {
            if (dirtyCells == null)
            {
                throw new Exception();
            }

            // make sure the point is with the bounding rect
            if (PosX < numCellsX && PosY < numCellsY)
            {
                // now check the pixel
                return dirtyCells[yTable[PosY] + PosX].isDirty;
            }

            return false;
        }

        public bool IsAnyDirty()
        {
            int NumTotalCells = numCellsX * numCellsY;
            for (int x = 0; x < NumTotalCells; ++x)
            {
                if (dirtyCells[x].isDirty)
                {
                    return true;
                }
            }
            return false;
        }

        public DirtyCell GetCell(int PosX, int PosY)
        {
            if (dirtyCells == null)
            {
                throw new Exception();
            }

            // make sure the point is with the bounding rect
            if (PosX < numCellsX && PosY < numCellsY)
            {
                // now check the pixel
                return dirtyCells[yTable[PosY] + PosX];
            }

            return null;
        }

        public void SetSize(int wide, int height)
        {
            // size of the area having the dirty rects
            xSize = wide;
            ySize = height;

            Initialize();
        }

        public void SetFactor(int xFactor, int yFactor)
        {
            // the binary shift factor for x
            xCellFactor = xFactor;
            // the binary shift factor for y
            yCellFactor = yFactor;

            Initialize();
        }

        public void SetDirty()
        {
            int Total = (int)numCellsX * numCellsY;
            for (int i = 0; i < Total; i++)
            {
                dirtyCells[i].isDirty = true;
                dirtyCells[i].coverage.Left = 0;
                dirtyCells[i].coverage.Bottom = 0;
                dirtyCells[i].coverage.Right = xCellSize;
                dirtyCells[i].coverage.Top = yCellSize;
            }
        }

        public void SetDirty(RectangleDouble areaToSetDirty)
        {
            SetDirty(new RectangleInt(
                (int)Math.Floor(areaToSetDirty.Left), 
                (int)Math.Floor(areaToSetDirty.Bottom),
                (int)Math.Ceiling(areaToSetDirty.Right), 
                (int)Math.Ceiling(areaToSetDirty.Top)
                ));
        }

        public void SetDirty(RectangleInt areaToSetDirty)
        {
            if (areaToSetDirty.Left >= areaToSetDirty.Right || areaToSetDirty.Bottom >= areaToSetDirty.Top)
            {
                return;
            }

            RectangleInt BoundRect = new RectangleInt(0, 0, numCellsX << xCellFactor, numCellsY << yCellFactor);
            RectangleInt Area = areaToSetDirty;
            if (Area.IntersectWithRectangle(BoundRect))
            {
                Area.Right--;
                Area.Top--;
                Area.Left = Area.Left >> xCellFactor;
                Area.Right = Area.Right >> xCellFactor;
                Area.Bottom = Area.Bottom >> yCellFactor;
                Area.Top = Area.Top >> yCellFactor;

                int offsetForY = yTable[Area.Bottom];

                for (int y = Area.Bottom; y <= Area.Top; y++)
                {
                    for (int x = Area.Left; x <= Area.Right; x++)
                    {
                        DirtyCell currCell = dirtyCells[offsetForY + x];

                        // if it's not set or it's not totaly covered
                        RectangleInt CurCellBounds = new RectangleInt((x << xCellFactor), (y << yCellFactor),
                            ((x << xCellFactor) + xCellSize), ((y << yCellFactor) + yCellSize));
                        // if we are setting it for the first time
                        if (!currCell.isDirty)
                        {
                            currCell.coverage.Left = Math.Max(Math.Min((areaToSetDirty.Left - CurCellBounds.Left), xCellSize), 0);
                            currCell.coverage.Bottom = Math.Max(Math.Min((areaToSetDirty.Bottom - CurCellBounds.Bottom), yCellSize), 0);
                            currCell.coverage.Right = Math.Max(Math.Min((areaToSetDirty.Right - CurCellBounds.Left), xCellSize), 0);
                            currCell.coverage.Top = Math.Max(Math.Min((areaToSetDirty.Top - CurCellBounds.Bottom), yCellSize), 0);
                        }
                        else // we are adding to it's coverage
                        {
                            currCell.coverage.Left = Math.Max(Math.Min(Math.Min(currCell.coverage.Left, (areaToSetDirty.Left - CurCellBounds.Left)), xCellSize), 0);
                            currCell.coverage.Bottom = Math.Max(Math.Min(Math.Min(currCell.coverage.Bottom, (areaToSetDirty.Bottom - CurCellBounds.Bottom)), yCellSize), 0);
                            currCell.coverage.Right = Math.Max(Math.Min(Math.Max(currCell.coverage.Right, (areaToSetDirty.Right - CurCellBounds.Left)), xCellSize), 0);
                            currCell.coverage.Top = Math.Max(Math.Min(Math.Max(currCell.coverage.Top, (areaToSetDirty.Top - CurCellBounds.Bottom)), yCellSize), 0);
                        }

                        currCell.isDirty = true;
                    }

                    offsetForY += numCellsX;
                }
            }
        }

        public void SetClean()
        {
            int Total = (int)numCellsX * numCellsY;
            for (int i = 0; i < Total; i++)
            {
                dirtyCells[i].isDirty = false;
            }
        }

        public void SetClean(RectangleInt areaToSetClean)
        {
            int curRow;

            RectangleInt BoundRect = new RectangleInt(0, 0, numCellsX << xCellFactor, numCellsY << yCellFactor);
            RectangleInt Area = areaToSetClean;
            if (Area.IntersectWithRectangle(BoundRect))
            {
                Area.Right--;
                Area.Top--;
                Area.Left = Area.Left >> xCellFactor;
                Area.Right = Area.Right >> xCellFactor;
                Area.Bottom = Area.Bottom >> yCellFactor;
                Area.Top = Area.Top >> yCellFactor;

                curRow = yTable[Area.Bottom];

                for (int y = Area.Bottom; y <= Area.Top; y++)
                {
                    for (int x = Area.Left; x <= Area.Right; x++)
                    {
                        dirtyCells[curRow + x].isDirty = false;
                    }
                    curRow += numCellsX;
                }
            }
        }

        public int GetXCellFactor() { return xCellFactor; }
        public int GetYCellFactor() { return yCellFactor; }

        public int GetXCellSize() { return xCellSize; }
        public int GetYCellSize() { return yCellSize; }

        public int GetWidth() { return numCellsX; }
        public int GetHeight() { return numCellsY; }

        public void BuildDirtyRect(ref int x, int y, out int XStart, out int YStart, out int XEnd, out int YEnd)
        {
            int LastX = this.numCellsX - x;
            DirtyCell curCell = GetCell(x, y);
            XStart = (x << GetXCellFactor()) + curCell.coverage.Left;
            YStart = (y << GetYCellFactor()) + curCell.coverage.Bottom;
            XEnd = (x << GetXCellFactor()) + curCell.coverage.Right;
            YEnd = (y << GetYCellFactor()) + curCell.coverage.Top;

            int XCellSize = GetXCellSize();
            DirtyCell nextCell = GetCell(x + 1, y);
            if (curCell.coverage.Right == (int)XCellSize // it touches the right side of the cell
                && nextCell != null && nextCell.isDirty // there is a next cell 
                && nextCell.coverage.Left == 0 // that touches the left
                && curCell.coverage.Top == nextCell.coverage.Top // and they have the same top
                && curCell.coverage.Bottom == nextCell.coverage.Bottom) // and bottom
            {
                // get all the ones we can (build a nice big strip rather than every rect)
                while (x + 1 < LastX
                    && curCell.coverage.Right == (int)XCellSize // it touches the right side of the cell
                    && nextCell != null && nextCell.isDirty // there is a next cell 
                    && nextCell.coverage.Left == 0 // that touches the left
                    && curCell.coverage.Top == nextCell.coverage.Top // and they have the same top
                    && curCell.coverage.Bottom == nextCell.coverage.Bottom) // and bottom
                {
                    x++;
                    XEnd += nextCell.coverage.Width;
                    curCell = nextCell;
                    nextCell = GetCell(x + 1, y);
                }
            }
        }

#if false
	CDirtyRects operator=(CDirtyRects SourceDirtyRects);
{
	assert(xSize == SourceDirtyRects.xSize);
	assert(ySize == SourceDirtyRects.ySize);
	assert(numCellsX == SourceDirtyRects.numCellsX);
	assert(numCellsY == SourceDirtyRects.numCellsY);
	assert(xCellSize == SourceDirtyRects.xCellSize);
	assert(yCellSize == SourceDirtyRects.yCellSize);
	assert(xCellFactor == SourceDirtyRects.xCellFactor);
	assert(yCellFactor == SourceDirtyRects.yCellFactor);

	int Total = (int)numCellsX * numCellsY;
	for(int i=0; i<Total; i++)
	{
		cellInfo[i].isDirty = SourceDirtyRects.cellInfo[i].isDirty;
		cellInfo[i].coverage = SourceDirtyRects.cellInfo[i].coverage;
		assert(!(cellInfo[i].isDirty) || (cellInfo[i].coverage.Width() && cellInfo[i].coverage.Height()));
	}

	return *this;
}

const CDirtyRects& operator+=(const CDirtyRects& AdditionalDirtyRects);
{
	assert(xSize == AdditionalDirtyRects.xSize);
	assert(ySize == AdditionalDirtyRects.ySize);
	assert(numCellsX == AdditionalDirtyRects.numCellsX);
	assert(numCellsY == AdditionalDirtyRects.numCellsY);
	assert(xCellSize == AdditionalDirtyRects.xCellSize);
	assert(yCellSize == AdditionalDirtyRects.yCellSize);
	assert(xCellFactor == AdditionalDirtyRects.xCellFactor);
	assert(yCellFactor == AdditionalDirtyRects.yCellFactor);

	int Total = (int)numCellsX * numCellsY;
	for(int i=0; i<Total; i++)
	{
		if(cellInfo[i].isDirty)
		{
			if(AdditionalDirtyRects.cellInfo[i].isDirty)
			{
				// They are both dirty, maximize the coverage [9/12/2001] LBB
				CCharRect* pThisCoverage = &cellInfo[i].coverage;
				CCharRect* pAdditionalCoverage = &AdditionalDirtyRects.cellInfo[i].coverage;
				pThisCoverage.Left = Math.Min(pThisCoverage.Left, pAdditionalCoverage.Left);
				pThisCoverage.Top = Math.Min(pThisCoverage.Top, pAdditionalCoverage.Top);
				pThisCoverage.Right = Math.Max(pThisCoverage.Right, pAdditionalCoverage.Right);
				pThisCoverage.Bottom = Math.Max(pThisCoverage.Bottom, pAdditionalCoverage.Bottom);
			}
		}
		else if(AdditionalDirtyRects.cellInfo[i].isDirty) // only the additional is dirty copy it
		{
			cellInfo[i].isDirty = true;
			cellInfo[i].coverage = AdditionalDirtyRects.cellInfo[i].coverage;
		}
		assert(!(cellInfo[i].isDirty) || (cellInfo[i].coverage.Width() && cellInfo[i].coverage.Height()));
	}

	return *this;
}
#endif

        public void Initialize()
        {
            xCellSize = (1 << xCellFactor);			// size of a single cell
            yCellSize = (1 << yCellFactor);

            numCellsX = xSize / xCellSize;
            numCellsY = ySize / yCellSize;

            if ((xSize & (xCellSize - 1)) != 0)
            {
                numCellsX++;
            }

            if ((ySize & (yCellSize - 1)) != 0)
            {
                numCellsY++;
            }

            yTable = null;
            if (numCellsY != 0)
            {
                yTable = new int[numCellsY];
            }
            int CurOffset = 0;
            for (int i = 0; i < numCellsY; i++)
            {
                yTable[i] = CurOffset;
                CurOffset += numCellsX;
            }

            dirtyCells = null;
            if (numCellsX * numCellsY != 0)
            {
                dirtyCells = new DirtyCell[numCellsX * numCellsY];
                for (int i = 0; i < dirtyCells.Length; i++)
                {
                    dirtyCells[i] = new DirtyCell();
                }
            }
        }
    }
}
