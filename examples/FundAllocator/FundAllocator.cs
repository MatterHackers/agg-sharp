using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.Agg
{
    public class ExpenseData
    {
        private static double colorInc = .1;
        private static Dictionary<string, Color> foundColors = new Dictionary<string, Color>();
        private static int nextColor = 0;
        public static double Scale { get; set; } = 1;

        private double _amount;
        public double Amount 
        {
            get
            {
                var good = _amount;
                if (good > 100 / 12.0)
                {
                    good = 100 / 12.0;
                }

                return good;
            }
            
            set => _amount = value;
        }
        public Vector2 Center { get; set; }
        public string Description { get; set; }
        public string Group { get; set; }

        public Color GetColor()
        {
            if (!foundColors.ContainsKey(Group))
            {
                foundColors[Group] = ColorF.FromHSL(nextColor++ * colorInc, .8, .8).ToColor();
            }

            return foundColors[Group];
        }

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

        private string data = @"David Gaylord,12.82,Product Group
Mara Hitner,12.14,Sales
Nick Lefors,11.63,Development
Lars Brubaker,11.94,Executive
Kevin Pope,11.40,Executive
Taylor Landry,10.33,Product Group
Michael Hulse,10.76,Finance
Justin Kirkman,9.91,Development
Logan Kay,9.38,Finance
Chris Costello,8.50,Sales
Maisie Clement,7.58,Customer Success
Rhonda Grandy,7.59,Marketing
Anton Sather,7.29,Finance
Dylan Wolfe,6.85,Marketing
Max Boscher,6.67,Vendor Ops
Federico Ayala,6.74,Fulfillment
Kyle Mingus,6.08,Sales
Joel Gordon,6.36,Sales
Patrick McBride,5.87,Sales
Christopher Morgan,6.09,Sales
Diane Abell,6.42,Finance
Michael Petitclerc,5.71,Customer Success
Michael Ponc,5.97,Production
Nathan Linton,5.71,Fulfillment
Chelsea Bosworth,5.46,Customer Success
Matt Hendricks,5.46,Product Group
Vanessa Yi,5.26,Vendor Ops
Samuel Alsto,5.21,Vendor Ops
Tina Chau,5.15,Product Group
Nikki Sandoval,4.88,Sales
Alec Richter,5.04,Marketing
Cassie Tomlinson,4.56,Customer Success
Sabha Mizyed,4.14,Inventory Recovery
Andrew Rossol,4.35,Customer Success
Fernando Silva,3.96,Product Group
Stephen Reiter,4.14,Customer Success
Justin Leos,3.90,Sales
Tanya Crooks,3.68,Sales
Adrian Andrade,3.84,Fulfillment
Brina Carrier,3.83,Fulfillment
Clara Grace,3.57,Sales
Mike Bermudez,3.83,Production
Megan Clisby,3.80,Customer Success
Philip Hogberg,3.79,Sales
Amber Scott,3.84,Customer Success
Bradley Hanstad,3.79,Customer Success
Alex Chavez,3.75,Sales
Olivia Wilson,3.58,Customer Success
Jennifer Lantrip,3.80,Customer Success
Natalie Reba,3.62,Fulfillment
Paul Nguyen,3.54,Production
Timothy Eaves,3.43,Fulfillment
Danny Garcia,3.25,Fulfillment
Amanda Rivera,3.25,Fulfillment
Nathan Blanchard,3.34,Production
Ivan Salazar,3.34,Production
Paula Woods,2.88,Fulfillment
Leah Orndorff,2.87,Fulfillment
Brianna Oliver,1.35,Fulfillment
Carleigh Baker,1.35,Fulfillment
Miguel Ayala,0.83,Fulfillment";

        private Vector2 downPosition = new Vector2();
        private int dragingRegionIndex = -1;
        private Vector2 regionStartPosition = new Vector2();

        public FundAllocator()
        {
            ExpenseData.Scale = 60;
            var bottomBar = new FlowLayoutWidget()
            {
                HAnchor = HAnchor.Stretch,
                VAnchor = VAnchor.Bottom | VAnchor.Fit,
            };

            this.AddChild(bottomBar);

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
            bottomBar.AddChild(loadButton);

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
            bottomBar.AddChild(saveButton);

            var resetButton = new Button("Reset");
            resetButton.Click += (s, e) =>
            {
                ArangeExpenses();
            };
            bottomBar.AddChild(resetButton);

            var swapButton = new Button("Swap");
            swapButton.Click += (s, e) =>
            {
                foreach (var expense in Expenses)
                {
                    if (expense.Center.X > Width / 2)
                    {
                        expense.Center = new Vector2(expense.Center.X - Width / 2, expense.Center.Y);
                    }
                    else
                    {
                        expense.Center = new Vector2(expense.Center.X + Width / 2, expense.Center.Y);
                    }
                }

                Invalidate();
            };

            bottomBar.AddChild(swapButton);

            var randomButton = new Button("Random");
            randomButton.Click += (s, e) =>
            {
                ArangeExpenses();

                // now move expenses to the right untile we reach the right side limit
                var rand = new Random();
                var tries = 0;
                while (RightAmount < TotalSpendLimit && tries < 1000)
                {
                    var index = rand.Next(0, Expenses.Count);
                    var expense = Expenses[index];
                    if (expense.Center.X < Width / 2)
                    {
                        if (RightAmount + expense.Amount < TotalSpendLimit)
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
            bottomBar.AddChild(randomButton);

            bottomBar.AddChild(new TextWidget("Spend Limit: ")
            {
                VAnchor = VAnchor.Center
            });
            var spendLimitField = new NumberEdit(TotalSpendLimit)
            {
                VAnchor = VAnchor.Center,
                BackgroundOutlineWidth = 1,
            };
            spendLimitField.EditComplete += (s, e) =>
            {
                TotalSpendLimit = spendLimitField.Value;
                ArangeExpenses();
            };
            
            bottomBar.AddChild(spendLimitField);

            AnchorAll();
        }

        public string DemoCategory { get; } = "FundAllocator";
        public string DemoDescription { get; } = "An easy way to allocate your funds";
        public double LeftAmount => GetLeftRight().left;
        public double RightAmount => GetLeftRight().right;
        public string Title { get; } = "FundAllocator";
        private double TotalSpendLimit { get; set; } = 220;

        [STAThread]
        public static void Main(string[] args)
        {
            Clipboard.SetSystemClipboard(new WindowsFormsClipboard());

            // get all the file in the download folder
            //foreach (var file in Directory.EnumerateFiles(@"C:\Users\LarsBrubaker\Downloads\alpha"))
            //{
            //    var image = ImageIO.LoadImage(file);
            //    for (var y = 0; y < image.Height; y++)
            //    {
            //        for (var x = 0; x < image.Width; x++)
            //        {
            //            var color = image.GetPixel(x, y);
            //            if (color.Alpha0To255 < 20
            //                && color.Red0To255 < 20
            //                && color.Green0To255 < 20
            //                && color.Blue0To255 < 20)
            //            {
            //                image.SetPixel(x, y, Color.White.WithAlpha(0));
            //            }
            //        }
            //    }

            //    var fileName = Path.GetFileNameWithoutExtension(file);
            //    var path = Path.GetDirectoryName(file);
            //    ImageIO.SaveImageData(Path.Combine(path, "A_" + fileName + ".PNG"), image);
            //}

            var demoWidget = new FundAllocator();

            var systemWindow = new SystemWindow(1600, 900);
            AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.Agg.UI.OpenGLWinformsWindowProvider, agg_platform_win32";
            systemWindow.Title = demoWidget.Title;
            systemWindow.AddChild(demoWidget);
            systemWindow.ShowAsSystemWindow();
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

                graphics.FillRectangle(region, expense.GetColor());
                graphics.DrawString(expense.Description, expense.Center + new Vector2(0, 3), 14, Font.Justification.Center, drawFromHintedCach: true);
            }

            var (left, right) = GetLeftRight();
            // draw the left and right amounts
            graphics.DrawString(left.ToString("C1"), new Vector2(Width / 4, Height * .95), 24, drawFromHintedCach: true);
            graphics.DrawString(right.ToString("C1"), new Vector2(Width / 4 * 3, Height * .95), 24, drawFromHintedCach: true);

            // let the buttons draw
            base.OnDraw(graphics2D);
        }

        public override void OnLoad(EventArgs args)
        {
            var rows = data.Split('\n').ToList();
            rows.Sort();
            foreach (var row in rows)
            {
                var columns = row.Split(',');
                if (columns.Length == 3)
                {
                    var name = columns[0];
                    var amount = double.Parse(columns[1]);
                    var group = columns[2];
                    var firstLast = name.Trim().Split(' ');
                    if (firstLast.Length == 2)
                    {
                        Expenses.Add(new ExpenseData()
                        {
                            Amount = amount,
                            Description = firstLast[0] + "\n" + firstLast[1],
                            Group = group,
                        });
                    }
                    else
                    {
                        Expenses.Add(new ExpenseData()
                        {
                            Amount = amount,
                            Description = name,
                            Group = group,
                        });
                    }
                }
            }

            ArangeExpenses();

            base.OnLoad(args);
        }

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

                if (expense.Center.X > Width / 2 && GetLeftRight().right > TotalSpendLimit)
                {
                    expense.Center = new Vector2(Width / 2 - 1, expense.Center.Y);
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
    }
}