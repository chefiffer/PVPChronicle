using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace BattlefieldFrontlineCollector;

/// <summary>
/// Decodes the client EffectResult packet without borrowing the packet memory.
///
/// The packet contains up to sixteen independent target entries.  Each entry
/// owns its own entity ID and up to four status changes.  Treating the first
/// function argument as the target for every status incorrectly assigns player
/// self-buffs to an attacked objective, especially Shatter ice.
/// </summary>
internal static class EffectResultPacketReader
{
    internal const int PacketSize = 0x584;
    private const int EntryOffset = 0x04;
    private const int EntrySize = 0x58;
    private const int MaximumEntries = 16;
    private const int EffectOffset = 0x18;
    private const int EffectSize = 0x10;
    private const int MaximumEffects = 4;

    internal readonly record struct StatusResult(uint TargetEntityId, uint SourceEntityId,
        ushort StatusId, ushort Parameter, float RemainingSeconds);

    internal static IReadOnlyList<StatusResult> Decode(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < PacketSize) return [];
        var rows = new List<StatusResult>();
        var entryCount = Math.Min((int)packet[0], MaximumEntries);
        for (var entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            var entry = EntryOffset + entryIndex * EntrySize;
            var targetEntityId = BinaryPrimitives.ReadUInt32LittleEndian(
                packet.Slice(entry + 0x04, sizeof(uint)));
            if (targetEntityId == 0) continue;
            var effectCount = Math.Min((int)packet[entry + 0x15], MaximumEffects);
            for (var effectIndex = 0; effectIndex < effectCount; effectIndex++)
            {
                var effect = entry + EffectOffset + effectIndex * EffectSize;
                var statusId = BinaryPrimitives.ReadUInt16LittleEndian(
                    packet.Slice(effect + 0x02, sizeof(ushort)));
                if (statusId == 0) continue;
                var parameter = BinaryPrimitives.ReadUInt16LittleEndian(
                    packet.Slice(effect + 0x04, sizeof(ushort)));
                var remainingBits = BinaryPrimitives.ReadInt32LittleEndian(
                    packet.Slice(effect + 0x08, sizeof(int)));
                var sourceEntityId = BinaryPrimitives.ReadUInt32LittleEndian(
                    packet.Slice(effect + 0x0C, sizeof(uint)));
                rows.Add(new StatusResult(targetEntityId, sourceEntityId, statusId,
                    parameter, BitConverter.Int32BitsToSingle(remainingBits)));
            }
        }
        return rows;
    }
}
