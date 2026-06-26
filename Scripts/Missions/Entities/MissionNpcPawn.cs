using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionNpcPawn : Node2D, IInteractable
{
	private const int NameLabelZIndex = 220;
	private static readonly Vector2 SelectionRingOffset = new Vector2(0f, 10f);
	[Signal] public delegate void EnteredCellEventHandler(MissionNpcPawn pawn, Vector2I cell);
	[Signal] public delegate void ReachedCellEventHandler(MissionNpcPawn pawn, Vector2I cell);
	[Signal] public delegate void CombatStateChangedEventHandler(MissionNpcPawn pawn);
	[Signal] public delegate void DiedEventHandler(MissionNpcPawn pawn);

	[Export] public NodePath VisualSpritePath { get; set; } = new NodePath("Sprite2D");
	[Export] public NodePath NameLabelPath { get; set; } = new NodePath("NameLabel");
	[Export] public NodePath ShadowPath { get; set; } = new NodePath("Shadow");
	[Export] public float MoveSpeed { get; set; } = 200f;

	public string NpcId { get; private set; } = string.Empty;
	public string DefinitionResourcePath { get; private set; } = string.Empty;
	public string DisplayName { get; private set; } = string.Empty;
	public string Description { get; private set; } = string.Empty;
	public string Notes { get; private set; } = string.Empty;
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
	public List<string> PersonalInventoryItemIDs { get; private set; } = new List<string>();
	public string EquippedMissionWeaponId { get; private set; } = string.Empty;
	public string EquippedMissionShieldId { get; private set; } = string.Empty;
	public List<string> OwnedMissionWeaponIds { get; private set; } = new List<string>();
	public List<string> OwnedMissionShieldIds { get; private set; } = new List<string>();
	public bool IsDead { get; private set; }
	public bool IsExtracted { get; private set; }
	public bool IsMoving => _isMoving;

	private MissionNpcDefinition _definition;
	private Polygon2D _selectionRing;
	private Sprite2D _visualSprite;
	private Texture2D _visualTexture;
	private Sprite2D _coverGhostSprite;
	private Label _nameLabel;
	private Polygon2D _shadow;
	private Vector2 _targetPosition;
	private Vector2I _targetCell = Vector2I.Zero;
	private bool _isMoving;
	private bool _isSelected;
	private Vector2I _pendingDestinationCell = Vector2I.Zero;
	private readonly Queue<Vector2> _pathPoints = new Queue<Vector2>();
	private readonly Queue<Vector2I> _pathCells = new Queue<Vector2I>();
	private readonly Dictionary<string, Texture2D> _directionTextures = new Dictionary<string, Texture2D>();
	private float _animationClock;
	private string _facingDirection = "se";
	private Vector2 _baseVisualPosition = Vector2.Zero;
	private Vector2 _baseVisualScale = new Vector2(0.11f, 0.11f);
	private Color _baseVisualModulate = Colors.White;
	private Vector2 _reactionOffset = Vector2.Zero;
	private float _reactionRotationDegrees;
	private float _reactionStretch;
	private float _reactionFlashStrength;
	private Color _reactionFlashColor = Colors.White;
	private float _facingHoldTimer;
	private bool _useDirectionalTextures;
	private string _defaultWeaponDefinitionId = string.Empty;
	private string _defaultShieldDefinitionId = string.Empty;
	private int _baseAttackMinDamage = 1;
	private int _baseAttackRange = 1;
	private int _baseAttackDamage = 3;
	private string _baseWeaponName = "Claws";
	private string _baseShieldName = "Reactive Screen";
	private int _baseMaxShields = 0;
	private int _baseShieldRechargePerTurn = 1;
	private bool _baseUsesMeleeWeapon;

	public override void _Ready()
	{
		_selectionRing = new Polygon2D
		{
			Visible = false,
			Color = new Color(0.52f, 1f, 0.82f, 0.28f),
			Polygon = BuildDiamond(14f, 8f),
			Position = SelectionRingOffset
		};
		_selectionRing.ZAsRelative = true;
		_selectionRing.ZIndex = -1;
		AddChild(_selectionRing);
		_visualSprite = GetNodeOrNull<Sprite2D>(VisualSpritePath);
		_nameLabel = GetNodeOrNull<Label>(NameLabelPath);
		if (_nameLabel != null)
		{
			_nameLabel.ZAsRelative = false;
			_nameLabel.ZIndex = NameLabelZIndex;
			_nameLabel.Visible = false;
		}
		_shadow = GetNodeOrNull<Polygon2D>(ShadowPath);
		_coverGhostSprite = new Sprite2D
		{
			Name = "CoverGhost",
			Centered = true,
			Visible = false,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			Modulate = new Color(0.55f, 0.95f, 1f, 0.28f)
		};
		AddChild(_coverGhostSprite);
		_targetPosition = GlobalPosition;
	}

	public void SetSelected(bool isSelected)
	{
		_isSelected = isSelected;
		if (_selectionRing != null)
		{
			_selectionRing.Visible = isSelected;
		}
	}

	public override void _Process(double delta)
	{
		float frameDelta = (float)delta;
		_facingHoldTimer = Mathf.Max(0f, _facingHoldTimer - frameDelta);
		if (_isMoving)
		{
			UpdateFacing(_targetPosition - GlobalPosition);
			GlobalPosition = GlobalPosition.MoveToward(_targetPosition, MoveSpeed * frameDelta);
			if (GlobalPosition.DistanceTo(_targetPosition) > 2f)
			{
				UpdateVisualAnimation(frameDelta);
				return;
			}

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

		_reactionOffset = _reactionOffset.MoveToward(Vector2.Zero, 34f * frameDelta);
		_reactionRotationDegrees = Mathf.MoveToward(_reactionRotationDegrees, 0f, 180f * frameDelta);
		_reactionStretch = Mathf.MoveToward(_reactionStretch, 0f, 1.7f * frameDelta);
		_reactionFlashStrength = Mathf.MoveToward(_reactionFlashStrength, 0f, 5.5f * frameDelta);
		UpdateVisualAnimation(frameDelta);
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
		_reactionOffset += away * 4.5f + new Vector2(0f, -1f);
		_reactionRotationDegrees += Mathf.Clamp(away.X * 8f, -8f, 8f);
		_reactionStretch = Mathf.Max(_reactionStretch, 0.1f);
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
		float force = hullHit ? 7f : shieldHit ? 4.5f : 3f;
		_reactionOffset += away * force;
		_reactionRotationDegrees += Mathf.Clamp(away.X * (hullHit ? 10f : 6f), -10f, 10f);
		_reactionStretch = Mathf.Max(_reactionStretch, hullHit ? 0.15f : 0.08f);
		_reactionFlashColor = shieldHit && !hullHit
			? new Color(0.35f, 0.92f, 1f, 1f)
			: new Color(1f, 0.42f, 0.38f, 1f);
		_reactionFlashStrength = Mathf.Max(_reactionFlashStrength, hullHit ? 0.6f : 0.42f);
	}

	public void ApplyDefinition(MissionNpcDefinition definition)
	{
		if (definition == null)
		{
			return;
		}

		_definition = definition;
		DefinitionResourcePath = definition.ResourcePath ?? string.Empty;
		NpcId = definition.NpcId;
		DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NpcId : definition.DisplayName;
		Description = definition.Description ?? string.Empty;
		Notes = definition.Notes ?? string.Empty;
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
		PersonalInventoryItemIDs = (definition.PersonalInventoryItemIDs ?? new Godot.Collections.Array<string>())
			.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
			.ToList();
		OwnedMissionWeaponIds = new List<string>();
		OwnedMissionShieldIds = new List<string>();
		EquippedMissionWeaponId = string.Empty;
		EquippedMissionShieldId = string.Empty;
		ApplyEquipmentDefinitions(definition);

		if (_visualSprite != null)
		{
			_baseVisualModulate = definition.AccentColor;
			_baseVisualScale = new Vector2(definition.VisualScaleMultiplier, definition.VisualScaleMultiplier);
			LoadDirectionalTextures(definition.SpriteTexturePath);
			RefreshVisualTexture();
		}

		if (_coverGhostSprite != null && _visualSprite != null)
		{
			_coverGhostSprite.Modulate = new Color(0.55f, 0.95f, 1f, 0.28f);
		}

		if (_nameLabel != null)
		{
			_nameLabel.Text = DisplayName;
			_nameLabel.Visible = false;
		}

		if (_shadow != null)
		{
			_shadow.Color = new Color(definition.AccentColor.R, definition.AccentColor.G, definition.AccentColor.B, 0.18f);
		}

		ApplyFacingToVisuals();
	}

	private static Texture2D LoadTextureWithoutCache(string resourcePath)
	{
		if (string.IsNullOrWhiteSpace(resourcePath) || !ResourceLoader.Exists(resourcePath))
		{
			return null;
		}

		return ResourceLoader.Load<Texture2D>(resourcePath, string.Empty, ResourceLoader.CacheMode.IgnoreDeep);
	}

	private void LoadDirectionalTextures(string spriteTexturePath)
	{
		_directionTextures.Clear();
		_useDirectionalTextures = false;
		_visualTexture = LoadTextureWithoutCache(spriteTexturePath);
		if (string.IsNullOrWhiteSpace(spriteTexturePath))
		{
			return;
		}

		string normalizedPath = spriteTexturePath.Replace('\\', '/');
		int slashIndex = normalizedPath.LastIndexOf('/');
		int extensionIndex = normalizedPath.LastIndexOf('.');
		if (slashIndex < 0 || extensionIndex <= slashIndex)
		{
			return;
		}

		string directory = normalizedPath[..slashIndex];
		string extension = normalizedPath[extensionIndex..];
		string fileWithoutExtension = normalizedPath[(slashIndex + 1)..extensionIndex];
		if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(fileWithoutExtension) || string.IsNullOrWhiteSpace(directory))
		{
			return;
		}

		string[] suffixes = { "_ne", "_nw", "_se", "_sw" };
		string matchedSuffix = suffixes.FirstOrDefault(fileWithoutExtension.EndsWith);
		if (string.IsNullOrWhiteSpace(matchedSuffix))
		{
			return;
		}

		string baseName = fileWithoutExtension[..^matchedSuffix.Length];
		string[] directions = { "ne", "nw", "se", "sw" };
		foreach (string direction in directions)
		{
			string candidatePath = $"{directory}/{baseName}_{direction}{extension}";
			Texture2D texture = LoadTextureWithoutCache(candidatePath);
			if (texture != null)
			{
				_directionTextures[direction] = texture;
			}
		}

		_useDirectionalTextures = _directionTextures.Count > 0;
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

	public void ApplySavedRuntimeState(
		int currentHp,
		int currentShields,
		int currentActions,
		string activeStatusEffectId,
		bool isDead,
		bool isConsumed,
		bool isExtracted,
		IReadOnlyList<string> personalInventoryItemIds = null,
		IReadOnlyList<string> ownedMissionWeaponIds = null,
		IReadOnlyList<string> ownedMissionShieldIds = null,
		string equippedMissionWeaponId = "",
		string equippedMissionShieldId = "")
	{
		OwnedMissionWeaponIds = (ownedMissionWeaponIds ?? new List<string>())
			.Where(weaponId => !string.IsNullOrWhiteSpace(weaponId))
			.ToList();
		OwnedMissionShieldIds = (ownedMissionShieldIds ?? new List<string>())
			.Where(shieldId => !string.IsNullOrWhiteSpace(shieldId))
			.ToList();
		EquippedMissionWeaponId = equippedMissionWeaponId ?? string.Empty;
		EquippedMissionShieldId = equippedMissionShieldId ?? string.Empty;
		EnsureMissionLoadout();
		ApplyCurrentEquipmentDefinitions();
		CurrentHP = Mathf.Clamp(currentHp, 0, MaxHP);
		CurrentShields = Mathf.Clamp(currentShields, 0, MaxShields);
		CurrentActions = Mathf.Clamp(currentActions, 0, MaxActions);
		ActiveStatusEffectId = activeStatusEffectId ?? string.Empty;
		PersonalInventoryItemIDs = (personalInventoryItemIds ?? new List<string>())
			.Where(itemId => !string.IsNullOrWhiteSpace(itemId))
			.ToList();
		IsConsumed = isConsumed;
		IsExtracted = isExtracted;
		IsDead = isDead || CurrentHP <= 0;
		_pathPoints.Clear();
		_pathCells.Clear();
		_targetCell = CurrentCell;
		_pendingDestinationCell = CurrentCell;
		_targetPosition = GlobalPosition;
		_isMoving = false;

		if (IsConsumed)
		{
			Modulate = new Color(1f, 1f, 1f, 0.45f);
		}
		else
		{
			Modulate = Colors.White;
		}

		if (IsDead || IsExtracted)
		{
			Visible = false;
			SetProcess(false);
			if (_nameLabel != null)
			{
				_nameLabel.Visible = false;
			}
			return;
		}

		Visible = true;
		SetProcess(true);
	}

	public IReadOnlyList<string> GetOwnedWeaponIds()
	{
		EnsureMissionLoadout();
		return OwnedMissionWeaponIds.ToList();
	}

	public IReadOnlyList<string> GetOwnedShieldIds()
	{
		EnsureMissionLoadout();
		return OwnedMissionShieldIds.ToList();
	}

	public IReadOnlyList<string> ApplyCampaignItemUnlocks()
	{
		PersonalInventoryItemIDs ??= new List<string>();
		OwnedMissionWeaponIds ??= new List<string>();
		OwnedMissionShieldIds ??= new List<string>();

		List<string> unlockedDisplayNames = new List<string>();
		List<string> remainingItems = new List<string>();
		foreach (string itemId in PersonalInventoryItemIDs)
		{
			if (string.IsNullOrWhiteSpace(itemId))
			{
				continue;
			}

			CampaignItemDefinition item = CampaignItemRegistry.GetItem(itemId);
			bool itemProvidesEquipment = false;
			if (!string.IsNullOrWhiteSpace(item?.MissionWeaponId) && MissionEquipmentRegistry.GetWeapon(item.MissionWeaponId) != null)
			{
				itemProvidesEquipment = true;
				if (!OwnedMissionWeaponIds.Contains(item.MissionWeaponId))
				{
					OwnedMissionWeaponIds.Add(item.MissionWeaponId);
					unlockedDisplayNames.Add(MissionEquipmentRegistry.GetWeapon(item.MissionWeaponId)?.DisplayName ?? item.MissionWeaponId);
				}
			}

			if (!string.IsNullOrWhiteSpace(item?.MissionShieldId) && MissionEquipmentRegistry.GetShield(item.MissionShieldId) != null)
			{
				itemProvidesEquipment = true;
				if (!OwnedMissionShieldIds.Contains(item.MissionShieldId))
				{
					OwnedMissionShieldIds.Add(item.MissionShieldId);
					unlockedDisplayNames.Add(MissionEquipmentRegistry.GetShield(item.MissionShieldId)?.DisplayName ?? item.MissionShieldId);
				}
			}

			if (!(item?.ConsumeOnUnlock == true && itemProvidesEquipment))
			{
				remainingItems.Add(itemId);
			}
		}

		if (remainingItems.Count != PersonalInventoryItemIDs.Count)
		{
			PersonalInventoryItemIDs = remainingItems;
		}

		EnsureMissionLoadout();
		return unlockedDisplayNames
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Distinct(StringComparer.Ordinal)
			.ToList();
	}

	public bool EquipWeapon(string weaponId)
	{
		if (string.IsNullOrWhiteSpace(weaponId) || MissionEquipmentRegistry.GetWeapon(weaponId) == null)
		{
			return false;
		}

		EnsureMissionLoadout();
		if (!OwnedMissionWeaponIds.Contains(weaponId))
		{
			return false;
		}

		EquippedMissionWeaponId = weaponId;
		ApplyCurrentEquipmentDefinitions();
		EmitSignal(SignalName.CombatStateChanged, this);
		return true;
	}

	public bool EquipShield(string shieldId)
	{
		if (string.IsNullOrWhiteSpace(shieldId) || MissionEquipmentRegistry.GetShield(shieldId) == null)
		{
			return false;
		}

		EnsureMissionLoadout();
		if (!OwnedMissionShieldIds.Contains(shieldId))
		{
			return false;
		}

		EquippedMissionShieldId = shieldId;
		ApplyCurrentEquipmentDefinitions(true);
		EmitSignal(SignalName.CombatStateChanged, this);
		return true;
	}

	public bool RefreshLoadout()
	{
		EnsureMissionLoadout();
		ApplyCurrentEquipmentDefinitions();
		EmitSignal(SignalName.CombatStateChanged, this);
		return true;
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
		Visible = !IsDead && !IsExtracted && isVisible;
		if (_nameLabel != null && _definition != null)
		{
			_nameLabel.Visible = false;
		}
	}

	public void SetCoverOccluded(bool occluded, int overlayZIndex)
	{
		if (_coverGhostSprite == null)
		{
			return;
		}

		bool hasVisualSprite = _visualSprite != null && _visualSprite.Texture != null;
		_coverGhostSprite.Visible = false;
		_coverGhostSprite.ZAsRelative = false;
		_coverGhostSprite.ZIndex = overlayZIndex;

		if (_visualSprite != null)
		{
			_visualSprite.Visible = !occluded && !IsExtracted && hasVisualSprite;
		}

		if (_selectionRing != null)
		{
			_selectionRing.Visible = !occluded && !IsDead && !IsExtracted && _isSelected;
		}

		if (_shadow != null)
		{
			_shadow.Visible = !occluded && !IsExtracted;
		}

		if (_nameLabel != null && _definition != null)
		{
			_nameLabel.Visible = false;
		}
	}

	public void BeginTurn()
	{
		if (IsDead || IsExtracted)
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
		return !IsDead && !IsExtracted && amount > 0 && CurrentActions >= amount;
	}

	public void SpendActions(int amount)
	{
		if (amount <= 0 || IsDead || IsExtracted)
		{
			return;
		}

		CurrentActions = Mathf.Max(0, CurrentActions - amount);
		EmitSignal(SignalName.CombatStateChanged, this);
	}

	public CombatDamageResult ApplyDamage(int damage, int bonusShieldDamage = 0, int directHealthDamage = 0)
	{
		if ((damage <= 0 && bonusShieldDamage <= 0 && directHealthDamage <= 0) || IsDead || IsExtracted)
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
		if (IsDead || IsExtracted || string.IsNullOrWhiteSpace(statusEffectId))
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

	public void SetExtracted()
	{
		if (IsDead || IsExtracted)
		{
			return;
		}

		IsExtracted = true;
		_pathPoints.Clear();
		_pathCells.Clear();
		_isMoving = false;
		Visible = false;
		SetProcess(false);
		if (_nameLabel != null)
		{
			_nameLabel.Visible = false;
		}
		EmitSignal(SignalName.CombatStateChanged, this);
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
		RefreshVisualTexture();
	}

	private void ApplyFacingToVisuals()
	{
		bool flipHorizontally = !_useDirectionalTextures && (_facingDirection == "nw" || _facingDirection == "sw");
		if (_visualSprite != null)
		{
			_visualSprite.FlipH = flipHorizontally;
		}

		if (_coverGhostSprite != null)
		{
			_coverGhostSprite.FlipH = flipHorizontally;
		}
	}

	private void RefreshVisualTexture()
	{
		if (_visualSprite == null)
		{
			return;
		}

		Texture2D facingTexture = null;
		if (_directionTextures.TryGetValue(_facingDirection, out Texture2D directionalTexture))
		{
			facingTexture = directionalTexture;
		}
		else
		{
			facingTexture = _visualTexture;
		}

		_visualSprite.Texture = facingTexture;
		_visualSprite.Visible = facingTexture != null;
		_visualSprite.Modulate = _baseVisualModulate;
		_visualSprite.Scale = _baseVisualScale;
		if (facingTexture != null)
		{
			_baseVisualPosition = new Vector2(0f, -(facingTexture.GetHeight() * _baseVisualScale.Y * 0.5f));
			_visualSprite.Position = _baseVisualPosition;
		}

		if (_coverGhostSprite != null)
		{
			_coverGhostSprite.Texture = facingTexture;
			_coverGhostSprite.Visible = false;
			_coverGhostSprite.Position = _baseVisualPosition;
			_coverGhostSprite.Scale = _baseVisualScale * 1.04f;
		}

		ApplyFacingToVisuals();
	}

	private void UpdateVisualAnimation(float delta)
	{
		_animationClock += delta * (_isMoving ? 8f : 2.4f);
		float swayX = _isMoving ? Mathf.Sin(_animationClock * 0.5f) * 1.6f : Mathf.Sin(_animationClock * 0.35f) * 0.7f;
		float bobY = _isMoving ? Mathf.Abs(Mathf.Sin(_animationClock)) * -5f : Mathf.Sin(_animationClock * 0.8f) * -1.4f;
		float squash = _isMoving ? 1f + Mathf.Sin(_animationClock * 2f) * 0.035f : 1f + Mathf.Sin(_animationClock * 1.4f) * 0.012f;
		squash += _reactionStretch;
		Vector2 animationOffset = new Vector2(swayX, bobY) + _reactionOffset;
		Vector2 animatedScale = new Vector2(_baseVisualScale.X / squash, _baseVisualScale.Y * squash);
		Color flashColor = _baseVisualModulate.Lerp(_reactionFlashColor, _reactionFlashStrength);

		if (_visualSprite != null)
		{
			_visualSprite.Position = _baseVisualPosition + animationOffset;
			_visualSprite.Scale = animatedScale;
			_visualSprite.RotationDegrees = _reactionRotationDegrees;
			_visualSprite.Modulate = flashColor;
		}

		if (_coverGhostSprite != null)
		{
			_coverGhostSprite.Position = _baseVisualPosition + animationOffset;
			_coverGhostSprite.Scale = animatedScale * 1.04f;
			_coverGhostSprite.RotationDegrees = _reactionRotationDegrees;
		}

		if (_shadow != null)
		{
			_shadow.Scale = _isMoving
				? new Vector2(1.03f + Mathf.Sin(_animationClock * 2f) * 0.04f, 0.96f - (_reactionStretch * 0.1f))
				: new Vector2(1f + (_reactionStretch * 0.12f), 1f - (_reactionStretch * 0.08f));
		}
	}

	private string GetBlockedReason(PropInteractionContext context)
	{
		if (_definition == null)
		{
			return "No NPC definition assigned.";
		}

		if (IsExtracted)
		{
			return $"{DisplayName} has already been evacuated.";
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
		_defaultWeaponDefinitionId = definition?.WeaponDefinitionId ?? string.Empty;
		_defaultShieldDefinitionId = definition?.ShieldDefinitionId ?? string.Empty;
		_baseAttackMinDamage = AttackMinDamage;
		_baseAttackRange = AttackRange;
		_baseAttackDamage = AttackDamage;
		_baseWeaponName = WeaponName;
		_baseShieldName = ShieldName;
		_baseMaxShields = MaxShields;
		_baseShieldRechargePerTurn = ShieldRechargePerTurn;
		_baseUsesMeleeWeapon = UsesMeleeWeapon;
		EnsureMissionLoadout();
		ApplyCurrentEquipmentDefinitions(true);
	}

	private void EnsureMissionLoadout()
	{
		OwnedMissionWeaponIds ??= new List<string>();
		OwnedMissionShieldIds ??= new List<string>();
		OwnedMissionWeaponIds = SanitizeOwnedIds(OwnedMissionWeaponIds, MissionEquipmentRegistry.GetWeapon);
		OwnedMissionShieldIds = SanitizeOwnedIds(OwnedMissionShieldIds, MissionEquipmentRegistry.GetShield);

		if (!string.IsNullOrWhiteSpace(_defaultWeaponDefinitionId) && MissionEquipmentRegistry.GetWeapon(_defaultWeaponDefinitionId) != null && !OwnedMissionWeaponIds.Contains(_defaultWeaponDefinitionId))
		{
			OwnedMissionWeaponIds.Add(_defaultWeaponDefinitionId);
		}

		if (!string.IsNullOrWhiteSpace(_defaultShieldDefinitionId) && MissionEquipmentRegistry.GetShield(_defaultShieldDefinitionId) != null && !OwnedMissionShieldIds.Contains(_defaultShieldDefinitionId))
		{
			OwnedMissionShieldIds.Add(_defaultShieldDefinitionId);
		}

		EquippedMissionWeaponId = ResolveEquippedId(EquippedMissionWeaponId, OwnedMissionWeaponIds, _defaultWeaponDefinitionId, MissionEquipmentRegistry.GetWeapon);
		EquippedMissionShieldId = ResolveEquippedId(EquippedMissionShieldId, OwnedMissionShieldIds, _defaultShieldDefinitionId, MissionEquipmentRegistry.GetShield);
	}

	private void ApplyCurrentEquipmentDefinitions(bool refillShields = false)
	{
		WeaponId = string.Empty;
		WeaponName = _baseWeaponName;
		UsesMeleeWeapon = _baseUsesMeleeWeapon;
		AttackRange = Mathf.Max(1, _baseAttackRange);
		AttackMinDamage = Mathf.Max(1, _baseAttackMinDamage);
		AttackDamage = Mathf.Max(AttackMinDamage, _baseAttackDamage);
		BonusShieldDamage = 0;
		ShieldPiercingDamage = 0;
		WeaponStatusEffectId = string.Empty;
		WeaponStatusEffectChance = 0f;
		ShieldName = _baseShieldName;
		MaxShields = Mathf.Max(0, _baseMaxShields);
		ShieldRechargePerTurn = Mathf.Max(0, _baseShieldRechargePerTurn);

		MissionWeaponDefinition weapon = MissionEquipmentRegistry.GetWeapon(EquippedMissionWeaponId);
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

		MissionShieldDefinition shield = MissionEquipmentRegistry.GetShield(EquippedMissionShieldId);
		if (shield != null)
		{
			ShieldName = string.IsNullOrWhiteSpace(shield.DisplayName) ? ShieldName : shield.DisplayName;
			MaxShields = Mathf.Max(0, _baseMaxShields + shield.CapacityBonus);
			CurrentShields = refillShields
				? MaxShields
				: Mathf.Clamp(CurrentShields, 0, MaxShields);
			ShieldRechargePerTurn = Mathf.Max(0, _baseShieldRechargePerTurn + shield.RechargePerTurn);
		}
		else
		{
			CurrentShields = Mathf.Clamp(CurrentShields, 0, MaxShields);
		}
	}

	private static List<string> SanitizeOwnedIds<TDefinition>(IEnumerable<string> ids, Func<string, TDefinition> resolver)
		where TDefinition : class
	{
		HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
		List<string> sanitizedIds = new List<string>();
		foreach (string id in ids ?? Enumerable.Empty<string>())
		{
			if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id) || resolver(id) == null)
			{
				continue;
			}

			sanitizedIds.Add(id);
		}

		return sanitizedIds;
	}

	private static string ResolveEquippedId<TDefinition>(
		string equippedId,
		IReadOnlyList<string> ownedIds,
		string defaultId,
		Func<string, TDefinition> resolver)
		where TDefinition : class
	{
		if (!string.IsNullOrWhiteSpace(equippedId) && resolver(equippedId) != null)
		{
			return equippedId;
		}

		string ownedFallback = ownedIds?.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id) && resolver(id) != null);
		if (!string.IsNullOrWhiteSpace(ownedFallback))
		{
			return ownedFallback;
		}

		return resolver(defaultId) != null ? defaultId : string.Empty;
	}

	private static Vector2[] BuildDiamond(float halfWidth, float halfHeight)
	{
		return new[]
		{
			new Vector2(0f, -halfHeight),
			new Vector2(halfWidth, 0f),
			new Vector2(0f, halfHeight),
			new Vector2(-halfWidth, 0f)
		};
	}
}
