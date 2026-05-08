public sealed class MissionSpawnContext
{
	public GlobalData GlobalData { get; init; }
	public MissionRuntimeState MissionState { get; init; }
	public MissionTemplate MissionTemplate { get; init; }
	public MissionRoomBuilder RoomBuilder { get; init; }
}
