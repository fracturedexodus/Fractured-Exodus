using Godot;
using System.Linq;

public partial class MissionNpcPawn : Node2D, IInteractable
{
	[Export] public NodePath VisualSpritePath { get; set; } = new NodePath("Sprite2D");
	[Export] public NodePath NameLabelPath { get; set; } = new NodePath("NameLabel");
	[Export] public NodePath ShadowPath { get; set; } = new NodePath("Shadow");

	public string NpcId { get; private set; } = string.Empty;
	public string DisplayName { get; private set; } = string.Empty;
	public string PortraitPath { get; private set; } = string.Empty;
	public string DialogueId { get; private set; } = string.Empty;
	public int InteractionRange { get; private set; } = 1;
	public Vector2I CurrentCell { get; private set; } = Vector2I.Zero;
	public bool IsConsumed { get; private set; }

	private MissionNpcDefinition _definition;
	private Sprite2D _visualSprite;
	private Label _nameLabel;
	private Polygon2D _shadow;

	public override void _Ready()
	{
		_visualSprite = GetNodeOrNull<Sprite2D>(VisualSpritePath);
		_nameLabel = GetNodeOrNull<Label>(NameLabelPath);
		_shadow = GetNodeOrNull<Polygon2D>(ShadowPath);
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
		GlobalPosition = globalPosition;
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

	public void CommitInteractionResult(PropInteractionResult result)
	{
		if (result?.Success != true)
		{
			return;
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
