using Godot;
using System.Collections.Generic;

public class AudioPlaybackService
{
	private readonly Dictionary<string, AudioStream> _streamCache = new Dictionary<string, AudioStream>();

	public AudioStream GetStream(string streamPath)
	{
		if (string.IsNullOrEmpty(streamPath)) return null;

		if (!_streamCache.TryGetValue(streamPath, out AudioStream stream))
		{
			stream = GD.Load<AudioStream>(streamPath);
			if (stream == null) return null;
			_streamCache[streamPath] = stream;
		}

		return stream;
	}

	public void TryPlay(AudioStreamPlayer player, string streamPath, float? pitchScale = null)
	{
		if (player == null || string.IsNullOrEmpty(streamPath)) return;

		AudioStream stream = GetStream(streamPath);
		if (stream == null) return;

		TryPlayLoaded(player, stream, pitchScale);
	}

	public void TryPlayLoaded(AudioStreamPlayer player, AudioStream stream, float? pitchScale = null)
	{
		if (player == null || stream == null) return;

		player.Stream = stream;
		if (pitchScale.HasValue)
		{
			player.PitchScale = pitchScale.Value;
		}

		player.Play();
	}
}
