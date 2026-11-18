using System;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using PvpChronicle.RecordingContract;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Narrow seam between Chronicle and an optional recording plugin.  Capture remains functional
/// when no subscriber exists, and malformed external messages are rejected without touching data.
/// </summary>
internal sealed class RecordingIpcBridge : IDisposable
{
    private readonly ICallGateProvider<string, bool> events;
    private readonly ICallGateProvider<string, bool> attachments;
    private readonly ICallGateProvider<string> info;
    private readonly ICallGateSubscriber<string, long, string> openAt;
    private readonly ICallGateSubscriber<string, string> playbackControl;
    private readonly RecordingLinkStore store;
    private readonly IPluginLog log;

    public RecordingIpcBridge(IDalamudPluginInterface pluginInterface, RecordingLinkStore store, IPluginLog log)
    {
        this.store = store;
        this.log = log;
        events = pluginInterface.GetIpcProvider<string, bool>(RecordingContract.MatchEventChannel);
        attachments = pluginInterface.GetIpcProvider<string, bool>(RecordingContract.AttachRecordingChannel);
        info = pluginInterface.GetIpcProvider<string>(RecordingContract.ChronicleInfoChannel);
        openAt = pluginInterface.GetIpcSubscriber<string, long, string>(RecordingContract.OpenAtChannel);
        playbackControl = pluginInterface.GetIpcSubscriber<string, string>(
            RecordingContract.PlaybackControlChannel);
        attachments.RegisterFunc(AttachJson);
        info.RegisterFunc(() => JsonSerializer.Serialize(new
        {
            schema = RecordingContract.Schema,
            plugin = "BattlefieldFrontlineCollector",
            available = true
        }, RecordingContract.Json));
    }

    public void Publish(MatchRecordingEvent value)
    {
        try { events.SendMessage(JsonSerializer.Serialize(value, RecordingContract.Json)); }
        catch (Exception ex) { log.Debug(ex, "Optional recording IPC event was not delivered"); }
    }

    public string OpenAt(string matchUid, long elapsedMs)
    {
        if (store.Find(matchUid) == null) return "recording_not_attached";
        try { return openAt.InvokeFunc(matchUid, Math.Max(0, elapsedMs)); }
        catch (Exception ex)
        {
            log.Debug(ex, "Optional recording plugin is unavailable");
            return "recorder_unavailable";
        }
    }

    public RecordingAttachment? Find(string matchUid) => store.Find(matchUid);

    /// <summary>
    /// Sends one playback intent across the optional recorder seam.  Missing or reloading Recorder
    /// is represented as state, so the replay window itself never fails because video is optional.
    /// </summary>
    public RecordingPlaybackState Control(RecordingPlaybackRequest request)
    {
        try
        {
            var json = playbackControl.InvokeFunc(JsonSerializer.Serialize(request, RecordingContract.Json));
            return JsonSerializer.Deserialize<RecordingPlaybackState>(json, RecordingContract.Json) ??
                   Unavailable(request.MatchUid, "invalid_recorder_response");
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Optional recording playback interface is unavailable");
            return Unavailable(request.MatchUid, "recorder_unavailable");
        }
    }

    private static RecordingPlaybackState Unavailable(string matchUid, string status) =>
        new(RecordingContract.Schema, matchUid, false, false, false, false,
            0, 0, 0, 1, status);

    private bool AttachJson(string json)
    {
        try
        {
            var value = JsonSerializer.Deserialize<RecordingAttachment>(json, RecordingContract.Json);
            return value != null && store.Attach(value);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Rejected malformed recording attachment IPC message");
            return false;
        }
    }

    public void Dispose()
    {
        attachments.UnregisterFunc();
        info.UnregisterFunc();
    }
}
