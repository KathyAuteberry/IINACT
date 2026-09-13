using NAudio.Wave;
using SoundTouch;

namespace IINACT;

/// <summary>Streams decoded speech through SoundTouch without changing pitch or sample rate.</summary>
internal sealed class SpeechTempoSampleProvider : ISampleProvider
{
    public const float MinimumTempo = 0.5f;
    public const float MaximumTempo = 2.0f;

    private readonly ISampleProvider source;
    private readonly SoundTouchProcessor processor;
    private readonly float[] input;
    private readonly float[] output;
    private int outputOffset;
    private int outputCount;
    private bool flushed;

    public SpeechTempoSampleProvider(ISampleProvider source, float tempo)
    {
        this.source = source;
        WaveFormat = source.WaveFormat;
        if (WaveFormat.Channels is not (1 or 2))
            throw new ArgumentException("Speech tempo supports mono or stereo audio.", nameof(source));

        processor = new SoundTouchProcessor
        {
            SampleRate = WaveFormat.SampleRate,
            Channels = WaveFormat.Channels,
            Tempo = NormalizeTempo(tempo),
        };
        input = new float[4096 * WaveFormat.Channels];
        output = new float[input.Length];
    }

    public WaveFormat WaveFormat { get; }

    public static float NormalizeTempo(float tempo) =>
        float.IsFinite(tempo) ? Math.Clamp(tempo, MinimumTempo, MaximumTempo) : 1.0f;

    public int Read(float[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
            throw new ArgumentOutOfRangeException(nameof(count));

        var written = 0;
        while (written < count)
        {
            if (outputCount > 0)
            {
                var copyCount = Math.Min(count - written, outputCount);
                Array.Copy(output, outputOffset, buffer, offset + written, copyCount);
                outputOffset += copyCount;
                outputCount -= copyCount;
                written += copyCount;
                continue;
            }

            // SoundTouch counts frames; NAudio counts interleaved channel samples.
            outputOffset = 0;
            outputCount = processor.ReceiveSamples(output.AsSpan(), output.Length / WaveFormat.Channels)
                          * WaveFormat.Channels;
            if (outputCount > 0) continue;
            if (flushed) break;

            // Fill a complete block, including when the upstream provider returns short reads.
            var read = 0;
            while (read < input.Length)
            {
                var received = source.Read(input, read, input.Length - read);
                if (received == 0) break;
                read += received;
            }
            if (read % WaveFormat.Channels != 0)
                throw new InvalidDataException("Speech audio ended with an incomplete sample frame.");
            if (read > 0)
                processor.PutSamples(input.AsSpan(0, read), read / WaveFormat.Channels);
            if (read < input.Length)
            {
                // Flush exactly once, then drain the tail before reporting end of stream.
                processor.Flush();
                flushed = true;
            }
        }
        return written;
    }
}
