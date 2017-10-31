using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using NeuralNet;
using System;
using System.Collections.Generic;

namespace SmartSweeper
{
	public class CController
	{
		private VertexStorage m_LinesToDraw = new VertexStorage();
		private VertexStorage m_BestPathToDraw = new VertexStorage();
		private VertexStorage m_AveragePathToDraw = new VertexStorage();
		private double m_SweeperScale;

		private Vector3[] sweeper = {new Vector3(-1, -1, 0),
                                         new Vector3(-1, 1, 0),
                                         new Vector3(-0.5, 1, 0),
                                         new Vector3(-0.5, -1, 0),

                                         new Vector3(0.5, -1, 0),
                                         new Vector3(1, -1, 0),
                                         new Vector3(1, 1, 0),
                                         new Vector3(0.5, 1, 0),

                                         new Vector3(-0.5, -0.5, 0),
                                         new Vector3(0.5, -0.5, 0),

                                         new Vector3(-0.5, 0.5, 0),
                                         new Vector3(-0.25, 0.5, 0),
                                         new Vector3(-0.25, 1.75, 0),
                                         new Vector3(0.25, 1.75, 0),
                                         new Vector3(0.25, 0.5, 0),
                                         new Vector3(0.5, 0.5, 0)};

		private double m_MineScale;

		private Vector3[] mine = {new Vector3(-1, -1, 0),
                                   new Vector3(-1, 1, 0),
                                   new Vector3(1, 1, 0),
                                   new Vector3(1, -1, 0)};

		//storage for the population of genomes
		private List<SGenome> m_vecThePopulation;

		//and the minesweepers
		private List<CMinesweeper> m_vecSweepers = new List<CMinesweeper>();

		//and the mines
		private List<Vector3> m_vecMines = new List<Vector3>();

		//pointer to the GA
		private CGenAlg m_pGA;

		private int m_NumWeightsInNN;

		//vertex buffer for the sweeper shape's vertices
		private List<Vector3> m_SweeperVB = new List<Vector3>();

		//vertex buffer for the mine shape's vertices
		private List<Vector3> m_MineVB = new List<Vector3>();

		//stores the average fitness per generation for use
		//in graphing.
		private List<double> m_vecAvFitness = new List<double>();

		//stores the best fitness per generation
		private List<double> m_vecBestFitness = new List<double>();

		private double dMutationRate;
		private double dCrossoverRate;

		//pens we use for the stats
		private Color m_BlackPen;

		private Color m_RedPen;
		private Color m_BluePen;
		private Color m_GreenPen;

		//handle to the application window
		private IImageByte m_hwndMain;

		//toggles the speed at which the simulation runs
		private bool m_bFastRender;

		//cycles per generation
		private int m_NumTicksPerGeneration;

		private int m_TicksThisGeneration;

		//generation counter
		private int m_iGenerations;

		private double m_BestFitnessYet;
		private int LastFitnessCount;

		//window dimensions
		private int cxClient, cyClient;

		private Stroke m_AverageLinesToDraw;
		private Stroke m_BestLinesToDraw;

		//this function plots a graph of the average and best fitnesses
		//over the course of a run
		private void PlotStats(Graphics2D renderer)
		{
			if (m_vecBestFitness.Count == 0)
			{
				return;
			}
			string s = "Best Fitness:       " + m_pGA.BestFitness.ToString();
			MatterHackers.Agg.UI.TextWidget InfoText = new MatterHackers.Agg.UI.TextWidget(s, 9);
			InfoText.OriginRelativeParent = new Vector2(5, 30);
			//InfoText.Render(renderer);

			s = "Average Fitness: " + m_pGA.AverageFitness.ToString();
			InfoText = new MatterHackers.Agg.UI.TextWidget(s, 9);
			InfoText.OriginRelativeParent = new Vector2(5, 45);
			//InfoText.Render(renderer);

			//render the graph
			double HSlice = (double)cxClient / (m_iGenerations + 1);
			double VSlice = (double)(cyClient / ((m_BestFitnessYet + 1) * 2));

			bool foundNewBest = false;
			if (m_vecBestFitness[m_vecBestFitness.Count - 1] > m_BestFitnessYet
				|| m_BestLinesToDraw == null
				|| LastFitnessCount != m_vecBestFitness.Count)
			{
				LastFitnessCount = m_vecBestFitness.Count;
				m_BestFitnessYet = m_vecBestFitness[m_vecBestFitness.Count - 1];

				foundNewBest = true;
			}

			if (foundNewBest)
			{
				//plot the graph for the best fitness
				double x = 0;
				m_BestPathToDraw.remove_all();
				m_BestPathToDraw.MoveTo(0, 0);
				for (int i = 0; i < m_vecBestFitness.Count; ++i)
				{
					m_BestPathToDraw.LineTo(x, VSlice * m_vecBestFitness[i]);
					x += HSlice;
				}

				m_BestLinesToDraw = new Stroke(m_BestPathToDraw);

				//plot the graph for the average fitness
				x = 0;

				m_AveragePathToDraw.remove_all();
				m_AveragePathToDraw.MoveTo(0, 0);
				for (int i = 0; i < m_vecAvFitness.Count; ++i)
				{
					m_AveragePathToDraw.LineTo(x, VSlice * m_vecAvFitness[i]);
					x += HSlice;
				}

				m_AverageLinesToDraw = new Stroke(m_AveragePathToDraw);
			}
			else
			{
				renderer.Render(m_BestLinesToDraw, m_BluePen);
				renderer.Render(m_AverageLinesToDraw, m_RedPen);
			}
		}

		public CController(IImageByte hwndMain, int iNumSweepers, int iNumMines, double _dMutationRate, double _dCrossoverRate, double dMaxPerturbation,
			int NumElite, int NumCopiesElite, int NumTicksPerGeneration)
		{
			Random Rand = new Random();

			m_MineScale = 1;
			m_SweeperScale = 3;

			m_pGA = null;
			m_bFastRender = (false);
			m_TicksThisGeneration = 0;
			m_NumTicksPerGeneration = NumTicksPerGeneration;
			m_hwndMain = hwndMain;
			m_iGenerations = (0);
			cxClient = (int)hwndMain.Width;
			cyClient = (int)hwndMain.Height;
			dMutationRate = _dMutationRate;
			dCrossoverRate = _dCrossoverRate;
			//let's create the mine sweepers
			for (int i = 0; i < iNumSweepers; ++i)
			{
				m_vecSweepers.Add(new CMinesweeper((int)hwndMain.Width, (int)hwndMain.Height, m_SweeperScale, .3));
			}

			//get the total number of weights used in the sweepers
			//NN so we can initialise the GA
			m_NumWeightsInNN = m_vecSweepers[0].GetNumberOfWeights();

			//initialize the Genetic Algorithm class
			m_pGA = new CGenAlg(iNumSweepers, dMutationRate, dCrossoverRate, m_NumWeightsInNN, dMaxPerturbation,
				NumElite, NumCopiesElite);

			//Get the weights from the GA and insert into the sweepers brains
			m_vecThePopulation = m_pGA.GetChromos();

			for (int i = 0; i < iNumSweepers; i++)
			{
				m_vecSweepers[i].PutWeights(m_vecThePopulation[i].Weights);
			}

			//initialize mines in random positions within the application window
			for (int i = 0; i < iNumMines; ++i)
			{
				m_vecMines.Add(new Vector3(Rand.NextDouble() * cxClient,
										   Rand.NextDouble() * cyClient, 0));
			}

			//create a pen for the graph drawing
			m_BlackPen = new Color(0, 0, 0, 255);
			m_BluePen = new Color(0, 0, 255);
			m_RedPen = new Color(255, 0, 0);
			m_GreenPen = new Color(0, 150, 0);

			//fill the vertex buffers
			for (int i = 0; i < sweeper.Length; ++i)
			{
				m_SweeperVB.Add(sweeper[i]);
			}

			for (int i = 0; i < mine.Length; ++i)
			{
				m_MineVB.Add(mine[i]);
			}
		}

		public void Render(Graphics2D renderer)
		{
			//render the stats
			string s = "Generation:          " + m_iGenerations.ToString();
			MatterHackers.Agg.UI.TextWidget GenerationText = new MatterHackers.Agg.UI.TextWidget(s, 9);
			GenerationText.OriginRelativeParent = new Vector2(150, 10);
			//GenerationText.Render(renderer);

			//do not render if running at accelerated speed
			if (!m_bFastRender)
			{
				//render the mines
				for (int i = 0; i < m_vecMines.Count; ++i)
				{
					//grab the vertices for the mine shape
					List<Vector3> mineVB = new List<Vector3>();
					foreach (Vector3 vector in m_MineVB)
					{
						mineVB.Add(vector);
					}

					WorldTransform(mineVB, m_vecMines[i]);

					//draw the mines
					m_LinesToDraw.remove_all();
					m_LinesToDraw.MoveTo(mineVB[0].X, mineVB[0].Y);
					for (int vert = 1; vert < mineVB.Count; ++vert)
					{
						m_LinesToDraw.LineTo(mineVB[vert].X, mineVB[vert].Y);
					}

					renderer.Render(m_LinesToDraw, m_BlackPen);
				}

				Color currentColor = m_RedPen;
				//render the sweepers
				for (int i = 0; i < m_vecSweepers.Count; i++)
				{
					//grab the sweeper vertices
					List<Vector3> sweeperVB = new List<Vector3>();
					foreach (Vector3 vector in m_SweeperVB)
					{
						sweeperVB.Add(vector);
					}

					//transform the vertex buffer
					m_vecSweepers[i].WorldTransform(sweeperVB);

					//draw the sweeper left track
					m_LinesToDraw.remove_all();
					m_LinesToDraw.MoveTo(sweeperVB[0].X, sweeperVB[0].Y);
					for (int vert = 1; vert < 4; ++vert)
					{
						m_LinesToDraw.LineTo(sweeperVB[vert].X, sweeperVB[vert].Y);
					}

					if (i == m_pGA.NumElite)
					{
						currentColor = m_BlackPen;
					}

					renderer.Render(m_LinesToDraw, currentColor);

					//draw the sweeper right track
					m_LinesToDraw.remove_all();
					m_LinesToDraw.MoveTo(sweeperVB[4].X, sweeperVB[4].Y);
					for (int vert = 5; vert < 8; ++vert)
					{
						m_LinesToDraw.LineTo(sweeperVB[vert].X, sweeperVB[vert].Y);
					}
					renderer.Render(m_LinesToDraw, currentColor);

					// draw the body
					m_LinesToDraw.remove_all();
					m_LinesToDraw.MoveTo(sweeperVB[8].X, sweeperVB[8].Y);
					m_LinesToDraw.LineTo(sweeperVB[9].X, sweeperVB[9].Y);
					m_LinesToDraw.MoveTo(sweeperVB[10].X, sweeperVB[10].Y);
					for (int vert = 11; vert < 16; ++vert)
					{
						m_LinesToDraw.LineTo(sweeperVB[vert].X, sweeperVB[vert].Y);
					}
					renderer.Render(m_LinesToDraw, currentColor);
				}
			}
			else
			{
				PlotStats(renderer);
			}
		}

		public void WorldTransform(List<Vector3> VBuffer, Vector3 vPos)
		{
			//create the world transformation matrix
			Matrix4X4 matTransform = Matrix4X4.Identity;

			//scale
			matTransform = Matrix4X4.CreateScale(m_MineScale, m_MineScale, 1);

			//translate
			matTransform *= Matrix4X4.CreateTranslation(vPos.X, vPos.Y, 0);

			//transform the ships vertices
			for (int i = 0; i < VBuffer.Count; i++)
			{
				Vector3 Temp = VBuffer[i];
				Temp = Vector3.Transform(Temp, matTransform);
				VBuffer[i] = Temp;
			}
		}

		public bool Update()
		{
			//run the sweepers through NumTicks amount of cycles. During
			//this loop each sweepers NN is constantly updated with the appropriate
			//information from its surroundings. The output from the NN is obtained
			//and the sweeper is moved. If it encounters a mine its fitness is
			//updated appropriately,
			int NumSweepers = m_vecSweepers.Count;
			if (m_TicksThisGeneration++ < m_NumTicksPerGeneration)
			{
				for (int i = 0; i < NumSweepers; ++i)
				{
					//update the NN and position
					if (!m_vecSweepers[i].Update(m_vecMines))
					{
						//error in processing the neural net
						//MessageBox(m_hwndMain, "Wrong amount of NN inputs!", "Error", MB_OK);

						return false;
					}

					//see if it's found a mine
					int GrabHit = m_vecSweepers[i].CheckForMine(m_vecMines, m_MineScale);

					if (GrabHit >= 0)
					{
						Random Rand = new Random();
						//we have discovered a mine so increase fitness
						m_vecSweepers[i].IncrementFitness();

						//mine found so replace the mine with another at a random
						//position
						m_vecMines[GrabHit] = new Vector3(Rand.NextDouble() * cxClient,
												  Rand.NextDouble() * cyClient, 0);
					}

					//update the chromos fitness score
					m_vecThePopulation[i].Fitness = m_vecSweepers[i].Fitness();
				}
			}
			//Another generation has been completed.
			//Time to run the GA and update the sweepers with their new NNs
			else
			{
				//update the stats to be used in our stat window
				m_vecAvFitness.Add(m_pGA.AverageFitness);
				m_vecBestFitness.Add(m_pGA.BestFitness);

				//increment the generation counter
				++m_iGenerations;

				//reset cycles
				m_TicksThisGeneration = 0;

				//run the GA to create a new population
				m_vecThePopulation = m_pGA.Epoch(m_vecThePopulation);

				//insert the new (hopefully)improved brains back into the sweepers
				//and reset their positions etc
				for (int i = 0; i < NumSweepers; ++i)
				{
					m_vecSweepers[i].PutWeights(m_vecThePopulation[i].Weights);

					m_vecSweepers[i].Reset();
				}
			}

			return true;
		}

		//accessor methods
		public bool FastRender()
		{
			return m_bFastRender;
		}

		public void FastRender(bool arg)
		{
			m_bFastRender = arg;
		}

		public void FastRenderToggle()
		{
			m_bFastRender = !m_bFastRender;
		}
	};
}