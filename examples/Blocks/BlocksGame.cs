/*
Copyright (c) 2013, Lars Brubaker
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

using System;
using Gaming.Game;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;

namespace MatterHackers.Agg
{
    public class BlocksGame : GamePlatform, IDemoApp
	{
        private BlocksBoard board;

        public BlocksGame()
            : base(60, 3, 512, 800)
		{
			BackgroundColor = Color.LightBlue;
            board = new BlocksBoard(10, 20);

			var buttonsBar = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Bottom
			};

			this.AddChild(buttonsBar);

			var theme = new ThemeConfig();

			var newGame = new ThemedTextButton("New Game", theme);
			newGame.Click += (s, e) =>
			{
				board.Reset();
			};
            
			buttonsBar.AddChild(newGame);

			var pauseGame = new ThemedTextButton("Pause", theme);
			pauseGame.Click += (s, e) =>
			{
				board.Paused = !board.Paused;
			};
			buttonsBar.AddChild(pauseGame);
		}

		public string Title { get; } = "Blocks Game";

		public string DemoCategory { get; } = "Games";

		public string DemoDescription { get; } = "A fun example of a game 4 block line compleation game.";

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();
			base.OnParentChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var pieceSize = 20;

			board.Draw(graphics2D, 40, 125, pieceSize);

			Piece.Draw(graphics2D, board.NextPiece, 0, (int)Width/4*3, (int)Height/4*3, pieceSize);

			graphics2D.DrawString(board.Score.ToString(), Width / 2, Height * .95, 16, Font.Justification.Center);
		}

		public override void OnUpdate(double numSecondsPassed)
        {
			if (!board.Paused)
			{
				board.SecondsToNextMoveDown -= numSecondsPassed;
			}
            
            if (board.SecondsToNextMoveDown < 0)
            {
				board.MoveDown();
            }
            
            base.OnUpdate(numSecondsPassed);
        }

        public override void OnKeyDown(KeyEventArgs keyEvent)
        {
            switch(keyEvent.KeyCode)
            {
                case Keys.Left:
                    board.MoveLeft();
                    break;

                case Keys.Right:
                    board.MoveRight();
                    break;

				case Keys.Up:
                    board.Rotate();
                    break;

				case Keys.Down:
					board.Rotate();
					break;

				case Keys.P:
					board.Paused = !board.Paused;
					break;

				case Keys.Space:
					board.DropPiece();
					break;
            }
            
            base.OnKeyDown(keyEvent);
        }

        [STAThread]
		public static void Main(string[] args)
		{
			// Init agg with our OpenGL window definition
			// AggContext.Init(embeddedResourceName: "lion.config.json");
			AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.Agg.UI.OpenGLWinformsWindowProvider, agg_platform_win32";
			// AggContext.Config.ProviderTypes.SystemWindowProvider = "MatterHackers.GlfwProvider.GlfwWindowProvider, MatterHackers.GlfwProvider";

			var demoWidget = new BlocksGame();

			demoWidget.ShowAsSystemWindow();
		}
	}
}