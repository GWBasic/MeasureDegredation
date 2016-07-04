using System;

namespace MeasureDegredation
{
	public class ComparisonAgregator
	{
		public ComparisonAgregator(string displayTitle, IComparable sortOrder)
		{
			this.displayTitle = displayTitle;
			this.sortOrder = sortOrder;
		}

		public string DisplayTitle
		{
			get { return this.displayTitle; }
		}
		private readonly string displayTitle;

		public IComparable SortOrder
		{
			get { return this.sortOrder; }
		}
		private readonly IComparable sortOrder;

		public double LargestError
		{
			get { return this.largestError; }
		}
		double largestError = float.MinValue;

		public double LargestEquilization
		{
			get { return this.largestEquilization; }
		}
		double largestEquilization = float.MinValue;

		double errorSum = 0;
		double equalizationSum = 0;
		long numSamples = 0;

		public void DetermineEquilization(float[] originalSamples, float[] compressedSamples)
		{
			for (var channelCtr = 0; channelCtr < originalSamples.Length; channelCtr++)
			{
				if (originalSamples[channelCtr] > 0.01)
				{
					var equalization = Math.Abs(compressedSamples[channelCtr]) / Math.Abs(originalSamples[channelCtr]);
					this.largestEquilization = Math.Max(this.largestEquilization, equalization);

					this.equalizationSum += equalization;
					this.numEquilizationSamples++;
				}
			}
		}

		public void CompareSamples(float[] originalSamples, float[] compressedSamples)
		{
			for (var channelCtr = 0; channelCtr < originalSamples.Length; channelCtr++)
			{
				var error = Math.Abs((compressedSamples[channelCtr] / this.Equilization) - originalSamples[channelCtr]);
				this.largestError = Math.Max(this.largestError, error);

				this.errorSum += error;
				this.numSamples++;
			}
		}

		public void WriteResults()
		{
			Console.WriteLine(displayTitle);
			Console.WriteLine("\tLargest error: {0}, if 16-bit: {1}", this.largestError, this.largestError * short.MaxValue);
			Console.WriteLine("\tWorst number of bits per sample: {0}", this.WorstBits);
			Console.WriteLine("\tWorst equilization: {0:0.00}", this.LargestEquilization * 100);
			Console.WriteLine("\tAverage error: {0}, if 16-bit: {1}", this.AverageError, this.AverageError * short.MaxValue);
			Console.WriteLine("\tAverage number of bits per sample: {0}", this.AverageBits);
			Console.WriteLine("\tEquilization: {0:0.00}", this.Equilization * 100);
		}

		public double WorstBits
		{
			get
			{
				var worstNumberOfUniqueValues = 2 * (1.0 / this.largestError);
				return Math.Log(worstNumberOfUniqueValues, 2);
			}
		}

		public double AverageError
		{
			get { return this.errorSum / Convert.ToDouble(this.numSamples); }
		}

		public double AverageBits
		{
			get
			{
				var averageNumberOfUniqueValues = 2 * (1.0 / this.AverageError);
				return Math.Log(averageNumberOfUniqueValues, 2);
			}
		}

		public double Equilization
		{
			get { return this.equalizationSum / Convert.ToDouble(this.numEquilizationSamples); }
		}

		private long numEquilizationSamples = 0;
	}
}

