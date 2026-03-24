using Godot;
using System;

public partial class ShipSimDashboard : Control
{
	// We create a variable to hold the power level data
	private int reactorPower = 100;
	
	// We create an empty container to hold our text label
	private Label reactorTextNode;

	// _Ready() runs exactly once when the scene first loads
	public override void _Ready()
	{
		// This tells the script to find the "ReactorText" node and store it
		reactorTextNode = GetNode<Label>("ReactorText");
	}

	// This is the method Godot created when we connected the button signal!
	public void _on_drain_power_button_pressed()
	{
		// Subtract 10 from the power level
		reactorPower -= 10;
		
		// Update the text on the screen to show the new number
		reactorTextNode.Text = "Reactor Cores: " + reactorPower + "% Stable";
	}
}
