Run the platform-independent audio regression checks with .NET 10:

```sh
dotnet run --project tests/SpeechTempo
```

These compile the production adapter against the actual NAudio.Core and
SoundTouch.Net packages. They check duration and pitch at five tempos for mono
and stereo, channel independence, offset/short reads, chunk invariance, empty and
short streams, flushing, repeated EOF, and invalid saved speed values.

Before release, also build the complete plugin with Dalamud and the Machina
submodule available, and test in-game on Windows and Wine:

- Under Text to Speech, enable “Adjust Google TTS playback speed”. Verify the
  slider is hidden while disabled and the checkbox/speed survive reopening and
  restarting the plugin.
- Force Google TTS and exercise an overlay `say` call at 0.5x, 1.0x, and 2.0x.
  Confirm unchanged pitch, intelligibility, complete short alerts and long
  sentences, and the selected output device.
- Check consecutive alerts, automatic Google fallback on Wine, disabled speed
  adjustment, and SAPI on Windows (which should remain unchanged).
- Inspect the generated plugin ZIP for SoundTouch.Net.dll and the bundled
  ThirdPartyNotices/SoundTouch.Net.txt license notice.

SoundTouch can add processing latency and audible time-stretch artifacts,
particularly at extreme speeds. Synthetic tests do not substitute for listening
through the actual MP3 decoder and WaveOut device on Windows/Wine.
