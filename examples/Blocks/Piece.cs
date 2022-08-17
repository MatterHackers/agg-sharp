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
    public static class Piece
    {
        public static string I => @"
00000 00000 00000 00000 
00000 00100 00000 00100 
00000 00100 00000 00100 
01111 00100 01111 00100 
00000 00100 00000 00100 ";

        public static string J => @"
00000 00000 00000 00000 
00000 00100 01000 00110 
01110 00100 01110 00100 
00010 01100 00000 00100 
00000 00000 00000 00000 ";

        public static string L => @"
00000 00000 00000 00000 
00000 01100 00010 00100 
01110 00100 01110 00100 
01000 00100 00000 00110 
00000 00000 00000 00000 ";

        public static string S => @"
00000 00000 00000 00000 
00000 00100 00000 00100 
00110 00110 00110 00110 
01100 00010 01100 00010 
00000 00000 00000 00000 ";

        public static string Z => @"
00000 00000 00000 00000 
00000 00010 00000 00010 
01100 00110 01100 00110 
00110 00100 00110 00100 
00000 00000 00000 00000 ";

        public static string O => @"
00000 00000 00000 00000 
00110 00110 00110 00110 
00110 00110 00110 00110 
00000 00000 00000 00000 
00000 00000 00000 00000 ";

        public static string T => @"
00000 00000 00000 00000 
00000 00100 00100 00100 
01110 01100 01110 00110 
00100 00100 00000 00100 
00000 00000 00000 00000 ";

        public static string GetPiece(int index)
        {
            switch(index)
            {
                case 0:
                    return I;
                case 1:
                    return O;
                case 2:
                    return T;
                case 3:
                    return J;
                case 4:
                    return L;
                case 5:
                    return S;
                case 6:
                    return Z;

            }

            return null;
        }
        
        public static int GetOffset(int rotation, int x, int y)
        {
            var offsetY = (4 - y) * (6 * 4 + 2) + 2;
            var offsetR = rotation * 6;
            return offsetY + offsetR + x;
        }

        public static int GetValue(int currentPiece, int rotation, int x, int y)
        {
            var piece = GetPiece(currentPiece);
            return int.Parse(piece[GetOffset(rotation, x, y)].ToString());
        }

        public static void Draw(Graphics2D graphics2D, int currentPiece, int rotation, int pixelX, int pixelY, int pieceSize)
        {
            // draw all the board pieces
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    if (GetValue(currentPiece, rotation, x, y) != 0)
                    {
                        var xOffset = pixelX + x * (pieceSize + 1);
                        var yOffset = pixelY + y * (pieceSize + 1);
                        graphics2D.FillRectangle(xOffset, yOffset, xOffset + pieceSize, yOffset + pieceSize, Color.Red);
                    }
                }
            }
        }
    }
}