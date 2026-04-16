using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
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
        public int TotalCallInPoints { get; set; } = 0;
        public int TotalDamageDealt { get; set; } = 0;
        public string LastKilledWith { get; set; } = "";
        public PlayerStatusType Status { get; set; } = PlayerStatusType.Alive;
        public FlagStatus? CapturingFlag { get; set; } = null;
        public PlayerClassType ClassType { get; set; } = PlayerClassType.Assault;
        public PlayerSquad? Squad { get; set; } = null;
        public PlayerSelectedWeapons SelectedWeapons { get; set; } = new PlayerSelectedWeapons();
        public List<PlayerSpecificItems> PlayerItems = new List<PlayerSpecificItems>();
        public Dictionary<string, (int, float)> CallInAttacksUsage = new Dictionary<string, (int, float)>();
        public CPhysicsPropMultiplayer? PlayerCallInAttackCamera { get; set; } = null;
        public Vector CallInAttackPosition { get; set; } = Vector.Zero;
        public List<CBeam> CallInAttackBeams = new List<CBeam>();
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
        GivePlayerCallInPoints(player, points);
        var squad = PlayerStatuses[player].Squad; // Also add points to squad
        if (squad != null)
        {
            squad.TotalPoints += points;
        }
    }
    public void GivePlayerCallInPoints(CCSPlayerController player, int points)
    {
        if (player == null || !player.IsValid) return;
        if (!PlayerStatuses.ContainsKey(player)) return;
        PlayerStatuses[player].TotalCallInPoints += points;
        player.InGameMoneyServices!.Account += points; // Give money (points) to player. We using money as call in points
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
    }
    public void TakePlayerCallInPoints(CCSPlayerController player, int points)
    {
        if (player == null || !player.IsValid) return;
        if (!PlayerStatuses.ContainsKey(player)) return;
        PlayerStatuses[player].TotalCallInPoints -= points;
        if (PlayerStatuses[player].TotalCallInPoints < 0) PlayerStatuses[player].TotalCallInPoints = 0; // Prevent negative points
        player.InGameMoneyServices!.Account -= points; // Take money (points) from player. We using money as call in points
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
    }
    public int GetPlayerPoints(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return 0;
        if (!PlayerStatuses.ContainsKey(player)) return 0;
        return PlayerStatuses[player].TotalPoints;
    }
    public int GetPlayerCallInPoints(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return 0;
        if (!PlayerStatuses.ContainsKey(player)) return 0;
        return PlayerStatuses[player].TotalCallInPoints;
    }

    public int GetPlayerSquadPoints(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return 0;
        if (!PlayerStatuses.ContainsKey(player)) return 0;
        var squad = PlayerStatuses[player].Squad;
        if (squad == null) return 0;
        return squad.TotalPoints;
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