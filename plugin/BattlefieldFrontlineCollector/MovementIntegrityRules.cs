using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Versioned, pure-data movement rules shared by the live collector and the
/// contract tests. Every positive bonus is tied to its source Action ID,
/// observed Status ID, multiplier and reviewed resource versions. Unknown
/// patches fail closed instead of treating an unknown state as zero bonus.
///
/// 版本化的纯数据移速规则。每项都保存来源 Action ID、观测到的 Status ID、
/// 倍率与已审核资源版本；未知版本拒绝发布玩家级结论。
/// </summary>
internal static class MovementIntegrityRules
{
    internal const string CatalogVersion = "pvp-movement-status-2026-10-v4";
    internal const string DisplacementCatalogVersion = "pvp-displacement-2026-11-v3";
    internal const string CnGameVersion = "2026.10.06.0000.0000";
    internal const string GlobalGameVersion = "2026.10.16.0000.0000";
    internal const double ObserverRadiusYalms = 200.0;
    internal const long ObserverStabilizationMs = 2_000;

    internal sealed record PositiveSpeedRule(
        uint StatusId,
        double Bonus,
        string SemanticName,
        IReadOnlyList<uint> SourceActionIds,
        IReadOnlySet<string> ReviewedGameVersions);

    internal sealed record JobMovementRule(
        byte ClassJobId,
        double Bonus,
        string SemanticName,
        IReadOnlySet<string> ReviewedGameVersions);

    private static readonly IReadOnlySet<string> BothReviewedVersions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            CnGameVersion,
            GlobalGameVersion
        };

    /// <summary>
    /// Current positive movement-speed status catalog. Bonuses are additive
    /// only when the distinct statuses are simultaneously visible. The two
    /// Warden status IDs are retained because both were observed immediately
    /// after Action 29400 in current CN and international captures.
    ///
    /// 当前正向移速目录。只有同时可见且 Status ID 不同的状态才保守相加。
    /// </summary>
    internal static readonly IReadOnlyList<PositiveSpeedRule> PositiveSpeedRules =
    [
        new(1342, .50, "PvP Sprint", [29057], BothReviewedVersions),
        new(1303, .25, "Inner Release (Primal Scream)", [29083], BothReviewedVersions),
        new(3042, .25, "No Mercy (Rough Divide)", [29123], BothReviewedVersions),
        new(3092, .25, "Expedience", [29236], BothReviewedVersions),
        new(2007, .50, "Wind Resonance", [41509], BothReviewedVersions),
        new(3209, .25, "Elusive Jump movement bonus", [29494], BothReviewedVersions),
        new(3180, .50, "Sky High movement bonus", [29497], BothReviewedVersions),
        new(3186, .50, "Huton", [29512], BothReviewedVersions),
        new(3207, .25, "Hell's Ingress movement bonus", [29550], BothReviewedVersions),
        new(4095, .50, "Slither movement bonus", [39184], BothReviewedVersions),
        new(4094, .50, "Reawakened movement bonus", [39190], BothReviewedVersions),
        new(2178, .25, "The Warden's Paean movement state A", [29400], BothReviewedVersions),
        new(3143, .25, "The Warden's Paean movement state B", [29400], BothReviewedVersions),
        new(3144, .25, "Final Fantasia movement bonus", [29401], BothReviewedVersions),
        // The recipient receives a separate status from the Bard.  Treating
        // only the caster-side 3144 status as valid created false positives
        // for party members moving under the same Final Fantasia aura.
        new(3145, .25, "Final Fantasia party movement bonus", [29401], BothReviewedVersions),
        new(3149, .25, "Overheated movement bonus", [29402, 41469], BothReviewedVersions),
        new(3161, .25, "Starfall Dance movement bonus", [29421], BothReviewedVersions),
        new(3162, .50, "Honing Dance movement bonus", [29422], BothReviewedVersions),
        new(3226, .25, "Slipstream movement bonus", [29669], BothReviewedVersions),
        new(4110, .25, "Winged Muse movement bonus", [39206], BothReviewedVersions),
        new(4113, .25, "Smudge movement bonus", [39210], BothReviewedVersions),
        new(4477, 1.00, "Swift", [43247], BothReviewedVersions),
        new(4478, .33, "Dervish", [43249], BothReviewedVersions),
    ];

    /// <summary>
    /// Version-reviewed passive job movement modifiers. Ninja's Fleet of Foot
    /// is always active and therefore cannot be discovered from StatusList.
    /// It must be part of the legal baseline rather than treated as an
    /// unexplained movement tail.
    /// 版本审核后的职业常驻移速。忍者的“落地无声”不以状态出现，必须计入合法基线。
    /// </summary>
    internal static readonly IReadOnlyList<JobMovementRule> JobMovementRules =
    [
        new(30, .10, "Ninja Fleet of Foot", BothReviewedVersions)
    ];

    private static readonly IReadOnlyDictionary<uint, PositiveSpeedRule> RulesByStatus =
        PositiveSpeedRules.ToDictionary(rule => rule.StatusId);
    private static readonly IReadOnlyDictionary<byte, JobMovementRule> RulesByJob =
        JobMovementRules.ToDictionary(rule => rule.ClassJobId);

    // Legal PvP actions that move their caster. Lumina's AffectsPosition flag
    // remains the primary source, while this versioned fallback covers actions
    // whose current resource row or packet metadata is incomplete. In
    // particular, Thunderclap, Shukuchi, Aetherial Manipulation and Smudge are
    // animation-driven relocation, not ordinary ground running.
    // 合法的本人位移目录。资源表标记缺失时也必须排除轻身步法、缩地、魔纹步和疾速涂鸦。
    private static readonly IReadOnlySet<uint> CasterDisplacementActions = new HashSet<uint>
    {
        29065, 29079, 29092, 29123, 29229, 29261, 29430, 29479, 29484,
        29494, 29510, 29513, 29522, 29532, 29550, 29660, 29689, 29692,
        29699, 29700, 29707, 29708, 39148, 39153, 39184, 39210, 41488,
        41581, 41646, 41654, 41942, 42039
    };

    internal static bool SupportsGameVersion(string version)
    {
        var normalized = version.Trim();
        return (normalized == CnGameVersion || normalized == GlobalGameVersion) &&
               PositiveSpeedRules.All(rule => rule.ReviewedGameVersions.Contains(normalized)) &&
               JobMovementRules.All(rule => rule.ReviewedGameVersions.Contains(normalized));
    }

    internal static bool IsKnownPositiveSpeedStatus(uint statusId) =>
        RulesByStatus.ContainsKey(statusId);

    internal static double SpeedBonus(uint statusId) =>
        RulesByStatus.TryGetValue(statusId, out var rule) ? rule.Bonus : 0;

    internal static double JobMovementBonus(byte classJobId) =>
        RulesByJob.TryGetValue(classJobId, out var rule) ? rule.Bonus : 0;

    internal static double MovementMultiplier(IEnumerable<int> statuses, byte classJobId = 0) =>
        1.0 + JobMovementBonus(classJobId) +
        statuses.Distinct().Sum(id => SpeedBonus((uint)id));

    internal static bool IsActiveMovement(double distanceYalm, double durationSeconds) =>
        durationSeconds is >= .10 and <= .50 &&
        distanceYalm >= Math.Max(.20, 3.5 * durationSeconds);

    internal static bool IsCasterDisplacementAction(uint actionId) =>
        CasterDisplacementActions.Contains(actionId);

    /// <summary>
    /// Exact speed evidence is limited to ordinary ground locomotion. A large
    /// vertical component is characteristic of elevators, launch pads, falls
    /// and scripted transport; it remains visible in replay but cannot be
    /// compared with the six-yalm-per-second running baseline.
    /// 精确移速只比较贴地行走；电梯、弹射、坠落和场景运输不进入跑速样本。
    /// </summary>
    internal static bool IsGroundLocomotionInterval(double horizontalDistanceYalm,
        double verticalDistanceYalm, double durationSeconds) =>
        IsActiveMovement(horizontalDistanceYalm, durationSeconds) &&
        verticalDistanceYalm <= Math.Max(.35, 3.0 * durationSeconds);

    internal static bool IsInsideObserverRadius(double observerX, double observerY,
        double observerZ, double actorX, double actorY, double actorZ) =>
        Math.Sqrt(Math.Pow(actorX - observerX, 2) + Math.Pow(actorY - observerY, 2) +
                  Math.Pow(actorZ - observerZ, 2)) <= ObserverRadiusYalms;

    /// <summary>
    /// A negative start means that the actor is outside the trusted observer
    /// window. Re-entry always starts a new two-second stabilization period;
    /// no coordinate before that period can be joined to the new segment.
    /// 负值表示已断轨；重新进入两百 yalm 后必须重新连续稳定两秒。
    /// </summary>
    internal static long NextStableWindowStart(long previousStartMs, bool inside, long elapsedMs) =>
        !inside ? -1 : previousStartMs < 0 ? elapsedMs : previousStartMs;

    internal static bool IsStableObserverWindow(long startMs, long elapsedMs) =>
        startMs >= 0 && elapsedMs - startMs >= ObserverStabilizationMs;

    /// <summary>
    /// Type 32 is an explicit target-side displacement effect. Wind's Reply
    /// is a patch-specific exception: its packet carries ordinary damage
    /// effects, while only target index zero receives the documented knockback.
    /// 明确的 Type 32 按实际目标归因；绝空拳仅把技能包首目标视为击退目标。
    /// </summary>
    internal static bool IsForcedTargetEffect(uint actionId, byte targetIndex, byte effectType) =>
        effectType == 32 || actionId == 41509 && targetIndex == 0;

    /// <summary>
    /// A caster's legal displacement effect may precede its coordinate tail by
    /// up to three seconds in a remote observation, but an effect observed after the interval cannot
    /// retroactively explain it. Forced movement retains a symmetric 250 ms
    /// packet/sampling boundary because it is attributed to the target.
    ///
    /// 本人合法位移的效果包可领先坐标尾迹最多三秒，但区间结束后才出现的效果
    /// 不能反向解释该区间；他人强制位移保留目标侧前后 250ms 配对边界。
    /// </summary>
    internal static bool ExplainsInterval(string kind, long attributionMs, long fromMs, long toMs,
        long? attributionEndMs = null) =>
        kind switch
        {
            "caster_displacement_action" => attributionMs >= fromMs - 3_000 &&
                                             attributionMs <= toMs,
            "forced_displacement_effect" => attributionMs >= fromMs - 250 &&
                                             attributionMs <= toMs + 250,
            "forced_displacement_trajectory" => attributionMs <= toMs + 250 &&
                (attributionEndMs ?? attributionMs) >= fromMs - 250,
            _ => false
        };
}
