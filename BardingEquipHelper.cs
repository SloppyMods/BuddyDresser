using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BuddyDresser;

/// <summary>
/// Applies barding by sending the same Buddy agent event the game sends.
/// Captured from manual change: kind=0, values=[20, slot, bardingId].
/// Slot: 0=head, 1=body, 2=legs.
/// </summary>
public class BardingEquipHelper : IDisposable
{
    private readonly IPluginLog log;

    public BardingEquipHelper(IPluginLog log)
    {
        this.log = log;
    }

    public void Dispose()
    {
    }

    public unsafe bool ApplyBarding(uint head, uint body, uint legs)
    {
        try
        {
            var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Buddy);
            if (agent == null)
            {
                this.log.Error("Buddy agent not found; cannot apply barding.");
                return false;
            }

            this.SendEquip(agent, 0, (int)head);
            this.SendEquip(agent, 1, (int)body);
            this.SendEquip(agent, 2, (int)legs);
            return true;
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Failed to apply barding.");
            return false;
        }
    }

    private unsafe void SendEquip(AgentInterface* agent, int slot, int bardingId)
    {
        Span<AtkValue> values = stackalloc AtkValue[3];
        values[0].Type = AtkValueType.Int;
        values[0].Int = 20;
        values[1].Type = AtkValueType.Int;
        values[1].Int = slot;
        values[2].Type = AtkValueType.Int;
        values[2].Int = bardingId;

        AtkValue ret = default;
        fixed (AtkValue* ptr = values)
        {
            agent->ReceiveEvent(&ret, ptr, 3, 0);
        }

        this.log.Information($"Sent equip event: slot={slot} barding={bardingId}");
    }
}
