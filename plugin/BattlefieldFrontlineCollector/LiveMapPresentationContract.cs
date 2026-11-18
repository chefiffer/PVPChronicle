using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Pure presentation rules for the live map. Keeping colour and crowding
/// decisions outside ImGui makes them deterministic and regression-testable.
/// 实时地图的纯显示契约；不依赖 ImGui，便于验证色阶、透明度和密集区引导线。
/// </summary>
internal static class LiveMapPresentationContract
{
    internal const float DenseMarkerRadiusPixels = 28f;
    internal const int DenseMarkerMinimumCount = 3;

    internal static Vector4 FrontlineSquadColor(byte team, byte alliance, float opacity)
    {
        var baseColor = team < FrontlineTeamPalette.Colors.Length
            ? FrontlineTeamPalette.Colors[team]
            : new Vector4(.72f, .74f, .76f, 1f);
        var shaded = alliance switch
        {
            0 => new Vector4(baseColor.X * .70f, baseColor.Y * .70f, baseColor.Z * .70f, 1f),
            1 => baseColor,
            2 => new Vector4(baseColor.X + (1f - baseColor.X) * .30f,
                baseColor.Y + (1f - baseColor.Y) * .30f,
                baseColor.Z + (1f - baseColor.Z) * .30f, 1f),
            _ => baseColor
        };
        return shaded with { W = Math.Clamp(opacity, 0f, 1f) };
    }

    internal static bool IsCrowded(Vector2 center, IEnumerable<Vector2> visiblePoints)
    {
        var radiusSquared = DenseMarkerRadiusPixels * DenseMarkerRadiusPixels;
        return visiblePoints.Count(point => Vector2.DistanceSquared(center, point) <= radiusSquared)
               >= DenseMarkerMinimumCount;
    }

    internal static Vector2 CalloutDiagonal(int markerIndex)
    {
        var right = markerIndex % 2 == 0;
        return new Vector2(right ? 24f : -24f, -24f);
    }
}
