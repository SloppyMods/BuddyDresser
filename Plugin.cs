using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace BuddyDresser;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Buddy Dresser";

    private const string CommandName1 = "/buddydresser";
    private const string CommandName2 = "/barding";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Interop { get; private set; } = null!;

    public Configuration Configuration { get; }
    public WindowSystem WindowSystem { get; } = new("BuddyDresser");
    public BardingManager BardingManager { get; }
    public BardingEquipHelper EquipHelper { get; }
    public ConfigWindow ConfigWindow { get; }
    public CaptureService Capture { get; }

    private float lastTimeLeft = 0f;

    public Plugin()
    {
        this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.Initialize(PluginInterface);

        this.BardingManager = new BardingManager(DataManager, PluginLog, this.Configuration);
        this.EquipHelper = new BardingEquipHelper(PluginLog);
        this.Capture = new CaptureService(Interop, PluginLog);
        this.ConfigWindow = new ConfigWindow(this.Configuration, this.BardingManager, this.EquipHelper, this.RandomizeBarding);

        this.WindowSystem.AddWindow(this.ConfigWindow);

        PluginInterface.UiBuilder.Draw += this.DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += this.OpenConfigUI;

        CommandManager.AddHandler(CommandName1, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open configuration or use '/buddydresser random' to randomize barding."
        });

        CommandManager.AddHandler(CommandName2, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open configuration or use '/barding random' to randomize barding."
        });

        Framework.Update += this.OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= this.OnFrameworkUpdate;

        CommandManager.RemoveHandler(CommandName1);
        CommandManager.RemoveHandler(CommandName2);

        PluginInterface.UiBuilder.Draw -= this.DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= this.OpenConfigUI;

        this.WindowSystem.RemoveAllWindows();
        this.ConfigWindow.Dispose();
        this.EquipHelper.Dispose();
        this.Capture.Dispose();
    }

    private void DrawUI()
    {
        this.WindowSystem.Draw();
    }

    public void OpenConfigUI()
    {
        this.ConfigWindow.IsOpen = true;
    }

    private void OnCommand(string command, string args)
    {
        var cleanArgs = args.Trim().ToLowerInvariant();

        if (cleanArgs is "random" or "r")
        {
            this.RandomizeBarding();
        }
        else if (cleanArgs == "capture")
        {
            this.Capture.SetEnabled(!this.Capture.Enabled);
            ChatGui.Print(this.Capture.Enabled
                ? "[Buddy Dresser] Capture ON. Change one barding piece by hand, then run /barding capture again."
                : "[Buddy Dresser] Capture OFF.");
        }
        else
        {
            this.OpenConfigUI();
        }
    }

    public void RandomizeBarding()
    {
        try
        {
            PluginLog.Information("RandomizeBarding called.");
            var pick = this.BardingManager.PickRandom();
            if (pick == null)
            {
                PluginLog.Warning("PickRandom returned null.");
                ChatGui.Print("[Buddy Dresser] No unlocked bardings available to equip.");
                return;
            }

            string N(uint id) => this.BardingManager.AllBardings.FirstOrDefault(b => b.RowId == id)?.Name ?? $"#{id}";
            PluginLog.Information($"Picked: Head={pick.Value.Head}, Body={pick.Value.Body}, Legs={pick.Value.Legs}");
            ChatGui.Print($"[Buddy Dresser] Applying: Head {N(pick.Value.Head)}, Body {N(pick.Value.Body)}, Legs {N(pick.Value.Legs)}");
            if (!this.EquipHelper.ApplyBarding(pick.Value.Head, pick.Value.Body, pick.Value.Legs))
                ChatGui.Print("[Buddy Dresser] Apply failed. See Dalamud log for details.");
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "Exception in RandomizeBarding.");
        }
    }

    private unsafe void OnFrameworkUpdate(IFramework _)
    {
        try
        {
            if (!ClientState.IsLoggedIn)
            {
                this.lastTimeLeft = 0f;
                return;
            }

            var uiState = UIState.Instance();
            if (uiState == null)
                return;

            float currentTimeLeft = uiState->Buddy.CompanionInfo.TimeLeft;

            // Detect transition from 0 (not summoned) to positive (just summoned)
            if (this.lastTimeLeft <= 0f && currentTimeLeft > 0f)
            {
                if (this.Configuration.AutoOnSummon)
                {
                    PluginLog.Information($"Chocobo companion summoned (TimeLeft={currentTimeLeft}). Triggering auto-randomize.");
                    this.RandomizeBarding();
                }
            }

            this.lastTimeLeft = currentTimeLeft;
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "Exception in OnFrameworkUpdate.");
        }
    }
}
