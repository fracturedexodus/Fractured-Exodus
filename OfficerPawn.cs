using Godot;
using System.Collections.Generic;

public partial class OfficerPawn : Node2D
{
	[Export] public float MoveSpeed = 220f;

	public string OfficerID { get; private set; } = string.Empty;
	public string ShipName { get; private set; } = string.Empty;
	public string OfficerName { get; private set; } = "Officer";
	public string Specialty { get; private set; } = string.Empty;
	public Vector2I CurrentCell { get; private set; } = Vector2I.Zero;

	private Polygon2D _selectionRing;
	private Polygon2D _body;
	private Label _nameLabel;
	private Vector2 _targetPosition;
	private bool _isMoving;
	private readonly Queue<Vector2> _pathPoints = new Queue<Vector2>();

	public override void _Ready()
	{
		BuildVisuals();
		_targetPosition = GlobalPosition;
	}

	public override void _Process(double delta)
	{
		if (!_isMoving)
		{
			return;
		}

		GlobalPosition = GlobalPosition.MoveToward(_targetPosition, MoveSpeed * (float)delta);
		if (GlobalPosition.DistanceTo(_targetPosition) <= 2f)
		{
			GlobalPosition = _targetPosition;
			if (_pathPoints.Count > 0)
			{
				_targetPosition = _pathPoints.Dequeue();
			}
			else
			{
				_isMoving = false;
			}
		}
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
		Specialty = officer.Specialty;

		if (_nameLabel != null)
		{
			_nameLabel.Text = officer.DisplayName;
		}

		if (_body != null)
		{
			_body.Color = GetSpecialtyColor(officer.Specialty);
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
		_targetPosition = targetPosition;
		_isMoving = true;
	}

	public void SetGridCell(Vector2I cell, Vector2 globalPosition)
	{
		CurrentCell = cell;
		_pathPoints.Clear();
		_targetPosition = globalPosition;
		GlobalPosition = globalPosition;
		_isMoving = false;
	}

	public void MoveAlongPath(IReadOnlyList<Vector2> globalPathPoints, Vector2I destinationCell)
	{
		if (globalPathPoints == null || globalPathPoints.Count == 0)
		{
			return;
		}

		_pathPoints.Clear();
		for (int i = 1; i < globalPathPoints.Count; i++)
		{
			_pathPoints.Enqueue(globalPathPoints[i]);
		}

		CurrentCell = destinationCell;
		_targetPosition = globalPathPoints[0];
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
