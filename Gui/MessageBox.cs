using System;

namespace MatterHackers.Agg.UI
{
	public class MessageBox : SystemWindow
	{
		public event EventHandler ClickedOk;

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
			BackgroundColor = new Color(50, 50, 50, 240);
			FlowLayoutWidget topToBottomFlow = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomFlow.HAnchor = HAnchor.Stretch;
			topToBottomFlow.VAnchor = VAnchor.Stretch;
			topToBottomFlow.AddChild(new WrappedTextWidget(message, textColor: Color.White));

			Title = windowTitle;

			// add a spacer
			topToBottomFlow.AddChild(new GuiWidget(10, 10));

			switch (messageType)
			{
				case MessageType.YES_NO:
					{
						FlowLayoutWidget yesNoButtonsFlow = new FlowLayoutWidget();

						Button yesButton = new Button("Yes");
						yesButton.Click += okButton_Click;
						yesNoButtonsFlow.AddChild(yesButton);

						Button noButton = new Button("No");
						noButton.Click += (s, e) => Close();
						yesNoButtonsFlow.AddChild(noButton);

						topToBottomFlow.AddChild(yesNoButtonsFlow);
					}
					break;

				case MessageType.OK:
					{
						Button okButton = new Button("Ok");
						okButton.Click += okButton_Click;
						topToBottomFlow.AddChild(okButton);
					}
					break;

				default:
					throw new NotImplementedException();
			}

			AddChild(topToBottomFlow);

			IsModal = true;
		}

		private void okButton_Click(object sender, EventArgs mouseEvent)
		{
			ClickedOk?.Invoke(this, null);
			Close();
		}
	}
}