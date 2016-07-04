using System;
using System.Collections.Generic;
using System.Text;

namespace MeasureDegredation
{
	internal class WaveReaderWriterException : Exception
	{
		internal WaveReaderWriterException(string message) : base(message) { }
	}

	internal class NotAWaveFileException : WaveReaderWriterException
	{
		internal NotAWaveFileException() : base("Not a wave file") { }
	}

	internal class UnsupportedResolutionException : WaveReaderWriterException
	{
		internal UnsupportedResolutionException() : base("Unsupported resolution") { }
	}

	internal class CompressedWavesNotSupported : WaveReaderWriterException
	{
		internal CompressedWavesNotSupported() : base("Compressed wave files not supported") { }
	}
}
