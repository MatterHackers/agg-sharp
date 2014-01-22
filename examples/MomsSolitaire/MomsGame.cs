// created on 2/3/2002 at 8:59 PM
using System; //.Security.Cryptography.RandomNumberGenerator;
using System.Diagnostics; // for assert

namespace MomsSolitaire
{
    public class CMove
    {
        public CMove()
        {
        }

        public CMove(int x1, int y1, int x2, int y2, int MoveIndex)
        {
            m_x1 = x1; m_y1 = y1; m_x2 = x2; m_y2 = y2; m_MoveIndex = MoveIndex;
        }

        public int m_x1;
        public int m_y1;
        public int m_x2;
        public int m_y2;
        public int m_MoveIndex;
    };

    public class CMomsGame
    {
        enum BoardSize { WIDTH = 13, HEIGHT = 4 };
        enum Undos { MAX_UNDOS = 10000 };

        Random Rand = new Random();
        CMove[] m_SwapHistory = new CMove[(int)Undos.MAX_UNDOS];
        int m_NumActionsInGame;
        int m_CurSwapIndex;
        int m_NumSwapsInGame;

        bool m_WaitingForKing;
        int m_WaitingKingY;
        CDeckOfCards m_DeckOfCards = new CDeckOfCards();
        int m_NumShuffles;
        CCard[,] m_CardLayout = new CCard[(int)BoardSize.WIDTH, (int)BoardSize.HEIGHT];

        public CMomsGame()
        {
            NewGame();
        }

        public int GetWidth()
        {
            return (int)BoardSize.WIDTH;
        }

        public int GetHeight()
        {
            return (int)BoardSize.HEIGHT;
        }

        public CCard GetCard(int SlotX, int SlotY)
        {
            return m_CardLayout[SlotX, SlotY];
        }

		public bool SpaceIsClickable(int CardX, int CardY)
		{
            if (CardX == 0)
            {
                return true;
            }
            CCard card = GetCard(CardX, CardY);
            CCard CardToLeft = GetCard(CardX - 1, CardY);

            if ((card.GetValue() == (int)CCard.CARD_VALUE.VALUE_ACE)
                && CardToLeft.GetValue() > 2)
			{
				return true;
			}

			return false;
		}

        bool CardIsInOrder(int SlotX, int SlotY)
        {
            CCard CurCard = GetCard(SlotX, SlotY);
            if (CurCard.GetValue() - (13 - SlotX) == 0)
            {
                // we know the card is in the right place is everything to the left of it
                // in the right place and the same suit
                for (int i = 0; i < SlotX; i++)
                {
                    CCard TestCard = GetCard(i, SlotY);
                    if (CurCard.GetSuit() != TestCard.GetSuit()
                        || TestCard.GetValue() - (13 - i) != 0)
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public bool MoveCard(int SlotX, int SlotY)
        {
            CCard CurCard = GetCard(SlotX, SlotY);

            if (m_WaitingForKing)
            {
                if (CurCard.GetValue() == 13)
                {
                    CMove Move = new CMove(0, m_WaitingKingY, SlotX, SlotY, m_NumActionsInGame++);
                    SwapCards(Move);
                    m_WaitingForKing = false;
                    return true;
                }
            }
            else
            {
                // make sure we clicked on a hole
                if (CurCard.GetValue() == 1)
                {
                    // figure out what card we want to put here
                    if (SlotX == 0)
                    {
                        // we clicked on a king only slot
                        m_WaitingForKing = true;
                        m_WaitingKingY = SlotY;
                        return true;
                    }
                    else
                    {
                        CCard CardToLeft = GetCard(SlotX - 1, SlotY);
                        if (CardToLeft.GetValue() < 3)
                        {
                            return false;
                        }
                        // find the card that we want to put here
                        for (int Y = 0; Y < (int)BoardSize.HEIGHT; Y++)
                        {
                            for (int X = 0; X < (int)BoardSize.WIDTH; X++)
                            {
                                CCard CheckCard = GetCard(X, Y);
                                if (CheckCard.GetSuit() == CardToLeft.GetSuit()
                                    && CheckCard.GetValue() == CardToLeft.GetValue() - 1)
                                {
                                    CMove Move = new CMove(X, Y, SlotX, SlotY, m_NumActionsInGame++);
                                    SwapCards(Move);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        void SwapCards(CCard CardA, CCard CardB)
        {
            int TempSuit = CardA.GetSuit();
            int TempValue = CardA.GetValue();
            CardA.SetSuit(CardB.GetSuit());
            CardA.SetValue(CardB.GetValue());
            CardB.SetSuit(TempSuit);
            CardB.SetValue(TempValue);
        }

        public void NewGame()
        {
            m_NumShuffles = 0;
            m_CurSwapIndex = 0;
            m_NumActionsInGame = 0;

            m_WaitingForKing = false;

            // first put them in order so they start in a known state	
            for (int Y = 0; Y < (int)BoardSize.HEIGHT; Y++)
            {
                for (int X = 0; X < (int)BoardSize.WIDTH; X++)
                {
                    int SlotIndex = Y * (int)BoardSize.WIDTH + X;
                    m_CardLayout[X, Y] = m_DeckOfCards.GetCard(SlotIndex);
                }
            }

            // then shuffle them
            for (int Y = 0; Y < (int)BoardSize.HEIGHT; Y++)
            {
                for (int X = 0; X < (int)BoardSize.WIDTH; X++)
                {
                    int OtherX = (int)Rand.Next((int)BoardSize.WIDTH);
                    int OtherY = (int)Rand.Next((int)BoardSize.HEIGHT);

                    SwapCards(m_CardLayout[OtherX, OtherY], m_CardLayout[X, Y]);
                }
            }
        }

        public void Shuffle()
        {
            if (m_WaitingForKing)
            {
                m_WaitingForKing = false;
            }

            m_NumShuffles++;

            for (int Y = 0; Y < (int)BoardSize.HEIGHT; Y++)
            {
                for (int X = 0; X < (int)BoardSize.WIDTH; X++)
                {
                    if (!CardIsInOrder(X, Y))
                    {
                        int Tries = 0;
                        int OtherX = (int)Rand.Next((int)BoardSize.WIDTH);
                        int OtherY = (int)Rand.Next((int)BoardSize.HEIGHT);
                        while (CardIsInOrder(OtherX, OtherY) && Tries++ < 100000)
                        {
                            OtherX = (int)Rand.Next((int)BoardSize.WIDTH);
                            OtherY = (int)Rand.Next((int)BoardSize.HEIGHT);
                        }

                        CMove Move = new CMove(X, Y, OtherX, OtherY, m_NumActionsInGame);
                        SwapCards(Move);
                    }
                }
            }

            m_NumActionsInGame++;

			if(!MoveAvailable() && !IsSolved())
			{
				int expectedNumShuffles = m_NumShuffles;
				// if you shuffle results in no move available, undo the shuffel and try again.  Rand should change and we will get a new shuffle
				UndoLastMove();
				Shuffle();
				m_NumShuffles =  expectedNumShuffles;
			}
        }

        public void SwapCards(CMove Move)
        {
            SwapCards(m_CardLayout[Move.m_x1, Move.m_y1], m_CardLayout[Move.m_x2, Move.m_y2]);
            if (m_CurSwapIndex < (int)Undos.MAX_UNDOS)
            {
                m_SwapHistory[m_CurSwapIndex++] = Move;
            }
            m_NumSwapsInGame = m_CurSwapIndex;
        }

        public void UndoLastMove()
        {
            if (m_WaitingForKing)
            {
                m_WaitingForKing = false;
                return;
            }

            if (m_CurSwapIndex > 0)
            {
                //Debug.Assert(m_NumActionsInGame > 0);
                //Debug.Assert(m_CurSwapIndex <= m_NumSwapsInGame);
                int NumBackuped = 0;

                while (m_CurSwapIndex > 0 && m_SwapHistory[m_CurSwapIndex - 1].m_MoveIndex == m_NumActionsInGame - 1)
                {
                    NumBackuped++;
                    CMove CurMove = m_SwapHistory[m_CurSwapIndex - 1];
                    //Debug.Assert(m_CurSwapIndex > 0);
                    SwapCards(m_CardLayout[CurMove.m_x2, CurMove.m_y2], m_CardLayout[CurMove.m_x1, CurMove.m_y1]);
                    m_CurSwapIndex--;
                }

                if (NumBackuped > 1)
                {
                    m_NumShuffles--;
                }

                m_NumActionsInGame--;
            }
        }

        void RedoLastUndo()
        {
            if (m_CurSwapIndex < m_NumSwapsInGame)
            {
                //Debug.Assert(m_NumActionsInGame >= 0);
                int NumRedone = 0;

                while (m_CurSwapIndex <= m_NumSwapsInGame
                    && m_SwapHistory[m_CurSwapIndex].m_MoveIndex == m_NumActionsInGame)
                {
                    NumRedone++;
                    CMove CurMove = m_SwapHistory[m_CurSwapIndex];
                    //Debug.Assert(m_CurSwapIndex < m_NumSwapsInGame);
                    SwapCards(m_CardLayout[CurMove.m_x2, CurMove.m_y2], m_CardLayout[CurMove.m_x1, CurMove.m_y1]);
                    m_CurSwapIndex++;
                }

                if (NumRedone > 1)
                {
                    m_NumShuffles++;
                }

                m_NumActionsInGame++;
            }
        }

		public bool IsSolved()
		{
			if(CardIsInOrder(11, 0)
			   && CardIsInOrder(11,1 )
			   && CardIsInOrder(11, 2)
			   && CardIsInOrder(11, 3))
			{
				return true;
			}
			
			return false;
		}

		public bool MoveAvailable()
		{
			for (int Y = 0; Y < (int)BoardSize.HEIGHT; Y++)
            {
                if (GetCard(0, Y).GetValue() == (int)CCard.CARD_VALUE.VALUE_ACE)
                {
                    return true;
                }
                for (int X = 1; X < (int)BoardSize.WIDTH; X++)
                {
					if(SpaceIsClickable(X, Y))
					{
						return true;
					}
				}
			}

			return false;
		}

        public bool GetWaitingForKing()
        {
            return m_WaitingForKing;
        }

        public int GetNumShuffles()
        {
            return m_NumShuffles;
        }
    };
}