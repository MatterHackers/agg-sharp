/*
Copyright (c) 2022, Lars Brubaker
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

using MatterHackers.Agg.Platform;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public class OnScreenKeyboard : FlowLayoutWidget
	{
		public class Key
        {
			public string Default { get; set; }

			public string Shifted { get; set; }

			public double Size { get; set; } = 1;

            public override string ToString() => $"{Default} : {Shifted} : {Size}";
        }

		public class Row
        {
			public List<Key> Keys { get; set; }
        }

		public class KeyboardLayout
		{
			public string Name { get; set; }
			public List<Row> Rows { get; set; }
		}

		public class Layouts
		{
			public List<KeyboardLayout> Layout { get; set; }
		}

		private TextEditWidget hadFocusWidget = null;
        private Layouts keyLayouts;

        public OnScreenKeyboard(int width, int height)
			: base(FlowDirection.TopToBottom)
		{
			Height = height;
			HAnchor = HAnchor.Center;
			Width = 17.5 * 64 * DeviceScale;

			var layoutContent = StaticData.Instance.ReadAllText("TouchKeyboard.json");
			keyLayouts = JsonConvert.DeserializeObject<Layouts>(layoutContent);

			MakeKeyButtons();
		}

		private void MakeKeyButtons()
		{
			foreach(var row in keyLayouts.Layout[0].Rows)
            {
				var rowWidget = this.AddChild(new FlowLayoutWidget()
                {
					HAnchor = HAnchor.Stretch,
					Padding = 3
                });
				var first = true;
				foreach (var key in row.Keys)
				{
					if (!first)
					{
						rowWidget.AddChild(new HorizontalSpacer());
					}
					first = false;
					var width = 64 * DeviceScale * key.Size;
					var height = 57 * DeviceScale;
					var inputButton = new Button(GetButtonImage(key))
                    {
						MinimumSize = new Vector2(width, height),
						Width = width,
						Height = height,
						Margin = 0,
						BackgroundRadius = 3 * DeviceScale,
						Border = 3 * DeviceScale,
						BorderColor = Color.Black
                    };
					inputButton.MouseEnterBounds += (s, e) =>
					{
						inputButton.BackgroundColor = Color.Blue;
					};
					inputButton.MouseLeaveBounds += (s, e) =>
					{
						inputButton.BackgroundColor = Color.Transparent;
					};
					inputButton.Click += inputButton_Click;
					rowWidget.AddChild(inputButton);
				}

				rowWidget.AddChild(new GuiWidget(4, 2));
			}
		}

        private GuiWidget GetButtonImage(Key key)
        {
			switch (key.Default)
			{
				default:
					{
						var container = new FlowLayoutWidget();
						container.HAnchor |= HAnchor.Center;
						container.VAnchor |= VAnchor.Center;
						container.AddChild(new TextWidget(key.Default));
						if (!string.IsNullOrEmpty(key.Shifted))
						{
							container.AddChild(new TextWidget(key.Shifted));
							container.Children.Last().Visible = false;
							Keyboard.StateChanged += (s, e) =>
							{
								if (Keyboard.IsKeyDown(Keys.ShiftKey))
								{
									container.Children.First().Visible = false;
									container.Children.Last().Visible = true;
								}
								else
								{
									container.Children.First().Visible = true;
									container.Children.Last().Visible = false;
								}
							};
						}
						return container;
					}
			}

			throw new NotImplementedException();
		}

        private void inputButton_Click(object sender, EventArgs e)
		{
			if (hadFocusWidget != null)
			{
				hadFocusWidget.OnKeyPress(new KeyPressEventArgs(((Button)sender).Children.FirstOrDefault().Children.FirstOrDefault().Text[0]));
			}
		}

		public void SetFocusWidget(TextEditWidget hadFocusWidget)
		{
			this.hadFocusWidget = hadFocusWidget;
		}
	}

    public class SoftKeyboardDisplayStateManager : GuiWidget
	{
		private TextEditWidget hadFocusWidget = null;
		private GuiWidget content;
		private GuiWidget contentOffsetHolder;
		private OnScreenKeyboard keyboard;

		public SoftKeyboardDisplayStateManager(SystemWindow rootSystemWindow)
		{
			AnchorAll();
			// AddChild(content);

			keyboard = new OnScreenKeyboard(800, 300);
			// keyboard.BackgroundColor = backgroundColor;
			AddChild(keyboard);
			keyboard.Visible = false;

			TextEditWidget.ShowSoftwareKeyboard += DoShowSoftwareKeyboard;
			TextEditWidget.HideSoftwareKeyboard += DoHideSoftwareKeyboard;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (keyboard.Visible)
			{
				contentOffsetHolder.OnDraw(graphics2D);
			}
			base.OnDraw(graphics2D);
		}

		private MouseEventArgs lastMouseDownEvent;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			CheckMouseCaptureStates();
			lastMouseDownEvent = mouseEvent;
			base.OnMouseDown(mouseEvent);
			CheckMouseCaptureStates();

			if (keyboard.Visible)
			{
				RectangleDouble textWidgetBounds = TextWidgetScreenBounds();
				if (textWidgetBounds.Contains(mouseEvent.Position))
				{
					contentOffsetHolder.OnMouseDown(mouseEvent);
				}

				CheckMouseCaptureStates();
			}
		}

		public void CheckMouseCaptureStates()
		{
			ValidateMouseCaptureRecursive(this);
			if (contentOffsetHolder != null)
			{
				contentOffsetHolder.ValidateMouseCaptureRecursive(contentOffsetHolder);
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			CheckMouseCaptureStates();
			base.OnMouseMove(mouseEvent);
			CheckMouseCaptureStates();

			if (keyboard != null
				&& keyboard.Visible)
			{
				RectangleDouble textWidgetBounds = TextWidgetScreenBounds();
				if (textWidgetBounds.Contains(mouseEvent.Position))
				{
					contentOffsetHolder.OnMouseMove(mouseEvent);
				}

				CheckMouseCaptureStates();
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			CheckMouseCaptureStates();
			base.OnMouseUp(mouseEvent);
			if (keyboard != null
				&& keyboard.Visible)
			{
				CheckMouseCaptureStates();

				RectangleDouble textWidgetBounds = TextWidgetScreenBounds();
				if (textWidgetBounds.Contains(mouseEvent.Position))
				{
					contentOffsetHolder.OnMouseUp(mouseEvent);
				}
				else
				{
					if (FirstWidgetUnderMouse)
					{
						UiThread.RunOnIdle(() =>
						{
							CheckMouseCaptureStates();
							DoHideSoftwareKeyboard(this, null);
							CheckMouseCaptureStates();
						});
					}
				}

				CheckMouseCaptureStates();
			}
		}

		private RectangleDouble TextWidgetScreenBounds()
		{
			RectangleDouble textWidgetBounds = hadFocusWidget.LocalBounds;
			return hadFocusWidget.TransformToScreenSpace(textWidgetBounds);
		}

		private VAnchor oldVAnchor;
		private Vector2 oldOrigin;

		private void DoShowSoftwareKeyboard(object sender, EventArgs e)
		{
			CheckMouseCaptureStates();

			hadFocusWidget = sender as TextEditWidget;
			// if we are not currently hooked up
			if (contentOffsetHolder == null
				|| contentOffsetHolder.Children.Count == 0)
			{
				content.Invalidated += content_Invalidated;
				RemoveChild(content);
				contentOffsetHolder = new GuiWidget(Width, Height);
				contentOffsetHolder.AddChild(content);

				// remember where we were
				oldVAnchor = content.VAnchor;
				oldOrigin = content.OriginRelativeParent;
				if (hadFocusWidget != null)
				{
					// test if the text widget is visible
					RectangleDouble textWidgetScreenBounds = TextWidgetScreenBounds();
					int topOfKeyboard = (int)keyboard.LocalBounds.Height;
					if (textWidgetScreenBounds.Bottom < topOfKeyboard)
					{
						// make sure the screen is not resizing vertically
						content.VAnchor = VAnchor.Absolute;
						// move the screen up so we can see the bottom of the text widget
						content.OriginRelativeParent = new Vector2(0, topOfKeyboard - textWidgetScreenBounds.Bottom + 3);
					}
				}
			}

			if (keyboard != null
				&& !keyboard.Visible)
			{
				MouseEventArgs upMouseEvent = e as MouseEventArgs;
				if (lastMouseDownEvent != null)
				{
					// The OnFocus that put us here had a mouse down event that we want to unwind if using our soft keyboard.
					CheckMouseCaptureStates();
					content.OnMouseUp(lastMouseDownEvent);
					CheckMouseCaptureStates();
				}
				keyboard.SetFocusWidget(hadFocusWidget);
				CheckMouseCaptureStates();
				keyboard.Visible = true;
				CheckMouseCaptureStates();
			}
			CheckMouseCaptureStates();

		}

		private void DoHideSoftwareKeyboard(object sender, EventArgs e)
		{
			CheckMouseCaptureStates();
			if (keyboard.Visible)
			{
				// the click that got rid of the soft keyboard should be clicked after we lower
				// this code currently messes up
				contentOffsetHolder.OnMouseDown(lastMouseDownEvent);

				CheckMouseCaptureStates();
			}

			if (contentOffsetHolder.Children.Count > 0)
			{
				content.Invalidated -= content_Invalidated;

				contentOffsetHolder.RemoveChild(content);
				AddChild(content, 0);

				if (hadFocusWidget != null)
				{
					content.VAnchor = oldVAnchor;
					content.OriginRelativeParent = oldOrigin;
				}
			}

			if (keyboard.Visible)
			{
				keyboard.Visible = false;
				CheckMouseCaptureStates();
			}

			CheckMouseCaptureStates();
		}

		private void content_Invalidated(object sender, EventArgs e)
		{
			Invalidate();
		}
	}

	public class SoftKeyboardContentOffset : GuiWidget
	{
		private TextEditWidget hadFocusWidget = null;
		private GuiWidget content;
		private GuiWidget contentOffsetHolder;

		static SoftKeyboardContentOffset ()
		{
			KeyboardHeight = 253;
			IsActive = false;
		}

		public SoftKeyboardContentOffset(GuiWidget content)
		{
			this.content = content;
			AnchorAll();
			contentOffsetHolder = new GuiWidget(Width, Height);
			contentOffsetHolder.AnchorAll();
			contentOffsetHolder.AddChild(content);
			AddChild(contentOffsetHolder);

			TextEditWidget.ShowSoftwareKeyboard += EnsureEditControlIsVisible;
			TextEditWidget.KeyboardCollapsed += MoveContentBackDown;
		}

		public static int KeyboardHeight { get; set; }

		public static bool IsActive 
		{
			get;
			private set;
		}

		private RectangleDouble TextWidgetScreenBounds()
		{
			RectangleDouble textWidgetBounds = hadFocusWidget.LocalBounds;
			return hadFocusWidget.TransformToScreenSpace(textWidgetBounds);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			if (content.OriginRelativeParent.Y != 0)
			{
				graphics2D.FillRectangle(0, 0, Width, KeyboardHeight, Color.Black);
			}
		}

		private VAnchor oldVAnchor;
		private Vector2 oldOrigin;

		private void EnsureEditControlIsVisible(object sender, EventArgs e)
		{
			hadFocusWidget = sender as TextEditWidget;
			// remember where we were
			oldVAnchor = content.VAnchor;
			oldOrigin = content.OriginRelativeParent;

			// test if the text widget is visible
			RectangleDouble textWidgetScreenBounds = TextWidgetScreenBounds();
			int topOfKeyboard = KeyboardHeight;
			if (textWidgetScreenBounds.Bottom < topOfKeyboard)
			{
				IsActive = true;

				// make sure the screen is not resizing vertically
				content.VAnchor = VAnchor.Absolute;
				// move the screen up so we can see the bottom of the text widget
				content.OriginRelativeParent = new Vector2(0, topOfKeyboard - textWidgetScreenBounds.Bottom + 10);
			}
		}

		private void MoveContentBackDown(object sender, EventArgs e)
		{
			if (hadFocusWidget != null)
			{
				content.VAnchor = oldVAnchor;
				content.OriginRelativeParent = oldOrigin;

				IsActive = false;

				// Clear focus so that future clicks into the original control will fire focus events that restore the keyboard view
				hadFocusWidget.Unfocus();
				hadFocusWidget = null;
			}
		}
	}
}