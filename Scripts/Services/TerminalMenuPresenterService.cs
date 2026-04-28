using Godot;
using System;
using System.Collections.Generic;

public class TerminalMenuPresenterService
{
	public void PopulateShopMenu(
		VBoxContainer container,
		IEnumerable<EquipmentData> shopItems,
		Func<string, bool> canAfford,
		Action<string> onBuy,
		int ancientTechCount,
		Action onSellAncientTech,
		IEnumerable<InventoryStack> sellableInventory,
		int standardSaleValue,
		string outpostName,
		Action<string, string, int> onSellInventory)
	{
		if (container == null) return;

		ClearChildren(container);

		Label buyTitle = new Label();
		buyTitle.Text = "AVAILABLE UPGRADES";
		buyTitle.AddThemeColorOverride("font_color", new Color(0f, 1f, 0.8f));
		container.AddChild(buyTitle);

		foreach (EquipmentData item in shopItems)
		{
			HBoxContainer row = new HBoxContainer();
			row.AddChild(BuildRichInfo(
				$"[b][color=yellow]{item.Name}[/color][/b] ({item.Category})\n{item.Description}\n[color=cyan]Cost: {item.CostTech} Tech, {item.CostRaw} Raw[/color]",
				new Vector2(380, 75)));

			Button buyButton = BuildActionButton("BUY");
			if (!canAfford(item.ItemID))
			{
				buyButton.Disabled = true;
				buyButton.Text = "FUNDS";
			}

			buyButton.Pressed += () => onBuy(item.ItemID);
			row.AddChild(buyButton);
			container.AddChild(row);
		}

		container.AddChild(new HSeparator());

		Label sellTitle = new Label();
		sellTitle.Text = $"SALVAGE BUYBACK: {outpostName.ToUpper()}";
		sellTitle.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.2f));
		container.AddChild(sellTitle);

		bool hasSellables = false;
		if (ancientTechCount > 0)
		{
			HBoxContainer ancientTechRow = new HBoxContainer();
			ancientTechRow.AddChild(BuildRichInfo(
				$"[b][color=yellow]{GameConstants.ResourceKeys.AncientTech}[/color][/b] (x{ancientTechCount})\nExchange Rate: [color=cyan]{GameConstants.StandardEquipment.AncientTechSaleRaw} {GameConstants.ResourceKeys.RawMaterials} each[/color]",
				new Vector2(380, 70)));

			Button sellAncientTechButton = BuildActionButton("SELL 1");
			sellAncientTechButton.Pressed += onSellAncientTech;
			ancientTechRow.AddChild(sellAncientTechButton);
			container.AddChild(ancientTechRow);
			hasSellables = true;
		}

		foreach (InventoryStack stack in sellableInventory)
		{
			HBoxContainer row = new HBoxContainer();
			row.AddChild(BuildRichInfo(
				$"[b][color=yellow]{stack.Item.Name}[/color][/b] (x{stack.Count})\n{stack.Item.Description}\n[color=cyan]Sell Value: {standardSaleValue} {GameConstants.ResourceKeys.RawMaterials}[/color]",
				new Vector2(380, 75)));

			Button sellButton = BuildActionButton("SELL 1");
			sellButton.Pressed += () => onSellInventory(stack.ItemID, stack.Item.Name, standardSaleValue);
			row.AddChild(sellButton);
			container.AddChild(row);
			hasSellables = true;
		}

		if (!hasSellables)
		{
			container.AddChild(new Label { Text = "No standard gear or Ancient Tech available to sell." });
		}
	}

	public void PopulateEquipMenu(
		VBoxContainer container,
		string shipName,
		string weaponName,
		string shieldName,
		string armorName,
		IEnumerable<InventoryStack> inventoryStacks,
		Action<string> onEquip)
	{
		if (container == null) return;

		ClearChildren(container);

		RichTextLabel currentLoadoutText = new RichTextLabel();
		currentLoadoutText.BbcodeEnabled = true;
		currentLoadoutText.FitContent = true;
		currentLoadoutText.Text = $"[color=cyan]--- {shipName.ToUpper()}'s CURRENT LOADOUT ---[/color]\nWeapon: {weaponName}\nShield: {shieldName}\nArmor: {armorName}\n\n[color=yellow]--- CARGO HOLD (AVAILABLE INVENTORY) ---[/color]";
		container.AddChild(currentLoadoutText);

		bool hasInventory = false;
		foreach (InventoryStack stack in inventoryStacks)
		{
			hasInventory = true;
			HBoxContainer row = new HBoxContainer();
			row.AddChild(BuildRichInfo($"[b]{stack.Item.Name}[/b] (x{stack.Count})\n{stack.Item.Description}", new Vector2(380, 50)));

			Button equipButton = BuildActionButton("EQUIP");
			equipButton.Pressed += () => onEquip(stack.ItemID);
			row.AddChild(equipButton);
			container.AddChild(row);
		}

		if (!hasInventory)
		{
			container.AddChild(new Label { Text = "No unequipped items available in cargo." });
		}
	}

	private static RichTextLabel BuildRichInfo(string text, Vector2 minimumSize)
	{
		RichTextLabel info = new RichTextLabel();
		info.BbcodeEnabled = true;
		info.Text = text;
		info.CustomMinimumSize = minimumSize;
		info.FitContent = true;
		return info;
	}

	private static Button BuildActionButton(string text)
	{
		Button button = new Button();
		button.Text = text;
		button.CustomMinimumSize = new Vector2(90, 40);
		button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		return button;
	}

	private static void ClearChildren(VBoxContainer container)
	{
		foreach (Node child in container.GetChildren())
		{
			child.QueueFree();
		}
	}
}
