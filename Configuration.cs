using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace BuddyDresser;

public enum RandomizerMode
{
    FullSet = 0,
    PerSlot = 1
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool AutoOnSummon { get; set; } = false;
    public RandomizerMode Mode { get; set; } = RandomizerMode.FullSet;
    public bool ExcludeCurrent { get; set; } = true;
    public HashSet<uint> ExcludedBardingIds { get; set; } = new();

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi)
    {
        this.pluginInterface = pi;
    }

    public void Save()
    {
        this.pluginInterface?.SavePluginConfig(this);
    }
}
