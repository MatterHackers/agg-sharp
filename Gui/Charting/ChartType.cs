/*
Copyright (c) 2023, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

namespace Gui.Charting
{
    /// <summary>
    /// Defines the type of chart to create.
    /// </summary>
    public enum ChartType
    {
        /// <summary>
        /// A line chart or line graph is a type of chart which displays information 
        /// as a series of data points called 'markers' connected by straight line segments.
        /// </summary>
        Line,

        /// <summary>
        /// A bar chart or bar graph is a chart or graph that presents categorical data with rectangular bars 
        /// with heights or lengths proportional to the values that they represent.
        /// </summary>
        Bar,

        /// <summary>
        /// A pie chart (or a circle chart) is a circular statistical graphic, 
        /// which is divided into slices to illustrate numerical proportion.
        /// </summary>
        Pie,

        /// <summary>
        /// A doughnut chart (also spelled donut) is a variant of the pie chart, 
        /// with a blank center allowing for additional information about the data as a whole to be included.
        /// </summary>
        Doughnut,

        /// <summary>
        /// Radar or spider or polar chart is a two-dimensional chart type designed to plot one or more series of values over multiple common quantitative variables.
        /// </summary>
        Radar,

        /// <summary>
        /// A polar area diagram, also known as polar area chart or star plot or occasionaly Kiviat diagram,
        /// displays of multivariate, quantitative data.
        /// </summary>
        PolarArea,

        /// <summary>
        /// A scatter plot, scatterplot, or scattergraph is a type of plot or mathematical diagram 
        /// using Cartesian coordinates to display values for typically two variables for a set of data.
        /// </summary>
        Scatter,

        /// <summary>
        /// An area chart or area graph represents quantitatively the change of one or more quantities over time. 
        /// It is similar to a line graph but with the area between the axis and line filled in.
        /// </summary>
        Area,

        /// <summary>
        /// A bubble chart is a type of chart that displays three dimensions of data.
        /// </summary>
        Bubble
    }
}