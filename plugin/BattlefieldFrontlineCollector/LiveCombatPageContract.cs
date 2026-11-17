namespace BattlefieldFrontlineCollector;

/// <summary>
/// Stable page identities for the four independent live-data windows.
/// 页面只共享采集数据，不共享窗口位置、尺寸或开关状态。
/// </summary>
internal enum LiveCombatPage
{
    CombatSlice,
    MarkerMap,
    TendencyCandidates,
    ObservedKda
}

internal readonly record struct LiveMarkerPresentation(
    bool Transparent,
    bool Borderless,
    bool DrawBackdrop,
    bool DrawMapTexture,
    bool DrawGrid,
    bool AcceptPointerInput);

internal readonly record struct LiveSliceActorColumns(bool ShowSource, bool ShowTarget);

internal static class LiveCombatPageContract
{
    public static readonly LiveCombatPage[] Pages =
    [
        LiveCombatPage.CombatSlice,
        LiveCombatPage.MarkerMap,
        LiveCombatPage.TendencyCandidates,
        LiveCombatPage.ObservedKda
    ];

    public static LiveMarkerPresentation MarkerPresentation(bool transparentOverlay) =>
        transparentOverlay
            ? new(true, true, false, false, false, false)
            : new(false, false, true, true, true, true);

    // “当前状态”中的手动入口属于排版工具，场外也必须保留；
    // 只有自动弹窗才应受真实 PvP 战局状态约束。
    public static bool ShowManualLaunchers => true;

    public static bool ShouldAutoOpenLivePages(bool capturing, uint territoryId, uint dutyId) =>
        capturing && GameLayout.IsSupportedArea(territoryId, dutyId);

    public static byte? ResolveDisplayedTeam(bool crystalline, byte team, byte battalion)
    {
        var maximumTeam = crystalline ? 1 : 2;
        if (team <= maximumTeam) return team;
        return battalion <= maximumTeam ? battalion : null;
    }

    public static LiveSliceActorColumns ActorColumns(int direction) => direction switch
    {
        1 => new(false, true),  // Only my outgoing events: source is always me.
        2 => new(true, false),  // Only my incoming events: target is always me.
        _ => new(true, true)
    };
}
