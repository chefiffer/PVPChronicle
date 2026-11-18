using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using PvpChronicle.RecordingContract;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Converts Recorder's local BGRA frame buffer into a real Dalamud texture. ImGui then owns the
/// final draw, clipping and scrolling, so there is no second native window patched over the page.
/// </summary>
internal sealed class RecordingFrameTexture : IDisposable
{
    private readonly ITextureProvider textureProvider;
    private readonly IPluginLog log;
    private RecordingFrameConsumer? consumer;
    private IDalamudTextureWrap? texture;
    private string? mapName;
    private long sequence;
    private bool firstTextureLogged;
    private DateTimeOffset lastFailureLogUtc;

    public RecordingFrameTexture(ITextureProvider textureProvider, IPluginLog log)
    {
        this.textureProvider = textureProvider;
        this.log = log;
    }

    public ImTextureID Handle => texture?.Handle ?? default;
    public bool Available => texture != null;

    public void Update(RecordingPlaybackState state)
    {
        if (string.IsNullOrWhiteSpace(state.FrameMapName)) return;
        if (!string.Equals(mapName, state.FrameMapName, StringComparison.Ordinal))
        {
            Reset();
            mapName = state.FrameMapName;
            try { consumer = new RecordingFrameConsumer(mapName); }
            catch (Exception ex)
            {
                consumer = null;
                LogFailure(ex, "Could not open Recorder's embedded frame buffer");
            }
        }
        if (consumer == null || state.FrameSequence <= sequence ||
            !consumer.TryRead(sequence, out var frame) || frame == null) return;

        try
        {
            var next = textureProvider.CreateFromRaw(
                RawImageSpecification.Bgra32(frame.Width, frame.Height), frame.Bgra,
                "PVP Chronicle embedded recording frame");
            var previous = texture;
            texture = next;
            sequence = frame.Sequence;
            previous?.Dispose();
            if (!firstTextureLogged)
            {
                firstTextureLogged = true;
                log.Information(
                    "PVP Chronicle created its first in-page recording texture: {Width}x{Height}, sequence={Sequence}",
                    frame.Width, frame.Height, frame.Sequence);
            }
        }
        catch (Exception ex)
        {
            LogFailure(ex, "Could not create the in-page recording texture");
        }
    }

    private void Reset()
    {
        texture?.Dispose();
        texture = null;
        consumer?.Dispose();
        consumer = null;
        mapName = null;
        sequence = 0;
    }

    private void LogFailure(Exception ex, string message)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - lastFailureLogUtc < TimeSpan.FromSeconds(5)) return;
        lastFailureLogUtc = now;
        log.Warning(ex, message);
    }

    public void Dispose() => Reset();
}
