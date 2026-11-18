using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Immediate-mode adapter for the pure Frontline identity contract.
/// 只负责 ImGui 绘制，排序和标签语义仍由可独立测试的契约模块决定。
/// </summary>
internal static class FrontlinePlayerIdentityRenderer
{
    public static void DrawSquadBadge(string id, byte team, byte alliance, string tooltip)
    {
        var label = FrontlinePlayerIdentityContract.SquadLabel(alliance);
        var at = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton($"{label}##frontline-squad-{id}",
            new Vector2(15, ImGui.GetTextLineHeight()));
        var packed = ImGui.ColorConvertFloat4ToU32(
            FrontlinePlayerIdentityContract.TeamColor(team));
        var draw = ImGui.GetWindowDrawList();
        draw.AddText(at, packed, label);
        draw.AddText(at + new Vector2(.65f, 0), packed, label);
        if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(tooltip))
            ImGui.SetTooltip(tooltip);
    }
}
