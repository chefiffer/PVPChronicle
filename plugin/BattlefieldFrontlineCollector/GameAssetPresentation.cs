namespace BattlefieldFrontlineCollector;

/// <summary>
/// Keeps game-owned texture addressing separate from packaged fallback assets.
/// 游戏原生纹理始终优先；插件资源只在验证期承担回退作用。
/// </summary>
internal static class GameAssetPresentation
{
    internal static string MapTexturePath(string mapId)
    {
        var id = (mapId ?? string.Empty).Trim();
        if (id.Length == 0) return string.Empty;
        return $"ui/map/{id}/{id.Replace("/", string.Empty)}_m.tex";
    }

    internal static uint MapTextureCacheKey(uint territoryId) =>
        0x8000_0000u | territoryId;
}
