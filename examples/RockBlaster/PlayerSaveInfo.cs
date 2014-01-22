using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;

using Gaming.Math;
using Gaming.Game;


namespace RockBlaster
{
    public class PlayerSaveInfo : GameObject
    {
        static PlayerSaveInfo s_TestInfo = new PlayerSaveInfo();

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
        #endregion

        public void Draw(Graphics2D currentRenderer)
        {
        }

        public void Update(double NumSecondsPassed)
        {
        }
    }
}
