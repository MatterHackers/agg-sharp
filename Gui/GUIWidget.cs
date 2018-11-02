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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using static System.Math;
using static MatterHackers.Agg.Color;

namespace MatterHackers.Agg.UI
{
	public class LayoutLock : IDisposable
	{
		GuiWidget item;

		public LayoutLock(GuiWidget item)
		{
			this.item = item;
			item.LayoutLockCount++;
		}

		public void Dispose()
		{
			item.LayoutLockCount--;
		}
	}

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
		/// The widget will not change width automatically and will be positions at the OriginRelative to parent in x.
		/// </summary>
		Absolute = 0,
		/// <summary>
		/// Hold the widget to the parents left edge, respecting widget margin and parent padding.
		/// </summary>
		Left = 1,
		Center = 2,
		Right = 4,
		/// <summary>
		/// Maintain a size that horizontally encloses all of its visible children.
		/// </summary>
		Fit = 8,
		/// <summary>
		/// Maintain a width that is the same width as its parent.
		/// </summary>
		Stretch = Left | Right,
		/// <summary>
		/// Take the larger of FitToChildren or Stretch.
		/// </summary>
		MaxFitOrStretch = Fit | Stretch,
	};

	/// <summary>
	/// Sets Vertical alignment used for a widget, respecting widget margin and parent padding.
	/// </summary>
	[Flags]
	public enum VAnchor
	{
		Absolute = 0,
		Bottom = 1,
		Center = 2,
		Top = 4,
		/// <summary>
		/// Maintain a size that vertically encloses all of its visible children.
		/// </summary>
		Fit = 8,
		Stretch = Bottom | Top,
		/// <summary>
		/// Take the larger of FitToChildren or Stretch.
		/// </summary>
		MaxFitOrStretch = Fit | Stretch,
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

	public class GuiWidget
	{
		public static double DeviceScale { get; set; } = 1;

		private const double dumpIfLongerThanTime = 1;
		private static bool debugShowSize = false;

		private ScreenClipping screenClipping;

		// this should probably some type of dirty rects with the current invalid set stored.
		private bool isCurrentlyInvalid = true;

		public static bool DebugBoundsUnderMouse = false;

		private bool doubleBuffer;
		private ImageBuffer backBuffer;

		public bool HasBeenClosed { get; private set; }

		private bool debugShowBounds = false;
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
				if (debugShowBounds != value)
				{
					debugShowBounds = value;
					Invalidate();
				}
			}
		}

		public LayoutEngine LayoutEngine { get; protected set; }

		public UnderMouseState UnderMouseState { get; private set; }

		public bool ContainsFirstUnderMouseRecursive()
		{
			if (UnderMouseState == UnderMouseState.FirstUnderMouse)
			{
				return true;
			}
			if (UnderMouseState == UnderMouseState.NotUnderMouse)
			{
				return false;
			}
			foreach (var child in Children)
			{
				if (child.ContainsFirstUnderMouseRecursive())
				{
					return true;
				}
			}

			return false;
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

		public bool Selectable { get; set; } = true;

		private enum MouseCapturedState { NotCaptured, ChildHasMouseCaptured, ThisHasMouseCaptured };

		private MouseCapturedState mouseCapturedState;

		public bool TabStop { get; set; }

		public virtual int TabIndex { get; set; }

		#region BackgroundColor
		private Color _backgroundColor = new Color();

		public virtual Color BackgroundColor
		{
			get { return _backgroundColor; }
			set
			{
				if (_backgroundColor != value)
				{
					_backgroundColor = value;
					OnBackgroundColorChanged(null);
					Invalidate();
				}
			}
		}

		public event EventHandler BackgroundColorChanged;

		public virtual void OnBackgroundColorChanged(EventArgs e)
		{
			BackgroundColorChanged?.Invoke(this, e);
		}
		#endregion

		/// <summary>
		/// The boarder and padding scaled by the DeviceScale (used by the layout engine)
		/// </summary>
		public BorderDouble DevicePadding
		{
			get;
			private set;
		}

		#region Padding
		public event EventHandler PaddingChanged;

		private BorderDouble _padding;

		/// <summary>
		/// The space between the Widget and it's contents (the inside border).
		/// </summary>
		[Category("Layout")]
		public virtual BorderDouble Padding
		{
			get { return _padding; }
			set
			{
				//using (new PerformanceTimer("Draw Timer", "On Layout"))
				{
					if (_padding != value)
					{
						_padding = value;
						DevicePadding = _padding * GuiWidget.DeviceScale;
						if (EnforceIntegerBounds)
						{
							DevicePadding.Round();
						}
						// the padding affects the children so make sure they are laid out
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
		#endregion

		#region Border
		private Color _borderColor = Color.Transparent;

		public virtual Color BorderColor
		{
			get { return _borderColor; }
			set
			{
				if (_borderColor != value)
				{
					_borderColor = value;
					OnBorderColorChanged(null);
					Invalidate();
				}
			}
		}

		public event EventHandler BorderColorChanged;

		public virtual void OnBorderColorChanged(EventArgs e)
		{
			BorderColorChanged?.Invoke(this, e);
		}

		public event EventHandler BorderChanged;

		private BorderDouble deviceBorder;
		private BorderDouble _border;

		/// <summary>
		/// The space between the Widget and its border.
		/// </summary>
		[Category("Layout")]
		public BorderDouble Border
		{
			get { return _border; }
			set
			{
				//using (new PerformanceTimer("Draw Timer", "On Layout"))
				{
					if (_border != value)
					{
						_border = value;
						deviceBorder = _border * GuiWidget.DeviceScale;
						if (EnforceIntegerBounds)
						{
							deviceBorder.Round();
						}
						// the border affects the children so make sure they are laid out
						OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.Border));
						OnBorderChanged();
					}
				}
			}
		}

		public virtual void OnBorderChanged()
		{
			BorderChanged?.Invoke(this, null);
		}
		#endregion

		#region Margin
		public event EventHandler MarginChanged;

		private BorderDouble margin;

		public long LastMouseDownMs { get; private set; }

		BorderDouble deviceMargin;

		/// <summary>
		/// The Margin scaled by the DeviceScale
		/// </summary>
		public BorderDouble DeviceMarginAndBorder
		{
			get { return deviceMargin + deviceBorder; }
		}

		/// <summary>
		/// The space between the Widget and it's parent (the outside border).
		/// </summary>
		[Category("Layout")]
		public BorderDouble Margin
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
					deviceMargin = Margin * GuiWidget.DeviceScale;

					if (EnforceIntegerBounds)
					{
						deviceMargin.Round();
					}
					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.Margin));
					OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.Margin));
					OnMarginChanged();
				}
			}
		}

		public virtual void OnMarginChanged()
		{
			MarginChanged?.Invoke(this, null);
		}
		#endregion

		/// <summary>
		/// Sets the cursor that will be used when the mouse is over this control
		/// </summary>
		public virtual Cursors Cursor { get; set; }

		[Conditional("DEBUG")]
		public static void BreakInDebugger(string description = "")
		{
			Debug.WriteLine(description);
#if DEBUG && false
			Debugger.Break();
#endif
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
				if (HAnchorIsSet(UI.HAnchor.Left))
				{
					numSet++;
				}
				if (HAnchorIsSet(UI.HAnchor.Center))
				{
					numSet++;
				}
				if (HAnchorIsSet(UI.HAnchor.Right))
				{
					numSet++;
				}

				return numSet == 1;
			}
		}

		private HAnchor hAnchor;

		[Category("Layout Anchor")]
		public virtual HAnchor HAnchor
		{
			get { return hAnchor; }
			set
			{
				if (hAnchor != value)
				{
					if (value == (HAnchor.Left | HAnchor.Center | HAnchor.Right))
					{
						BreakInDebugger("You cannot be anchored to all three positions.");
					}
					hAnchor = value;
					this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.HAnchor));

					if (HAnchorIsSet(HAnchor.Fit))
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
				if (VAnchorIsSet(UI.VAnchor.Bottom))
				{
					numSet++;
				}
				if (VAnchorIsSet(UI.VAnchor.Center))
				{
					numSet++;
				}
				if (VAnchorIsSet(UI.VAnchor.Top))
				{
					numSet++;
				}

				return numSet == 1;
			}
		}

		private VAnchor vAnchor;

		[Category("Layout Anchor")]
		public VAnchor VAnchor
		{
			get { return vAnchor; }
			set
			{
				if (vAnchor != value)
				{
					if (value == (VAnchor.Bottom | VAnchor.Center | VAnchor.Top))
					{
						BreakInDebugger("You cannot be anchored to all three positions.");
					}
					vAnchor = value;

					if (this.Visible)
					{
						this.Parent?.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.VAnchor));

						if (VAnchorIsSet(VAnchor.Fit))
						{
							OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.VAnchor));
						}
					}

					VAnchorChanged?.Invoke(this, null);
				}
			}
		}

		public void AnchorAll()
		{
			VAnchor = VAnchor.Bottom | VAnchor.Top;
			HAnchor = HAnchor.Left | HAnchor.Right;
		}

		public void AnchorCenter()
		{
			VAnchor = VAnchor.Center;
			HAnchor = HAnchor.Center;
		}

		protected Transform.Affine parentToChildTransform = Affine.NewIdentity();
		private bool containsFocus = false;

		internal int LayoutLockCount { get; set; }

		public LayoutLock LayoutLock()
		{
			return new LayoutLock(this);
		}

		public bool LayoutLocked
		{
			get
			{
				return LayoutLockCount > 0;
			}
		}

		public event EventHandler Layout;

		// the event args will be a DrawEventArgs
		public event EventHandler<DrawEventArgs> BeforeDraw;

		public event EventHandler<DrawEventArgs> AfterDraw;

		public event EventHandler<KeyPressEventArgs> KeyPressed;

		public event EventHandler Invalidated;

		public event EventHandler<KeyEventArgs> KeyDown;

		public event EventHandler<KeyEventArgs> KeyUp;

		public event EventHandler Closed;

		public event EventHandler ParentChanged;

		public event EventHandler FocusChanged;

		public event EventHandler<FocusChangedArgs> ContainsFocusChanged;

		/// <summary>
		/// The mouse has gone down on this widget. This will not trigger if a child of this widget gets the down message.
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseDownCaptured;

		public event EventHandler<MouseEventArgs> MouseUpCaptured;

		public class FocusChangedArgs : EventArgs
		{
			public FocusChangedArgs(GuiWidget sourceWidget, bool focused)
			{
				this.Focused = focused;
				this.SourceWidget = sourceWidget;
			}

			public bool Focused { get; }

			public GuiWidget SourceWidget { get; }
		}

		/// <summary>
		/// The mouse has gone down while in the bounds of this widget
		/// </summary>
		public event EventHandler<MouseEventArgs> MouseDown;

		public event EventHandler<MouseEventArgs> MouseUp;

		public event EventHandler<MouseEventArgs> Click;

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

		public event EventHandler BoundsChanged;

		public event EventHandler MinimumSizeChanged;

		public event EventHandler TextChanged;

		public event EventHandler VisibleChanged;

		public event EventHandler EnabledChanged;

		public event EventHandler VAnchorChanged;

		public event EventHandler HAnchorChanged;

		public event EventHandler ChildAdded;

		public event EventHandler ChildRemoved;

		private static readonly RectangleDouble largestValidBounds = new RectangleDouble(-1000000, -1000000, 1000000, 1000000);

		public GuiWidget(double width, double height, SizeLimitsToSet sizeLimits = SizeLimitsToSet.Minimum)
			: this()
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

		public GuiWidget()
		{
			screenClipping = new ScreenClipping(this);
			LayoutEngine = new LayoutEngineSimpleAlign();
			HAnchor = hAnchor;
			VAnchor = vAnchor;
		}

		public override string ToString()
		{
			return $"Name = {Name}, Bounds = {LocalBounds} - {GetType().Name}";
		}

		public List<GuiWidget> Children { get; } = new List<GuiWidget>();

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

		public virtual void OnFocusChanged(EventArgs e)
		{
			FocusChanged?.Invoke(this, e);
		}

		public virtual void OnContainsFocusChanged(FocusChangedArgs e)
		{
			ContainsFocusChanged?.Invoke(this, e);
		}

		private void AllocateBackBuffer()
		{
			RectangleDouble localBounds = LocalBounds;
			int intWidth = Max((int)(Ceiling(localBounds.Right) - Floor(localBounds.Left)), 1);
			int intHeight = Max((int)(Ceiling(localBounds.Top) - Floor(localBounds.Bottom)), 1);
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
				if (this.DoubleBuffer != value)
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

					Invalidate();
				}
			}
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

		[Category("Layout Constraints")]
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
					if (value.X < 0 || value.Y < 0)
					{
						BreakInDebugger("These have to be 0 or greater.");
					}
					minimumSize = value;

					maximumSize.X = Max(minimumSize.X, maximumSize.X);
					maximumSize.Y = Max(minimumSize.Y, maximumSize.Y);

					RectangleDouble localBounds = LocalBounds;
					if (localBounds.Width < MinimumSize.X)
					{
						localBounds.Right = localBounds.Left + MinimumSize.X;
					}
					if (localBounds.Height < MinimumSize.Y)
					{
						localBounds.Top = localBounds.Bottom + MinimumSize.Y;
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

		[Category("Layout Constraints")]
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
					if (value.X < 0 || value.Y < 0)
					{
						BreakInDebugger("These have to be 0 or greater.");
					}
					maximumSize = value;

					minimumSize.X = Min(minimumSize.X, maximumSize.X);
					minimumSize.Y = Min(minimumSize.Y, maximumSize.Y);
				}
			}
		}

		#region Position
		public event EventHandler PositionChanged;

		public virtual void OnPositionChanged(EventArgs e)
		{
			PositionChanged?.Invoke(this, e);
		}

		/// <summary>
		/// The bottom left position of the widget in its parent space (or the logical/intuitive position).
		/// </summary>
		[Category("Layout")]
		public Vector2 Position
		{
			get
			{
				var bounds = BoundsRelativeToParent;
				return new Vector2(bounds.Left, bounds.Bottom);
			}

			set
			{
				if (value != Position)
				{
					var delta = value - Position;
					OriginRelativeParent = OriginRelativeParent + delta;
					OnPositionChanged(null);
				}
			}
		}
		#endregion

		#region Size
		public event EventHandler SizeChanged;

		public virtual void OnSizeChanged(EventArgs e)
		{
			SizeChanged?.Invoke(this, e);
		}

		/// <summary>
		/// The width height of the control (its size!)
		/// </summary>
		[Category("Layout")]
		public Vector2 Size
		{
			get
			{
				return new Vector2(LocalBounds.Width, LocalBounds.Height);
			}

			set
			{
				Width = value.X;
				Height = value.Y;
			}
		}
		#endregion

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
					value.X = Math.Round(value.X);
					value.Y = Math.Round(value.Y);
				}

				if (tempLocalToParentTransform.tx != value.X || tempLocalToParentTransform.ty != value.Y)
				{
					screenClipping.MarkRecalculate();
					tempLocalToParentTransform.tx = value.X;
					tempLocalToParentTransform.ty = value.Y;
					ParentToChildTransform = tempLocalToParentTransform;
					Invalidate();
					if (this.Parent != null)
					{
						// when this object moves it requires that the parent re-layout this object (and maybe others)
						if (!this.Parent.LayoutLocked)
						{
							this.Parent.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.Position));
						}
						#if false
						// and it also means the mouse moved relative to this widget (so the parent and it's children)
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

		[Category("Layout")]
		public virtual RectangleDouble LocalBounds
		{
			get
			{
				return localBounds;
			}

			set
			{
				if (value.Width < MinimumSize.X)
				{
					value.Right = value.Left + MinimumSize.X;
				}
				else if (value.Width > MaximumSize.X)
				{
					value.Right = value.Left + MaximumSize.X;
				}

				if (value.Height < MinimumSize.Y)
				{
					value.Top = value.Bottom + MinimumSize.Y;
				}
				else if (value.Height > MaximumSize.Y)
				{
					value.Top = value.Bottom + MaximumSize.Y;
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
					if (this.Parent != null
						&& !this.Parent.LayoutLocked)
					{
						this.Parent.OnLayout(new LayoutEventArgs(this.Parent, this, PropertyCausingLayout.ChildLocalBounds));
					}

					Invalidate();

					if (DoubleBuffer)
					{
						AllocateBackBuffer();
					}

					OnBoundsChanged(null);

					screenClipping.MarkRecalculate();
				}
			}
		}

		public RectangleDouble BoundsRelativeToParent
		{
			get
			{
				RectangleDouble boundsRelParent = LocalBounds;
				boundsRelParent.Offset(OriginRelativeParent.X, OriginRelativeParent.Y);
				return boundsRelParent;
			}
			set
			{
				// constrain this to MinimumSize
				if (value.Width < MinimumSize.X)
				{
					value.Right = value.Left + MinimumSize.X;
				}
				if (value.Height < MinimumSize.Y)
				{
					value.Top = value.Bottom + MinimumSize.Y;
				}
				if (value != BoundsRelativeToParent)
				{
					value.Offset(-OriginRelativeParent.X, -OriginRelativeParent.Y);
					LocalBounds = value;
#if false
                    if (Parent != null)
                    {
                        // and it also means the mouse moved relative to this widget (so the parent and it's children)
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

		public RectangleDouble GetChildrenBoundsIncludingMargins(bool considerChildAnchor = false, Func<GuiWidget, GuiWidget, bool> considerChild = null)
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
					if (child.Visible == false
					|| (considerChild != null && !considerChild(this, child)))
					{
						continue;
					}

					if (considerChildAnchor)
					{
						var childSize = child.MinimumSize;
						minSize.X = Max((child.HAnchor == HAnchor.Stretch) ? 0 : child.Width
							+ child.DeviceMarginAndBorder.Width, minSize.X);
						minSize.Y = Max((child.VAnchor == VAnchor.Stretch) ? 0 : child.Height
							+ child.DeviceMarginAndBorder.Height, minSize.Y);

						RectangleDouble childBoundsWithMargin = child.BoundsRelativeToParent;
						childBoundsWithMargin.Inflate(child.DeviceMarginAndBorder);

						FlowLayoutWidget flowLayout = this as FlowLayoutWidget;
						bool childHSizeHasBeenAdjusted = flowLayout != null && (flowLayout.FlowDirection == FlowDirection.LeftToRight || flowLayout.FlowDirection == FlowDirection.RightToLeft);
						if (!child.HAnchorIsFloating
							&& (child.HAnchor != HAnchor.Stretch || childHSizeHasBeenAdjusted))
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

						bool childVSizeHasBeenAdjusted = flowLayout != null && (flowLayout.FlowDirection == FlowDirection.BottomToTop || flowLayout.FlowDirection == FlowDirection.TopToBottom);
						if (!child.VAnchorIsFloating
							&& (child.VAnchor != VAnchor.Stretch || childVSizeHasBeenAdjusted))
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
						boundsOfAllChildrenIncludingMargin.Right = boundsOfAllChildrenIncludingMargin.Left + Max(boundsOfAllChildrenIncludingMargin.Width, minSize.X);
					}
					else
					{
						boundsOfAllChildrenIncludingMargin.Left = 0;
						boundsOfAllChildrenIncludingMargin.Right = minSize.X;
					}

					if (foundVBounds)
					{
						boundsOfAllChildrenIncludingMargin.Top = boundsOfAllChildrenIncludingMargin.Bottom + Max(boundsOfAllChildrenIncludingMargin.Height, minSize.Y);
					}
					else
					{
						boundsOfAllChildrenIncludingMargin.Bottom = 0;
						boundsOfAllChildrenIncludingMargin.Top = minSize.Y;
					}
				}
			}

			return boundsOfAllChildrenIncludingMargin;
		}

		public RectangleDouble GetMinimumBoundsToEncloseChildren(bool considerChildAnchor = false)
		{
			RectangleDouble minimumSizeToEncloseChildren = GetChildrenBoundsIncludingMargins(considerChildAnchor);
			minimumSizeToEncloseChildren.Inflate(DevicePadding);
			return minimumSizeToEncloseChildren;
		}

		public void SetBoundsToEncloseChildren()
		{
			RectangleDouble childrenBounds = GetMinimumBoundsToEncloseChildren();
			LocalBounds = childrenBounds;
		}

		public virtual void OnBoundsChanged(EventArgs e)
		{
			BoundsChanged?.Invoke(this, e);

			// make sure we call size changed (we are planning to depricate bounds changed at some point)
			OnSizeChanged(e);
		}

		public string Name { get; set; }

		private string _text = "";
		public virtual string Text
		{
			get => _text;
			set
			{
				// make sure value is set to empty string rather than null
				value = value ?? "";
				if (_text != value)
				{
					_text = value;
					OnTextChanged(null);
					Invalidate();
				}
			}
		}

		/// <summary>
		/// If this is set the control will show tool tips on hover, if the platform specific SystemWindow implements tool tips.
		/// You can change the settings for the tool tip delays in the containing SystemWindow.
		/// </summary>
		public virtual string ToolTipText { get; set; }

		public virtual void OnTextChanged(EventArgs e)
		{
			TextChanged?.Invoke(this, e);
		}

		public void SetBoundsRelativeToParent(RectangleInt newBounds)
		{
			RectangleDouble bounds = new RectangleDouble(newBounds.Left, newBounds.Bottom, newBounds.Right, newBounds.Top);

			BoundsRelativeToParent = bounds;
		}

		public bool MouseCaptured => mouseCapturedState == MouseCapturedState.ThisHasMouseCaptured;

		public bool ChildHasMouseCaptured => mouseCapturedState == MouseCapturedState.ChildHasMouseCaptured;

		public virtual bool Visible
		{
			get => visible;
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
			get => this.enabled && this.Parent?.Enabled != false;
			set
			{
				if (enabled != value)
				{
					enabled = value;
					if (enabled == false)
					{
						ClearCapturedState();
						Unfocus();
					}

					this.Invalidate();

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
				child.ClearMouseOverWidget();
			}

			UnderMouseState = UI.UnderMouseState.NotUnderMouse;
		}

		public virtual void OnEnabledChanged(EventArgs e)
		{
			if (Enabled == false)
			{
				if (FirstWidgetUnderMouse)
				{
					ClearMouseOverWidget();
					OnMouseLeave(null);
				}
			}

			Invalidate();
			EnabledChanged?.Invoke(this, null);

			foreach (GuiWidget child in Children)
			{
				child.OnEnabledChanged(e);
			}
		}

		private GuiWidget _parent = null;

		public GuiWidget Parent
		{
			get => _parent;
			set
			{
				if (value == null && _parent != null)
				{
					if (_parent.Children.Contains(this))
					{
						throw new Exception("Take this out of the parent before setting this to null.");
					}
				}

				_parent = value;
			}
		}

		#region Risizeable
		private bool _resizable = true;

		public bool Resizable
		{
			get => _resizable;

			set
			{
				if (_resizable != value)
				{
					_resizable = value;
					OnResizeableChanged(null);
					Invalidate();
				}
			}
		}

		public event EventHandler ResizeableChanged;

		public virtual void OnResizeableChanged(EventArgs e)
		{
			ResizeableChanged?.Invoke(this, e);
		}
		#endregion

		// Place holder, this is not really implemented.

		[Category("Layout")]
		public double Width
		{
			get => LocalBounds.Width;
			set
			{
				if (value != Width)
				{
					RectangleDouble localBounds = LocalBounds;
					localBounds.Right = localBounds.Left + value;
					LocalBounds = localBounds;
				}
			}
		}

		[Category("Layout")]
		public double Height
		{
			get => LocalBounds.Height;
			set
			{
				if (value != Height)
				{
					RectangleDouble localBounds = LocalBounds;
					localBounds.Top = localBounds.Bottom + value;
					LocalBounds = localBounds;
				}
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
					throw new Exception("You are adding a child that has previously been removed. You should probably be creating a new widget, or calling ClearRemovedFlag() before adding.");
				}
#endif

				// first thing we do is make sure the child has been initialized
				childToAdd.Initialize();

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
					throw new Exception("This is already the child of another widget.");
				}
				childToAdd.Parent = this;
				childToAdd.HasBeenClosed = false;
				Children.Insert(indexInChildrenList, childToAdd);
				OnChildAdded(new GuiWidgetEventArgs(childToAdd));
				childToAdd.OnParentChanged(null);

				childToAdd.InitLayout();
				OnLayout(new LayoutEventArgs(this, childToAdd, PropertyCausingLayout.AddChild));
			}
		}

		/// <summary>
		/// Override this to create child controls and other
		/// </summary>
		public virtual void Initialize()
		{
			Initialized = true;
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
				child.Parent = null;
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

		public void ReplaceChild(GuiWidget existing, GuiWidget replacement)
		{
			int pos = this.GetChildIndex(existing);
			if (pos >= 0)
			{
				this.Children[pos].Close();
				this.AddChild(replacement, pos);
			}
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
			childToRemove.Parent = null;
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

		public bool PositionWithinLocalBounds(Vector2 postion)
		{
			return PositionWithinLocalBounds(postion.X, postion.Y);
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

			var threadSafeParent = Parent;
			if (threadSafeParent != null && threadSafeParent.Visible)
			{
				rectToInvalidate.Offset(OriginRelativeParent);

				// This code may be a good idea but it needs to be tested to make sure there are no subtle consequences
				//rectToInvalidate.IntersectWithRectangle(Parent.LocalBounds);
				//if (rectToInvalidate.Width > 0 && rectToInvalidate.Height > 0)
				{
					threadSafeParent.Invalidate(rectToInvalidate);
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
				OnFocusChanged(null);
			}
		}

		public void Unfocus()
		{
			if (containsFocus == true)
			{
				if (Focused)
				{
					containsFocus = false;
					OnContainsFocusChanged(new FocusChangedArgs(this, false));
					OnFocusChanged(null);
					return;
				}

				// If it is still focused it was not the primary widget one of its children was
				if (containsFocus)
				{
					containsFocus = false;
					OnContainsFocusChanged(new FocusChangedArgs(this, false));
					foreach (GuiWidget child in Children.ToArray())
					{
						child.Unfocus();
					}
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

				var parent = curGUIWidget.Parent;
				if (parent != null)
				{
					// offset our bounds to the parent bounds
					visibleBounds.Offset(curGUIWidget.OriginRelativeParent.X, curGUIWidget.OriginRelativeParent.Y);
					visibleBounds.IntersectWithRectangle(parent.LocalBounds);
				}

				curGUIWidget = parent;
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
					visibleBounds.Offset(curGUIWidget.OriginRelativeParent.X, curGUIWidget.OriginRelativeParent.Y);
					visibleBounds.IntersectWithRectangle(curGUIWidget.Parent.LocalBounds);
				}

				curGUIWidget = curGUIWidget.Parent;
			}

			return true;
		}

		public virtual bool CanFocus => this.Visible && this.Enabled;

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

		public bool ContainsFocus => containsFocus;

		public bool Initialized { get; private set; } = false;

		public void PerformLayout()
		{
			OnLayout(new LayoutEventArgs(this, null, PropertyCausingLayout.PerformLayout));
		}

		public virtual void InitLayout()
		{
		}

		public virtual void OnLayout(LayoutEventArgs layoutEventArgs)
		{
			if (this.HasBeenClosed)
			{
				return;
			}

			if (Visible && !LayoutLocked)
			{
				LayoutCount++;

				if ((LayoutCount % 11057) == 0)
				{
					int a = 0;
				}

				if (LayoutEngine != null)
				{
					using (LayoutLock())
					{
						LayoutEngine.Layout(layoutEventArgs);
					}
				}

				Layout?.Invoke(this, layoutEventArgs);
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
		public static int LayoutCount;

		protected bool formHasLoaded = false;

		/// <summary>
		/// Called before the very first draw of this widget
		/// </summary>
		public event EventHandler Load;

		/// <summary>
		/// Called before the very first draw of this widget
		/// </summary>
		/// <param name="args"></param>
		public virtual void OnLoad(EventArgs args)
		{
			Load?.Invoke(this, args);
		}

		public virtual void OnDraw(Graphics2D graphics2D)
		{
			//using (new PerformanceTimer("Draw Timer", "Widget Draw"))
			{
				if (!formHasLoaded)
				{
					OnLoad(null);
					formHasLoaded = true;
				}

				DrawCount++;

				BeforeDraw?.Invoke(this, new DrawEventArgs(graphics2D));

				for (int i = 0; i < Children.Count; i++)
				{
					GuiWidget child = Children[i];
					if (child.Visible)
					{
						if (child.DebugShowBounds)
						{
							// draw the margin
							BorderDouble invertedMargin = child.DeviceMarginAndBorder;
							invertedMargin.Left = -invertedMargin.Left;
							invertedMargin.Bottom = -invertedMargin.Bottom;
							invertedMargin.Right = -invertedMargin.Right;
							invertedMargin.Top = -invertedMargin.Top;
							DrawBorderAndPaddingBounds(graphics2D, child.BoundsRelativeToParent, invertedMargin, new Color(Red, 128));
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

									double yFraction = offsetToRenderSurface.Y - (int)offsetToRenderSurface.Y;
									double xFraction = offsetToRenderSurface.X - (int)offsetToRenderSurface.X;
									int xOffset = (int)Floor(child.LocalBounds.Left);
									int yOffset = (int)Floor(child.LocalBounds.Bottom);
									if (child.isCurrentlyInvalid)
									{
										Graphics2D childBackBufferGraphics2D = child.backBuffer.NewGraphics2D();
										childBackBufferGraphics2D.Clear(new Color(0, 0, 0, 0));
										Affine transformToBuffer = Affine.NewTranslation(-xOffset + xFraction, -yOffset + yFraction);
										childBackBufferGraphics2D.SetTransform(transformToBuffer);
										child.OnDrawBackground(childBackBufferGraphics2D);
										child.OnDraw(childBackBufferGraphics2D);

										child.backBuffer.MarkImageChanged();
										child.isCurrentlyInvalid = false;
									}

									offsetToRenderSurface.X = (int)offsetToRenderSurface.X + xOffset;
									offsetToRenderSurface.Y = (int)offsetToRenderSurface.Y + yOffset;
									// The transform to draw the backbuffer to the graphics2D must not have a factional amount
									// or we will get aliasing in the image and we want our back buffer pixels to map 1:1 to the next buffer
									if (offsetToRenderSurface.X - (int)offsetToRenderSurface.X != 0
										|| offsetToRenderSurface.Y - (int)offsetToRenderSurface.Y != 0)
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

						DrawBorder(graphics2D, child);
					}
				}

				AfterDraw?.Invoke(this, new DrawEventArgs(graphics2D));

				if (DebugShowBounds)
				{
					// draw the padding
					DrawBorderAndPaddingBounds(graphics2D, LocalBounds, DevicePadding, new Color(Cyan, 128));

					// show the bounds and inside with an x
					graphics2D.Line(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Top, new Color(Green, 100), 3);
					graphics2D.Line(LocalBounds.Left, LocalBounds.Top, LocalBounds.Right, LocalBounds.Bottom, new Color(Green, 100), 3);
					graphics2D.Rectangle(LocalBounds, Red);

					RenderAnchoreInfo(graphics2D);
				}
				if (debugShowSize)
				{
					graphics2D.DrawString(string.Format("{4} {0}, {1} : {2}, {3}", (int)MinimumSize.X, (int)MinimumSize.Y, (int)LocalBounds.Width, (int)LocalBounds.Height, Name),
						Width / 2, Max(Height - 16, Height / 2 - 16 * graphics2D.TransformStackCount), color: Magenta, justification: Font.Justification.Center);
				}
			}
		}

		private void RenderAnchoreInfo(Graphics2D graphics2D)
		{
			var color = Color.Cyan;
			double size = 10;

			// an arrow pointing right
			VertexStorage rightArrow = new VertexStorage();
			rightArrow.MoveTo(new Vector2(size * 2, 0));
			rightArrow.LineTo(new Vector2(size * 1, size * .6));
			rightArrow.LineTo(new Vector2(size * 1, -size * .6));

			if (HAnchor == HAnchor.Absolute)
			{
				//graphics2D.Line(LocalBounds.Center + new Vector2(0, size * .8),
				//LocalBounds.Center + new Vector2(0, -size * .8),
				//color, size * .5);
			}
			else // figure out what it is
			{
				if (HAnchor.HasFlag(HAnchor.Left))
				{
					graphics2D.Render(new VertexSourceApplyTransform(rightArrow, Affine.NewRotation(MathHelper.DegreesToRadians(180))), LocalBounds.Center, color);
				}
				if (HAnchor.HasFlag(HAnchor.Center))
				{
					graphics2D.Circle(LocalBounds.Center, size / 2, color);
				}
				if (HAnchor.HasFlag(HAnchor.Right))
				{
					graphics2D.Render(rightArrow, LocalBounds.Center, color);
				}
				if (HAnchor.HasFlag(HAnchor.Fit))
				{
					// draw the right arrow offset
					var offsetArrow = new VertexSourceApplyTransform(rightArrow, Affine.NewTranslation(-size * 3, 0));
					graphics2D.Render(offsetArrow, LocalBounds.Center, color);
					graphics2D.Render(new VertexSourceApplyTransform(offsetArrow,
						Affine.NewRotation(MathHelper.DegreesToRadians(180))), LocalBounds.Center, color);
				}
			}

			if (VAnchor == VAnchor.Absolute)
			{
				//graphics2D.Line(LocalBounds.Center + new Vector2(size * .8, 0),
				//LocalBounds.Center + new Vector2(-size * .8, 0),
				//color, size * .5);
			}
			else // figure out what it is
			{
				var upArrow = new VertexSourceApplyTransform(rightArrow, Affine.NewRotation(MathHelper.DegreesToRadians(90)));
				if (VAnchor.HasFlag(VAnchor.Bottom))
				{
					graphics2D.Render(new VertexSourceApplyTransform(upArrow, Affine.NewRotation(MathHelper.DegreesToRadians(180))), LocalBounds.Center, color);
				}
				if (VAnchor.HasFlag(VAnchor.Center))
				{
					graphics2D.Circle(LocalBounds.Center, size / 2, color);
				}
				if (VAnchor.HasFlag(VAnchor.Top))
				{
					graphics2D.Render(upArrow, LocalBounds.Center, color);
				}
				if (VAnchor.HasFlag(VAnchor.Fit))
				{
					// draw the right arrow offset
					var offsetArrow = new VertexSourceApplyTransform(upArrow, Affine.NewTranslation(0, -size * 3));
					graphics2D.Render(offsetArrow, LocalBounds.Center, color);
					graphics2D.Render(new VertexSourceApplyTransform(offsetArrow,
						Affine.NewRotation(MathHelper.DegreesToRadians(180))), LocalBounds.Center, color);
				}
			}
		}

		private static void DrawBorderAndPaddingBounds(Graphics2D graphics2D, RectangleDouble bounds, BorderDouble border, Color color)
		{
			if (border.Width != 0
				|| border.Height != 0)
			{
				VertexStorage borderPath = new VertexStorage();
				// put in the bounds
				borderPath.MoveTo(bounds.Left, bounds.Bottom);
				borderPath.LineTo(bounds.Left, bounds.Top);
				borderPath.LineTo(bounds.Right, bounds.Top);
				borderPath.LineTo(bounds.Right, bounds.Bottom);
				borderPath.LineTo(bounds.Left, bounds.Bottom);

				// take out inside the border
				borderPath.MoveTo(bounds.Left + border.Left, bounds.Bottom + border.Bottom);
				borderPath.LineTo(bounds.Right - border.Right, bounds.Bottom + border.Bottom);
				borderPath.LineTo(bounds.Right - border.Right, bounds.Top - border.Top);
				borderPath.LineTo(bounds.Left + border.Left, bounds.Top - border.Top);
				borderPath.LineTo(bounds.Left + border.Left, bounds.Bottom + border.Bottom);
				graphics2D.Render(borderPath, color);
			}
		}

		protected void DrawBorder(Graphics2D graphics2D, GuiWidget child)
		{
			var childDeviceBorder = child.deviceBorder;
			var childBorderColor = child.BorderColor;

			if (childBorderColor == Color.Transparent
				|| (childDeviceBorder.Left == 0
					&& childDeviceBorder.Right == 0
					&& childDeviceBorder.Bottom == 0
					&& childDeviceBorder.Top == 0))
			{
				return;
			}

			var childBounds = child.TransformToParentSpace(this, child.localBounds);
			//bounds = this.localBounds;
			//graphics2D.FillRectangle(bounds, new Color(Color.Cyan, 100));
			//var expand = bounds;
			//expand.Inflate(1);
			//graphics2D.Rectangle(expand, new Color(Color.Magenta, 100));

			if (childDeviceBorder.Left > 0)
			{
				// do a fill rect that does not include the top or bottom
				graphics2D.FillRectangle(childBounds.Left,
					childBounds.Bottom,
					childBounds.Left - childDeviceBorder.Left,
					childBounds.Top,
					childBorderColor);
			}

			if (childDeviceBorder.Bottom > 0)
			{
				// do a fill rect
				graphics2D.FillRectangle(childBounds.Left - childDeviceBorder.Left,
					childBounds.Bottom,
					childBounds.Right + childDeviceBorder.Right,
					childBounds.Bottom - childDeviceBorder.Bottom,
					childBorderColor);
			}

			if (childDeviceBorder.Right > 0)
			{
				// do a fill rect that does not include the top or bottom
				graphics2D.FillRectangle(childBounds.Right + childDeviceBorder.Right,
					childBounds.Bottom,
					childBounds.Right,
					childBounds.Top,
					childBorderColor);
			}

			if (childDeviceBorder.Top > 0)
			{
				// do a fill rect
				graphics2D.FillRectangle(childBounds.Left - childDeviceBorder.Left,
					childBounds.Top + childDeviceBorder.Top,
					childBounds.Right + childDeviceBorder.Right,
					childBounds.Top,
					childBorderColor);
			}
		}

		internal class ScreenClipping
		{
			private readonly GuiWidget attachedTo;

			internal bool NeedRebuild { get; set; } = true;

			internal void MarkRecalculate()
			{
				GuiWidget nextParent = attachedTo.Parent;
				while (nextParent != null)
				{
					nextParent.screenClipping.NeedRebuild = true;
					nextParent = nextParent.Parent;
				}

				MarkChildrenRecalculate();
			}

			private void MarkChildrenRecalculate()
			{
				if (attachedTo.HasBeenClosed)
				{
					return;
				}

				NeedRebuild = true;

				foreach (GuiWidget child in attachedTo.Children)
				{
					child.screenClipping.MarkChildrenRecalculate();

					if (attachedTo.HasBeenClosed)
					{
						return;
					}
				}
			}

			internal bool visibleAfterClipping = true;
			internal RectangleDouble screenClippingRect;

			internal ScreenClipping(GuiWidget attachedTo)
			{
				this.attachedTo = attachedTo;
			}
		}

		protected bool CurrentScreenClipping(out RectangleDouble screenClippingRect)
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

		public void CloseOnIdle()
		{
			if (!HasBeenClosed)
			{
				UiThread.RunOnIdle(() => this.Close());
			}
		}

		public void Close()
		{
			if (childrenLockedInMouseUpCount != 0)
			{
				BreakInDebugger("You should put this close onto the UiThread.RunOnIdle so it can happen after the child list is unlocked.");
			}

			// Validate via OnClosing if SystemWindow.Close is called
			if (this is SystemWindow systemWindow)
			{
				var closingArgs = new ClosingEventArgs();
				systemWindow.OnClosing(closingArgs);

				if (closingArgs.Cancel)
				{
					return;
				}
			}

			if (!HasBeenClosed)
			{
				HasBeenClosed = true;

				this.CloseAllChildren();

				OnClosed(null);
				if (Parent != null)
				{
					// This code will only execute if this is the actual widget we called close on (not a child of the widget we called close on).
					Parent.RemoveChild(this);
					this.Parent = null;
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

			while (prevGUIWidget != null
				&& !(prevGUIWidget is SystemWindow))
			{
				vectorToTransform += prevGUIWidget.OriginRelativeParent;
				prevGUIWidget = prevGUIWidget.Parent;
			}

			return vectorToTransform;
		}

		public GuiWidget TopmostParent()
		{
			return this.Parents<SystemWindow>().FirstOrDefault() ?? this.Parents<GuiWidget>().Last();
		}

		public Vector2 TransformFromScreenSpace(Vector2 vectorToTransform)
		{
			return this.TransformFromParentSpace(TopmostParent(), vectorToTransform);
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
			bool needToCallLeaveBounds = UnderMouseState != UI.UnderMouseState.NotUnderMouse;
			bool needToCallLeave = UnderMouseState == UI.UnderMouseState.FirstUnderMouse;

			UnderMouseState = UI.UnderMouseState.NotUnderMouse;

			if (needToCallLeave)
			{
				OnMouseLeave(mouseEvent);
			}

			if (needToCallLeaveBounds)
			{
				OnMouseLeaveBounds(mouseEvent);
			}

			foreach (GuiWidget child in Children)
			{
				double childX = mouseEvent.X;
				double childY = mouseEvent.Y;
				child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
				MouseEventArgs childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);
				child.DoMouseMovedOffWidgetRecursive(childMouseEvent);
			}
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
			bool focusStateBeforeProcessing = containsFocus;
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				bool willBeInChild = false;

				// figure out what state we will be in when done
				for (int i = Children.Count - 1; i >= 0; i--)
				{
					GuiWidget child = Children[i];
					double childX = mouseEvent.X;
					double childY = mouseEvent.Y;
					child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
					if (child.Visible
						&& child.Enabled
						&& child.CanSelect
						&& child.PositionWithinLocalBounds(childX, childY))
					{
						willBeInChild = true;
						break;
					}
				}

				if (willBeInChild)
				{
					if (UnderMouseState == UnderMouseState.FirstUnderMouse)
					{
						// set it before we call the function to have the state right to the callee
						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						OnMouseLeave(mouseEvent);
					}
					else if (UnderMouseState == UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						OnMouseEnterBounds(mouseEvent);
					}
					UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
				}
				else // It is in this but not children. It will be the first under mouse
				{
					if (UnderMouseState == UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnterBounds(mouseEvent);
						OnMouseEnter(mouseEvent);
					}
					else if (UnderMouseState == UnderMouseState.UnderMouseNotFirst)
					{
						UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnter(mouseEvent);
					}
				}

				bool childHasAcceptedThisEvent = false;
				bool childHasTakenFocus = false;
				for (int i = Children.Count - 1; i >= 0; i--)
				{
					GuiWidget child = Children[i];
					double childX = mouseEvent.X;
					double childY = mouseEvent.Y;
					child.ParentToChildTransform.inverse_transform(ref childX, ref childY);

					MouseEventArgs childMouseEvent = new MouseEventArgs(mouseEvent, childX, childY);

					// If any previous child has accepted the MouseDown, then we won't continue propagating the event and
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

				if (childHasAcceptedThisEvent)
				{
					mouseCapturedState = MouseCapturedState.ChildHasMouseCaptured;
				}
				else
				{
					mouseCapturedState = MouseCapturedState.ThisHasMouseCaptured;

					MouseDownCaptured?.Invoke(this, mouseEvent);
				}

				if (!childHasTakenFocus)
				{
					if (CanFocus)
					{
						Focus();
					}
				}

				MouseDown?.Invoke(this, mouseEvent);
			}
			// not under the mouse
			else if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
			{
				Unfocus();
				mouseCapturedState = MouseCapturedState.NotCaptured;
				UnderMouseState = UnderMouseState.NotUnderMouse;

				OnMouseLeaveBounds(mouseEvent);
				if (UnderMouseState == UI.UnderMouseState.FirstUnderMouse)
				{
					OnMouseLeave(mouseEvent);
				}
				DoMouseMovedOffWidgetRecursive(mouseEvent);
			}

			LastMouseDownMs = UiThread.CurrentTimerMs;

			if (focusStateBeforeProcessing != containsFocus)
			{
				OnContainsFocusChanged(new FocusChangedArgs(this, containsFocus));
			}
		}

		public bool IsDoubleClick(MouseEventArgs mouseEvent)
		{
			// The OS told up the mouse is 2 clicks (shot time between clicks)
			// but we also want to check if the original click happened on our control.
			if (mouseEvent.Clicks == 2
				&& LastMouseDownMs > UiThread.CurrentTimerMs - 550)
			{
				return true;
			}

			return false;
		}

		public bool MouseDownOnWidget
		{
			get
			{
				return mouseCapturedState == MouseCapturedState.ThisHasMouseCaptured;
			}
		}

		public static bool TouchScreenMode { get; protected set; }

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
						UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnter(mouseEvent);
						OnMouseEnterBounds(mouseEvent);
					}
					else if (UnderMouseState == UI.UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
						OnMouseEnterBounds(mouseEvent);
					}

					UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
				}
				else
				{
					if (FirstWidgetUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.NotUnderMouse;
						OnMouseLeave(mouseEvent);
						OnMouseLeaveBounds(mouseEvent);
					}
					else if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.NotUnderMouse;
						OnMouseLeaveBounds(mouseEvent);
					}

					UnderMouseState = UI.UnderMouseState.NotUnderMouse;
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

			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				if (mouseMoveEventHasBeenAcceptedByOther)
				{
					if (UnderMouseState == UnderMouseState.FirstUnderMouse)
					{
						// set it before we call the function to have the state right to the callee
						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						OnMouseLeave(mouseEvent);
					}
					else if (UnderMouseState == UnderMouseState.NotUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
						OnMouseEnterBounds(mouseEvent);
					}
				}
				else
				{
					bool willBeInChild = false;

					// figure out what state we will be in when done
					for (int i = Children.Count - 1; i >= 0; i--)
					{
						GuiWidget child = Children[i];
						double childX = mouseEvent.X;
						double childY = mouseEvent.Y;
						child.ParentToChildTransform.inverse_transform(ref childX, ref childY);
						if (child.Visible
							&& child.Enabled
							&& child.CanSelect
							&& child.PositionWithinLocalBounds(childX, childY))
						{
							willBeInChild = true;
							break;
						}
					}

					if (willBeInChild)
					{
						if (UnderMouseState == UnderMouseState.FirstUnderMouse)
						{
							// set it before we call the function to have the state right to the callee
							UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
							OnMouseLeave(mouseEvent);
						}
						else if (UnderMouseState == UnderMouseState.NotUnderMouse)
						{
							UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
							OnMouseEnterBounds(mouseEvent);
						}
						UnderMouseState = UI.UnderMouseState.UnderMouseNotFirst;
					}
					else // It is in this but not children. It will be the first under mouse
					{
						if (UnderMouseState == UnderMouseState.NotUnderMouse)
						{
							UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
							OnMouseEnterBounds(mouseEvent);
							OnMouseEnter(mouseEvent);
						}
						else if (UnderMouseState == UnderMouseState.UnderMouseNotFirst)
						{
							UnderMouseState = UI.UnderMouseState.FirstUnderMouse;
							OnMouseEnter(mouseEvent);
						}
					}
				}
			}
			else // mouse is not in this bounds
			{
				if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
				{
					if (FirstWidgetUnderMouse)
					{
						UnderMouseState = UI.UnderMouseState.NotUnderMouse;
						OnMouseLeave(mouseEvent);
					}
					UnderMouseState = UI.UnderMouseState.NotUnderMouse;
					OnMouseLeaveBounds(mouseEvent);
				}
			}

			MouseMove?.Invoke(this, mouseEvent);

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
					mouseEvent.AcceptDrop |= childMouseEvent.AcceptDrop;
					if (child.UnderMouseState != UnderMouseState.NotUnderMouse)
					{
						mouseMoveEventHasBeenAcceptedByOther = true;
					}
				}
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

			bool mouseUpOnWidget = PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y);
			bool childHasAcceptedThisEvent = false;

			if (mouseCapturedState == MouseCapturedState.NotCaptured)
			{
				if (mouseUpOnWidget)
				{
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
									UnderMouseState = UI.UnderMouseState.NotUnderMouse;
								}
							}
						}
					}

					if (!childHasAcceptedThisEvent)
					{
						MouseUpCaptured?.Invoke(this, mouseEvent);
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
					for (int childIndex = 0; childIndex < Children.Count(); childIndex++)
					{
						GuiWidget child = Children[childIndex];
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

					bool upHappenedAboveChild = false;
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
								upHappenedAboveChild = true;
								break;
							}
						}
					}

					if (!upHappenedAboveChild)
					{
						MouseUpCaptured?.Invoke(this, mouseEvent);

						if (mouseUpOnWidget)
						{
							OnClick(mouseEvent);
						}
					}
				}

				if (!mouseUpOnWidget)
				{
					if (UnderMouseState != UI.UnderMouseState.NotUnderMouse)
					{
						if (FirstWidgetUnderMouse)
						{
							UnderMouseState = UI.UnderMouseState.NotUnderMouse;
							OnMouseLeave(mouseEvent);
							OnMouseLeaveBounds(mouseEvent);
						}
						else
						{
							UnderMouseState = UI.UnderMouseState.NotUnderMouse;
							OnMouseLeaveBounds(mouseEvent);
						}
						DoMouseMovedOffWidgetRecursive(mouseEvent);
					}
				}

				ClearCapturedState();
			}

			MouseUp?.Invoke(this, mouseEvent);

			childrenLockedInMouseUpCount--;

			if (childrenLockedInMouseUpCount != 0)
			{
				BreakInDebugger("This should not be locked.");
			}
		}

		/// <summary>
		/// Fire a mouse click within the bounds of the control
		/// </summary>
		public void InvokeClick()
		{
			this.OnClick(new MouseEventArgs(MouseButtons.Left, 1, new[] { this.Position + Vector2.One }, 0, null));
		}

		public virtual void OnClick(MouseEventArgs mouseEvent)
		{
			Click?.Invoke(this, mouseEvent);
		}

		protected virtual void SetCursor(Cursors cursorToSet)
		{
			Parent?.SetCursor(cursorToSet);
		}

		public virtual void OnMouseEnter(MouseEventArgs mouseEvent)
		{
			SetCursor(Cursor);
			MouseEnter?.Invoke(this, mouseEvent);
		}

		public virtual void OnMouseLeave(MouseEventArgs mouseEvent)
		{
			MouseLeave?.Invoke(this, mouseEvent);
		}

		public virtual void SendToChildren(object objectToRoute)
		{
			foreach (GuiWidget child in Children)
			{
				child.SendToChildren(objectToRoute);
			}
		}

		public class WidgetAndPosition
		{
			public Point2D position { get; private set; }
			public GuiWidget widget { get; private set; }
			public string name { get; private set; }
			public object NamedObject { get; private set; }

			public WidgetAndPosition(GuiWidget widget, Point2D position, string name, object namedObject = null)
			{
				this.name = name;
				this.widget = widget;
				this.position = position;
				if (namedObject == null)
				{
					this.NamedObject = widget;
				}
				else
				{
					this.NamedObject = namedObject;
				}
			}
		}

		public enum SearchType { Exact, Partial };

		public void FindNamedChildrenRecursive(string nameToSearchFor, List<WidgetAndPosition> foundChildren)
		{
			FindNamedChildrenRecursive(nameToSearchFor, foundChildren, new RectangleDouble(double.MinValue, double.MinValue, double.MaxValue, double.MaxValue), SearchType.Exact);
		}

		// allowInvalidItems - automation tests use this function and may need to find disabled or non-visible items to validate their state
		public virtual void FindNamedChildrenRecursive(string nameToSearchFor, List<WidgetAndPosition> foundChildren, RectangleDouble touchingBounds, SearchType seachType, bool allowDisabledOrHidden = true)
		{
			bool nameFound = false;

			if (seachType == SearchType.Exact)
			{
				if (Name == nameToSearchFor)
				{
					nameFound = true;
				}
			}
			else
			{
				if (nameToSearchFor == ""
					|| Name.Contains(nameToSearchFor))
				{
					nameFound = true;
				}
			}

			if (nameFound)
			{
				if (touchingBounds.IntersectWithRectangle(this.LocalBounds))
				{
					foundChildren.Add(new WidgetAndPosition(this, new Point2D(Width / 2, Height / 2), Name));
				}
			}

			List<GuiWidget> searchChildren = new List<GuiWidget>(Children);
			foreach (GuiWidget child in searchChildren.Where(child => allowDisabledOrHidden || (child.Visible && child.Enabled)))
			{
				RectangleDouble touchingBoundsRelChild = touchingBounds;
				touchingBoundsRelChild.Offset(-child.OriginRelativeParent);
				child.FindNamedChildrenRecursive(nameToSearchFor, foundChildren, touchingBoundsRelChild, seachType, allowDisabledOrHidden);
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

				mouseCapturedState = MouseCapturedState.NotCaptured;

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

		/// <summary>
		/// Occurs when a character. space or backspace key is pressed while the control has focus.
		/// base.OnKeyPress should always be called first during override to ensure we get the correct Handled state
		/// </summary>
		/// <param name="keyPressEvent"></param>
		public virtual void OnKeyPress(KeyPressEventArgs keyPressEvent)
		{
			GuiWidget childWithFocus = GetChildContainingFocus();
			if (childWithFocus != null && childWithFocus.Visible && childWithFocus.Enabled)
			{
				childWithFocus.OnKeyPress(keyPressEvent);
			}

			KeyPressed?.Invoke(this, keyPressEvent);
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

		/// <summary>
		/// Occurs when a character. space or backspace key is pressed while the control has focus.
		/// base.OnKeyDown should always be called first during override to ensure we get the correct Handled state
		/// </summary>
		/// <param name="keyEvent"></param>
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

			KeyDown?.Invoke(this, keyEvent);
		}

		/// <summary>
		/// Occurs when a character. space or backspace key is released while the control has focus.
		/// base.OnKeyUp should always be called first during override to ensure we get the correct Handled state
		/// </summary>
		/// <param name="keyEvent"></param>
		public virtual void OnKeyUp(KeyEventArgs keyEvent)
		{
			GuiWidget childWithFocus = GetChildContainingFocus();
			if (childWithFocus != null && childWithFocus.Visible && childWithFocus.Enabled)
			{
				childWithFocus.OnKeyUp(keyEvent);
			}

			KeyUp?.Invoke(this, keyEvent);
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
			return widget.Children.OfType<T>();
		}

		public static IEnumerable<GuiWidget> DescendantsAndSelf(this GuiWidget widget)
		{
			return DescendantsAndSelf<GuiWidget>(widget);
		}

		/// <summary>
		/// Returns all descendants and this of the current GuiWiget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>All matching child widgets</returns>
		public static IEnumerable<T> DescendantsAndSelf<T>(this GuiWidget widget) where T : GuiWidget
		{
			var items = new Stack<GuiWidget>();
			items.Push(widget);

			while (items.Any())
			{
				GuiWidget item = items.Pop();

				foreach (var child in item.Children)
				{
					items.Push(child);
				}

				if (item is T itemIsType)
				{
					yield return itemIsType;
				}
			}
		}

		public static IEnumerable<GuiWidget> Descendants(this GuiWidget widget)
		{
			return Descendants<GuiWidget>(widget);
		}

		/// <summary>
		/// Returns all descendants of the current GuiWiget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>All matching child widgets</returns>
		public static IEnumerable<T> Descendants<T>(this GuiWidget widget) where T : GuiWidget
		{
			var items = new Stack<GuiWidget>(widget.Children);

			while (items.Any())
			{
				GuiWidget item = items.Pop();

				foreach (var child in item.Children)
				{
					items.Push(child);
				}

				if (item is T itemIsType)
				{
					yield return itemIsType;
				}
			}
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