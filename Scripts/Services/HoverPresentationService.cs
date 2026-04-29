using Godot;
using System.Collections.Generic;

public class HoverPresentationState
{
	public bool RadarVisible { get; set; }
	public Vector2 RadarPosition { get; set; } = Vector2.Zero;
	public bool HoverVisible { get; set; }
	public Vector2 HoverPosition { get; set; } = Vector2.Zero;
	public Color HoverColor { get; set; } = new Color(0f, 1f, 1f, 0.4f);
	public bool TooltipVisible { get; set; }
	public string TooltipText { get; set; } = string.Empty;
	public Vector2 TooltipPosition { get; set; } = Vector2.Zero;
}

public class HoverPresentationService
{
	public HoverPresentationState BuildState(
		bool isTargetingLongRange,
		Vector2I hoveredHex,
		float hexSize,
		float viewportScale,
		Dictionary<Vector2I, Node2D> hexGrid,
		Dictionary<Vector2I, MapEntity> hexContents)
	{
		HoverPresentationState state = new HoverPresentationState();

		if (isTargetingLongRange)
		{
			state.RadarVisible = true;
			state.RadarPosition = HexMath.HexToPixel(hoveredHex, hexSize);
			state.HoverVisible = false;
			state.TooltipVisible = false;
			return state;
		}

		state.RadarVisible = false;
		if (!hexGrid.ContainsKey(hoveredHex))
		{
			state.HoverVisible = false;
			state.TooltipVisible = false;
			return state;
		}

		state.HoverVisible = true;
		state.HoverPosition = HexMath.HexToPixel(hoveredHex, hexSize);
		state.HoverColor = new Color(0f, 1f, 1f, 0.4f);

		if (!hexContents.ContainsKey(hoveredHex))
		{
			state.TooltipVisible = false;
			return state;
		}

		MapEntity hoveredEntity = hexContents[hoveredHex];
		bool isEnemy = hoveredEntity.Type == GameConstants.EntityTypes.EnemyFleet;
		bool isPlayer = hoveredEntity.Type == GameConstants.EntityTypes.PlayerFleet;
		if (!(isEnemy || isPlayer) || !GodotObject.IsInstanceValid(hoveredEntity.VisualSprite) || !hoveredEntity.VisualSprite.Visible)
		{
			state.TooltipVisible = false;
			return state;
		}

		state.HoverColor = isEnemy
			? new Color(1f, 0f, 0f, 0.4f)
			: new Color(0f, 1f, 0f, 0.4f);
		state.TooltipVisible = true;
		state.TooltipText = $"=== {hoveredEntity.Name.ToUpper()} ===\nHP: {hoveredEntity.CurrentHP} / {hoveredEntity.MaxHP}\nShields: {hoveredEntity.CurrentShields} / {hoveredEntity.MaxShields}\nAttack: {hoveredEntity.AttackDamage} DMG\nRange: {hoveredEntity.AttackRange} Hexes";
		state.TooltipPosition = state.HoverPosition + new Vector2((hexSize * viewportScale) + 15, -60);
		return state;
	}

	public void ApplyState(Polygon2D radarHighlight, Polygon2D hoverHighlight, Label hoverTooltip, HoverPresentationState state)
	{
		if (radarHighlight == null || hoverHighlight == null || hoverTooltip == null || state == null) return;

		radarHighlight.Visible = state.RadarVisible;
		radarHighlight.Position = state.RadarPosition;

		hoverHighlight.Visible = state.HoverVisible;
		hoverHighlight.Position = state.HoverPosition;
		hoverHighlight.Color = state.HoverColor;

		hoverTooltip.Visible = state.TooltipVisible;
		hoverTooltip.Text = state.TooltipText;
		hoverTooltip.Position = state.TooltipPosition;
	}
}
