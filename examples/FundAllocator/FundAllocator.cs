using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.Agg
{
	public class ExpenseData
	{
		public static double Scale { get; set; } = 1;
            
		public string Description { get; set; }
		public double Amount { get; set; }
		public Vector2 Center { get; set; }

        public RectangleDouble GetRegion()
        {
            var scaled = Scale * Amount;
            var square = Math.Sqrt(scaled);
            return new RectangleDouble(Center.X - square * 2, Center.Y - square, Center.X + square * 2, Center.Y + square);
        }
    }

    public class FundAllocator : GuiWidget, IDemoApp
	{
		public static List<ExpenseData> Expenses = new List<ExpenseData>();
        
        string data = @"Cars,12.82
Trucks,12.14
Planes,11.63 
Boats,11.94";

		double rightSideLimit { get; set; } = 220;

		public FundAllocator()
		{
            ExpenseData.Scale = 60;
			var buttonBar = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Bottom | VAnchor.Fit,
			};

			this.AddChild(buttonBar);

			var loadButton = new Button("Load");
			loadButton.Click += (s, e) =>
			{
				Stream myStream = null;
				System.Windows.Forms.OpenFileDialog theDialog = new System.Windows.Forms.OpenFileDialog();
				theDialog.Filter = "Json files|*.json";
				if (theDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					try
					{
						if ((myStream = theDialog.OpenFile()) != null)
						{
							using (myStream)
							{
                                // load into Expenses
                                Expenses = JsonConvert.DeserializeObject<List<ExpenseData>>(File.ReadAllText(theDialog.FileName));
                            }
                        }
					}
					catch (Exception ex)
					{
					}

					Invalidate();
				}
			};
			buttonBar.AddChild(loadButton);

			var saveButton = new Button("Save");
			saveButton.Click += (s, e) =>
			{
				var json = JsonConvert.SerializeObject(Expenses);
				// open a file dialog to save the file
				System.Windows.Forms.SaveFileDialog savefile = new System.Windows.Forms.SaveFileDialog();
				// set a default file name
				savefile.FileName = "layout.json";
				// set filters - this can be done in properties as well
				savefile.Filter = "Json files|*.json";

				if (savefile.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					using (StreamWriter sw = new StreamWriter(savefile.FileName))
					{
						sw.Write(json);
					}
				}
			};
			buttonBar.AddChild(saveButton);

			var randomButton = new Button("Random");
			randomButton.Click += (s, e) =>
			{
				ArangeExpenses();

				// now move expenses to the right untile we reach the right side limit
				var rand = new Random();
				var tries = 0;
                while (RightAmount < rightSideLimit && tries < 1000)
                {
                    var index = rand.Next(0, Expenses.Count);
                    var expense = Expenses[index];
					if (expense.Center.X < Width / 2)
					{
						if (RightAmount + expense.Amount < rightSideLimit)
						{
							expense.Center = new Vector2(expense.Center.X + Width / 2, expense.Center.Y);
						}
						else
						{
							tries++;
						}
					}

                    if (LeftAmount == 0)
                    {
						break;
                    }
                }                
            };
			buttonBar.AddChild(randomButton);

			AnchorAll();
		}

        public void ArangeExpenses()
        {
			var yDelta = 65;
			var offset = new Vector2(130, Height - yDelta);
			foreach (var expense in Expenses)
            {
                expense.Center = offset;
                
                offset.Y -= yDelta;
				if (offset.Y < 45)
				{
					offset.Y = Height - yDelta;
					offset.X += 150;
				}
			}

			Invalidate();
		}

		public override void OnLoad(EventArgs args)
        {
			var rows = data.Split('\n').ToList();
			rows.Sort();
			foreach (var row in rows)
			{
				var columns = row.Split(',');
				if (columns.Length == 2)
				{
					var name = columns[0];
					var amount = double.Parse(columns[1]);
					var firstLast = name.Trim().Split(' ');
					if (firstLast.Length == 2)
					{
						Expenses.Add(new ExpenseData()
						{
							Amount = amount,
							Description = firstLast[0] + "\n" + firstLast[1],
						});
					}
                    else
                    {
                        Expenses.Add(new ExpenseData()
                        {
                            Amount = amount,
                            Description = name,
                        });
                    }
                }
			}

			ArangeExpenses();

			base.OnLoad(args);
        }

        public (double left, double right) GetLeftRight()
        {
            var left = 0.0;
            var right = 0.0;
            foreach (var expense in Expenses)
            {
				if (expense.Center.X < Width / 2)
				{
					left += expense.Amount;
				}
				else
				{
					right += expense.Amount;
				}
			}

			return (left, right);
        }

        public double RightAmount => GetLeftRight().right;

        public double LeftAmount => GetLeftRight().left;

        public override void OnDraw(Graphics2D graphics2D)
		{
			var graphics = this.NewGraphics2D();
			graphics.Clear(new Color(255, 255, 255));

            graphics.DrawLine(Color.Black, new Vector2(Width / 2, 0), new Vector2(Width / 2, Height));

            // draw all the rectangles
            for (var i = 0; i < Expenses.Count; i++)
			{
				var expense = Expenses[i];
				var region = expense.GetRegion();

				graphics.FillRectangle(region, Color.Red);
				graphics.DrawString(expense.Description, expense.Center + new Vector2(0, 3), 14, Font.Justification.Center, drawFromHintedCach: true);
			}

			var (left, right) = GetLeftRight();
			// draw the left and right amounts
			graphics.DrawString(left.ToString("C1"), new Vector2(Width/4, Height*.95), 24, drawFromHintedCach: true);
            graphics.DrawString(right.ToString("C1"), new Vector2(Width/4*3, Height*.95), 24, drawFromHintedCach: true);

            // let the buttons draw
            base.OnDraw(graphics2D);
		}

		int dragingRegionIndex = -1;
		Vector2 downPosition = new Vector2();
		Vector2 regionStartPosition = new Vector2();

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			// if we did not click on anything else
			if (!ChildHasMouseCaptured)
			{
				var foundIndex = -1;
				for (var i = 0; i < Expenses.Count; i++)
				{
					var expense = Expenses[i];
					var region = expense.GetRegion();

					// it is our click check the regions of the expenses
					if (region.Contains(mouseEvent.Position))
					{
						foundIndex = i;
						downPosition = mouseEvent.Position;
						regionStartPosition = region.Center;
						break;
					}
				}

				dragingRegionIndex = foundIndex;
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);

			if (dragingRegionIndex != -1)
			{
				// drag the region around
				var expense = Expenses[dragingRegionIndex];
				expense.Center = regionStartPosition + (mouseEvent.Position - downPosition);
                
				if (GetLeftRight().right > rightSideLimit)
                {
                    expense.Center = new Vector2(Width / 2, expense.Center.Y);
                }
                
                Invalidate();
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			base.OnMouseUp(mouseEvent);

			if (dragingRegionIndex != -1)
			{
				// drag the region around
				dragingRegionIndex = -1;
			}
		}

		public string Title { get; } = "FundAllocator";

		public string DemoCategory { get; } = "FundAllocator";

        public string DemoDescription { get; } = "An easy way to allocate your funds";

		[STAThread]
		public static void Main(string[] args)
		{
			Clipboard.SetSystemClipboard(new WindowsFormsClipboard());

			var demoWidget = new FundAllocator();

			var systemWindow = new SystemWindow(1600, 900);
			AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.Agg.UI.OpenGLWinformsWindowProvider, agg_platform_win32";
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}