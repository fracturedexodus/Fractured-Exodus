using Godot;

public partial class MissionWorkbenchPlaytestSmoke : Node
{
	public override void _Ready()
	{
		MissionTemplate template = GetNodeOrNull<MissionManager>("/root/MissionManager")?.GetTemplate("black_site_relay");
		MissionDocument document = MissionDocumentSerializer.Load(
			MissionDocumentSerializer.GetDocumentResourcePath("black_site_relay"),
			out string error);
		if (template == null || document == null)
		{
			GD.PushError($"Workbench playtest smoke setup failed: {error}");
			GetTree().Quit(1);
			return;
		}

		string outputPath = MissionDocumentCompiler.GetPlaytestOutputPath(document);
		MissionCompileResult compile = MissionDocumentCompiler.Compile(document, outputPath);
		MissionRuntimeState state = GetNode<MissionManager>("/root/MissionManager")
			.PrepareMission(document.MissionId, "res://mission_workbench_v2.tscn", "Workbench smoke test");
		if (!compile.Success || state == null)
		{
			GD.PushError("Workbench playtest smoke compile or mission preparation failed.");
			GetTree().Quit(2);
			return;
		}

		MissionWorkbenchPlaytestSession.Prepare(document.MissionId, outputPath);
		CallDeferred(nameof(LaunchMission), document.Metadata.MissionScenePath);
	}

	private void LaunchMission(string scenePath)
	{
		GetTree().ChangeSceneToFile(scenePath);
	}
}
