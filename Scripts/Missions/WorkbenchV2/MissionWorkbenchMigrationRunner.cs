using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MissionWorkbenchMigrationRunner : Node
{
	public override void _Ready()
	{
		int failures = 0;
		foreach (MissionTemplate template in new MissionRegistry().LoadTemplates().Where(template => template != null).OrderBy(template => template.MissionId))
		{
			if (!MigrateAndVerify(template, out string report)) failures++;
			GD.Print(report);
		}

		GD.Print(failures == 0 ? "MISSION_WORKBENCH_V2_MIGRATION_OK" : $"MISSION_WORKBENCH_V2_MIGRATION_FAILED count={failures}");
		GetTree().Quit(failures == 0 ? 0 : 1);
	}

	private static bool MigrateAndVerify(MissionTemplate template, out string report)
	{
		MissionImportResult import = LegacyMissionImporter.Import(template);
		if (!import.Success)
		{
			report = $"MIGRATE_FAIL {template.MissionId}: {string.Join(" ", import.Errors)}";
			return false;
		}

		string sourcePath = MissionDocumentSerializer.GetDocumentResourcePath(template.MissionId);
		MissionDocument sourceDocument = import.Document;
		if (FileAccess.FileExists(sourcePath))
		{
			sourceDocument = MissionDocumentSerializer.Load(sourcePath, out string existingError);
			if (sourceDocument == null)
			{
				report = $"MIGRATE_FAIL {template.MissionId}: existing v2 source is invalid and was not overwritten. {existingError}";
				return false;
			}
		}
		else if (!MissionDocumentSerializer.Save(sourceDocument, sourcePath, out string saveError))
		{
			report = $"MIGRATE_FAIL {template.MissionId}: {saveError}";
			return false;
		}

		MissionDocument reloaded = MissionDocumentSerializer.Load(sourcePath, out string loadError);
		if (reloaded == null || MissionDocumentSerializer.Serialize(sourceDocument) != MissionDocumentSerializer.Serialize(reloaded))
		{
			report = $"MIGRATE_FAIL {template.MissionId}: source round-trip mismatch. {loadError}";
			return false;
		}

		if (!VerifyCommandRoundTrip(reloaded))
		{
			report = $"MIGRATE_FAIL {template.MissionId}: command undo/redo round-trip failed.";
			return false;
		}

		string compilePath = $"user://mission_workbench_v2/migration/{template.MissionId}.json";
		string sourceBeforeCompile = MissionDocumentSerializer.Serialize(reloaded);
		MissionCompileResult compile = MissionDocumentCompiler.Compile(reloaded, compilePath);
		if (!compile.Success)
		{
			string errors = string.Join(" | ", compile.Issues.Where(issue => issue.Severity == MissionValidationSeverity.Error).Select(issue => issue.Message));
			report = $"MIGRATE_FAIL {template.MissionId}: compile failed. {errors}";
			return false;
		}
		MissionCompileResult repeatCompile = MissionDocumentCompiler.Compile(reloaded, compilePath);
		if (!repeatCompile.Success
			|| !string.Equals(compile.Content, repeatCompile.Content, StringComparison.Ordinal)
			|| !string.Equals(sourceBeforeCompile, MissionDocumentSerializer.Serialize(reloaded), StringComparison.Ordinal))
		{
			report = $"MIGRATE_FAIL {template.MissionId}: compile is not deterministic or mutated the source document.";
			return false;
		}

		MissionTemplate compiledTemplate = new MissionTemplate
		{
			MissionId = template.MissionId,
			Title = template.Title,
			MissionScenePath = template.MissionScenePath,
			LayoutResourcePath = compilePath
		};
		MissionImportResult compiledImport = LegacyMissionImporter.Import(compiledTemplate);
		if (!compiledImport.Success || !AreLayoutsEquivalent(reloaded, compiledImport.Document))
		{
			report = $"MIGRATE_FAIL {template.MissionId}: compiled layout is not semantically equivalent.";
			return false;
		}

		int warningCount = compile.Issues.Count(issue => issue.Severity != MissionValidationSeverity.Error);
		foreach (MissionValidationIssue warning in compile.Issues.Where(issue => issue.Severity != MissionValidationSeverity.Error))
		{
			GD.Print($"MIGRATE_WARNING {template.MissionId} [{warning.RuleId}] {warning.Message}");
		}
		report = $"MIGRATE_OK {template.MissionId}: elements={reloaded.Elements.Count} flow={reloaded.FlowNodes.Count} dialogue={reloaded.DialogueConversationIds.Count} warnings={warningCount}";
		return true;
	}

	private static bool VerifyCommandRoundTrip(MissionDocument document)
	{
		MissionDocument working = MissionDocumentSerializer.Clone(document);
		MissionMapElement first = working.Elements.FirstOrDefault();
		if (first == null) return false;
		int originalColumn = first.Column;
		int originalRow = first.Row;
		MissionWorkbenchStore store = new MissionWorkbenchStore();
		store.SetDocument(working);
		string before = MissionDocumentSerializer.Serialize(working);
		store.Execute(new MoveMissionElementCommand(first.Id, originalColumn, originalRow, originalColumn + 1, originalRow + 1));
		store.Undo();
		if (MissionDocumentSerializer.Serialize(working) != before) return false;
		store.Redo();
		return working.Elements.First(element => element.Id == first.Id).Column == originalColumn + 1;
	}

	private static bool AreLayoutsEquivalent(MissionDocument expected, MissionDocument actual)
	{
		if (expected.Environment.BackgroundId != actual.Environment.BackgroundId || expected.Elements.Count != actual.Elements.Count) return false;
		List<string> expectedSignatures = expected.Elements.OrderBy(element => element.SourceOrder).Select(BuildElementSignature).ToList();
		List<string> actualSignatures = actual.Elements.OrderBy(element => element.SourceOrder).Select(BuildElementSignature).ToList();
		return expectedSignatures.SequenceEqual(actualSignatures, StringComparer.Ordinal);
	}

	private static string BuildElementSignature(MissionMapElement element)
	{
		MissionElementLogic logic = element.Logic ?? new MissionElementLogic();
		return string.Join("|", element.Kind, element.TileId, element.MarkerId, element.Column, element.Row,
			element.OffsetX, element.OffsetY, element.RotationDegrees, element.FlipH, element.FlipV,
			element.PropDefinitionPath, element.NpcDefinitionPath, logic.Role, logic.Label, logic.TargetId,
			logic.NpcPortraitPath, logic.RequiredFlag, logic.SetFlag, logic.TriggerMode, logic.OneShot, logic.Notes);
	}
}
