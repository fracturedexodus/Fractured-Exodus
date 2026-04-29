using Godot;
using System.Collections.Generic;

public class BattleMapAnimationService
{
	public void UpdateSelectedShipFacing(IEnumerable<Vector2I> selectedHexes, Dictionary<Vector2I, MapEntity> hexContents, Vector2 mousePosition)
	{
		foreach (Vector2I hex in selectedHexes)
		{
			if (!hexContents.ContainsKey(hex)) continue;

			MapEntity selectedShip = hexContents[hex];
			if (!GodotObject.IsInstanceValid(selectedShip.VisualSprite)) continue;

			float targetAngle = selectedShip.VisualSprite.GlobalPosition.AngleToPoint(mousePosition) + selectedShip.BaseRotationOffset;
			selectedShip.VisualSprite.Rotation = Mathf.LerpAngle(selectedShip.VisualSprite.Rotation, targetAngle, 0.15f);
		}
	}

	public void AnimateEnvironment(Node2D environmentLayer, float delta, bool allowAsteroidDrift)
	{
		if (environmentLayer == null) return;

		if (allowAsteroidDrift)
		{
			environmentLayer.Rotation -= 0.05f * delta;
		}

		foreach (Node child in environmentLayer.GetChildren())
		{
			if (child is not Polygon2D rock) continue;

			float spin = rock.GetMeta("spin_speed").AsSingle();
			rock.Rotation += spin * delta;
		}
	}
}
