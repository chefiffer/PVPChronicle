using System.Collections.Generic;
using System.Linq;

namespace BattlefieldFrontlineCollector;

internal enum UploadProtocolCompatibility
{
    Compatible,
    LegacyServer,
    ClientUpgradeRequired
}

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
    internal const string ServerUpgradeRequiredCode = "server_upgrade_required";
    internal const string LegacyServerCode = "legacy_server_compatibility";

    internal static UploadProtocolCompatibility Evaluate(
        IReadOnlyList<int>? acceptedProtocols, int minimumProtocol)
    {
        // Servers released before explicit protocol negotiation publish neither
        // field. Let the request proceed so the server can make the final call;
        // never mislabel an internal build as an obsolete public release.
        if ((acceptedProtocols == null || acceptedProtocols.Count == 0) &&
            minimumProtocol <= 0)
            return UploadProtocolCompatibility.LegacyServer;
        return acceptedProtocols?.Contains(Current) == true && minimumProtocol <= Current
            ? UploadProtocolCompatibility.Compatible
            : UploadProtocolCompatibility.ClientUpgradeRequired;
    }

    internal static string ClassifyUpgradeResponse(bool responseAdvertisesProtocol) =>
        responseAdvertisesProtocol ? UpgradeRequiredCode : ServerUpgradeRequiredCode;

    internal static bool ShouldRetryAutomatically(string? compatibilityCode) =>
        compatibilityCode is not UpgradeRequiredCode and not ServerUpgradeRequiredCode;
}
