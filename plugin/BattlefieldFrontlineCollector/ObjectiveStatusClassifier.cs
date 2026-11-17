namespace BattlefieldFrontlineCollector;

/// <summary>
/// Defines which game-data Status rows may be shown as an objective effect.
/// Beneficial job gauges/readiness buffs are actor-local state and must never
/// be rendered on a Shatter tomelith merely because the action targeted it.
/// </summary>
internal static class ObjectiveStatusClassifier
{
    // Lumina Status.StatusCategory: 1 = beneficial, 2 = detrimental.
    internal const byte DetrimentalCategory = 2;

    internal static bool IsDisplayable(byte statusCategory, uint iconId, string? name) =>
        statusCategory == DetrimentalCategory && iconId != 0 &&
        !string.IsNullOrWhiteSpace(name);
}
