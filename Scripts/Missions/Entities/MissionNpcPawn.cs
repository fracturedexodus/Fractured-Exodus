using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class MissionNpcPawn : Node2D, IInteractable
{
	[Signal] public delegate void EnteredCellEventHandler(MissionNpcPawn pawn, Vector2I cell);
	[Signal] public delegate void ReachedCellEventHandler(MissionNpcPawn pawn, Vector2I cell);
	[Signal] public delegate void CombatStateChangedEventHandler(MissionNpcPawn pawn);
	[Signal] public delegate void DiedEventHandler(MissionNpcPawn pawn);

	[Export] public NodePath VisualSpritePath { get; set; } = new NodePath("Sprite2D");
	[Export] public NodePath NameLabelPath { get; set; } = new NodePath("NameLabel");
	[Export] public NodePath ShadowPath { get; set; } = new NodePath("Shadow");
	[Export] public float MoveSpeed { get; set; } = 200f;

	public string NpcId { get; private set; } = string.Empty;
	public string DisplayName { get; private set; } = string.Empty;
	public string PortraitPath { get; private set; } = string.Empty;
	public string DialogueId { get; private set; } = string.Empty;
	public int InteractionRange { get; private set; } = 1;
	public Vector2I CurrentCell { get; private set; } = Vector2I.Zero;
	public bool IsConsumed { get; private set; }
	public bool IsHostile => _definition?.IsHostile == true;
	public int MaxHP { get; private set; } = 10;
	public int CurrentHP { get; private set; } = 10;
	public int MaxActions { get; private set; } = 2;
	public int CurrentActions { get; private set; } = 2;
	public int AttackRange { get; private set; } = 1;
	public int AttackDamage { get; private set; } = 3;
	public int InitiativeBonus { get; private set; }
	public string WeaponName { get; private set; } = "Claws";
	public bool IsDead { get; private set; }
	public bool IsMoving => _isMoving;

	private MissionNpcDefinition _definition;
	private Sprite2D _visualSprite;
	private Label _nameLabel;
	private Polygon2D _shadow;
	private Vector2 _targetPosition;
	private Vector2I _targetCell = Vector2I.Zero;
	private bool _isMoving;
	private Vector2I _pendingDestinationCell = Vector2I.Zero;
	private readonly Queue<Vector2> _pathPoints = new Queue<Vector2>();
	private readonly Queue<Vector2I> _pathCells = new Queue<Vector2I>();

	public override void _Ready()
	{
		_visualSprite = GetNodeOrNull<Sprite2D>(VisualSpritePath);
		_nameLabel = GetNodeOrNull<Label>(NameLabelPath);
		_shadow = GetNodeOrNull<Polygon2D>(ShadowPath);
		_targetPosition = GlobalPosition;
	}

	public override void _Process(double delta)
	{
		if (!_isMoving)
		{
			return;
		}

		float frameDelta = (float)delta;
		GlobalPosition = GlobalPosition.MoveToward(_targetPosition, MoveSpeed * frameDelta);
		if (GlobalPosition.DistanceTo(_targetPosition) > 2f)
		{
			return;
		}

		GlobalPosition = _targetPosition;
		CurrentCell = _targetCell;
		EmitSignal(SignalName.EnteredCell, this, CurrentCell);
		if (_pathPoints.Count > 0)
		{
			_targetPosition = _pathPoints.Dequeue();
			_targetCell = _pathCells.Dequeue();
			return;
		}

		_isMoving = false;
		CurrentCell = _pendingDestinationCell;
		EmitSignal(SignalName.ReachedCell, this, CurrentCell);
	}

	public void ApplyDefinition(MissionNpcDefinition definition)
	{
		if (definition == null)
		{
			return;
		}

		_definition = definition;
		NpcId = definition.NpcId;
		DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NpcId : definition.DisplayName;
		PortraitPath = definition.PortraitPath ?? string.Empty;
		DialogueId = definition.DefaultDialogueId ?? string.Empty;
		InteractionRange = Mathf.Max(1, definition.InteractionRange);
		MaxHP = Mathf.Max(1, definition.MaxHP);
		CurrentHP = MaxHP;
		MaxActions = Mathf.Max(1, definition.MaxActions);
		CurrentActions = MaxActions;
		AttackRange = Mathf.Max(1, definition.AttackRange);
		AttackDamage = Mathf.Max(1, definition.AttackDamage);
		InitiativeBonus = definition.InitiativeBonus;
		WeaponName = string.IsNullOrWhiteSpace(definition.WeaponName) ? "Claws" : definition.WeaponName;

		if (_visualSprite != null)
		{
			_visualSprite.Texture = string.IsNullOrWhiteSpace(definition.SpriteTexturePath)
				? null
				: GD.Load<Texture2D>(definition.SpriteTexturePath);
			_visualSprite.Modulate = definition.AccentColor;
			_visualSprite.Scale = new Vector2(definition.VisualScaleMultiplier, definition.VisualScaleMultiplier);
			if (_visualSprite.Texture != null)
			{
				_visualSprite.Position = new Vector2(0f, -(_visualSprite.Texture.GetHeight() * definition.VisualScaleMultiplier * 0.5f));
			}
		}

		if (_nameLabel != null)
		{
			_nameLabel.Text = DisplayName;
			_nameLabel.Visible = definition.ShowNameLabel;
		}

		if (_shadow != null)
		{
			_shadow.Color = new Color(definition.AccentColor.R, definition.AccentColor.G, definition.AccentColor.B, 0.18f);
		}
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

	public void SetFogVisibility(bool isVisible)
	{
		Visible = !IsDead && isVisible;
		if (_nameLabel != null && _definition != null)
		{
			_nameLabel.Visible = !IsDead && isVisible && _definition.ShowNameLabel;
		}
	}

	public void BeginTurn()
	{
		if (IsDead)
		{
			return;
		}

		CurrentActions = MaxActions;
		EmitSignal(SignalName.CombatStateChanged, this);
	}

	public bool CanSpendActions(int amount)
	{
		return !IsDead && amount > 0 && CurrentActions >= amount;
	}

	public void SpendActions(int amount)
	{
		if (amount <= 0 || IsDead)
		{
			return;
		}

		CurrentActions = Mathf.Max(0, CurrentActions - amount);
		EmitSignal(SignalName.CombatStateChanged, this);
	}

	public void ApplyDamage(int damage)
	{
		if (damage <= 0 || IsDead)
		{
			return;
		}

		CurrentHP = Mathf.Max(0, CurrentHP - damage);
		if (CurrentHP <= 0)
		{
			IsDead = true;
			Visible = false;
			SetProcess(false);
			EmitSignal(SignalName.Died, this);
		}

		EmitSignal(SignalName.CombatStateChanged, this);
	}

	public bool CanInteract(PropInteractionContext context)
	{
		return string.IsNullOrEmpty(GetBlockedReason(context));
	}

	public PropInteractionResult Interact(PropInteractionContext context)
	{
		string blockedReason = GetBlockedReason(context);
		if (!string.IsNullOrEmpty(blockedReason))
		{
			return PropInteractionResult.Blocked(blockedReason);
		}

		PropInteractionResult result = PropInteractionResult.Completed(_definition?.SuccessMessage ?? string.Empty);
		result.DialogueId = _definition?.DefaultDialogueId ?? string.Empty;
		if (_definition?.SetFlags != null)
		{
			foreach (string flag in _definition.SetFlags.Where(flag => !string.IsNullOrWhiteSpace(flag)))
			{
				if (!result.FlagsToSet.Contains(flag))
				{
					result.FlagsToSet.Add(flag);
				}
			}
		}

		if (_definition?.OneShot == true)
		{
			result.ConsumeProp = true;
		}

		return result;
	}

	public void CommitInteractionResult(PropInteractionResult result, PropInteractionContext context)
	{
		if (result?.Success != true)
		{
			return;
		}

		if (context?.GlobalData != null && result.Reward != null)
		{
			new CampaignRewardService(context.GlobalData).ApplyReward(result.Reward, context.Officer?.OfficerID ?? string.Empty);
		}

		if (result.ConsumeProp)
		{
			IsConsumed = true;
			Modulate = new Color(1f, 1f, 1f, 0.45f);
		}
	}

	private string GetBlockedReason(PropInteractionContext context)
	{
		if (_definition == null)
		{
			return "No NPC definition assigned.";
		}

		if (IsHostile)
		{
			return $"{DisplayName} is hostile.";
		}

		if (IsConsumed)
		{
			return $"{DisplayName} has nothing more to say.";
		}

		if (context?.Officer == null)
		{
			return "No officer is available to interact.";
		}

		if (_definition.RequiredFlags == null || _definition.RequiredFlags.Count == 0)
		{
			return string.Empty;
		}

		if (context.GlobalData?.StoryFlags == null)
		{
			return $"{DisplayName} is unavailable right now.";
		}

		foreach (string requiredFlag in _definition.RequiredFlags)
		{
			if (!string.IsNullOrWhiteSpace(requiredFlag) && !context.GlobalData.StoryFlags.Contains(requiredFlag))
			{
				return $"{DisplayName} is unavailable right now.";
			}
		}

		return string.Empty;
	}
}
