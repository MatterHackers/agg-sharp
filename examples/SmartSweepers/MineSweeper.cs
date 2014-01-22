using System;
using System.Collections.Generic;
using NeuralNet;

using Gaming.Math;

using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace SmartSweeper
{
    class CMinesweeper
    {
        double m_dMaxTurnRate;
        int m_WindowWidth;
        int m_WindowHeight;

        //the minesweeper's neural net
        CNeuralNet m_ItsBrain;

        //its position in the world
        Vector3 m_vPosition;

        //direction sweeper is facing
        Vector3 m_vLookAt;

        //its rotation (surprise surprise)
        double m_dRotation;

        double m_dSpeed;

        //to store output from the ANN
        double m_lTrack, m_rTrack;

        //the sweeper's fitness score 
        double m_dFitness;

        //the scale of the sweeper when drawn
        double m_dScale;

        //index position of closest mine
        int m_iClosestMine;


        public CMinesweeper(int WindowWidth, int WindowHeight, double SweeperScale, double dMaxTurnRate)
        {
            m_ItsBrain = new CNeuralNet(4, 2, 1, 6, -1, 1);
            //m_ItsBrain = new CNeuralNet(4, 2, 3, 8, -1, 1);
            Random Rand = new Random();
            m_WindowWidth = WindowWidth;
            m_WindowHeight = WindowHeight;
            m_dMaxTurnRate = dMaxTurnRate;
            m_dRotation = (Rand.NextDouble() * (System.Math.PI * 2));
            m_lTrack = (0.16);
            m_rTrack = (0.16);
            m_dFitness = (0);
            m_dScale = SweeperScale;
            m_iClosestMine = (0);
            //create a random start position
            m_vPosition = new Vector3((Rand.NextDouble() * WindowWidth), (Rand.NextDouble() * WindowHeight), 0);
        }

        //updates the ANN with information from the sweepers enviroment
        public bool Update(List<Vector3> mines)
        {

            //this will store all the inputs for the NN
            List<double> inputs = new List<double>();

            //get List to closest mine
            Vector3 vClosestMine = GetClosestMine(mines);

            //normalise it
            vClosestMine.Normalize();

#if false
            // get the angle to the closest mine
            Vector3 DeltaToMine = vClosestMine - m_vPosition;
            Vector2D DeltaToMine2D = new Vector2D(DeltaToMine.x, DeltaToMine.y);
            Vector2D LookAt2D = new Vector2D(m_vLookAt.x, m_vLookAt.y);
            double DeltaAngle = LookAt2D.GetDeltaAngle(DeltaToMine2D);

            inputs.Add(DeltaAngle);
            inputs.Add(DeltaAngle);
            inputs.Add(DeltaAngle);
            inputs.Add(DeltaAngle);
#else

            //add in List to closest mine
            inputs.Add(vClosestMine.x);
            inputs.Add(vClosestMine.y);

            //add in sweepers look at List
            inputs.Add(m_vLookAt.x);
            inputs.Add(m_vLookAt.y);
#endif

            //update the brain and get feedback
            List<double> output = m_ItsBrain.Update(inputs);

            //make sure there were no errors in calculating the 
            //output
            if (output.Count < m_ItsBrain.NumOutputs)
            {
                return false;
            }

            //assign the outputs to the sweepers left & right tracks
            m_lTrack = output[0];
            m_rTrack = output[1];

            //calculate steering forces
            double RotForce = m_lTrack - m_rTrack;

            //clamp rotation
            RotForce = System.Math.Min(System.Math.Max(RotForce, -m_dMaxTurnRate), m_dMaxTurnRate);

            m_dRotation += RotForce;

            m_dSpeed = (m_lTrack + m_rTrack);

            //update Look At 
            m_vLookAt.x = (double)-System.Math.Sin(m_dRotation);
            m_vLookAt.y = (double)System.Math.Cos(m_dRotation);

            //update position
            m_vPosition += (m_vLookAt * m_dSpeed);

            //wrap around window limits
            if (m_vPosition.x > m_WindowWidth) m_vPosition.x = 0;
            if (m_vPosition.x < 0) m_vPosition.x = m_WindowWidth;
            if (m_vPosition.y > m_WindowHeight) m_vPosition.y = 0;
            if (m_vPosition.y < 0) m_vPosition.y = m_WindowHeight;

            return true;
        }


        //used to transform the sweepers vertices prior to rendering
        public void WorldTransform(List<Vector3> sweeper)
        {
            //create the world transformation matrix
            Matrix4X4 matTransform = Matrix4X4.Identity;

            //scale
            matTransform *= Matrix4X4.CreateScale(m_dScale, m_dScale, 1);

            //rotate
            matTransform *= Matrix4X4.CreateRotationZ(m_dRotation);

            //and translate
            matTransform *= Matrix4X4.CreateTranslation(m_vPosition.x, m_vPosition.y, 0);

            //now transform the ships vertices
            for (int i = 0; i < sweeper.Count; i++)
            {
                sweeper[i] = Vector3.Transform(sweeper[i], matTransform);
            }
        }


        //returns a List to the closest mine
        public Vector3 GetClosestMine(List<Vector3> mines)
        {
            double closest_so_far = 99999;

            Vector3 vClosestObject = new Vector3(0, 0, 0);

            //cycle through mines to find closest
            for (int i = 0; i < mines.Count; i++)
            {
                double len_to_object = (mines[i] - m_vPosition).Length;

                if (len_to_object < closest_so_far)
                {
                    closest_so_far = len_to_object;

                    vClosestObject = m_vPosition - mines[i];

                    m_iClosestMine = i;
                }
            }

            return vClosestObject;
        }


        //checks to see if the minesweeper has 'collected' a mine
        public int CheckForMine(List<Vector3> mines, double size)
        {
            Vector3 DistToObject = m_vPosition - mines[m_iClosestMine];

            if (DistToObject.Length < (size + 5))
            {
                return m_iClosestMine;
            }

            return -1;
        }


        public void Reset()
        {
            Random Rand = new Random();
            //reset the sweepers positions
            m_vPosition = new Vector3((Rand.NextDouble() * m_WindowWidth),
                                            (Rand.NextDouble() * m_WindowHeight), 0);

            //and the fitness
            m_dFitness = 0;

            //and the rotation
            m_dRotation = Rand.NextDouble() * (System.Math.PI * 2);

            return;
        }

        //-------------------accessor functions
        public Vector3 Position() { return m_vPosition; }

        public void IncrementFitness() { ++m_dFitness; }

        public double Fitness() { return m_dFitness; }

        public void PutWeights(List<double> w) { m_ItsBrain.PutWeights(w); }

        public int GetNumberOfWeights() { return m_ItsBrain.GetNumberOfWeights(); }
    };
}