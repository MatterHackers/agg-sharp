using MatterHackers.VectorMath;
using NeuralNet;
using System;
using System.Collections.Generic;

namespace SmartSweeper
{
	internal class CMinesweeper
	{
		private double m_dMaxTurnRate;
		private int m_WindowWidth;
		private int m_WindowHeight;

		//the minesweeper's neural net
		private CNeuralNet Brain;

		//its position in the world
		private Vector3 Position;

		//direction sweeper is facing
		private Vector3 LookAt;

		//its rotation (surprise surprise)
		private double Rotation;

		private double Speed;

		//to store output from the ANN
		private double leftTrack, rightTrack;

		//the sweeper's fitness score
		public double Fitness { get; private set; }

		//the scale of the sweeper when drawn
		private double Scale;

		//index position of closest mine
		private int ClosestMine;

		public CMinesweeper(int WindowWidth, int WindowHeight, double SweeperScale, double dMaxTurnRate)
		{
			//Brain = new CNeuralNet(4, 2, 1, 6, -1, 1);
			Brain = new CNeuralNet(4, 2, 3, 8, -1, 1);
			Random Rand = new Random();
			m_WindowWidth = WindowWidth;
			m_WindowHeight = WindowHeight;
			m_dMaxTurnRate = dMaxTurnRate;
			Rotation = (Rand.NextDouble() * (Math.PI * 2));
			leftTrack = (0.16);
			rightTrack = (0.16);
			Fitness = (0);
			Scale = SweeperScale;
			ClosestMine = (0);
			//create a random start position
			Position = new Vector3((Rand.NextDouble() * WindowWidth), (Rand.NextDouble() * WindowHeight), 0);
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
			inputs.Add(vClosestMine.X);
			inputs.Add(vClosestMine.Y);

			//add in sweepers look at List
			inputs.Add(LookAt.X);
			inputs.Add(LookAt.Y);
#endif

			//update the brain and get feedback
			List<double> output = Brain.Update(inputs);

			//make sure there were no errors in calculating the
			//output
			if (output.Count < Brain.NumOutputs)
			{
				return false;
			}

			//assign the outputs to the sweepers left & right tracks
			leftTrack = output[0];
			rightTrack = output[1];

			//calculate steering forces
			double RotForce = leftTrack - rightTrack;

			//clamp rotation
			RotForce = Math.Min(Math.Max(RotForce, -m_dMaxTurnRate), m_dMaxTurnRate);

			Rotation += RotForce;

			Speed = (leftTrack + rightTrack);

			//update Look At
			LookAt.X = (double)-Math.Sin(Rotation);
			LookAt.Y = (double)Math.Cos(Rotation);

			//update position
			Position += (LookAt * Speed);

			//wrap around window limits
			if (Position.X > m_WindowWidth) Position.X = 0;
			if (Position.X < 0) Position.X = m_WindowWidth;
			if (Position.Y > m_WindowHeight) Position.Y = 0;
			if (Position.Y < 0) Position.Y = m_WindowHeight;

			return true;
		}

		//used to transform the sweepers vertices prior to rendering
		public void WorldTransform(List<Vector3> sweeper)
		{
			//create the world transformation matrix
			Matrix4X4 matTransform = Matrix4X4.Identity;

			//scale
			matTransform *= Matrix4X4.CreateScale(Scale, Scale, 1);

			//rotate
			matTransform *= Matrix4X4.CreateRotationZ(Rotation);

			//and translate
			matTransform *= Matrix4X4.CreateTranslation(Position.X, Position.Y, 0);

			//now transform the ships vertices
			for (int i = 0; i < sweeper.Count; i++)
			{
				sweeper[i] = sweeper[i].Transform(matTransform);
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
				double len_to_object = (mines[i] - Position).Length;

				if (len_to_object < closest_so_far)
				{
					closest_so_far = len_to_object;

					vClosestObject = Position - mines[i];

					ClosestMine = i;
				}
			}

			return vClosestObject;
		}

		//checks to see if the minesweeper has 'collected' a mine
		public int CheckForMine(List<Vector3> mines, double size)
		{
			Vector3 DistToObject = Position - mines[ClosestMine];

			if (DistToObject.Length < (size + 5))
			{
				return ClosestMine;
			}

			return -1;
		}

		public void Reset()
		{
			Random Rand = new Random();
			//reset the sweepers positions
			Position = new Vector3((Rand.NextDouble() * m_WindowWidth),
											(Rand.NextDouble() * m_WindowHeight), 0);

			//and the fitness
			Fitness = 0;

			//and the rotation
			Rotation = Rand.NextDouble() * (Math.PI * 2);

			return;
		}

		//-------------------accessor functions
		public void IncrementFitness()
		{
			++Fitness;
		}

		public void PutWeights(List<double> w)
		{
			Brain.PutWeights(w);
		}

		public int GetNumberOfWeights()
		{
			return Brain.GetNumberOfWeights();
		}
	};
}