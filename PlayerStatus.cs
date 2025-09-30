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
        public int TotalPoints { get; set; } = 0;
        public string LastKilledWith { get; set; } = "";
        public PlayerStatusType Status { get; set; } = PlayerStatusType.Alive;
        public FlagStatus? CapturingFlag { get; set; } = null;
        public PlayerClassType ClassType { get; set; } = PlayerClassType.Assault;
        public PlayerSquad? Squad { get; set; } = null;
        public PlayerSelectedWeapons SelectedWeapons { get; set; } = new PlayerSelectedWeapons();
        public List<PlayerSpecificItems> PlayerItems = new List<PlayerSpecificItems>();
        public float LastCombatTime { get; set; } = 0;
        public float LastStuckTime { get; set; } = 0;
        public float LastFlagCaptureTime { get; set; } = 0;
        public bool CaptureCooldown => (Server.CurrentTime - LastFlagCaptureTime) < 1f;
        public PlayerStatus() { }
    }
    public void GivePlayerPoints(CCSPlayerController player, int points)
    {
        if (player == null || !player.IsValid) return;
        if (!PlayerStatuses.ContainsKey(player)) return;
        PlayerStatuses[player].TotalPoints += points;
        player.InGameMoneyServices!.Account += points; // Give money (points) to player. We using money as points
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
    }
    public void TakePlayerPoints(CCSPlayerController player, int points)
    {
        if (player == null || !player.IsValid) return;
        if (!PlayerStatuses.ContainsKey(player)) return;
        PlayerStatuses[player].TotalPoints -= points;
        if (PlayerStatuses[player].TotalPoints < 0) PlayerStatuses[player].TotalPoints = 0; // Prevent negative points
        player.InGameMoneyServices!.Account -= points; // Take money (points) from player. We using money as points
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
    }
    public int GetPlayerPoints(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return 0;
        if (!PlayerStatuses.ContainsKey(player)) return 0;
        return PlayerStatuses[player].TotalPoints;
    }
    public void ResetPlayerPoints(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return;
        if (!PlayerStatuses.ContainsKey(player)) return;
        PlayerStatuses[player].TotalPoints = 0;
        player.InGameMoneyServices!.Account = 0; // Give money (points) to player. We using money as points
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
    }
}