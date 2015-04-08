//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# Port port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	public interface IUndoData
	{
		IUndoData Clone();
	}

	public class UndoBuffer
	{
		public enum MergeType { Mergable, NotMergable };

		private class UndoCheckPoint
		{
			internal IUndoData objectToUndoTo;
			internal string typeOfObject;
			internal MergeType mergeType;

			internal UndoCheckPoint(IUndoData objectToUndoTo, string typeOfObject, MergeType mergeType)
			{
				this.objectToUndoTo = objectToUndoTo;
				this.typeOfObject = typeOfObject;
				this.mergeType = mergeType;
			}
		}

		private List<UndoCheckPoint> undoBuffer = new List<UndoCheckPoint>();
		private int currentUndoIndex = -1;
		private int lastValidUndoIndex = -1;

		public UndoBuffer()
		{
		}

		public void Add(IUndoData objectToUndoTo, string typeOfObject, MergeType mergeType)
		{
			IUndoData cloneableObject = objectToUndoTo;
			if (cloneableObject != null)
			{
				if (currentUndoIndex <= 0
					|| mergeType == MergeType.NotMergable
					|| undoBuffer[currentUndoIndex].typeOfObject != typeOfObject)
				{
					currentUndoIndex++;
				}

				UndoCheckPoint newUndoCheckPoint = new UndoCheckPoint(cloneableObject.Clone(), typeOfObject, mergeType);
				if (currentUndoIndex < undoBuffer.Count)
				{
					undoBuffer[currentUndoIndex] = newUndoCheckPoint;
				}
				else
				{
					undoBuffer.Add(newUndoCheckPoint);
				}

				lastValidUndoIndex = currentUndoIndex;
			}
		}

		public object GetPrevUndoObject()
		{
			if (currentUndoIndex > 0)
			{
				return undoBuffer[--currentUndoIndex].objectToUndoTo.Clone();
			}

			return null;
		}

		public object GetNextRedoObject()
		{
			if (lastValidUndoIndex > currentUndoIndex)
			{
				currentUndoIndex++;
				return undoBuffer[currentUndoIndex].objectToUndoTo.Clone();
			}

			return null;
		}

		internal void ClearHistory()
		{
			undoBuffer.Clear();
			currentUndoIndex = -1;
			lastValidUndoIndex = -1;
		}
	}
}