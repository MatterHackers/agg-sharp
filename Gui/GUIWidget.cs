//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using static System.Math;
using static MatterHackers.Agg.RGBA_Bytes;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	[Flags]
	public enum SizeLimitsToSet
	{
		None = 0,
		Minimum = 1,
		Maximum = 2
	}

	[Flags]
	/// <summary>
	/// Sets Horizontal alignment used for a widget, respecting widget margin and parent padding.
	/// </summary>
	public enum HAnchor
	{
		/// <summary>
		/// The widget will not change width automaticaly and will be positions at the OriginRelative to parent in x.
		/// </summary>
		AbsolutePosition = 0,
		/// <summary>
		/// Hold the widget to the paretns left edge, respecting widget margin and parent padding.
		/// </summary>
		ParentLeft = 1,
		ParentCenter = 2,
		ParentRight = 4,
		/// <summary>
		/// Maintain a size that horizontaly encloses all of its visible children.
		/// </summary>
		FitToChildren = 8,
		/// <summary>
		/// Maintin a width that is the same width as its parent.
		/// </summary>
		ParentLeftRight = ParentLeft | ParentRight,
		ParentLeftCenter = ParentLeft | ParentCenter,
		ParentCenterRight = ParentCenter | ParentRight,
		/// <summary>
		/// Take the larger of FitToChildren or ParentLeftRight.
		/// </summary>
		Max_FitToChildren_ParentWidth = FitToChildren | ParentLeftRight,
	};

	/// <summary>
	/// Sets Vertical alignment used for a widget, respecting widget margin and parent padding.
	/// </summary>
	[Flags]
	public enum VAnchor
	{
		AbsolutePosition = 0,
		ParentBottom = 1,
		ParentCenter = 2,
		ParentTop = 4,
		/// <summary>
		/// Maintain a size that verticaly encloses all of its visible children.
		/// </summary>
		FitToChildren = 8,
		ParentBottomTop = ParentBottom | ParentTop,
		ParentBottomCenter = ParentBottom | ParentCenter,
		ParentCenterTop = ParentCenter | ParentTop,
		/// <summary>
		/// Take the larger of FitToChildren or ParentBottomTop.
		/// </summary>
		Max_FitToChildren_ParentHeight = FitToChildren | ParentBottomTop,
	};

	public enum Cursors
	{
		Arrow,
		Cross,
		Default,
		Hand,
		Help,
		HSplit,
		IBeam,
		No,
		NoMove2D,
		NoMoveHoriz,
		NoMoveVert,
		PanEast,
		PanNE,
		PanNorth,
		PanNW,
		PanSE,
		PanSouth,
		PanSW,
		PanWest,
		SizeAll,
		SizeNESW,
		SizeNS,
		SizeNWSE,
		SizeWE,
		UpArrow,
		VSplit,
		WaitCursor
	};

	public enum UnderMouseState
	{
		NotUnderMouse,
		UnderMouseNotFirst,
		FirstUnderMouse
	};

	[DebuggerDisplay("Name = {Name}, Bounds = {LocalBounds}")]
	public class GuiWidget
	{
		private const double dumpIfLongerThanTime = 1;
		private static bool debugShowSize = false;

		private ScreenClipping screenClipping;

		// this should probably some type of dirty rects with the current invalid set stored.
		private bool isCurrentlyInvalid = true;

		public static bool DebugBoundsUnderMouse = false;

		private bool doubleBuffer;
		private ImageBuffer backBuffer;

		private bool debugShowBounds = false;
		private bool widgetHasBeenClosed = false;

		public bool HasBeenClosed { get { return widgetHasBeenClosed; } }

		protected bool WidgetHasBeenClosed { get { return widgetHasBeenClosed; } }

		public bool DebugShowBounds
		{
			get
			{
				if (DebugBoundsUnderMouse)
				{
					if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
					{
						return true;
					}
				}

				return debugShowBounds;
			}

			set
			{
				debugShowBounds = value;
			}
		}

		public LayoutEngine LayoutEngine { get; set; }

		private UnderMouseState underMouseState = UnderMouseState.NotUnderMouse;

		public UnderMouseState UnderMouseState
		{
			get
			{
				return underMouseState;
			}
		}

		static public bool DefaultEnforceIntegerBounds
		{
			get;
			set;
		}

		private bool enforceIntegerBounds = DefaultEnforceIntegerBounds;

		public bool EnforceIntegerBounds
		{
			get { return enforceIntegerBounds; }

			set { enforceIntegerBounds = value; }
		}

		public bool FirstWidgetUnderMouse
		{
			get { return this.UnderMouseState == UnderMouseState.FirstUnderMouse; }
		}

		private RectangleDouble localBounds;

		private bool visible = true;
		private bool enabled = true;

		private bool selectable = true;

		public bool Selectable
		{
			get { return selectable; }
			set { selectable = value; }
		}

		private enum MouseCapturedState { NotCaptured, ChildHasMouseCaptured, ThisHasMouseCaptured };

		private MouseCapturedState mouseCapturedState;

		public bool TabStop { get; set; }

		public virtual int TabIndex { get; set; }

		private RGBA_Bytes backgroundColor = new RGBA_Bytes();

		public RGBA_Bytes BackgroundColor
		{
			get { return backgroundColor; }
			set
			{
				if (backgroundColor != value)
				{
					backgroundColor = value;
					OnBackgroundColorChanged(null);
					Invalidate();
				}
			}
		}

		private BorderDouble padding;

		/// <summary>
		/// The space between the Widget and it's contents (the inside border).
		/// </summary>
		public virtual BorderDouble Padding
		{
			get { return padding; }
			set
			{
				//using (new PerformanceTimer("Draw Timer", "On Layout"))
				{
					if (padding != value)
					{
						padding = value;
						// the padding affects the children so make sure they are layed out
						OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.Padding));
						OnPaddingChanged();
					}
				}
			}
		}

		public virtual void OnPaddingChanged()
		{
			PaddingChanged?.Invoke(this, null);
		}

		/// <summary>
		/// Sets the cusor that will be used when the mouse is over this control
		/// </summary>
		public Cursors Cursor { get; set; }

		private BorderDouble margin;

		public long LastMouseDownMs { get; private set; }

		/// <summary>
		/// The space between the Widget and it's parent (the outside border).
		/// </summary>
		public virtual BorderDouble Margin
		{
			get
			{
				return margin;
			}
			set
			{
				if (margin != value)
				{
					margin = value;
					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.Margin));
					OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.Margin));
					OnMarginChanged();
				}
			}
		}

		[Conditional("DEBUG")]
		public static void BreakInDebugger(string description = "")
		{
			Debug.WriteLine(description);
#if DEBUG && false
			Debugger.Break();
#endif
		}

		public virtual void OnMarginChanged()
		{
			MarginChanged?.Invoke(this, null);
		}

		public bool HAnchorIsSet(HAnchor testFlags)
		{
			return (HAnchor & testFlags) == testFlags;
		}

		public bool HAnchorIsFloating
		{
			get
			{
				int numSet = 0;
				if (HAnchorIsSet(UI.HAnchor.ParentLeft))
				{
					numSet++;
				}
				if (HAnchorIsSet(UI.HAnchor.ParentCenter))
				{
					numSet++;
				}
				if (HAnchorIsSet(UI.HAnchor.ParentRight))
				{
					numSet++;
				}

				return numSet == 1;
			}
		}

		private HAnchor hAnchor;

		public virtual HAnchor HAnchor
		{
			get { return hAnchor; }
			set
			{
				if (hAnchor != value)
				{
					if (value == (HAnchor.ParentLeft | HAnchor.ParentCenter | HAnchor.ParentRight))
					{
						BreakInDebugger("You cannot be anchored to all three positions.");
					}
					hAnchor = value;
					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.HAnchor));

					if (HAnchorIsSet(HAnchor.FitToChildren))
					{
						OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.HAnchor));
					}

					HAnchorChanged?.Invoke(this, null);
				}
			}
		}

		public bool VAnchorIsSet(VAnchor testFlags)
		{
			return (VAnchor & testFlags) == testFlags;
		}

		public bool VAnchorIsFloating
		{
			get
			{
				int numSet = 0;
				if (VAnchorIsSet(UI.VAnchor.ParentBottom))
				{
					numSet++;
				}
				if (VAnchorIsSet(UI.VAnchor.ParentCenter))
				{
					numSet++;
				}
				if (VAnchorIsSet(UI.VAnchor.ParentTop))
				{
					numSet++;
				}

				return numSet == 1;
			}
		}

		private VAnchor vAnchor;

		public virtual VAnchor VAnchor
		{
			get { return vAnchor; }
			set
			{
				if (vAnchor != value)
				{
					if (value == (VAnchor.ParentBottom | VAnchor.ParentCenter | VAnchor.ParentTop))
					{
						BreakInDebugger("You cannot be anchored to all three positions.");
					}
					vAnchor = value;
					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.VAnchor));

					if (VAnchorIsSet(VAnchor.FitToChildren))
					{
						OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.VAnchor));
					}

					VAnchorChanged?.Invoke(this, null);
				}
			}
		}

		public void AnchorAll()
		{
			VAnchor = VAnchor.ParentBottom | VAnchor.ParentTop;
			HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight;
		}

		public void AnchorCenter()
		{
			VAnchor = VAnchor.ParentCenter;
			HAnchor = HAnchor.ParentCenter;
		}

		protected Transform.Affine parentToChildTransform = Affine.NewIdentity();
		private ObservableCollection<GuiWidget> children = new ObservableCollection<GuiWidget>();

		private bool containsFocus = false;

		private int layoutSuspendCount;

		public event EventHandler Layout;

		// the event args will be a DrawEventArgs
		public event DrawEventHandler DrawBefore;

		public event DrawEventHandler DrawAfter;

		public event EventHandler<KeyPressEventArgs> KeyPressed;

		public event EventHandler Invalidated;

		public event KeyEventHandler KeyDown;

		public event KeyEventHandler KeyUp;

		public event WidgetClosingEventHandler Closing;

		public event EventHandler Closed;

		public event EventHandler GotFocus;

		public event EventHandler ParentChanged;

		public event EventHandler LostFocus;

		/// <summary>
		/// The mouse has gone down while in the bounds of this widget
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseDownInBounds;

		/// <summary>
		/// The mouse has gon down on this widget. This will not trigger if a child of this widget gets the down message.
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseDown;

		public event EventHandler<MouseEventArgs> MouseUp;

		public event EventHandler<MouseEventArgs> MouseWheel;

		public event EventHandler<MouseEventArgs> MouseMove;

		public event EventHandler<FlingEventArgs> GestureFling;

		/// <summary>
		/// The mouse has entered the bounds of this widget.  It may also be over a child.
		/// </summary>
		public event EventHandler MouseEnterBounds;

		/// <summary>
		/// The mouse has left the bounds of this widget.
		/// </summary>
		public event EventHandler MouseLeaveBounds;

		/// <summary>
		/// The mouse has entered the bounds of this widget and is also not over a child widget.
		/// </summary>
		public event EventHandler MouseEnter;

		/// <summary>
		/// The mouse has left this widget but may still be over the bounds, it could be above a child.
		/// </summary>
		public event EventHandler MouseLeave;

		public event EventHandler PositionChanged;

		public event EventHandler BoundsChanged;

		public event EventHandler MarginChanged;

		public event EventHandler PaddingChanged;

		public event EventHandler MinimumSizeChanged;

		public event EventHandler BackgroundColorChanged;

		public event EventHandler TextChanged;

		public event EventHandler VisibleChanged;

		public event EventHandler EnabledChanged;

		public event EventHandler VAnchorChanged;

		public event EventHandler HAnchorChanged;

		public event EventHandler ChildAdded;

		public event EventHandler ChildRemoved;

		private static readonly RectangleDouble largestValidBounds = new RectangleDouble(-1000000, -1000000, 1000000, 1000000);

		public GuiWidget(double width, double height, SizeLimitsToSet sizeLimits = SizeLimitsToSet.Minimum)
			: this(HAnchor.AbsolutePosition, VAnchor.AbsolutePosition)
		{
			screenClipping = new ScreenClipping(this);
			if ((sizeLimits & SizeLimitsToSet.Minimum) == SizeLimitsToSet.Minimum)
			{
				MinimumSize = new Vector2(width, height);
			}
			if ((sizeLimits & SizeLimitsToSet.Maximum) == SizeLimitsToSet.Maximum)
			{
				MaximumSize = new Vector2(width, height);
			}
			LocalBounds = new RectangleDouble(0, 0, width, height);
		}

		public GuiWidget(HAnchor hAnchor = HAnchor.AbsolutePosition, VAnchor vAnchor = VAnchor.AbsolutePosition)
		{
			screenClipping = new ScreenClipping(this);
			children.CollectionChanged += children_CollectionChanged;
			LayoutEngine = new LayoutEngineSimpleAlign();
			HAnchor = hAnchor;
			VAnchor = vAnchor;
		}

		private void children_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (childrenLockedInMouseUpCount != 0)
			{
				BreakInDebugger("The mouse should not be locked when the child list changes.");
			}
		}

		public virtual ObservableCollection<GuiWidget> Children
		{
			get
			{
				return children;
			}
		}

		public void ClearRemovedFlag()
		{
			hasBeenRemoved = false;
		}

		public Affine ParentToChildTransform
		{
			get
			{
				return parentToChildTransform;
			}

			set
			{
				//if (parentToChildTransform != value)
				{
					parentToChildTransform = value;
					screenClipping.MarkRecalculate();
				}
			}
		}

		public int CountVisibleChildren()
		{
			int count = 0;
			foreach (GuiWidget child in this.Children)
			{
				if (child.Visible == true)
				{
					count++;
				}
			}
			return count;
		}

		public virtual void OnLostFocus(EventArgs e)
		{
			LostFocus?.Invoke(this, e);
		}

		public virtual void OnGotFocus(EventArgs e)
		{
			GotFocus?.Invoke(this, e);
		}

		private void AllocateBackBuffer()
		{
			RectangleDouble localBounds = LocalBounds;
			int intWidth = Max((int)(Ceiling(localBounds.Right) - Floor(localBounds.Left)) + 1, 1);
			int intHeight = Max((int)(Ceiling(localBounds.Top) - Floor(localBounds.Bottom)) + 1, 1);
			if (backBuffer == null || backBuffer.Width != intWidth || backBuffer.Height != intHeight)
			{
				backBuffer = new ImageBuffer(intWidth, intHeight, 32, new BlenderPreMultBGRA());
			}
		}

		/// <summary>
		/// This will return the backBuffer object for widgets that are double buffered.  It will return null if they are not.
		/// </summary>
		public ImageBuffer BackBuffer
		{
			get
			{
				if (DoubleBuffer)
				{
					return backBuffer;
				}

				return null;
			}
		}

		public bool DoubleBuffer
		{
			get
			{
				return doubleBuffer;
			}

			set
			{
				doubleBuffer = value;
				if (doubleBuffer)
				{
					AllocateBackBuffer();
				}
				else
				{
					backBuffer = null;
				}
			}
		}

		public virtual Vector2 GetDefaultMinimumSize()
		{
			return Vector2.Zero;
		}

		public virtual Keys ModifierKeys
		{
			get
			{
				if (Parent != null)
				{
					return Parent.ModifierKeys;
				}

				return Keys.None;
			}
		}

		private Vector2 minimumSize = new Vector2();

		public virtual Vector2 MinimumSize
		{
			get
			{
				return minimumSize;
			}

			set
			{
				if (value != minimumSize)
				{
					if (value.x < 0 || value.y < 0)
					{
						BreakInDebugger("These have to be 0 or greater.");
					}
					minimumSize = value;

					maximumSize.x = Max(minimumSize.x, maximumSize.x);
					maximumSize.y = Max(minimumSize.y, maximumSize.y);

					RectangleDouble localBounds = LocalBounds;
					if (localBounds.Width < MinimumSize.x)
					{
						localBounds.Right = localBounds.Left + MinimumSize.x;
					}
					if (localBounds.Height < MinimumSize.y)
					{
						localBounds.Top = localBounds.Bottom + MinimumSize.y;
					}
					LocalBounds = localBounds;

					OnMinimumSizeChanged(null);
				}
			}
		}

		public virtual void OnMinimumSizeChanged(EventArgs e)
		{
			MinimumSizeChanged?.Invoke(this, e);
		}

		private Vector2 maximumSize = new Vector2(double.MaxValue, double.MaxValue);

		public Vector2 MaximumSize
		{
			get
			{
				return maximumSize;
			}

			set
			{
				if (value != maximumSize)
				{
					if (value.x < 0 || value.y < 0)
					{
						BreakInDebugger("These have to be 0 or greater.");
					}
					maximumSize = value;

					minimumSize.x = Min(minimumSize.x, maximumSize.x);
					minimumSize.y = Min(minimumSize.y, maximumSize.y);
				}
			}
		}

		public virtual Vector2 OriginRelativeParent
		{
			get
			{
				Affine tempLocalToParentTransform = ParentToChildTransform;
				Vector2 originRelParent = new Vector2(tempLocalToParentTransform.tx, tempLocalToParentTransform.ty);
				return originRelParent;
			}
			set
			{
				Affine tempLocalToParentTransform = ParentToChildTransform;
				if (EnforceIntegerBounds)
				{
					value.x = Floor(value.x);
					value.y = Floor(value.y);
				}

				if (tempLocalToParentTransform.tx != value.x || tempLocalToParentTransform.ty != value.y)
				{
					screenClipping.MarkRecalculate();
					tempLocalToParentTransform.tx = value.x;
					tempLocalToParentTransform.ty = value.y;
					ParentToChildTransform = tempLocalToParentTransform;
					Invalidate();
					if (this.Parent != null)
					{
						// when this object moves it requires that the parent re-layout this object (and maybe others)
						this.Parent.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.Position));
#if false
                        // and it also means the mouse moved realtive to this widget (so the parent and it's children)
                        Vector2 parentMousePosition;
                        if (Parent.GetMousePosition(out parentMousePosition))
                        {
                            this.Parent.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, parentMousePosition.x, parentMousePosition.y, 0));
                        }
#endif
					}
					OnPositionChanged(null);
				}
			}
		}

		public virtual bool GetMousePosition(out Vector2 position)
		{
			position = new Vector2();
			if (Parent != null)
			{
				Vector2 parentMousePosition;
				if (Parent.GetMousePosition(out parentMousePosition))
				{
					position = parentMousePosition;
					ParentToChildTransform.transform(ref position.x, ref position.y);
					return true;
				}
			}

			return false;
		}

		public virtual RectangleDouble LocalBounds
		{
			get
			{
				return localBounds;
			}

			set
			{
				if (value.Width < MinimumSize.x)
				{
					value.Right = value.Left + MinimumSize.x;
				}
				else if (value.Width > MaximumSize.x)
				{
					value.Right = value.Left + MaximumSize.x;
				}

				if (value.Height < MinimumSize.y)
				{
					value.Top = value.Bottom + MinimumSize.y;
				}
				else if (value.Height > MaximumSize.y)
				{
					value.Top = value.Bottom + MaximumSize.y;
				}

				if (EnforceIntegerBounds)
				{
					value.Left = Floor(value.Left);
					value.Bottom = Floor(value.Bottom);
					value.Right = Ceiling(value.Right);
					value.Top = Ceiling(value.Top);
				}

				if (localBounds != value)
				{
					if (!largestValidBounds.Contains(value))
					{
						BreakInDebugger("The bounds you are passing seems like they are probably wrong.  Check it.");
					}

					localBounds = value;

					OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.LocalBounds));
					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.ChildLocalBounds));

					Invalidate();

					OnBoundsChanged(null);

					if (DoubleBuffer)
					{
						AllocateBackBuffer();
					}

					screenClipping.MarkRecalculate();
				}
			}
		}

		public RectangleDouble BoundsRelativeToParent
		{
			get
			{
				RectangleDouble boundsRelParent = LocalBounds;
				boundsRelParent.Offset(OriginRelativeParent.x, OriginRelativeParent.y);
				return boundsRelParent;
			}
			set
			{
				// constrain this to MinimumSize
				if (value.Width < MinimumSize.x)
				{
					value.Right = value.Left + MinimumSize.x;
				}
				if (value.Height < MinimumSize.y)
				{
					value.Top = value.Bottom + MinimumSize.y;
				}
				if (value != BoundsRelativeToParent)
				{
					value.Offset(-OriginRelativeParent.x, -OriginRelativeParent.y);
					LocalBounds = value;
#if false
                    if (Parent != null)
                    {
                        // and it also means the mouse moved realtive to this widget (so the parent and it's children)
                        Vector2 parentMousePosition;
                        if (Parent.GetMousePosition(out parentMousePosition))
                        {
                            this.Parent.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, parentMousePosition.x, parentMousePosition.y, 0));
                        }
                    }
#endif
				}
			}
		}

		public RectangleDouble GetChildrenBoundsIncludingMargins(bool considerChildAnchor = false)
		{
			RectangleDouble boundsOfAllChildrenIncludingMargin = new RectangleDouble();

			if (this.CountVisibleChildren() > 0)
			{
				Vector2 minSize = Vector2.Zero;
				boundsOfAllChildrenIncludingMargin = RectangleDouble.ZeroIntersection;
				bool foundHBounds = false;
				bool foundVBounds = false;
				foreach (GuiWidget child in Children)
				{
					if (child.Visible == false)
					{
						continue;
					}

					if (considerChildAnchor)
					{
						minSize.x = Max(child.Width + child.Margin.Width, minSize.x);
						minSize.y = Max(child.Height + child.Margin.Height, minSize.y);

						RectangleDouble childBoundsWithMargin = child.BoundsRelativeToParent;
						childBoundsWithMargin.Inflate(child.Margin);

						if (!child.HAnchorIsFloating)
						{
							foundHBounds = true;
							// it can't move so make sure our horizontal bounds enclose it
							if (boundsOfAllChildrenIncludingMargin.Right < childBoundsWithMargin.Right)
							{
								boundsOfAllChildrenIncludingMargin.Right = childBoundsWithMargin.Right;
							}

							if (boundsOfAllChildrenIncludingMargin.Left > childBoundsWithMargin.Left)
							{
								boundsOfAllChildrenIncludingMargin.Left = childBoundsWithMargin.Left;
							}
						}

						if (!child.VAnchorIsFloating)
						{
							foundVBounds = true;
							// it can't move so make sure our vertical bounds enclose it
							if (boundsOfAllChildrenIncludingMargin.Top < childBoundsWithMargin.Top)
							{
								boundsOfAllChildrenIncludingMargin.Top = childBoundsWithMargin.Top;
							}

							if (boundsOfAllChildrenIncludingMargin.Bottom > childBoundsWithMargin.Bottom)
							{
								boundsOfAllChildrenIncludingMargin.Bottom = childBoundsWithMargin.Bottom;
							}
						}
					}
					else
					{
						RectangleDouble childBoundsWithMargin = child.BoundsRelativeToParent;
						childBoundsWithMargin.Inflate(child.Margin);
						boundsOfAllChildrenIncludingMargin.ExpandToInclude(childBoundsWithMargin);
					}
				}

				if (considerChildAnchor)
				{
					if (foundHBounds)
					{
						boundsOfAllChildrenIncludingMargin.Right = boundsOfAllChildrenIncludingMargin.Left + Max(boundsOfAllChildrenIncludingMargin.Width, minSize.x);
					}
					else
					{
						boundsOfAllChildrenIncludingMargin.Left = 0;
						boundsOfAllChildrenIncludingMargin.Right = minSize.x;
					}

					if (foundVBounds)
					{
						boundsOfAllChildrenIncludingMargin.Top = boundsOfAllChildrenIncludingMargin.Bottom + Max(boundsOfAllChildrenIncludingMargin.Height, minSize.y);
					}
					else
					{
						boundsOfAllChildrenIncludingMargin.Bottom = 0;
						boundsOfAllChildrenIncludingMargin.Top = minSize.y;
					}
				}
			}

			return boundsOfAllChildrenIncludingMargin;
		}

		public RectangleDouble GetMinimumBoundsToEncloseChildren(bool considerChildAnchor = false)
		{
			RectangleDouble minimumSizeToEncloseChildren = GetChildrenBoundsIncludingMargins(considerChildAnchor);
			minimumSizeToEncloseChildren.Inflate(Padding);
			return minimumSizeToEncloseChildren;
		}

		public void SetBoundsToEncloseChildren()
		{
			RectangleDouble childrenBounds = GetMinimumBoundsToEncloseChildren();
			LocalBounds = childrenBounds;
		}

		public virtual void OnBackgroundColorChanged(EventArgs e)
		{
			BackgroundColorChanged?.Invoke(this, e);
		}

		public virtual void OnBoundsChanged(EventArgs e)
		{
			BoundsChanged?.Invoke(this, e);
		}

		public virtual string Name { get; set; }

		private string text = "";
		public virtual string Text
		{
			get
			{
				return text;
			}

			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("You cannot set the Text to null.");
				}
				if (text != value)
				{
					Invalidate(); // do it before and after in case it changes size.
					text = value;
					OnTextChanged(null);
					Invalidate();
				}
			}
		}

		/// <summary>
		/// If this is set the control will show tool tips on hover, if the platfrom specific SystemWindow implements tool tips.
		/// You can change the settings for the tool tip delays in the containing SystemWindow.
		/// </summary>
		public string ToolTipText
		{
			get; set;
		}

		public virtual void OnTextChanged(EventArgs e)
		{
			TextChanged?.Invoke(this, e);
		}

		public void SetBoundsRelativeToParent(RectangleInt newBounds)
		{
			RectangleDouble bounds = new RectangleDouble(newBounds.Left, newBounds.Bottom, newBounds.Right, newBounds.Top);

			BoundsRelativeToParent = bounds;
		}

		public bool MouseCaptured
		{
			get { return (mouseCapturedState == MouseCapturedState.ThisHasMouseCaptured); }
		}

		public bool ChildHasMouseCaptured
		{
			get { return (mouseCapturedState == MouseCapturedState.ChildHasMouseCaptured); }
		}

		public bool Visible
		{
			get
			{
				return visible;
			}
			set
			{
				if (visible != value)
				{
					visible = value;
					if (visible == false)
					{
						Unfocus();
					}

					OnVisibleChanged(null);

					OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.Visible));
					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.Visible));

					Invalidate();
					screenClipping.MarkRecalculate();
				}
			}
		}

		public virtual bool Enabled
		{
			get
			{
				GuiWidget curGUIWidget = this;
				while (curGUIWidget != null)
				{
					if (!curGUIWidget.enabled)
					{
						return false;
					}
					curGUIWidget = curGUIWidget.Parent;
				}

				return true;
			}
			set
			{
				if (enabled != value)
				{
					enabled = value;
					if (enabled == false)
					{
						Unfocus();
					}

					OnEnabledChanged(null);
				}
			}
		}

		public virtual void OnVisibleChanged(EventArgs e)
		{
			VisibleChanged?.Invoke(this, e);
		}

		private void ClearMouseOverWidget()
		{
			foreach (GuiWidget child in Children)
			{
				ClearMouseOverWidget();
			}

			underMouseState = UnderMouseState.NotUnderMouse;
		}

		public virtual void OnEnabledChanged(EventArgs e)
		{
			if (Enabled == false)
			{
				if (FirstWidgetUnderMouse)
				{
					SetUnderMouseStateRecursive();
					OnMouseLeave(null);
				}
			}

			Invalidate();
			EnabledChanged?.Invoke(this, null);

			foreach (GuiWidget child in Children)
			{
				child.OnParentEnabledChanged(e);
			}
		}

		public virtual void OnParentEnabledChanged(EventArgs e)
		{
			EnabledChanged?.Invoke(this, e);
		}

		private GuiWidget parentBackingStore = null;

		private GuiWidget parent
		{
			set
			{
				if (value == null && parentBackingStore != null)
				{
					if (parentBackingStore.Children.Contains(this))
					{
						throw new Exception("Take this out of the parent before setting this to null.");
					}
				}
				parentBackingStore = value;
			}
		}

		public GuiWidget Parent
		{
			get
			{
				return parentBackingStore;
			}
		}

		// Place holder, this is not really implemented.
		public bool Resizable
		{
			get { return true; }
		}

		public virtual double Width
		{
			get
			{
				return LocalBounds.Width;
			}
			set
			{
				RectangleDouble localBounds = LocalBounds;
				localBounds.Right = localBounds.Left + value;
				LocalBounds = localBounds;
			}
		}

		public virtual double Height
		{
			get
			{
				return LocalBounds.Height;
			}
			set
			{
				RectangleDouble localBounds = LocalBounds;
				localBounds.Top = localBounds.Bottom + value;
				LocalBounds = localBounds;
			}
		}

		public class GuiWidgetEventArgs : EventArgs
		{
			public GuiWidget Child;

			public GuiWidgetEventArgs(GuiWidget child)
			{
				Child = child;
			}
		}

		public virtual void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			//using (new PerformanceTimer("_LAST_", "Add Child"))
			{
#if DEBUG
				if (childToAdd.hasBeenRemoved)
				{
					throw new Exception("You are adding a child that has previously been remove. You should probably be creating a new widget, or calling ClearRemovedFlag() before adding.");
				}
#endif

				if (indexInChildrenList == -1)
				{
					indexInChildrenList = Children.Count;
				}

				if (childToAdd == this)
				{
					BreakInDebugger("A GuiWidget cannot be a child of itself.");
				}
				if (indexInChildrenList > Children.Count)
				{
					throw new IndexOutOfRangeException();
				}
				if (Children.Contains(childToAdd))
				{
					throw new Exception("You cannot add the same child twice.");
				}
				if (childToAdd.Parent != null)
				{
					throw new Exception("This is alread the child of another widget.");
				}
				childToAdd.parent = this;
				childToAdd.widgetHasBeenClosed = false;
				Children.Insert(indexInChildrenList, childToAdd);
				OnChildAdded(new GuiWidgetEventArgs(childToAdd));
				childToAdd.OnParentChanged(null);

				childToAdd.InitLayout();
				OnLayout(new LayoutEventArgs(this, childToAdd, PropertyCausingLayout.AddChild));
			}
		}

		public int GetChildIndex(GuiWidget child)
		{
			for (int i = 0; i < Children.Count; i++)
			{
				if (Children[i] == child)
				{
					return i;
				}
			}

			BreakInDebugger("You asked for the index of a child that is not a child of this widget.");
			return -1;
		}

		public void SendToBack()
		{
			if (Parent == null)
			{
				return;
			}

			Parent.Children.Remove(this);
			Parent.Children.Insert(0, this);
		}

		public virtual void BringToFront()
		{
			if (Parent == null)
			{
				return;
			}

			Parent.Children.Remove(this);
			Parent.Children.Add(this);
		}

		public virtual void OnChildAdded(EventArgs e)
		{
			ChildAdded?.Invoke(this, e);
		}

		public void CloseAllChildren()
		{
			for (int i = Children.Count - 1; i >= 0; i--)
			{
				GuiWidget child = Children[i];
				Children.RemoveAt(i);
				child.parent = null;
				child.Close();
			}
		}

		public void RemoveAllChildren()
		{
			for (int i = Children.Count - 1; i >= 0; i--)
			{
				RemoveChild(Children[i]);
			}
		}

		public virtual void RemoveChild(int index)
		{
			RemoveChild(Children[index]);
		}

		private bool hasBeenRemoved = false;

		public virtual void RemoveChild(GuiWidget childToRemove)
		{
			if (!Children.Contains(childToRemove))
			{
				throw new InvalidOperationException("You can only remove children that this control has.");
			}
			childToRemove.ClearCapturedState();
			childToRemove.hasBeenRemoved = true;
			Children.Remove(childToRemove);
			childToRemove.parent = null;
			childToRemove.OnParentChanged(null);
			OnChildRemoved(new GuiWidgetEventArgs(childToRemove));
			OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.RemoveChild));
			Invalidate();
		}

		public virtual void OnChildRemoved(EventArgs e)
		{
			ChildRemoved?.Invoke(this, e);
		}

		public virtual Graphics2D NewGraphics2D()
		{
			if (DoubleBuffer)
			{
				return BackBuffer.NewGraphics2D();
			}

			if (Parent != null)
			{
				// call recursively to get the first parent that can return a Graphics2D
				Graphics2D parentGraphics2D = Parent.NewGraphics2D();
				if (parentGraphics2D != null)
				{
					Affine parentToLocalTransform = parentGraphics2D.GetTransform();
					parentToLocalTransform *= ParentToChildTransform;
					parentGraphics2D.SetTransform(parentToLocalTransform);

					RectangleDouble currentScreenClipping;
					if (CurrentScreenClipping(out currentScreenClipping))
					{
						parentGraphics2D.SetClippingRect(currentScreenClipping);
						return parentGraphics2D;
					}
				}
			}

			return null;
		}

		public virtual bool PositionWithinLocalBounds(double x, double y)
		{
			if (LocalBounds.Contains(x, y))
			{
				return true;
			}
			return false;
		}

		public void Invalidate()
		{
			Invalidate(LocalBounds);
		}

		public virtual void Invalidate(RectangleDouble rectToInvalidate)
		{
			isCurrentlyInvalid = true;

			if (Parent != null && Parent.Visible)
			{
				rectToInvalidate.Offset(OriginRelativeParent);

				// This code may be a good idea but it needs to be tested to make sure there are no subtle consequences
				//rectToInvalidate.IntersectWithRectangle(Parent.LocalBounds);
				//if (rectToInvalidate.Width > 0 && rectToInvalidate.Height > 0)
				{
					Parent.Invalidate(rectToInvalidate);
				}
			}

			Invalidated?.Invoke(this, new InvalidateEventArgs(rectToInvalidate));
		}

		public virtual void Focus()
		{
			if (CanFocus && CanSelect && !Focused)
			{
				List<GuiWidget> allWidgetsThatWillContainFocus = new List<GuiWidget>();
				List<GuiWidget> allWidgetsThatCurrentlyHaveFocus = new List<GuiWidget>();

				GuiWidget widgetNeedingFocus = this;
				while (widgetNeedingFocus != null)
				{
					allWidgetsThatWillContainFocus.Add(widgetNeedingFocus);
					widgetNeedingFocus = widgetNeedingFocus.Parent;
				}

				GuiWidget currentWithFocus = allWidgetsThatWillContainFocus[allWidgetsThatWillContainFocus.Count - 1];
				while (currentWithFocus != null)
				{
					allWidgetsThatCurrentlyHaveFocus.Add(currentWithFocus);
					GuiWidget childWithFocus = null;
					foreach (GuiWidget child in currentWithFocus.Children)
					{
						if (child.ContainsFocus)
						{
							if (childWithFocus != null)
							{
								BreakInDebugger("Two children should never have focus.");
							}
							childWithFocus = child;
						}
					}
					currentWithFocus = childWithFocus;
				}

				// Try to remove all the widgets we are giving focus to from all the ones that have it.
				// This will leave us with a list of all the widgets that need to lose focus.
				foreach (GuiWidget childThatWillNeedFocus in allWidgetsThatWillContainFocus)
				{
					if (allWidgetsThatCurrentlyHaveFocus.Contains(childThatWillNeedFocus))
					{
						allWidgetsThatCurrentlyHaveFocus.Remove(childThatWillNeedFocus);
					}
				}

				// take the focus away from all the widgets that will not have it after this focus.
				foreach (GuiWidget childThatIsLosingFocus in allWidgetsThatCurrentlyHaveFocus)
				{
					childThatIsLosingFocus.Unfocus();
				}

				// and give focus to everything in our direct parent chain (including this).
				GuiWidget curWidget = this;
				do
				{
					curWidget.containsFocus = true;
					curWidget = curWidget.Parent;
				} while (curWidget != null);

				// finally call any delegates
				OnGotFocus(null);
			}
		}

		public void Unfocus()
		{
			if (containsFocus == true)
			{
				if (Focused)
				{
					containsFocus = false;
					OnLostFocus(null);
					return;
				}

				containsFocus = false;
				foreach (GuiWidget child in Children)
				{
					child.Unfocus();
				}
			}
		}

		public bool CanSelect
		{
			get
			{
				if (Selectable && Parent != null && AllParentsVisibleAndEnabled())
				{
					return true;
				}

				return false;
			}
		}

		private bool AllParentsVisibleAndEnabled()
		{
			GuiWidget curGUIWidget = this;
			RectangleDouble visibleBounds = this.LocalBounds;
			while (curGUIWidget != null)
			{
				if (!curGUIWidget.Visible || !curGUIWidget.Enabled
					|| visibleBounds.Width <= 0
					|| visibleBounds.Height <= 0)
				{
					return false;
				}

				if (curGUIWidget.Parent != null)
				{
					// offset our bounds to the parent bounds
					visibleBounds.Offset(curGUIWidget.OriginRelativeParent.x, curGUIWidget.OriginRelativeParent.y);
					visibleBounds.IntersectWithRectangle(curGUIWidget.Parent.LocalBounds);
				}

				curGUIWidget = curGUIWidget.Parent;
			}

			return true;
		}

		public bool ActuallyVisibleOnScreen()
		{
			GuiWidget curGUIWidget = this;
			RectangleDouble visibleBounds = this.LocalBounds;
			while (curGUIWidget != null)
			{
				if (!curGUIWidget.Visible
					|| visibleBounds.Width <= 0
					|| visibleBounds.Height <= 0)
				{
					return false;
				}

				if (curGUIWidget.Parent != null)
				{
					// offset our bounds to the parent bounds
					visibleBounds.Offset(curGUIWidget.OriginRelativeParent.x, curGUIWidget.OriginRelativeParent.y);
					visibleBounds.IntersectWithRectangle(curGUIWidget.Parent.LocalBounds);
				}

				curGUIWidget = curGUIWidget.Parent;
			}

			return true;
		}

		public virtual bool CanFocus
		{
			get { return Visible && Enabled; }
		}

		public bool Focused
		{
			get
			{
				if (ContainsFocus && CanFocus)
				{
					foreach (GuiWidget child in Children)
					{
						if (child.ContainsFocus)
						{
							return false;
						}
					}

					// we contain focus and none of our children do so we are focused.
					return true;
				}

				return false;
			}
		}

		public bool ContainsFocus
		{
			get
			{
				return containsFocus;
			}
		}

		public void SuspendLayout()
		{
			layoutSuspendCount++;
		}

		public void ResumeLayout()
		{
			layoutSuspendCount--;
		}

		public void PerformLayout()
		{
			OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.PerformLayout));
		}

		public virtual void InitLayout()
		{
		}

		public virtual void OnDragEnter(FileDropEventArgs fileDropEventArgs)
		{
			if (PositionWithinLocalBounds(fileDropEventArgs.X, fileDropEventArgs.Y))
			{
				for (int i = Children.Count - 1; i >= 0; i--)
				{
					GuiWidget child = Children[i];
					if (child.Visible)
					{
						double childX = fileDropEventArgs.X;
						double childY = fileDropEventArgs.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);

						FileDropEventArgs childDropEvent = new FileDropEventArgs(fileDropEventArgs.DroppedFiles, childX, childY);
						child.OnDragEnter(childDropEvent);
						if (childDropEvent.AcceptDrop)
						{
							fileDropEventArgs.AcceptDrop = true;
						}
					}
				}
			}
		}

		public virtual void OnDragOver(FileDropEventArgs fileDropEventArgs)
		{
			if (PositionWithinLocalBounds(fileDropEventArgs.X, fileDropEventArgs.Y))
			{
				for (int i = Children.Count - 1; i >= 0; i--)
				{
					GuiWidget child = Children[i];
					if (child.Visible)
					{
						double childX = fileDropEventArgs.X;
						double childY = fileDropEventArgs.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);

						FileDropEventArgs childDropEvent = new FileDropEventArgs(fileDropEventArgs.DroppedFiles, childX, childY);
						child.OnDragOver(childDropEvent);
						if (childDropEvent.AcceptDrop)
						{
							fileDropEventArgs.AcceptDrop = true;
						}
					}
				}
			}
		}

		public virtual void OnDragDrop(FileDropEventArgs fileDropEventArgs)
		{
			// to do this we would need to implement OnDragOver (and debug it, it was a mess when I started it and don't care right now). // LBB 2013 04 30
			if (PositionWithinLocalBounds(fileDropEventArgs.X, fileDropEventArgs.Y))
			{
				for (int i = Children.Count - 1; i >= 0; i--)
				{
					GuiWidget child = Children[i];
					if (child.Visible)
					{
						double childX = fileDropEventArgs.X;
						double childY = fileDropEventArgs.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);

						FileDropEventArgs childDropEvent = new FileDropEventArgs(fileDropEventArgs.DroppedFiles, childX, childY);
						child.OnDragDrop(childDropEvent);
					}
				}
			}
		}

		public virtual void OnLayout(LayoutEventArgs layoutEventArgs)
		{
			//using (new PerformanceTimer("_LAST_", "Widget OnLayout"))
			{
				if (Visible && layoutSuspendCount < 1)
				{
					if (LayoutEngine != null)
					{
						SuspendLayout();
						LayoutEngine.Layout(layoutEventArgs);
						ResumeLayout();
					}

					Layout?.Invoke(this, layoutEventArgs);
				}
			}
		}

		public virtual void OnParentChanged(EventArgs e)
		{
			ParentChanged?.Invoke(this, e);
		}

		/// <summary>
		/// This is called before the OnDraw method.
		/// When overriding OnPaintBackground in a derived class it is not necessary to call the base class's OnPaintBackground.
		/// </summary>
		/// <param name="graphics2D"></param>
		public virtual void OnDrawBackground(Graphics2D graphics2D)
		{
			if (BackgroundColor.Alpha0To255 > 0)
			{
				graphics2D.FillRectangle(LocalBounds, BackgroundColor);
			}
		}

		public static int DrawCount;

		public virtual void OnDraw(Graphics2D graphics2D)
		{
			//using (new PerformanceTimer("Draw Timer", "Widget Draw"))
			{
				DrawCount++;

				DrawBefore?.Invoke(this, new DrawEventArgs(graphics2D));

				for (int i = 0; i < Children.Count; i++)
				{
					GuiWidget child = Children[i];
					if (child.Visible)
					{
						if (child.DebugShowBounds)
						{
							// draw the margin
							BorderDouble invertedMargin = child.Margin;
							invertedMargin.Left = -invertedMargin.Left;
							invertedMargin.Bottom = -invertedMargin.Bottom;
							invertedMargin.Right = -invertedMargin.Right;
							invertedMargin.Top = -invertedMargin.Top;
							DrawBorderBounds(graphics2D, child.BoundsRelativeToParent, invertedMargin, new RGBA_Bytes(Red, 128));
						}

						RectangleDouble oldClippingRect = graphics2D.GetClippingRect();
						graphics2D.PushTransform();
						{
							Affine currentGraphics2DTransform = graphics2D.GetTransform();
							Affine accumulatedTransform = currentGraphics2DTransform * child.ParentToChildTransform;
							graphics2D.SetTransform(accumulatedTransform);

							RectangleDouble currentScreenClipping;
							if (child.CurrentScreenClipping(out currentScreenClipping))
							{
								currentScreenClipping.Left = Floor(currentScreenClipping.Left);
								currentScreenClipping.Right = Ceiling(currentScreenClipping.Right);
								currentScreenClipping.Bottom = Floor(currentScreenClipping.Bottom);
								currentScreenClipping.Top = Ceiling(currentScreenClipping.Top);
								if (currentScreenClipping.Right < currentScreenClipping.Left || currentScreenClipping.Top < currentScreenClipping.Bottom)
								{
									BreakInDebugger("Right is less than Left or Top is less than Bottom");
								}

								graphics2D.SetClippingRect(currentScreenClipping);

								if (child.DoubleBuffer)
								{
									Vector2 offsetToRenderSurface = new Vector2(currentGraphics2DTransform.tx, currentGraphics2DTransform.ty);
									offsetToRenderSurface += child.OriginRelativeParent;

									double yFraction = offsetToRenderSurface.y - (int)offsetToRenderSurface.y;
									double xFraction = offsetToRenderSurface.x - (int)offsetToRenderSurface.x;
									int xOffset = (int)Floor(child.LocalBounds.Left);
									int yOffset = (int)Floor(child.LocalBounds.Bottom);
									if (child.isCurrentlyInvalid)
									{
										Graphics2D childBackBufferGraphics2D = child.backBuffer.NewGraphics2D();
										childBackBufferGraphics2D.Clear(new RGBA_Bytes(0, 0, 0, 0));
										Affine transformToBuffer = Affine.NewTranslation(-xOffset + xFraction, -yOffset + yFraction);
										childBackBufferGraphics2D.SetTransform(transformToBuffer);
										child.OnDrawBackground(childBackBufferGraphics2D);
										child.OnDraw(childBackBufferGraphics2D);

										child.backBuffer.MarkImageChanged();
										child.isCurrentlyInvalid = false;
									}

									offsetToRenderSurface.x = (int)offsetToRenderSurface.x + xOffset;
									offsetToRenderSurface.y = (int)offsetToRenderSurface.y + yOffset;
									// The transform to draw the backbuffer to the graphics2D must not have a factional amount
									// or we will get aliasing in the image and we want our back buffer pixels to map 1:1 to the next buffer
									if (offsetToRenderSurface.x - (int)offsetToRenderSurface.x != 0
										|| offsetToRenderSurface.y - (int)offsetToRenderSurface.y != 0)
									{
										BreakInDebugger("The transform for a back buffer must be integer to avoid aliasing.");
									}
									graphics2D.SetTransform(Affine.NewTranslation(offsetToRenderSurface));

									graphics2D.Render(child.backBuffer, 0, 0);
								}
								else
								{
									child.OnDrawBackground(graphics2D);
									child.OnDraw(graphics2D);
								}
							}
						}
						graphics2D.PopTransform();
						graphics2D.SetClippingRect(oldClippingRect);
					}
				}

				DrawAfter?.Invoke(this, new DrawEventArgs(graphics2D));

				if (DebugShowBounds)
				{
					// draw the padding
					DrawBorderBounds(graphics2D, LocalBounds, Padding, new RGBA_Bytes(Cyan, 128));

					// show the bounds and inside with an x
					graphics2D.Line(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Top, Green);
					graphics2D.Line(LocalBounds.Left, LocalBounds.Top, LocalBounds.Right, LocalBounds.Bottom, Green);
					graphics2D.Rectangle(LocalBounds, Red);
				}
				if (debugShowSize)
				{
					graphics2D.DrawString(string.Format("{4} {0}, {1} : {2}, {3}", (int)MinimumSize.x, (int)MinimumSize.y, (int)LocalBounds.Width, (int)LocalBounds.Height, Name),
						Width / 2, Max(Height - 16, Height / 2 - 16 * graphics2D.TransformStackCount), color: Magenta, justification: Font.Justification.Center);
				}
			}
		}

		private static void DrawBorderBounds(Graphics2D graphics2D, RectangleDouble bounds, BorderDouble border, RGBA_Bytes color)
		{
			if (border.Width != 0
				|| border.Height != 0)
			{
				PathStorage borderPath = new PathStorage();
				borderPath.MoveTo(bounds.Left, bounds.Bottom);
				borderPath.LineTo(bounds.Left, bounds.Top);
				borderPath.LineTo(bounds.Right, bounds.Top);
				borderPath.LineTo(bounds.Right, bounds.Bottom);
				borderPath.LineTo(bounds.Left, bounds.Bottom);

				borderPath.MoveTo(bounds.Left + border.Left, bounds.Bottom + border.Bottom);
				borderPath.LineTo(bounds.Right - border.Right, bounds.Bottom + border.Bottom);
				borderPath.LineTo(bounds.Right - border.Right, bounds.Top - border.Top);
				borderPath.LineTo(bounds.Left + border.Left, bounds.Top - border.Top);
				borderPath.LineTo(bounds.Left + border.Left, bounds.Bottom + border.Bottom);
				graphics2D.Render(borderPath, color);
			}
		}

		internal class ScreenClipping
		{
			private GuiWidget attachedTo;
			internal bool needRebuild = true;

			internal bool NeedRebuild
			{
				get { return needRebuild; }
				set
				{
					needRebuild = value;
				}
			}

			internal void MarkRecalculate()
			{
				GuiWidget nextParent = attachedTo.Parent;
				while (nextParent != null)
				{
					nextParent.screenClipping.NeedRebuild = true;
					nextParent = nextParent.Parent;
				}

				MarkChildrenRecaculate();
			}

			private void MarkChildrenRecaculate()
			{
				NeedRebuild = true;
				foreach (GuiWidget child in attachedTo.Children)
				{
					child.screenClipping.MarkChildrenRecaculate();
				}
			}

			internal bool visibleAfterClipping = true;
			internal RectangleDouble screenClippingRect;

			internal ScreenClipping(GuiWidget attachedTo)
			{
				this.attachedTo = attachedTo;
			}
		}

		protected virtual bool CurrentScreenClipping(out RectangleDouble screenClippingRect)
		{
			if (screenClipping.NeedRebuild)
			{
				DrawCount++;
				screenClipping.screenClippingRect = TransformToScreenSpace(LocalBounds);

				if (Parent != null)
				{
					RectangleDouble screenParentClipping;
					if (Parent.CurrentScreenClipping(out screenParentClipping))
					{
						RectangleDouble intersectionRect = new RectangleDouble();
						if (intersectionRect.IntersectRectangles(screenClipping.screenClippingRect, screenParentClipping))
						{
							screenClipping.screenClippingRect = intersectionRect;
							screenClipping.visibleAfterClipping = true;
						}
						else
						{
							// this rect is clipped away by the parent rect so return false.
							screenClipping.visibleAfterClipping = false;
						}
					}
					else
					{
						// the parent is completely clipped away, so this is too.
						screenClipping.visibleAfterClipping = false;
					}
				}
				screenClipping.NeedRebuild = false;
			}

			screenClippingRect = screenClipping.screenClippingRect;
			return screenClipping.visibleAfterClipping;
		}

		public virtual void OnClosing(out bool cancelClose)
		{
			cancelClose = false;

			if (Closing != null)
			{
				WidgetClosingEnventArgs closingEventArgs = new WidgetClosingEnventArgs();
				Closing(this, closingEventArgs);
				if (closingEventArgs.Cancel == true)
				{
					// someone canceled it so stop checking.
					cancelClose = true;
					return;
				}
			}
		}

		public void CloseOnIdle()
		{
			UiThread.RunOnIdle(this.Close);
		}

		/// <summary>
		/// Request a close
		/// </summary>
		public void Close()
		{
			if (childrenLockedInMouseUpCount != 0)
			{
				BreakInDebugger("You should put this close onto the UiThread.RunOnIdle so it can happen after the child list is unlocked.");
			}

			if (!widgetHasBeenClosed)
			{
				bool cancelClose;
				OnClosing(out cancelClose);

				// If the close request was aborted by the control, abort the close attempt
				if (cancelClose)
				{
					return;
				}

				widgetHasBeenClosed = true;

				this.CloseAllChildren();

				OnClosed(null);
				if (Parent != null)
				{
					// This code will only execute if this is the actual widget we called close on (not a child of the widget we called close on).
					Parent.RemoveChild(this);
					parent = null;
				}
			}
		}

		public virtual void OnClosed(EventArgs e)
		{
			Closed?.Invoke(this, e);
		}

		public Vector2 TransformFromParentSpace(GuiWidget parentToGetRelativeTo, Vector2 position)
		{
			GuiWidget parent = Parent;
			while (parent != null
				&& parent != parentToGetRelativeTo)
			{
				position -= new Vector2(parent.BoundsRelativeToParent.Left, parent.BoundsRelativeToParent.Bottom);
				parent = parent.Parent;
			}

			return position;
		}

		public Vector2 TransformToParentSpace(GuiWidget parentToGetRelativeTo, Vector2 position)
		{
			GuiWidget widgetToTransformBy = this;
			while (widgetToTransformBy != null
				&& widgetToTransformBy != parentToGetRelativeTo)
			{
				position += new Vector2(widgetToTransformBy.BoundsRelativeToParent.Left, widgetToTransformBy.BoundsRelativeToParent.Bottom);
				widgetToTransformBy = widgetToTransformBy.Parent;
			}

			return position;
		}

		public RectangleDouble TransformToParentSpace(GuiWidget parentToGetRelativeTo, RectangleDouble rectangleToTransform)
		{
			GuiWidget widgetToTransformBy = this;
			while (widgetToTransformBy != null
				&& widgetToTransformBy != parentToGetRelativeTo)
			{
				rectangleToTransform.Offset(widgetToTransformBy.OriginRelativeParent);
				widgetToTransformBy = widgetToTransformBy.Parent;
			}

			return rectangleToTransform;
		}

		public Vector2 TransformToScreenSpace(Vector2 vectorToTransform)
		{
			GuiWidget prevGUIWidget = this;
			while (prevGUIWidget != null)
			{
				vectorToTransform += prevGUIWidget.OriginRelativeParent;
				prevGUIWidget = prevGUIWidget.Parent;
			}

			return vectorToTransform;
		}

		public RectangleDouble TransformToScreenSpace(RectangleDouble rectangleToTransform)
		{
			GuiWidget prevGUIWidget = this;
			while (prevGUIWidget != null)
			{
				rectangleToTransform.Offset(prevGUIWidget.OriginRelativeParent);
				prevGUIWidget = prevGUIWidget.Parent;
			}

			return rectangleToTransform;
		}

		protected GuiWidget GetChildContainingFocus()
		{
			foreach (GuiWidget child in Children)
			{
				if (child.ContainsFocus)
				{
					return child;
				}
			}

			return null;
		}

		private GuiWidget GetFocusedChild()
		{
			GuiWidget childWithFocus = this;
			GuiWidget nextChildWithFocus = GetChildContainingFocus();
			while (nextChildWithFocus != null)
			{
				childWithFocus = nextChildWithFocus;
				nextChildWithFocus = childWithFocus.GetChildContainingFocus();
			}

			return childWithFocus;
		}

		private void DoMouseMovedOffWidgetRecursive(MouseEventArgs mouseEvent)
		{
			foreach (GuiWidget child in Children)
			{
				double childX = mouseEvent.X;
				double childY = mouseEvent.Y;
				child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
				MouseEventArgs childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
				child.DoMouseMovedOffWidgetRecursive(childMouseEvent);
			}

			bool needToCallLeaveBounds = underMouseState != UI.UnderMouseState.NotUnderMouse;
			bool needToCallLeave = UnderMouseState == UI.UnderMouseState.FirstUnderMouse;

			underMouseState = UI.UnderMouseState.NotUnderMouse;

			if (needToCallLeave)
			{
				OnMouseLeave(mouseEvent);
			}

			if (needToCallLeaveBounds)
			{
				OnMouseLeaveBounds(mouseEvent);
			}
		}

		private void SetUnderMouseStateRecursive()
		{
			foreach (GuiWidget child in Children)
			{
				child.SetUnderMouseStateRecursive();
			}
			underMouseState = UI.UnderMouseState.NotUnderMouse;
		}

		public virtual void OnGestureFling(FlingEventArgs flingEvent)
		{
			if (PositionWithinLocalBounds(flingEvent.X, flingEvent.Y))
			{
				//bool childHasAcceptedThisEvent = false;
				for (int i = Children.Count - 1; i >= 0; i--)
				{
					GuiWidget child = Children[i];
					if (child.Visible & child.Enabled)
					{
						double childX = flingEvent.X;
						double childY = flingEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						FlingEventArgs childFlingEvent = new FlingEventArgs(childX, childY, flingEvent.Direction);

						if (child.PositionWithinLocalBounds(childFlingEvent.X, childFlingEvent.Y))
						{
							// recurse in
							child.OnGestureFling(childFlingEvent);
						}
					}
				}

				GestureFling?.Invoke(this, flingEvent);
			}
		}

		public virtual void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				bool childHasAcceptedThisEvent = false;
				bool childHasTakenFocus = false;
				for (int i = Children.Count - 1; i >= 0; i--)
				{
					GuiWidget child = Children[i];
					double childX = mouseEvent.X;
					double childY = mouseEvent.Y;
					child.ParentToChildTransform.inverse_transform(ref childX, ref childY);

					MouseEventArgs childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);

					// If any previous child has accepted the MouseDown, then we won't continue propogating the event and
					// will attempt to fire MovedOffWidget logic
					if (childHasAcceptedThisEvent)
					{
						// another child already took the down so no one else can.
						child.DoMouseMovedOffWidgetRecursive(childMouseEvent);
					}
					else
					{
						if (child.Visible && child.Enabled && child.CanSelect)
						{
							if (child.PositionWithinLocalBounds(childX, childY))
							{
								childHasAcceptedThisEvent = true;
								child.OnMouseDown(childMouseEvent);
								if (child.ContainsFocus)
								{
									childHasTakenFocus = true;
								}
							}
							else
							{
								child.DoMouseMovedOffWidgetRecursive(childMouseEvent);
								child.Unfocus();
							}
						}
					}
				}

				bool mouseEnteredBounds = underMouseState == UI.UnderMouseState.NotUnderMouse;

				if (childHasAcceptedThisEvent)
				{
					mouseCapturedState = MouseCapturedState.ChildHasMouseCaptured;

					if (UnderMouseState == UI.UnderMouseState.FirstUnderMouse)
					{
						underMouseState = UI.UnderMouseState.NotUnderMouse;
						OnMouseLeave(mouseEvent);
					}
					underMouseState = UI.UnderMouseState.UnderMouseNotFirst;
				}
				else
				{
					mouseCapturedState = MouseCapturedState.ThisHasMouseCaptured;
					if (!FirstWidgetUnderMouse)
					{
						underMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnter(mouseEvent);
					}

					MouseDown?.Invoke(this, mouseEvent);
				}

				if (mouseEnteredBounds)
				{
					OnMouseEnterBounds(mouseEvent);
				}

				if (!childHasTakenFocus)
				{
					if (CanFocus)
					{
						Focus();
					}
				}

				MouseDownInBounds?.Invoke(this, mouseEvent);
			}
			else if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
			{
				Unfocus();
				mouseCapturedState = MouseCapturedState.NotCaptured;

				OnMouseLeaveBounds(mouseEvent);
				if (UnderMouseState == UI.UnderMouseState.FirstUnderMouse)
				{
					OnMouseLeave(mouseEvent);
				}
				DoMouseMovedOffWidgetRecursive(mouseEvent);
			}

			LastMouseDownMs = UiThread.CurrentTimerMs;
		}

		public bool IsDoubleClick(MouseEventArgs mouseEvent)
		{
			// The os told up the mouse is 2 cilks (shot time beteewn clicks)
			// but we also want to check if the original click happend on our control.
			if (mouseEvent.Clicks == 2
				&& LastMouseDownMs > UiThread.CurrentTimerMs - 550)
			{
				return true;
			}

			return false;
		}

		private void SetToolTipText(MouseEventArgs mouseEvent)
		{
			if (ToolTipText != null)
			{
				GuiWidget parent = this;
				while (parent.Parent != null
					&& parent as SystemWindow == null)
				{
					parent = parent.Parent;
				}

				SystemWindow systemWindow = parent as SystemWindow;
				systemWindow?.SetHoveredWidget(this);
			}
		}

		internal bool mouseMoveEventHasBeenAcceptedByOther = false;

		public virtual void OnMouseMove(MouseEventArgs mouseEvent)
		{
			mouseMoveEventHasBeenAcceptedByOther = false;
			if (mouseCapturedState == MouseCapturedState.NotCaptured)
			{
				OnMouseMoveNotCaptured(mouseEvent);
			}
			else // either this or a child has the mouse captured
			{
				OnMouseMoveWhenCaptured(mouseEvent);
			}
		}

		public void ValidateMouseCaptureRecursive(GuiWidget lastUpdatedParent = null)
		{
			int countOfChildernThatThinkTheyHaveTheMouseCaptured = 0;
			foreach (GuiWidget child in Children)
			{
				if (child.mouseCapturedState != MouseCapturedState.NotCaptured)
				{
					// keep a count
					countOfChildernThatThinkTheyHaveTheMouseCaptured++;

					// validate that every parent is marked as containing mouse capture
					GuiWidget parent = this.Parent;
					while (parent != null
						&& parent != lastUpdatedParent
						&& this != lastUpdatedParent)
					{
						if (parent.mouseCapturedState != MouseCapturedState.ChildHasMouseCaptured)
						{
							BreakInDebugger("All parents must know a child has the mouse captured.");
						}
						parent = parent.Parent;
					}
				}
				child.ValidateMouseCaptureRecursive(lastUpdatedParent);
			}

			switch (mouseCapturedState)
			{
				case MouseCapturedState.NotCaptured:
				case MouseCapturedState.ThisHasMouseCaptured:
					if (countOfChildernThatThinkTheyHaveTheMouseCaptured != 0)
					{
						BreakInDebugger("No child should have the mouse captured.");
					}
					break;

				case MouseCapturedState.ChildHasMouseCaptured:
					if (countOfChildernThatThinkTheyHaveTheMouseCaptured < 1 || countOfChildernThatThinkTheyHaveTheMouseCaptured > 1)
					{
						BreakInDebugger("One and only one child should ever have the mouse captured.");
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}

		private void OnMouseMoveWhenCaptured(MouseEventArgs mouseEvent)
		{
			if (mouseCapturedState == MouseCapturedState.ChildHasMouseCaptured)
			{
				int countOfChildernThatThinkTheyHaveTheMouseCaptured = 0;
				foreach (GuiWidget child in Children)
				{
					double childX = mouseEvent.X;
					double childY = mouseEvent.Y;
					child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
					MouseEventArgs childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
					if (child.mouseCapturedState != MouseCapturedState.NotCaptured)
					{
						child.OnMouseMove(childMouseEvent);
						countOfChildernThatThinkTheyHaveTheMouseCaptured++;
					}
				}

				if (countOfChildernThatThinkTheyHaveTheMouseCaptured < 1 || countOfChildernThatThinkTheyHaveTheMouseCaptured > 1)
				{
					BreakInDebugger("One and only one child should ever have the mouse captured.");
				}
			}
			else
			{
				if (mouseCapturedState != MouseCapturedState.ThisHasMouseCaptured)
				{
					BreakInDebugger("You should only ever get here if you have the mouse captured.");
				}

				if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
				{
					if (!FirstWidgetUnderMouse)
					{
						underMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnter(mouseEvent);
						OnMouseEnterBounds(mouseEvent);
					}
					else if (underMouseState == UI.UnderMouseState.NotUnderMouse)
					{
						underMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnterBounds(mouseEvent);
					}

					underMouseState = UI.UnderMouseState.FirstUnderMouse;
				}
				else
				{
					if (FirstWidgetUnderMouse)
					{
						underMouseState = UI.UnderMouseState.NotUnderMouse;
						OnMouseLeave(mouseEvent);
						OnMouseLeaveBounds(mouseEvent);
					}
					else if (underMouseState != UI.UnderMouseState.NotUnderMouse)
					{
						underMouseState = UI.UnderMouseState.NotUnderMouse;
						OnMouseLeaveBounds(mouseEvent);
					}

					underMouseState = UI.UnderMouseState.NotUnderMouse;
				}

				MouseMove?.Invoke(this, mouseEvent);
			}
		}

		private void OnMouseMoveNotCaptured(MouseEventArgs mouseEvent)
		{
			if (Parent != null && Parent.mouseMoveEventHasBeenAcceptedByOther)
			{
				mouseMoveEventHasBeenAcceptedByOther = true;
			}

			for (int i = Children.Count - 1; i >= 0; i--)
			{
				GuiWidget child = Children[i];
				double childX = mouseEvent.X;
				double childY = mouseEvent.Y;
				child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
				MouseEventArgs childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
				if (child.Visible && child.Enabled && child.CanSelect)
				{
					child.OnMouseMove(childMouseEvent);
					if (child.PositionWithinLocalBounds(childX, childY))
					{
						mouseMoveEventHasBeenAcceptedByOther = true;
					}
				}
			}

			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				bool needToCallEnterBounds = underMouseState == UI.UnderMouseState.NotUnderMouse;

				if (mouseMoveEventHasBeenAcceptedByOther)
				{
					if (UnderMouseState == UI.UnderMouseState.FirstUnderMouse)
					{
						// set it before we call the function to have the state right to the callee
						underMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						OnMouseLeave(mouseEvent);
					}
					underMouseState = UI.UnderMouseState.UnderMouseNotFirst;
				}
				else
				{
					if (!FirstWidgetUnderMouse)
					{
						if (mouseMoveEventHasBeenAcceptedByOther)
						{
							underMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						}
						else
						{
							underMouseState = UI.UnderMouseState.FirstUnderMouse;
							SetToolTipText(mouseEvent);
							OnMouseEnter(mouseEvent);
						}
					}
					else // we are the first under mouse
					{
						if (mouseMoveEventHasBeenAcceptedByOther)
						{
							underMouseState = UI.UnderMouseState.UnderMouseNotFirst;
							OnMouseLeave(mouseEvent);
						}
					}
				}

				if (needToCallEnterBounds)
				{
					OnMouseEnterBounds(mouseEvent);
				}

				MouseMove?.Invoke(this, mouseEvent);
			}
			else if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
			{
				if (FirstWidgetUnderMouse)
				{
					underMouseState = UI.UnderMouseState.NotUnderMouse;
					OnMouseLeave(mouseEvent);
				}
				underMouseState = UI.UnderMouseState.NotUnderMouse;
				OnMouseLeaveBounds(mouseEvent);
			}
		}

		private int childrenLockedInMouseUpCount = 0;

		public virtual void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (childrenLockedInMouseUpCount != 0)
			{
				BreakInDebugger("This should not be locked.");
			}

			childrenLockedInMouseUpCount++;
			if (mouseCapturedState == MouseCapturedState.NotCaptured)
			{
				if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
				{
					bool childHasAcceptedThisEvent = false;
					for (int i = Children.Count - 1; i >= 0; i--)
					{
						GuiWidget child = Children[i];
						double childX = mouseEvent.X;
						double childY = mouseEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						MouseEventArgs childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
						if (child.Visible && child.Enabled && child.CanSelect)
						{
							if (child.PositionWithinLocalBounds(childX, childY))
							{
								childHasAcceptedThisEvent = true;
								child.OnMouseUp(childMouseEvent);
								i = -1;
							}
							else
							{
								if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
								{
									if (FirstWidgetUnderMouse)
									{
										OnMouseLeave(mouseEvent);
									}
									DoMouseMovedOffWidgetRecursive(mouseEvent);
									underMouseState = UI.UnderMouseState.NotUnderMouse;
								}
							}
						}
					}

					if (!childHasAcceptedThisEvent)
					{
						MouseUp?.Invoke(this, mouseEvent);
					}
				}
			}
			else // either this or a child has the mouse captured
			{
				if (mouseCapturedState == MouseCapturedState.ChildHasMouseCaptured)
				{
					if (childrenLockedInMouseUpCount != 1)
					{
						BreakInDebugger("The mouse should always be locked while in mouse up.");
					}

					int countOfChildernThatThinkTheyHaveTheMouseCaptured = 0;
					foreach (GuiWidget child in Children)
					{
						if (childrenLockedInMouseUpCount != 1)
						{
							BreakInDebugger("The mouse should always be locked while in mouse up.");
						}

						double childX = mouseEvent.X;
						double childY = mouseEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						MouseEventArgs childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
						if (child.mouseCapturedState != MouseCapturedState.NotCaptured)
						{
							if (countOfChildernThatThinkTheyHaveTheMouseCaptured > 0)
							{
								BreakInDebugger("One and only one child should ever have the mouse captured.");
							}
							child.OnMouseUp(childMouseEvent);
							countOfChildernThatThinkTheyHaveTheMouseCaptured++;
						}
					}
				}
				else
				{
					if (mouseCapturedState != MouseCapturedState.ThisHasMouseCaptured)
					{
						BreakInDebugger("You should only ever get here if you have the mouse captured.");
					}

					MouseUp?.Invoke(this, mouseEvent);
				}

				if (!PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
				{
					if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
					{
						if (FirstWidgetUnderMouse)
						{
							underMouseState = UI.UnderMouseState.NotUnderMouse;
							OnMouseLeave(mouseEvent);
							OnMouseLeaveBounds(mouseEvent);
						}
						else
						{
							underMouseState = UI.UnderMouseState.NotUnderMouse;
							OnMouseLeaveBounds(mouseEvent);
						}
						DoMouseMovedOffWidgetRecursive(mouseEvent);
					}
				}

				ClearCapturedState();
			}
			childrenLockedInMouseUpCount--;

			if (childrenLockedInMouseUpCount != 0)
			{
				BreakInDebugger("This should not be locked.");
			}
		}

		protected virtual void SetCursorOnEnter(Cursors cursorToSet)
		{
			Parent?.SetCursorOnEnter(cursorToSet);
		}

		public virtual void OnMouseEnter(MouseEventArgs mouseEvent)
		{
			SetCursorOnEnter(Cursor);
			MouseEnter?.Invoke(this, mouseEvent);
		}

		public virtual void OnMouseLeave(MouseEventArgs mouseEvent)
		{
			MouseLeave?.Invoke(this, mouseEvent);
		}

		public virtual void SendToChildren(object objectToRout)
		{
			foreach (GuiWidget child in Children)
			{
				child.SendToChildren(objectToRout);
			}
		}

		public void FindNamedChildrenRecursive(string nameToSearchFor, List<GuiWidget> foundChildren)
		{
			if (Name == nameToSearchFor)
			{
				foundChildren.Add(this);
			}

			List<GuiWidget> searchChildren = new List<GuiWidget>(Children);
			foreach (GuiWidget child in searchChildren)
			{
				child.FindNamedChildrenRecursive(nameToSearchFor, foundChildren);
			}
		}

		public GuiWidget FindNamedChildRecursive(string nameToSearchFor)
		{
			if (Name == nameToSearchFor)
			{
				return this;
			}

			List<GuiWidget> searchChildren = new List<GuiWidget>(Children);
			foreach (GuiWidget child in searchChildren)
			{
				GuiWidget namedChild = child.FindNamedChildRecursive(nameToSearchFor);
				if (namedChild != null)
				{
					return namedChild;
				}
			}

			return null;
		}

		public virtual void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			MouseEnterBounds?.Invoke(this, mouseEvent);
		}

		public virtual void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			MouseLeaveBounds?.Invoke(this, mouseEvent);
		}

		private void ClearCapturedState()
		{
			if (MouseCaptured || ChildHasMouseCaptured)
			{
				foreach (GuiWidget child in Children)
				{
					child.ClearCapturedState();
				}

				GuiWidget parent = this;
				while (parent != null)
				{
					parent.mouseCapturedState = MouseCapturedState.NotCaptured;
					parent = parent.Parent;
				}
			}
		}

		public virtual void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				for (int i = Children.Count - 1; i >= 0; i--)
				{
					GuiWidget child = Children[i];
					if (child.Visible & child.Enabled)
					{
						double childX = mouseEvent.X;
						double childY = mouseEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						MouseEventArgs childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);

						if (child.PositionWithinLocalBounds(childMouseEvent.X, childMouseEvent.Y))
						{
							// recurse in
							child.OnMouseWheel(childMouseEvent);
							mouseEvent.WheelDelta = childMouseEvent.WheelDelta;
						}
					}
				}

				MouseWheel?.Invoke(this, mouseEvent);
			}
		}

		public virtual void OnKeyPress(KeyPressEventArgs keyPressEvent)
		{
			GuiWidget childWithFocus = GetChildContainingFocus();
			if (childWithFocus != null && childWithFocus.Visible && childWithFocus.Enabled)
			{
				childWithFocus.OnKeyPress(keyPressEvent);
			}

			if (KeyPressed != null)
			{
				KeyPressed(this, keyPressEvent);
			}
		}

		public virtual void OnPositionChanged(EventArgs e)
		{
			if (PositionChanged != null)
			{
				PositionChanged(this, e);
			}
		}

		private static int SortOnTabIndex(GuiWidget one, GuiWidget two)
		{
			return one.TabIndex.CompareTo(two.TabIndex);
		}

		private void AddAllTabStopsRecursive(List<GuiWidget> allWidgetsThatAreTabStops)
		{
			foreach (GuiWidget child in Children)
			{
				if (child.Visible && child.Selectable)
				{
					child.AddAllTabStopsRecursive(allWidgetsThatAreTabStops);
				}
			}

			if (TabStop)
			{
				allWidgetsThatAreTabStops.Add(this);
			}
		}

		protected void AdvanceFocus(int andvanceAmount)
		{
			if (Parent != null)
			{
				List<GuiWidget> allWidgetsThatAreTabStops = new List<GuiWidget>();

				GuiWidget topParent = Parent;
				while (topParent != null && topParent.Parent != null)
				{
					topParent = topParent.Parent;
				}

				topParent.AddAllTabStopsRecursive(allWidgetsThatAreTabStops);
				if (allWidgetsThatAreTabStops.Count > 0)
				{
					allWidgetsThatAreTabStops.Sort(SortOnTabIndex);

					int currentIndex = allWidgetsThatAreTabStops.IndexOf(this);
					int nextIndex = (currentIndex + andvanceAmount) % allWidgetsThatAreTabStops.Count;
					if (nextIndex < 0)
					{
						nextIndex += allWidgetsThatAreTabStops.Count;
					}

					if (currentIndex != nextIndex)
					{
						allWidgetsThatAreTabStops[nextIndex].Focus();
						allWidgetsThatAreTabStops[nextIndex].OnKeyDown(new KeyEventArgs(Keys.A | Keys.Control));
					}
				}
			}
		}

		protected void FocusNext()
		{
			AdvanceFocus(1);
		}

		protected void FocusPrevious()
		{
			AdvanceFocus(-1);
		}

		public virtual void OnKeyDown(KeyEventArgs keyEvent)
		{
			GuiWidget childWithFocus = GetChildContainingFocus();
			if (childWithFocus != null && childWithFocus.Visible && childWithFocus.Enabled)
			{
				childWithFocus.OnKeyDown(keyEvent);
			}

			if (!keyEvent.Handled && keyEvent.KeyCode == Keys.Tab && ContainsFocus)
			{
				if (keyEvent.Shift)
				{
					FocusPrevious();
				}
				else
				{
					FocusNext();
				}
				keyEvent.Handled = true;
				keyEvent.SuppressKeyPress = true;
			}

			if (KeyDown != null)
			{
				KeyDown(this, keyEvent);
			}
		}

		public virtual void OnKeyUp(KeyEventArgs keyEvent)
		{
			GuiWidget childWithFocus = GetChildContainingFocus();
			if (childWithFocus != null && childWithFocus.Visible && childWithFocus.Enabled)
			{
				childWithFocus.OnKeyUp(keyEvent);
			}

			if (KeyUp != null)
			{
				KeyUp(this, keyEvent);
			}
		}
	}

	public static class ExtensionMethods
	{
		/// <summary>
		/// Returns all children of the current GuiWiget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>All matching child widgets</returns>
		public static IEnumerable<T> Children<T>(this GuiWidget widget) where T : GuiWidget
		{
			return widget.Children.Where(w => w is T).Select(w => (T)w);
		}

		/// <summary>
		/// Returns all ancestors of the current GuiWidget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>The matching ancestor widgets</returns>
		public static IEnumerable<T> Parents<T>(this GuiWidget widget) where T : GuiWidget
		{
			GuiWidget context = widget.Parent;
			while (context != null)
			{
				if (context is T)
				{
					yield return (T)context;
				}

				context = context.Parent;
			}
		}
	}
}