using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Drawing;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619

namespace SLAYER_CaptureTheFlag;

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    public enum SpecialItemsMaxCount
    {
        Claymore = 1,
        Medkit = 1,
        AmmoBox = 1,
        MedicPouch = -1,
        AmmoPouch = -1,
        ReconRadio = 1
    }
    public class PlayerSpecificItems
    {
        public string ItemName { get; set; } = "None"; // Default item is None
        public int ItemUseCount { get; set; } = 1; // Number of times the item can be used
        public float ItemUseCooldown { get; set; } = 10f; // Cooldown in seconds
        public float LastItemUseTime { get; set; } = Server.CurrentTime; // Last time the item was used
        public float ItemRegenerateTime { get; set; } = 1f; // Time it takes for the item to regenerate (-1 means no regeneration | 0 means instant regeneration)
        public bool IsOnCooldown => (Server.CurrentTime - LastItemUseTime) < ItemUseCooldown; // Check if item is on cooldown

    }
}