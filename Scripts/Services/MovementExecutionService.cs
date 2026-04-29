using Godot;
using System.Collections.Generic;

public class MoveExecutionResult
{
	public bool Allowed { get; set; }
	public MapEntity Ship { get; set; }
	public string SoundPath { get; set; } = string.Empty;
	public bool RefreshResourceUi { get; set; }
	public Vector2 TargetPixelPosition { get; set; } = Vector2.Zero;
	public float Duration { get; set; } = 0.3f;
	public bool ReopenShipMenu { get; set; }
}

public class MovementExecutionService
{
	public MoveExecutionResult ExecuteMove(
		Dictionary<Vector2I, MapEntity> hexContents,
		List<Vector2I> selectedHexes,
		Vector2I fromHex,
		Vector2I toHex,
		int cost,
		float hexSize,
		bool inCombat,
		GlobalData globalData)
	{
		MoveExecutionResult result = new MoveExecutionResult();
		if (hexContents == null || !hexContents.ContainsKey(fromHex))
		{
			return result;
		}

		MapEntity ship = hexContents[fromHex];
		hexContents.Remove(fromHex);
		hexContents[toHex] = ship;
		ship.CurrentActions -= cost;

		result.Allowed = true;
		result.Ship = ship;
		result.SoundPath = Database.GetShipMovementSoundPath(ship.Name);
		result.ReopenShipMenu = selectedHexes.Count == 1 && selectedHexes[0] == fromHex;

		if (ship.Type == GameConstants.EntityTypes.PlayerFleet && globalData != null && !inCombat)
		{
			int distance = HexMath.HexDistance(fromHex, toHex);
			float fuelCost = distance * 0.25f;
			float currentFuel = globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials].AsSingle();
			globalData.FleetResources[GameConstants.ResourceKeys.RawMaterials] = Mathf.Max(0f, currentFuel - fuelCost);
			result.RefreshResourceUi = true;
		}

		result.TargetPixelPosition = HexMath.HexToPixel(toHex, hexSize);
		result.Duration = Mathf.Max(0.3f, ship.VisualSprite.Position.DistanceTo(result.TargetPixelPosition) / 500f);
		return result;
	}
}
