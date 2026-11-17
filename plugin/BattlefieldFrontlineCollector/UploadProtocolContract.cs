using System.Collections.Generic;
using System.Linq;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Defines the upload envelope independently from public and internal plugin
/// release numbers. A rejected upload remains recoverable from the local
/// capture file after the plugin is updated.
/// </summary>
internal static class UploadProtocolContract
{
    internal const int Current = 1;
    internal const string HeaderName = "X-PvPChronicle-Upload-Protocol";
    internal const string UpgradeRequiredCode = "client_upgrade_required";

    internal static bool ServerAccepts(
        IReadOnlyList<int>? acceptedProtocols, int minimumProtocol) =>
        acceptedProtocols?.Contains(Current) == true && minimumProtocol <= Current;

    internal static bool ShouldRetryAutomatically(string? compatibilityCode) =>
        compatibilityCode != UpgradeRequiredCode;
}
