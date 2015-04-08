using Gaming.Game;
using MatterHackers.Agg;
using System;

namespace RockBlaster
{
	public class PlayerSaveInfo : GameObject
	{
		private static PlayerSaveInfo s_TestInfo = new PlayerSaveInfo();

		public static PlayerSaveInfo GetPlayerInfo()
		{
			return s_TestInfo;
		}

		#region GameObjectStuff

		public PlayerSaveInfo()
		{
		}

		public static new GameObject Load(String PathName)
		{
			return GameObject.Load(PathName);
		}

		#endregion GameObjectStuff

		public void Draw(Graphics2D currentRenderer)
		{
		}

		public void Update(double NumSecondsPassed)
		{
		}
	}
}