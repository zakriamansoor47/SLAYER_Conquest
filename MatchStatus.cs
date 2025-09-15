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
using CS2TraceRay.Class;
using CS2TraceRay.Enum;
using CS2TraceRay.Struct;
using Vector3 = System.Numerics.Vector3;
using System.Text.RegularExpressions;

// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619

namespace SLAYER_CaptureTheFlag;

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    public MatchStatusInfo MatchStatus = new MatchStatusInfo();
    public enum MatchStatusType
    {
        Ongoing,
        TerroristWin,
        CounterTerroristWin
    }
    public class MatchStatusInfo
    {
        public MatchStatusType Status { get; set; } = MatchStatusType.Ongoing;
        public int TerroristTickets { get; set; } = 800;
        public int CounterTerroristTickets { get; set; } = 800;
        public float MatchStartTime { get; set; } = 0;
        public float MatchEndTime { get; set; } = 0f;
        public bool IsLowTicketsSoundPlaying { get; set; } = false;
        public List<uint> LowTicketsSoundEventGuid { get; set; } = new List<uint>();
        public List<CPhysicsPropMultiplayer> PlayersMatchEndCamera { get; set; } = new List<CPhysicsPropMultiplayer>();
        public Vector MatchEndCameraPosition { get; set; } = new Vector(0, 0, 0);
    }
    public float GetRemainingTeamTicketsPercentage(int team)
    {
        if (team == 2)
        {
            return (MatchStatus.TerroristTickets / (float)Config.TerroristTeamTickets) * 100f;
        }
        else if (team == 3)
        {
            return (MatchStatus.CounterTerroristTickets / (float)Config.CTerroristTeamTickets) * 100f;
        }
        return -1;
    }
    public void PlayerLowTicketsSound(int soundIndex)
    {
        if (soundIndex < 1 || soundIndex > 5) soundIndex = 5;
        MatchStatus.IsLowTicketsSoundPlaying = true;

        // Play the sound to all players
        foreach (var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsBot && !p.IsHLTV))
        {
            var soundEventGuid = p.EmitSound($"CTF.BF.MatchEndingMusic_{soundIndex}", new RecipientFilter { p }, Config.SoundsVolume);
            MatchStatus.LowTicketsSoundEventGuid.Add(soundEventGuid);
        }

    }
    public void StartMatch()
    {
        MatchStatus.Status = MatchStatusType.Ongoing;
        MatchStatus.MatchStartTime = Server.CurrentTime;
        MatchStatus.MatchEndTime = 0f;
        // Set Team Tickets
        MatchStatus.TerroristTickets = Config.TerroristTeamTickets;
        MatchStatus.CounterTerroristTickets = Config.CTerroristTeamTickets;
        // Set Team Win score to tickets (A clever way to show tickets in scoreboard)
        SetTeamScore(2, Config.TerroristTeamTickets);
        SetTeamScore(3, Config.CTerroristTeamTickets);

        MatchStatus.IsLowTicketsSoundPlaying = false;
        MatchStatus.LowTicketsSoundEventGuid.Clear();
        MatchStatus.PlayersMatchEndCamera.Clear();
        MatchStatus.MatchEndCameraPosition = MatchEndCameraPosition.Item1; // Save the end camera position for later use
    }
    public void EndMatch()
    {
        MatchStatus.Status = MatchStatus.TerroristTickets <= 0 ? MatchStatusType.CounterTerroristWin : MatchStatusType.TerroristWin;
        MatchStatus.MatchEndTime = Server.CurrentTime;
        string Winner = MatchStatus.Status == MatchStatusType.TerroristWin ? "Terrorists" : "Counter-Terrorists";

        foreach (var weapon in Utilities.FindAllEntitiesByDesignerName<CCSWeaponBaseGun>("weapon_").Where(w => w != null && w.IsValid))
        {
            weapon.Remove(); // Remove all weapons from the ground and players, so they don't interfere with the end match state
        }

        // Stop the lowTicket music if it's playing
        foreach (var soundEventGuid in MatchStatus.LowTicketsSoundEventGuid)
        {
            StopPlayingSound(soundEventGuid);
        }

        // Freeze all players and stop them from shooting
        var Manager = GetMenuManager();
        var recipientFilter = new RecipientFilter();
        foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV))
        {
            RemoveAllGlowOfPlayer(player); // Remove all glow effects from the player
            if (PlayersRedeployTimer != null && PlayersRedeployTimer.ContainsKey(player))
            {
                if (PlayersRedeployTimer[player].Item1 != null) PlayersRedeployTimer[player].Item1?.Kill();
                PlayersRedeployTimer.Remove(player);
            }
            // Close any open menu, freeze player
            if (Manager != null) Manager.CloseMenu(player);
            FreezePlayer(player);
            SetPlayerScale(player, 0.01f); // make player invisible

            if (!player.IsBot)
            {
                recipientFilter.Add(player);
                var cameraProp = CreateEndMatchCameraProp(player);
                if (cameraProp != null)
                {
                    MatchStatus.PlayersMatchEndCamera.Add(cameraProp);
                    // Play victory/defeat sound
                    if (MatchStatus.Status == MatchStatusType.TerroristWin && player.TeamNum == 2 ) cameraProp.EmitSound("CTF.BF.Victory", new RecipientFilter { player }, Config.SoundsVolume);
                    else if (MatchStatus.Status == MatchStatusType.CounterTerroristWin && player.TeamNum == 3) cameraProp.EmitSound("CTF.BF.Victory", new RecipientFilter { player }, Config.SoundsVolume);
                    else cameraProp.EmitSound("CTF.BF.Defeat", new RecipientFilter { player }, Config.SoundsVolume);
                }
            } 
        }

        // Announce the match result
        ClearAllCenterMessageLines(); // Clear any existing center message lines
        UpdateCenterMessageLine(1, $"<font class='fontSize-m' color='{(MatchStatus.Status == MatchStatusType.TerroristWin ? Config.TerroristTeamColor : Config.CTerroristTeamColor)}'>{Winner} Win</font>", recipientFilter, -1, true);
        var bestSquad = GetBestSquad();
        if (bestSquad != null)
        {
            var squadMembers = string.Join(", ", bestSquad.Members.Keys.Where(m => m != null && m.IsValid).Select(m => PlayerStatuses[m].DefaultName));
            UpdateCenterMessageLine(2, $"<font class='fontSize-m' color='lime'>Best Squad: {bestSquad.SquadName}</font>", recipientFilter, -1, true);
            UpdateCenterMessageLine(3, $"<font class='fontSize-m' color='silver'>Kills: {bestSquad.TotalKills}, Assists: {bestSquad.TotalAssists}, Revives: {bestSquad.TotaltRevives}</font>", recipientFilter, -1, true);
            UpdateCenterMessageLine(4, $"<font class='fontSize-m' color='gold'>{squadMembers}</font>", recipientFilter, -1, true);
        }
    }
    public void MatchStatusOnTick()
    {
        if (MatchStatus.Status == MatchStatusType.CounterTerroristWin || MatchStatus.Status == MatchStatusType.TerroristWin)
        {
            // We teleport the match end cameras to match end position
            MatchStatus.PlayersMatchEndCamera.ForEach(camera =>
            {
                if (camera != null && camera.IsValid) camera.Teleport(MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item2); // Teleport Camera prop to the match End player Pose Position
            });
            // A clever way to make the camera slowly move backwards (A zoom out animation, I am genius)
            if (CalculateDistanceBetween(MatchStatus.MatchEndCameraPosition, MatchEndCameraPosition.Item1) <= 100f) // If the camera is already far enough, don't move it anymore
            {
                var newpos = GetFrontPosition(MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item2, -0.1f); // Get a position slightly in back of the camera
                MatchEndCameraPosition.Item1 = newpos; // Update the camera position to the new position
            }
        }
    }
    public CPhysicsPropMultiplayer? CreateEndMatchCameraProp(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.Pawn.Value == null || player.IsBot || player.IsHLTV) return null;

        var _cameraProp = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
        if (_cameraProp == null || !_cameraProp.IsValid) return null;

        _cameraProp.AcceptInput("targetname", value: $"CTF_MatchEndCamera{player.PlayerPawn.Value.Index}");
        _cameraProp.DispatchSpawn();
        _cameraProp.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NEVER;
        _cameraProp.Collision.SolidFlags = 12;
        _cameraProp.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        _cameraProp.TakesDamage = false;
        _cameraProp.Render = Color.FromArgb(0, 255, 255, 255);

        player.PlayerPawn.Value!.CameraServices!.ViewEntity.Raw = _cameraProp.EntityHandle.Raw; // Set the player camera to the prop
        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");

        _cameraProp.Teleport(MatchEndCameraPosition.Item1, MatchEndCameraPosition.Item2); // Teleport Camera prop to the match End player Pose Position

        return _cameraProp;
    }
    public void SetTeamTickets(int team, int tickets)
    {
        if (team == 2)
        {
            MatchStatus.TerroristTickets = tickets;
        }
        else if (team == 3)
        {
            MatchStatus.CounterTerroristTickets = tickets;
        }
    }
    public int GetTeamTickets(int team)
    {
        if (team == 2)
        {
            return MatchStatus.TerroristTickets;
        }
        else if (team == 3)
        {
            return MatchStatus.CounterTerroristTickets;
        }
        return -1;
    }
    public int GetTotalTickets()
    {
        return MatchStatus.TerroristTickets + MatchStatus.CounterTerroristTickets;
    }
    public void DecreaseTeamTickets(int team, int tickets)
    {
        if (team == 2)
        {
            MatchStatus.TerroristTickets -= tickets;
        }
        else if (team == 3)
        {
            MatchStatus.CounterTerroristTickets -= tickets;
        }
    }
    public void IncreaseTeamTickets(int team, int tickets)
    {
        if (team == 2)
        {
            MatchStatus.TerroristTickets += tickets;
        }
        else if (team == 3)
        {
            MatchStatus.CounterTerroristTickets += tickets;
        }
    }
    public bool IsValidToDecreaeTeamTickets(int DyingPlayerteam)
    {
        var CapturedFlagsByT = GetFlagsCapturedBy(CsTeam.Terrorist);
        var CapturedFlagsByCT = GetFlagsCapturedBy(CsTeam.CounterTerrorist);
        if (DyingPlayerteam == 3) 
        {
            return CapturedFlagsByT > CapturedFlagsByCT;
        }
        else if (DyingPlayerteam == 2)
        {
            return CapturedFlagsByCT > CapturedFlagsByT;
        }
        else return false;
    }
    public void UpdateTicketCount(int teamNum)
    {
        if (teamNum < 2) return;

        if (IsValidToDecreaeTeamTickets(teamNum))
        {
            DecreaseTeamTickets(teamNum, 1);
            SetTeamScore(teamNum, -1, true);
        }
    }
}