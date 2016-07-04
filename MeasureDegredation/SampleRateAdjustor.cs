using System;
using System.Collections.Generic;
using System.Linq;

using MathNet.Numerics;
using MathNet.Numerics.Transformations;

namespace MeasureDegredation
{
	public static class SampleRateAdjustor
	{
		public static IEnumerable<float[]> DownSample (IEnumerable<float[]> samplesEnumerator, int numChannels, int windowSize, Action<float[]> highSampleCallback)
		{
			var fft = new MathNet.Numerics.Transformations.ComplexFourierTransformation ();

			var determineVolumeSource = new Complex[windowSize];
			for (var sampleCtr = 0; sampleCtr < windowSize; sampleCtr++)
				determineVolumeSource[sampleCtr] = new Complex(1, 0);

			fft.TransformForward(determineVolumeSource);

			var determineVolumeDestination = new Complex[]
			{
				determineVolumeSource[0],
				Complex.FromModulusArgument(0,0),
				Complex.FromModulusArgument(0,0),
				Complex.FromModulusArgument(0,0)
			};

			fft.TransformBackward(determineVolumeDestination);

			var dcMultiplier = determineVolumeDestination[0].Modulus;

			var ratio = (1d / Convert.ToDouble(windowSize)) * 2d * Math.PI;
			for (var sampleCtr = 0; sampleCtr < windowSize; sampleCtr++)
			{
				var sampleCtrDouble = Convert.ToDouble(sampleCtr);
				determineVolumeSource[sampleCtr] = new Complex(Math.Cos(sampleCtrDouble * ratio), 0);
			}

			fft.TransformForward(determineVolumeSource);

			determineVolumeDestination = new Complex[]
			{
				Complex.FromModulusArgument(0,0),
				determineVolumeSource[1],
				Complex.FromModulusArgument(0,0),
				determineVolumeSource[determineVolumeSource.Length - 1],
			};

			fft.TransformBackward(determineVolumeDestination);

			var midMultiplier = determineVolumeDestination.Select(s => s.Modulus).Max();

			/*// This only works when windowsize is 8
			for (var sampleCtr = 0; sampleCtr < windowSize; sampleCtr++)
			{
				var abs = sampleCtr % 2 == 1 ? 1 : 0;
				var sign = sampleCtr % 4 > 1 ? 1 : -1;
				determineVolumeSource[sampleCtr] = new Complex(abs * sign, 0);
			}*/

			ratio = (1d / Convert.ToDouble(windowSize)) * 4d * Math.PI;
			for (var sampleCtr = 0; sampleCtr < windowSize; sampleCtr++)
			{
				var sampleCtrDouble = Convert.ToDouble(sampleCtr);
				determineVolumeSource[sampleCtr] = new Complex(Math.Cos(sampleCtrDouble * ratio), 0);
			}


			fft.TransformForward(determineVolumeSource);

			determineVolumeDestination = new Complex[]
			{
				Complex.FromModulusArgument(0,0),
				Complex.FromModulusArgument(0,0),
				determineVolumeSource[2],
				Complex.FromModulusArgument(0,0),
			};


			fft.TransformBackward(determineVolumeDestination);

			var highMultiplier = determineVolumeDestination.Select(s => s.Modulus).Max();

			var sampleQueue = new Queue<float[]>();
			for (var sampleCtr = 0; sampleCtr < windowSize / 4; sampleCtr++)
				sampleQueue.Enqueue(new float[numChannels]);

			var maxOriginalSample = 0.0;
			var maxLowFrequencySample = 0.0;

			using (var sampleEnumerator = samplesEnumerator.GetEnumerator())
			{
				int samplesFromSource;

				do
				{
					samplesFromSource = 0;

					while (sampleQueue.Count < windowSize)
					{
						if (sampleEnumerator.MoveNext())
						{
							sampleQueue.Enqueue(sampleEnumerator.Current.ToArray());
							samplesFromSource++;
						}
						else
							sampleQueue.Enqueue(new float[numChannels]);
					}

					var simplified = new float[][]
					{
						new float[numChannels],
						new float[numChannels]
					};

					var filtered = new float[windowSize / 2][];
					for (var sampleCtr = 0; sampleCtr < windowSize / 2; sampleCtr++)
						filtered[sampleCtr] = new float[numChannels];

					for (var channelCtr = 0; channelCtr < numChannels; channelCtr++)
					{
						maxOriginalSample = Math.Max(
							maxOriginalSample,
							sampleQueue.Select(s => Math.Abs(s[channelCtr])).Max());

						var samplesToTransform = sampleQueue.Select (s => new Complex (s [channelCtr], 0)).ToArray ();

						fft.TransformForward (samplesToTransform);

						var samplesToTransformBack = new Complex[]
						{
							samplesToTransform[0], 
							samplesToTransform[1],
							samplesToTransform[2],
							samplesToTransform[samplesToTransform.Length - 1]
						};

						samplesToTransformBack[0].Modulus /= dcMultiplier;
						samplesToTransformBack[1].Modulus /= midMultiplier;
						samplesToTransformBack[2].Modulus /= highMultiplier;
						samplesToTransformBack[3].Modulus /= midMultiplier;

						fft.TransformBackward(samplesToTransformBack);

						simplified[0][channelCtr] = Convert.ToSingle(samplesToTransformBack[1].Real);
						simplified[1][channelCtr] = Convert.ToSingle(samplesToTransformBack[2].Real);

						for (var filterCtr = 3; filterCtr < (samplesToTransform.Length - 1); filterCtr++)
							samplesToTransform[filterCtr] = Complex.FromModulusArgument(0, 0);

						fft.TransformBackward(samplesToTransform);
						for (var sampleCtr = 0; sampleCtr < windowSize / 2; sampleCtr++)
							filtered[sampleCtr][channelCtr] = Convert.ToSingle(samplesToTransform[sampleCtr + (windowSize / 4)].Real);
					}

					maxLowFrequencySample = Math.Max(
						maxLowFrequencySample,
						simplified.SelectMany(s => s).Select(Math.Abs).Max());

					foreach (var samples in simplified)
						yield return samples;

					foreach (var samples in filtered)
						highSampleCallback(samples);

					while (sampleQueue.Count > windowSize / 2)
						sampleQueue.Dequeue();

				} while (samplesFromSource > (windowSize / 4));
			}

			/*Console.WriteLine("Loudest sample in source: {0}", maxOriginalSample);
			Console.WriteLine("Loudest sample in low frequency: {0}", maxLowFrequencySample);
			//Console.WriteLine("Loudest sample in high frequency: {0}", maxHighFrequencySample);
			Console.WriteLine();*/
		}

		static IEnumerable<IEnumerable<float>> UpSample (WaveReader reader, int windowSize)
		{
			var fft = new MathNet.Numerics.Transformations.ComplexFourierTransformation ();

			var determineVolumeSource = new Complex[]
			{
				new Complex(1, 0),
				new Complex(1, 0),
				new Complex(1, 0),
				new Complex(1, 0),
			};

			fft.TransformForward(determineVolumeSource);

			var determineVolumeDestination = new Complex[windowSize];
			determineVolumeDestination[0] = determineVolumeSource[0];
			for (var sampleCtr = 1; sampleCtr < windowSize; sampleCtr++)
				determineVolumeDestination[sampleCtr] = Complex.FromModulusArgument(0,0);

			fft.TransformBackward(determineVolumeDestination);

			var multiplier = determineVolumeDestination[0].Real;

			var sampleQueue = new Queue<float[]>();
			for (var sampleCtr = 0; sampleCtr < 2; sampleCtr++)
				sampleQueue.Enqueue(new float[reader.NumChannels]);

			foreach (var samples in reader.ReadAllSamples_Float().Select(s => s.ToArray()))
			{
				sampleQueue.Enqueue(samples);

				if (sampleQueue.Count == 4)
				{
					var expanded = new float[windowSize / 2][];

					for (var ctr = 0; ctr < windowSize / 2; ctr++)
						expanded[ctr] = new float[reader.NumChannels];

					for (var channelCtr = 0; channelCtr < reader.NumChannels; channelCtr++)
					{
						var samplesToTransform = sampleQueue.Select(s => new Complex (s[channelCtr], 0)).ToArray();

						fft.TransformForward (samplesToTransform);

						var samplesToTransformBack = new Complex[windowSize];
						samplesToTransformBack[0] = samplesToTransform[0];
						samplesToTransformBack[1] = samplesToTransform[1];
						samplesToTransformBack[1].Modulus /= 2;
						samplesToTransformBack[samplesToTransformBack.Length - 1] = samplesToTransform[1];
						samplesToTransformBack[samplesToTransformBack.Length - 1].Modulus /= 2;
						samplesToTransformBack[samplesToTransformBack.Length - 1].Argument *= -1;

						for (var ctr = 2; ctr < samplesToTransformBack.Length - 1; ctr++)
							samplesToTransformBack[ctr] = Complex.FromModulusArgument(0, 0);

						fft.TransformBackward(samplesToTransformBack);

						for (var ctr = 0; ctr < windowSize / 2; ctr++)
							expanded[ctr][channelCtr] = Convert.ToSingle(samplesToTransformBack[ctr + windowSize / 4].Real / multiplier);
					}

					foreach (var simplified in expanded)
						yield return simplified;

					sampleQueue.Dequeue();
					sampleQueue.Dequeue();
				}
			}
		}
	}
}

