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
        CounterTerroristWin,
        Draw
    }
    public class MatchStatusInfo
    {
        public MatchStatusType Status { get; set; } = MatchStatusType.Ongoing;
        public int TerroristTickets { get; set; } = 800;
        public int CounterTerroristTickets { get; set; } = 800;
        public float MatchStartTime { get; set; } = 0;
        public float MatchEndTime { get; set; } = 0f;
        public bool IsLowTicketsSoundPlaying { get; set; } = false;
        public int LowTicketsSoundEventGuid { get; set; } = -1;
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

        //get the world entity so we can emit global sounds
        var TempEntity = Utilities.CreateEntityByName<CBaseEntity>("prop_static");
        if (TempEntity == null || !TempEntity.IsValid) return;

        
        // Play the sound to all players
        var recipients = new RecipientFilter();
        foreach (var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsBot && !p.IsHLTV))
        {
            p.PrintToChat($"[CTF] Low tickets warning! Only {Math.Min(GetTeamTickets(2), GetTeamTickets(3))} tickets remaining!");
            recipients.Add(p);
        }

        if (MatchStatus.LowTicketsSoundEventGuid != -1) StopPlayingSounds(MatchStatus.LowTicketsSoundEventGuid, recipients);
        MatchStatus.LowTicketsSoundEventGuid = (int)TempEntity.EmitSound($"CTF.BF.MatchEndingMusic_{soundIndex}", recipients, Config.SoundsVolume);
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
    }
    public void EndMatch()
    {
        MatchStatus.Status = MatchStatus.TerroristTickets <= 0 ? MatchStatusType.CounterTerroristWin : MatchStatusType.TerroristWin;
        MatchStatus.MatchEndTime = Server.CurrentTime;

        // Stop the lowTicket music if it's playing
        if (MatchStatus.LowTicketsSoundEventGuid != -1) StopPlayingSounds(MatchStatus.LowTicketsSoundEventGuid);
        // Freeze all players and stop them from shooting
        PlayersRedeployTimer.Clear();
        foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsBot && !p.IsHLTV))
        {
            MenuManager.CloseMenu(player);
            FreezePlayer(player);
            StopShootingForSpecificTime(player);
            player.PrintToChat($"[CTF] Match ended! {(MatchStatus.Status == MatchStatusType.TerroristWin ? "Terrorists" : "Counter-Terrorists")} win!");
        }

        // Announce the match result

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