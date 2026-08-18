using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap
{
	private void SetupMissionAudio()
	{
		_bgmPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionMusic");
		if (_bgmPlayer == null)
		{
			_bgmPlayer = new AudioStreamPlayer
			{
				Name = "MissionMusic",
				VolumeDb = -14.0f
			};
			AddChild(_bgmPlayer);
		}
		else
		{
			_bgmPlayer.VolumeDb = -14.0f;
		}

		AudioStream missionMusic = _audioPlaybackService?.GetStream(MissionMusicPath)
			?? _audioPlaybackService?.GetMp3StreamFromFile(MissionMusicPath, true);
		if (missionMusic == null)
		{
			GD.PrintErr($"Mission music not found at {MissionMusicPath}.");
			return;
		}

		if (missionMusic is AudioStreamMP3 mp3Stream)
		{
			mp3Stream.Loop = true;
		}

		_audioPlaybackService?.TryPlayLoaded(_bgmPlayer, missionMusic);

		_hitSfxPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionHitSfx");
		if (_hitSfxPlayer == null)
		{
			_hitSfxPlayer = new AudioStreamPlayer
			{
				Name = "MissionHitSfx",
				VolumeDb = -9.0f
			};
			AddChild(_hitSfxPlayer);
		}
		else
		{
			_hitSfxPlayer.VolumeDb = -9.0f;
		}

		_missionHitSound = _audioPlaybackService?.GetStream(MissionHitSoundPath)
			?? _audioPlaybackService?.GetOggStreamFromFile(MissionHitSoundPath, false);
		if (_missionHitSound == null)
		{
			GD.PrintErr($"Mission hit sound not found at {MissionHitSoundPath}.");
		}

		_playerShieldHitSfxPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionPlayerShieldHitSfx");
		if (_playerShieldHitSfxPlayer == null)
		{
			_playerShieldHitSfxPlayer = new AudioStreamPlayer
			{
				Name = "MissionPlayerShieldHitSfx",
				VolumeDb = -8.0f
			};
			AddChild(_playerShieldHitSfxPlayer);
		}
		else
		{
			_playerShieldHitSfxPlayer.VolumeDb = -8.0f;
		}

		_missionPlayerShieldHitSound = _audioPlaybackService?.GetStream(MissionPlayerShieldHitSoundPath)
			?? _audioPlaybackService?.GetOggStreamFromFile(MissionPlayerShieldHitSoundPath, false);
		if (_missionPlayerShieldHitSound == null)
		{
			GD.PrintErr($"Mission player shield hit sound not found at {MissionPlayerShieldHitSoundPath}.");
		}

		_officerLaserFireSfxPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionOfficerLaserFireSfx");
		if (_officerLaserFireSfxPlayer == null)
		{
			_officerLaserFireSfxPlayer = new AudioStreamPlayer
			{
				Name = "MissionOfficerLaserFireSfx",
				VolumeDb = -7.0f
			};
			AddChild(_officerLaserFireSfxPlayer);
		}
		else
		{
			_officerLaserFireSfxPlayer.VolumeDb = -7.0f;
		}

		_missionOfficerLaserFireSound = _audioPlaybackService?.GetStream(MissionOfficerLaserFireSoundPath)
			?? _audioPlaybackService?.GetWavStreamFromFile(MissionOfficerLaserFireSoundPath, false);
		if (_missionOfficerLaserFireSound == null)
		{
			GD.PrintErr($"Mission officer laser fire sound not found at {MissionOfficerLaserFireSoundPath}.");
		}

		_enemyLaserFireSfxPlayer = GetNodeOrNull<AudioStreamPlayer>("MissionEnemyLaserFireSfx");
		if (_enemyLaserFireSfxPlayer == null)
		{
			_enemyLaserFireSfxPlayer = new AudioStreamPlayer
			{
				Name = "MissionEnemyLaserFireSfx",
				VolumeDb = -7.5f
			};
			AddChild(_enemyLaserFireSfxPlayer);
		}
		else
		{
			_enemyLaserFireSfxPlayer.VolumeDb = -7.5f;
		}

		_missionEnemyLaserFireSound = _audioPlaybackService?.GetStream(MissionEnemyLaserFireSoundPath)
			?? _audioPlaybackService?.GetWavStreamFromFile(MissionEnemyLaserFireSoundPath, false);
		if (_missionEnemyLaserFireSound == null)
		{
			GD.PrintErr($"Mission enemy laser fire sound not found at {MissionEnemyLaserFireSoundPath}.");
		}
	}
}

