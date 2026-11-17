using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Reconstructs a Crystalline Conflict route from two independent direct
/// sources: loaded tactical-crystal coordinates and the content director's
/// signed progress gauge.  It never invents an arena coordinate.  A milestone
/// exists only when the match actually observed enough of that side's route.
/// </summary>
internal static class CrystallineRouteOverlay
{
    internal const string CrystalPositionLabel = "cc_crystal_position";
    internal const string AstraProgressLabel = "cc_astra_progress";
    internal const string UmbraProgressLabel = "cc_umbra_progress";

    internal readonly record struct RoutePoint(double SignedPercent, double X, double Z);
    internal readonly record struct GaugePoint(long ElapsedMs, double Value);
    internal readonly record struct GaugeSegment(GaugePoint From, GaugePoint To);

    /// <summary>
    /// Builds every visible segment without dropping alternating samples.
    /// Checkpoint gauges are state values, so they use a continuous step line:
    /// retain the old value until the next sample, then change vertically.
    /// </summary>
    internal static IReadOnlyList<GaugeSegment> BuildGaugeSegments(
        IReadOnlyList<GaugePoint> samples, bool stepped)
    {
        if (samples.Count < 2) return [];
        var result = new List<GaugeSegment>(stepped ? (samples.Count - 1) * 2 : samples.Count - 1);
        for (var i = 1; i < samples.Count; i++)
        {
            var from = samples[i - 1];
            var to = samples[i];
            if (!stepped)
            {
                result.Add(new GaugeSegment(from, to));
                continue;
            }
            var corner = new GaugePoint(to.ElapsedMs, from.Value);
            result.Add(new GaugeSegment(from, corner));
            result.Add(new GaugeSegment(corner, to));
        }
        return result;
    }

    internal static IReadOnlyList<RoutePoint> Build(IReadOnlyList<ObjectiveStateEvent>? timeline)
    {
        var rows = timeline ?? [];
        var gauges = rows.Where(row => row.Label == CrystalPositionLabel &&
                                       row.ProgressPercent.HasValue &&
                                       Math.Abs(row.ProgressPercent.Value) <= 100.5)
            .OrderBy(row => row.ElapsedMs).ToArray();
        if (gauges.Length == 0) return [];
        var samples = new List<RoutePoint>();
        foreach (var crystal in rows.Where(IsTacticalCrystal)
                     .Where(row => !string.Equals(row.State, "inactive",
                         StringComparison.OrdinalIgnoreCase)))
        {
            var nearest = Nearest(gauges, crystal.ElapsedMs);
            if (nearest == null || Math.Abs(nearest.ElapsedMs - crystal.ElapsedMs) > 1_000)
                continue;
            samples.Add(new RoutePoint(nearest.ProgressPercent!.Value, crystal.X, crystal.Z));
        }
        if (samples.Count == 0) return [];

        // One-percent bins remove 20 Hz sampling noise while preserving bends.
        // Averaging repeated visits also prevents a retreat from producing a
        // second overdrawn route at the same progress value.
        return samples.GroupBy(row => Math.Round(row.SignedPercent,
                MidpointRounding.AwayFromZero))
            .Select(group => new RoutePoint(group.Key,
                group.Average(row => row.X), group.Average(row => row.Z)))
            .OrderBy(row => row.SignedPercent).ToArray();
    }

    internal static bool TryInterpolate(IReadOnlyList<RoutePoint> route,
        double signedPercent, out RoutePoint result)
    {
        result = default;
        if (route.Count == 0 || signedPercent < route[0].SignedPercent ||
            signedPercent > route[^1].SignedPercent) return false;
        var exact = route.OrderBy(row => Math.Abs(row.SignedPercent - signedPercent)).First();
        if (Math.Abs(exact.SignedPercent - signedPercent) < .001)
        {
            result = exact;
            return true;
        }
        var lower = route.LastOrDefault(row => row.SignedPercent < signedPercent);
        var upper = route.FirstOrDefault(row => row.SignedPercent > signedPercent);
        if (lower.SignedPercent >= upper.SignedPercent) return false;
        var ratio = (signedPercent - lower.SignedPercent) /
                    (upper.SignedPercent - lower.SignedPercent);
        result = new RoutePoint(signedPercent,
            lower.X + (upper.X - lower.X) * ratio,
            lower.Z + (upper.Z - lower.Z) * ratio);
        return true;
    }

    internal static double ProgressAt(IReadOnlyList<ObjectiveStateEvent>? timeline,
        string label, long elapsedMs)
    {
        var value = (timeline ?? []).Where(row => row.Label == label &&
                    row.ElapsedMs <= elapsedMs && row.ProgressPercent.HasValue)
            .OrderByDescending(row => row.ElapsedMs).FirstOrDefault()?.ProgressPercent ?? 0;
        return Math.Clamp(value, 0, 100);
    }

    private static bool IsTacticalCrystal(ObjectiveStateEvent row) =>
        row.Label == "cc_tactical_crystal" ||
        row.EvidenceScope == "cc_loaded_tactical_crystal_position_v1";

    private static ObjectiveStateEvent? Nearest(ObjectiveStateEvent[] rows, long elapsedMs)
    {
        var low = 0; var high = rows.Length - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (rows[middle].ElapsedMs < elapsedMs) low = middle + 1;
            else high = middle - 1;
        }
        ObjectiveStateEvent? before = high >= 0 ? rows[high] : null;
        ObjectiveStateEvent? after = low < rows.Length ? rows[low] : null;
        if (before == null) return after;
        if (after == null) return before;
        return elapsedMs - before.ElapsedMs <= after.ElapsedMs - elapsedMs ? before : after;
    }
}
