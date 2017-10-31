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

using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Processors;
using MatterHackers.Csg.Transform;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Csg
{
	public enum Alignment { x, y, z, negX, negY, negZ };

	[Flags]
	public enum Face
	{
		Left = 0x01,
		Right = 0x02,
		Front = 0x04,
		Back = 0x08,
		Bottom = 0x10,
		Top = 0x20,
	};

	[Flags]
	public enum Edge
	{
		LeftFront = Face.Left | Face.Front,
		LeftBack = Face.Left | Face.Back,
		LeftBottom = Face.Left | Face.Bottom,
		LeftTop = Face.Left | Face.Top,
		RightFront = Face.Right | Face.Front,
		RightBack = Face.Right | Face.Back,
		RightBottom = Face.Right | Face.Bottom,
		RightTop = Face.Right | Face.Top,
		FrontBottom = Face.Front | Face.Bottom,
		FrontTop = Face.Front | Face.Top,
		BackBottom = Face.Back | Face.Bottom,
		BackTop = Face.Back | Face.Top
	}

	public static class Helpers
	{
		public static string FormatWith(this string format, params object[] args)
		{
			if (format == null)
				throw new ArgumentNullException("format");

			return string.Format(format, args);
		}

		public static string FormatWith(this string format, IFormatProvider provider, params object[] args)
		{
			if (format == null)
				throw new ArgumentNullException("format");

			return string.Format(provider, format, args);
		}
	}

	public abstract class CsgObject
	{
		private Dictionary<string, string> properties = new Dictionary<string, string>();

		public Dictionary<string, string> Properties
		{
			get
			{
				return properties;
			}
		}

		protected string name
		{
			get
			{
				if (properties.ContainsKey("name"))
				{
					return properties["name"];
				}
				return "";
			}

			set
			{
				properties["name"] = value;
			}
		}

		public CsgObject(string name)
		{
			//UnitTests.Run();
			this.name = name;
		}

		public CsgObject(Dictionary<string, string> properties)
		{
			//UnitTests.Run();
			if (properties != null)
			{
				this.properties = properties;
			}
		}

		#region Member Functions

		abstract public AxisAlignedBoundingBox GetAxisAlignedBoundingBox();

		public String Name { get { return name; } }

		public Vector3 GetCenter()
		{
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
			return new Vector3((bounds.maxXYZ + bounds.minXYZ) / 2);
		}

		#region Size Functions

		public virtual Vector3 Size
		{
			get
			{
				AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
				return new Vector3(
					bounds.maxXYZ.X - bounds.minXYZ.X,
					bounds.maxXYZ.Y - bounds.minXYZ.Y,
					bounds.maxXYZ.Z - bounds.minXYZ.Z);
			}

			set
			{
				throw new NotImplementedException("You have to implement this in the derived class.");
			}
		}

		public double XSize { get { return Size.X; } }

		public double YSize { get { return Size.Y; } }

		public double ZSize { get { return Size.Z; } }

		#endregion Size Functions

		#region Mirror Functions

		public CsgObject NewMirrorAccrossX(double offsetFromOrigin = 0, string name = "")
		{
			if (offsetFromOrigin != 0)
			{
				return new Translate(new Scale(this, new Vector3(-1, 1, 1)), new Vector3(offsetFromOrigin * 2, 0, 0), name);
			}
			return new Scale(this, new Vector3(-1, 1, 1), name);
		}

		public CsgObject NewMirrorAccrossY(double offsetFromOrigin = 0, string name = "")
		{
			return new Translate(new Scale(this, new Vector3(1, -1, 1)), new Vector3(0, offsetFromOrigin * 2, 0), name);
		}

		public CsgObject NewMirrorAccrossZ(double offsetFromOrigin = 0, string name = "")
		{
			return new Translate(new Scale(this, new Vector3(1, 1, -1)), new Vector3(0, 0, offsetFromOrigin * 2), name);
		}

		#endregion Mirror Functions

		#endregion Member Functions

		#region Operators

		public static CsgObject operator +(CsgObject left, CsgObject right)
		{
			if (left == null)
			{
				return right;
			}
			if (right == null)
			{
				return left;
			}
			return new Union(left, right);
		}

		public static Union operator +(Union left, CsgObject right)
		{
			left.Add(right);
			return left;
		}

		public static Union operator +(CsgObject left, Union right)
		{
			return right + left;
		}

		public static CsgObject operator -(CsgObject left, CsgObject right)
		{
			return new Difference(left, right);
		}

		public static Difference operator -(Difference left, CsgObject right)
		{
			left.AddToSubtractList(right);
			return left;
		}

		#endregion Operators

		#region Static Functions

		/// <summary>
		/// Normaly ObjectCSG tree can be dirrected acyclic graphs and have instances appear more than once in the tree.
		/// This function will ensure that that every Object is a unique instance in the structure.
		/// </summary>
		/// <param name="dagRoot"></param>
		/// <returns>A new ObjectCSG root that is a new tree of all the objects in the original tree.</returns>
		public static CsgObject Flatten(CsgObject dagRoot)
		{
			CopyAndFlatten flattener = new CopyAndFlatten();
			return flattener.DoCopyAndFlatten((dynamic)dagRoot);
		}

		public static Face GetOposite(Face face)
		{
			Face oppositeFace = face;
			switch (face)
			{
				case Face.Left:
					oppositeFace = Face.Right;
					break;

				case Face.Right:
					oppositeFace = Face.Left;
					break;

				case Face.Front:
					oppositeFace = Face.Back;
					break;

				case Face.Back:
					oppositeFace = Face.Front;
					break;

				case Face.Bottom:
					oppositeFace = Face.Top;
					break;

				case Face.Top:
					oppositeFace = Face.Bottom;
					break;

				default:
					throw new NotImplementedException();
			}

			return oppositeFace;
		}

		#region Structure Optomizations

		public void OptomizeTransforms()
		{
		}

		#endregion Structure Optomizations

		#endregion Static Functions
	}
}