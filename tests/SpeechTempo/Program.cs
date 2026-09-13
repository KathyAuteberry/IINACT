using IINACT;
using NAudio.Wave;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static float[] Process(float[] data, int channels, float tempo, int readSize, int sourceReadSize)
{
    var source = new Samples(data, channels, sourceReadSize);
    var provider = new SpeechTempoSampleProvider(source, tempo);
    Check(provider.WaveFormat.SampleRate == 24000 && provider.WaveFormat.Channels == channels,
        "Audio format changed");
    var result = new List<float>();
    var buffer = new float[readSize + 2];
    Check(provider.Read(buffer, 1, 0) == 0, "Zero-length read");
    int count;
    while ((count = provider.Read(buffer, 1, readSize)) > 0)
    {
        Check(buffer[0] == 0 && buffer[^1] == 0, "Read wrote outside requested range");
        result.AddRange(buffer.AsSpan(1, count).ToArray());
        Check(result.Count < Math.Max(24000, data.Length * 3), "Stream did not terminate");
    }
    Check(provider.Read(buffer, 1, readSize) == 0, "EOF was not stable");
    return result.ToArray();
}

static double Frequency(float[] audio, int channels, int channel)
{
    // Ignore boundary transients; count positive crossings in the middle half.
    var start = audio.Length / channels / 4;
    var end = audio.Length / channels * 3 / 4;
    var crossings = 0;
    for (var i = start + 1; i < end; i++)
        if (audio[(i - 1) * channels + channel] <= 0 && audio[i * channels + channel] > 0)
            crossings++;
    return crossings * 24000.0 / (end - start);
}

foreach (var channels in new[] { 1, 2 })
foreach (var tempo in new[] { 0.5f, 0.75f, 1f, 1.5f, 2f })
{
    var data = new float[24000 * 2 * channels];
    for (var frame = 0; frame < data.Length / channels; frame++)
    for (var channel = 0; channel < channels; channel++)
        data[frame * channels + channel] = (float)(0.5 * Math.Sin(2 * Math.PI * (channel == 0 ? 440 : 660) * frame / 24000));
    var output = Process(data, channels, tempo, 997, 777);
    Check(Math.Abs(output.Length - data.Length / tempo) <= channels * 2, $"Duration mismatch: {channels}ch {tempo}x");
    for (var channel = 0; channel < channels; channel++)
        Check(Math.Abs(Frequency(output, channels, channel) - (channel == 0 ? 440 : 660)) < 8,
            $"Pitch changed: {channels}ch {tempo}x channel {channel}");
    Check(output.SequenceEqual(Process(data, channels, tempo, 4096, 8192)), "Read chunk size changed output");
    Console.WriteLine($"PASS {channels}ch {tempo}x: {output.Length / channels} frames, {Frequency(output, channels, 0):F1} Hz");
}
foreach (var frames in new[] { 0, 1, 240, 4096, 4200 })
foreach (var channels in new[] { 1, 2 })
foreach (var tempo in new[] { 0.5f, 2f })
{
    var output = Process(new float[frames * channels], channels, tempo, 7, 11);
    Check(Math.Abs(output.Length - frames * channels / tempo) <= channels * 2, "Short stream tail lost");
}
Check(SpeechTempoSampleProvider.NormalizeTempo(float.NaN) == 1, "NaN handling");
Check(SpeechTempoSampleProvider.NormalizeTempo(float.PositiveInfinity) == 1, "Infinity handling");
Check(SpeechTempoSampleProvider.NormalizeTempo(-1) == 0.5f, "Lower clamp");
Check(SpeechTempoSampleProvider.NormalizeTempo(10) == 2, "Upper clamp");
Console.WriteLine("PASS short/empty streams, stable EOF, offsets, chunk boundaries, and invalid settings");

sealed class Samples(float[] data, int channels, int maxRead) : ISampleProvider
{
    private int position;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(24000, channels);
    public int Read(float[] buffer, int offset, int count)
    {
        var read = Math.Min(Math.Min(count, maxRead), data.Length - position);
        Array.Copy(data, position, buffer, offset, read);
        position += read;
        return read;
    }
}
