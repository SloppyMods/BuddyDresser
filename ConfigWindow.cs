using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BuddyDresser;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly BardingManager bardingManager;
    private readonly BardingEquipHelper equipHelper;
    private readonly Action randomize;
    private string filterText = string.Empty;

    public ConfigWindow(Configuration configuration, BardingManager bardingManager, BardingEquipHelper equipHelper, Action randomize)
        : base("Buddy Dresser Configuration###BuddyDresserConfigWindow")
    {
        this.configuration = configuration;
        this.bardingManager = bardingManager;
        this.equipHelper = equipHelper;
        this.randomize = randomize;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 450),
            MaximumSize = new Vector2(800, 1000)
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var autoSummon = this.configuration.AutoOnSummon;
        if (ImGui.Checkbox("Auto-randomize when Chocobo is summoned", ref autoSummon))
        {
            this.configuration.AutoOnSummon = autoSummon;
            this.configuration.Save();
        }

        var excludeCurrent = this.configuration.ExcludeCurrent;
        if (ImGui.Checkbox("Avoid picking currently equipped barding", ref excludeCurrent))
        {
            this.configuration.ExcludeCurrent = excludeCurrent;
            this.configuration.Save();
        }

        ImGui.Separator();

        ImGui.Text("Randomization Mode:");
        var mode = this.configuration.Mode;

        if (ImGui.RadioButton("Full Set (matches Head, Body, Legs)", mode == RandomizerMode.FullSet))
        {
            this.configuration.Mode = RandomizerMode.FullSet;
            this.configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Per-Slot (mixes slots independently)", mode == RandomizerMode.PerSlot))
        {
            this.configuration.Mode = RandomizerMode.PerSlot;
            this.configuration.Save();
        }

        ImGui.Separator();

        // Current status
        var current = this.bardingManager.GetCurrentEquipped();
        ImGui.TextDisabled($"Current Barding IDs: Head={current.Head}, Body={current.Body}, Legs={current.Legs}");

        if (ImGui.Button("Randomize Now", new Vector2(150, 28)))
        {
            this.randomize();
        }

        ImGui.Separator();

        ImGui.Text("Barding Pool (check to include, uncheck to exclude):");
        ImGui.InputTextWithHint("##BardingFilter", "Search bardings...", ref this.filterText, 100);

        ImGui.SameLine();
        if (ImGui.Button("Select All"))
        {
            this.configuration.ExcludedBardingIds.Clear();
            this.configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Deselect All"))
        {
            foreach (var b in this.bardingManager.AllBardings)
            {
                this.configuration.ExcludedBardingIds.Add(b.RowId);
            }
            this.configuration.Save();
        }

        if (ImGui.BeginChild("##BardingListScroll", new Vector2(0, 0), true))
        {
            foreach (var b in this.bardingManager.AllBardings)
            {
                if (!string.IsNullOrWhiteSpace(this.filterText) &&
                    !b.Name.Contains(this.filterText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isUnlocked = this.bardingManager.IsUnlocked(b.RowId);
                bool isIncluded = !this.configuration.ExcludedBardingIds.Contains(b.RowId);

                if (!isUnlocked)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.Checkbox($"##barding_{b.RowId}", ref isIncluded))
                {
                    if (isIncluded)
                    {
                        this.configuration.ExcludedBardingIds.Remove(b.RowId);
                    }
                    else
                    {
                        this.configuration.ExcludedBardingIds.Add(b.RowId);
                    }
                    this.configuration.Save();
                }

                ImGui.SameLine();
                if (isUnlocked)
                {
                    ImGui.Text($"[{b.RowId}] {b.Name}");
                }
                else
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), $"[{b.RowId}] {b.Name} (Locked)");
                    ImGui.EndDisabled();
                }
            }

            ImGui.EndChild();
        }
    }
}
