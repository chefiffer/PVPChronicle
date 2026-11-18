using System;
using System.Numerics;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// One compact identity seam for every Frontline player list.  Callers only
/// provide the authoritative team/alliance values; this module owns the
/// faction-first ordering and the coloured bold A/B/C badge presentation.
/// 纷争前线玩家身份的统一接口：阵营与分队排序、颜色和 A/B/C 紧凑标记都在此实现。
/// </summary>
internal static class FrontlinePlayerIdentityContract
{
    /// <summary>
    /// The Frontline result packet numbers the nine alliances globally as 0..8,
    /// while the UI presents A/B/C inside each faction.  Older captures and a
    /// few derived rows may already contain 0..2, so modulo three is the one
    /// normalization that preserves both representations.  Values outside the
    /// documented packet range remain unknown instead of being guessed.
    /// 结算包按全场使用 0..8 编号；界面按每个阵营内的 A/B/C 展示。
    /// </summary>
    public static byte NormalizeSquad(byte alliance) => alliance <= 8
        ? (byte)(alliance % 3)
        : (byte)255;

    public static int TeamSquadSortKey(byte team, byte alliance)
    {
        var normalizedTeam = team < 3 ? team : 3;
        var squad = NormalizeSquad(alliance);
        var normalizedAlliance = squad < 3 ? squad : 3;
        return normalizedTeam * 4 + normalizedAlliance;
    }

    public static string SquadLabel(byte alliance) => NormalizeSquad(alliance) is var squad && squad < 3
        ? ((char)('A' + squad)).ToString()
        : "?";

    public static Vector4 TeamColor(byte team) => team < FrontlineTeamPalette.Colors.Length
        ? FrontlineTeamPalette.Colors[team]
        : new Vector4(.7f, .7f, .7f, 1f);

}
