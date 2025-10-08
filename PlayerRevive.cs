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

// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619

namespace SLAYER_CaptureTheFlag;

public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    public List<PlayerRevive> AvailableRevives { get; set; } = new List<PlayerRevive>();
    public enum ReviverType
    {
        SquadMember,
        Medic
    }
    public enum ReviveStatus
    {
        NotReviving,
        Reviving
    }

    public class PlayerRevive
    {
        public CCSPlayerController? player { get; set; } = null; // Player who is being revived
        public CCSPlayerController? reviver { get; set; } = null; // Player who is reviving
        public int PlayerTeamNum { get; set; } = 0; // Team number of the player being revived
        public ReviverType? ReviverType { get; set; } = null; // Type of the player who is reviving
        public float reviveTime { get; set; } = 0; // Time when the player was started to be revived
        public float reviveDuration { get; set; } = 4f; // Duration of the revive process
        public float reviveRequestCooldown { get; set; } = 0; // Cooldown for the revive request
        public ReviveStatus? status { get; set; } = ReviveStatus.NotReviving; // Current revive status
        public List<CBeam>? beaconBeams { get; set; } = null; // List of beacon beams drawn for the revive
    }
    private ReviverType? IsValidForRevive(CCSPlayerController? player, CCSPlayerController? reviver)
    {
        if (player == null || !player.IsValid || reviver == null || !reviver.IsValid || player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE || GetPlayerReviveEntry(player) == null || player.TeamNum != reviver.TeamNum || IsPlayerGettingRevived(player)) return null;

        if (IsPlayerBehind(reviver, player)) return null; // Reviver is behind the player being revived, not valid

        var reviverType = GetPlayerClassType(reviver);
        if (reviverType == PlayerClassType.Medic) return ReviverType.Medic; // Reviver is a Medic

        var squadMembers = GetPlayerSquad(reviver).Members;
        if (squadMembers != null && squadMembers.Any())
        {
            if (squadMembers.ContainsKey(player)) return ReviverType.SquadMember; // Reviver is a squad member
        }

        return null;
    }
    private void SetPlayerReviveEntry(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;

        var reviveEntry = GetPlayerReviveEntry(player);
        if (reviveEntry == null)
        {
            reviveEntry = new PlayerRevive
            {
                player = player,
                PlayerTeamNum = player.TeamNum,
            };
            AvailableRevives.Add(reviveEntry);
        }
    }
    private void RemovePlayerReviveEntry(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;

        // Use RemoveAll to safely remove entries
        StartShooting(player); // Allow the player to shoot again
        AvailableRevives.RemoveAll(revive => revive.player != null && revive.player.IsValid && revive.player == player);
    }
    private PlayerRevive GetPlayerReviveEntry(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return null;

        var reviveEntry = AvailableRevives.FirstOrDefault(revive => revive.player != null && revive.player.IsValid && revive.player == player);
        return reviveEntry;
    }
    private CCSPlayerController GetPlayerBeingRevivedByReviver(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || !IsPlayerReviving(player)) return null;

        var playerBeingRevived = AvailableRevives.FirstOrDefault(revive => revive.reviver != null && revive.reviver.IsValid && revive.reviver == player).player;
        return playerBeingRevived;
    }
    private void StartReviving(CCSPlayerController? player, CCSPlayerController? reviver, ReviverType? reviveType)
    {
        player.PrintToChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.GettingRevived", reviver.PlayerName]}"); // Send message to who is currently getting revived by his teammate

        var reviveEntry = GetPlayerReviveEntry(player);
        reviveEntry.reviver = reviver;
        reviveEntry.ReviverType = reviveType;
        reviveEntry.reviveTime = Server.CurrentTime;
        reviveEntry.reviveDuration = reviveType == ReviverType.Medic ? Config.MedicReviveTime : Config.SquadmateReviveTime;
        reviveEntry.status = ReviveStatus.Reviving;

        StopShootingForSpecificTime(reviver); // Stop the reviver from shooting while reviving
        reviveEntry.beaconBeams = DrawBeaconCircle(DeadPlayersPosition[player].Item1, 70, 15, Color.Aqua, 0.5f);
    }
    private void UpdateReviveStatus()
    {
        // Create a copy of the list to iterate over to avoid collection modification issues
        var reviveEntriesToProcess = AvailableRevives.ToList();

        foreach (var reviveEntry in reviveEntriesToProcess)
        {
            // Skip if the entry was already removed from the original list
            if (!AvailableRevives.Contains(reviveEntry))
                continue;

            if (reviveEntry.status == ReviveStatus.Reviving)
            {
                // If the Reviver is no longer valid or alive, abort the reviving process
                if (reviveEntry.reviver == null || !reviveEntry.reviver.IsValid || reviveEntry.reviver.Connected != PlayerConnectedState.PlayerConnected || reviveEntry.reviver.TeamNum != reviveEntry.player.TeamNum || reviveEntry.reviver.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                {
                    AbortReviving(reviveEntry.player);
                }
                // If the player being revived is already alive or not in the same team, remove the revive entry
                else if (reviveEntry.player != null && reviveEntry.player.IsValid && reviveEntry.player.TeamNum != reviveEntry.PlayerTeamNum || reviveEntry.player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                {
                    reviveEntry.beaconBeams?.ForEach(beam => { if (beam != null && beam.IsValid) beam.Remove(); });
                    reviveEntry.beaconBeams = null;
                    if (reviveEntry.reviver != null) StartShooting(reviveEntry.reviver); // Allow the reviver to shoot again
                    UnFreezePlayer(reviveEntry.reviver);
                    RemovePlayerReviveEntry(reviveEntry.player);
                }
                else // Reviving is still valid
                {
                    // Ensure the reviver is still within the required distance and facing the player being revived
                    if (CalculateDistanceBetween(reviveEntry.player.PlayerPawn.Value.AbsOrigin, reviveEntry.reviver.PlayerPawn.Value.AbsOrigin) > 80 || IsPlayerBehind(reviveEntry.reviver, reviveEntry.player))
                    {
                        AbortReviving(reviveEntry.player);
                        continue; // Skip further processing for this entry
                    }
                    // Update the beacon beams to reflect the current progress
                    float elapsedTime = Server.CurrentTime - reviveEntry.reviveTime;
                    float progressPercent = Math.Min(100f, (elapsedTime / reviveEntry.reviveDuration) * 100f);
                    UpdateBeamsColor(reviveEntry.beaconBeams, progressPercent, Color.Aqua, Color.White);

                    // Check if the revive duration has passed
                    if (elapsedTime >= reviveEntry.reviveDuration)
                    {
                        Revivied(reviveEntry.player, DeadPlayersPosition[reviveEntry.player].Item1, DeadPlayersPosition[reviveEntry.player].Item2, reviveEntry.ReviverType == ReviverType.Medic ? Config.MedicReviveSpawnHealth : Config.SquadmateReviveSpawnHealth);
                    }
                }
            }
            else if (reviveEntry.status == ReviveStatus.NotReviving)
            {
                if (reviveEntry.reviveRequestCooldown > 0f) reviveEntry.reviveRequestCooldown -= 0.5f; // Decrease the cooldown for the revive request, if any cooldown remaining
            }
        }
    }
    private void AbortReviving(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;

        var reviveEntry = GetPlayerReviveEntry(player);
        if (reviveEntry == null) return;

        if (reviveEntry.reviver != null) StartShooting(reviveEntry.reviver); // Allow the reviver to shoot again
        UnFreezePlayer(reviveEntry.reviver);

        // Reset Reviving Status
        reviveEntry.reviver = null;
        reviveEntry.ReviverType = null;
        reviveEntry.reviveTime = 0;
        reviveEntry.reviveDuration = 0;
        reviveEntry.status = ReviveStatus.NotReviving;
        reviveEntry.beaconBeams?.ForEach(beam => { if (beam != null && beam.IsValid) beam.Remove(); });
        reviveEntry.beaconBeams = null;
    }
    private void Revivied(CCSPlayerController? player, Vector SpawnPosition, QAngle SpawnAngles, int SpawnHealth)
    {
        if (player == null || !player.IsValid) return;

        if (PlayersRedeployTimer.ContainsKey(player) && PlayersRedeployTimer[player].Item1 != null) // If there is an active redeploy timer, remove it
        {
            PlayersRedeployTimer[player].Item1.Kill();
            PlayersRedeployTimer.Remove(player);
        }

        var reviveEntry = GetPlayerReviveEntry(player);
        if (reviveEntry == null) return;

        if (reviveEntry.reviver != null)
        {
            GivePlayerPoints(reviveEntry.reviver, reviveEntry.ReviverType == ReviverType.Medic ? Config.PlayerPoints.MedicRevivePoints : Config.PlayerPoints.SquadRevivePoints); // Give points to the reviver
            StartShooting(reviveEntry.reviver); // Allow the reviver to shoot again
            var squad = GetPlayerSquad(reviveEntry.reviver);
            if (squad != null) squad.TotalRevives += 1; // Increase squad total revives by 1
            if (PlayerStatuses.ContainsKey(reviveEntry.reviver)) PlayerStatuses[reviveEntry.reviver].TotalRevives += 1; // Increase total revives by 1
            UnFreezePlayer(reviveEntry.reviver);
        }

        reviveEntry.beaconBeams?.ForEach(beam => { if (beam != null && beam.IsValid) beam.Remove(); });
        reviveEntry.beaconBeams = null;

        RemovePlayerReviveEntry(player);

        player.Respawn();    // Respawn him after he get revivied
        AddTimer(0.05f, () => // Delay timer which will teleport the player to their death position
        {
            if (player != null && player.IsValid && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE)
            {
                player.PlayerPawn.Value!.Health = SpawnHealth; // give specific Health after getting revived
                player.PlayerPawn.Value.Teleport(SpawnPosition, SpawnAngles);
            }
        }, TimerFlags.STOP_ON_MAPCHANGE);
    }
    private bool IsPlayerReviving(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE) return false;

        var isReviving = AvailableRevives.Any(revive => revive.player != null && revive.player.IsValid && revive.reviver == player && revive.status == ReviveStatus.Reviving);
        return isReviving;
    }
    private bool IsPlayerGettingRevived(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return false;

        var isGettingRevived = AvailableRevives.Any(revive => revive.player != null && revive.player.IsValid && revive.player == player && revive.status == ReviveStatus.Reviving);
        return isGettingRevived;
    }
    private void RequestRevive(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE) return;

        if (IsPlayerGettingRevived(player)) return; // If the player is already being revived, do nothing

        var reviveEntry = GetPlayerReviveEntry(player);
        if (reviveEntry.reviveRequestCooldown <= 0f)
        {
            foreach (var medic in FindNearbyMedicsOrSquadmates(player).Where(m => m != null && m.IsValid && m.Connected == PlayerConnectedState.PlayerConnected && m.TeamNum == player.TeamNum && m.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE))
            {
                medic.PrintToChat($"{Localizer["Chat.Prefix"]} {ChatColors.Blue}{player.PlayerName} {ChatColors.Gold}is requesting a revive from {ChatColors.Lime}{(int)(CalculateDistanceBetween(medic.PlayerPawn.Value.AbsOrigin, player.PlayerPawn.Value.AbsOrigin) / 39.37f)} meters {ChatColors.Gold}away!"); // Notify the medic about the revive request

                // A little blink effect of glow
                RemoveGlowOnPlayerWhoRequestMedic(player);
                AddTimer(0.2f, () => { if (player != null && player.IsValid) SetGlowOnPlayerWhoRequestingMedic(player); });
                AddTimer(0.5f, () => { if (player != null && player.IsValid) RemoveGlowOnPlayerWhoRequestMedic(player); });
                AddTimer(0.7f, () => { if (player != null && player.IsValid) SetGlowOnPlayerWhoRequestingMedic(player); });
                AddTimer(1.0f, () => { if (player != null && player.IsValid) RemoveGlowOnPlayerWhoRequestMedic(player); });
                AddTimer(1.2f, () => { if (player != null && player.IsValid) SetGlowOnPlayerWhoRequestingMedic(player); });
                AddTimer(1.5f, () => { if (player != null && player.IsValid) RemoveGlowOnPlayerWhoRequestMedic(player); });
                AddTimer(1.7f, () => { if (player != null && player.IsValid) SetGlowOnPlayerWhoRequestingMedic(player); });
                reviveEntry.reviveRequestCooldown = 3f; // Set the cooldown for the revive request
            }
        }
    }
    private CCSPlayerController FindNearestTeammate(CCSPlayerController? player, float maxDistance = 100f, bool DeadOnly = false)
    {
        if (player == null || !player.IsValid || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return null;

        // Get the current player's position
        var currentPosition = player.PlayerPawn.Value.AbsOrigin;

        // Filter the dead players based on the criteria
        var nearestTeammate = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum > 1  && p != player && (DeadOnly ? p.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE : p.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE) && !IsPlayerGettingRevived(p)) // Not alive and not currently getting revived
            .Select(entry => new
            {
                Player = entry,
                Distance = CalculateDistanceBetween(currentPosition, entry.PlayerPawn.Value.AbsOrigin) // Use PlayerPawn's AbsOrigin for position
            })
            .Where(x => x.Distance <= maxDistance) // Distance check
            .OrderBy(x => x.Distance) // Order by distance
            .FirstOrDefault(); // Get the closest

        // Validate that the teammate is still in the game
        if (nearestTeammate?.Player == null || !nearestTeammate.Player.IsValid || nearestTeammate.Player.Connected != PlayerConnectedState.PlayerConnected || (DeadOnly ? nearestTeammate.Player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE : nearestTeammate.Player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE))
        {
            return null;
        }

        return nearestTeammate.Player; // Return the closest player, or null if none found
    }
    private List<CCSPlayerController> FindNearbyMedicsOrSquadmates(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
            return new List<CCSPlayerController>();

        var currentPosition = player.PlayerPawn.Value.AbsOrigin;

        // 40 meters = 40 * 39.37 = 1574.8 units (Source engine units)
        float maxDistance = 1600f;

        var nearbyMedics = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && !p.IsHLTV && p.TeamNum == player.TeamNum && p.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE && p != player &&
                (GetPlayerClassType(p) == PlayerClassType.Medic || // Check if the player is in a Medic class
                IsPlayerSquadmate(player, p) == true)) // Check if the player is in a squad member
            .Select(entry => new
            {
                Player = entry,
                Distance = CalculateDistanceBetween(currentPosition, entry.PlayerPawn.Value.AbsOrigin)
            })
            .Where(x => x.Distance <= maxDistance)
            .OrderBy(x => x.Distance)
            .Select(x => x.Player)
            .ToList();

        return nearbyMedics;
    }

    private void CheckReviveOnTick()
    {
        foreach (var player in Utilities.GetPlayers().Where(player => player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && player.TeamNum > 1 && player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE && !player.IsHLTV && !player.IsBot))
        {
            var buttons = player.Buttons;
            if ((buttons & PlayerButtons.Use) != 0) // Check, is player Pressed +use button on tick
            {
                if (!IsPlayerReviving(player)) // if he not reviving anyone rn
                {
                    var DeadTeammate = FindNearestTeammate(player, 70f, true);
                    if (DeadTeammate != null) // We found nearest reviveable teammate
                    {
                        var reviveType = IsValidForRevive(DeadTeammate, player);
                        if (reviveType != null) StartReviving(DeadTeammate, player, reviveType); // Start reviving Dead Teammate if valid
                    }
                }
                else // Already Reviving Someone
                {
                    var playerBeingRevived = GetPlayerBeingRevivedByReviver(player);
                    if (playerBeingRevived == null || !playerBeingRevived.IsValid || playerBeingRevived.Pawn.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE) continue;
                    var reviveEntry = GetPlayerReviveEntry(playerBeingRevived);
                    player.PrintToCenterHtml
                    (
                        $"{Localizer["CenterHtml.Reviving", playerBeingRevived.PlayerName]}" + "<br>" +
                        $"{GenerateLoadingText(Server.CurrentTime - reviveEntry.reviveTime, reviveEntry.reviveDuration)}"
                    );
                    FreezePlayer(player);
                    // Keep the player facing the same direction while reviving
                    UpdateReviveLookAt(player, playerBeingRevived);
                }
            }
            else // Not Pressing Use button
            {
                if (IsPlayerReviving(player)) // Was Reviving
                {
                    var playerBeingRevived = GetPlayerBeingRevivedByReviver(player);
                    if (playerBeingRevived != null) AbortReviving(playerBeingRevived);
                    UnFreezePlayer(player);
                }
            }
        }
    }
    /// <summary>
    /// Natural look-around with gentle pull back to target
    /// </summary>
    private void UpdateReviveLookAt(CCSPlayerController player, CCSPlayerController deadPlayer)
    {
        if (player == null || !player.IsValid || deadPlayer == null || !deadPlayer.IsValid || !DeadPlayersPosition.ContainsKey(deadPlayer)) return;

        var currentAngles = player.PlayerPawn.Value.EyeAngles;
        var playerEyePos = new Vector(player.PlayerPawn.Value.AbsOrigin.X, player.PlayerPawn.Value.AbsOrigin.Y, player.PlayerPawn.Value.AbsOrigin.Z + 64);
        var targetAngles = GetLookAtAngle(playerEyePos, DeadPlayersPosition[deadPlayer].Item1);
        
        // Calculate how far the player is looking away from target
        float pitchDiff = Math.Abs(NormalizeAngle(currentAngles.X - targetAngles.X));
        float yawDiff = Math.Abs(NormalizeAngle(currentAngles.Y - targetAngles.Y));
        
        // Maximum allowed deviation
        float maxDeviation = 50f; // degrees
        
        // Only apply correction if player is looking too far away
        if (pitchDiff > maxDeviation || yawDiff > maxDeviation)
        {
            // Gentle pull back towards target
            float pullStrength = 0.01f; // Very gentle pull
            var correctedAngles = LerpAngles(currentAngles, targetAngles, pullStrength);
            player.PlayerPawn.Value.Teleport(angles: correctedAngles);
        }
        // Otherwise, let the player look around freely within the range
    }

    /// <summary>
    /// Lerp between two QAngles with proper angle wrapping
    /// </summary>
    private QAngle LerpAngles(QAngle from, QAngle to, float t)
    {
        return new QAngle(
            LerpAngle(from.X, to.X, t),
            LerpAngle(from.Y, to.Y, t),
            LerpAngle(from.Z, to.Z, t)
        );
    }

    /// <summary>
    /// Lerp a single angle component with proper wrapping (-180 to 180)
    /// </summary>
    private float LerpAngle(float from, float to, float t)
    {
        // Normalize angles to -180 to 180 range
        from = NormalizeAngle(from);
        to = NormalizeAngle(to);
        
        // Calculate the shortest angular distance
        float diff = to - from;
        
        // Wrap around if the difference is greater than 180 degrees
        if (diff > 180f)
            diff -= 360f;
        else if (diff < -180f)
            diff += 360f;
        
        // Lerp and normalize result
        float result = from + (diff * t);
        return NormalizeAngle(result);
    }

}