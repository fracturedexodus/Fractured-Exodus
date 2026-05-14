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

	public AudioStream GetMp3StreamFromFile(string filePath, bool loop = true, float loopOffset = 0f)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !FileAccess.FileExists(filePath)) return null;

		string cacheKey = $"{filePath}|mp3|{loop}|{loopOffset}";
		if (_streamCache.TryGetValue(cacheKey, out AudioStream cachedStream))
		{
			return cachedStream;
		}

		using FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return null;
		}

		AudioStreamMP3 stream = new AudioStreamMP3
		{
			Data = file.GetBuffer((long)file.GetLength()),
			Loop = loop,
			LoopOffset = loopOffset
		};
		_streamCache[cacheKey] = stream;
		return stream;
	}

	public AudioStream GetOggStreamFromFile(string filePath, bool loop = false)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !FileAccess.FileExists(filePath)) return null;

		string cacheKey = $"{filePath}|ogg|{loop}";
		if (_streamCache.TryGetValue(cacheKey, out AudioStream cachedStream))
		{
			return cachedStream;
		}

		AudioStreamOggVorbis stream = AudioStreamOggVorbis.LoadFromFile(filePath);
		if (stream == null)
		{
			return null;
		}

		stream.Loop = loop;
		_streamCache[cacheKey] = stream;
		return stream;
	}

	public AudioStream GetWavStreamFromFile(string filePath, bool loop = false)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !FileAccess.FileExists(filePath)) return null;

		string cacheKey = $"{filePath}|wav|{loop}";
		if (_streamCache.TryGetValue(cacheKey, out AudioStream cachedStream))
		{
			return cachedStream;
		}

		AudioStreamWav stream = AudioStreamWav.LoadFromFile(filePath);
		if (stream == null)
		{
			return null;
		}

		stream.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
		_streamCache[cacheKey] = stream;
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
