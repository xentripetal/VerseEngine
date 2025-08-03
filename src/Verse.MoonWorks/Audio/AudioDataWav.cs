using System.Runtime.InteropServices;
using Verse.MoonWorks.Storage;

namespace Verse.MoonWorks.Audio;

public static class AudioDataWav
{
	private const int MAGIC_RIFF = 0x46464952;
	private const int MAGIC_WAVE = 0x45564157;
	private const int MAGIC_FMT = 0x20746d66;
	private const int MAGIC_DATA = 0x61746164;

	private static Format ParseFormat(ref ByteSpanReader reader)
	{
		// RIFF Signature
		if (reader.Read<int>() != MAGIC_RIFF) {
			throw new NotSupportedException("Specified stream is not a wave file.");
		}

		reader.Read<uint>(); // Riff chunk size

		// WAVE Header
		if (reader.Read<int>() != MAGIC_WAVE) {
			throw new NotSupportedException("Specified stream is not a wave file.");
		}

		// Skip over non-format chunks
		while (reader.Remaining >= 4 && reader.Read<int>() != MAGIC_FMT) {
			var chunkSize = reader.Read<uint>();
			reader.Advance(chunkSize);
		}

		if (reader.Remaining < 4) {
			throw new NotSupportedException("Specified stream is not a wave file.");
		}

		var format_chunk_size = reader.Read<uint>();

		// WaveFormatEx data
		var wFormatTag = reader.Read<ushort>();
		var nChannels = reader.Read<ushort>();
		var nSamplesPerSec = reader.Read<uint>();
		var nAvgBytesPerSec = reader.Read<uint>();
		var nBlockAlign = reader.Read<ushort>();
		var wBitsPerSample = reader.Read<ushort>();

		// Reads residual bytes
		if (format_chunk_size > 16) {
			reader.Advance(format_chunk_size - 16);
		}

		// Skip over non-data chunks
		while (reader.Remaining > 4 && reader.Read<int>() != MAGIC_DATA) {
			var chunkSize = reader.Read<uint>();
			reader.Advance(chunkSize);
		}

		if (reader.Remaining < 4) {
			throw new NotSupportedException("Specified stream is not a wave file.");
		}

		var format = new Format {
			Tag = (FormatTag)wFormatTag,
			BitsPerSample = wBitsPerSample,
			Channels = nChannels,
			SampleRate = nSamplesPerSec
		};

		return format;
	}

	private static ParseResult Parse(ReadOnlySpan<byte> span)
	{
		var stream = new ByteSpanReader(span);

		var format = ParseFormat(ref stream);

		var waveDataLength = stream.Read<int>();
		var dataSpan = stream.SliceRemainder(waveDataLength);

		return new ParseResult(format, dataSpan);
	}

	/// <summary>
	///     Sets an audio buffer from a span of raw WAV data.
	/// </summary>
	public static void SetData(AudioBuffer audioBuffer, ReadOnlySpan<byte> span)
	{
		var result = Parse(span);
		audioBuffer.Format = result.Format;
		audioBuffer.SetData(result.Data);
	}

	/// <summary>
	///     Create an AudioBuffer containing all the WAV audio data in a file.
	/// </summary>
	public static unsafe AudioBuffer CreateBuffer(AudioDevice device, TitleStorage storage, string path)
	{
		if (!storage.GetFileSize(path, out var size)) {
			return null;
		}

		var buffer = NativeMemory.Alloc((nuint)size);
		var span = new Span<byte>(buffer, (int)size);
		if (!storage.ReadFile(path, span)) {
			return null;
		}

		var result = Parse(span);

		var audioBuffer = AudioBuffer.Create(device, result.Format);
		audioBuffer.SetData(result.Data);
		NativeMemory.Free(buffer);

		return audioBuffer;
	}

	/// <summary>
	///     Get audio format data without reading the entire file.
	/// </summary>
	public static unsafe Format GetFormat(TitleStorage storage, string path)
	{
		if (!storage.GetFileSize(path, out var size)) {
			return new Format();
		}

		var buffer = NativeMemory.Alloc((nuint)size);
		var span = new Span<byte>(buffer, (int)size);
		if (!storage.ReadFile(path, span)) {
			return new Format();
		}

		var reader = new ByteSpanReader(span);
		var format = ParseFormat(ref reader);
		NativeMemory.Free(buffer);

		return format;
	}

	private ref struct ParseResult
	{
		public readonly Format Format;
		public readonly ReadOnlySpan<byte> Data;

		public ParseResult(Format format, ReadOnlySpan<byte> data)
		{
			Format = format;
			Data = data;
		}
	}
}