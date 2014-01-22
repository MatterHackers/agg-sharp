using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using AGG;
using AGG.Image;
using AGG.VertexSource;
using AGG.UI;

using Gaming.Math;
using Gaming.Game;


namespace CTFA
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

        public void Draw(RendererBase currentRenderer)
        {
        }

        public void Update(double NumSecondsPassed)
        {
        }
    }
}
