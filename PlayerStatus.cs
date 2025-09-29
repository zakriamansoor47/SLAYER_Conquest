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
    Dictionary<CCSPlayerController, PlayerStatus> PlayerStatuses = new Dictionary<CCSPlayerController, PlayerStatus>();
    public enum PlayerStatusType
    {
        Alive,
        Combat,
        Injured,
        Dead
    }
    public class PlayerStatus
    {
        public string DefaultName { get; set; } = "";
        public bool PlayerPressedKey { get; set; } = false;
        public bool IsSprinting { get; set; } = false;
        public int TotalRevives { get; set; } = 0;
        public int TotalKills { get; set; } = 0;
        public int TotalDeaths { get; set; } = 0;
        public int TotalAssists { get; set; } = 0;
        public string LastKilledWith { get; set; } = "";
        public PlayerStatusType Status { get; set; } = PlayerStatusType.Alive;
        public FlagStatus? CapturingFlag { get; set; } = null;
        public PlayerClassType ClassType { get; set; } = PlayerClassType.Medic;
        public PlayerSquad? Squad { get; set; } = null;
        public PlayerSelectedWeapons SelectedWeapons { get; set; } = new PlayerSelectedWeapons();
        public List<PlayerSpecificItems> PlayerItems = new List<PlayerSpecificItems>();
        public float LastCombatTime { get; set; } = Server.CurrentTime;
        public PlayerStatus()
        {
        }
    }
}