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
	public int MaxShields { get; private set; } = 4;
	public int CurrentShields { get; private set; } = 4;
	public int MaxActions { get; private set; } = 2;
	public int CurrentActions { get; private set; } = 2;
	public int AttackMinDamage { get; private set; } = 1;
	public int AttackRange { get; private set; } = 1;
	public int AttackDamage { get; private set; } = 3;
	public int BonusShieldDamage { get; private set; }
	public int ShieldPiercingDamage { get; private set; }
	public int InitiativeBonus { get; private set; }
	public string WeaponId { get; private set; } = string.Empty;
	public string WeaponName { get; private set; } = "Claws";
	public string ShieldName { get; private set; } = "Reactive Screen";
	public int ShieldRechargePerTurn { get; private set; } = 1;
	public bool UsesMeleeWeapon { get; private set; }
	public string WeaponStatusEffectId { get; private set; } = string.Empty;
	public float WeaponStatusEffectChance { get; private set; }
	public string ActiveStatusEffectId { get; private set; } = string.Empty;
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
		MaxShields = Mathf.Max(0, definition.MaxShields);
		if (MaxShields <= 0 && definition.IsHostile)
		{
			MaxShields = 5;
		}

		CurrentShields = MaxShields;
		MaxActions = Mathf.Max(1, definition.MaxActions);
		CurrentActions = MaxActions;
		AttackMinDamage = Mathf.Max(1, definition.AttackDamage / 2);
		AttackRange = Mathf.Max(1, definition.AttackRange);
		AttackDamage = Mathf.Max(1, definition.AttackDamage);
		InitiativeBonus = definition.InitiativeBonus;
		WeaponName = string.IsNullOrWhiteSpace(definition.WeaponName) ? "Claws" : definition.WeaponName;
		BonusShieldDamage = 0;
		ShieldPiercingDamage = 0;
		ShieldRechargePerTurn = 1;
		ShieldName = MaxShields > 0 ? "Reactive Screen" : "No Shields";
		UsesMeleeWeapon = AttackRange <= 1;
		WeaponStatusEffectId = string.Empty;
		WeaponStatusEffectChance = 0f;
		ApplyEquipmentDefinitions(definition);

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
		if (ShieldRechargePerTurn > 0 && CurrentShields < MaxShields)
		{
			CurrentShields = Mathf.Clamp(CurrentShields + ShieldRechargePerTurn, 0, MaxShields);
		}

		if (ActiveStatusEffectId == "disrupted")
		{
			CurrentActions = Mathf.Max(0, CurrentActions - 1);
			ActiveStatusEffectId = string.Empty;
		}

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

	public CombatDamageResult ApplyDamage(int damage, int bonusShieldDamage = 0, int directHealthDamage = 0)
	{
		if ((damage <= 0 && bonusShieldDamage <= 0 && directHealthDamage <= 0) || IsDead)
		{
			return new CombatDamageResult
			{
				IncomingDamage = Mathf.Max(0, damage) + Mathf.Max(0, bonusShieldDamage) + Mathf.Max(0, directHealthDamage),
				RemainingShields = CurrentShields,
				RemainingHealth = CurrentHP,
				WasFatal = IsDead
			};
		}

		int shieldDamage = Mathf.Min(CurrentShields, Mathf.Max(0, damage) + Mathf.Max(0, bonusShieldDamage));
		CurrentShields = Mathf.Max(0, CurrentShields - shieldDamage);
		int baseDamageAbsorbedByShields = Mathf.Min(Mathf.Max(0, damage), shieldDamage);
		int remainingDamage = Mathf.Max(0, damage - baseDamageAbsorbedByShields);
		int healthDamage = Mathf.Min(CurrentHP, remainingDamage + Mathf.Max(0, directHealthDamage));
		CurrentHP = Mathf.Max(0, CurrentHP - healthDamage);
		if (CurrentHP <= 0)
		{
			IsDead = true;
			Visible = false;
			SetProcess(false);
			EmitSignal(SignalName.Died, this);
		}

		EmitSignal(SignalName.CombatStateChanged, this);
		return new CombatDamageResult
		{
			IncomingDamage = Mathf.Max(0, damage) + Mathf.Max(0, bonusShieldDamage) + Mathf.Max(0, directHealthDamage),
			ShieldDamage = shieldDamage,
			HealthDamage = healthDamage,
			RemainingShields = CurrentShields,
			RemainingHealth = CurrentHP,
			WasFatal = IsDead
		};
	}

	public MissionAttackProfile GetAttackProfile()
	{
		return new MissionAttackProfile
		{
			WeaponId = WeaponId,
			WeaponName = WeaponName,
			IsMelee = UsesMeleeWeapon,
			Range = AttackRange,
			MinDamage = AttackMinDamage,
			MaxDamage = AttackDamage,
			BonusShieldDamage = BonusShieldDamage,
			ShieldPiercingDamage = ShieldPiercingDamage,
			StatusEffectId = WeaponStatusEffectId,
			StatusEffectChance = WeaponStatusEffectChance
		};
	}

	public bool TryApplyStatusEffect(string statusEffectId)
	{
		if (IsDead || string.IsNullOrWhiteSpace(statusEffectId))
		{
			return false;
		}

		ActiveStatusEffectId = statusEffectId;
		EmitSignal(SignalName.CombatStateChanged, this);
		return true;
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

	private void ApplyEquipmentDefinitions(MissionNpcDefinition definition)
	{
		MissionWeaponDefinition weapon = MissionEquipmentRegistry.GetWeapon(definition.WeaponDefinitionId);
		if (weapon != null)
		{
			WeaponId = weapon.WeaponId ?? string.Empty;
			WeaponName = string.IsNullOrWhiteSpace(weapon.DisplayName) ? WeaponName : weapon.DisplayName;
			UsesMeleeWeapon = weapon.IsMelee;
			AttackRange = Mathf.Max(1, weapon.AttackRange);
			AttackMinDamage = Mathf.Max(1, weapon.MinDamage);
			AttackDamage = Mathf.Max(AttackMinDamage, weapon.MaxDamage);
			BonusShieldDamage = Mathf.Max(0, weapon.BonusShieldDamage);
			ShieldPiercingDamage = Mathf.Max(0, weapon.ShieldPiercingDamage);
			WeaponStatusEffectId = weapon.StatusEffectId ?? string.Empty;
			WeaponStatusEffectChance = Mathf.Clamp(weapon.StatusEffectChance, 0f, 1f);
		}

		MissionShieldDefinition shield = MissionEquipmentRegistry.GetShield(definition.ShieldDefinitionId);
		if (shield != null)
		{
			ShieldName = string.IsNullOrWhiteSpace(shield.DisplayName) ? ShieldName : shield.DisplayName;
			MaxShields = Mathf.Max(0, MaxShields + shield.CapacityBonus);
			CurrentShields = MaxShields;
			ShieldRechargePerTurn = Mathf.Max(0, shield.RechargePerTurn);
		}
	}
}
