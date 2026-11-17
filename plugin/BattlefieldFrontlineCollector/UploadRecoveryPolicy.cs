using System;
using System.Net;
using System.Text.Json;

namespace BattlefieldFrontlineCollector;

internal static class UploadRecoveryPolicy
{
    internal static string? CredentialErrorCode(string? message)
    {
        if (string.Equals(message, "missing_token", StringComparison.Ordinal))
            return "missing_token";
        if (message?.Contains("credential_expired", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("credential expired", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("token expired", StringComparison.OrdinalIgnoreCase) == true)
            return "credential_expired";
        if (message?.Contains("401", StringComparison.OrdinalIgnoreCase) == true ||
            message?.Contains("invalid token", StringComparison.OrdinalIgnoreCase) == true)
            return "invalid_token";
        return null;
    }

    internal static bool ShouldAutoFetchDetail(
        string? uploadState, bool hasDetail, bool autoFetchPending) =>
        autoFetchPending && !hasDetail &&
        uploadState is "ready" or "complete";

    internal static bool DetailAccessMustBeReestablished(HttpStatusCode statusCode, string body)
    {
        if (statusCode != HttpStatusCode.NotFound || string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False &&
                   root.TryGetProperty("error", out var error) &&
                   string.Equals(error.GetString(), "not found", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // A proxy-generated HTML/plain 404 is a routing failure, not an ownership
            // signal, and must never trigger a potentially expensive re-upload.
            return false;
        }
    }
}
