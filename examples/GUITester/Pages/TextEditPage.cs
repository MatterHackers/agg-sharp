using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class TextEditPage : TabPage
	{
		public TextEditPage()
			: base("Text Edit Widget")
		{
			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			BackgroundColor = new RGBA_Bytes(210, 210, 255);
			topToBottom.Padding = new BorderDouble(20);

			topToBottom.AddChild(new TextWidget("testing underline jpqy", underline: true));
			topToBottom.AddChild(new TextWidget("testing1\ntest2\ntest3"));

			topToBottom.AddChild(new TextWidget("this is some multiline\ntext\nwith centering", justification: Justification.Center));

			int tabIndex = 0;
#if true
			InternalTextEditWidget internalMultiLine = new InternalTextEditWidget("line1\nline2\nline3", 12, true, tabIndex++);
			//InternalTextEditWidget internalMultiLine = new InternalTextEditWidget("Line 1 - Multi Line Text Control\nLine 2 - Multi Line Text Control\nLine 3 - Multi Line Text Control\n", 12, true);
			topToBottom.AddChild(internalMultiLine);
#endif
			// show some masking for passwords
			{
				FlowLayoutWidget leftToRight = new FlowLayoutWidget();
				leftToRight.Margin = new BorderDouble(3);
				TextEditWidget passwordeTextEdit = new TextEditWidget("Password", tabIndex: tabIndex++);
				//passwordeTextEdit.InternalTextEditWidget.MaskCharacter = '*';
				passwordeTextEdit.Margin = new BorderDouble(4, 0);
				leftToRight.AddChild(passwordeTextEdit);

				TextWidget description = new TextWidget("Content:");
				leftToRight.AddChild(description);

				TextWidget passwordContent = new TextWidget("Password");
				leftToRight.AddChild(passwordContent);

				passwordeTextEdit.TextChanged += (sender, e) =>
				{
					passwordContent.Text = passwordeTextEdit.Text;
				};

				topToBottom.AddChild(leftToRight);
			}

			TextEditWidget singleLineTextEdit = new TextEditWidget("Single Line Edit Text Control", tabIndex: tabIndex++);
			topToBottom.AddChild(singleLineTextEdit);

			TextEditWidget multiLineTextConrol = new TextEditWidget("Line 1 - Multi Line Text Control\nLine 2 - Multi Line Text Control\nLine 3 - Multi Line Text Control\n", tabIndex: tabIndex++);
			multiLineTextConrol.Multiline = true;
			topToBottom.AddChild(multiLineTextConrol);

			TextEditWidget longTextWidget = new TextEditWidget("This is some really long text.", pixelWidth: 100, tabIndex: tabIndex++);
			topToBottom.AddChild(longTextWidget);

			topToBottom.AddChild(new TextWidget("Integer Text Control:"));
			topToBottom.AddChild(new NumberEdit(512102416, tabIndex: tabIndex++));

			topToBottom.AddChild(new TextWidget("Floating Point Text Control:"));
			topToBottom.AddChild(new NumberEdit(512102416, allowNegatives: true, allowDecimals: true, tabIndex: tabIndex++));

			TextWidget paddingAdjustText = new TextWidget("Padding: 0");
			paddingAdjustText.AutoExpandBoundsToText = true;
			topToBottom.AddChild(paddingAdjustText);

			TextEditWidget paddingAdjustTextEdit = new TextEditWidget("Edit With Padding", tabIndex: tabIndex++);
			GuiWidget paddingAroundTextEdit = new GuiWidget(100, 16);
			topToBottom.AddChild(paddingAroundTextEdit);
			paddingAroundTextEdit.AddChild(paddingAdjustTextEdit);
			paddingAdjustText.SetBoundsToEncloseChildren();

			//AddChild(new TextEditWidget("Multiline Edit Text Widget line 1\nline 2\nline 3", 200, 400, 200, 80, multiLine: true));
			AddChild(topToBottom);

			foreach (GuiWidget child in topToBottom.Children)
			{
				//child.Padding = new BorderDouble(4);
				child.HAnchor = UI.HAnchor.Center;
				child.BackgroundColor = RGBA_Bytes.White;
				//child.Margin = new BorderDouble(3);
				if (child is TextWidget)
				{
					child.BackgroundColor = new RGBA_Bytes(255, 200, 200);
				}
			}

			Slider textPaddingSlider = new Slider(new Vector2(), 200, 0, 10);
			topToBottom.AddChild(textPaddingSlider);
			textPaddingSlider.ValueChanged += (sender, e) =>
			{
				double padding = ((Slider)sender).Value;
				paddingAdjustText.Padding = new BorderDouble(padding);

				paddingAroundTextEdit.Padding = new BorderDouble(padding);
				paddingAroundTextEdit.SetBoundsToEncloseChildren();
				((Slider)sender).Parent.SetBoundsToEncloseChildren();
			};

			topToBottom.HAnchor = UI.HAnchor.Center;
			topToBottom.VAnchor = UI.VAnchor.Center;
		}
	}
}