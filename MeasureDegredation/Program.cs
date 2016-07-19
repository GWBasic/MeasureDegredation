using System;
using System.Collections.Generic;
using System.Linq;

using MathNet.Numerics;
using MathNet.Numerics.Transformations;

namespace MeasureDegredation
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			var originalFile = args[0];
			var compressedFile = args[1];
			var spreadsheet = args[2];


			/*var pck = new OfficeOpenXml.ExcelPackage(System.IO.File.OpenRead(spreadsheet));


			foreach (var worksheet in pck.Workbook.Worksheets)
				SpreadsheetWriter.CreateChart(worksheet);

			pck.File = new System.IO.FileInfo(spreadsheet);
			pck.Save();*/

			var compressedSamplesToSkip = 0L;
			if (args.Length > 3)
				long.TryParse(args[3], out compressedSamplesToSkip);

			Console.WriteLine("Comparing files:\n\tOriginal: {0}\n\tCompressed: {1}", originalFile, compressedFile);

			// Goal is to compare three bands:
			// High frequencies: above 5000hz
			// Sensitive frequencies: Between 2000-5000hz
			// Low frequencies: below 2000hz

			// At 48khz
			// Low band: 3khz, (1/4th sampling rate from mid band)
			// Mid band: 12khz, (1/4th sampling rate from high band)
			// High band: 48khz


			using (var originalReader = new WaveReader(originalFile))
			{
				using (var compressedReader = new WaveReader(compressedFile))
				{
					if (originalReader.SamplingRate != compressedReader.SamplingRate)
					{
						Console.WriteLine("Sampling rates do not match. Can not compare files with different sampling rates");
						return;
					}

					if (originalReader.NumChannels != compressedReader.NumChannels)
					{
						Console.WriteLine("Number of channels do not match. Can not compare files with different channels");
						return;
					}

					var comparisonsByName = new Dictionary<string, IEnumerable<ComparisonAgregator>>();

					comparisonsByName["samples"] = CompareBySamples (originalReader, compressedReader, compressedSamplesToSkip).ToArray();

					if (originalReader.SamplingRate == 48000)
					{
						comparisonsByName["banded"] = CompareByFrequencyBands (originalReader, compressedReader, compressedSamplesToSkip).ToArray();
					}
					else
					{
						Console.WriteLine("Banded comparison requires a 48000 sampling rate");
					}

					comparisonsByName["frequencies"] = CompareByFrequencies (originalReader, compressedReader, originalReader.SamplingRate, originalReader.NumChannels, compressedSamplesToSkip).ToArray();

					if (null != spreadsheet)
						SpreadsheetWriter.WriteSpreadsheet(spreadsheet, comparisonsByName);
				}
			}
		}

		static IEnumerable<ComparisonAgregator> CompareBySamples (WaveReader originalReader, WaveReader compressedReader, long compressedSamplesToSkip)
		{
			var comparisonAgregator = new ComparisonAgregator ("All samples", 0);

			EnumerateBySamples (originalReader, compressedReader, compressedSamplesToSkip, comparisonAgregator.DetermineEquilization);
			EnumerateBySamples (originalReader, compressedReader, compressedSamplesToSkip, comparisonAgregator.CompareSamples);

			comparisonAgregator.WriteResults ();
			yield return comparisonAgregator;
		}

		static void EnumerateBySamples (WaveReader originalReader, WaveReader compressedReader, long compressedSamplesToSkip, Action<float[], float[]> compareMethod)
		{
			originalReader.Seek(0);
			compressedReader.Seek(compressedSamplesToSkip);

			using (var compressedSamplesItr = compressedReader.ReadAllSamples_Float ().GetEnumerator ())
			{
				foreach (var originalSamples in originalReader.ReadAllSamples_Float ())
				{
					if (compressedSamplesItr.MoveNext ())
					{
						var compressedSamples = compressedSamplesItr.Current;
						compareMethod (originalSamples, compressedSamples);
					}
					else
					{
						Console.WriteLine ("Compressed file is shorter");
						break;
					}
				}
				if (compressedSamplesItr.MoveNext ())
				{
					Console.WriteLine ("Compressed file is longer");
				}
			}
		}

		static IEnumerable<ComparisonAgregator> CompareByFrequencyBands (WaveReader originalReader, WaveReader compressedReader, long compressedSamplesToSkip)
		{
			var highComparisonAgregator = new ComparisonAgregator ("High Frequencies: > 6khz", 0);
			var midComparisonAgregator = new ComparisonAgregator ("Mid Frequencies: < 6khz, > 3khz", 1);
			var lowComparisonAgregator = new ComparisonAgregator ("Low Frequencies: < 3khz", 2);

			EnumerateByFrequencyBands (
				originalReader,
				compressedReader,
				compressedSamplesToSkip,
				highComparisonAgregator.DetermineEquilization,
				midComparisonAgregator.DetermineEquilization,
				lowComparisonAgregator.DetermineEquilization);

			EnumerateByFrequencyBands (
				originalReader,
				compressedReader,
				compressedSamplesToSkip,
				highComparisonAgregator.CompareSamples,
				midComparisonAgregator.CompareSamples,
				lowComparisonAgregator.CompareSamples);

			lowComparisonAgregator.WriteResults ();
			midComparisonAgregator.WriteResults ();
			highComparisonAgregator.WriteResults ();

			yield return lowComparisonAgregator;
			yield return midComparisonAgregator;
			yield return highComparisonAgregator;
		}

		static void EnumerateByFrequencyBands (
			WaveReader originalReader,
			WaveReader compressedReader,
			long compressedSamplesToSkip,
			Action<float[], float[]> highComparisonMethod,
			Action<float[], float[]> midComparisonMethod,
			Action<float[], float[]> lowComparisonMethod)
		{
			originalReader.Seek(0);
			compressedReader.Seek(compressedSamplesToSkip);

			var originalHighSamplesQueue = new Queue<float[]> ();
			var compressedHighSamplesQueue = new Queue<float[]> ();

			var originalMidSamples = SampleRateAdjustor.DownSample (originalReader.ReadAllSamples_Float (), originalReader.NumChannels, 16, originalHighSamplesQueue.Enqueue);
			var compressedMidSamples = SampleRateAdjustor.DownSample (compressedReader.ReadAllSamples_Float (), compressedReader.NumChannels, 16, compressedHighSamplesQueue.Enqueue);

			var originalMidSamplesQueue = new Queue<float[]> ();
			var compressedMidSamplesQueue = new Queue<float[]> ();

			var originalLowSamples = SampleRateAdjustor.DownSample (originalMidSamples, originalReader.NumChannels, 16, originalMidSamplesQueue.Enqueue);
			var compressedLowSamples = SampleRateAdjustor.DownSample (compressedMidSamples, compressedReader.NumChannels, 16, compressedMidSamplesQueue.Enqueue);

			using (var compressedSamplesItr = compressedLowSamples.GetEnumerator ())
			{
				foreach (var originalSamples in originalLowSamples)
				{
					if (compressedSamplesItr.MoveNext ())
					{
						var compressedSamples = compressedSamplesItr.Current;
						lowComparisonMethod (originalSamples.ToArray (), compressedSamples.ToArray ());

						while (originalHighSamplesQueue.Count > 0 && compressedHighSamplesQueue.Count > 0)
							highComparisonMethod (originalHighSamplesQueue.Dequeue (), compressedHighSamplesQueue.Dequeue ());

						while (originalMidSamplesQueue.Count > 0 && compressedMidSamplesQueue.Count > 0)
							midComparisonMethod (originalMidSamplesQueue.Dequeue (), compressedMidSamplesQueue.Dequeue ());
					}
					else
					{
						Console.WriteLine ("Compressed file is shorter");
						break;
					}
				}

				if (compressedSamplesItr.MoveNext ())
				{
					Console.WriteLine ("Compressed file is longer");
				}
			}
		}

		static IEnumerable<ComparisonAgregator> CompareByFrequencies (WaveReader originalReader, WaveReader compressedReader, float samplingRate, int numChannels, long compressedSamplesToSkip)
		{
			var minWindowSize = samplingRate / 16;
			int windowSize;
			var windowSizeCtr = 0;

			do
			{
				windowSizeCtr++;
				windowSize = Convert.ToInt32(Math.Pow(2, windowSizeCtr));
			} while (windowSize <= minWindowSize);

			var samplesPerCheck = minWindowSize / 2;

			var comparisonAgregators = new Dictionary<int, ComparisonAgregator>();
			for (var ctr = 1; ctr < windowSize / 2; ctr++)
			{
				var lowerFrequency = (ctr - 1) * samplingRate / windowSize;
				var frequency = ctr * samplingRate / windowSize;

				if (frequency >= 16)
					comparisonAgregators[ctr - 1] = new ComparisonAgregator(lowerFrequency.ToString() + "hz", lowerFrequency);
			}

			comparisonAgregators[windowSize / 2] = new ComparisonAgregator((samplingRate / 2).ToString() + "hz", samplingRate / 2);

			var equilizationMethods = new Dictionary<int, Action<float[], float[]>>();
			foreach (var kvp in comparisonAgregators)
				equilizationMethods[kvp.Key] = kvp.Value.DetermineEquilization;
			EnumerateByFrequencies (originalReader, compressedReader, numChannels, windowSize, samplesPerCheck, equilizationMethods, compressedSamplesToSkip);

			var comparisonMethods = new Dictionary<int, Action<float[], float[]>>();
			foreach (var kvp in comparisonAgregators)
				comparisonMethods[kvp.Key] = kvp.Value.CompareSamples;
			EnumerateByFrequencies (originalReader, compressedReader, numChannels, windowSize, samplesPerCheck, comparisonMethods, compressedSamplesToSkip);

			foreach (var compressionAgregator in comparisonAgregators.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value))
				compressionAgregator.WriteResults();

			return comparisonAgregators.Values;
		}

		static void EnumerateByFrequencies (WaveReader originalReader, WaveReader compressedReader, int numChannels, int windowSize, float samplesPerCheck, Dictionary<int, Action<float[], float[]>> comparisonMethods, long compressedSamplesToSkip)
		{
			originalReader.Seek(0);
			compressedReader.Seek(compressedSamplesToSkip);

			var fft = new ComplexFourierTransformation();

			var originalQueue = new Queue<float[]> ();
			var compressedQueue = new Queue<float[]> ();

			using (var compressedSamplesItr = compressedReader.ReadAllSamples_Float ().GetEnumerator ())
			{
				foreach (var originalSamples in originalReader.ReadAllSamples_Float ())
				{
					if (compressedSamplesItr.MoveNext ())
					{
						originalQueue.Enqueue (originalSamples);
						compressedQueue.Enqueue (compressedSamplesItr.Current);

						if (originalQueue.Count == windowSize)
						{
							for (var channelCtr = 0; channelCtr < numChannels; channelCtr++)
							{
								var originalSamplesToTransform = originalQueue.Select (s => new Complex (s [channelCtr], 0)).ToArray ();
								var compressedSamplesToTransform = compressedQueue.Select (s => new Complex (s [channelCtr], 0)).ToArray ();

								fft.TransformForward (originalSamplesToTransform);
								fft.TransformForward (compressedSamplesToTransform);

								foreach (var kvp in comparisonMethods)
								{
									var fftIndex = kvp.Key;
									var comparisonMethod = kvp.Value;

									comparisonMethod(
										new float[] { Convert.ToSingle (originalSamplesToTransform [fftIndex].Modulus) },
										new float[] { Convert.ToSingle (compressedSamplesToTransform [fftIndex].Modulus) });
								}
							}

							for (var ctr = 0; ctr < samplesPerCheck; ctr++)
							{
								originalQueue.Dequeue ();
								compressedQueue.Dequeue ();
							}
						}
					}
					else
					{
						Console.WriteLine ("Compressed file is shorter");
						break;
					}
				}

				if (compressedSamplesItr.MoveNext ())
				{
					Console.WriteLine ("Compressed file is longer");
				}
			}
		}
	}
}
