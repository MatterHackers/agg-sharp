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

namespace MatterHackers.Agg
{
    public class BlocksBoard
    {
        public BlocksBoard(int width, int height)
        {
            Width = width;
            Height = height;
            BoardArry = new int[Width + 2, Height + 1];
            Reset();
        }

        private void Reset()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    BoardArry[x, y] = 0;
                }
            }

            // set the first row to 1
            for (int x = 0; x < Width; x++)
            {
                BoardArry[x, 0] = 1;
            }

            // set the left and right side to 1
            for (int y = 0; y < Height; y++)
            {
                BoardArry[0, y] = 1;
                BoardArry[Width - 1, y] = 1;
            }

            X = Width / 2;
            Y = Height - 1;

            SecondsToNextMoveDown = 1;
        }

        public bool PositionValid(int x, int y, int rotation)
        {
            if (x < 0 || x >= Width || y < 0)
            {
                return false;
            }
            
            for (int py = 0; py < 5; py++)
            {
                for (int px = 0; px < 5; px++)
                {
                    if (Piece.GetValue(CurrentPiece, rotation, px, py) != 0
                        && BoardArry[x + px - 2, y + py - 2] != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void DropPiece()
        {
            while (PositionValid(X, Y - 1, Rotation))
            {
                Y--;
            }

            PlacePiece();
        }

        public void Rotate()
        {
            var newRotation = (Rotation + 1) % 4;
            if (PositionValid(X, Y, newRotation))
            {
                Rotation = newRotation;
            }
        }

        public void MoveDown()
        {
            if (PositionValid(X, Y - 1, Rotation))
            {
                Y--;
            }
            else
            {
                PlacePiece();
            }

            SecondsToNextMoveDown = SecondsBetweenMoveDowns;
        }

        public void ClearRows()
        {
            var linesCleared = 0;
            var removedARow = true;
            while (removedARow)
            {
                removedARow = false;
                // find all the filled rows and remove them
                for (int y = 1; y < Height; y++)
                {
                    bool rowFull = true;
                    for (int x = 0; x < Width; x++)
                    {
                        if (BoardArry[x, y] == 0)
                        {
                            rowFull = false;
                            break;
                        }
                    }

                    if (rowFull)
                    {
                        linesCleared++;
                        removedARow = true;
                        // move all the rows above down
                        for (int yy = y; yy < Height - 1; yy++)
                        {
                            for (int x = 0; x < Width; x++)
                            {
                                BoardArry[x, yy] = BoardArry[x, yy + 1];
                            }
                        }

                        // set the edges of the top row to 1
                        BoardArry[0, Height - 1] = 1;
                        BoardArry[Width - 1, Height - 1] = 1;
                    }
                }
            }

            AddToScore(linesCleared);
        }

        public int Score { get; set; }

        private void AddToScore(int linesCleared)
        {
            var points = 100;
            switch(linesCleared)
            {
                case 1:
                    points = 100;
                    break;
                case 2:
                    points = 300;
                    break;
                case 3:
                    points = 500;
                    break;
                case 4:
                    points = 800;
                    break;
            }

            Score += (int)(points * ScoreMultiplier);
        }

        public void MoveLeft()
        {
            if (PositionValid(X - 1, Y, Rotation))
            {
                X--;
            }
        }

        public void MoveRight()
        {
            if (PositionValid(X + 1, Y, Rotation))
            {
                X++;
            }
        }

        public void PlacePiece()
        {
            for (int py = 0; py < 5; py++)
            {
                for (int px = 0; px < 5; px++)
                {
                    var pieceValue = Piece.GetValue(CurrentPiece, Rotation, px, py);
                    if (pieceValue != 0)
                    {
                        BoardArry[X + px - 2, Y + py - 2] = pieceValue;
                    }
                }
            }

            ClearRows();

            GetNextPiece();
        }

        Random rand = new Random();

        public void GetNextPiece()
        {
            CurrentPiece = NextPiece;
            Rotation = 0;
            NextPiece = rand.Next(7);
            X = Width / 2;
            Y = Height - 1;
        }

        public void Draw(Graphics2D graphics2D, int pixelX, int pixelY, int pieceSize)
        {
            // draw all the board pieces
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    if (BoardArry[i, j] == 1)
                    {
                        var xOffset = pixelX + i * (pieceSize + 1);
                        var yOffset = pixelY + j * (pieceSize + 1);
                        graphics2D.FillRectangle(xOffset, yOffset, xOffset + pieceSize, yOffset + pieceSize, Color.Red);
                    }
                }
            }

            var pieceX = pixelX + (X - 2) * (pieceSize + 1);
            var pieceY = pixelY + (Y - 2) * (pieceSize + 1);
            Piece.Draw(graphics2D, CurrentPiece, Rotation, pieceX, pieceY, pieceSize);
        }

        public int[,] BoardArry { get; }
        public int Width { get; }
        public int Height { get; }

        public int X { get; set; }
        public int Y { get; set; }

        public int CurrentPiece { get; set; } = 2;
        
        public int NextPiece { get; set; } = 0;
        
        public int Rotation { get; set; }

        public double SecondsToNextMoveDown { get; set; }

        public double SecondsBetweenMoveDowns { get; set; } = .5;
        public double ScoreMultiplier { get; private set; } = 1;
        public bool Paused { get; internal set; }
    }
}