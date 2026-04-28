using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class JumpButtonState
{
	public string ButtonText { get; set; } = "ENTER STARGATE";
	public bool IsVisible { get; set; } = false;
}

public class JumpPlan
{
	public bool Allowed { get; set; }
	public bool IsEmergencyJump { get; set; }
	public Vector2I GateHex { get; set; } = Vector2I.Zero;
}

public class JumpService
{
	private readonly GlobalData _globalData;
	private readonly Random _rng = new Random();

	public JumpService(GlobalData globalData)
	{
		_globalData = globalData;
	}

	public JumpButtonState BuildJumpButtonState(bool inCombat, List<Vector2I> selectedHexes, Dictionary<Vector2I, MapEntity> hexContents)
	{
		return new JumpButtonState
		{
			ButtonText = inCombat ? "EMERGENCY JUMP" : "ENTER STARGATE",
			IsVisible = BuildJumpPlan(inCombat, selectedHexes, hexContents).Allowed
		};
	}

	public JumpPlan BuildJumpPlan(bool inCombat, List<Vector2I> selectedHexes, Dictionary<Vector2I, MapEntity> hexContents)
	{
		bool foundGate = TryFindJumpGate(inCombat, selectedHexes, hexContents, out Vector2I gateHex);
		return new JumpPlan
		{
			Allowed = foundGate,
			IsEmergencyJump = inCombat,
			GateHex = gateHex
		};
	}

	public void PrepareForJump()
	{
		if (_globalData != null)
		{
			_globalData.InCombat = false;
		}
	}

	public string FinalizeJump(bool isEmergencyJump)
	{
		if (_globalData == null)
		{
			return isEmergencyJump ? "res://exploration_battle.tscn" : "res://galactic_map.tscn";
		}

		_globalData.InCombat = false;
		_globalData.JustJumped = true;

		if (isEmergencyJump)
		{
			if (_globalData.CurrentSectorStars != null && _globalData.CurrentSectorStars.Count > 1)
			{
				List<StarMapData> availableStars = _globalData.CurrentSectorStars
					.Where(s => s.SystemName != _globalData.SavedSystem)
					.ToList();

				if (availableStars.Count > 0)
				{
					_globalData.SavedSystem = availableStars[_rng.Next(availableStars.Count)].SystemName;
				}
			}

			_globalData.SavedPlanet = string.Empty;

			if (_globalData.HasMethod("SaveGame"))
			{
				_globalData.Call("SaveGame");
			}

			return "res://exploration_battle.tscn";
		}

		return "res://galactic_map.tscn";
	}

	private bool TryFindJumpGate(bool inCombat, List<Vector2I> selectedHexes, Dictionary<Vector2I, MapEntity> hexContents, out Vector2I gateHex)
	{
		return inCombat
			? TryFindEmergencyEscapeGate(hexContents, out gateHex)
			: TryFindSelectedFleetGate(selectedHexes, hexContents, out gateHex);
	}

	private bool TryFindEmergencyEscapeGate(Dictionary<Vector2I, MapEntity> hexContents, out Vector2I gateHex)
	{
		gateHex = Vector2I.Zero;
		List<Vector2I> playerHexes = new List<Vector2I>();
		List<Vector2I> gateHexes = new List<Vector2I>();

		foreach (KeyValuePair<Vector2I, MapEntity> kvp in hexContents)
		{
			if (kvp.Value.Type == GameConstants.EntityTypes.PlayerFleet) playerHexes.Add(kvp.Key);
			if (kvp.Value.Type == GameConstants.EntityTypes.StarGate) gateHexes.Add(kvp.Key);
		}

		if (playerHexes.Count == 0 || gateHexes.Count == 0) return false;

		foreach (Vector2I candidateGateHex in gateHexes)
		{
			bool allNear = true;
			foreach (Vector2I shipHex in playerHexes)
			{
				if (HexMath.HexDistance(candidateGateHex, shipHex) > 1)
				{
					allNear = false;
					break;
				}
			}

			if (allNear)
			{
				gateHex = candidateGateHex;
				return true;
			}
		}

		return false;
	}

	private bool TryFindSelectedFleetGate(List<Vector2I> selectedHexes, Dictionary<Vector2I, MapEntity> hexContents, out Vector2I gateHex)
	{
		gateHex = Vector2I.Zero;
		foreach (Vector2I selectedHex in selectedHexes)
		{
			if (!hexContents.ContainsKey(selectedHex) || hexContents[selectedHex].Type != GameConstants.EntityTypes.PlayerFleet)
			{
				continue;
			}

			foreach (Vector2I dir in HexMath.Directions)
			{
				Vector2I neighbor = selectedHex + dir;
				if (hexContents.ContainsKey(neighbor) && hexContents[neighbor].Type == GameConstants.EntityTypes.StarGate)
				{
					gateHex = neighbor;
					return true;
				}
			}
		}

		return false;
	}
}
