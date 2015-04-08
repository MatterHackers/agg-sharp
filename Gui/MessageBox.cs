using System;

namespace MatterHackers.Agg.UI
{
	public class MessageBox : SystemWindow
	{
		public EventHandler ClickedOk;

		public enum MessageType { OK, YES_NO };

		public static bool ShowMessageBox(String message, string caption, MessageType messageType = MessageType.OK)
		{
			MessageBox messageBox = new MessageBox(message, caption, messageType, 400, 300);
			bool okClicked = false;
			messageBox.ClickedOk += (sender, e) => { okClicked = true; };
			messageBox.ShowAsSystemWindow();
			return okClicked;
		}

		public MessageBox(String message, string windowTitle, MessageType messageType, double width, double height)
			: base(width, height)
		{
			BackgroundColor = new RGBA_Bytes(50, 50, 50, 240);
			FlowLayoutWidget topToBottomFlow = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomFlow.HAnchor = Agg.UI.HAnchor.ParentCenter;
			topToBottomFlow.VAnchor = Agg.UI.VAnchor.ParentCenter;
			topToBottomFlow.AddChild(new TextWidget(message, textColor: RGBA_Bytes.White));

			Title = windowTitle;

			// add a spacer
			topToBottomFlow.AddChild(new GuiWidget(10, 10));

			switch (messageType)
			{
				case MessageType.YES_NO:
					{
						FlowLayoutWidget yesNoButtonsFlow = new FlowLayoutWidget();

						Button yesButton = new Button("Yes");
						yesButton.Click += new EventHandler(okButton_Click);
						yesNoButtonsFlow.AddChild(yesButton);

						Button noButton = new Button("No");
						noButton.Click += new EventHandler(noButton_Click);
						yesNoButtonsFlow.AddChild(noButton);

						topToBottomFlow.AddChild(yesNoButtonsFlow);
					}
					break;

				case MessageType.OK:
					{
						Button okButton = new Button("Ok");
						okButton.Click += new EventHandler(okButton_Click);
						topToBottomFlow.AddChild(okButton);
					}
					break;

				default:
					throw new NotImplementedException();
			}

			topToBottomFlow.SetBoundsToEncloseChildren();

			AddChild(topToBottomFlow);

			IsModal = true;
		}

		private void noButton_Click(object sender, EventArgs mouseEvent)
		{
			Close();
		}

		private void okButton_Click(object sender, EventArgs mouseEvent)
		{
			if (ClickedOk != null)
			{
				ClickedOk(this, null);
			}
			Close();
		}
	}
}