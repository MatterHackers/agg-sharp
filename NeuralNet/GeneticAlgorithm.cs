//------------------------------------------------------------------------
//
//	Name: CGenAlg.h
//
//  Author: Mat Buckland 2002
//
//  Desc: Genetic algorithm class.This is based for manipulating List(s)
//  of *real* numbers. Used to adjust the weights in a feedforward neural
//  network.
//
//------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace NeuralNet
{
	//-----------------------------------------------------------------------
	//
	//	create a structure to hold each genome
	//-----------------------------------------------------------------------
	public class SGenome : IComparable<SGenome>
	{
		private List<double> vecWeights = new List<double>();

		private double dFitness = 0;

		public double Fitness
		{
			get
			{
				return dFitness;
			}

			set
			{
				dFitness = value;
			}
		}

		public List<double> Weights
		{
			get
			{
				return vecWeights;
			}
		}

		public SGenome()
		{
		}

		public SGenome(List<double> w, double fitness)
		{
			vecWeights.Clear();
			foreach (double weight in w)
			{
				vecWeights.Add(weight);
			}
			dFitness = fitness;
		}

		public int CompareTo(SGenome other)
		{
			// The temperature comparison depends on the comparison of the
			// the underlying Double values. Because the CompareTo method is
			// strongly typed, it is not necessary to test for the correct
			// object type.
			return Fitness.CompareTo(other.Fitness);
		}

		public static bool operator <(SGenome LeftHandSide, SGenome RightHandSide)
		{
			return (LeftHandSide.Fitness < RightHandSide.Fitness);
		}

		public static bool operator >(SGenome LeftHandSide, SGenome RightHandSide)
		{
			return (LeftHandSide.Fitness > RightHandSide.Fitness);
		}
	};

	//-----------------------------------------------------------------------
	//
	//	the genetic algorithm class
	//-----------------------------------------------------------------------
	public class CGenAlg
	{
		private double m_dMaxPerturbation;
		private int m_NumElite;
		private int m_NumCopiesElite;

		public int NumElite
		{
			get
			{
				return m_NumElite;
			}
		}

		private Random m_Rand = new Random();

		//this holds the entire population of chromosomes
		private List<SGenome> m_vecPop = new List<SGenome>();

		//size of population
		private int m_iPopSize;

		//amount of weights per chromo
		private int m_iChromoLength;

		//total fitness of population
		private double m_dTotalFitness;

		//best fitness this population
		private double m_dBestFitness;

		//average fitness
		private double m_dAverageFitness;

		//worst
		private double m_dWorstFitness;

		//keeps track of the best genome
		private int m_iFittestGenome;

		//probability that a chromosones bits will mutate.
		//Try figures around 0.05 to 0.3 ish
		private double m_dMutationRate;

		//probability of chromosones crossing over bits
		//0.7 is pretty good
		private double m_dCrossoverRate;

		//generation counter
		private int m_cGeneration;

		public int NumGenerations
		{
			get
			{
				return m_cGeneration;
			}
		}

		private void Crossover(List<double> mum, List<double> dad, List<double> baby1, List<double> baby2)
		{
			//just return parents as offspring dependent on the rate
			//or if parents are the same
			if ((m_Rand.NextDouble() > m_dCrossoverRate) || (mum == dad))
			{
				int NumDoubles = mum.Count;
				for (int i = 0; i < NumDoubles; ++i)
				{
					baby1.Add(mum[i]);
					baby2.Add(dad[i]);
				}

				return;
			}

			//determine a crossover point
			int CrossoverIndex = m_Rand.Next(m_iChromoLength - 1);

			//create the offspring
			for (int i = 0; i < CrossoverIndex; ++i)
			{
				baby1.Add(mum[i]);
				baby2.Add(dad[i]);
			}

			for (int i = CrossoverIndex; i < mum.Count; ++i)
			{
				baby1.Add(dad[i]);
				baby2.Add(mum[i]);
			}

			return;
		}

		private void Mutate(List<double> chromo)
		{
			//traverse the chromosome and mutate each weight dependent
			//on the mutation rate
			for (int i = 0; i < chromo.Count; ++i)
			{
				//do we perturb this weight?
				if (m_Rand.NextDouble() < m_dMutationRate)
				{
					//add or subtract a small value to the weight
					chromo[i] += ((m_Rand.NextDouble() - m_Rand.NextDouble()) * m_dMaxPerturbation);
				}
			}
		}

		private SGenome GetChromoRoulette()
		{
			//generate a random number between 0 & total fitness count
			double Slice = (double)(m_Rand.NextDouble() * m_dTotalFitness);

			//this will be set to the chosen chromosome
			SGenome TheChosenOne = m_vecPop[0];

			//go through the chromosones adding up the fitness so far
			double FitnessSoFar = 0;

			for (int i = 0; i < m_iPopSize; ++i)
			{
				FitnessSoFar += m_vecPop[i].Fitness;

				//if the fitness so far > random number return the chromo at
				//this point
				if (FitnessSoFar >= Slice)
				{
					TheChosenOne = m_vecPop[i];

					break;
				}
			}

			return TheChosenOne;
		}

		//use to introduce elitism
		private void GrabNBest(int NBest, int NumCopies, List<SGenome> vecPop)
		{
			//add the required amount of copies of the n most fittest
			//to the supplied vector
			while (NBest-- != 0)
			{
				for (int i = 0; i < NumCopies; ++i)
				{
					vecPop.Add(m_vecPop[(m_iPopSize - 1) - NBest]);
				}
			}
		}

		private void CalculateBestWorstAvTot()
		{
			m_dTotalFitness = 0;

			double HighestSoFar = 0;
			double LowestSoFar = 9999999;

			for (int i = 0; i < m_iPopSize; ++i)
			{
				//update fittest if necessary
				if (m_vecPop[i].Fitness > HighestSoFar)
				{
					HighestSoFar = m_vecPop[i].Fitness;

					m_iFittestGenome = i;

					m_dBestFitness = HighestSoFar;
				}

				//update worst if necessary
				if (m_vecPop[i].Fitness < LowestSoFar)
				{
					LowestSoFar = m_vecPop[i].Fitness;

					m_dWorstFitness = LowestSoFar;
				}

				m_dTotalFitness += m_vecPop[i].Fitness;
			}//next chromo

			m_dAverageFitness = m_dTotalFitness / m_iPopSize;
		}

		private void Reset()
		{
			m_dTotalFitness = 0;
			m_dBestFitness = 0;
			m_dWorstFitness = 9999999;
			m_dAverageFitness = 0;
		}

		public CGenAlg(int popsize,
					double MutRat,
					double CrossRat,
					int numweights,
			double dMaxPerturbation,
			int NumElite, int NumCopiesElite)
		{
			m_iPopSize = popsize;
			m_dMutationRate = MutRat;
			m_dCrossoverRate = CrossRat;
			m_iChromoLength = numweights;
			m_dTotalFitness = 0;
			m_cGeneration = 0;
			m_iFittestGenome = 0;
			m_dBestFitness = 0;
			m_dWorstFitness = 99999999;
			m_dAverageFitness = 0;

			m_dMaxPerturbation = dMaxPerturbation;
			m_NumElite = NumElite;
			m_NumCopiesElite = NumCopiesElite;

			//initialise population with chromosomes consisting of random
			//weights and all fitnesses set to zero
			for (int i = 0; i < m_iPopSize; ++i)
			{
				m_vecPop.Add(new SGenome());

				for (int j = 0; j < m_iChromoLength; ++j)
				{
					m_vecPop[i].Weights.Add(m_Rand.NextDouble() - m_Rand.NextDouble());
				}
			}
		}

		//this runs the GA for one generation.
		public List<SGenome> Epoch(List<SGenome> old_pop)
		{
			//assign the given population to the classes population
			m_vecPop = old_pop;

			//reset the appropriate variables
			Reset();

			//sort the population (for scaling and elitism)
			m_vecPop.Sort();
			//sort(m_vecPop.begin(), m_vecPop.end());

			//calculate best, worst, average and total fitness
			CalculateBestWorstAvTot();

			//create a temporary vector to store new chromosones
			List<SGenome> vecNewPop = new List<SGenome>();

			//Now to add a little elitism we shall add in some copies of the
			//fittest genomes. Make sure we add an EVEN number or the roulette
			//wheel sampling will crash
			if ((m_NumCopiesElite * m_NumElite % 2) == 0)
			{
				GrabNBest(m_NumElite, m_NumCopiesElite, vecNewPop);
			}

			//now we enter the GA loop

			//repeat until a new population is generated
			while (vecNewPop.Count < m_iPopSize)
			{
				//grab two chromosones
				SGenome mum = GetChromoRoulette();
				SGenome dad = GetChromoRoulette();

				//create some offspring via crossover
				List<double> baby1 = new List<double>();
				List<double> baby2 = new List<double>();

				Crossover(mum.Weights, dad.Weights, baby1, baby2);

				//now we mutate
				Mutate(baby1);
				Mutate(baby2);

				//now copy into vecNewPop population
				vecNewPop.Add(new SGenome(baby1, 0));
				vecNewPop.Add(new SGenome(baby2, 0));
			}

			//finished so assign new pop back into m_vecPop
			m_vecPop = vecNewPop;

			return m_vecPop;
		}

		//-------------------accessor methods
		public List<SGenome> GetChromos()
		{
			return m_vecPop;
		}

		public double AverageFitness
		{
			get
			{
				return m_dTotalFitness / m_iPopSize;
			}
		}

		public double BestFitness
		{
			get
			{
				return m_dBestFitness;
			}
		}
	};
}