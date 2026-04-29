using Godot;
using System;

public class StrandedMenuPresenterService
{
	public CenterContainer BuildMenu(Node owner, Action onDistressSignal, Action onAbandonFleet)
	{
		if (owner == null) return null;

		CanvasLayer menuLayer = new CanvasLayer { Layer = 150 };
		owner.AddChild(menuLayer);

		CenterContainer wrapper = new CenterContainer();
		wrapper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		wrapper.MouseFilter = Control.MouseFilterEnum.Stop;
		wrapper.Visible = false;
		menuLayer.AddChild(wrapper);

		PanelContainer strandedPanel = new PanelContainer();

		StyleBoxFlat style = new StyleBoxFlat();
		style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
		style.BorderWidthTop = 2;
		style.BorderWidthBottom = 2;
		style.BorderWidthLeft = 2;
		style.BorderWidthRight = 2;
		style.BorderColor = new Color(1f, 0f, 0f, 0.8f);
		style.ContentMarginLeft = 20;
		style.ContentMarginRight = 20;
		style.ContentMarginTop = 20;
		style.ContentMarginBottom = 20;
		strandedPanel.AddThemeStyleboxOverride("panel", style);

		wrapper.AddChild(strandedPanel);

		VBoxContainer container = new VBoxContainer();
		container.AddThemeConstantOverride("separation", 15);
		strandedPanel.AddChild(container);

		Label title = new Label();
		title.Text = "=== WARNING: CRITICAL FUEL DEPLETION ===";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeColorOverride("font_color", new Color(1f, 0f, 0f));
		title.AddThemeFontSizeOverride("font_size", 18);
		container.AddChild(title);

		Label body = new Label();
		body.Text = "Your fleet has run out of Raw Materials.\nMain engines are offline. Life support is failing.\n\nWhat are your orders, Commander?";
		body.HorizontalAlignment = HorizontalAlignment.Center;
		body.AddThemeFontSizeOverride("font_size", 14);
		container.AddChild(body);

		Button btnDistress = BuildActionButton("SEND FTL DISTRESS SIGNAL (Gamble)");
		btnDistress.AddThemeColorOverride("font_color", new Color(1f, 1f, 0f));
		btnDistress.Pressed += onDistressSignal;
		container.AddChild(btnDistress);

		Button btnAbandon = BuildActionButton("ABANDON FLEET (Return to Menu)");
		btnAbandon.Pressed += onAbandonFleet;
		container.AddChild(btnAbandon);

		return wrapper;
	}

	private static Button BuildActionButton(string text)
	{
		Button button = new Button();
		button.Text = text;
		button.CustomMinimumSize = new Vector2(0, 40);
		return button;
	}
}
