using Godot;
using System.Collections.Generic;

public partial class OfficerPawn : Node2D
{
	private const int NameLabelZIndex = 220;
	private const float SelectionCellHalfWidth = 14f;
	private const float SelectionCellHalfHeight = 8f;
	private static readonly Vector2 SelectionRingOffset = new Vector2(0f, 10f);
	private const string OperativeNorthEastPath = "res://Assets/Missions/Characters/Operative01/operative_ne.png";
	private const string OperativeNorthWestPath = "res://Assets/Missions/Characters/Operative01/operative_nw.png";
	private const string OperativeSouthEastPath = "res://Assets/Missions/Characters/Operative01/operative_se.png";
	private const string OperativeSouthWestPath = "res://Assets/Missions/Characters/Operative01/operative_sw.png";

	[Signal]
	public delegate void EnteredCellEventHandler(OfficerPawn pawn, Vector2I cell);

	[Signal]
	public delegate void ReachedCellEventHandler(OfficerPawn pawn, Vector2I cell);

	[Signal]
	public delegate void CombatStateChangedEventHandler(OfficerPawn pawn);

	[Signal]
	public delegate void DiedEventHandler(OfficerPawn pawn);

	[Export] public float MoveSpeed = 220f;

	public string OfficerID { get; private set; } = string.Empty;
	public string ShipName { get; private set; } = string.Empty;
	public string OfficerName { get; private set; } = "Officer";
	public string PortraitPath { get; private set; } = string.Empty;
	public string Specialty { get; private set; } = string.Empty;
	public string CombatAbilityId { get; private set; } = string.Empty;
	public Vector2I CurrentCell { get; private set; } = Vector2I.Zero;
	public int MaxHP { get; private set; } = 14;
	public int CurrentHP { get; private set; } = 14;
	public int MaxShields { get; private set; } = 5;
	public int CurrentShields { get; private set; } = 5;
	public int MaxActions { get; private set; } = 2;
	public int CurrentActions { get; private set; } = 2;
	public int AttackMinDamage { get; private set; } = 2;
	public int AttackRange { get; private set; } = 3;
	public int AttackDamage { get; private set; } = 4;
	public int BonusShieldDamage { get; private set; }
	public int ShieldPiercingDamage { get; private set; }
	public int InitiativeBonus { get; private set; } = 1;
	public string WeaponId { get; private set; } = string.Empty;
	public string WeaponName { get; private set; } = "Sidearm";
	public string ShieldName { get; private set; } = "Field Aegis";
	public int ShieldRechargePerTurn { get; private set; } = 1;
	public bool UsesMeleeWeapon { get; private set; }
	public string WeaponStatusEffectId { get; private set; } = string.Empty;
	public float WeaponStatusEffectChance { get; private set; }
	public string ActiveStatusEffectId { get; private set; } = string.Empty;
	public bool IsDead { get; private set; }
	public bool IsMoving => _isMoving;

	private Polygon2D _selectionRing;
	private Polygon2D _shadow;
	private Polygon2D _body;
	private Sprite2D _sprite;
	private Sprite2D _coverGhostSprite;
	private Polygon2D _coverGhostBody;
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
	private int _baseMaxShields = 5;
	private int _baseShieldRechargePerTurn = 1;
	private bool _isSelected;
	private Vector2 _reactionOffset = Vector2.Zero;
	private float _reactionRotationDegrees;
	private float _reactionStretch;
	private float _reactionFlashStrength;
	private Color _reactionFlashColor = Colors.White;
	private float _facingHoldTimer;
	private Color _bodyBaseColor = Colors.White;
	private OfficerState _officerState;

	public override void _Ready()
	{
		BuildVisuals();
		_targetPosition = GlobalPosition;
	}

	public override void _Process(double delta)
	{
		float frameDelta = (float)delta;
		_facingHoldTimer = Mathf.Max(0f, _facingHoldTimer - frameDelta);
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

		_reactionOffset = _reactionOffset.MoveToward(Vector2.Zero, 38f * frameDelta);
		_reactionRotationDegrees = Mathf.MoveToward(_reactionRotationDegrees, 0f, 180f * frameDelta);
		_reactionStretch = Mathf.MoveToward(_reactionStretch, 0f, 1.8f * frameDelta);
		_reactionFlashStrength = Mathf.MoveToward(_reactionFlashStrength, 0f, 5.5f * frameDelta);
		UpdateVisualAnimation(frameDelta);
	}

	public void SetOfficer(OfficerState officer)
	{
		if (officer == null)
		{
			return;
		}

		_officerState = officer;
		OfficerID = officer.OfficerID;
		ShipName = officer.ShipName;
		OfficerName = officer.DisplayName;
		PortraitPath = officer.PortraitPath;
		Specialty = officer.Specialty;
		CombatAbilityId = officer.CombatAbilityID;
		ApplyCombatProfileForSpecialty(officer.Specialty);
		OfficerMissionLoadoutService.EnsureOfficerLoadout(officer);
		ApplyMissionLoadout(officer);

		if (_nameLabel != null)
		{
			_nameLabel.Text = officer.DisplayName;
			_nameLabel.Visible = false;
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
			_bodyBaseColor = accentColor;
			_body.Color = accentColor;
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

	public bool RefreshEquippedWeaponFromLoadout()
	{
		if (_officerState == null)
		{
			return false;
		}

		OfficerMissionLoadoutService.EnsureOfficerLoadout(_officerState);
		ApplyMissionWeaponLoadout(OfficerMissionLoadoutService.GetEquippedWeapon(_officerState));
		EmitSignal(SignalName.CombatStateChanged, this);
		return true;
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

	public int RestoreHealthToFull()
	{
		if (IsDead)
		{
			return 0;
		}

		int restoredAmount = Mathf.Max(0, MaxHP - CurrentHP);
		if (restoredAmount <= 0)
		{
			return 0;
		}

		CurrentHP = MaxHP;
		EmitSignal(SignalName.CombatStateChanged, this);
		return restoredAmount;
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

	public void SetSelected(bool isSelected)
	{
		_isSelected = isSelected;
		if (_selectionRing != null)
		{
			_selectionRing.Visible = isSelected;
		}
	}

	public void FaceToward(Vector2 globalTargetPosition, float holdSeconds = 0.3f)
	{
		UpdateFacing(globalTargetPosition - GlobalPosition);
		_facingHoldTimer = Mathf.Max(_facingHoldTimer, holdSeconds);
	}

	public void PlayAttackRecoil(Vector2 globalTargetPosition)
	{
		FaceToward(globalTargetPosition, 0.4f);
		Vector2 away = GlobalPosition - globalTargetPosition;
		if (away.LengthSquared() <= 0.001f)
		{
			away = _facingDirection == "nw" || _facingDirection == "sw"
				? new Vector2(-1f, 0f)
				: new Vector2(1f, 0f);
		}

		away = away.Normalized();
		_reactionOffset += away * 5f + new Vector2(0f, -1.2f);
		_reactionRotationDegrees += Mathf.Clamp(away.X * 8f, -8f, 8f);
		_reactionStretch = Mathf.Max(_reactionStretch, 0.11f);
		_reactionFlashColor = new Color(1f, 0.92f, 0.72f, 1f);
		_reactionFlashStrength = Mathf.Max(_reactionFlashStrength, 0.12f);
	}

	public void PlayHitReaction(Vector2 sourceGlobalPosition, bool shieldHit, bool hullHit)
	{
		Vector2 away = GlobalPosition - sourceGlobalPosition;
		if (away.LengthSquared() <= 0.001f)
		{
			away = Vector2.Up;
		}

		away = away.Normalized();
		float force = hullHit ? 8f : shieldHit ? 5f : 3f;
		_reactionOffset += away * force;
		_reactionRotationDegrees += Mathf.Clamp(away.X * (hullHit ? 12f : 7f), -12f, 12f);
		_reactionStretch = Mathf.Max(_reactionStretch, hullHit ? 0.16f : 0.08f);
		_reactionFlashColor = shieldHit && !hullHit
			? new Color(0.35f, 0.92f, 1f, 1f)
			: new Color(1f, 0.42f, 0.38f, 1f);
		_reactionFlashStrength = Mathf.Max(_reactionFlashStrength, hullHit ? 0.65f : 0.45f);
	}

	public void SetCoverOccluded(bool occluded, int overlayZIndex)
	{
		bool hasSpriteTexture = _sprite != null && _sprite.Texture != null;
		if (_coverGhostSprite != null)
		{
			_coverGhostSprite.Visible = false;
			_coverGhostSprite.ZAsRelative = false;
			_coverGhostSprite.ZIndex = overlayZIndex;
		}

		if (_coverGhostBody != null)
		{
			_coverGhostBody.Visible = false;
			_coverGhostBody.ZAsRelative = false;
			_coverGhostBody.ZIndex = overlayZIndex;
		}

		if (_sprite != null)
		{
			_sprite.Visible = !occluded && hasSpriteTexture;
		}

		if (_body != null)
		{
			_body.Visible = !occluded && !hasSpriteTexture;
		}

		if (_shadow != null)
		{
			_shadow.Visible = !occluded;
		}

		if (_selectionRing != null)
		{
			_selectionRing.Visible = !occluded && _isSelected;
		}

		if (_nameLabel != null)
		{
			_nameLabel.Visible = false;
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

	public void ApplySavedRuntimeState(int currentHp, int currentShields, int currentActions, string activeStatusEffectId, bool isDead)
	{
		CurrentHP = Mathf.Clamp(currentHp, 0, MaxHP);
		CurrentShields = Mathf.Clamp(currentShields, 0, MaxShields);
		CurrentActions = Mathf.Clamp(currentActions, 0, MaxActions);
		ActiveStatusEffectId = activeStatusEffectId ?? string.Empty;
		IsDead = isDead || CurrentHP <= 0;
		_pathPoints.Clear();
		_pathCells.Clear();
		_targetCell = CurrentCell;
		_pendingDestinationCell = CurrentCell;
		_targetPosition = GlobalPosition;
		_isMoving = false;

		if (IsDead)
		{
			Visible = false;
			SetProcess(false);
			return;
		}

		Visible = true;
		SetProcess(true);
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
			Color = new Color(0.15f, 0.95f, 0.95f, 0.30f),
			Polygon = BuildDiamond(SelectionCellHalfWidth, SelectionCellHalfHeight),
			Position = SelectionRingOffset
		};
		_selectionRing.ZAsRelative = true;
		_selectionRing.ZIndex = -1;
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

		_coverGhostSprite = new Sprite2D
		{
			Centered = true,
			Position = Vector2.Zero,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Visible = false,
			Modulate = new Color(0.55f, 0.95f, 1f, 0.32f),
			Scale = _baseSpriteScale * 1.04f
		};
		AddChild(_coverGhostSprite);

		_body = new Polygon2D
		{
			Color = GetSpecialtyColor(Specialty),
			Polygon = BuildDiamond(22f, 36f),
			Position = new Vector2(0f, -18f)
		};
		AddChild(_body);
		_bodyBaseColor = _body.Color;

		_coverGhostBody = new Polygon2D
		{
			Color = new Color(0.55f, 0.95f, 1f, 0.28f),
			Polygon = BuildDiamond(22f, 36f),
			Position = new Vector2(0f, -18f),
			Scale = new Vector2(1.08f, 1.08f),
			Visible = false
		};
		AddChild(_coverGhostBody);

		_nameLabel = new Label
		{
			Text = OfficerName,
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(-90f, 22f),
			Size = new Vector2(180f, 30f),
			Visible = false
		};
		_nameLabel.ZAsRelative = false;
		_nameLabel.ZIndex = NameLabelZIndex;
		_nameLabel.AddThemeFontSizeOverride("font_size", 14);
		_nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.98f, 1f));
		AddChild(_nameLabel);

		LoadDirectionalTextures();
		RefreshSpriteTexture();
	}

	private void ApplyCombatProfileForSpecialty(string specialty)
	{
		AttackMinDamage = 2;
		BonusShieldDamage = 0;
		ShieldPiercingDamage = 0;
		WeaponStatusEffectId = string.Empty;
		WeaponStatusEffectChance = 0f;
		UsesMeleeWeapon = false;
		ShieldRechargePerTurn = 1;
		ShieldName = "Field Aegis";

		switch (specialty)
		{
			case "Medical Triage":
			case "Morale Support":
				MaxHP = 16;
				MaxShields = 4;
				MaxActions = 2;
				AttackMinDamage = 2;
				AttackRange = 1;
				AttackDamage = 3;
				InitiativeBonus = 0;
				WeaponName = "Shock Baton";
				break;
			case "Salvage Efficiency":
			case "Engine Routing":
				MaxHP = 15;
				MaxShields = 5;
				MaxActions = 2;
				AttackMinDamage = 2;
				AttackRange = 1;
				AttackDamage = 4;
				InitiativeBonus = 1;
				WeaponName = "Cutting Rig";
				break;
			case "Missile Control":
				MaxHP = 13;
				MaxShields = 6;
				MaxActions = 2;
				AttackMinDamage = 3;
				AttackRange = 4;
				AttackDamage = 5;
				InitiativeBonus = 1;
				WeaponName = "Heavy Sidearm";
				break;
			case "Tactical Command":
				MaxHP = 14;
				MaxShields = 6;
				MaxActions = 2;
				AttackMinDamage = 3;
				AttackRange = 4;
				AttackDamage = 5;
				InitiativeBonus = 2;
				WeaponName = "Pulse Carbine";
				break;
			case "Shield Tuning":
				MaxHP = 17;
				MaxShields = 8;
				MaxActions = 2;
				AttackMinDamage = 2;
				AttackRange = 2;
				AttackDamage = 4;
				InitiativeBonus = 0;
				WeaponName = "Defense Pistol";
				ShieldRechargePerTurn = 2;
				break;
			default:
				MaxHP = 14;
				MaxShields = 5;
				MaxActions = 2;
				AttackMinDamage = 2;
				AttackRange = 3;
				AttackDamage = 4;
				InitiativeBonus = 1;
				WeaponName = "Sidearm";
				break;
		}

		_baseMaxShields = MaxShields;
		_baseShieldRechargePerTurn = ShieldRechargePerTurn;
		CurrentHP = MaxHP;
		CurrentShields = MaxShields;
		CurrentActions = MaxActions;
	}

	private void ApplyMissionLoadout(OfficerState officer)
	{
		ApplyMissionWeaponLoadout(OfficerMissionLoadoutService.GetEquippedWeapon(officer));
		ApplyMissionShieldLoadout(OfficerMissionLoadoutService.GetEquippedShield(officer));
	}

	private void ApplyMissionWeaponLoadout(MissionWeaponDefinition weapon)
	{
		if (weapon == null)
		{
			return;
		}

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

	private void ApplyMissionShieldLoadout(MissionShieldDefinition shield)
	{
		if (shield == null)
		{
			return;
		}

		ShieldName = string.IsNullOrWhiteSpace(shield.DisplayName) ? ShieldName : shield.DisplayName;
		MaxShields = Mathf.Max(0, _baseMaxShields + shield.CapacityBonus);
		CurrentShields = MaxShields;
		ShieldRechargePerTurn = Mathf.Max(0, _baseShieldRechargePerTurn + shield.RechargePerTurn);
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
			if (_coverGhostSprite != null)
			{
				_coverGhostSprite.Texture = null;
				_coverGhostSprite.Visible = false;
			}
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
		if (_coverGhostSprite != null)
		{
			_coverGhostSprite.Texture = texture;
			_coverGhostSprite.Position = _baseSpritePosition;
			_coverGhostSprite.Scale = _baseSpriteScale * 1.04f;
		}
	}

	private void UpdateFacing(Vector2 moveVector)
	{
		if (_facingHoldTimer > 0f && !_isMoving)
		{
			return;
		}

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
		float swayX = _isMoving ? Mathf.Sin(_animationClock * 0.5f) * 1.6f : Mathf.Sin(_animationClock * 0.35f) * 0.7f;
		float bobY = _isMoving ? Mathf.Abs(Mathf.Sin(_animationClock)) * -5f : Mathf.Sin(_animationClock * 0.8f) * -1.4f;
		float squash = _isMoving ? 1f + Mathf.Sin(_animationClock * 2f) * 0.035f : 1f + Mathf.Sin(_animationClock * 1.4f) * 0.012f;
		squash += _reactionStretch;
		Vector2 animationOffset = new Vector2(swayX, bobY) + _reactionOffset;
		Vector2 animatedScale = new Vector2(_baseSpriteScale.X / squash, _baseSpriteScale.Y * squash);
		Color flashColor = Colors.White.Lerp(_reactionFlashColor, _reactionFlashStrength);

		if (_sprite != null && _sprite.Visible)
		{
			_sprite.Position = _baseSpritePosition + animationOffset;
			_sprite.Scale = animatedScale;
			_sprite.RotationDegrees = _reactionRotationDegrees;
			_sprite.Modulate = flashColor;
		}
		if (_coverGhostSprite != null)
		{
			_coverGhostSprite.Position = _baseSpritePosition + animationOffset;
			_coverGhostSprite.Scale = animatedScale * 1.04f;
			_coverGhostSprite.RotationDegrees = _reactionRotationDegrees;
		}
		if (_body != null && _body.Visible)
		{
			_body.Position = new Vector2(0f, -18f) + animationOffset;
			_body.Scale = new Vector2(1f / squash, squash);
			_body.RotationDegrees = _reactionRotationDegrees;
			_body.Color = _bodyBaseColor.Lerp(_reactionFlashColor, _reactionFlashStrength);
		}
		if (_coverGhostBody != null)
		{
			_coverGhostBody.Position = new Vector2(0f, -18f) + animationOffset;
			_coverGhostBody.Scale = new Vector2(1.08f / squash, 1.08f * squash);
			_coverGhostBody.RotationDegrees = _reactionRotationDegrees;
		}
		_shadow.Scale = _isMoving
			? new Vector2(1.03f + Mathf.Sin(_animationClock * 2f) * 0.04f, 0.96f - (_reactionStretch * 0.1f))
			: new Vector2(1f + (_reactionStretch * 0.12f), 1f - (_reactionStretch * 0.08f));
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
