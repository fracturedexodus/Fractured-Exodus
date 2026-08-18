using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap
{
	private void UpdateFogOfWar()
	{
		if (_roomBuilder == null)
		{
			return;
		}

		_visibleCells.Clear();
		_visibleBuildCells.Clear();
		_exploredBuildCells.Clear();
		foreach (OfficerPawn pawn in _officerPawns)
		{
			if (pawn == null)
			{
				continue;
			}

			foreach (Vector2I cell in _roomBuilder.GetReachableMovementCells(pawn.CurrentCell, MissionGridRules.ScaleAuthoredUnit(FogRevealRadius)))
			{
				_visibleCells.Add(cell);
				_exploredCells.Add(cell);
				Vector2I buildCell = GetBuildCell(cell);
				_visibleBuildCells.Add(buildCell);
				_exploredBuildCells.Add(buildCell);
			}
		}

		foreach (Vector2I exploredCell in _exploredCells)
		{
			_exploredBuildCells.Add(GetBuildCell(exploredCell));
		}

		RevealAdjacentDoorCells();

		ApplyFogToLayer(GetNodeOrNull<Node2D>("IsoWorld/FloorLayer"));
		ApplyFogToAmbientFloor();
		ApplyFogToMovementGrid();
		ApplyFogToLayer(GetNodeOrNull<Node2D>("IsoWorld/WallLayer"));
		ApplyFogToLayer(GetNodeOrNull<Node2D>("IsoWorld/PropLayer"));
		ApplyFogToMissionProps();
		ApplyFogToMissionNpcs();
		MarkMissionDepthSortingDirty();
		if (!IsAnyCombatActorMoving())
		{
			EvaluateCombatState();
		}
	}

	private void ApplyFogToLayer(Node2D layer)
	{
		if (layer == null)
		{
			return;
		}

		foreach (Node child in layer.GetChildren())
		{
			if (child is MissionDoor2D door)
			{
				Vector2I doorCell = door.Cell;
				door.SetVisualModulate(GetStructureFogColor(doorCell));
				continue;
			}

			if (child is not Sprite2D sprite)
			{
				continue;
			}

			Vector2I cell = new Vector2I(
				sprite.GetMeta("column", int.MinValue).AsInt32(),
				sprite.GetMeta("row", int.MinValue).AsInt32());
			if (cell.X == int.MinValue || cell.Y == int.MinValue)
			{
				continue;
			}

			Color baseColor = sprite.HasMeta("fog_base_modulate")
				? sprite.GetMeta("fog_base_modulate").AsColor()
				: sprite.Modulate;
			if (!sprite.HasMeta("fog_base_modulate"))
			{
				sprite.SetMeta("fog_base_modulate", baseColor);
			}

			sprite.Modulate = GetFogAdjustedColor(baseColor, cell);
		}
	}

	private void ApplyFogToAmbientFloor()
	{
		foreach (Polygon2D polygon in _ambientFloorPolygons)
		{
			if (polygon == null)
			{
				continue;
			}

			Vector2I cell = new Vector2I(
				polygon.GetMeta("column", int.MinValue).AsInt32(),
				polygon.GetMeta("row", int.MinValue).AsInt32());
			if (cell.X == int.MinValue || cell.Y == int.MinValue)
			{
				continue;
			}

			if (_visibleBuildCells.Contains(cell))
			{
				polygon.Visible = true;
				polygon.Color = new Color(0.04f, 0.10f, 0.11f, 0.18f);
			}
			else if (_exploredBuildCells.Contains(cell))
			{
				polygon.Visible = true;
				polygon.Color = new Color(0.01f, 0.03f, 0.04f, 0.10f);
			}
			else
			{
				polygon.Visible = false;
			}
		}
	}

	private void RevealAdjacentDoorCells()
	{
		if (_roomBuilder == null || _visibleBuildCells.Count == 0)
		{
			return;
		}

		Vector2I[] directions =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};

		HashSet<Vector2I> additionalVisibleCells = new HashSet<Vector2I>();
		foreach (Vector2I cell in _visibleBuildCells)
		{
			foreach (Vector2I direction in directions)
			{
				Vector2I candidate = cell + direction;
				if (_roomBuilder.TryGetDoorIdAtCell(candidate, out string doorId) && !string.IsNullOrWhiteSpace(doorId))
				{
					additionalVisibleCells.Add(candidate);
				}
			}
		}

		foreach (Vector2I cell in additionalVisibleCells)
		{
			_visibleBuildCells.Add(cell);
			_exploredBuildCells.Add(cell);
		}
	}

	private bool IsStructureCellVisible(Vector2I cell)
	{
		return _visibleBuildCells.Contains(cell);
	}

	private bool IsStructureCellExplored(Vector2I cell)
	{
		return _exploredBuildCells.Contains(cell);
	}

	private Color GetStructureFogColor(Vector2I cell)
	{
		return GetFogAdjustedColor(Colors.White, cell);
	}

	private Color GetFogAdjustedSpriteColor(Sprite2D sprite, Vector2I cell)
	{
		Color baseColor = sprite.HasMeta("fog_base_modulate")
			? sprite.GetMeta("fog_base_modulate").AsColor()
			: sprite.Modulate;
		if (!sprite.HasMeta("fog_base_modulate"))
		{
			sprite.SetMeta("fog_base_modulate", baseColor);
		}

		return GetFogAdjustedColor(baseColor, cell);
	}

	private Color GetFogAdjustedColor(Color baseColor, Vector2I cell)
	{
		if (_visibleBuildCells.Contains(cell))
		{
			return baseColor;
		}

		if (_exploredBuildCells.Contains(cell))
		{
			return MultiplyColor(baseColor, 0.38f);
		}

		return MultiplyColor(baseColor, 0.08f);
	}

	private static Color MultiplyColor(Color color, float factor)
	{
		return new Color(
			Mathf.Clamp(color.R * factor, 0f, 1f),
			Mathf.Clamp(color.G * factor, 0f, 1f),
			Mathf.Clamp(color.B * factor, 0f, 1f),
			color.A);
	}

	private static Color BlendColor(Color from, Color to, float weight)
	{
		return new Color(
			Mathf.Lerp(from.R, to.R, weight),
			Mathf.Lerp(from.G, to.G, weight),
			Mathf.Lerp(from.B, to.B, weight),
			Mathf.Lerp(from.A, to.A, weight));
	}

	private static Color GetOccludedStructureColor(Color baseColor)
	{
		Color cooledColor = BlendColor(baseColor, new Color(0.70f, 0.84f, 0.88f, baseColor.A), 0.28f);
		return new Color(
			cooledColor.R,
			cooledColor.G,
			cooledColor.B,
			Mathf.Clamp(baseColor.A * 0.24f, 0.10f, 0.48f));
	}

	private void ApplyFogToMissionNpcs()
	{
		foreach (MissionNpcPawn npc in _missionNpcs)
		{
			if (npc == null)
			{
				continue;
			}

			npc.SetFogVisibility(_visibleCells.Contains(npc.CurrentCell));
		}
	}

	private void ApplyFogToMissionProps()
	{
		foreach (KeyValuePair<Vector2I, MissionProp> kvp in _missionPropsByCell)
		{
			if (kvp.Value == null)
			{
				continue;
			}

			bool isVisible = _visibleBuildCells.Contains(kvp.Key);
			kvp.Value.SetFogVisibility(isVisible);
			kvp.Value.Modulate = isVisible ? Colors.White : new Color(1f, 1f, 1f, 0f);
		}
	}

	private void MarkMissionDepthSortingDirty()
	{
		_missionDepthSortingDirty = true;
		if (_missionDepthSortingRefreshQueued || !IsInsideTree())
		{
			return;
		}

		_missionDepthSortingRefreshQueued = true;
		CallDeferred(nameof(RefreshMissionDepthSortingIfDirty));
	}

	private void RefreshMissionDepthSortingIfDirty()
	{
		_missionDepthSortingRefreshQueued = false;
		if (!_missionDepthSortingDirty)
		{
			return;
		}

		_missionDepthSortingDirty = false;
		UpdateMissionDepthSorting();
	}

	private void UpdateMissionDepthSorting()
	{
		if (_roomBuilder == null)
		{
			return;
		}

		_visibleOccludableBuildCells.Clear();
		foreach (OfficerPawn officer in _officerPawns.Where(officer => officer != null && !officer.IsDead))
		{
			Vector2I buildCell = GetBuildCell(officer.CurrentCell);
			officer.ZAsRelative = false;
			officer.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(buildCell, ActorSortBias);
			officer.SetCoverOccluded(false, CoverGhostZIndex);
			if (officer.Visible)
			{
				_visibleOccludableBuildCells.Add(buildCell);
			}
		}

		foreach (MissionNpcPawn npc in _missionNpcs.Where(npc => npc != null && !npc.IsDead && !npc.IsExtracted))
		{
			Vector2I buildCell = GetBuildCell(npc.CurrentCell);
			npc.ZAsRelative = false;
			npc.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(buildCell, ActorSortBias);
			npc.SetCoverOccluded(false, CoverGhostZIndex);
			if (npc.Visible)
			{
				_visibleOccludableBuildCells.Add(buildCell);
			}
		}

		foreach ((Vector2I cell, MissionProp prop) in _missionPropsByCell)
		{
			if (prop == null)
			{
				continue;
			}

			prop.ZAsRelative = false;
			prop.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(cell, RuntimePropSortBias);
			prop.SetCoverOccluded(false);
			if (!prop.IsConsumed && prop.Visible)
			{
				_visibleOccludableBuildCells.Add(cell);
			}
		}

		ApplyWallDepthSorting(_wallLayer);
		ApplyDoorAndStaticPropDepthSorting(_staticPropLayer);
	}

	private void ApplyWallDepthSorting(Node2D wallLayer)
	{
		if (wallLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in wallLayer.GetChildren())
		{
			if (child is not Sprite2D sprite)
			{
				continue;
			}

			string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
			Vector2I wallCell = new Vector2I(
				sprite.GetMeta("column", int.MinValue).AsInt32(),
				sprite.GetMeta("row", int.MinValue).AsInt32());
			if (string.IsNullOrWhiteSpace(tileId) || wallCell.X == int.MinValue || wallCell.Y == int.MinValue)
			{
				continue;
			}

			sprite.ZAsRelative = false;
			sprite.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(wallCell, WallBaseSortBias);
			sprite.Modulate = GetFogAdjustedSpriteColor(sprite, wallCell);

			string orientationSuffix = sprite.GetMeta("orientation_suffix", string.Empty).AsString();
			List<Vector2I> occludedCells = GetWallOccludedBuildCells(orientationSuffix, tileId, wallCell)
				.Where(IsAnyVisibleOccludableEntityInBuildCell)
				.Distinct()
				.ToList();
			if (occludedCells.Count == 0)
			{
				continue;
			}

			sprite.Modulate = GetOccludedStructureColor(sprite.Modulate);
			sprite.ZIndex = occludedCells
				.Select(cell => _roomBuilder.GetCanvasSortOrderForBuildCell(cell, WallOccluderSortBias))
				.Max();
			foreach (Vector2I occludedCell in occludedCells)
			{
				SetPropsInBuildCellOccluded(occludedCell);
			}
		}
	}

	private void ApplyDoorAndStaticPropDepthSorting(Node2D propLayer)
	{
		if (propLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in propLayer.GetChildren())
		{
			switch (child)
			{
				case MissionDoor2D door:
				{
					Vector2I doorCell = door.Cell;
					door.ZAsRelative = false;
					door.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(doorCell, WallBaseSortBias);
					Color doorColor = GetStructureFogColor(doorCell);
					List<Vector2I> occludedDoorCells = GetOccludedBuildCellsForOrientation(door.OrientationSuffix, doorCell)
						.Where(IsAnyVisibleOccludableEntityInBuildCell)
						.Distinct()
						.ToList();
					if (occludedDoorCells.Count > 0)
					{
						doorColor = GetOccludedStructureColor(doorColor);
						door.ZIndex = occludedDoorCells
							.Select(cell => _roomBuilder.GetCanvasSortOrderForBuildCell(cell, WallOccluderSortBias))
							.Max();
						foreach (Vector2I occludedDoorCell in occludedDoorCells)
						{
							SetPropsInBuildCellOccluded(occludedDoorCell);
						}
					}
					door.SetVisualModulate(doorColor);
					break;
				}
				case Sprite2D sprite:
				{
					string tileId = sprite.GetMeta("tile_id", string.Empty).AsString();
					Vector2I propCell = new Vector2I(
						sprite.GetMeta("column", int.MinValue).AsInt32(),
						sprite.GetMeta("row", int.MinValue).AsInt32());
					if (propCell.X == int.MinValue || propCell.Y == int.MinValue)
					{
						continue;
					}

					sprite.ZAsRelative = false;
					sprite.Modulate = GetFogAdjustedSpriteColor(sprite, propCell);
					string orientationSuffix = sprite.GetMeta("orientation_suffix", string.Empty).AsString();
					List<Vector2I> occludedPropCells = GetWallOccludedBuildCells(orientationSuffix, tileId, propCell)
						.Where(IsAnyVisibleOccludableEntityInBuildCell)
						.Distinct()
						.ToList();
					if (occludedPropCells.Count > 0)
					{
						sprite.Modulate = GetOccludedStructureColor(sprite.Modulate);
						sprite.ZIndex = occludedPropCells
							.Select(cell => _roomBuilder.GetCanvasSortOrderForBuildCell(cell, WallOccluderSortBias))
							.Max();
						foreach (Vector2I occludedPropCell in occludedPropCells)
						{
							SetPropsInBuildCellOccluded(occludedPropCell);
						}
					}
					else
					{
						sprite.ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(propCell, RuntimePropSortBias);
					}
					break;
				}
			}
		}
	}

	private bool IsAnyVisibleOccludableEntityInBuildCell(Vector2I buildCell)
	{
		return _visibleOccludableBuildCells.Contains(buildCell);
	}

	private void SetPropsInBuildCellOccluded(Vector2I buildCell)
	{
		if (_missionPropsByCell.TryGetValue(buildCell, out MissionProp prop) && prop != null)
		{
			prop.SetCoverOccluded(true);
		}
	}

	private IEnumerable<Vector2I> GetWallOccludedBuildCells(string orientationSuffix, string tileId, Vector2I wallCell)
	{
		if (string.IsNullOrWhiteSpace(orientationSuffix) && !TryGetWallOrientationFromTileId(tileId, out orientationSuffix))
		{
			yield break;
		}

		foreach (Vector2I occludedCell in GetOccludedBuildCellsForOrientation(orientationSuffix, wallCell))
		{
			yield return occludedCell;
		}
	}

	private static bool TryGetWallOrientationFromTileId(string tileId, out string orientationSuffix)
	{
		orientationSuffix = string.Empty;
		if (string.IsNullOrWhiteSpace(tileId))
		{
			return false;
		}

		if (tileId.Contains("_nw_", StringComparison.OrdinalIgnoreCase) || tileId.StartsWith("wall_nw", StringComparison.OrdinalIgnoreCase))
		{
			orientationSuffix = "nw";
			return true;
		}

		if (tileId.Contains("_ne_", StringComparison.OrdinalIgnoreCase) || tileId.StartsWith("wall_ne", StringComparison.OrdinalIgnoreCase))
		{
			orientationSuffix = "ne";
			return true;
		}

		if (tileId.Contains("_se_", StringComparison.OrdinalIgnoreCase) || tileId.StartsWith("wall_se", StringComparison.OrdinalIgnoreCase))
		{
			orientationSuffix = "se";
			return true;
		}

		if (tileId.Contains("_sw_", StringComparison.OrdinalIgnoreCase) || tileId.StartsWith("wall_sw", StringComparison.OrdinalIgnoreCase))
		{
			orientationSuffix = "sw";
			return true;
		}

		return false;
	}

	private static IEnumerable<Vector2I> GetOccludedBuildCellsForOrientation(string orientationSuffix, Vector2I wallCell)
	{
		switch (orientationSuffix)
		{
			case "se":
				yield return wallCell;
				yield return wallCell + new Vector2I(1, 0);
				yield return wallCell + new Vector2I(1, 1);
				yield break;
			case "sw":
				yield return wallCell;
				yield return wallCell + new Vector2I(0, 1);
				yield return wallCell + new Vector2I(1, 1);
				yield break;
		}
	}

	private void ApplyFogToMovementGrid()
	{
		if (!_combatActive)
		{
			foreach (Line2D outline in _movementGridOutlines)
			{
				if (outline != null)
				{
					outline.Visible = false;
				}
			}
			return;
		}

		foreach (Line2D outline in _movementGridOutlines)
		{
			if (outline == null)
			{
				continue;
			}

			Vector2I movementCell = new Vector2I(
				outline.GetMeta("movement_cell_x", int.MinValue).AsInt32(),
				outline.GetMeta("movement_cell_y", int.MinValue).AsInt32());
			if (movementCell.X == int.MinValue || movementCell.Y == int.MinValue)
			{
				continue;
			}

			if (_visibleCells.Contains(movementCell))
			{
				outline.Visible = true;
				outline.DefaultColor = new Color(0.30f, 0.76f, 1.00f, 0.30f);
			}
			else if (_exploredCells.Contains(movementCell))
			{
				outline.Visible = true;
				outline.DefaultColor = new Color(0.14f, 0.34f, 0.56f, 0.17f);
			}
			else
			{
				outline.Visible = false;
			}
		}
	}
}
