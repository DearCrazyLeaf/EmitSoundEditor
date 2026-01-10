using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace EmitSoundEditor.Utils;

internal sealed class PlayerPawnCache
{
    private readonly Dictionary<int, CCSPlayerController> _playerByPawnIndex = new();
    private readonly Dictionary<int, int> _pawnIndexBySlot = new();

    internal void Clear()
    {
        _playerByPawnIndex.Clear();
        _pawnIndexBySlot.Clear();
    }

    internal void Update(CCSPlayerController player)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid)
        {
            return;
        }

        var pawnIndex = (int)pawn.Index;
        if (_pawnIndexBySlot.TryGetValue(player.Slot, out var oldIndex) && oldIndex != pawnIndex)
        {
            _playerByPawnIndex.Remove(oldIndex);
        }

        _pawnIndexBySlot[player.Slot] = pawnIndex;
        _playerByPawnIndex[pawnIndex] = player;
    }

    internal void Remove(CCSPlayerController player)
    {
        if (player == null)
        {
            return;
        }

        if (_pawnIndexBySlot.TryGetValue(player.Slot, out var pawnIndex))
        {
            _pawnIndexBySlot.Remove(player.Slot);
            _playerByPawnIndex.Remove(pawnIndex);
        }
    }

    internal CCSPlayerController? Find(int pawnIndex)
    {
        if (pawnIndex <= 0)
        {
            return null;
        }

        if (_playerByPawnIndex.TryGetValue(pawnIndex, out var cached) &&
            cached.IsValid && cached.PlayerPawn?.Value?.Index == pawnIndex)
        {
            return cached;
        }

        foreach (var candidate in Utilities.GetPlayers())
        {
            if (!candidate.IsValid)
            {
                continue;
            }

            var pawn = candidate.PlayerPawn?.Value;
            if (candidate.Index == pawnIndex || (pawn != null && pawn.IsValid && pawn.Index == pawnIndex))
            {
                Update(candidate);
                return candidate;
            }
        }

        return null;
    }
}
