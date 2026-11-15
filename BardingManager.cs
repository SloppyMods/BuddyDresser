using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace BuddyDresser;

public class BardingManager
{
    private readonly IDataManager dataManager;
    private readonly IPluginLog pluginLog;
    private readonly Configuration configuration;
    private readonly List<BardingItem> bardingList = new();
    private readonly Random random = new();

    public IReadOnlyList<BardingItem> AllBardings => this.bardingList;

    public BardingManager(IDataManager dataManager, IPluginLog pluginLog, Configuration configuration)
    {
        this.dataManager = dataManager;
        this.pluginLog = pluginLog;
        this.configuration = configuration;
        this.LoadSheet();
    }

    private void LoadSheet()
    {
        try
        {
            var sheet = this.dataManager.GetExcelSheet<BuddyEquip>();
            if (sheet == null)
            {
                this.pluginLog.Error("Could not load BuddyEquip excel sheet.");
                return;
            }

            foreach (var row in sheet)
            {
                var name = row.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                this.bardingList.Add(new BardingItem(
                    row.RowId,
                    name,
                    row.ModelTop,
                    row.ModelBody,
                    row.ModelLegs,
                    row.IconHead,
                    row.IconBody,
                    row.IconLegs
                ));
            }

            this.pluginLog.Information($"Loaded {this.bardingList.Count} barding entries from game data.");
        }
        catch (Exception ex)
        {
            this.pluginLog.Error(ex, "Failed to load BuddyEquip sheet.");
        }
    }

    public unsafe bool IsUnlocked(uint bardingRowId)
    {
        // Row 0 (default saddle) is always unlocked
        if (bardingRowId == 0)
            return true;

        try
        {
            var uiState = UIState.Instance();
            if (uiState == null)
                return false;

            var bitmask = uiState->Buddy.CompanionInfo.BuddyEquipUnlockBitmask;
            int byteIndex = (int)(bardingRowId >> 3);
            if (byteIndex < 0 || byteIndex >= bitmask.Length)
                return false;

            int bit = (int)(bardingRowId & 7);
            return (bitmask[byteIndex] & (1 << bit)) != 0;
        }
        catch (Exception ex)
        {
            this.pluginLog.Error(ex, $"Error checking unlock status for barding {bardingRowId}");
            return false;
        }
    }

    public unsafe (byte Head, byte Body, byte Legs) GetCurrentEquipped()
    {
        try
        {
            var uiState = UIState.Instance();
            if (uiState == null)
                return (0, 0, 0);

            var equipped = uiState->Buddy.CompanionInfo.BuddyEquipRowIds;
            return (equipped[0], equipped[1], equipped[2]);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    public List<BardingItem> GetCandidateList(bool ignoreUserExclusions = false)
    {
        var candidates = new List<BardingItem>();
        foreach (var b in this.bardingList)
        {
            if (!this.IsUnlocked(b.RowId))
                continue;

            if (!ignoreUserExclusions && this.configuration.ExcludedBardingIds.Contains(b.RowId))
                continue;

            candidates.Add(b);
        }

        return candidates;
    }

    public (uint Head, uint Body, uint Legs)? PickRandom()
    {
        var candidates = this.GetCandidateList(false);
        this.pluginLog.Information($"GetCandidateList(false) returned {candidates.Count} candidates.");

        if (candidates.Count == 0)
        {
            // If all are excluded by user, try with all unlocked
            candidates = this.GetCandidateList(true);
            this.pluginLog.Information($"GetCandidateList(true) fallback returned {candidates.Count} candidates.");
            if (candidates.Count == 0)
                return null;
        }

        var current = this.GetCurrentEquipped();
        this.pluginLog.Information($"Current equipped: Head={current.Head}, Body={current.Body}, Legs={current.Legs}. Mode={this.configuration.Mode}");

        if (this.configuration.Mode == RandomizerMode.FullSet)
        {
            var pool = candidates;
            if (this.configuration.ExcludeCurrent && candidates.Count > 1)
            {
                var filtered = candidates.Where(b => b.RowId != current.Head || b.RowId != current.Body || b.RowId != current.Legs).ToList();
                if (filtered.Count > 0)
                    pool = filtered;
            }

            var pick = pool[this.random.Next(pool.Count)];
            return (pick.RowId, pick.RowId, pick.RowId);
        }
        else
        {
            // Per-Slot Mode
            var headPool = candidates.Where(b => b.HasHeadPiece).ToList();
            var bodyPool = candidates.Where(b => b.HasBodyPiece).ToList();
            var legsPool = candidates.Where(b => b.HasLegsPiece).ToList();

            if (this.configuration.ExcludeCurrent)
            {
                var fHead = headPool.Where(b => b.RowId != current.Head).ToList();
                if (fHead.Count > 0) headPool = fHead;

                var fBody = bodyPool.Where(b => b.RowId != current.Body).ToList();
                if (fBody.Count > 0) bodyPool = fBody;

                var fLegs = legsPool.Where(b => b.RowId != current.Legs).ToList();
                if (fLegs.Count > 0) legsPool = fLegs;
            }

            var headPick = headPool.Count > 0 ? headPool[this.random.Next(headPool.Count)].RowId : current.Head;
            var bodyPick = bodyPool.Count > 0 ? bodyPool[this.random.Next(bodyPool.Count)].RowId : current.Body;
            var legsPick = legsPool.Count > 0 ? legsPool[this.random.Next(legsPool.Count)].RowId : current.Legs;

            return (headPick, bodyPick, legsPick);
        }
    }
}
