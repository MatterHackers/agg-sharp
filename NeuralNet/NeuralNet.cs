using System;
using System.Collections.Generic;

namespace NeuralNet
{
	//-------------------------------------------------------------------
	//	define neuron struct
	//-------------------------------------------------------------------
	public class SNeuron
	{
		//the weights for each input
		private List<double> m_vecWeight = new List<double>();

		public int NumInputs
		{
			get
			{
				return m_vecWeight.Count;
			}
		}

		public List<double> Weight
		{
			get
			{
				return m_vecWeight;
			}
		}

		//ctor
		public SNeuron(int NumInputs)
		{
			Random Rand = new Random();
			//we need an additional weight for the bias hence the +1
			for (int i = 0; i < NumInputs + 1; ++i)
			{
				//set up the weights with an initial random value
				m_vecWeight.Add(Rand.NextDouble() - Rand.NextDouble());
			}
		}
	};

	//---------------------------------------------------------------------
	//	struct to hold a layer of neurons.
	//---------------------------------------------------------------------
	public class SNeuronLayer
	{
		//the layer of neurons
		private List<SNeuron> m_vecNeurons = new List<SNeuron>();

		public int NumNeurons
		{
			get
			{
				return m_vecNeurons.Count;
			}
		}

		public List<SNeuron> Neuron
		{
			get
			{
				return m_vecNeurons;
			}
		}

		public SNeuronLayer(int NumNeurons, int NumInputsPerNeuron)
		{
			for (int i = 0; i < NumNeurons; ++i)
			{
				m_vecNeurons.Add(new SNeuron(NumInputsPerNeuron));
			}
		}
	};

	//----------------------------------------------------------------------
	//	neural net class
	//----------------------------------------------------------------------

	public class CNeuralNet
	{
		private int m_NumInputs;
		private int m_NumOutputs;
		private int m_NumHiddenLayers;
		private int m_NeuronsPerHiddenLyr;
		private double m_Bias;
		private double m_ActivationResponse;

		public int NumOutputs
		{
			get
			{
				return m_NumOutputs;
			}
		}

		//storage for each layer of neurons including the output layer
		private List<SNeuronLayer> m_vecLayers = new List<SNeuronLayer>();

		public CNeuralNet(int NumInputs, int NumOutputs, int NumHiddenLayers, int NeuronsPerHiddenLyr, double Bias, double ActivationResponse)
		{
			m_NumInputs = NumInputs;
			m_NumOutputs = NumOutputs;
			m_NumHiddenLayers = NumHiddenLayers;
			m_NeuronsPerHiddenLyr = NeuronsPerHiddenLyr;
			m_Bias = Bias;
			m_ActivationResponse = ActivationResponse;

			CreateNet();
		}

		public void CreateNet()
		{
			//create the layers of the network
			if (m_NumHiddenLayers > 0)
			{
				//create first hidden layer
				m_vecLayers.Add(new SNeuronLayer(m_NeuronsPerHiddenLyr, m_NumInputs));

				for (int i = 0; i < m_NumHiddenLayers - 1; ++i)
				{
					m_vecLayers.Add(new SNeuronLayer(m_NeuronsPerHiddenLyr, m_NeuronsPerHiddenLyr));
				}

				//create output layer
				m_vecLayers.Add(new SNeuronLayer(m_NumOutputs, m_NeuronsPerHiddenLyr));
			}
			else
			{
				//create output layer
				m_vecLayers.Add(new SNeuronLayer(m_NumOutputs, m_NumInputs));
			}
		}

		//gets the weights from the NN
		public List<double> GetWeights()
		{
			//this will hold the weights
			List<double> weights = new List<double>();

			//for each layer
			for (int i = 0; i < m_NumHiddenLayers + 1; ++i)
			{
				//for each neuron
				for (int j = 0; j < m_vecLayers[i].NumNeurons; ++j)
				{
					//for each weight
					for (int k = 0; k < m_vecLayers[i].Neuron[j].NumInputs; ++k)
					{
						weights.Add(m_vecLayers[i].Neuron[j].Weight[k]);
					}
				}
			}

			return weights;
		}

		//returns total number of weights in net
		public int GetNumberOfWeights()
		{
			int weights = 0;

			//for each layer
			for (int i = 0; i < m_NumHiddenLayers + 1; ++i)
			{
				//for each neuron
				for (int j = 0; j < m_vecLayers[i].NumNeurons; ++j)
				{
					//for each weight
					for (int k = 0; k < m_vecLayers[i].Neuron[j].NumInputs; ++k)
					{
						weights++;
					}
				}
			}

			return weights;
		}

		//replaces the weights with new ones
		public void PutWeights(List<double> weights)
		{
			int cWeight = 0;

			//for each layer
			for (int i = 0; i < m_NumHiddenLayers + 1; ++i)
			{
				//for each neuron
				for (int j = 0; j < m_vecLayers[i].NumNeurons; ++j)
				{
					//for each weight
					for (int k = 0; k < m_vecLayers[i].Neuron[j].NumInputs; ++k)
					{
						m_vecLayers[i].Neuron[j].Weight[k] = weights[cWeight++];
					}
				}
			}

			return;
		}

		//calculates the outputs from a set of inputs
		public List<double> Update(List<double> inputs)
		{
			int cWeight = 0;

			//first check that we have the correct amount of inputs
			if (inputs.Count != m_NumInputs)
			{
				//just return an empty vector if incorrect.
				new List<double>();
			}

			//stores the resultant outputs from each layer
			List<double> outputs = new List<double>();

			//For each layer....
			for (int i = 0; i < m_NumHiddenLayers + 1; ++i)
			{
				if (i > 0)
				{
					inputs.Clear();
					foreach (double value in outputs)
					{
						inputs.Add(value);
					}
				}

				outputs.Clear();

				cWeight = 0;

				//for each neuron sum the (inputs * corresponding weights).Throw
				//the total at our sigmoid function to get the output.
				for (int j = 0; j < m_vecLayers[i].NumNeurons; ++j)
				{
					double netinput = 0;

					int NumInputs = m_vecLayers[i].Neuron[j].NumInputs;

					//for each weight
					for (int k = 0; k < NumInputs - 1; ++k)
					{
						//sum the weights x inputs
						netinput += m_vecLayers[i].Neuron[j].Weight[k] *
							inputs[cWeight++];
					}

					//add in the bias
					netinput += m_vecLayers[i].Neuron[j].Weight[NumInputs - 1] * m_Bias;

					//we can store the outputs from each layer as we generate them.
					//The combined activation is first filtered through the sigmoid
					//function
					outputs.Add(Sigmoid(netinput, m_ActivationResponse));

					cWeight = 0;
				}
			}

			return outputs;
		}

		//sigmoid response curve
		public double Sigmoid(double netinput, double response)
		{
			return (1 / (1 + Math.Exp(-netinput / response)));
		}
	};
}