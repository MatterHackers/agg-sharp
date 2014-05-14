//#define USE_GLSL
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

using Gaming.Math;
using Gaming.Game;
using Gaming.Graphics;

namespace RockBlaster
{
    public class Playfield : GameObject
    {
        [GameDataList("RockList")]
        private List<Entity> m_RockList = new List<Entity>();

        private List<Entity> m_SequenceEntityList = new List<Entity>();

        [GameData("PlayerList")]
        private List<Player> m_PlayerList = new List<Player>();

        //Model3D m_Ship = new Model3D("..\\..\\GameData\\ShipTris.dae");

        #region GameObjectStuff
        public Playfield()
        {
        }
        public static new GameObject Load(String PathName)
        {
            return GameObject.Load(PathName);
        }
        #endregion

        public List<Entity> RockList
        {
            get
            {
                return m_RockList;
            }
        }

        public List<Player> PlayerList
        {
            get
            {
                return m_PlayerList;
            }
        }

        public void StartOnePlayerGame()
        {
            m_SequenceEntityList.Add(new SequenceEntity(new Vector2(20, 20)));
            m_RockList.Add(new Rock(m_RockList, 40));
            m_RockList.Add(new Rock(m_RockList, 40));
            m_PlayerList.Add(new Player(0, 0, Keys.Z, Keys.X, Keys.OemPeriod, Keys.OemQuestion));
            m_PlayerList[0].Position = new Vector2(Entity.GameWidth / 2, Entity.GameHeight / 2);
        }

        internal void StartTwoPlayerGame()
        {
            m_SequenceEntityList.Add(new SequenceEntity(new Vector2(20, 20)));
            m_RockList.Add(new Rock(m_RockList, 0));
            m_RockList.Add(new Rock(m_RockList, 0));
            m_RockList.Add(new Rock(m_RockList, 0));

            m_PlayerList.Add(new Player(0, 0, Keys.Z, Keys.X, Keys.V, Keys.B));
            m_PlayerList[0].Position = new Vector2(Entity.GameWidth / 4, Entity.GameHeight / 2);

            m_PlayerList.Add(new Player(1, 1, Keys.N, Keys.M, Keys.OemPeriod, Keys.OemQuestion));
            m_PlayerList[1].Position = new Vector2(Entity.GameWidth / 4 * 3, Entity.GameHeight / 2);
        }

        internal void StartFourPlayerGame()
        {
            m_SequenceEntityList.Add(new SequenceEntity(new Vector2(20, 20)));
            m_RockList.Add(new Rock(m_RockList, 0));
            m_RockList.Add(new Rock(m_RockList, 0));
            m_RockList.Add(new Rock(m_RockList, 0));

            m_PlayerList.Add(new Player(0, -1, Keys.Z, Keys.X, Keys.V, Keys.B));
            m_PlayerList[0].Position = new Vector2(Entity.GameWidth / 4, Entity.GameHeight / 4 * 3);

            m_PlayerList.Add(new Player(1, -1, Keys.N, Keys.M, Keys.OemPeriod, Keys.OemQuestion));
            m_PlayerList[1].Position = new Vector2(Entity.GameWidth / 4 * 3, Entity.GameHeight / 4 * 3);

            m_PlayerList.Add(new Player(2, 0, Keys.Q, Keys.Q, Keys.Q, Keys.Q));
            m_PlayerList[2].Position = new Vector2(Entity.GameWidth / 4, Entity.GameHeight / 4);

            m_PlayerList.Add(new Player(3, 1, Keys.Q, Keys.Q, Keys.Q, Keys.Q));
            m_PlayerList[3].Position = new Vector2(Entity.GameWidth / 4 * 3, Entity.GameHeight / 4);
        }

        public void Draw(Graphics2D currentRenderer)
        {
            foreach (Rock aRock in m_RockList)
            {
                aRock.Draw(currentRenderer);
            }

            foreach (Player aPlayer in m_PlayerList)
            {
                aPlayer.DrawBullets(currentRenderer);
            }

            foreach (SequenceEntity aSequenceEntity in m_SequenceEntityList)
            {
                aSequenceEntity.Draw(currentRenderer);
            }

            foreach (Player aPlayer in m_PlayerList)
            {
                aPlayer.Draw(currentRenderer);
            }

            GameImageSequence hud = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), (m_PlayerList.Count).ToString() + "PlayerHUD");
            currentRenderer.Render(hud.GetImageByIndex(0), 400, 300);

            foreach (Player aPlayer in m_PlayerList)
            {
                aPlayer.DrawScore(currentRenderer);
            }
#if false
            Gl.glMatrixMode(Gl.GL_PROJECTION);                                  // Select The Projection Matrix
            Gl.glLoadIdentity();                                                // Reset The Projection Matrix
            Glu.gluPerspective(45, Entity.GameWidth / (double)Entity.GameHeight, 0.1, 100);          // Calculate The Aspect Ratio Of The Window
            Gl.glMatrixMode(Gl.GL_MODELVIEW);                                   // Select The Modelview Matrix

            float scale = .08f;
            Gl.glLoadIdentity();                                                // Reset The Current Modelview Matrix
            
            Gl.glLightf(Gl.GL_LIGHT0, Gl.GL_CONSTANT_ATTENUATION, 0.0f);
            Gl.glLightf(Gl.GL_LIGHT0, Gl.GL_LINEAR_ATTENUATION, 0.0f);
            Gl.glLightf(Gl.GL_LIGHT0, Gl.GL_QUADRATIC_ATTENUATION, 0.0002f);
            float[] position = new float[] { -10.5f, 10.0f, 20.0f, 1.0f };

            Gl.glScalef(scale, scale, scale);
            Gl.glTranslatef(0, 0, -160);                                      // Move Left 1.5 Units And Into The Screen 6.0
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, position);
            Gl.glTranslatef((float)m_Player.Position.x - 200, (float)m_Player.Position.y - 200, 0);
            Gl.glRotatef((float)((m_Player.m_Rotation - Math.PI / 2) / Math.PI * 180), 0, 0, 1);                                        // Rotate The Triangle On The Y axis ( NEW )
            Gl.glShadeModel(Gl.GL_FLAT);

            Gl.glClearDepth(1);                                                 // Depth Buffer Setup
            Gl.glEnable(Gl.GL_DEPTH_TEST);                                      // Enables Depth Testing
            Gl.glDepthFunc(Gl.GL_LEQUAL);                                       // The Type Of Depth Testing To Do

            Gl.glDisable(Gl.GL_CULL_FACE);
#endif

#if USE_GLSL
            String[] vertexSource = System.IO.File.ReadAllLines("..\\..\\GameData\\marble.vert");
            vertexSource = new string[] { "void main(void) { gl_Position = ftransform(); } " };
            vertexSource = new string[] { "varying vec3 normal, lightDir, eyeVec; void main() { normal = gl_NormalMatrix * gl_Normal; vec3 vVertex = vec3(gl_ModelViewMatrix * gl_Vertex); lightDir = vec3(gl_LightSource[0].position.xyz - vVertex); eyeVec = -vVertex; gl_Position = ftransform(); }" };

            int vertexShader = Gl.glCreateShader(Gl.GL_VERTEX_SHADER);
            Gl.glShaderSource(vertexShader, vertexSource.Length, vertexSource, null);
            Gl.glCompileShader(vertexShader);
            
            int goodCompile;
            Gl.glGetShaderiv(vertexShader, Gl.GL_COMPILE_STATUS, out goodCompile);

            String[] fragmentSource = System.IO.File.ReadAllLines("..\\..\\GameData\\marble.frag");
            fragmentSource = new string[] { "void main(void) { gl_FragColor = vec4(1.0, 0.0, 0.0, 1.0); }" };
            fragmentSource = new string[] { "varying vec3 normal, lightDir, eyeVec; void main (void) { vec4 final_color = (gl_FrontLightModelProduct.sceneColor * gl_FrontMaterial.ambient) + (gl_LightSource[0].ambient * gl_FrontMaterial.ambient); vec3 N = normalize(normal); vec3 L = normalize(lightDir); float lambertTerm = dot(N,L); if(lambertTerm > 0.0) { final_color += gl_LightSource[0].diffuse * gl_FrontMaterial.diffuse * lambertTerm; vec3 E = normalize(eyeVec); vec3 R = reflect(-L, N); float specular = pow( max(dot(R, E), 0.0), gl_FrontMaterial.shininess ); final_color += gl_LightSource[0].specular * gl_FrontMaterial.specular * specular; } gl_FragColor = final_color; }" };

            int fragmentShader = Gl.glCreateShader(Gl.GL_FRAGMENT_SHADER);
            Gl.glShaderSource(fragmentShader, fragmentSource.Length, fragmentSource, null);
            Gl.glCompileShader(fragmentShader);

            Gl.glGetShaderiv(fragmentShader, Gl.GL_COMPILE_STATUS, out goodCompile);

            int shaderProgram;
            shaderProgram = Gl.glCreateProgram();
            Gl.glAttachShader(shaderProgram, fragmentShader);
            Gl.glAttachShader(shaderProgram, vertexShader);
            Gl.glLinkProgram(shaderProgram);
            Gl.glGetProgramiv(shaderProgram, Gl.GL_LINK_STATUS, out goodCompile);

            System.Text.StringBuilder infoLog = new System.Text.StringBuilder();
            int length;
            Gl.glGetShaderInfoLog(shaderProgram, 500, out length, infoLog);

            Gl.glUseProgram(shaderProgram);
#endif
            //m_Ship.Render();

#if USE_GLSL
            Gl.glUseProgram(0);
            Gl.glDetachShader(shaderProgram, fragmentShader);
            Gl.glDetachShader(shaderProgram, vertexShader);
            Gl.glDeleteShader(fragmentShader);
            Gl.glDeleteShader(vertexShader);
            Gl.glDeleteProgram(shaderProgram);
#endif

        }

        public void CollidePlayersWithOtherPlayersBullets()
        {
            foreach (Player aPlayer in m_PlayerList)
            {
                foreach (Player bPlayer in m_PlayerList)
                {
                    if (aPlayer != bPlayer)
                    {
                        foreach (Bullet aBullet in aPlayer.m_BulletList)
                        {
                            Vector2 BulletRelBPlayer = aBullet.Position - bPlayer.Position;
                            double BothRadius = bPlayer.Radius + aBullet.Radius;
                            double BothRadiusSqrd = BothRadius * BothRadius;
                            if (BulletRelBPlayer.LengthSquared < BothRadiusSqrd)
                            {
                                Vector2 addVelocity = aBullet.Velocity;
                                addVelocity *= .7;
                                bPlayer.Velocity = bPlayer.Velocity + addVelocity;
                                bPlayer.m_LastPlayerToShot = aPlayer;
                                aBullet.GiveDamage();
                            }
                        }
                    }
                }
            }
        }

        public void CollideRocksAndPlayersAndBullets()
        {
            foreach (Player aPlayer in m_PlayerList)
            {
                foreach (Rock aRock in m_RockList)
                {
                    {
                        Vector2 playerRelRock = aPlayer.Position - aRock.Position;
                        double BothRadius = aRock.Radius + aPlayer.Radius;
                        double BothRadiusSqrd = BothRadius * BothRadius;
                        if (playerRelRock.LengthSquared < BothRadiusSqrd)
                        {
                            aRock.TakeDamage(aPlayer.GiveDamage(), aPlayer);
                            aPlayer.TakeDamage(20, null);
                        }
                    }

                    foreach (Bullet aBullet in aPlayer.m_BulletList)
                    {
                        Vector2 BulletRelRock = aBullet.Position - aRock.Position;
                        double BothRadius = aRock.Radius + aBullet.Radius;
                        double BothRadiusSqrd = BothRadius * BothRadius;
                        if (BulletRelRock.LengthSquared < BothRadiusSqrd)
                        {
                            aRock.TakeDamage(aBullet.GiveDamage(), aPlayer);
                            if (m_PlayerList.Count == 1)
                            {
                                aPlayer.m_Score += 2;
                            }
                        }
                    }
                }
            }
        }

        protected void RemoveDeadStuff(List<Entity> listToRemoveFrom)
        {
            List<Entity> RemoveList = new List<Entity>();

            foreach (Entity aEntity in listToRemoveFrom)
            {
                if (aEntity.Damage >= aEntity.MaxDamage)
                {
                    RemoveList.Add(aEntity);
                }
            }

            foreach (Entity aEntity in RemoveList)
            {
                aEntity.Destroying();
                listToRemoveFrom.Remove(aEntity);
            }
        }

        public void Update(double NumSecondsPassed)
        {
            int numBigRocks = 0;
            foreach (Rock aRock in m_RockList)
            {
                aRock.Update(NumSecondsPassed);
                if (aRock.scaleRatio == 1)
                {
                    numBigRocks++;
                }
            }

            if (m_RockList.Count < 20 && numBigRocks < 1 && m_PlayerList.Count > 1)
            {
                m_RockList.Add(new Rock(m_RockList, 0));
            }

            foreach(SequenceEntity aSequenceEntity in m_SequenceEntityList)
            {
                aSequenceEntity.Update(NumSecondsPassed);
            }

            foreach (Player aPlayer in m_PlayerList)
            {
                aPlayer.Update(NumSecondsPassed);
            }

            RemoveDeadStuff(m_RockList);

            CollideRocksAndPlayersAndBullets();

            CollidePlayersWithOtherPlayersBullets();

            RemoveDeadStuff(m_RockList);
            foreach (Player aPlayer in m_PlayerList)
            {
                RemoveDeadStuff(aPlayer.m_BulletList);
            }
            RemoveDeadStuff(m_SequenceEntityList);
        }
    }
}
