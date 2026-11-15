using System;
using System.Linq;
using System.Text;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BuddyDresser;

/// <summary>
/// Debug capture: logs every addon callback and every Buddy agent event while enabled.
/// Used to learn the exact payload the game sends when barding is changed by hand.
/// </summary>
public unsafe class CaptureService : IDisposable
{
    private readonly IPluginLog log;
    private readonly Hook<AtkUnitBase.Delegates.FireCallback> fireCallbackHook;
    private Hook<AgentInterface.Delegates.ReceiveEvent>? agentHook;
    private AgentInterface* buddyAgent;

    public bool Enabled { get; private set; }

    public CaptureService(IGameInteropProvider interop, IPluginLog log)
    {
        this.log = log;
        this.fireCallbackHook = interop.HookFromAddress<AtkUnitBase.Delegates.FireCallback>(
            AtkUnitBase.MemberFunctionPointers.FireCallback, this.FireCallbackDetour);

        this.buddyAgent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Buddy);
        if (this.buddyAgent != null)
        {
            this.agentHook = interop.HookFromAddress<AgentInterface.Delegates.ReceiveEvent>(
                (nint)this.buddyAgent->VirtualTable->ReceiveEvent, this.AgentReceiveEventDetour);
        }
        else
        {
            this.log.Warning("[Capture] Buddy agent not found; agent events will not be logged.");
        }
    }

    public void SetEnabled(bool on)
    {
        this.Enabled = on;
        if (on)
        {
            this.fireCallbackHook.Enable();
            this.agentHook?.Enable();
        }
        else
        {
            this.fireCallbackHook.Disable();
            this.agentHook?.Disable();
        }
        this.log.Information($"[Capture] {(on ? "ON" : "OFF")}");
    }

    private bool FireCallbackDetour(AtkUnitBase* addon, uint valueCount, AtkValue* values, bool close)
    {
        try
        {
            var name = addon == null ? "?" : addon->NameString;
            this.log.Information($"[Capture] FireCallback addon={name} count={valueCount} close={close} values=[{Decode(values, valueCount)}]");
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "[Capture] FireCallback decode failed");
        }
        return this.fireCallbackHook.Original(addon, valueCount, values, close);
    }

    private AtkValue* AgentReceiveEventDetour(AgentInterface* agent, AtkValue* returnValue, AtkValue* values, uint valueCount, ulong eventKind)
    {
        try
        {
            if (agent == this.buddyAgent)
                this.log.Information($"[Capture] BuddyAgent.ReceiveEvent kind={eventKind} count={valueCount} values=[{Decode(values, valueCount)}]");
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "[Capture] agent decode failed");
        }
        return this.agentHook!.Original(agent, returnValue, values, valueCount, eventKind);
    }

    private static string Decode(AtkValue* values, uint count)
    {
        if (values == null) return "";
        var sb = new StringBuilder();
        for (var i = 0; i < Math.Min(count, 32u); i++)
        {
            var v = values[i];
            sb.Append(v.Type switch
            {
                AtkValueType.Int => $"Int:{v.Int}",
                AtkValueType.UInt => $"UInt:{v.UInt}",
                AtkValueType.Bool => $"Bool:{v.Bool}",
                _ => $"{v.Type}:0x{v.UInt64:X}",
            });
            if (i < count - 1) sb.Append(", ");
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        this.fireCallbackHook.Dispose();
        this.agentHook?.Dispose();
    }
}
