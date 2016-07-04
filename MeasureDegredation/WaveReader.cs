using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace MeasureDegredation
{
	/// <summary>
	/// Summary description for WaveReader.
	/// </summary>
	public class WaveReader : IDisposable
	{
		public WaveReader(string filename)
		{
			this.fileStream = File.OpenRead(filename);

			try
			{
				var header = new byte[4];
				fileStream.Read(header, 0, header.Length);
				var riffString = Encoding.ASCII.GetString(header);

				if ("RIFF" != riffString)
					throw new NotAWaveFileException();

				fileStream.Read(header, 0, header.Length);
				var fileSize = BitConverter.ToInt32(header, 0);

				if (fileSize != fileStream.Length - 8)
					throw new NotAWaveFileException();

				header = new byte[8];
				fileStream.Read(header, 0, header.Length);
				var waveString = Encoding.ASCII.GetString(header);

				if ("WAVEfmt " != waveString)
					throw new NotAWaveFileException();

				header = new byte[4];
				fileStream.Read(header, 0, header.Length);
				var headerSize = BitConverter.ToInt32(header, 0);

				header = new byte[headerSize];
				fileStream.Read(header, 0, header.Length);

				var compressionType = BitConverter.ToUInt16(header, 0);

				if (3 != compressionType)
					throw new CompressedWavesNotSupported();

				this.numChannels = BitConverter.ToUInt16(header, 2);

				this.resolution = BitConverter.ToUInt16(header, 14);

				if (!resolution.In((uint)8, (uint)16, (uint)32))
					throw new UnsupportedResolutionException();

				this.sizeofSample = Convert.ToInt32(resolution) / 8;

				this.samplingRate = Convert.ToInt32(BitConverter.ToUInt32(header, 4));

				var length = 0L;
				header = new byte[4];
				string sectionString;

				do
				{
					this.fileStream.Seek(length, SeekOrigin.Current);

					if (this.fileStream.Position == this.fileStream.Length)
						throw new NotAWaveFileException();

					fileStream.Read(header, 0, header.Length);
					sectionString = Encoding.ASCII.GetString(header);
					fileStream.Read(header, 0, header.Length);
					length = BitConverter.ToUInt32(header, 0);
				} while ("data" != sectionString);

				this.length = length;
				this.startSample = this.fileStream.Position;
			}
			catch
			{
				fileStream.Close();
				throw;
			}
		}

		/// <summary>
		/// The file stream that the wave is being read from
		/// </summary>
		private FileStream fileStream;

		/// <summary>
		/// The number of samples
		/// </summary>
		public long LengthSamples
		{
			get { return (this.length / this.numChannels / (this.sizeofSample)); }
		}
		private long length;

		/// <summary>
		/// The length in seconds
		/// </summary>
		public float LengthSeconds
		{
			get { return Convert.ToSingle(LengthSamples) / Convert.ToSingle(this.samplingRate); }
		}

		/// <summary>
		/// The sampling reate
		/// </summary>
		public int SamplingRate
		{
			get { return this.samplingRate; }
		}
		private int samplingRate;

		/// <summary>
		/// The number of channels
		/// </summary>
		public ushort NumChannels
		{
			get { return this.numChannels; }
		}
		private ushort numChannels;

		/// <summary>
		/// The resolution, in number of bits per sample
		/// </summary>
		public uint Resolution
		{
			get { return this.resolution; }
		}
		private readonly uint resolution;

		/// <summary>
		/// The number of bytes in a sample
		/// </summary>
		public int SizeofSample
		{
			get { return this.sizeofSample; }
		}
		private readonly int sizeofSample;

		/// <summary>
		/// The current sample that is about to be read
		/// </summary>
		public long CurrentSample
		{
			get { return ((fileStream.Position - this.startSample) / this.numChannels / (this.sizeofSample)); }
		}

		/// <summary>
		/// The file position where samples start
		/// </summary>
		private long startSample;

		/// <summary>
		/// Reads the next sample from the wave file.  Note that the array is the size
		/// of the number of channels
		/// </summary>
		/// <returns></returns>
		private IEnumerable<float> NextSamples_Float(int sampleCtr, byte[] buffer)
		{
			for (var channelCtr = 0; channelCtr < this.numChannels; channelCtr++)
				yield return BitConverter.ToSingle (buffer, ((sampleCtr * this.numChannels) + channelCtr) * this.sizeofSample);
		}

		/// <summary>
		/// Use a 256 megabyte buffer size
		/// </summary>
		const uint BUFFER_SIZE = 1024 * 1024 * 256;

		public IEnumerable<float[]> ReadAllSamples_Float()
		{
			//TODO: Support integer formats
			if (resolution != 32)
				throw new UnsupportedResolutionException();

			// Round down the read-buffer via interger math. It must be a multiple of the number of channels and the bytes per sample
			var bufferSize = (BUFFER_SIZE / this.numChannels / this.sizeofSample) * (this.numChannels * this.sizeofSample);

			var buffer = new byte[bufferSize];

			var readSamples = this.CurrentSample;
			var lengthSamples = this.LengthSamples;

			while (readSamples < lengthSamples)
			{
				var samplesInBuffer = this.fileStream.Read(buffer, 0, buffer.Length) / this.numChannels / this.sizeofSample;

				if (samplesInBuffer > (lengthSamples - readSamples))
					samplesInBuffer = Convert.ToInt32(lengthSamples - readSamples);

				for (var sampleCtr = 0; sampleCtr < samplesInBuffer; sampleCtr++)
					yield return this.NextSamples_Float (sampleCtr, buffer).ToArray();

				readSamples += samplesInBuffer;
			}
		}

		public void Seek(long position)
		{
			switch (Resolution)
			{
				case (8):
					fileStream.Seek(this.startSample + (position * NumChannels), SeekOrigin.Begin);
					break;

				case (16):
					fileStream.Seek(this.startSample + (position * NumChannels * 2), SeekOrigin.Begin);
					break;

				case (32):
					fileStream.Seek(this.startSample + (position * NumChannels * 4), SeekOrigin.Begin);
					break;

				default:
					throw new NotImplementedException();
			}
		}

		#region IDisposable Members

		public void Dispose()
		{
			fileStream.Close();
			//Console.WriteLine ("Minimum sample: {0}\nMaximum Sample: {1}", this.minSample, this.maxSample);
		}

		#endregion
	}
}