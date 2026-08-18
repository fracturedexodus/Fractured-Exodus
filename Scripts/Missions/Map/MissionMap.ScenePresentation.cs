using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionMap
{
	private void ConfigureMissionView()
	{
		if (_camera != null)
		{
			ApplyZoom(MaxZoom);
		}
	}

	private void FocusCameraOnAwayTeam()
	{
		if (_camera == null)
		{
			return;
		}

		List<OfficerPawn> officers = GetAliveOfficers().ToList();
		if (officers.Count == 0)
		{
			Vector2 roomCenter = _roomBuilder != null
				? _isoWorld.ToGlobal(_roomBuilder.GetRoomCenterWorldPosition())
				: Vector2.Zero;
			_camera.Position = roomCenter;
			ApplyZoom(MaxZoom);
			return;
		}

		Vector2 focusPosition = Vector2.Zero;
		foreach (OfficerPawn officer in officers)
		{
			focusPosition += officer.GlobalPosition;
		}

		_camera.Position = focusPosition / officers.Count;
		ApplyZoom(MaxZoom);
	}

	private void FocusCameraOnUnit(Node2D unit)
	{
		if (_camera == null || unit == null)
		{
			return;
		}

		_camera.Position = unit.GlobalPosition;
	}

	private void EnsureBackgroundNodes()
	{
		CanvasLayer backdropCanvas = GetNode<CanvasLayer>("BackdropCanvas");
		_backgroundBackdrop = backdropCanvas.GetNodeOrNull<TextureRect>("BackgroundTexture");
		if (_backgroundBackdrop == null)
		{
			_backgroundBackdrop = new TextureRect
			{
				Name = "BackgroundTexture",
				AnchorsPreset = (int)Control.LayoutPreset.FullRect,
				AnchorRight = 1f,
				AnchorBottom = 1f,
				GrowHorizontal = Control.GrowDirection.Both,
				GrowVertical = Control.GrowDirection.Both,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				Visible = false,
				Modulate = new Color(0.52f, 0.60f, 0.66f, 0.18f)
			};
			backdropCanvas.AddChild(_backgroundBackdrop);
			backdropCanvas.MoveChild(_backgroundBackdrop, 1);
		}

		Node2D backgroundLayer = _isoWorld.GetNodeOrNull<Node2D>("BackgroundArtLayer");
		if (backgroundLayer == null)
		{
			backgroundLayer = new Node2D
			{
				Name = "BackgroundArtLayer",
				ZIndex = -10
			};
			_isoWorld.AddChild(backgroundLayer);
			_isoWorld.MoveChild(backgroundLayer, 0);
		}

		_backgroundFeatureSprite = backgroundLayer.GetNodeOrNull<Sprite2D>("BackgroundFeature");
		if (_backgroundFeatureSprite == null)
		{
			_backgroundFeatureSprite = new Sprite2D
			{
				Name = "BackgroundFeature",
				Centered = true,
				Visible = false
			};
			backgroundLayer.AddChild(_backgroundFeatureSprite);
		}
	}

	private void ApplyMissionBackground()
	{
		MissionBackgroundDefinition definition = MissionBackgroundCatalog.GetById(_roomBuilder?.GetSelectedBackgroundId());

		if (_backgroundBackdrop != null)
		{
			_backgroundBackdrop.Texture = string.IsNullOrEmpty(definition.BackdropTexturePath)
				? null
				: GD.Load<Texture2D>(definition.BackdropTexturePath);
			_backgroundBackdrop.Visible = _backgroundBackdrop.Texture != null;
		}

		if (_backgroundFeatureSprite == null)
		{
			return;
		}

		_backgroundFeatureSprite.Texture = string.IsNullOrEmpty(definition.FeatureTexturePath)
			? null
			: GD.Load<Texture2D>(definition.FeatureTexturePath);
		_backgroundFeatureSprite.Visible = _backgroundFeatureSprite.Texture != null;
		if (!_backgroundFeatureSprite.Visible || _roomBuilder == null)
		{
			return;
		}

		_backgroundFeatureSprite.Scale = new Vector2(definition.FeatureScale, definition.FeatureScale);
		_backgroundFeatureSprite.Modulate = BlendColor(
			definition.FeatureModulate,
			new Color(0.74f, 0.84f, 0.88f, definition.FeatureModulate.A),
			0.34f);
		_backgroundFeatureSprite.Position = _roomBuilder.GetRoomCenterWorldPosition() + definition.FeatureOffset;
	}

	private void BuildAmbientFloorShading()
	{
		EnsureAmbientFloorLayer();
		if (_ambientFloorLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in _ambientFloorLayer.GetChildren())
		{
			_ambientFloorLayer.RemoveChild(child);
			child.QueueFree();
		}

		_ambientFloorPolygons.Clear();

		Vector2 tileStep = _roomBuilder.TileStep;
		Vector2[] diamondPoints =
		{
			new Vector2(0f, -tileStep.Y * AmbientFloorDiamondHeightFactor),
			new Vector2(tileStep.X * AmbientFloorDiamondWidthFactor, 0f),
			new Vector2(0f, tileStep.Y * AmbientFloorDiamondHeightFactor),
			new Vector2(-tileStep.X * AmbientFloorDiamondWidthFactor, 0f)
		};

		foreach (Vector2I buildCell in _roomBuilder.GetFloorCells())
		{
			Polygon2D polygon = new Polygon2D
			{
				Name = $"AmbientFloor_{buildCell.X}_{buildCell.Y}",
				Polygon = diamondPoints,
				Position = _roomBuilder.GetCellWorldPosition(buildCell.X, buildCell.Y, new Vector2(0f, tileStep.Y * 0.02f)),
				Color = Colors.Transparent
			};
			polygon.SetMeta("column", buildCell.X);
			polygon.SetMeta("row", buildCell.Y);
			_ambientFloorLayer.AddChild(polygon);
			_ambientFloorPolygons.Add(polygon);
		}
	}

	private void EnsureAmbientFloorLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_ambientFloorLayer = _isoWorld.GetNodeOrNull<Node2D>("AmbientFloorLayer");
		if (_ambientFloorLayer == null)
		{
			_ambientFloorLayer = new Node2D
			{
				Name = "AmbientFloorLayer",
				ZIndex = 0
			};
			_isoWorld.AddChild(_ambientFloorLayer);
		}

		Node floorLayer = _isoWorld.GetNodeOrNull<Node>("FloorLayer");
		int insertIndex = floorLayer != null ? floorLayer.GetIndex() + 1 : 1;
		_isoWorld.MoveChild(_ambientFloorLayer, insertIndex);
	}

	private void BuildEvacZoneHighlights()
	{
		EnsureEvacZoneLayer();
		if (_evacZoneLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in _evacZoneLayer.GetChildren())
		{
			_evacZoneLayer.RemoveChild(child);
			child.QueueFree();
		}

		_evacZoneVisualRoots.Clear();
		_evacZoneHighlightPolygons.Clear();
		_evacZoneHighlightOutlines.Clear();

		List<MissionRoomBuilder.MarkerPlacement> evacMarkers = _roomBuilder.GetMarkerPlacements()
			.Where(marker => marker.MarkerId == "evac_zone")
			.ToList();
		if (evacMarkers.Count == 0)
		{
			return;
		}

		Vector2 tileStep = _roomBuilder.TileStep;
		Vector2[] diamondPoints =
		{
			new Vector2(0f, -tileStep.Y * 0.28f),
			new Vector2(tileStep.X * 0.28f, 0f),
			new Vector2(0f, tileStep.Y * 0.28f),
			new Vector2(-tileStep.X * 0.28f, 0f)
		};
		Vector2[] innerDiamondPoints =
		{
			new Vector2(0f, -tileStep.Y * 0.16f),
			new Vector2(tileStep.X * 0.16f, 0f),
			new Vector2(0f, tileStep.Y * 0.16f),
			new Vector2(-tileStep.X * 0.16f, 0f)
		};

		foreach (MissionRoomBuilder.MarkerPlacement evacMarker in evacMarkers)
		{
			Node2D root = new Node2D
			{
				Name = $"EvacZone_{evacMarker.Cell.X}_{evacMarker.Cell.Y}",
				Position = _roomBuilder.GetCellWorldPosition(evacMarker.Cell.X, evacMarker.Cell.Y)
			};
			_evacZoneLayer.AddChild(root);
			_evacZoneVisualRoots.Add(root);

			Polygon2D outerFill = new Polygon2D
			{
				Polygon = diamondPoints,
				Color = new Color(0.18f, 0.92f, 0.56f, 0.18f)
			};
			root.AddChild(outerFill);
			_evacZoneHighlightPolygons.Add(outerFill);

			Line2D outline = new Line2D
			{
				Points = diamondPoints,
				Closed = true,
				Width = 4f,
				DefaultColor = new Color(0.48f, 1.00f, 0.72f, 0.82f)
			};
			root.AddChild(outline);
			_evacZoneHighlightOutlines.Add(outline);

			Polygon2D innerFill = new Polygon2D
			{
				Polygon = innerDiamondPoints,
				Color = new Color(0.62f, 1.00f, 0.82f, 0.22f)
			};
			root.AddChild(innerFill);
			_evacZoneHighlightPolygons.Add(innerFill);

			Label label = new Label
			{
				Text = "EVAC",
				Position = new Vector2(-70f, -tileStep.Y * 0.62f),
				Size = new Vector2(140f, 28f),
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			label.AddThemeFontSizeOverride("font_size", 18);
			label.AddThemeColorOverride("font_color", new Color(0.90f, 1.00f, 0.95f, 0.96f));
			label.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.12f, 0.08f, 0.92f));
			label.AddThemeConstantOverride("outline_size", 4);
			root.AddChild(label);
		}

		UpdateEvacZoneHighlightVisuals(0f);
	}

	private void BuildMovementGridOverlay()
	{
		EnsureMovementGridLayer();
		if (_movementGridLayer == null || _roomBuilder == null)
		{
			return;
		}

		foreach (Node child in _movementGridLayer.GetChildren())
		{
			_movementGridLayer.RemoveChild(child);
			child.QueueFree();
		}

		_movementGridOutlines.Clear();

		Vector2 movementTileStep = _roomBuilder.TileStep / _roomBuilder.MovementSubdivision;
		Vector2[] diamondPoints =
		{
			new Vector2(0f, -movementTileStep.Y * 0.5f),
			new Vector2(movementTileStep.X * 0.5f, 0f),
			new Vector2(0f, movementTileStep.Y * 0.5f),
			new Vector2(-movementTileStep.X * 0.5f, 0f)
		};
		Vector2[] diamondOutlinePoints =
		{
			diamondPoints[0],
			diamondPoints[1],
			diamondPoints[2],
			diamondPoints[3],
			diamondPoints[0]
		};

		foreach (Vector2I buildCell in _roomBuilder.GetFloorCells())
		{
			foreach (Vector2I movementCell in _roomBuilder.GetMovementCellsForBuildCell(buildCell))
			{
				Line2D outline = new Line2D
				{
					Name = $"MovementGrid_{movementCell.X}_{movementCell.Y}",
					Points = diamondOutlinePoints,
					Closed = false,
					Width = 1.15f,
					DefaultColor = new Color(0.24f, 0.72f, 1.00f, 0.24f),
					Position = _roomBuilder.GetMovementCellWorldPosition(movementCell.X, movementCell.Y),
					ZIndex = _roomBuilder.GetCanvasSortOrderForBuildCell(buildCell, FloorGridSortBias),
					Visible = false
				};
				outline.ZAsRelative = false;
				outline.SetMeta("movement_cell_x", movementCell.X);
				outline.SetMeta("movement_cell_y", movementCell.Y);
				_movementGridLayer.AddChild(outline);
				_movementGridOutlines.Add(outline);
			}
		}

		UpdateMovementGridVisibility();
	}

	private void EnsureMovementGridLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_movementGridLayer = _isoWorld.GetNodeOrNull<Node2D>("MovementGridLayer");
		if (_movementGridLayer != null)
		{
			return;
		}

		_movementGridLayer = new Node2D
		{
			Name = "MovementGridLayer",
			ZIndex = 0
		};
		_isoWorld.AddChild(_movementGridLayer);

		Node ambientFloorLayer = _isoWorld.GetNodeOrNull<Node>("AmbientFloorLayer");
		Node floorLayer = _isoWorld.GetNodeOrNull<Node>("FloorLayer");
		int insertIndex = ambientFloorLayer != null
			? ambientFloorLayer.GetIndex() + 1
			: floorLayer != null ? floorLayer.GetIndex() + 1 : 1;
		_isoWorld.MoveChild(_movementGridLayer, insertIndex);
	}

	private void BuildMovementCursorHighlight()
	{
		EnsureMovementCursorLayer();
		if (_movementCursorLayer == null || _roomBuilder == null)
		{
			return;
		}

		Vector2 movementTileStep = _roomBuilder.TileStep / _roomBuilder.MovementSubdivision;
		Vector2[] diamondPoints =
		{
			new Vector2(0f, -movementTileStep.Y * 0.5f),
			new Vector2(movementTileStep.X * 0.5f, 0f),
			new Vector2(0f, movementTileStep.Y * 0.5f),
			new Vector2(-movementTileStep.X * 0.5f, 0f)
		};

		if (_movementCursorFill == null)
		{
			_movementCursorFill = new Polygon2D
			{
				Name = "MovementCursorFill",
				Color = new Color(0.26f, 0.78f, 1.00f, 0.12f),
				Visible = false
			};
			_movementCursorLayer.AddChild(_movementCursorFill);
		}
		_movementCursorFill.Polygon = diamondPoints;

		if (_movementCursorOutlineRoot == null)
		{
			_movementCursorOutlineRoot = new Node2D
			{
				Name = "MovementCursorOutline",
				Visible = false
			};
			_movementCursorLayer.AddChild(_movementCursorOutlineRoot);
		}

		EnsureMovementCursorOutlineSegments();
		UpdateMovementCursorOutlineGeometry(diamondPoints, 3.1f);
		SetMovementCursorOutlineColor(new Color(0.56f, 0.92f, 1.00f, 0.88f));
	}

	private void EnsureMovementCursorLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_movementCursorLayer = _isoWorld.GetNodeOrNull<Node2D>("MovementCursorLayer");
		if (_movementCursorLayer != null)
		{
			return;
		}

		_movementCursorLayer = new Node2D
		{
			Name = "MovementCursorLayer",
			ZIndex = 2
		};
		_isoWorld.AddChild(_movementCursorLayer);

		Node movementGridLayer = _isoWorld.GetNodeOrNull<Node>("MovementGridLayer");
		int insertIndex = movementGridLayer != null ? movementGridLayer.GetIndex() + 1 : 2;
		_isoWorld.MoveChild(_movementCursorLayer, insertIndex);
	}

	private void UpdateMovementCursorHighlight()
	{
		if (_movementCursorFill == null || _movementCursorOutlineRoot == null || _roomBuilder == null || _isoWorld == null)
		{
			return;
		}

		if (_missionGameOver || (_dialogueUi?.IsConversationOpen ?? false) || (_missionUi?.IsStoryEventVisible ?? false))
		{
			_movementCursorFill.Visible = false;
			_movementCursorOutlineRoot.Visible = false;
			return;
		}

		Vector2 mousePosition = GetViewport().GetMousePosition();
		Vector2 screenSize = GetViewportRect().Size;
		if (mousePosition.X < 0f || mousePosition.Y < 0f || mousePosition.X > screenSize.X || mousePosition.Y > screenSize.Y)
		{
			_movementCursorFill.Visible = false;
			_movementCursorOutlineRoot.Visible = false;
			return;
		}

		Vector2I hoveredCell = ResolveMovementTargetCell(_roomBuilder.GetNearestMovementCell(_isoWorld.ToLocal(GetGlobalMousePosition())));
		if (_combatActive)
		{
			if (!IsPlayerTurnActive())
			{
				_movementCursorFill.Visible = false;
				_movementCursorOutlineRoot.Visible = false;
				return;
			}

			if (GetHoveredVisibleHostileAtMouse() is MissionNpcPawn hoveredHostile)
			{
				hoveredCell = hoveredHostile.CurrentCell;
			}
		}

		if (!_roomBuilder.IsWalkableMovementCell(hoveredCell))
		{
			_movementCursorFill.Visible = false;
			_movementCursorOutlineRoot.Visible = false;
			return;
		}

		if (_combatActive && !_visibleCells.Contains(hoveredCell))
		{
			_movementCursorFill.Visible = false;
			_movementCursorOutlineRoot.Visible = false;
			return;
		}

		MissionNpcPawn hoveredEnemy = _combatActive ? GetNpcAtMovementCell(hoveredCell) : null;
		bool hostileTargetCell = hoveredEnemy != null && hoveredEnemy.IsHostile && !hoveredEnemy.IsDead && _visibleCells.Contains(hoveredEnemy.CurrentCell);
		if (_combatActive)
		{
			bool attackMode = _selectedCombatActionMode == MissionPlayerCombatActionMode.Attack;
			_movementCursorFill.Color = attackMode && hostileTargetCell
				? new Color(1.00f, 0.42f, 0.26f, 0.22f)
				: new Color(0.26f, 0.78f, 1.00f, 0.16f);
			SetMovementCursorOutlineColor(attackMode && hostileTargetCell
				? new Color(1.00f, 0.72f, 0.48f, 0.96f)
				: new Color(0.56f, 0.92f, 1.00f, 0.92f));
		}
		else
		{
			_movementCursorFill.Color = new Color(0.26f, 0.78f, 1.00f, 0.12f);
			SetMovementCursorOutlineColor(new Color(0.56f, 0.92f, 1.00f, 0.88f));
		}

		Vector2 hoverPosition = _roomBuilder.GetMovementCellWorldPosition(hoveredCell.X, hoveredCell.Y).Round();
		_movementCursorFill.Position = hoverPosition;
		_movementCursorOutlineRoot.Position = hoverPosition;
		int cursorZIndex = GetMovementOverlayZIndex(hoveredCell, MovementCursorSortBias);
		_movementCursorFill.ZAsRelative = false;
		_movementCursorFill.ZIndex = cursorZIndex;
		_movementCursorOutlineRoot.ZAsRelative = false;
		_movementCursorOutlineRoot.ZIndex = cursorZIndex;
		_movementCursorFill.Visible = true;
		_movementCursorOutlineRoot.Visible = true;
	}

	private void EnsureMovementCursorOutlineSegments()
	{
		if (_movementCursorOutlineRoot == null)
		{
			return;
		}

		while (_movementCursorOutlineSegments.Count < 4)
		{
			Polygon2D segment = new Polygon2D
			{
				Name = $"MovementCursorOutlineSegment{_movementCursorOutlineSegments.Count}",
				Color = new Color(0.56f, 0.92f, 1.00f, 0.88f)
			};
			_movementCursorOutlineRoot.AddChild(segment);
			_movementCursorOutlineSegments.Add(segment);
		}
	}

	private void UpdateMovementCursorOutlineGeometry(IReadOnlyList<Vector2> diamondPoints, float thickness)
	{
		if (diamondPoints == null || diamondPoints.Count < 4 || _movementCursorOutlineSegments.Count < 4)
		{
			return;
		}

		float halfThickness = thickness * 0.5f;
		for (int i = 0; i < 4; i++)
		{
			Vector2 start = diamondPoints[i];
			Vector2 end = diamondPoints[(i + 1) % 4];
			Vector2 direction = (end - start).Normalized();
			Vector2 normal = new Vector2(-direction.Y, direction.X) * halfThickness;
			_movementCursorOutlineSegments[i].Polygon = new[]
			{
				start + normal,
				end + normal,
				end - normal,
				start - normal
			};
		}
	}

	private void SetMovementCursorOutlineColor(Color color)
	{
		foreach (Polygon2D segment in _movementCursorOutlineSegments)
		{
			if (segment != null)
			{
				segment.Color = color;
			}
		}
	}

	private void UpdateMovementGridVisibility()
	{
		if (_movementGridLayer == null)
		{
			return;
		}

		_movementGridLayer.Visible = _combatActive;
		if (_combatActive)
		{
			ApplyFogToMovementGrid();
		}
		else
		{
			foreach (Line2D outline in _movementGridOutlines)
			{
				if (outline != null)
				{
					outline.Visible = false;
				}
			}
		}
	}

	private void EnsureEvacZoneLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_evacZoneLayer = _isoWorld.GetNodeOrNull<Node2D>("EvacZoneLayer");
		if (_evacZoneLayer != null)
		{
			return;
		}

		_evacZoneLayer = new Node2D
		{
			Name = "EvacZoneLayer",
			ZIndex = 3
		};
		_isoWorld.AddChild(_evacZoneLayer);
		_isoWorld.MoveChild(_evacZoneLayer, 1);
	}

	private void EnsureCombatEffectLayer()
	{
		if (_isoWorld == null)
		{
			return;
		}

		_combatEffectLayer = _isoWorld.GetNodeOrNull<Node2D>("CombatEffectLayer");
		if (_combatEffectLayer != null)
		{
			return;
		}

		_combatEffectLayer = new Node2D
		{
			Name = "CombatEffectLayer",
			ZIndex = 30
		};
		_isoWorld.AddChild(_combatEffectLayer);
	}

	private void UpdateEvacZoneHighlightVisuals(float delta)
	{
		if (_evacZoneHighlightPolygons.Count == 0 && _evacZoneHighlightOutlines.Count == 0)
		{
			return;
		}

		_evacPulseClock += delta;
		bool extractionAvailable = GetAvailableExtractionOptions().Count > 0;
		bool allOfficersOnEvac = AreAllOfficersOnEvacZone();
		float pulse = 0.5f + 0.5f * Mathf.Sin(_evacPulseClock * 2.4f);
		float emphasis = allOfficersOnEvac && extractionAvailable ? 1f : extractionAvailable ? 0.55f : 0.2f;
		float fillAlpha = 0.16f + (pulse * 0.16f * emphasis);
		float innerAlpha = 0.12f + (pulse * 0.22f * emphasis);
		float outlineAlpha = 0.48f + (pulse * 0.34f * emphasis);
		float scaleBoost = allOfficersOnEvac && extractionAvailable ? 1.05f + pulse * 0.05f : 1f + pulse * 0.025f;

		foreach (Node2D root in _evacZoneVisualRoots)
		{
			if (root != null)
			{
				root.Scale = new Vector2(scaleBoost, scaleBoost);
			}
		}

		for (int i = 0; i < _evacZoneHighlightPolygons.Count; i++)
		{
			Polygon2D polygon = _evacZoneHighlightPolygons[i];
			if (polygon == null)
			{
				continue;
			}

			bool isInner = (i % 2) == 1;
			polygon.Color = isInner
				? new Color(0.70f, 1.00f, 0.86f, innerAlpha)
				: new Color(0.22f, 0.96f, 0.60f, fillAlpha);
		}

		foreach (Line2D outline in _evacZoneHighlightOutlines)
		{
			if (outline != null)
			{
				outline.DefaultColor = new Color(0.56f, 1.00f, 0.76f, outlineAlpha);
				outline.Width = allOfficersOnEvac && extractionAvailable ? 5f : 4f;
			}
		}
	}

	private void AdjustZoom(float delta)
	{
		if (_camera == null)
		{
			return;
		}

		ApplyZoom(_camera.Zoom.X + delta);
	}

	private void ApplyZoom(float zoomValue)
	{
		if (_camera == null)
		{
			return;
		}

		float clampedZoom = Mathf.Clamp(zoomValue, MinZoom, MaxZoom);
		_camera.Zoom = new Vector2(clampedZoom, clampedZoom);
	}
}

