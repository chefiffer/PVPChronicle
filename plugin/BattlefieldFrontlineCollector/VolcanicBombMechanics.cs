using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlefieldFrontlineCollector;

/// <summary>Pure replay state for Volcanic Heart bomb-cross telegraphs.</summary>
internal static class VolcanicBombMechanics
{
    // Across 39 captured bomb lifetimes, the directly observed action begins
    // 3.13-3.57 seconds before the object's HP transitions to zero. Use the
    // conservative upper bound only when that local action row is absent.
    internal const long WarningLeadMs = 3_600;
    internal const long PostDetonationMs = 1_200;

    internal sealed record Telegraph(uint ObjectId, double X, double Z,
        long WarningAtMs, long DetonatedAtMs, bool DirectActionEvidence)
    {
        internal bool IsVisibleAt(long elapsedMs) =>
            elapsedMs >= WarningAtMs && elapsedMs <= DetonatedAtMs + PostDetonationMs;
    }

    internal static IReadOnlyList<Telegraph> BuildTelegraphs(
        IEnumerable<ObjectiveObjectObservation> observations,
        IEnumerable<ActionSliceEvent> actions)
    {
        // Action effects are emitted once per affected target. Collapse them
        // before matching, otherwise one physical explosion becomes several
        // increasingly opaque copies of the same cross.
        var actionEvidence = actions.Where(row =>
                row.ActionId == CrystallineMechanismCatalog.VolcanicBombExplosionActionId &&
                row.SourceX.HasValue && row.SourceZ.HasValue)
            .GroupBy(row => (row.ElapsedMs, row.SourceEntityId,
                X: Math.Round(row.SourceX!.Value, 2),
                Z: Math.Round(row.SourceZ!.Value, 2)))
            .Select(group => group.First())
            .OrderBy(row => row.ElapsedMs)
            .ToArray();
        var matchedActions = new HashSet<(long ElapsedMs, uint SourceEntityId,
            double X, double Z)>();
        var result = new List<Telegraph>();

        foreach (var objectGroup in observations
                     .Where(IsBombObservation)
                     .GroupBy(row => row.ObjectId))
        {
            foreach (var generation in SplitGenerations(objectGroup))
            {
                var loaded = generation.FirstOrDefault(row => row.Loaded);
                if (loaded == null) continue;
                var detonation = generation.FirstOrDefault(row =>
                    row.Loaded && row.MaxHp > 0 && row.CurrentHp == 0);

                var directRows = actionEvidence.Where(row =>
                    row.SourceEntityId == loaded.ObjectId &&
                    row.ElapsedMs >= loaded.ElapsedMs &&
                    (detonation == null || row.ElapsedMs <= detonation.ElapsedMs))
                    .ToArray();
                var direct = directRows.FirstOrDefault();
                if (detonation == null && direct == null) continue;

                var warningAt = direct?.ElapsedMs ??
                    Math.Max(loaded.ElapsedMs, detonation!.ElapsedMs - WarningLeadMs);
                var detonatedAt = detonation?.ElapsedMs ??
                    direct!.ElapsedMs + WarningLeadMs;
                result.Add(new Telegraph(loaded.ObjectId, loaded.X, loaded.Z,
                    warningAt, detonatedAt, direct != null));
                foreach (var row in directRows)
                    matchedActions.Add(ActionKey(row));
            }
        }

        // Old details may contain combat effects but predate global object
        // discovery. Retain those crosses, still collapsed per physical cast.
        foreach (var action in actionEvidence.Where(row =>
                     !matchedActions.Contains(ActionKey(row))))
            result.Add(new Telegraph(action.SourceEntityId,
                action.SourceX!.Value, action.SourceZ!.Value,
                action.ElapsedMs, action.ElapsedMs + WarningLeadMs, true));

        return result.OrderBy(row => row.WarningAtMs)
            .ThenBy(row => row.ObjectId).ToArray();
    }

    private static bool IsBombObservation(ObjectiveObjectObservation row) =>
        row.TerritoryId is 1033 or 1059 && row.BaseId == 14471;

    private static (long ElapsedMs, uint SourceEntityId, double X, double Z)
        ActionKey(ActionSliceEvent row) =>
        (row.ElapsedMs, row.SourceEntityId,
            Math.Round(row.SourceX!.Value, 2), Math.Round(row.SourceZ!.Value, 2));

    private static IEnumerable<IReadOnlyList<ObjectiveObjectObservation>> SplitGenerations(
        IEnumerable<ObjectiveObjectObservation> rows)
    {
        List<ObjectiveObjectObservation>? current = null;
        foreach (var row in rows.OrderBy(value => value.ElapsedMs))
        {
            if (row.Loaded)
            {
                current ??= [];
                current.Add(row);
                continue;
            }
            if (current == null) continue;
            current.Add(row);
            yield return current;
            current = null;
        }
        if (current is { Count: > 0 }) yield return current;
    }
}
