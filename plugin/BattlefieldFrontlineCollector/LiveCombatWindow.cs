using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ActionSheet = Lumina.Excel.Sheets.Action;
using StatusSheet = Lumina.Excel.Sheets.Status;
using WorldSheet = Lumina.Excel.Sheets.World;
using TerritoryTypeSheet = Lumina.Excel.Sheets.TerritoryType;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Encounter-relative 15/30/60 second combat buffer. A quiet gap completes the
/// encounter but keeps its final actions available until the next encounter.
/// 以交战结束为锚点的缓冲池；停战后仍保留最后一段技能，直到下一次交战。
/// </summary>
internal sealed class LiveCombatWindow : Window
{
    private enum LiveSide { Unknown, Own, Enemy }
    private static readonly Vector4[] TeamColors = FrontlineTeamPalette.Colors;
    private static readonly Vector4[] CrystallineTeamColors = CrystallineTeamPalette.Colors;
    private static readonly Vector4 OwnSideColor = new(.30f, .78f, 1f, 1f);
    private static readonly Vector4 EnemySideColor = new(1f, .48f, .42f, 1f);
    private static readonly Vector4 MapLocalPlayerColor = new(.25f, .88f, 1f, 1f);
    private static readonly Vector4 MapAllyPlayerColor = new(.34f, .88f, .45f, 1f);
    private static readonly Vector4 MapEnemyPlayerColor = new(1f, .28f, .34f, 1f);
    private static readonly Vector4 MapUnknownPlayerColor = new(.72f, .74f, .76f, .9f);
    private readonly Configuration configuration;
    private readonly LiveCombatPage page;
    private readonly LiveCombatBuffer buffer;
    private readonly LiveMarkerState markers;
    private readonly Func<CollectorUiState> state;
    private readonly IDataManager dataManager;
    private readonly ITextureProvider textureProvider;
    private readonly Func<uint, string, ushort, bool> tryTargetVisiblePlayer;
    private readonly ActionLocalizationCatalog actionCatalog;
    private readonly StatusLocalizationCatalog statusCatalog;
    private readonly string assetRoot;
    private readonly Dictionary<ushort, string> worldNames = new();
    private readonly Dictionary<(string Language, long Id), string> statusNames = new();
    private readonly Dictionary<byte, ISharedImmediateTexture> jobIcons = new();
    private readonly Dictionary<uint, ISharedImmediateTexture> actionIcons = new();
    private readonly Dictionary<uint, ISharedImmediateTexture> mapTextures = new();
    private readonly Dictionary<uint, ISharedImmediateTexture> markerIcons = new();
    private IReadOnlyList<LiveCombatRow> cached = [];
    private IReadOnlyList<LiveCoordinationRow> cachedCoordination = [];
    private IReadOnlyList<LiveCombatRow> cachedMatchTendencyRows = [];
    private IReadOnlyList<LiveCoordinationRow> cachedMatchCoordination = [];
    private IReadOnlyList<LiveObservedKda> cachedObservedKda = [];
    private long cachedMatchEvidenceEndMs = -1;
    private long cachedEndMs = -1;
    private bool cachedEncounterActive;
    private long nextRefreshAt;
    private long copiedUntil;
    private bool frozen;
    private int seconds = 30;
    // The live slice opens on the question players most often ask after an
    // unexpected hit: "what did I just take?"
    // 即时切片默认只看本机承受，减少打开页面后的第一次操作。
    private int direction = 2;
    private string activeMatchUid = string.Empty;
    private float markerMapZoom = 1.45f;
    private Vector2 markerMapCenterUv = new(.5f, .5f);

    private sealed record MapAsset(string FileName, string NameKey);
    private static readonly IReadOnlyDictionary<uint, MapAsset> MapAssets = new Dictionary<uint, MapAsset>
    {
        [127] = new("borderland_ruins.png", "mapBorderland"),
        [130] = new("seal_rock.png", "mapSealRock"),
        [180] = new("fields_of_glory.png", "mapShatter"),
        [701] = new("onsal_hakair.png", "mapOnsal"),
        [1080] = new("worqor_chirteh.png", "mapWorqor")
    };

    public LiveCombatWindow(Configuration configuration, LiveCombatPage page,
        LiveCombatBuffer buffer, LiveMarkerState markers,
        Func<CollectorUiState> state, IDataManager dataManager, ITextureProvider textureProvider,
        Func<uint, string, ushort, bool> tryTargetVisiblePlayer, string assetRoot)
        : base($"Battlefield Chronicle · Live combat###BattlefieldLiveCombat-{page}")
    {
        this.configuration = configuration; this.page = page;
        this.buffer = buffer; this.markers = markers; this.state = state;
        this.dataManager = dataManager; this.textureProvider = textureProvider;
        this.tryTargetVisiblePlayer = tryTargetVisiblePlayer;
        actionCatalog = new ActionLocalizationCatalog(assetRoot);
        statusCatalog = new StatusLocalizationCatalog(assetRoot);
        this.assetRoot = assetRoot;
        Size = page == LiveCombatPage.MarkerMap ? new Vector2(920, 680) : new Vector2(1180, 540);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = true;
    }

    private bool TransparentMarkerOverlay =>
        page == LiveCombatPage.MarkerMap && configuration.TransparentLiveMarkerMap;

    public override void PreDraw()
    {
        Flags = TransparentMarkerOverlay
            ? ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
              ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
              ImGuiWindowFlags.NoMouseInputs
            : ImGuiWindowFlags.None;
        if (TransparentMarkerOverlay) ImGui.SetNextWindowBgAlpha(0f);
        BattlefieldUiTheme.Push(configuration.UiTheme);
    }
    public override void PostDraw() => BattlefieldUiTheme.Pop();

    public override void Draw()
    {
        var ui = state();
        var crystalline = GameLayout.IsCrystallineDuty(ui.DutyId);
        var titleKey = page switch
        {
            LiveCombatPage.MarkerMap => "liveMarkerMap",
            LiveCombatPage.TendencyCandidates => "liveTendencyCandidates",
            LiveCombatPage.ObservedKda => "liveObservedKda",
            _ => "liveCombatSlice"
        };
        WindowName = crystalline
            ? $"{T("crystallineMode")} · {T(titleKey)}###BattlefieldLiveCombat-{page}"
            : $"{T(titleKey)}###BattlefieldLiveCombat-{page}";
        if (ui.MatchUid.Length > 0 && ui.MatchUid != activeMatchUid)
        {
            activeMatchUid = ui.MatchUid; frozen = false; cached = []; cachedCoordination = [];
            cachedMatchTendencyRows = []; cachedMatchCoordination = [];
            cachedObservedKda = [];
            cachedEndMs = -1; cachedMatchEvidenceEndMs = -1; nextRefreshAt = 0;
        }
        var now = Environment.TickCount64;
        if (!frozen && now >= nextRefreshAt)
        {
            Refresh(ui);
            nextRefreshAt = now + 250;
        }
        if (TransparentMarkerOverlay)
        {
            DrawLiveMarkerMap(ui, true);
            return;
        }
        if (page == LiveCombatPage.MarkerMap)
        {
            DrawLiveMarkerMap(ui, false);
            return;
        }
        if (page == LiveCombatPage.TendencyCandidates)
        {
            if (crystalline)
            {
                ImGui.TextDisabled(T("liveTendencyUnavailableCrystalline"));
            }
            else DrawLiveTendencyCandidates(ui);
            return;
        }
        if (page == LiveCombatPage.ObservedKda) { DrawObservedKda(); return; }

        ImGui.SetNextItemWidth(155);
        var options = new[] { 15, 30, 60 }; var index = Array.IndexOf(options, seconds);
        if (ImGui.Combo(T("secondsBeforeEncounterEnd"), ref index, "15 s\0 30 s\0 60 s\0")) seconds = options[index];
        ImGui.SameLine(); ImGui.SetNextItemWidth(190);
        ImGui.Combo(T("liveDirection"), ref direction,
            $"{T("liveAll")}\0{T("liveOutgoing")}\0{T("liveIncoming")}\0");
        ImGui.SameLine();
        if (ImGui.Button(frozen ? T("resumeBuffer") : T("freezeBuffer")))
        {
            if (!frozen) { Refresh(ui); frozen = true; }
            else { frozen = false; nextRefreshAt = 0; }
        }

        ImGui.SameLine();
        var bufferState = frozen ? T("bufferFrozen") : cachedEncounterActive ? T("encounterActive") : T("lastEncounterStored");
        ImGui.TextColored(frozen ? new Vector4(1f,.72f,.28f,1) : new Vector4(.45f,.8f,1f,1), bufferState);
        if (copiedUntil > now) { ImGui.SameLine(); ImGui.TextColored(new Vector4(.35f,.9f,.58f,1), T("copied")); }
        var cutoff = cachedEndMs - seconds * 1000L;
        var actorColumns = LiveCombatPageContract.ActorColumns(direction);
        var tableColumnCount = 5 + (actorColumns.ShowSource ? 1 : 0) +
                               (actorColumns.ShowTarget ? 1 : 0);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(3, 1));
        if (!ImGui.BeginTable("live-actions", tableColumnCount,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH |
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp,
            new Vector2(0, -1)))
        {
            ImGui.PopStyleVar();
            return;
        }
        ImGui.TableSetupColumn(T("beforeEncounterEnd"), ImGuiTableColumnFlags.WidthFixed, 58);
        if (actorColumns.ShowSource)
            ImGui.TableSetupColumn(T("source"), ImGuiTableColumnFlags.WidthStretch, 1.15f);
        ImGui.TableSetupColumn(T("action"), ImGuiTableColumnFlags.WidthStretch, 1.15f);
        if (actorColumns.ShowTarget)
            ImGui.TableSetupColumn(T("target"), ImGuiTableColumnFlags.WidthStretch, 1.15f);
        ImGui.TableSetupColumn(T("amount"), ImGuiTableColumnFlags.WidthFixed, 72);
        ImGui.TableSetupColumn(T("hpAndShield"), ImGuiTableColumnFlags.WidthStretch, .9f);
        ImGui.TableSetupColumn(T("operations"), ImGuiTableColumnFlags.WidthFixed, 54);
        ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
        for (var i = cached.Count - 1; i >= 0; i--)
        {
            var row = cached[i];
            if (row.ElapsedMs < cutoff) continue;
            var outgoing = IsLocal(row.SourceEntityId, row.SourceName, row.SourceWorldId, ui);
            var incoming = IsLocal(row.TargetEntityId, row.TargetName, row.TargetWorldId, ui);
            if (direction == 1 && !outgoing || direction == 2 && !incoming) continue;
            var sourceSide = incoming && !outgoing && row.Kind == "damage" ? LiveSide.Enemy
                : outgoing || incoming && row.Kind == "heal" ? LiveSide.Own : LiveSide.Unknown;
            var targetSide = outgoing && !incoming && row.Kind == "damage" ? LiveSide.Enemy
                : incoming || outgoing && row.Kind == "heal" ? LiveSide.Own : LiveSide.Unknown;
            ImGui.TableNextRow(ImGuiTableRowFlags.None, 21); Cell(RelativeTime(row.ElapsedMs));
            if (actorColumns.ShowSource)
            {
                ImGui.TableNextColumn(); DrawActor(row.SourceJobId, row.SourceName, row.SourceWorldId,
                    row.SourceTeam, row.SourceBattalion, ui.LocalPlayerBattalion, sourceSide, crystalline);
            }
            ImGui.TableNextColumn(); DrawAction(row.ActionId, row.ActionType, 18);
            if (actorColumns.ShowTarget)
            {
                ImGui.TableNextColumn(); DrawActor(row.TargetJobId, row.TargetName, row.TargetWorldId,
                    row.TargetTeam, row.TargetBattalion, ui.LocalPlayerBattalion, targetSide, crystalline);
            }
            ImGui.TableNextColumn(); ImGui.TextColored(row.Kind == "heal" ? new Vector4(.35f,.86f,.58f,1) : new Vector4(1f,.52f,.42f,1),
                $"{(row.Kind == "heal" ? "+" : "-")}{row.Amount:N0}");
            ImGui.TableNextColumn();
            var hpWidth = Math.Max(46, ImGui.GetContentRegionAvail().X);
            CombatUiPrimitives.DrawHpShieldBar(row.TargetCurrentHp, row.TargetMaxHp,
                row.TargetShieldPercent, new Vector2(hpWidth, 15),
                $"live-{i}-{row.ElapsedMs}-{row.TargetEntityId}");
            ImGui.TableNextColumn(); DrawOperations(row, incoming, outgoing, sourceSide, targetSide,
                ui.LocalPlayerBattalion, i, crystalline);
        }
        ImGui.EndTable();
        ImGui.PopStyleVar();
    }

    private void DrawLiveMarkerMap(CollectorUiState ui, bool transparentOverlay)
    {
        var presentation = LiveCombatPageContract.MarkerPresentation(transparentOverlay);
        var snapshot = markers.Snapshot();
        var available = ImGui.GetContentRegionAvail();
        var size = new Vector2(Math.Max(420, available.X),
            Math.Max(300, available.Y - (transparentOverlay ? 4 : 0)));
        var origin = ImGui.GetCursorScreenPos();
        var viewport = MarkerUiPrimitives.BeginMapCanvas("live-marker-map-canvas", size,
            ref markerMapZoom, ref markerMapCenterUv, 1.45f, 34f);
        var afterCanvas = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        if (!transparentOverlay)
        {
            draw.AddRectFilled(origin, origin + size,
                ImGui.ColorConvertFloat4ToU32(BattlefieldUiTheme.ChartBackground(configuration.UiTheme)), 2);
            draw.AddRect(origin, origin + size,
                ImGui.ColorConvertFloat4ToU32(BattlefieldUiTheme.Border(configuration.UiTheme)), 2);
        }

        var spanX = viewport.SpanX; var spanY = viewport.SpanY;
        var uvMin = viewport.UvMin; var uvMax = viewport.UvMax;

        var mapFactor = 100;
        var mapOffsetX = 0;
        var mapOffsetY = 0;
        if (presentation.DrawMapTexture && MapAssets.TryGetValue(ui.DutyId, out var mapAsset))
        {
            try
            {
                if (!mapTextures.TryGetValue(ui.DutyId, out var texture))
                    mapTextures[ui.DutyId] = texture = textureProvider.GetFromFile(
                        Path.Combine(assetRoot, "assets", "maps", mapAsset.FileName));
                var wrap = texture.GetWrapOrDefault();
                if (wrap != null) draw.AddImage(wrap.Handle, origin, origin + size, uvMin, uvMax,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, .9f)));
            }
            catch { /* Optional map asset: coordinate grid remains usable. */ }
        }
        else if (presentation.DrawMapTexture && GameLayout.IsCrystallineDuty(ui.DutyId) && ui.TerritoryId != 0)
        {
            try
            {
                var territory = dataManager.GetExcelSheet<TerritoryTypeSheet>().GetRow(ui.TerritoryId);
                var map = territory.Map.Value;
                mapFactor = Math.Max(1, (int)map.SizeFactor);
                mapOffsetX = map.OffsetX;
                mapOffsetY = map.OffsetY;
                var id = map.Id.ToString();
                var texturePath = $"ui/map/{id}/{id.Replace("/", string.Empty)}_m.tex";
                var textureKey = 0x8000_0000u | ui.TerritoryId;
                if (!mapTextures.TryGetValue(textureKey, out var texture))
                    mapTextures[textureKey] = texture = textureProvider.GetFromGame(texturePath);
                var wrap = texture.GetWrapOrDefault();
                if (wrap != null) draw.AddImage(wrap.Handle, origin, origin + size, uvMin, uvMax,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, .9f)));
            }
            catch { /* Keep the coordinate grid when the game map texture is unavailable. */ }
        }

        Vector2 Point(float x, float z)
        {
            var uv = MapCoordinateProjection.WorldToUv(x, z, mapFactor, mapOffsetX, mapOffsetY);
            return origin + new Vector2((uv.X - uvMin.X) / spanX * size.X,
                (uv.Y - uvMin.Y) / spanY * size.Y);
        }

        draw.PushClipRect(origin, origin + size, true);
        for (var i = 1; presentation.DrawGrid && i < 8; i++)
        {
            var grid = ImGui.ColorConvertFloat4ToU32(new Vector4(.22f, .3f, .32f, .28f));
            var x = origin.X + i * size.X / 8; var y = origin.Y + i * size.Y / 8;
            draw.AddLine(new Vector2(x, origin.Y), new Vector2(x, origin.Y + size.Y), grid);
            draw.AddLine(new Vector2(origin.X, y), new Vector2(origin.X + size.X, y), grid);
        }
        foreach (var field in snapshot.Fields)
            DrawMarkerIcon(draw, Point(field.X, field.Z), field.Key, true, 14f, field.Index);
        var weather = snapshot.Objectives
            .Where(x => x.Id == WorqorWeatherMechanics.WeatherObjectiveId ||
                        x.Label == WorqorWeatherMechanics.WeatherLabel)
            .OrderByDescending(x => WorqorWeatherMechanics.IsWarningState(x.State))
            .FirstOrDefault();
        if (ui.DutyId == 127)
        {
            foreach (var node in SecureObjectiveCatalog.Nodes)
            {
                var dynamicState = snapshot.Objectives
                    .Where(row => !SecureObjectiveCatalog.IsFixedNodeId(row.Id) &&
                                  SecureObjectiveCatalog.TryGetMapIcon(row.State, out var icon) &&
                                  SecureObjectiveCatalog.IsOccupationIcon(icon) &&
                                  Math.Pow(row.X - node.X, 2) + Math.Pow(row.Z - node.Z, 2) <= 12 * 12)
                    .FirstOrDefault();
                var owner = dynamicState != null && dynamicState.OwnerTeam < 3
                    ? dynamicState.OwnerTeam : (byte?)null;
                var neutral = dynamicState != null &&
                              SecureObjectiveCatalog.TryGetMapIcon(dynamicState.State, out var icon) &&
                              SecureObjectiveCatalog.IsNeutralOccupationIcon(icon);
                var ownerColor = owner is byte team ? TeamColors[team]
                    : neutral ? new Vector4(.92f, .92f, .82f, 1f)
                    : new Vector4(.56f, .60f, .62f, .92f);
                var labelColor = node.HomeTeam is byte homeTeam ? TeamColors[homeTeam] : ownerColor;
                ObjectiveMapGlyphRenderer.DrawSecureNode(draw, Point(node.X, node.Z), node.Code,
                    node.HomeTeam.HasValue, ownerColor, labelColor);
            }
        }

        IEnumerable<LiveObjectiveMarker> objectivesToRender = snapshot.Objectives;
        if (ui.DutyId == SealRockObjectiveCatalog.DutyId)
        {
            var spawns = snapshot.Objectives.Where(row =>
                FrontlineSpawnCatalog.TryGetTeam(row.State, out _));
            var reduced = SealRockGlobalObjectiveReducer.ReduceLive(snapshot.Objectives)
                .Select(point => point.ToObjectiveStateEvent())
                .Select(row => new LiveObjectiveMarker(row.ObjectId, row.Label, row.State,
                    row.OwnerTeam ?? 255, (float)row.X, (float)row.Y, (float)row.Z,
                    EvidenceScope: row.EvidenceScope, ProgressPercent: row.ProgressPercent));
            objectivesToRender = spawns.Concat(reduced);
        }

        foreach (var objective in objectivesToRender.Where(x =>
                     x.Id != WorqorWeatherMechanics.WeatherObjectiveId &&
                     x.Label != WorqorWeatherMechanics.WeatherLabel &&
                     (ui.DutyId != 127 ||
                      !SecureObjectiveCatalog.IsFixedNodeId(x.Id) &&
                      (!SecureObjectiveCatalog.TryGetMapIcon(x.State, out var secureIcon) ||
                       !SecureObjectiveCatalog.IsOccupationIcon(secureIcon))) &&
                     (!GameLayout.IsCrystallineDuty(ui.DutyId) ||
                      !IsRawMapIdentifier(x.Label) ||
                      CrystallineMapMarkerCatalog.TryGetIcon(x.State, out _)) &&
                     (x.MaxHp == 0 || x.CurrentHp > 1) &&
                     (ui.DutyId != ShatterObjectiveCatalog.DutyId ||
                      !string.Equals(x.EvidenceScope, ShatterObjectiveCatalog.GlobalMapEvidenceScope,
                          StringComparison.Ordinal) ||
                      ShatterObjectiveCatalog.ShouldDisplayMapState(x.State, x.RemainingSeconds,
                          x.ProgressPercent, x.DisplayText, x.Label)) &&
                     (ui.DutyId != OnsalObjectiveCatalog.DutyId ||
                      FrontlineSpawnCatalog.TryGetTeam(x.State, out _) ||
                      OnsalObjectiveCatalog.IsObjectiveState(x.State))))
        {
            var point = Point(objective.X, objective.Z);
            if (FrontlineSpawnCatalog.TryGetTeam(objective.State, out var spawnTeam))
            {
                var spawnColor = TeamColors[spawnTeam];
                ObjectiveMapGlyphRenderer.DrawSpawnLabel(draw, point, TeamName(spawnTeam),
                    spawnColor, ui.LocalPlayerBattalion == spawnTeam);
                continue;
            }
            var ownerTeam = objective.OwnerTeam < 3 ? objective.OwnerTeam :
                ui.DutyId == OnsalObjectiveCatalog.DutyId
                    ? OnsalObjectiveCatalog.OwnerFromState(objective.State) ?? (byte)255
                    : (byte)255;
            var onsalNeutral = ui.DutyId == OnsalObjectiveCatalog.DutyId &&
                               OnsalObjectiveCatalog.IsNeutralState(objective.State);
            var color = GameLayout.IsCrystallineDuty(ui.DutyId) && ownerTeam < 2
                ? CrystallineTeamColors[ownerTeam]
                : ownerTeam < 3
                    ? TeamColors[ownerTeam]
                    : onsalNeutral
                        ? new Vector4(.92f, .92f, .82f, 1f)
                        : new Vector4(.48f, .52f, .55f, .9f);
            if (ui.DutyId == WorqorObjectiveCatalog.DutyId)
                draw.AddCircleFilled(point, 10f, ImGui.ColorConvertFloat4ToU32(
                    color with { W = ownerTeam < 3 ? .46f : .18f }), 24);
            ObjectiveMapGlyphRenderer.Draw(draw, point, ui.DutyId,
                objective.Label ?? string.Empty, objective.MaxHp, color,
                (objective.State ?? string.Empty).Contains("captur",
                    StringComparison.OrdinalIgnoreCase), objective.State ?? string.Empty);
            var shatterPercent = ui.DutyId == ShatterObjectiveCatalog.DutyId
                ? ShatterObjectiveCatalog.ResolveDisplayedHealthPercent(
                    objective.CurrentHp, objective.MaxHp, objective.ProgressPercent)
                : null;
            var suffix = shatterPercent.HasValue
                ? $" {shatterPercent.Value:0}%"
                : objective.ProgressPercent.HasValue
                    ? $" {objective.ProgressPercent.Value:0}%"
                    : objective.MaxHp > 0
                        ? $" {100d * objective.CurrentHp / objective.MaxHp:0}%"
                        : (objective.RemainingSeconds ??
                           (ui.DutyId == OnsalObjectiveCatalog.DutyId
                               ? OnsalObjectiveCatalog.ParseDisplayedCountdownSeconds(objective.DisplayText) ??
                                 OnsalObjectiveCatalog.ParseDisplayedCountdownSeconds(objective.Label)
                               : null)) is int remainingSeconds
                            ? $" T-{remainingSeconds}s"
                            : ui.DutyId == OnsalObjectiveCatalog.DutyId ||
                              string.IsNullOrWhiteSpace(objective.DisplayText)
                                ? string.Empty : $" {objective.DisplayText}";
            var objectiveLabel = ui.DutyId == WorqorObjectiveCatalog.DutyId
                ? WorqorObjectiveCatalog.ShortPointCode(objective.Label)
                : ui.DutyId == OnsalObjectiveCatalog.DutyId
                    ? OnsalObjectiveCatalog.ShortPointCode(objective.Label)
                : ui.DutyId == ShatterObjectiveCatalog.DutyId
                    ? ShatterObjectiveCatalog.DisplayCode(objective.Label, objective.X, objective.Z)
                : objective.Label == "cc_tactical_crystal"
                    ? T("ccTacticalCrystal")
                : objective.Label == "cc_tactical_checkpoint"
                    ? T("ccHarmoniasCheckpoint")
                    : objective.Label;
            if (string.IsNullOrWhiteSpace(objectiveLabel))
                objectiveLabel = ui.DutyId is ShatterObjectiveCatalog.DutyId or OnsalObjectiveCatalog.DutyId
                    ? T("unresolvedObjective") : objective.Label;
            // The global point icon proves only the final aggregate owner. It
            // does not expose each field's colour during a contested capture,
            // so never fabricate N/N from that aggregate state.
            draw.AddText(point + new Vector2(11, -9),
                ImGui.ColorConvertFloat4ToU32(Vector4.One), objectiveLabel + suffix);
            if (ui.DutyId == OnsalObjectiveCatalog.DutyId &&
                (objective.RemainingSeconds ??
                 OnsalObjectiveCatalog.ParseDisplayedCountdownSeconds(objective.DisplayText) ??
                 OnsalObjectiveCatalog.ParseDisplayedCountdownSeconds(objective.Label)) is int onsalSeconds)
            {
                DrawCompactCountdownBar(draw, point + new Vector2(-16, 12), onsalSeconds, color);
            }
        }
        var crystallinePlayers = GameLayout.IsCrystallineDuty(ui.DutyId);
        foreach (var player in snapshot.Players.Where(x => !x.IsLocal))
            DrawLivePlayerMarker(draw, Point(player.X, player.Z), player,
                LivePlayerMapContract.Resolve(crystallinePlayers, player.Battalion,
                    ui.LocalPlayerBattalion, false));
        foreach (var player in snapshot.Players.Where(x => x.IsLocal))
            DrawLivePlayerMarker(draw, Point(player.X, player.Z), player,
                LivePlayerRelation.Local);
        foreach (var target in snapshot.Targets.Where(x => x.PositionVisible))
        {
            var point = Point(target.X, target.Z);
            DrawMarkerIcon(draw, point, target.Key, false, 10f, target.Index);
            if (!string.IsNullOrWhiteSpace(target.Name))
                draw.AddText(point + new Vector2(13, -9), ImGui.ColorConvertFloat4ToU32(Vector4.One),
                    Display(target.Name, target.WorldId));
        }
        if (snapshot.Fields.Count == 0 && snapshot.Targets.Count == 0 &&
            snapshot.Objectives.Count == 0 && snapshot.Players.Count == 0)
        {
            var message = T("noMarkerEvents");
            var extent = ImGui.CalcTextSize(message);
            draw.AddText(origin + (size - extent) / 2,
                ImGui.ColorConvertFloat4ToU32(new Vector4(.53f, .61f, .66f, 1f)), message);
        }
        if (!transparentOverlay)
            draw.AddText(origin + new Vector2(12, 10), ImGui.ColorConvertFloat4ToU32(Vector4.One),
                MapAssets.TryGetValue(ui.DutyId, out var asset) ? T(asset.NameKey) : T("mapOverlay"));
        if (ui.DutyId == WorqorWeatherMechanics.DutyId && weather != null)
        {
            var key = WorqorWeatherMechanics.TextKey(weather.State);
            if (!string.IsNullOrEmpty(key))
            {
                var text = WorqorWeatherMechanics.IsWarningState(weather.State) &&
                           weather.RemainingSeconds is int seconds
                    ? string.Format(T(key), seconds)
                    : T(key);
                draw.AddText(origin + new Vector2(12, 32),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(.30f, .88f, 1f, 1f)), text);
            }
        }
        if (!transparentOverlay)
            draw.AddText(origin + new Vector2(12, size.Y - 23),
                ImGui.ColorConvertFloat4ToU32(new Vector4(.53f, .61f, .66f, 1f)), T("mapControls"));
        draw.PopClipRect();
        if (presentation.AcceptPointerInput)
        {
            var height = Math.Max(80, size.Y - 88);
            ImGui.SetCursorScreenPos(origin + new Vector2(size.X - 27, 42));
            ImGui.PushStyleColor(ImGuiCol.FrameBg,
                BattlefieldUiTheme.Panel(configuration.UiTheme) with { W = .88f });
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, BattlefieldUiTheme.Accent(configuration.UiTheme));
            ImGui.VSliderFloat("##live-marker-map-zoom", new Vector2(17, height),
                ref markerMapZoom, 1f, 8f, "");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{T("mapZoom")} {markerMapZoom:0.0}×");
            ImGui.PopStyleColor(2);
        }
        ImGui.SetCursorScreenPos(afterCanvas);
    }

    private static bool IsRawMapIdentifier(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return true;
        var value = label.Trim();
        return value.StartsWith("icon:", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith('#') && value[1..].All(char.IsDigit);
    }

    private static void DrawCompactObjectivePolygon(ImDrawListPtr draw, Vector2 center, int sides,
        Vector4 color)
    {
        if (sides is < 3 or > 5) return;
        var rotation = sides == 4 ? MathF.PI / 4 : -MathF.PI / 2;
        var fill = ImGui.ColorConvertFloat4ToU32(color with { W = .46f });
        var outline = ImGui.ColorConvertFloat4ToU32(color);
        for (var index = 0; index < sides; index++)
        {
            var fromAngle = rotation + index * MathF.Tau / sides;
            var toAngle = rotation + (index + 1) * MathF.Tau / sides;
            var from = center + new Vector2(MathF.Cos(fromAngle), MathF.Sin(fromAngle)) * 9f;
            var to = center + new Vector2(MathF.Cos(toAngle), MathF.Sin(toAngle)) * 9f;
            draw.AddTriangleFilled(center, from, to, fill);
            draw.AddLine(from, to, outline, 2.5f);
        }
    }

    private static void DrawCompactCountdownBar(ImDrawListPtr draw, Vector2 at, int remainingSeconds,
        Vector4 color)
    {
        const float width = 32, height = 4;
        draw.AddRectFilled(at, at + new Vector2(width, height),
            ImGui.ColorConvertFloat4ToU32(new Vector4(.04f, .06f, .07f, .88f)), 2);
        draw.AddRectFilled(at, at + new Vector2(width *
                (float)(OnsalObjectiveCatalog.CountdownPercent(remainingSeconds) / 100d), height),
            ImGui.ColorConvertFloat4ToU32(color), 2);
    }

    private static void DrawLivePlayerMarker(ImDrawListPtr draw, Vector2 center,
        LivePlayerMarker player, LivePlayerRelation relation)
    {
        var color = relation switch
        {
            LivePlayerRelation.Local => MapLocalPlayerColor,
            LivePlayerRelation.Ally => MapAllyPlayerColor,
            LivePlayerRelation.Enemy => MapEnemyPlayerColor,
            _ => MapUnknownPlayerColor
        };
        if (!player.IsAlive) color = color with { W = .52f };
        var fill = ImGui.ColorConvertFloat4ToU32(color);
        var outline = ImGui.ColorConvertFloat4ToU32(
            relation == LivePlayerRelation.Local
                ? new Vector4(1f, 1f, 1f, .96f)
                : new Vector4(.06f, .08f, .09f, .82f));
        var radius = relation == LivePlayerRelation.Local ? 8f : 6f;
        var direction = new Vector2(MathF.Sin(player.Rotation), -MathF.Cos(player.Rotation));
        var right = new Vector2(-direction.Y, direction.X);
        var tip = center + direction * radius;
        var left = center - direction * radius * .62f + right * radius * .72f;
        var rightPoint = center - direction * radius * .62f - right * radius * .72f;
        draw.AddTriangleFilled(tip, left, rightPoint, fill);
        draw.AddLine(tip, left, outline, relation == LivePlayerRelation.Local ? 2f : 1.2f);
        draw.AddLine(left, rightPoint, outline, relation == LivePlayerRelation.Local ? 2f : 1.2f);
        draw.AddLine(rightPoint, tip, outline, relation == LivePlayerRelation.Local ? 2f : 1.2f);
        if (!player.IsAlive)
        {
            var cross = radius * .72f;
            var dead = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, .9f));
            draw.AddLine(center - new Vector2(cross), center + new Vector2(cross), dead, 1.5f);
            draw.AddLine(center + new Vector2(-cross, cross),
                center + new Vector2(cross, -cross), dead, 1.5f);
        }
    }

    private void DrawMarkerIcon(ImDrawListPtr draw, Vector2 center, string markerKey,
        bool field, float radius, int markerIndex = -1)
    {
        var iconId = MarkerUiPrimitives.IconId(markerKey, field, markerIndex);
        if (iconId != 0)
        {
            try
            {
                if (!markerIcons.TryGetValue(iconId, out var texture))
                {
                    var bundled = Path.Combine(assetRoot, "assets", "markers",
                        MarkerUiPrimitives.AssetFileName(iconId));
                    markerIcons[iconId] = texture = File.Exists(bundled)
                        ? textureProvider.GetFromFile(bundled)
                        : textureProvider.GetFromGameIcon(new GameIconLookup(iconId, hiRes: true));
                }
                var wrap = texture.GetWrapOrDefault();
                if (wrap != null)
                {
                    var extent = new Vector2(radius * 2.35f);
                    draw.AddImage(wrap.Handle, center - extent / 2, center + extent / 2);
                    return;
                }
            }
            catch { /* Never replace a missing official icon with a different-looking badge. */ }
            return;
        }
        var muted = ImGui.ColorConvertFloat4ToU32(new Vector4(.53f, .61f, .66f, 1f));
        draw.AddCircle(center, radius, muted, 16, 1.5f);
        draw.AddText(center - ImGui.CalcTextSize("?") / 2, muted, "?");
    }

    private void Refresh(CollectorUiState ui)
    {
        var snapshot = buffer.Snapshot(ui.ElapsedMs);
        cached = snapshot.Rows; cachedCoordination = snapshot.Coordination;
        cachedMatchTendencyRows = snapshot.MatchTendencyRows;
        cachedMatchCoordination = snapshot.MatchCoordination;
        cachedObservedKda = snapshot.ObservedKda;
        cachedEndMs = snapshot.EndElapsedMs;
        cachedMatchEvidenceEndMs = snapshot.MatchEvidenceEndElapsedMs;
        cachedEncounterActive = snapshot.IsActive;
    }

    private void DrawObservedKda()
    {
        var rows = cachedObservedKda.Where(x => x.Kills + x.Deaths + x.Assists > 0).ToArray();
        if (rows.Length == 0) { ImGui.TextDisabled(T("noObservedKda")); return; }
        if (!ImGui.BeginTable("live-observed-kda", 4,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH |
                ImGuiTableFlags.SizingStretchProp)) return;
        ImGui.TableSetupColumn(T("player"), ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("K", ImGuiTableColumnFlags.WidthFixed, 42);
        ImGui.TableSetupColumn("D", ImGuiTableColumnFlags.WidthFixed, 42);
        ImGui.TableSetupColumn("A", ImGuiTableColumnFlags.WidthFixed, 42);
        ImGui.TableSetupScrollFreeze(0, 1); ImGui.TableHeadersRow();
        foreach (var row in rows)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn(); DrawJob(row.JobId); ImGui.SameLine(0, 4);
            ImGui.TextUnformatted(Display(row.Name, row.WorldId));
            ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Kills.ToString());
            ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Deaths.ToString());
            ImGui.TableNextColumn(); ImGui.TextUnformatted(row.Assists.ToString());
        }
        ImGui.EndTable();
    }

    private void DrawLiveTendencyCandidates(CollectorUiState ui)
    {
        if (!ui.Capturing || cachedMatchEvidenceEndMs < 0)
        {
            ImGui.TextDisabled(T("noLiveTendencyEvidence"));
            return;
        }
        var result = LiveTendencyAnalyzer.Analyze(cachedMatchTendencyRows, cachedMatchCoordination,
            ui.LocalPlayerBattalion, cachedMatchEvidenceEndMs);
        DrawGroupCandidates(T("coordination_core"), result.Coordination);
        ImGui.Separator();
        DrawActorCandidates(T("commander"), result.Commanders);
        ImGui.Separator();
        DrawActorCandidates(T("disruptor"), result.Disruptors);
    }

    private void DrawGroupCandidates(string title, IReadOnlyList<LiveGroupCandidate> candidates)
    {
        ImGui.TextColored(new Vector4(.35f, .83f, .91f, 1f), title);
        if (candidates.Count == 0) { ImGui.TextDisabled(T("noLiveTendencyEvidence")); return; }
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            ImGui.TextColored(LogsColor(candidate.Score), $"{candidate.Score:0.0}");
            ImGui.SameLine(); ImGui.TextDisabled(string.Format(T("sharedBurstBuckets"), candidate.SharedBurstBuckets));
            foreach (var member in candidate.Members)
            {
                ImGui.SameLine(0, 8); DrawLiveCandidateActor(member, $"group-{i}-{member.EntityId}");
            }
        }
    }

    private void DrawActorCandidates(string title, IReadOnlyList<LiveActorCandidate> candidates)
    {
        ImGui.TextColored(new Vector4(.35f, .83f, .91f, 1f), title);
        if (candidates.Count == 0) { ImGui.TextDisabled(T("noLiveTendencyEvidence")); return; }
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            ImGui.TextColored(LogsColor(candidate.Score), $"{candidate.Score:0.0}");
            ImGui.SameLine(0, 8); DrawLiveCandidateActor(candidate.Actor, $"actor-{title}-{i}");
            ImGui.SameLine(0, 8); ImGui.TextDisabled(string.Format(T("evidenceCount"), candidate.EvidenceCount));
        }
    }

    private void DrawLiveCandidateActor(LiveActorRef actor, string id)
    {
        DrawJob(actor.JobId); ImGui.SameLine(0, 4);
        var display = Display(actor.Name, actor.WorldId);
        ImGui.TextUnformatted(display);
        if (ImGui.BeginPopupContextItem($"candidate-menu-{id}"))
        {
            if (ImGui.MenuItem(T("copyPlayerName"))) ImGui.SetClipboardText(display);
            ImGui.BeginDisabled(actor.EntityId == 0);
            if (ImGui.MenuItem(T("selectVisiblePlayer")))
                tryTargetVisiblePlayer(actor.EntityId, actor.Name, actor.WorldId);
            ImGui.EndDisabled();
            ImGui.EndPopup();
        }
        ImGui.SameLine(0, 5);
        if (ImGui.SmallButton($"⧉##copy-{id}")) ImGui.SetClipboardText(display);
        ImGui.SameLine(0, 3);
        ImGui.BeginDisabled(actor.EntityId == 0);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(1, 0));
        if (ImGui.SmallButton($"◎##target-{id}"))
            tryTargetVisiblePlayer(actor.EntityId, actor.Name, actor.WorldId);
        ImGui.PopStyleVar();
        ImGui.EndDisabled();
    }

    private static Vector4 LogsColor(double value) => value >= 95 ? new Vector4(1f, .35f, .78f, 1)
        : value >= 75 ? new Vector4(1f, .55f, .2f, 1) : value >= 50 ? new Vector4(.3f, .65f, 1f, 1)
        : value >= 25 ? new Vector4(.35f, .8f, .45f, 1) : new Vector4(.6f, .62f, .65f, 1);

    private string RelativeTime(long elapsedMs)
    {
        if (cachedEndMs < 0) return "—";
        var delta = Math.Max(0, cachedEndMs - elapsedMs) / 1000.0;
        return delta < .05 ? "0.0s" : $"-{delta:0.0}s";
    }

    private void DrawOperations(LiveCombatRow row, bool incoming, bool outgoing,
        LiveSide sourceSide, LiveSide targetSide, byte localBattalion, int index, bool crystalline)
    {
        // The targeting button always means "select the other side of my event":
        // my outgoing event -> target; incoming event -> source.  For a row not
        // involving the local player, source is the deterministic fallback.
        // 选中按钮始终指向与本机交互的另一端：我方输出选目标，我方承受选来源。
        var targetTheTarget = outgoing;
        var entityId = targetTheTarget ? row.TargetEntityId : row.SourceEntityId;
        var name = targetTheTarget ? row.TargetName : row.SourceName;
        var worldId = targetTheTarget ? row.TargetWorldId : row.SourceWorldId;
        var canTarget = entityId != 0 && name.Length > 0;
        ImGui.BeginDisabled(!canTarget);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.SmallButton($"◎##live-target-{index}-{row.ElapsedMs}-{entityId}");
        ImGui.PopStyleVar();
        var held = ImGui.IsItemActive();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(canTarget
                ? T(targetTheTarget ? "holdToTargetVisibleTarget" : "holdToTargetVisibleSource")
                : T("targetActorUnavailable"));
        ImGui.EndDisabled();
        if (held && !tryTargetVisiblePlayer(entityId, name, worldId))
        {
            ImGui.SameLine(); ImGui.TextDisabled("×");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("targetActorUnavailable"));
        }
        ImGui.SameLine(0, 3);
        if (ImGui.SmallButton($"⧉##live-copy-{index}-{row.ElapsedMs}-{row.ActionId}"))
        {
            ImGui.SetClipboardText(BuildEvidenceText(row, sourceSide, targetSide,
                localBattalion, crystalline));
            copiedUntil = Environment.TickCount64 + 1800;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(T("copyCombatRecord"));
    }

    private string BuildEvidenceText(LiveCombatRow row, LiveSide sourceSide, LiveSide targetSide,
        byte localBattalion, bool crystalline)
    {
        var shield = CombatUiPrimitives.ShieldAmount(row.TargetMaxHp, row.TargetShieldPercent);
        var statuses = row.TargetStatusIds is { Count: > 0 }
            ? string.Join(", ", row.TargetStatusIds.Select(id => StatusName(id))) : T("noCapturedStatuses");
        var amountKind = row.Kind == "heal" ? T("healingEvent") : T("damage");
        return $"[{TimeSpan.FromMilliseconds(row.ElapsedMs):mm\\:ss}] " +
               $"{T("source")}: {ActorText(row.SourceName, row.SourceWorldId, row.SourceTeam, row.SourceBattalion, localBattalion, sourceSide, crystalline)} | " +
               $"{T("action")}: {ActionName(row.ActionId)} | {amountKind}: {row.Amount:N0} | " +
               $"{T("target")}: {ActorText(row.TargetName, row.TargetWorldId, row.TargetTeam, row.TargetBattalion, localBattalion, targetSide, crystalline)} | " +
               $"{T("stateAfterEvent")}: HP {row.TargetCurrentHp:N0}/{row.TargetMaxHp:N0} (+{T("shield")} {shield:N0}); " +
               $"{T("statuses")}: {statuses}";
    }

    private bool IsLocal(uint entityId, string name, ushort worldId, CollectorUiState ui)
    {
        if (ui.LocalPlayerEntityId != 0 && entityId == ui.LocalPlayerEntityId) return true;
        return ui.LocalPlayerName.Length > 0 && name == ui.LocalPlayerName &&
               (ui.LocalPlayerWorldId == 0 || worldId == 0 || worldId == ui.LocalPlayerWorldId);
    }

    private void DrawActor(byte jobId, string name, ushort worldId, byte team, byte battalion,
        byte localBattalion, LiveSide side, bool crystalline)
    {
        DrawJob(jobId, 18); ImGui.SameLine(0, 3);
        var isOwn = localBattalion <= (crystalline ? 1 : 2) && battalion == localBattalion ||
                    side == LiveSide.Own;
        var displayedTeam = LiveCombatPageContract.ResolveDisplayedTeam(crystalline, team, battalion);
        var color = crystalline && displayedTeam is byte ccTeam
            ? CrystallineTeamColors[ccTeam]
            : !crystalline && displayedTeam is byte frontlineTeam
                ? TeamColors[frontlineTeam]
                : isOwn ? OwnSideColor : side == LiveSide.Enemy ? EnemySideColor : Vector4.One;
        ImGui.TextColored(color, Display(name, worldId));
    }

    private string ActorText(string name, ushort worldId, byte team, byte battalion,
        byte localBattalion, LiveSide side, bool crystalline)
    {
        var label = TeamOrSideText(team, battalion, localBattalion, side, crystalline);
        return label.Length == 0 ? Display(name, worldId) : $"{Display(name, worldId)} · {label}";
    }

    private string TeamOrSideText(byte team, byte battalion, byte localBattalion, LiveSide side,
        bool crystalline)
    {
        if (crystalline)
        {
            var ccTeam = ResolveCrystallineTeam(team, battalion);
            return ccTeam switch { 0 => T("ccAstra"), 1 => T("ccUmbra"), _ => string.Empty };
        }
        if (team < 3) return team switch { 0 => T("maelstrom"), 1 => T("adders"), _ => T("flames") };
        if (battalion <= 2)
        {
            if (localBattalion <= 2 && battalion == localBattalion) return T("ownSide");
            if (localBattalion <= 2) return $"{T("enemyBattlefieldGroup")} {battalion + 1}";
            return $"{T("battlefieldGroup")} {battalion + 1}";
        }
        return side == LiveSide.Own ? T("ownSide")
            : side == LiveSide.Enemy ? T("enemySide") : string.Empty;
    }

    private static byte? ResolveCrystallineTeam(byte team, byte battalion) =>
        team <= 1 ? team : battalion <= 1 ? battalion : null;

    private string TeamName(byte team) => team switch
    {
        0 => T("maelstrom"), 1 => T("adders"), 2 => T("flames"), _ => T("unknownTeam")
    };

    private static void Cell(string value) { ImGui.TableNextColumn(); ImGui.TextUnformatted(value); }
    private string Display(string name, ushort world) => string.IsNullOrWhiteSpace(name) ? T("unresolvedActor")
        : world == 0 ? $"{name}@{T("unknownWorld")}" : $"{name}@{WorldName(world)}";
    private string WorldName(ushort id)
    {
        if (id == 0) return T("unknownWorld");
        if (worldNames.TryGetValue(id, out var cachedName)) return cachedName;
        try
        {
            var value = dataManager.GetExcelSheet<WorldSheet>().GetRow(id).Name.ToString();
            return worldNames[id] = string.IsNullOrWhiteSpace(value) ? T("unknownWorld") : value;
        }
        catch { return worldNames[id] = T("unknownWorld"); }
    }
    private void DrawJob(byte id, float size = 22)
    {
        if (id == 0) { ImGui.TextDisabled("?"); return; }
        try
        {
            if (!jobIcons.TryGetValue(id, out var shared))
                jobIcons[id] = shared = textureProvider.GetFromGameIcon(new GameIconLookup((uint)(62100 + id), true));
            var wrap = shared.GetWrapOrDefault();
            if (wrap != null) { ImGui.Image(wrap.Handle, new Vector2(size)); return; }
        }
        catch { }
        ImGui.TextDisabled("?");
    }
    private void DrawAction(uint id, byte actionType, float iconSize = 22)
    {
        _ = actionType; // Capture type is evidence metadata; the Action row remains authoritative for artwork.
        try
        {
            var language = EffectiveLanguage();
            var action = dataManager.GetExcelSheet<ActionSheet>(language == "ja" ? ClientLanguage.Japanese : ClientLanguage.English).GetRow(id);
            switch (ActionIconPresentation.Classify(id, action.Icon))
            {
                case ActionIconPresentation.Kind.Official:
                    if (!actionIcons.TryGetValue(id, out var shared))
                        actionIcons[id] = shared = textureProvider.GetFromGameIcon(new GameIconLookup(action.Icon, true));
                    var wrap = shared.GetWrapOrDefault();
                    if (wrap != null) ImGui.Image(wrap.Handle, new Vector2(iconSize));
                    else DrawActionBadge(iconSize, pending: true);
                    break;
                case ActionIconPresentation.Kind.Mechanism:
                    DrawActionBadge(iconSize, mechanism: true);
                    break;
                default:
                    DrawActionBadge(iconSize);
                    break;
            }
        }
        catch { DrawActionBadge(iconSize); }
        ImGui.SameLine(0,3); ImGui.TextUnformatted(ActionName(id));
    }
    private static void DrawActionBadge(float size, bool mechanism = false, bool pending = false)
    {
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(mechanism
            ? new Vector4(1f, .72f, .24f, .95f)
            : new Vector4(.62f, .65f, .68f, pending ? .48f : .9f));
        var center = origin + new Vector2(size * .5f);
        var radius = size * .34f;
        draw.AddRect(origin + new Vector2(1), origin + new Vector2(size - 1), color, 3, 0, 1.2f);
        if (mechanism)
        {
            var top = center + new Vector2(0, -radius);
            var right = center + new Vector2(radius, 0);
            var bottom = center + new Vector2(0, radius);
            var left = center + new Vector2(-radius, 0);
            draw.AddLine(top, right, color, 1.8f); draw.AddLine(right, bottom, color, 1.8f);
            draw.AddLine(bottom, left, color, 1.8f); draw.AddLine(left, top, color, 1.8f);
            draw.AddCircleFilled(center, Math.Max(1.5f, size * .08f), color);
        }
        else if (!pending)
        {
            var mark = "?";
            var textSize = ImGui.CalcTextSize(mark);
            draw.AddText(center - textSize * .5f, color, mark);
        }
    }
    private string ActionName(uint id) => actionCatalog.Find(id, EffectiveLanguage()) ?? $"{T("action")} #{id}";
    private string StatusName(long id)
    {
        var language = EffectiveLanguage(); var key = (language, id);
        if (statusNames.TryGetValue(key, out var value)) return value;
        value = statusCatalog.Find(id, language) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) && language is not ("zh-CN" or "zh-TW"))
        {
            try
            {
                var sheetLanguage = language == "ja" ? ClientLanguage.Japanese : ClientLanguage.English;
                value = dataManager.GetExcelSheet<StatusSheet>(sheetLanguage).GetRow((uint)id).Name.ToString();
            }
            catch { }
        }
        return statusNames[key] = string.IsNullOrWhiteSpace(value) ? T("unresolvedStatus") : value;
    }
    private string EffectiveLanguage()
    {
        if (configuration.Language != "auto") return configuration.Language;
        var culture = CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
            culture.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
            culture.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase)) return "zh-TW";
        if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (culture.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja";
        return culture.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? "ko" : "en";
    }
    private string T(string key) => I18n.Get(configuration.Language, key);
}
