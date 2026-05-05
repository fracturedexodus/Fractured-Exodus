using Godot;
using System.Collections.Generic;

public partial class OfficerPawn : Node2D
{
	private const string OperativeNorthEastPath = "res://Assets/Missions/Characters/Operative01/operative_ne.png";
	private const string OperativeNorthWestPath = "res://Assets/Missions/Characters/Operative01/operative_nw.png";
	private const string OperativeSouthEastPath = "res://Assets/Missions/Characters/Operative01/operative_se.png";
	private const string OperativeSouthWestPath = "res://Assets/Missions/Characters/Operative01/operative_sw.png";

	[Signal]
	public delegate void EnteredCellEventHandler(OfficerPawn pawn, Vector2I cell);

	[Signal]
	public delegate void ReachedCellEventHandler(OfficerPawn pawn, Vector2I cell);

	[Export] public float MoveSpeed = 220f;

	public string OfficerID { get; private set; } = string.Empty;
	public string ShipName { get; private set; } = string.Empty;
	public string OfficerName { get; private set; } = "Officer";
	public string PortraitPath { get; private set; } = string.Empty;
	public string Specialty { get; private set; } = string.Empty;
	public Vector2I CurrentCell { get; private set; } = Vector2I.Zero;

	private Polygon2D _selectionRing;
	private Polygon2D _shadow;
	private Polygon2D _body;
	private Sprite2D _sprite;
	private Label _nameLabel;
	private Vector2 _targetPosition;
	private Vector2I _targetCell = Vector2I.Zero;
	private bool _isMoving;
	private Vector2I _pendingDestinationCell = Vector2I.Zero;
	private readonly Queue<Vector2> _pathPoints = new Queue<Vector2>();
	private readonly Queue<Vector2I> _pathCells = new Queue<Vector2I>();
	private readonly Dictionary<string, Texture2D> _directionTextures = new Dictionary<string, Texture2D>();
	private string _facingDirection = "se";
	private float _animationClock;
	private Vector2 _baseSpritePosition = Vector2.Zero;
	private Vector2 _baseSpriteScale = new Vector2(0.11f, 0.11f);

	public override void _Ready()
	{
		BuildVisuals();
		_targetPosition = GlobalPosition;
	}

	public override void _Process(double delta)
	{
		float frameDelta = (float)delta;
		if (_isMoving)
		{
			UpdateFacing(_targetPosition - GlobalPosition);
			GlobalPosition = GlobalPosition.MoveToward(_targetPosition, MoveSpeed * frameDelta);
			if (GlobalPosition.DistanceTo(_targetPosition) <= 2f)
			{
				GlobalPosition = _targetPosition;
				CurrentCell = _targetCell;
				EmitSignal(SignalName.EnteredCell, this, CurrentCell);
				if (_pathPoints.Count > 0)
				{
					_targetPosition = _pathPoints.Dequeue();
					_targetCell = _pathCells.Dequeue();
					UpdateFacing(_targetPosition - GlobalPosition);
				}
				else
				{
					_isMoving = false;
					CurrentCell = _pendingDestinationCell;
					EmitSignal(SignalName.ReachedCell, this, CurrentCell);
				}
			}
		}

		UpdateVisualAnimation(frameDelta);
	}

	public void SetOfficer(OfficerState officer)
	{
		if (officer == null)
		{
			return;
		}

		OfficerID = officer.OfficerID;
		ShipName = officer.ShipName;
		OfficerName = officer.DisplayName;
		PortraitPath = officer.PortraitPath;
		Specialty = officer.Specialty;

		if (_nameLabel != null)
		{
			_nameLabel.Text = officer.DisplayName;
		}

		Color accentColor = GetSpecialtyColor(officer.Specialty);
		if (_selectionRing != null)
		{
			_selectionRing.Color = new Color(accentColor.R, accentColor.G, accentColor.B, 0.35f);
		}

		if (_shadow != null)
		{
			_shadow.Color = new Color(accentColor.R, accentColor.G, accentColor.B, 0.18f);
		}

		if (_body != null)
		{
			_body.Color = accentColor;
		}
	}

	public void SetSelected(bool isSelected)
	{
		if (_selectionRing != null)
		{
			_selectionRing.Visible = isSelected;
		}
	}

	public void MoveTo(Vector2 targetPosition)
	{
		_pathPoints.Clear();
		_pathCells.Clear();
		_targetPosition = targetPosition;
		_targetCell = CurrentCell;
		_isMoving = true;
	}

	public void SetGridCell(Vector2I cell, Vector2 globalPosition)
	{
		CurrentCell = cell;
		_pathPoints.Clear();
		_pathCells.Clear();
		_targetPosition = globalPosition;
		_targetCell = cell;
		GlobalPosition = globalPosition;
		_pendingDestinationCell = cell;
		_isMoving = false;
	}

	public void MoveAlongPath(IReadOnlyList<Vector2> globalPathPoints, IReadOnlyList<Vector2I> pathCells, Vector2I destinationCell)
	{
		if (globalPathPoints == null || pathCells == null || globalPathPoints.Count == 0 || globalPathPoints.Count != pathCells.Count)
		{
			return;
		}

		_pathPoints.Clear();
		_pathCells.Clear();
		for (int i = 1; i < globalPathPoints.Count; i++)
		{
			_pathPoints.Enqueue(globalPathPoints[i]);
			_pathCells.Enqueue(pathCells[i]);
		}

		_pendingDestinationCell = destinationCell;
		_targetPosition = globalPathPoints[0];
		_targetCell = pathCells[0];
		_isMoving = true;
	}

	private void BuildVisuals()
	{
		_selectionRing = new Polygon2D
		{
			Visible = false,
			Color = new Color(0.15f, 0.95f, 0.95f, 0.35f),
			Polygon = BuildDiamond(34f, 18f)
		};
		AddChild(_selectionRing);

		_shadow = new Polygon2D
		{
			Color = new Color(0f, 0f, 0f, 0.22f),
			Polygon = BuildEllipse(22f, 10f, 20),
			Position = new Vector2(0f, 2f)
		};
		AddChild(_shadow);

		_sprite = new Sprite2D
		{
			Centered = true,
			Position = Vector2.Zero,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear
		};
		AddChild(_sprite);

		_body = new Polygon2D
		{
			Color = GetSpecialtyColor(Specialty),
			Polygon = BuildDiamond(22f, 36f),
			Position = new Vector2(0f, -18f)
		};
		AddChild(_body);

		_nameLabel = new Label
		{
			Text = OfficerName,
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(-90f, 22f),
			Size = new Vector2(180f, 30f)
		};
		_nameLabel.AddThemeFontSizeOverride("font_size", 14);
		_nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.98f, 1f));
		AddChild(_nameLabel);

		LoadDirectionalTextures();
		RefreshSpriteTexture();
	}

	private Vector2[] BuildDiamond(float halfWidth, float halfHeight)
	{
		return new[]
		{
			new Vector2(0f, -halfHeight),
			new Vector2(halfWidth, 0f),
			new Vector2(0f, halfHeight),
			new Vector2(-halfWidth, 0f)
		};
	}

	private Vector2[] BuildEllipse(float radiusX, float radiusY, int segments)
	{
		Vector2[] points = new Vector2[segments];
		for (int i = 0; i < segments; i++)
		{
			float angle = Mathf.Tau * i / segments;
			points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
		}

		return points;
	}

	private void LoadDirectionalTextures()
	{
		_directionTextures.Clear();
		_directionTextures["ne"] = GD.Load<Texture2D>(OperativeNorthEastPath);
		_directionTextures["nw"] = GD.Load<Texture2D>(OperativeNorthWestPath);
		_directionTextures["se"] = GD.Load<Texture2D>(OperativeSouthEastPath);
		_directionTextures["sw"] = GD.Load<Texture2D>(OperativeSouthWestPath);
	}

	private void RefreshSpriteTexture()
	{
		if (_sprite == null)
		{
			return;
		}

		if (!_directionTextures.TryGetValue(_facingDirection, out Texture2D texture) || texture == null)
		{
			_sprite.Texture = null;
			_sprite.Visible = false;
			if (_body != null)
			{
				_body.Visible = true;
			}

			return;
		}

		_sprite.Texture = texture;
		_sprite.Visible = true;
		_body.Visible = false;
		_baseSpritePosition = new Vector2(0f, -(texture.GetHeight() * _baseSpriteScale.Y * 0.5f));
		_sprite.Position = _baseSpritePosition;
		_sprite.Scale = _baseSpriteScale;
	}

	private void UpdateFacing(Vector2 moveVector)
	{
		if (moveVector.LengthSquared() <= 4f)
		{
			return;
		}

		string nextFacing = moveVector.Y < 0f
			? (moveVector.X >= 0f ? "ne" : "nw")
			: (moveVector.X >= 0f ? "se" : "sw");
		if (nextFacing == _facingDirection)
		{
			return;
		}

		_facingDirection = nextFacing;
		RefreshSpriteTexture();
	}

	private void UpdateVisualAnimation(float delta)
	{
		_animationClock += delta * (_isMoving ? 8f : 2.4f);
		if (_sprite == null || !_sprite.Visible)
		{
			return;
		}

		float swayX = _isMoving ? Mathf.Sin(_animationClock * 0.5f) * 1.6f : Mathf.Sin(_animationClock * 0.35f) * 0.7f;
		float bobY = _isMoving ? Mathf.Abs(Mathf.Sin(_animationClock)) * -5f : Mathf.Sin(_animationClock * 0.8f) * -1.4f;
		float squash = _isMoving ? 1f + Mathf.Sin(_animationClock * 2f) * 0.035f : 1f + Mathf.Sin(_animationClock * 1.4f) * 0.012f;

		_sprite.Position = _baseSpritePosition + new Vector2(swayX, bobY);
		_sprite.Scale = new Vector2(_baseSpriteScale.X / squash, _baseSpriteScale.Y * squash);
		_shadow.Scale = _isMoving
			? new Vector2(1.03f + Mathf.Sin(_animationClock * 2f) * 0.04f, 0.96f)
			: new Vector2(1f, 1f);
	}

	private Color GetSpecialtyColor(string specialty)
	{
		return specialty switch
		{
			"Medical Triage" => new Color(0.5f, 0.95f, 0.7f),
			"Salvage Efficiency" => new Color(0.95f, 0.75f, 0.35f),
			"Shield Tuning" => new Color(0.45f, 0.8f, 1f),
			"Missile Control" => new Color(1f, 0.55f, 0.45f),
			"Morale Support" => new Color(0.95f, 0.6f, 0.95f),
			_ => new Color(0.75f, 0.82f, 0.9f)
		};
	}
}
