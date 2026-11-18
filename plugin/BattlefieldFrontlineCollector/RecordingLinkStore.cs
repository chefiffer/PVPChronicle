using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PvpChronicle.RecordingContract;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Local-only index from a PVP Chronicle match to its recording.  Video files never enter a
/// capture package or an upload request; this index only lets the replay window find them.
/// </summary>
internal sealed class RecordingLinkStore
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mkv", ".mov", ".webm", ".flv", ".avi" };
    private readonly object gate = new();
    private readonly string path;
    private Dictionary<string, RecordingAttachment> items = new(StringComparer.OrdinalIgnoreCase);

    public RecordingLinkStore(string path)
    {
        this.path = path;
        Load();
    }

    public bool Attach(RecordingAttachment attachment)
    {
        if (attachment.Schema != RecordingContract.Schema ||
            string.IsNullOrWhiteSpace(attachment.MatchUid) ||
            string.IsNullOrWhiteSpace(attachment.VideoPath)) return false;
        string fullPath;
        try { fullPath = Path.GetFullPath(attachment.VideoPath); }
        catch { return false; }
        if (!VideoExtensions.Contains(Path.GetExtension(fullPath))) return false;
        lock (gate)
        {
            items[attachment.MatchUid] = attachment with { VideoPath = fullPath };
            SaveLocked();
        }
        return true;
    }

    public RecordingAttachment? Find(string matchUid)
    {
        lock (gate) return items.TryGetValue(matchUid, out var value) ? value : null;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(path)) return;
            var loaded = JsonSerializer.Deserialize<List<RecordingAttachment>>(
                File.ReadAllText(path), RecordingContract.Json) ?? [];
            items = loaded.Where(x => x.Schema == RecordingContract.Schema &&
                                      !string.IsNullOrWhiteSpace(x.MatchUid))
                .GroupBy(x => x.MatchUid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // A damaged optional index must never stop match capture or local replay.
            items = new Dictionary<string, RecordingAttachment>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveLocked()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(items.Values.ToArray(), RecordingContract.Json));
        File.Move(temp, path, true);
    }
}
