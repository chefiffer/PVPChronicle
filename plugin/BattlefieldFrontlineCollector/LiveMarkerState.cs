using System.Collections.Generic;
using System.Linq;

namespace BattlefieldFrontlineCollector;

internal sealed record LiveFieldMarker(int Index, string Key, float X, float Y, float Z);
internal sealed record LiveTargetMarker(int Index, string Key, uint EntityId, string Name,
    ushort WorldId, uint JobId, byte Team, float X, float Y, float Z, bool PositionVisible);
internal sealed record LivePlayerMarker(uint EntityId, string Name, ushort WorldId, uint JobId,
    byte Battalion, float X, float Y, float Z, float Rotation, bool IsLocal, bool IsAlive);
internal sealed record LiveObjectiveMarker(uint Id, string Label, string State, byte OwnerTeam,
    float X, float Y, float Z, uint CurrentHp = 0, uint MaxHp = 0,
    string EvidenceScope = "", int? RemainingSeconds = null,
    double? ProgressPercent = null, string DisplayText = "");
internal sealed record LiveMarkerSnapshot(IReadOnlyList<LiveFieldMarker> Fields,
    IReadOnlyList<LiveTargetMarker> Targets, IReadOnlyList<LiveObjectiveMarker> Objectives,
    IReadOnlyList<LivePlayerMarker> Players);

internal enum LivePlayerRelation { Local, Ally, Enemy, Unknown }

internal static class LivePlayerMapContract
{
    public static LivePlayerRelation Resolve(bool crystalline, byte battalion,
        byte localBattalion, bool isLocal)
    {
        if (isLocal) return LivePlayerRelation.Local;
        var maximumTeam = crystalline ? 1 : 2;
        if (battalion > maximumTeam || localBattalion > maximumTeam)
            return LivePlayerRelation.Unknown;
        return battalion == localBattalion ? LivePlayerRelation.Ally : LivePlayerRelation.Enemy;
    }
}

/// <summary>
/// Small lock-protected state cache. It never performs IO and is updated only
/// with the 8 field-marker and 17 target-marker slots observed from the client.
/// 小型只读快照缓存；不执行磁盘或网络操作，避免实时 UI 阻塞游戏线程。
/// </summary>
internal sealed class LiveMarkerState
{
    private readonly object gate = new();
    private readonly Dictionary<int, LiveFieldMarker> fields = new();
    private readonly Dictionary<int, LiveTargetMarker> targets = new();
    private readonly Dictionary<uint, LiveObjectiveMarker> objectives = new();
    private readonly Dictionary<uint, LivePlayerMarker> players = new();

    public void Reset()
    {
        lock (gate) { fields.Clear(); targets.Clear(); objectives.Clear(); players.Clear(); }
    }

    public void SetField(LiveFieldMarker marker)
    {
        lock (gate) fields[marker.Index] = marker;
    }

    public void RemoveField(int index)
    {
        lock (gate) fields.Remove(index);
    }

    public void SetTarget(LiveTargetMarker marker)
    {
        lock (gate)
        {
            if (marker.PositionVisible) targets[marker.Index] = marker;
            else targets.Remove(marker.Index);
        }
    }

    public void RemoveTarget(int index)
    {
        lock (gate) targets.Remove(index);
    }

    public void SetObjective(LiveObjectiveMarker marker)
    {
        lock (gate) objectives[marker.Id] = marker;
    }

    public void RemoveObjective(uint id)
    {
        lock (gate) objectives.Remove(id);
    }

    public void ReplacePlayers(IEnumerable<LivePlayerMarker> current)
    {
        lock (gate)
        {
            players.Clear();
            foreach (var player in current)
                if (player.EntityId != 0)
                    players[player.EntityId] = player;
        }
    }

    public LiveMarkerSnapshot Snapshot()
    {
        lock (gate) return new LiveMarkerSnapshot(fields.Values.OrderBy(x => x.Index).ToArray(),
            targets.Values.OrderBy(x => x.Index).ToArray(),
            objectives.Values.OrderBy(x => x.Label).ThenBy(x => x.Id).ToArray(),
            players.Values.OrderBy(x => x.IsLocal).ThenBy(x => x.EntityId).ToArray());
    }
}
