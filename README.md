# MeasureDegredation
Compares two wave files, measures the degradation in the second wave file from compression

This program lets you quantify the degredation caused when compressing audio to mp3, aac, ect. It also
lets you better understnd how a 24-bit wav is quantized to 16-bit when noise-shaping is used.

Results are written to the console, and to an excel spreadsheet.

This program has two limitations:
- It can only read 48000 khz wave files
- It can only read floating-point wav files

For best results:
1: Convert your source to 48khz, floating point: sox (source) -e floating-point original.wav rate -v 48k
2: Compress / quantify original.wav using whatever program / algorithm you want to test
3: Convert the compressed file back to floating-point wav. (If sox supports the format): sox (compressed) -e floating-point compressed.wav
4: Compare

Arguments:
1: Original wav file, (48khz, 32-bit floating-point)
2: Compressed file, extracted back to 48khz, 32-bit floating-point)
3: Spreadsheet to generate (will be .xlsx)
4: Padding length in samples. (aac is typically 2112 samples, mp3 is 572 samples, opus and 16-bit wav are 0.)

Note, when comparing, the samples must align exactly. Use tools like afinfo or ffmpeg to find out if the compresser added padding

Results:

A spreadsheet with 3 pages will be generated:
1: First page just compares samples. It tries to give a rough estimation of how many bits / sample are preserved. Note that using things like noise shaping will make this appear less than the true number of bits per sample
2: Second page is a similar comparison, but the signal is broken into three frequency bands. (This is the part that only supports 48khz)
3: Third page runs a fourier transform many times and compares frequency levels. This is most accurate to what the ear hears. (The ear does not hear phase)

Dependancies:
- MathNet.Iridium.dll: Performs fourier transforms
- EPPlus.dll: Writes excel spreadsheets
