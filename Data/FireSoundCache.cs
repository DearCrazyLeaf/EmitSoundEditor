using System;

namespace EmitSoundEditor.Data;

internal sealed class FireSoundCache
{
    private readonly FireSoundEntry?[] _entries;

    internal FireSoundCache(int size)
    {
        _entries = new FireSoundEntry?[size];
    }

    internal void ClearAll()
    {
        Array.Clear(_entries, 0, _entries.Length);
    }

    internal FireSoundEntry? Get(int slot)
    {
        if (!IsValidSlot(slot))
        {
            return null;
        }

        return _entries[slot];
    }

    internal void Set(int slot, FireSoundEntry entry)
    {
        if (!IsValidSlot(slot))
        {
            return;
        }

        _entries[slot] = entry;
    }

    internal void Update(int slot, int itemDefIndex, string? customEvent, string? officialEvent, long updatedAtMs)
    {
        if (!IsValidSlot(slot))
        {
            return;
        }

        _entries[slot] = new FireSoundEntry(
            itemDefIndex,
            string.IsNullOrWhiteSpace(customEvent) ? null : customEvent,
            string.IsNullOrWhiteSpace(officialEvent) ? null : officialEvent,
            updatedAtMs);
    }

    internal void Clear(int slot)
    {
        if (!IsValidSlot(slot))
        {
            return;
        }

        _entries[slot] = null;
    }

    private bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot < _entries.Length;
    }
}

internal sealed class FireSoundEntry
{
    public int ItemDefIndex { get; }
    public string? CustomEvent { get; }
    public string? OfficialEvent { get; }
    public long UpdatedAtMs { get; }

    public FireSoundEntry(int itemDefIndex, string? customEvent, string? officialEvent, long updatedAtMs)
    {
        ItemDefIndex = itemDefIndex;
        CustomEvent = customEvent;
        OfficialEvent = officialEvent;
        UpdatedAtMs = updatedAtMs;
    }
}
