using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System;
using System.Globalization;

namespace MomsSolitaire
{
	public class MomsSolitaire : GuiWidget
	{
		private static double CARD_WIDTH = 50;
		private static double CARD_HEIGHT = 72;
		private static double m_BoardX = 20;
		private static double m_BoardY = 55;
		public CMomsGame MomsGame = new CMomsGame();
		private Button m_ShuffleButton;
		private Button m_UndoButton;
		private Button m_NewGameButton;
		private IVertexSource m_HeartShape;
		private IVertexSource m_DiamondShape;
		private IVertexSource m_SpadeShape;
		private IVertexSource m_ClubShape;

		public MomsSolitaire()
		{
			m_ShuffleButton = new Button("Shuffle", 20, 10);
			m_ShuffleButton.Click += new EventHandler(DoShuffle);
			AddChild(m_ShuffleButton);

			m_UndoButton = new Button("Undo", 120, 10);
			m_UndoButton.Click += new EventHandler(DoUndo);
			AddChild(m_UndoButton);

			m_NewGameButton = new Button("New Game", 530, 350);
			m_NewGameButton.Click += new EventHandler(DoNewGame);
			AddChild(m_NewGameButton);

			String inputString = "M -4,0 L 0,6 L 4,0 L 0,-6 z";
			m_DiamondShape = CreatePath(inputString, 0, 0);
			inputString = "M -0.0036575739,1047.6594 L -0.003242788,1047.6598 C -0.57805752,1047.6682 -1.0418252,1047.8121 -1.5280905,1048.3164 C -2.2710057,1049.0869 -2.3624142,1050.7811 -0.8554188,1051.6756 C -1.9609897,1051.1324 -3.4301937,1051.5612 -3.8418147,1052.8315 C -4.230835,1054.0319 -3.3342833,1055.446 -2.030705,1055.5037 C -0.46271141,1055.5731 -0.25927323,1054.5307 -0.25927323,1054.5307 C -0.30401639,1056.0846 -0.30268325,1056.7329 -1.3991304,1056.9219 C -1.454804,1056.9314 -1.4622286,1056.9868 -1.4506204,1057.0616 L 1.4587769,1057.0616 C 1.4703856,1056.9868 1.4629607,1056.9314 1.4072872,1056.9219 C 0.31084199,1056.7329 0.3121745,1056.0846 0.26743186,1054.5307 C 0.26743186,1054.5307 0.47087012,1055.5731 2.0388616,1055.5037 C 3.3424404,1055.446 4.2389918,1054.0319 3.8499716,1052.8315 C 3.4383507,1051.5612 1.9691468,1051.1324 0.86357723,1051.6756 C 2.3705711,1050.7811 2.2791628,1049.0869 1.5362473,1048.3164 C 1.0499825,1047.8121 0.58621566,1047.6682 0.011400931,1047.6598 C 0.006741066,1047.6587 0.0011981822,1047.6595 -0.0036575739,1047.6594 z";
			m_ClubShape = CreatePath(inputString, 0, -1052);
			inputString = "M -1.8088716,1048.1286 C -1.8891616,1048.1298 -1.9720416,1048.1368 -2.0574115,1048.1501 C -3.6481016,1048.399 -4.7262116,1050.4536 -3.3973316,1052.136 C -1.9660316,1053.9481 -1.2941415,1054.4327 -0.01629155,1056.5983 L 0.01845845,1056.5983 C 1.2963084,1054.4327 1.9681985,1053.9481 3.3994984,1052.136 C 4.7283785,1050.4536 3.6502684,1048.399 2.0595785,1048.1501 C 0.84266845,1047.9598 0.13995845,1049.0762 0.00085845,1049.3225 C -0.12867155,1049.0932 -0.74468155,1048.1124 -1.8088716,1048.1286 z";
			m_HeartShape = CreatePath(inputString, 0, -1052);
			inputString = "M -0.014134084,1046.8659 C -0.11617408,1047.0729 -0.51444404,1048.0169 -1.3283268,1049.1402 C -2.2279798,1050.3819 -2.8141348,1051.01 -3.5427888,1052.198 C -4.0205138,1052.9768 -4.5747888,1055.4361 -2.5444308,1055.9721 C -0.68185405,1056.4637 -0.31250405,1055.0924 -0.31250405,1055.0924 C -0.36461405,1056.9023 -0.36315405,1057.4807 -1.6400568,1057.7007 C -1.7048928,1057.7118 -1.7136608,1057.7763 -1.7001428,1057.8635 L 1.6996155,1057.8635 C 1.7131255,1057.7763 1.7043655,1057.7118 1.6395255,1057.7007 C 0.36262595,1057.4807 0.36408595,1056.9023 0.31197595,1055.0924 C 0.31197595,1055.0924 0.68132594,1056.4637 2.5438955,1055.9721 C 4.5742555,1055.4361 4.0199855,1052.9768 3.5422555,1052.198 C 2.8136055,1051.01 2.2274455,1050.3819 1.3277955,1049.1402 C 0.51391594,1048.0169 0.11564595,1047.0729 0.013605915,1046.8659 L -0.014134084,1046.8659 z";
			m_SpadeShape = CreatePath(inputString, 0, -1052);
		}

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();
			base.OnParentChanged(e);
		}

		private void GetSuitOffset(ref double OffsetX, ref double OffsetY, int CurDot, int TotalDots)
		{
			switch (TotalDots)
			{
				case 2:
					OffsetX = CARD_WIDTH / 2;
					OffsetY = CARD_HEIGHT / 4 + 2 * (CARD_HEIGHT / 4 * CurDot);
					break;

				case 3:
					OffsetX = CARD_WIDTH / 2;
					OffsetY = CARD_HEIGHT / 4 + CARD_HEIGHT / 4 * CurDot;
					break;

				case 4:
					OffsetX = CARD_WIDTH / 3 + CARD_WIDTH / 3 * (CurDot % 2);
					OffsetY = CARD_HEIGHT / 4 + 2 * (CARD_HEIGHT / 4 * (CurDot / 2));
					break;

				case 5:
					if (CurDot < 4)
					{
						OffsetX = CARD_WIDTH / 3 + CARD_WIDTH / 3 * (CurDot % 2);
						OffsetY = CARD_HEIGHT / 4 + 2 * (CARD_HEIGHT / 4 * (CurDot / 2));
					}
					else
					{
						OffsetX = CARD_WIDTH / 2;
						OffsetY = CARD_HEIGHT / 2;
					}
					break;

				case 6:
					OffsetX = CARD_WIDTH / 3 + CARD_WIDTH / 3 * (CurDot % 2);
					OffsetY = CARD_HEIGHT / 4 + CARD_HEIGHT / 4 * (CurDot / 2);
					break;

				case 7:
					if (CurDot < 6)
					{
						OffsetX = CARD_WIDTH / 3 + CARD_WIDTH / 3 * (CurDot % 2);
						OffsetY = CARD_HEIGHT / 4 + CARD_HEIGHT / 4 * (CurDot / 2);
					}
					else
					{
						OffsetX = CARD_WIDTH / 2;
						OffsetY = 5 * CARD_HEIGHT / 8;
					}
					break;

				case 8:
					if (CurDot < 6)
					{
						OffsetX = CARD_WIDTH / 3 + CARD_WIDTH / 3 * (CurDot % 2);
						OffsetY = CARD_HEIGHT / 4 + CARD_HEIGHT / 4 * (CurDot / 2);
					}
					else
					{
						OffsetX = CARD_WIDTH / 2;
						if (CurDot == 6)
							OffsetY = 3 * CARD_HEIGHT / 8;
						else
							OffsetY = 5 * CARD_HEIGHT / 8;
					}
					break;

				case 9:
					if (CurDot < 8)
					{
						OffsetX = CARD_WIDTH / 3 + CARD_WIDTH / 3 * (CurDot % 2);
						OffsetY = CARD_HEIGHT / 4 + CARD_HEIGHT / 6 * (CurDot / 2);
					}
					else
					{
						OffsetX = CARD_WIDTH / 2;
						OffsetY = CARD_HEIGHT / 2;
					}
					break;

				case 10:
					if (CurDot == 1)
					{
						OffsetX = CARD_WIDTH / 6 + CARD_WIDTH / 6 * 4;
						OffsetY = CARD_HEIGHT - (CARD_HEIGHT / 4 + CARD_HEIGHT / 12 * 3);
					}
					else if (CurDot == 6)
					{
						OffsetX = CARD_WIDTH / 6;
						OffsetY = CARD_HEIGHT - (CARD_HEIGHT / 4 + CARD_HEIGHT / 12 * 3);
					}
					else if (CurDot < 8)
					{
						OffsetX = CARD_WIDTH / 3 + CARD_WIDTH / 3 * (CurDot % 2);
						OffsetY = CARD_HEIGHT / 4 + CARD_HEIGHT / 6 * (CurDot / 2);
					}
					else
					{
						OffsetX = CARD_WIDTH / 2;
						if (CurDot == 9)
							OffsetY = CARD_HEIGHT / 4 + CARD_HEIGHT / 12;
						else
							OffsetY = CARD_HEIGHT - (CARD_HEIGHT / 4 + CARD_HEIGHT / 12);
					}
					break;

				default:
					throw new Exception("11 - 13 are draw custom");
			}
		}

		public static IVertexSource CreatePath(String DFromSVGFile, double xOffset, double yOffset)
		{
			PathStorage path = new PathStorage();
			string[] splitOnSpace = DFromSVGFile.Split(' ');
			string[] splitOnComma;
			double xc1, yc1, xc2, yc2, x, y;
			for (int i = 0; i < splitOnSpace.Length; i++)
			{
				switch (splitOnSpace[i++])
				{
					case "M":
						{
							splitOnComma = splitOnSpace[i].Split(',');
							double.TryParse(splitOnComma[0], NumberStyles.Number, null, out x);
							double.TryParse(splitOnComma[1], NumberStyles.Number, null, out y);
							path.MoveTo(x, y + yOffset);
						}
						break;

					case "L":
						{
							splitOnComma = splitOnSpace[i].Split(',');
							double.TryParse(splitOnComma[0], NumberStyles.Number, null, out x);
							double.TryParse(splitOnComma[1], NumberStyles.Number, null, out y);
							path.LineTo(x, y + yOffset);
						}
						break;

					case "C":
						{
							splitOnComma = splitOnSpace[i++].Split(',');
							double.TryParse(splitOnComma[0], NumberStyles.Number, null, out xc1);
							double.TryParse(splitOnComma[1], NumberStyles.Number, null, out yc1);

							splitOnComma = splitOnSpace[i++].Split(',');
							double.TryParse(splitOnComma[0], NumberStyles.Number, null, out xc2);
							double.TryParse(splitOnComma[1], NumberStyles.Number, null, out yc2);

							splitOnComma = splitOnSpace[i].Split(',');
							double.TryParse(splitOnComma[0], NumberStyles.Number, null, out x);
							double.TryParse(splitOnComma[1], NumberStyles.Number, null, out y);
							path.curve4(xc1, yc1 + yOffset, xc2, yc2 + yOffset, x, y + yOffset);
						}
						break;

					case "z":
						if (i < splitOnSpace.Length)
						{
							throw new Exception();
						}
						break;

					default:
						throw new NotImplementedException();
				}
			}

			path.arrange_orientations_all_paths(MatterHackers.Agg.ShapePath.FlagsAndCommand.FlagCW);
			VertexSourceApplyTransform flipped = new VertexSourceApplyTransform(path, Affine.NewScaling(1, -1));
			return flipped;
		}

		public void DrawCard(Graphics2D DestGraphics, int CardX, int CardY)
		{
			double StartX = CardX * CARD_WIDTH + m_BoardX;
			double StartY = CardY * CARD_HEIGHT + m_BoardY;

			RectangleDouble CardBounds = new RectangleDouble(StartX + 1.5, StartY + 1.5, StartX + CARD_WIDTH - 1.5, StartY + CARD_HEIGHT - 1.5);
			RoundedRect CardFiledRoundedRect = new RoundedRect(CardBounds.Left, CardBounds.Bottom, CardBounds.Right, CardBounds.Top, 5);
			Stroke CardRectBounds = new Stroke(CardFiledRoundedRect);
			CardRectBounds.width(1);

			CCard Card = MomsGame.GetCard(CardX, CardY);
			int CardValue = Card.GetValue();
			int CardSuit = Card.GetSuit();
			String ValueString = "Uninitialized ";
			if (CardValue > (int)CCard.CARD_VALUE.VALUE_ACE)
			{
				DestGraphics.SetTransform(Affine.NewIdentity());
				DestGraphics.Render(CardRectBounds, new RGBA_Bytes(0, 0, 0));
				if (CardValue > 10)
				{
					switch (CardValue)
					{
						case 11:
							ValueString = "J";
							break;

						case 12:
							ValueString = "Q";
							break;

						case 13:
							ValueString = "K";
							break;

						default:
							throw new Exception();
					}
				}
				else
				{
					ValueString = CardValue.ToString();
				}

				TextWidget stringToDraw = new TextWidget(ValueString, 10);
				RectangleDouble textBounds = stringToDraw.Printer.LocalBounds;
				DestGraphics.SetTransform(Affine.NewTranslation(CardBounds.Left + 2, CardBounds.Top - 8 - textBounds.Height / 2));
				DestGraphics.Render(stringToDraw.Printer, new RGBA_Bytes(0, 0, 0));
				DestGraphics.SetTransform(Affine.NewTranslation(CardBounds.Right - 4 - textBounds.Width, CardBounds.Bottom + 9 - textBounds.Height / 2));
				DestGraphics.Render(stringToDraw.Printer, new RGBA_Bytes(0, 0, 0));

				RGBA_Bytes SuitColor = new RGBA_Bytes(0, 0, 0);
				IVertexSource suitPath = new PathStorage();

				switch (CardSuit)
				{
					case (int)CCard.CARD_SUIT.SUIT_DIAMOND:
						{
							SuitColor = new RGBA_Bytes(0xFF, 0x11, 0x11);
							suitPath = m_DiamondShape;
						}
						break;

					case (int)CCard.CARD_SUIT.SUIT_CLUB:
						{
							SuitColor = new RGBA_Bytes(0x22, 0x22, 0x66);
							suitPath = new FlattenCurves(m_ClubShape);
						}
						break;

					case (int)CCard.CARD_SUIT.SUIT_HEART:
						{
							SuitColor = new RGBA_Bytes(0xBB, 0x00, 0x00);
							suitPath = new FlattenCurves(m_HeartShape);
						}
						break;

					case (int)CCard.CARD_SUIT.SUIT_SPADE:
						{
							SuitColor = new RGBA_Bytes(0x00, 0x00, 0x00);
							suitPath = new FlattenCurves(m_SpadeShape);
						}
						break;

					default:
						break;
				}

				textBounds = stringToDraw.Printer.LocalBounds;

				if (CardValue < 11)
				{
					for (int CurDot = 0; CurDot < CardValue; CurDot++)
					{
						double OffsetX = 0, OffsetY = 0;
						GetSuitOffset(ref OffsetX, ref OffsetY, CurDot, (int)CardValue);
						DestGraphics.SetTransform(Affine.NewTranslation(CardBounds.Left + OffsetX, CardBounds.Bottom + OffsetY));
						DestGraphics.Render(suitPath, SuitColor);
					}
				}
				else
				{
					DestGraphics.SetTransform(Affine.NewTranslation(CardBounds.Left + 9, CardBounds.Bottom + 17));
					DestGraphics.Render(suitPath, SuitColor);
					DestGraphics.SetTransform(Affine.NewTranslation(CardBounds.Right - 9, CardBounds.Top - 17));
					DestGraphics.Render(suitPath, SuitColor);

					stringToDraw = new TextWidget(ValueString, 22);
					textBounds = stringToDraw.Printer.LocalBounds;
					DestGraphics.SetTransform(Affine.NewTranslation(-1 + CardBounds.Left + CardBounds.Width / 2 - textBounds.Width / 2, CardBounds.Bottom + CardBounds.Height / 2 - textBounds.Height / 2));
					DestGraphics.Render(stringToDraw.Printer, new RGBA_Bytes(0, 0, 0));
				}
			}
			else
			{
				RGBA_Bytes HoleColor = new RGBA_Bytes(0, 0, 0);

				String OpenSpaceString;

				if (!MomsGame.SpaceIsClickable(CardX, CardY))
				{
					HoleColor = new RGBA_Bytes(0xf8, 0xe2, 0xe8);
					OpenSpaceString = "X";
				}
				else
				{
					HoleColor = new RGBA_Bytes(0xe1, 0xe0, 0xf6);
					OpenSpaceString = "O";
				}

				TextWidget stringToDraw = new TextWidget(OpenSpaceString, 35);
				RectangleDouble Size = stringToDraw.Printer.LocalBounds;
				DestGraphics.SetTransform(Affine.NewTranslation(CardBounds.Left + CardBounds.Width / 2 - Size.Width / 2, CardBounds.Bottom + CardBounds.Height / 2 - Size.Height / 2));
				DestGraphics.Render(stringToDraw.Printer, HoleColor);
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			{
				ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());
				Graphics2D subGraphics2D = widgetsSubImage.NewGraphics2D();

				subGraphics2D.Clear(new RGBA_Bytes(255, 255, 255));
				for (int y = 0; y < MomsGame.GetHeight(); y++)
				{
					for (int x = 0; x < MomsGame.GetWidth(); x++)
					{
						DrawCard(subGraphics2D, x, y);
					}
				}

				String whatToDo = "Select any open space marked with an 'O'";
				RGBA_Bytes backFillCollor = new RGBA_Bytes(0xe1, 0xe0, 0xf6);

				if (MomsGame.GetWaitingForKing())
				{
					backFillCollor = new RGBA_Bytes(0xf8, 0x89, 0x78);
					whatToDo = "Select a King for the hole";
				}
				else if (MomsGame.IsSolved())
				{
					backFillCollor = new RGBA_Bytes(0xf8, 0x89, 0x78);
					whatToDo = "You win!";
				}
				else if (!MomsGame.MoveAvailable())
				{
					backFillCollor = new RGBA_Bytes(0xf8, 0x89, 0x78);
					whatToDo = "No more moves! Shuffle to continue.";
				}

				if (whatToDo != null)
				{
					TextWidget stringToDraw = new TextWidget(whatToDo, 12);
					RectangleDouble Size = stringToDraw.Printer.LocalBounds;
					double TextX = m_BoardX + CARD_WIDTH * 4;
					double TextY = m_BoardY - 34;
					RoundedRect BackFill = new RoundedRect(Size.Left - 6, Size.Bottom - 3, Size.Right + 6, Size.Top + 6, 3);
					Stroke BackBorder = new Stroke(BackFill);
					BackBorder.width(2);

					subGraphics2D.SetTransform(Affine.NewTranslation(TextX, TextY));
					subGraphics2D.Render(BackFill, backFillCollor);
					subGraphics2D.Render(BackBorder, new RGBA_Bytes(0, 0, 0));
					subGraphics2D.Render(stringToDraw.Printer, new RGBA_Bytes(0, 0, 0));
				}

				String ShufflesString;
				ShufflesString = "Number of shuffles so far = ";
				ShufflesString += MomsGame.GetNumShuffles().ToString();

				TextWidget shuffelStringToDraw = new TextWidget(ShufflesString, 12);
				subGraphics2D.SetTransform(Affine.NewTranslation(m_BoardX, 350));
				subGraphics2D.Render(shuffelStringToDraw.Printer, new RGBA_Bytes(0, 0, 0));

				subGraphics2D.SetTransform(Affine.NewIdentity());
			}
			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			if (MouseCaptured)
			{
				int StartX = (int)((mouseEvent.X - m_BoardX) / CARD_WIDTH);
				int StartY = (int)((mouseEvent.Y - m_BoardY) / CARD_HEIGHT);
				if (StartX < 13 && StartY < 4 && MomsGame.MoveCard(StartX, StartY))
				{
					Invalidate();
				}
			}
		}

		public void DoShuffle(object sender, EventArgs mouseEvent)
		{
			MomsGame.Shuffle();
			Invalidate();
		}

		public void DoUndo(object sender, EventArgs mouseEvent)
		{
			MomsGame.UndoLastMove();
			Invalidate();
		}

		public void DoNewGame(object sender, EventArgs mouseEvent)
		{
			MomsGame.NewGame();
			Invalidate();
		}

		public override void OnKeyDown(KeyEventArgs KeyEvent)
		{
			switch (KeyEvent.KeyCode)
			{
				case Keys.S:
					DoShuffle(this, null);
					break;

				case Keys.U:
					DoUndo(this, null);
					break;
			}
		}
	}

	public class MomsSolitaireFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new MomsSolitaire();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
			"Game",
			"Moms Solitaire",
			"A port of the Forth solitaire game that my cousin Marlin Eller wrote for his mom on mothers day in 1989.",
			691,
			390);

			return appWidgetInfo;
		}
	}
}