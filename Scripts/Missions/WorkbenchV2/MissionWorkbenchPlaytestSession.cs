public static class MissionWorkbenchPlaytestSession
{
	private static string _missionId = string.Empty;
	private static string _layoutResourcePath = string.Empty;

	public static void Prepare(string missionId, string layoutResourcePath)
	{
		_missionId = missionId ?? string.Empty;
		_layoutResourcePath = layoutResourcePath ?? string.Empty;
	}

	public static bool TryConsumeLayoutOverride(string missionId, out string layoutResourcePath)
	{
		layoutResourcePath = string.Empty;
		if (string.IsNullOrWhiteSpace(_missionId) || string.IsNullOrWhiteSpace(_layoutResourcePath) || _missionId != missionId) return false;
		layoutResourcePath = _layoutResourcePath;
		_missionId = string.Empty;
		_layoutResourcePath = string.Empty;
		return true;
	}
}
