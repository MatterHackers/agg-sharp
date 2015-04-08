// createBd on 2/3/2002 at 6:15 PM
//*********************************************************************************************************************
namespace MomsSolitaire
{
	public class CCard
	{
		public enum CARD_SUIT { SUIT_NONE, SUIT_HEART, SUIT_DIAMOND, SUIT_CLUB, SUIT_SPADE, SUIT_WILD };

		public enum CARD_VALUE
		{
			VALUE_NONE, VALUE_ACE, VALUE_TWO, VALUE_THREE, VALUE_FOUR,
			VALUE_FIVE, VALUE_SIX, VALUE_SEVEN, VALUE_EIGHT,
			VALUE_NINE, VALUE_TEN, VALUE_JACK, VALUE_QUEN, VALUE_KING, VALUE_WILD
		};

		public CCard()
		{
		}

		public int GetSuit()
		{
			return m_Suit;
		}

		public void SetSuit(int Suit)
		{
			m_Suit = Suit;
		}

		public int GetValue()
		{
			return m_Value;
		}

		public void SetValue(int Suit)
		{
			m_Value = Suit;
		}

		private int m_Suit;
		private int m_Value;
	};

	//*********************************************************************************************************************
	internal class CDeckOfCards
	{
		public enum NUM_CARDS { CARDS_IN_DECK = 52 };

		public CDeckOfCards()
		{
			int CardIndex = 0;
			for (int SuitType = (int)CCard.CARD_SUIT.SUIT_HEART; SuitType <= (int)CCard.CARD_SUIT.SUIT_SPADE; SuitType++)
			{
				for (int Value = (int)CCard.CARD_VALUE.VALUE_ACE; Value <= (int)CCard.CARD_VALUE.VALUE_KING; Value++)
				{
					m_Cards[CardIndex] = new CCard();
					m_Cards[CardIndex].SetSuit(SuitType);
					m_Cards[CardIndex].SetValue(Value);
					CardIndex++;
				}
			}
		}

		public CCard GetCard(int Index)
		{
			return m_Cards[Index];
		}

		protected CCard[] m_Cards = new CCard[(int)NUM_CARDS.CARDS_IN_DECK];
	};
}