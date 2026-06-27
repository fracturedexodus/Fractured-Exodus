using Godot;

public static class MissionGridRules
{
	public const int SubgridUnitsPerTile = 8;
	public const int StandardActionCost = SubgridUnitsPerTile;

	public static int ScaleAuthoredUnit(int authoredValue)
	{
		return Mathf.Max(1, authoredValue) * SubgridUnitsPerTile;
	}

	public static int ScaleAuthoredUnitAllowZero(int authoredValue)
	{
		return authoredValue <= 0 ? 0 : authoredValue * SubgridUnitsPerTile;
	}
}
