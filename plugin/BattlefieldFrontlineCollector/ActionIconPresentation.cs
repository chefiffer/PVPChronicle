namespace BattlefieldFrontlineCollector;

/// <summary>
/// Separates a real Action-sheet icon from the generic mechanism placeholder.
/// 区分官方技能图标、游戏自身的通用机制图标和真正无法解析的动作。
/// </summary>
internal static class ActionIconPresentation
{
    // Row 405 is Square Enix's generic action/mechanism icon. Treating it as
    // an unknown skill makes correctly captured NPC and arena actions look
    // broken even though the game sheet contains no more specific artwork.
    internal const uint GenericMechanismIconId = 405;

    internal enum Kind
    {
        Official,
        Mechanism,
        Unresolved
    }

    internal static Kind Classify(uint actionId, uint iconId)
    {
        if (actionId == 0 || iconId == 0) return Kind.Unresolved;
        return iconId == GenericMechanismIconId ? Kind.Mechanism : Kind.Official;
    }
}
